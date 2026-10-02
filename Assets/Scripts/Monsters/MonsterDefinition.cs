using UnityEngine;
using Wolfenstein.Audio;
using Wolfenstein.Pickups;

namespace Wolfenstein.Monsters
{
    /// <summary>
    /// Tuning for one kind of monster: how tough it is, how it moves, which attacks it has and what killing it earns.
    /// A monster can claw at close range, fire projectiles from a distance, charge, or any mix of them;
    /// a new kind of monster is a new asset and a model rather than new code.
    /// </summary>
    [CreateAssetMenu(fileName = "Monster", menuName = "Wolfenstein/Monster Definition")]
    public sealed class MonsterDefinition : ScriptableObject
    {
        [SerializeField, Tooltip("Shown to the player, such as in wave announcements.")]
        private string displayName = "Monster";

        [Header("Vitality")]
        [SerializeField, Min(1), Tooltip("Hit points before any wave's difficulty is applied.")]
        private int maxHealth = 50;

        [SerializeField, Range(0f, 1f), Tooltip("Chance that a hit which does not kill stops the monster in its tracks.")]
        private float painChance = 0.5f;

        [SerializeField, Min(0f), Tooltip("Seconds a hit that stops the monster holds it.")]
        private float painDuration = 0.3f;

        [Header("Movement")]
        [SerializeField, Min(0f), Tooltip("Running speed while hunting, in metres per second.")]
        private float moveSpeed = 4f;

        [SerializeField, Min(0f), Tooltip("How fast the monster turns, in degrees per second.")]
        private float turnSpeed = 480f;

        [SerializeField, Min(0f), Tooltip("How far away an idle monster notices the player, in metres. Hunting monsters always know where they are.")]
        private float sightRange = 30f;

        [Header("Melee")]
        [SerializeField] private bool hasMelee = true;

        [SerializeField, Min(0.1f), Tooltip("How far the monster reaches, from the edge of its body to the edge of the target's, in metres.")]
        private float meleeReach = 1f;

        [SerializeField, Min(0)] private int meleeDamage = 8;

        [SerializeField, Min(0f), Tooltip("Seconds the monster winds up before a blow lands; the player can dodge in this time.")]
        private float meleeWindup = 0.35f;

        [SerializeField, Min(0f), Tooltip("Shortest time from one blow to the next, in seconds.")]
        private float meleeCooldown = 1f;

        [SerializeField, Min(0f), Tooltip("Radius of the shockwave around the point of impact that also hurts, in metres. 0 for a single blow.")]
        private float meleeSplashRadius;

        [Header("Ranged")]
        [SerializeField] private bool hasRanged;

        [SerializeField, Tooltip("Fired at the player. Needs a projectile prefab.")]
        private MonsterProjectile projectile;

        [SerializeField, Min(0.1f), Tooltip("Speed of each projectile, in metres per second.")]
        private float projectileSpeed = 12f;

        [SerializeField, Min(0)] private int projectileDamage = 10;

        [SerializeField, Min(1), Tooltip("Projectiles fired at once, fanned out across the spread.")]
        private int projectilesPerVolley = 1;

        [SerializeField, Range(0f, 45f), Tooltip("Angle between neighbouring projectiles of a volley, in degrees.")]
        private float volleySpread = 5f;

        [SerializeField, Min(0f), Tooltip("Closer than this the monster will not fire, in metres.")]
        private float minFireRange = 3f;

        [SerializeField, Min(0.1f), Tooltip("Further than this the monster will not fire, in metres.")]
        private float maxFireRange = 22f;

        [SerializeField, Min(0f), Tooltip("A monster with only a ranged attack tries to keep this far from its target, in metres.")]
        private float preferredDistance = 10f;

        [SerializeField, Min(0f), Tooltip("Seconds the monster charges a shot before it flies.")]
        private float fireWindup = 0.5f;

        [SerializeField, Min(0f), Tooltip("Shortest time from one volley to the next, in seconds.")]
        private float fireCooldown = 2f;

        [Header("Charge")]
        [SerializeField, Tooltip("Rushes at the player from mid range.")]
        private bool canCharge;

        [SerializeField, Min(1f), Tooltip("Speed while charging, as a multiple of the running speed.")]
        private float chargeSpeedMultiplier = 2.2f;

        [SerializeField, Min(0f)] private float chargeMinRange = 5f;
        [SerializeField, Min(0f)] private float chargeMaxRange = 12f;

        [SerializeField, Min(0.1f), Tooltip("Longest a charge lasts, in seconds.")]
        private float chargeDuration = 0.8f;

        [SerializeField, Min(0f), Tooltip("Shortest time from one charge to the next, in seconds.")]
        private float chargeCooldown = 5f;

        [Header("Spawning")]
        [SerializeField, Min(0f), Tooltip("Seconds the monster takes to rise into the level, during which it cannot attack.")]
        private float spawnDuration = 0.8f;

        [Header("Rewards")]
        [SerializeField, Min(0), Tooltip("Points the player scores for killing the monster.")]
        private int killScore = 100;

        [SerializeField, Tooltip("The item the monster may leave where it falls. Can be empty.")]
        private Pickup drop;

        [SerializeField, Range(0f, 1f), Tooltip("Chance that the monster leaves its item.")]
        private float dropChance = 0.25f;

        [Header("Sound")]
        [SerializeField, Tooltip("Its voice and the noises it makes, which tell the player what is coming.")]
        private CreatureSounds sounds;

        public string DisplayName => displayName;
        public int MaxHealth => maxHealth;
        public float PainChance => painChance;
        public float PainDuration => painDuration;
        public float MoveSpeed => moveSpeed;
        public float TurnSpeed => turnSpeed;
        public float SightRange => sightRange;

        public bool HasMelee => hasMelee;
        public float MeleeReach => meleeReach;
        public int MeleeDamage => meleeDamage;
        public float MeleeWindup => meleeWindup;
        public float MeleeCooldown => meleeCooldown;
        public float MeleeSplashRadius => meleeSplashRadius;

        public bool HasRanged => hasRanged && projectile != null;
        public MonsterProjectile Projectile => projectile;
        public float ProjectileSpeed => projectileSpeed;
        public int ProjectileDamage => projectileDamage;
        public int ProjectilesPerVolley => projectilesPerVolley;
        public float VolleySpread => volleySpread;
        public float MinFireRange => minFireRange;
        public float MaxFireRange => maxFireRange;
        public float PreferredDistance => preferredDistance;
        public float FireWindup => fireWindup;
        public float FireCooldown => fireCooldown;

        public bool CanCharge => canCharge;
        public float ChargeSpeedMultiplier => chargeSpeedMultiplier;
        public float ChargeMinRange => chargeMinRange;
        public float ChargeMaxRange => chargeMaxRange;
        public float ChargeDuration => chargeDuration;
        public float ChargeCooldown => chargeCooldown;

        public float SpawnDuration => spawnDuration;
        public int KillScore => killScore;
        public Pickup Drop => drop;
        public float DropChance => dropChance;
        public CreatureSounds Sounds => sounds;

        private void OnValidate()
        {
            maxFireRange = Mathf.Max(maxFireRange, minFireRange);
            chargeMaxRange = Mathf.Max(chargeMaxRange, chargeMinRange);
        }
    }
}
