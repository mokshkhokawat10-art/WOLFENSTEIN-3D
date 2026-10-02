using UnityEngine;
using Wolfenstein.Audio;
using Wolfenstein.Pickups;

namespace Wolfenstein.Enemies
{
    /// <summary>
    /// Tuning for one kind of enemy: how it notices the player, how it moves, how it shoots and what killing it earns.
    /// Its hit points live on the enemy prefab's Health component.
    /// </summary>
    [CreateAssetMenu(fileName = "Enemy", menuName = "Wolfenstein/Enemy Definition")]
    public sealed class EnemyDefinition : ScriptableObject
    {
        [Header("Senses")]
        [SerializeField, Min(0f), Tooltip("How far away the enemy can see the player, in metres.")]
        private float sightRange = 40f;

        [SerializeField, Range(0f, 360f), Tooltip("Width of the view an idle enemy watches, in degrees. It cannot see the player outside it.")]
        private float fieldOfView = 180f;

        [SerializeField, Min(0f), Tooltip("Within this distance an idle enemy notices the player whichever way it faces, in metres.")]
        private float closeAwarenessRange = 2f;

        [SerializeField, Min(0f), Tooltip("Seconds from noticing the player to the earliest first shot.")]
        private float reactionTime = 0.5f;

        [Header("Movement")]
        [SerializeField, Min(0f), Tooltip("Walking speed while chasing the player, in metres per second.")]
        private float chaseSpeed = 3f;

        [SerializeField, Min(0f), Tooltip("How fast the enemy turns, in degrees per second.")]
        private float turnSpeed = 540f;

        [SerializeField, Min(0f), Tooltip("A chasing enemy that can see the player stops this close to them, in metres.")]
        private float stoppingDistance = 2f;

        [Header("Attack")]
        [SerializeField, Min(0.1f), Tooltip("The furthest the enemy will shoot from, in metres.")]
        private float attackRange = 25f;

        [SerializeField, Min(0f), Tooltip("Seconds spent standing still and aiming before each shot.")]
        private float aimTime = 0.4f;

        [SerializeField, Min(0f), Tooltip("Shortest time from one shot to the next, in seconds.")]
        private float minAttackInterval = 1f;

        [SerializeField, Min(0f), Tooltip("Longest time from one shot to the next, in seconds.")]
        private float maxAttackInterval = 2f;

        [SerializeField, Range(0f, 1f), Tooltip("Chance that a shot hits at point-blank range.")]
        private float closeAccuracy = 0.85f;

        [SerializeField, Range(0f, 1f), Tooltip("Chance that a shot hits at the edge of the attack range.")]
        private float longRangeAccuracy = 0.15f;

        [SerializeField, Min(0), Tooltip("Damage of a hit at point-blank range.")]
        private int closeDamage = 15;

        [SerializeField, Min(0), Tooltip("Damage of a hit at the edge of the attack range.")]
        private int longRangeDamage = 4;

        [Header("Pain")]
        [SerializeField, Min(0f), Tooltip("Seconds a hit that does not kill stops the enemy in its tracks.")]
        private float painDuration = 0.3f;

        [Header("Rewards")]
        [SerializeField, Min(0), Tooltip("Points the player scores for killing the enemy.")]
        private int killScore = 100;

        [SerializeField, Tooltip("The item the enemy leaves where it falls. Can be empty.")]
        private Pickup drop;

        [Header("Sound")]
        [SerializeField, Tooltip("Its voice, its gunshots and its footsteps.")]
        private CreatureSounds sounds;

        public float SightRange => sightRange;
        public float FieldOfView => fieldOfView;
        public float CloseAwarenessRange => closeAwarenessRange;
        public float ReactionTime => reactionTime;
        public float ChaseSpeed => chaseSpeed;
        public float TurnSpeed => turnSpeed;
        public float StoppingDistance => stoppingDistance;
        public float AttackRange => attackRange;
        public float AimTime => aimTime;
        public float MinAttackInterval => minAttackInterval;
        public float MaxAttackInterval => maxAttackInterval;
        public float PainDuration => painDuration;
        public int KillScore => killScore;
        public Pickup Drop => drop;
        public CreatureSounds Sounds => sounds;

        /// <summary>
        /// The chance that a shot fired from this far away hits. It falls steadily across the attack range.
        /// </summary>
        public float HitChanceAt(float distance) => Mathf.Lerp(closeAccuracy, longRangeAccuracy, distance / attackRange);

        /// <summary>
        /// The damage of a hit from this far away. It falls steadily across the attack range.
        /// </summary>
        public int DamageAt(float distance) => Mathf.RoundToInt(Mathf.Lerp(closeDamage, longRangeDamage, distance / attackRange));

        private void OnValidate() => maxAttackInterval = Mathf.Max(maxAttackInterval, minAttackInterval);
    }
}
