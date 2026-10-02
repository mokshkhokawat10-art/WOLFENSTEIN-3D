using System;
using UnityEngine;
using Wolfenstein.Combat;

namespace Wolfenstein.Weapons
{
    /// <summary>
    /// Fires the equipped hitscan weapon from the view point: it enforces the fire rate, spends ammo,
    /// traces the shot and damages what it hits. It reads no input itself; its owner works the trigger.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(AmmoSupply))]
    public sealed class WeaponController : MonoBehaviour
    {
        [SerializeField, Tooltip("Where shots start and which way they travel, normally the player camera.")]
        private Transform viewPoint;

        [SerializeField]
        private WeaponDefinition weapon;

        [SerializeField, Tooltip("Layers that shots can hit. Trigger colliders, such as doorways, never stop a shot.")]
        private LayerMask hitMask = Physics.DefaultRaycastLayers;

        private AmmoSupply ammo;
        private float nextShotTime;

        /// <summary>Raised after every shot, whether it hit anything or not.</summary>
        public event Action<ShotResult> Fired;

        /// <summary>Raised when the trigger is pressed on a weapon ready to fire but without the ammo for a shot.</summary>
        public event Action DryFired;

        public WeaponDefinition Weapon => weapon;
        public AmmoSupply Ammo => ammo;

        /// <summary>Whether there are too few rounds left for a shot.</summary>
        public bool IsOutOfAmmo => ammo.Current < weapon.AmmoPerShot;

        private void Awake() => ammo = GetComponent<AmmoSupply>();

        /// <summary>
        /// Called every frame with the trigger state. A shot needs a new press unless the weapon is automatic.
        /// A new press with too little ammo clicks on an empty chamber once the weapon is ready.
        /// </summary>
        public void UpdateTrigger(bool pressedThisFrame, bool held)
        {
            if (!pressedThisFrame && !(held && weapon.Automatic))
                return;

            if (!TryFire() && pressedThisFrame && IsOutOfAmmo && Time.time >= nextShotTime)
                DryFired?.Invoke();
        }

        /// <summary>
        /// Fires once if the weapon is ready and there is enough ammo.
        /// </summary>
        public bool TryFire()
        {
            if (Time.time < nextShotTime || !ammo.TryConsume(weapon.AmmoPerShot))
                return false;

            nextShotTime = Time.time + weapon.FireInterval;
            Fired?.Invoke(TraceShot());
            return true;
        }

        private ShotResult TraceShot()
        {
            // The ray starts inside the player's own collider, which a raycast never reports.
            var ray = new Ray(viewPoint.position, viewPoint.forward);
            if (!Physics.Raycast(ray, out RaycastHit hit, weapon.Range, hitMask, QueryTriggerInteraction.Ignore))
                return new ShotResult(ray, null, null);

            var target = hit.collider.GetComponentInParent<IDamageable>();
            target?.TakeDamage(new DamageInfo(weapon.Damage, hit.point, ray.direction, gameObject));
            return new ShotResult(ray, hit, target);
        }
    }
}
