using System;
using UnityEngine;
using Wolfenstein.Combat;
using Wolfenstein.World;
using Random = UnityEngine.Random;

namespace Wolfenstein.Enemies
{
    /// <summary>
    /// The mind of an enemy soldier. It stands guard until it sees the player in front of it, hears their gunfire
    /// or is shot. From then on it hunts the player down, opening doors on the way. Whenever it can see the player
    /// within range and is ready, it stops, aims and fires a hitscan shot that is less likely to hit, and does less
    /// damage, the further away the player is. A hit that does not kill it stops it briefly.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Health), typeof(EnemyMotor))]
    public sealed class EnemyController : MonoBehaviour
    {
        [SerializeField]
        private EnemyDefinition definition;

        [SerializeField, Tooltip("Where the enemy sees and shoots from, at eye height.")]
        private Transform eyes;

        [SerializeField, Tooltip("Layers that block the enemy's view of the player. Trigger colliders, such as doorways, never do.")]
        private LayerMask sightMask = Physics.DefaultRaycastLayers;

        private Health health;
        private EnemyMotor motor;
        private CharacterController body;
        private Health target;
        private Collider targetBody;
        private float stateEndTime;
        private float nextAttackTime;

        public event Action<EnemyState> StateChanged;

        /// <summary>Raised after every shot, with whether it hit the player.</summary>
        public event Action<bool> Fired;

        public EnemyState State { get; private set; } = EnemyState.Idle;
        public EnemyDefinition Definition => definition;
        public Health Health => health;
        public EnemyMotor Motor => motor;

        private void Awake()
        {
            health = GetComponent<Health>();
            motor = GetComponent<EnemyMotor>();
            body = GetComponent<CharacterController>();
        }

        private void OnEnable()
        {
            health.Damaged += HandleDamaged;
            health.Died += HandleDied;
        }

        private void OnDisable()
        {
            health.Damaged -= HandleDamaged;
            health.Died -= HandleDied;
        }

        /// <summary>
        /// Places the enemy in a built level and sets the player it hunts, who must have a collider.
        /// It stays idle until this is called.
        /// </summary>
        public void Initialise(WorldManager world, Health player)
        {
            target = player;
            targetBody = player.GetComponent<Collider>();
            motor.Initialise(world, definition);
        }

        /// <summary>
        /// Makes an idle enemy aware of the player, as when it hears a shot. It then hunts the player down.
        /// </summary>
        public void Alert()
        {
            if (State != EnemyState.Idle)
                return;

            nextAttackTime = Time.time + definition.ReactionTime;
            SetState(EnemyState.Chase);
        }

        /// <summary>
        /// Whether the player is within sight range with nothing solid in between, whichever way the enemy faces.
        /// </summary>
        public bool CanSeeTarget()
        {
            Vector3 toTarget = TargetPoint - eyes.position;
            return toTarget.magnitude <= definition.SightRange && HasLineOfSight(toTarget);
        }

        private void Update()
        {
            if (target == null || State == EnemyState.Dead)
                return;

            float deltaTime = Time.deltaTime;

            // Once the player is dead the fight is over, and the enemy stands where it is.
            if (target.IsDead)
            {
                motor.Stand(deltaTime);
                return;
            }

            switch (State)
            {
                case EnemyState.Idle:
                    motor.Stand(deltaTime);
                    if (CanSpotTarget())
                        Alert();
                    break;

                case EnemyState.Chase:
                    UpdateChase(deltaTime);
                    break;

                case EnemyState.Attack:
                    UpdateAttack(deltaTime);
                    break;

                case EnemyState.Pain:
                    motor.Stand(deltaTime);
                    if (Time.time >= stateEndTime)
                        SetState(EnemyState.Chase);
                    break;
            }
        }

        private void UpdateChase(float deltaTime)
        {
            Vector3 targetPosition = target.transform.position;
            float distance = Vector3.ProjectOnPlane(targetPosition - transform.position, Vector3.up).magnitude;
            bool visible = CanSeeTarget();

            if (visible && distance <= definition.AttackRange && Time.time >= nextAttackTime)
            {
                stateEndTime = Time.time + definition.AimTime;
                SetState(EnemyState.Attack);
                UpdateAttack(deltaTime);
            }
            else if (visible && distance <= definition.StoppingDistance)
            {
                motor.Stand(deltaTime);
                motor.TurnTowards(targetPosition, deltaTime);
            }
            else
            {
                motor.MoveTowards(targetPosition, deltaTime);
            }
        }

        private void UpdateAttack(float deltaTime)
        {
            motor.Stand(deltaTime);
            motor.TurnTowards(target.transform.position, deltaTime);

            // Losing sight of the player while aiming calls the shot off. The next chance to see them is taken at once.
            if (!CanSeeTarget())
            {
                SetState(EnemyState.Chase);
                return;
            }

            if (Time.time < stateEndTime)
                return;

            Fire();
            nextAttackTime = Time.time + Random.Range(definition.MinAttackInterval, definition.MaxAttackInterval);
            SetState(EnemyState.Chase);
        }

        private void Fire()
        {
            Vector3 targetPoint = TargetPoint;
            Vector3 toTarget = targetPoint - eyes.position;
            float distance = toTarget.magnitude;
            bool hit = Random.value < definition.HitChanceAt(distance);

            if (hit)
                target.TakeDamage(new DamageInfo(definition.DamageAt(distance), targetPoint, toTarget / distance, gameObject));

            Fired?.Invoke(hit);
        }

        // An idle enemy only notices the player inside its field of view, unless the player is right beside it.
        private bool CanSpotTarget()
        {
            Vector3 toTarget = TargetPoint - eyes.position;
            float distance = toTarget.magnitude;
            if (distance > definition.SightRange)
                return false;

            bool inView = distance <= definition.CloseAwarenessRange ||
                          Vector3.Angle(transform.forward, Vector3.ProjectOnPlane(toTarget, Vector3.up)) <= definition.FieldOfView * 0.5f;
            return inView && HasLineOfSight(toTarget);
        }

        // The enemy looks and aims at the middle of the player's body.
        private Vector3 TargetPoint => targetBody.bounds.center;

        // The ray ends inside the player's collider, so an unobstructed ray always ends on the player.
        // It also starts inside the enemy's own collider, which a raycast never reports.
        private bool HasLineOfSight(Vector3 toTarget) =>
            Physics.Raycast(eyes.position, toTarget, out RaycastHit hit, toTarget.magnitude, sightMask, QueryTriggerInteraction.Ignore) &&
            hit.collider.transform.IsChildOf(target.transform);

        private void HandleDamaged(DamageInfo damage)
        {
            if (health.IsDead)
                return;

            Alert();
            stateEndTime = Time.time + definition.PainDuration;
            SetState(EnemyState.Pain);
        }

        // The body stays where it fell, but no longer blocks shots, movement or doors.
        private void HandleDied()
        {
            body.enabled = false;
            SetState(EnemyState.Dead);
        }

        private void SetState(EnemyState newState)
        {
            if (newState == State)
                return;

            State = newState;
            StateChanged?.Invoke(newState);
        }
    }
}
