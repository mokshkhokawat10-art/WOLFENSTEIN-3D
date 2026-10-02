using System.Collections.Generic;
using UnityEngine;
using Wolfenstein.World;

namespace Wolfenstein.Enemies
{
    /// <summary>
    /// Moves an enemy through a CharacterController. It follows routes across the tile grid from one tile centre to the
    /// next, opens any door on the route and waits for it, and turns the body toward where it is going.
    /// It has no Update of its own; <see cref="EnemyController"/> drives it once per frame.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(CharacterController))]
    public sealed class EnemyMotor : MonoBehaviour
    {
        [SerializeField, Min(0.01f), Tooltip("How close to a tile's centre counts as having reached it, in metres.")]
        private float waypointTolerance = 0.1f;

        [SerializeField, Min(0.05f), Tooltip("Seconds between route updates while the destination keeps moving.")]
        private float repathInterval = 0.5f;

        [SerializeField, Min(0f), Tooltip("Small downward speed that keeps the enemy on the floor.")]
        private float groundStickSpeed = 2f;

        private readonly List<Vector2Int> route = new List<Vector2Int>();
        private CharacterController body;
        private WorldManager world;
        private EnemyDefinition definition;
        private int routeIndex;
        private Vector2Int routeGoal;
        private float nextRepathTime;

        /// <summary>How fast the enemy is moving across the floor.</summary>
        public Vector3 Velocity { get; private set; }

        /// <summary>The closed door the enemy is standing at, waiting for it to open. Null when there is none.</summary>
        public Door WaitingForDoor { get; private set; }

        private void Awake() => body = GetComponent<CharacterController>();

        public void Initialise(WorldManager levelWorld, EnemyDefinition enemyDefinition)
        {
            world = levelWorld;
            definition = enemyDefinition;
            route.Clear();
        }

        /// <summary>
        /// Walks one frame along the route to a destination, planning the route again whenever the destination
        /// moves to another tile and at regular intervals.
        /// </summary>
        public void MoveTowards(Vector3 destination, float deltaTime)
        {
            WaitingForDoor = null;
            Vector2Int goal = world.WorldToCell(destination);
            if (goal != routeGoal || routeIndex >= route.Count || Time.time >= nextRepathTime)
                PlanRoute(goal);

            while (routeIndex < route.Count && HorizontalOffsetTo(route[routeIndex]).magnitude <= waypointTolerance)
                routeIndex++;

            if (routeIndex >= route.Count)
            {
                Stand(deltaTime);
                return;
            }

            Vector2Int nextCell = route[routeIndex];
            if (IsBlockedByDoor(nextCell))
            {
                TurnTowards(world.CellToWorld(nextCell), deltaTime);
                Stand(deltaTime);
                return;
            }

            Vector3 offset = HorizontalOffsetTo(nextCell);
            Vector3 step = Vector3.ClampMagnitude(offset, definition.ChaseSpeed * deltaTime);
            TurnTowards(transform.position + offset, deltaTime);
            Move(step, deltaTime);
        }

        /// <summary>Stays in place for a frame.</summary>
        public void Stand(float deltaTime) => Move(Vector3.zero, deltaTime);

        /// <summary>Turns toward a point for a frame, around the vertical axis only.</summary>
        public void TurnTowards(Vector3 point, float deltaTime)
        {
            Vector3 direction = Vector3.ProjectOnPlane(point - transform.position, Vector3.up);
            if (direction.sqrMagnitude < 0.0001f)
                return;

            transform.rotation = Quaternion.RotateTowards(
                transform.rotation, Quaternion.LookRotation(direction), definition.TurnSpeed * deltaTime);
        }

        private void PlanRoute(Vector2Int goal)
        {
            routeGoal = goal;
            routeIndex = 0;
            nextRepathTime = Time.time + repathInterval;
            world.Navigator.TryFindPath(world.WorldToCell(transform.position), goal, route);
        }

        // A door on the route is opened, and the enemy waits outside it until it is fully open.
        // Once in the doorway it keeps going: the door cannot close on it and reopens if it tries.
        private bool IsBlockedByDoor(Vector2Int cell)
        {
            if (!world.TryGetDoorAt(cell, out Door door) || door.State == DoorState.Open)
                return false;

            door.Open();
            if (world.WorldToCell(transform.position) == cell)
                return false;

            WaitingForDoor = door;
            return true;
        }

        private Vector3 HorizontalOffsetTo(Vector2Int cell) =>
            Vector3.ProjectOnPlane(world.CellToWorld(cell) - transform.position, Vector3.up);

        private void Move(Vector3 step, float deltaTime)
        {
            if (!body.enabled || deltaTime <= 0f)
            {
                Velocity = Vector3.zero;
                return;
            }

            body.Move(step + Vector3.down * (groundStickSpeed * deltaTime));
            Velocity = Vector3.ProjectOnPlane(body.velocity, Vector3.up);
        }
    }
}
