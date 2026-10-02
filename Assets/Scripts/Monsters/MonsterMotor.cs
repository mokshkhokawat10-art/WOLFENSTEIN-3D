using UnityEngine;
using Wolfenstein.World;

namespace Wolfenstein.Monsters
{
    /// <summary>
    /// Moves a monster through a CharacterController. Out of sight of its target it follows the shared cost field the
    /// <see cref="MonsterManager"/> keeps toward that target, opening doors on the way; with a clear run at the target it
    /// heads straight for it. It keeps a little apart from other monsters so crowds spread out rather than pile up,
    /// and when it makes no headway it sidesteps until it is free.
    /// It has no Update of its own; its <see cref="MonsterController"/> drives it.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(CharacterController))]
    public sealed class MonsterMotor : MonoBehaviour
    {
        [SerializeField, Min(0.1f), Tooltip("How quickly the monster reaches its running speed, in metres per second squared.")]
        private float acceleration = 30f;

        [SerializeField, Min(0f), Tooltip("How strongly the monster keeps clear of others, as a share of its speed.")]
        private float separationWeight = 0.8f;

        [SerializeField, Min(0f), Tooltip("Small downward speed that keeps the monster on the floor.")]
        private float groundStickSpeed = 2f;

        [Header("Getting Unstuck")]
        [SerializeField, Min(0.1f), Tooltip("Seconds between checks on how far the monster has got.")]
        private float progressInterval = 0.5f;

        [SerializeField, Min(0f), Tooltip("Moving less than this between checks while trying to move counts as stuck, in metres.")]
        private float minimumProgress = 0.15f;

        [SerializeField, Min(0.1f), Tooltip("Seconds stuck before the monster sidesteps.")]
        private float stuckBeforeSidestep = 1f;

        [SerializeField, Min(0.1f), Tooltip("Seconds each sidestep lasts.")]
        private float sidestepDuration = 0.6f;

        private CharacterController body;
        private WorldManager world;
        private MonsterManager manager;
        private Vector3 velocity;
        private float wantedSpeed;
        private Vector3 lastProgressPosition;
        private float nextProgressCheck;
        private Vector3 sidestep;
        private float sidestepEndTime;

        /// <summary>How fast the monster is moving across the floor.</summary>
        public Vector3 Velocity { get; private set; }

        /// <summary>How long the monster has been trying to move without getting anywhere, in seconds.</summary>
        public float StuckTime { get; private set; }

        /// <summary>The closed door the monster is waiting at. Null when there is none.</summary>
        public Door WaitingForDoor { get; private set; }

        public float Radius => body.radius;

        private void Awake() => body = GetComponent<CharacterController>();

        public void Initialise(WorldManager levelWorld, MonsterManager monsterManager)
        {
            world = levelWorld;
            manager = monsterManager;
        }

        /// <summary>Puts the monster somewhere at once, with no movement in between.</summary>
        public void Teleport(Vector3 position, Quaternion rotation)
        {
            bool wasEnabled = body.enabled;
            body.enabled = false;
            transform.SetPositionAndRotation(position, rotation);
            body.enabled = wasEnabled;

            velocity = Vector3.zero;
            Velocity = Vector3.zero;
            StuckTime = 0f;
            sidestepEndTime = 0f;
            lastProgressPosition = position;
            nextProgressCheck = Time.time + progressInterval;
        }

        public void SetSolid(bool solid) => body.enabled = solid;

        /// <summary>
        /// Runs one frame toward a target: straight at it when the way is known to be clear, otherwise along the
        /// cost field toward the target's cell.
        /// </summary>
        public void Chase(float[] costField, Vector3 targetPosition, bool directRoute, float speed, float turnSpeed, float deltaTime)
        {
            WaitingForDoor = null;
            Vector3 heading;
            if (directRoute)
            {
                heading = Flat(targetPosition - transform.position);
            }
            else if (!TryFollowField(costField, out heading, turnSpeed, deltaTime))
            {
                Stand(deltaTime);
                return;
            }

            Steer(heading, speed, turnSpeed, deltaTime);
        }

        /// <summary>Runs one frame straight at a point, as in a charge.</summary>
        public void Rush(Vector3 targetPosition, float speed, float turnSpeed, float deltaTime)
        {
            WaitingForDoor = null;
            Steer(Flat(targetPosition - transform.position), speed, turnSpeed, deltaTime);
        }

        /// <summary>
        /// Backs away one frame from a threat, toward whichever neighbouring tile takes it furthest away.
        /// Returns false when there is nowhere further to go.
        /// </summary>
        public bool Retreat(Vector3 threat, float speed, float turnSpeed, float deltaTime)
        {
            WaitingForDoor = null;
            Vector2Int cell = world.WorldToCell(transform.position);
            float bestDistance = Flat(transform.position - threat).magnitude + 0.5f;
            Vector3 bestHeading = Vector3.zero;

            for (int dx = -1; dx <= 1; dx++)
            {
                for (int dy = -1; dy <= 1; dy++)
                {
                    var next = new Vector2Int(cell.x + dx, cell.y + dy);
                    if ((dx == 0 && dy == 0) || !IsOpenFloor(next) || (dx != 0 && dy != 0 &&
                        (!IsOpenFloor(new Vector2Int(cell.x + dx, cell.y)) || !IsOpenFloor(new Vector2Int(cell.x, cell.y + dy)))))
                        continue;

                    Vector3 centre = world.CellToWorld(next);
                    float distance = Flat(centre - threat).magnitude;
                    if (distance > bestDistance)
                    {
                        bestDistance = distance;
                        bestHeading = Flat(centre - transform.position);
                    }
                }
            }

            if (bestHeading == Vector3.zero)
                return false;

            Steer(bestHeading, speed, turnSpeed, deltaTime, face: false);
            TurnTowards(threat, turnSpeed, deltaTime);
            return true;
        }

        /// <summary>
        /// Circles one frame sideways around a point while facing it. Returns false when a wall is in the way on that side.
        /// </summary>
        public bool Strafe(Vector3 around, int side, float speed, float turnSpeed, float deltaTime)
        {
            WaitingForDoor = null;
            Vector3 toCentre = Flat(around - transform.position);
            Vector3 heading = Vector3.Cross(Vector3.up, toCentre).normalized * side;
            if (heading == Vector3.zero || !IsOpenFloor(world.WorldToCell(transform.position + heading * (Radius + 0.6f))))
                return false;

            Steer(heading, speed, turnSpeed, deltaTime, face: false);
            TurnTowards(around, turnSpeed, deltaTime);
            return true;
        }

        /// <summary>Stays in place for a frame, slowing to a stop.</summary>
        public void Stand(float deltaTime)
        {
            wantedSpeed = 0f;
            velocity = Vector3.MoveTowards(velocity, Vector3.zero, acceleration * deltaTime);
            Move(velocity * deltaTime, deltaTime);
        }

        /// <summary>Turns toward a point for a frame, around the vertical axis only.</summary>
        public void TurnTowards(Vector3 point, float turnSpeed, float deltaTime)
        {
            Vector3 direction = Flat(point - transform.position);
            if (direction.sqrMagnitude < 0.0001f)
                return;

            transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(direction), turnSpeed * deltaTime);
        }

        /// <summary>
        /// Whether a body as wide as this one could walk in a straight line to a point without leaving open floor or
        /// passing a closed door. It checks the tiles along the way, not the colliders in them.
        /// </summary>
        public bool HasClearRunTo(Vector3 point)
        {
            Vector3 start = transform.position;
            Vector3 offset = Flat(point - start);
            float length = offset.magnitude;
            if (length < 0.01f)
                return true;

            Vector3 side = Vector3.Cross(Vector3.up, offset / length) * (Radius * 0.9f);
            int samples = Mathf.CeilToInt(length / (world.TileSize * 0.25f));
            for (int i = 1; i <= samples; i++)
            {
                Vector3 along = start + offset * ((float)i / samples);
                if (!IsPassable(along) || !IsPassable(along + side) || !IsPassable(along - side))
                    return false;
            }

            return true;
        }

        // Follows the cost field from the current tile: toward the next tile's centre, opening any door that stands there.
        private bool TryFollowField(float[] costField, out Vector3 heading, float turnSpeed, float deltaTime)
        {
            heading = Vector3.zero;
            Vector2Int cell = world.WorldToCell(transform.position);
            if (costField == null || !world.Navigator.TryGetNextStep(cell, costField, out Vector2Int next))
                return false;

            if (world.TryGetDoorAt(next, out Door door) && door.State != DoorState.Open)
            {
                door.Open();
                WaitingForDoor = door;
                TurnTowards(world.CellToWorld(next), turnSpeed, deltaTime);
                return false;
            }

            // Two steps ahead when that tile can be reached in a straight line, which rounds off corners of the grid.
            Vector3 target = world.CellToWorld(next);
            if (world.Navigator.TryGetNextStep(next, costField, out Vector2Int afterNext) && !world.TryGetDoorAt(afterNext, out _) &&
                HasClearRunTo(world.CellToWorld(afterNext)))
                target = world.CellToWorld(afterNext);

            heading = Flat(target - transform.position);
            return true;
        }

        private void Steer(Vector3 heading, float speed, float turnSpeed, float deltaTime, bool face = true)
        {
            wantedSpeed = speed;
            Vector3 direction = heading.sqrMagnitude > 0.0001f ? heading.normalized : Vector3.zero;

            if (Time.time < sidestepEndTime)
                direction = (direction * 0.3f + sidestep).normalized;

            Vector3 desired = direction * speed + manager.GetSeparation(this) * (speed * separationWeight);
            desired = Vector3.ClampMagnitude(desired, speed);
            velocity = Vector3.MoveTowards(velocity, desired, acceleration * deltaTime);

            if (face && direction != Vector3.zero)
                TurnTowards(transform.position + direction, turnSpeed, deltaTime);

            Move(velocity * deltaTime, deltaTime);
            TrackProgress(direction);
        }

        // Every so often, checks whether the monster got anywhere. One that keeps failing to sidesteps for a moment.
        private void TrackProgress(Vector3 direction)
        {
            if (Time.time < nextProgressCheck)
                return;

            nextProgressCheck = Time.time + progressInterval;
            float moved = Flat(transform.position - lastProgressPosition).magnitude;
            lastProgressPosition = transform.position;

            if (wantedSpeed < 0.3f || moved >= minimumProgress)
            {
                StuckTime = 0f;
                return;
            }

            StuckTime += progressInterval;
            if (StuckTime >= stuckBeforeSidestep && Time.time >= sidestepEndTime)
            {
                float side = Random.value < 0.5f ? -1f : 1f;
                sidestep = (Vector3.Cross(Vector3.up, direction) * side - direction * 0.3f).normalized;
                if (sidestep == Vector3.zero)
                    sidestep = Random.insideUnitSphere;
                sidestep = Flat(sidestep).normalized;
                sidestepEndTime = Time.time + sidestepDuration;
            }
        }

        private void Move(Vector3 step, float deltaTime)
        {
            if (!body.enabled || deltaTime <= 0f)
            {
                Velocity = Vector3.zero;
                return;
            }

            body.Move(step + Vector3.down * (groundStickSpeed * deltaTime));
            Velocity = Flat(body.velocity);
        }

        private bool IsPassable(Vector3 point)
        {
            Vector2Int cell = world.WorldToCell(point);
            if (!world.Layout.IsWalkable(cell))
                return false;

            return !world.TryGetDoorAt(cell, out Door door) || door.State == DoorState.Open;
        }

        private bool IsOpenFloor(Vector2Int cell) => world.Layout.IsWalkable(cell) && !world.Layout.TryGetDoor(cell, out _);

        private static Vector3 Flat(Vector3 vector) => Vector3.ProjectOnPlane(vector, Vector3.up);
    }
}
