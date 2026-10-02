using System;
using UnityEngine;
using Wolfenstein.Combat;
using Wolfenstein.Pickups;
using Wolfenstein.Player;
using Wolfenstein.Weapons;
using Wolfenstein.World;

namespace Wolfenstein.Audio
{
    /// <summary>
    /// The player's sounds: the gun firing, clicking empty and striking whatever it hits, footsteps that follow the floor
    /// underfoot, jumping and landing, being hurt, dying and collecting items. It only listens to the player's components
    /// and watches how the player moves; it changes nothing about them.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PlayerAudio : MonoBehaviour
    {
        // Further than this in one frame is a teleport rather than a step.
        private const float MaxStepPerFrame = 1f;

        [Serializable]
        private struct SurfaceSound
        {
            public SurfaceKind surface;
            public SoundCue sound;
        }

        [SerializeField] private AudioManager audioManager;
        [SerializeField] private PlayerController player;

        [SerializeField, Tooltip("Says what the floor and walls are made of where the player walks and shoots.")]
        private WorldManager world;

        [Header("Footsteps")]
        [SerializeField, Tooltip("The footsteps on each kind of floor.")]
        private SurfaceSound[] footsteps = Array.Empty<SurfaceSound>();

        [SerializeField, Tooltip("The floor where the level's zone map sets none.")]
        private SurfaceKind defaultFloor = SurfaceKind.Stone;

        [SerializeField, Tooltip("The walls where the level's zone map sets none.")]
        private SurfaceKind defaultWall = SurfaceKind.Stone;

        [SerializeField, Tooltip("Door tiles are crossed on the door's steel track.")]
        private SurfaceKind doorwaySurface = SurfaceKind.Metal;

        [SerializeField, Min(0.2f), Tooltip("Distance covered between one footstep and the next, in metres.")]
        private float stepDistance = 2.1f;

        [SerializeField, Min(0f), Tooltip("Slower than this the player makes no footsteps, in metres per second.")]
        private float minStepSpeed = 1f;

        [Header("Body")]
        [SerializeField] private SoundCue jump;
        [SerializeField] private SoundCue land;

        [SerializeField, Min(0f), Tooltip("Falling speed at which a landing is heard, in metres per second.")]
        private float landingSpeed = 3f;

        [SerializeField] private SoundCue hurt;
        [SerializeField] private SoundCue death;

        [Header("Shots")]
        [SerializeField, Tooltip("A shot striking each kind of surface.")]
        private SurfaceSound[] impacts = Array.Empty<SurfaceSound>();

        [SerializeField, Tooltip("A shot striking something alive.")]
        private SoundCue fleshImpact;

        [Header("Items")]
        [SerializeField, Tooltip("For items that give health and have no sound of their own.")]
        private SoundCue healthPickup;

        [SerializeField, Tooltip("For items that give ammo and have no sound of their own.")]
        private SoundCue ammoPickup;

        [SerializeField, Tooltip("For items worth points and with no sound of their own.")]
        private SoundCue treasurePickup;

        private PlayerMovement movement;
        private WeaponController weapon;
        private Health health;
        private PickupCollector collector;
        private Vector3 lastPosition;
        private float stride;
        private bool wasGrounded = true;
        private float airTime;
        private float fallSpeed;

        /// <summary>What the floor under the player is made of.</summary>
        public SurfaceKind SurfaceUnderfoot => FloorAt(player.transform.position);

        public float StepDistance => stepDistance;

        private void Awake()
        {
            // Read from the player's object rather than its controller, which may not have woken yet.
            movement = player.GetComponent<PlayerMovement>();
            weapon = player.GetComponent<WeaponController>();
            health = player.GetComponent<Health>();
            collector = player.GetComponent<PickupCollector>();
            lastPosition = player.transform.position;
        }

        private void OnEnable()
        {
            weapon.Fired += HandleFired;
            weapon.DryFired += HandleDryFired;
            health.Damaged += HandleDamaged;
            health.Died += HandleDied;
            collector.Collected += HandleCollected;
        }

        private void OnDisable()
        {
            weapon.Fired -= HandleFired;
            weapon.DryFired -= HandleDryFired;
            health.Damaged -= HandleDamaged;
            health.Died -= HandleDied;
            collector.Collected -= HandleCollected;
        }

        /// <summary>What the floor is made of at a point: a doorway's track, the zone's floor, or the usual floor.</summary>
        public SurfaceKind FloorAt(Vector3 position)
        {
            if (world == null || world.Layout == null)
                return defaultFloor;

            Vector2Int cell = world.WorldToCell(position);
            if (world.Layout.TryGetDoor(cell, out _))
                return doorwaySurface;

            ZoneTheme theme = world.GetZoneTheme(cell);
            return theme != null ? theme.FloorSurface : defaultFloor;
        }

        public SoundCue FootstepOn(SurfaceKind surface) => Find(footsteps, surface, defaultFloor);

        /// <summary>The sound of a shot striking what it hit: flesh, a door's steel, or the surface of the floor or wall.</summary>
        public SoundCue ImpactFor(ShotResult shot)
        {
            if (!shot.HasHit)
                return null;

            if (shot.Target != null)
                return fleshImpact;

            RaycastHit hit = shot.Hit;
            if (hit.collider.GetComponentInParent<Door>() != null)
                return Find(impacts, SurfaceKind.Metal, defaultWall);

            // A wall face takes the look of the zone it faces, so the struck surface belongs to the cell in front of it.
            ZoneTheme theme = world != null ? world.GetZoneTheme(world.WorldToCell(hit.point + hit.normal * 0.2f)) : null;
            bool floorOrCeiling = Mathf.Abs(hit.normal.y) > 0.7f;
            SurfaceKind surface = theme == null
                ? floorOrCeiling ? defaultFloor : defaultWall
                : floorOrCeiling ? theme.FloorSurface : theme.WallSurface;
            return Find(impacts, surface, defaultWall);
        }

        /// <summary>The sound of collecting an item: its own, or the usual one for what it gives.</summary>
        public SoundCue CollectSoundFor(PickupDefinition item)
        {
            if (item.CollectSound != null)
                return item.CollectSound;
            if (item.Health > 0)
                return healthPickup;
            return item.Ammo > 0 ? ammoPickup : treasurePickup;
        }

        private void Update()
        {
            Vector3 position = player.transform.position;
            float moved = Vector3.ProjectOnPlane(position - lastPosition, Vector3.up).magnitude;
            lastPosition = position;

            bool grounded = movement.IsGrounded;
            UpdateAirborne(grounded);
            if (!grounded || health.IsDead || moved > MaxStepPerFrame)
                return;

            // Standing still readies the next step, so the first comes soon after the player sets off.
            if (moved < minStepSpeed * Time.deltaTime)
            {
                stride = Mathf.Max(stride, stepDistance * 0.6f);
                return;
            }

            stride += moved;
            if (stride >= stepDistance)
            {
                stride = Mathf.Min(stride - stepDistance, stepDistance * 0.5f);
                audioManager.Play(FootstepOn(SurfaceUnderfoot));
            }
        }

        // A jump is heard as the player leaves the ground moving up, and a landing when they come down fast enough.
        private void UpdateAirborne(bool grounded)
        {
            float verticalSpeed = movement.Velocity.y;
            if (!grounded)
            {
                if (wasGrounded && verticalSpeed > 0.5f)
                    audioManager.Play(jump);

                airTime += Time.deltaTime;
                fallSpeed = Mathf.Max(fallSpeed, -verticalSpeed);
            }
            else if (!wasGrounded)
            {
                if (fallSpeed >= landingSpeed && airTime > 0.15f && !health.IsDead)
                {
                    audioManager.Play(land);
                    audioManager.Play(FootstepOn(SurfaceUnderfoot));
                    stride = 0f;
                }

                airTime = 0f;
                fallSpeed = 0f;
            }

            wasGrounded = grounded;
        }

        private void HandleFired(ShotResult shot)
        {
            WeaponDefinition definition = weapon.Weapon;
            audioManager.Play(definition.FireSound);
            audioManager.Play(definition.MechanismSound);
            if (shot.HasHit)
                audioManager.Play(ImpactFor(shot), shot.Hit.point);
        }

        private void HandleDryFired() => audioManager.Play(weapon.Weapon.DryFireSound);

        // The blow that kills is heard as the death, not as one more hurt.
        private void HandleDamaged(DamageInfo damage)
        {
            if (!health.IsDead)
                audioManager.Play(hurt);
        }

        private void HandleDied() => audioManager.Play(death);

        private void HandleCollected(PickupDefinition item) => audioManager.Play(CollectSoundFor(item));

        private static SoundCue Find(SurfaceSound[] table, SurfaceKind surface, SurfaceKind fallback)
        {
            SoundCue fallbackSound = null;
            foreach (SurfaceSound entry in table)
            {
                if (entry.surface == surface)
                    return entry.sound;
                if (entry.surface == fallback)
                    fallbackSound = entry.sound;
            }

            return fallbackSound;
        }
    }
}
