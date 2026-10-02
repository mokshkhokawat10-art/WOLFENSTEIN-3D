using System;
using UnityEngine;
using Wolfenstein.Combat;
using Wolfenstein.World;
using Random = UnityEngine.Random;

namespace Wolfenstein.Monsters
{
    /// <summary>
    /// The mind of a monster. It rises into the level, then hunts the nearest living target: melee monsters run it down
    /// and strike once they are close, ranged monsters keep their distance and fire volleys of projectiles, and some
    /// charge from mid range. Every attack has a wind-up the player can see and escape, and only lands if the target is
    /// still in reach and in sight when it ends, so nothing strikes through walls. A hit that does not kill may stop the
    /// monster briefly. Its <see cref="MonsterManager"/> drives it each frame and looks after it once it dies.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Health), typeof(MonsterMotor))]
    public sealed class MonsterController : MonoBehaviour
    {
        private static readonly RaycastHit[] SightHits = new RaycastHit[16];

        [SerializeField]
        private MonsterDefinition definition;

        [SerializeField, Tooltip("Where the monster looks from, at eye height.")]
        private Transform eyes;

        [SerializeField, Tooltip("Where its projectiles leave from. The eyes are used when this is empty.")]
        private Transform muzzle;

        [SerializeField, Tooltip("Layers that block the monster's view. Other monsters never do, nor do trigger colliders.")]
        private LayerMask sightMask = Physics.DefaultRaycastLayers;

        [SerializeField, Min(0.02f), Tooltip("Seconds between looks for the target. Monsters look at different moments, spreading the cost.")]
        private float thinkInterval = 0.15f;

        [SerializeField, Min(0.1f), Tooltip("Seconds between checks for a nearer target.")]
        private float retargetInterval = 1f;

        [SerializeField, Min(0.1f), Tooltip("Seconds a ranged monster circles one way before it may turn back.")]
        private float strafeSwitchInterval = 2.5f;

        private Health health;
        private MonsterMotor motor;
        private MonsterManager manager;
        private Collider targetBody;
        private MonsterModifiers modifiers = MonsterModifiers.None;
        private bool huntOnceSpawned;
        private float stateEndTime;
        private float nextThinkTime;
        private float nextRetargetTime;
        private float nextMeleeTime;
        private float nextRangedTime;
        private float nextChargeTime;
        private float nextStrafeSwitchTime;
        private int strafeSide = 1;
        private bool directRoute;

        public event Action<MonsterState> StateChanged;

        /// <summary>Raised when the monster begins winding up an attack, which gives the player warning.</summary>
        public event Action<MonsterAttack> AttackStarted;

        /// <summary>Raised when an attack lands or flies: for a blow, whether it hit; a volley always counts as fired.</summary>
        public event Action<MonsterAttack, bool> Attacked;

        /// <summary>Raised once when the monster is killed.</summary>
        public event Action<MonsterController> Died;

        public MonsterState State { get; private set; } = MonsterState.Inactive;
        public MonsterDefinition Definition => definition;
        public Health Health => health;
        public MonsterMotor Motor => motor;
        public MonsterModifiers Modifiers => modifiers;

        /// <summary>The target being hunted. Null while there is none.</summary>
        public Health Target { get; private set; }

        /// <summary>The attack being wound up or last made.</summary>
        public MonsterAttack CurrentAttack { get; private set; }

        /// <summary>Whether the target was in sight at the monster's latest look.</summary>
        public bool CanSeeTarget { get; private set; }

        /// <summary>Distance to the target across the floor at the latest look, in metres.</summary>
        public float DistanceToTarget { get; private set; }

        /// <summary>In the level and not yet dead.</summary>
        public bool IsAlive => State != MonsterState.Inactive && State != MonsterState.Dead;

        /// <summary>Whether it hunts as soon as it has risen, rather than lurking until it notices a target.</summary>
        public bool HuntsOnceSpawned => huntOnceSpawned;

        /// <summary>When it died, for how long its body has lain. Only meaningful once dead.</summary>
        public float TimeOfDeath { get; private set; }

        public float MoveSpeed => definition.MoveSpeed * modifiers.speed;

        /// <summary>The reach of its blows, measured from the monster's centre to the target's.</summary>
        public float MeleeRange => definition.MeleeReach + motor.Radius + TargetRadius;

        private float TurnSpeed => definition.TurnSpeed * Mathf.Max(1f, modifiers.speed);

        private float TargetRadius => targetBody is CharacterController controller ? controller.radius : 0.4f;

        private Vector3 TargetPoint => targetBody != null ? targetBody.bounds.center : Target.transform.position;

        private Vector3 EyePosition => eyes != null ? eyes.position : transform.position + Vector3.up * 1.5f;

        private void Awake()
        {
            health = GetComponent<Health>();
            motor = GetComponent<MonsterMotor>();
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
        /// Brings the monster into the level at full health, rising from the floor. Once risen it hunts at once,
        /// or lurks until it notices a target.
        /// </summary>
        public void Activate(MonsterManager monsterManager, WorldManager world, Vector3 position, float yaw,
            MonsterModifiers difficulty, bool hunt)
        {
            manager = monsterManager;
            modifiers = difficulty;
            huntOnceSpawned = hunt;
            motor.Initialise(world, monsterManager);
            motor.SetSolid(true);
            motor.Teleport(position, Quaternion.Euler(0f, yaw, 0f));
            health.Restore(modifiers.ScaleHealth(definition.MaxHealth));

            SetTarget(null);
            float now = Time.time;
            stateEndTime = now + definition.SpawnDuration;
            nextThinkTime = now + Random.Range(0f, thinkInterval);
            nextRetargetTime = now;
            nextMeleeTime = stateEndTime;
            nextRangedTime = stateEndTime + Random.Range(0.3f, 1f);
            nextChargeTime = stateEndTime + definition.ChargeCooldown * 0.5f;
            strafeSide = Random.value < 0.5f ? -1 : 1;
            CanSeeTarget = false;
            SetState(MonsterState.Spawning);
        }

        /// <summary>Takes the monster out of the level, back to its pool.</summary>
        public void Deactivate()
        {
            SetTarget(null);
            SetState(MonsterState.Inactive);
        }

        /// <summary>Makes a lurking monster hunt, as when it hears gunfire or is shot.</summary>
        public void Alert()
        {
            if (State == MonsterState.Idle)
                SetState(MonsterState.Chase);
            else if (State == MonsterState.Spawning)
                huntOnceSpawned = true;
        }

        /// <summary>Runs the monster for one frame. Called by its manager.</summary>
        public void Tick(float deltaTime)
        {
            if (!IsAlive)
                return;

            UpdateTarget();
            if (Target == null || Target.IsDead)
            {
                // With nobody left to fight, the monster stands where it is.
                motor.Stand(deltaTime);
                if (State is MonsterState.Attack or MonsterState.Charge)
                    SetState(MonsterState.Chase);
                return;
            }

            if (Time.time >= nextThinkTime)
                Look();

            switch (State)
            {
                case MonsterState.Spawning:
                    motor.Stand(deltaTime);
                    if (Time.time >= stateEndTime)
                        SetState(huntOnceSpawned ? MonsterState.Chase : MonsterState.Idle);
                    break;

                case MonsterState.Idle:
                    motor.Stand(deltaTime);
                    if (CanSeeTarget && DistanceToTarget <= definition.SightRange)
                        Alert();
                    break;

                case MonsterState.Chase:
                    UpdateChase(deltaTime);
                    break;

                case MonsterState.Attack:
                    UpdateAttack(deltaTime);
                    break;

                case MonsterState.Charge:
                    UpdateCharge(deltaTime);
                    break;

                case MonsterState.Pain:
                    motor.Stand(deltaTime);
                    if (Time.time >= stateEndTime)
                        SetState(MonsterState.Chase);
                    break;
            }
        }

        private void UpdateChase(float deltaTime)
        {
            float now = Time.time;
            float distance = DistanceToTarget;

            if (definition.HasMelee && CanSeeTarget && distance <= MeleeRange && now >= nextMeleeTime)
            {
                BeginAttack(MonsterAttack.Melee, definition.MeleeWindup);
                return;
            }

            bool tooCloseToShoot = definition.HasMelee && distance <= MeleeRange * 1.5f;
            if (definition.HasRanged && CanSeeTarget && !tooCloseToShoot && now >= nextRangedTime &&
                distance >= definition.MinFireRange && distance <= definition.MaxFireRange)
            {
                BeginAttack(MonsterAttack.Ranged, definition.FireWindup);
                return;
            }

            if (definition.CanCharge && CanSeeTarget && directRoute && now >= nextChargeTime &&
                distance >= definition.ChargeMinRange && distance <= definition.ChargeMaxRange)
            {
                stateEndTime = now + definition.ChargeDuration;
                nextChargeTime = now + definition.ChargeCooldown;
                SetState(MonsterState.Charge);
                return;
            }

            Vector3 targetPosition = Target.transform.position;
            if (definition.HasMelee)
            {
                // Close enough to strike: it waits face to face for its next blow rather than pushing into the target.
                if (CanSeeTarget && distance <= MeleeRange * 0.85f)
                {
                    motor.Stand(deltaTime);
                    motor.TurnTowards(targetPosition, TurnSpeed, deltaTime);
                }
                else
                {
                    motor.Chase(manager.GetCostField(Target), targetPosition, directRoute, MoveSpeed, TurnSpeed, deltaTime);
                }

                return;
            }

            KeepDistance(targetPosition, distance, deltaTime);
        }

        // A monster that only fights from range backs off when the target comes too close, circles while it is at a good
        // distance, and closes in when the target is far away or out of sight.
        private void KeepDistance(Vector3 targetPosition, float distance, float deltaTime)
        {
            float preferred = definition.PreferredDistance;
            if (!CanSeeTarget || distance > preferred + 3f)
            {
                motor.Chase(manager.GetCostField(Target), targetPosition, directRoute, MoveSpeed, TurnSpeed, deltaTime);
                return;
            }

            if (distance < preferred - 2f && motor.Retreat(targetPosition, MoveSpeed, TurnSpeed, deltaTime))
                return;

            if (Time.time >= nextStrafeSwitchTime && Random.value < 0.35f)
                strafeSide = -strafeSide;
            nextStrafeSwitchTime = Mathf.Max(nextStrafeSwitchTime, Time.time + strafeSwitchInterval);

            if (!motor.Strafe(targetPosition, strafeSide, MoveSpeed * 0.7f, TurnSpeed, deltaTime))
            {
                strafeSide = -strafeSide;
                motor.Stand(deltaTime);
                motor.TurnTowards(targetPosition, TurnSpeed, deltaTime);
            }
        }

        private void BeginAttack(MonsterAttack attack, float windup)
        {
            CurrentAttack = attack;
            stateEndTime = Time.time + windup;
            SetState(MonsterState.Attack);
            AttackStarted?.Invoke(attack);
        }

        private void UpdateAttack(float deltaTime)
        {
            motor.Stand(deltaTime);
            motor.TurnTowards(Target.transform.position, TurnSpeed, deltaTime);

            // A volley needs the target in sight all the way through the wind-up; losing it calls the shot off.
            if (CurrentAttack == MonsterAttack.Ranged && !CanSeeTarget)
            {
                nextRangedTime = Time.time + 0.3f;
                SetState(MonsterState.Chase);
                return;
            }

            if (Time.time < stateEndTime)
                return;

            if (CurrentAttack == MonsterAttack.Melee)
                StrikeMelee();
            else
                FireVolley();

            if (State == MonsterState.Attack)
                SetState(MonsterState.Chase);
        }

        private void UpdateCharge(float deltaTime)
        {
            motor.Rush(Target.transform.position, MoveSpeed * definition.ChargeSpeedMultiplier, TurnSpeed, deltaTime);

            if (definition.HasMelee && DistanceToTarget <= MeleeRange && CanSeeTarget)
            {
                // A charge ends in a quicker blow than usual.
                BeginAttack(MonsterAttack.Melee, definition.MeleeWindup * 0.5f);
                return;
            }

            if (Time.time >= stateEndTime || motor.StuckTime > 0f)
                SetState(MonsterState.Chase);
        }

        // The blow lands only on a target still within reach, in front of the monster and in sight. A blow with a
        // shockwave also catches a target anywhere close to where it strikes.
        private void StrikeMelee()
        {
            nextMeleeTime = Time.time + definition.MeleeCooldown;
            Look();

            Vector3 toTarget = Vector3.ProjectOnPlane(Target.transform.position - transform.position, Vector3.up);
            bool inReach = DistanceToTarget <= MeleeRange + 0.4f && Vector3.Angle(transform.forward, toTarget) <= 70f;
            if (definition.MeleeSplashRadius > 0f)
            {
                Vector3 impact = transform.position + transform.forward * (definition.MeleeReach * 0.7f + motor.Radius);
                manager.ShowSlam(impact, definition.MeleeSplashRadius);
                inReach |= Vector3.ProjectOnPlane(Target.transform.position - impact, Vector3.up).magnitude <=
                           definition.MeleeSplashRadius + TargetRadius;
            }

            bool hit = inReach && CanSeeTarget;
            if (hit)
            {
                Vector3 direction = toTarget.sqrMagnitude > 0.0001f ? toTarget.normalized : transform.forward;
                Target.TakeDamage(new DamageInfo(modifiers.ScaleDamage(definition.MeleeDamage), TargetPoint, direction, gameObject));
            }

            Attacked?.Invoke(MonsterAttack.Melee, hit);
        }

        // Aims at the middle of the target, fanning a volley out evenly either side of the aim.
        private void FireVolley()
        {
            nextRangedTime = Time.time + definition.FireCooldown * Random.Range(0.85f, 1.15f);
            Vector3 origin = muzzle != null ? muzzle.position : EyePosition;
            Vector3 aim = (TargetPoint - origin).normalized;
            int count = definition.ProjectilesPerVolley;
            int damage = modifiers.ScaleDamage(definition.ProjectileDamage);

            for (int i = 0; i < count; i++)
            {
                float angle = (i - (count - 1) * 0.5f) * definition.VolleySpread;
                Vector3 direction = Quaternion.AngleAxis(angle, Vector3.up) * aim;
                manager.FireProjectile(definition.Projectile, origin, direction * definition.ProjectileSpeed, damage, this);
            }

            Attacked?.Invoke(MonsterAttack.Ranged, true);
        }

        private void UpdateTarget()
        {
            if (Target != null && !Target.IsDead && Time.time < nextRetargetTime)
                return;

            nextRetargetTime = Time.time + retargetInterval;
            SetTarget(manager.FindNearestTarget(transform.position));
        }

        private void SetTarget(Health newTarget)
        {
            Target = newTarget;
            targetBody = newTarget != null ? newTarget.GetComponent<Collider>() : null;
        }

        // Looks for the target: how far away it is, whether it is in sight, and whether the way to it is clear.
        private void Look()
        {
            nextThinkTime = Time.time + thinkInterval;
            if (Target == null)
                return;

            DistanceToTarget = Vector3.ProjectOnPlane(Target.transform.position - transform.position, Vector3.up).magnitude;
            CanSeeTarget = DistanceToTarget <= Mathf.Max(definition.SightRange, definition.MaxFireRange) + 10f && HasLineOfSight();
            directRoute = CanSeeTarget && motor.HasClearRunTo(Target.transform.position);
        }

        // The nearest thing in the way that is not a monster must be the target. The ray starts inside the monster's own
        // collider, which a raycast never reports.
        private bool HasLineOfSight()
        {
            Vector3 origin = EyePosition;
            Vector3 toTarget = TargetPoint - origin;
            float distance = toTarget.magnitude;
            if (distance < 0.01f)
                return true;

            int count = Physics.RaycastNonAlloc(origin, toTarget / distance, SightHits, distance + 0.1f, sightMask, QueryTriggerInteraction.Ignore);
            float nearest = float.PositiveInfinity;
            Collider blocker = null;
            for (int i = 0; i < count; i++)
            {
                if (SightHits[i].distance < nearest && !manager.IsMonsterCollider(SightHits[i].collider))
                {
                    nearest = SightHits[i].distance;
                    blocker = SightHits[i].collider;
                }
            }

            return blocker != null && blocker.transform.IsChildOf(Target.transform);
        }

        private void HandleDamaged(DamageInfo damage)
        {
            if (health.IsDead)
                return;

            Alert();
            if (State is MonsterState.Spawning or MonsterState.Charge || Random.value >= definition.PainChance)
                return;

            stateEndTime = Time.time + definition.PainDuration;
            SetState(MonsterState.Pain);
        }

        // The body stays where it fell for a while, but no longer blocks shots, movement or doors.
        private void HandleDied()
        {
            motor.SetSolid(false);
            TimeOfDeath = Time.time;
            SetState(MonsterState.Dead);
            Died?.Invoke(this);
        }

        private void SetState(MonsterState newState)
        {
            if (newState == State)
                return;

            State = newState;
            StateChanged?.Invoke(newState);
        }
    }
}
