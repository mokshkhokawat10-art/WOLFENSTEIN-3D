using System;
using UnityEngine;
using Wolfenstein.Combat;
using Wolfenstein.Core;
using Wolfenstein.Weapons;

namespace Wolfenstein.Pickups
{
    /// <summary>
    /// Collects the items the player's body touches. As in the original game, an item is only taken when it is of use:
    /// health only while the player is hurt and ammo only while they are short of the maximum.
    /// Anything worth points is always taken, and so is a key the player does not carry yet, which goes on their key ring.
    /// Nothing is collected once the player is dead.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(CharacterController), typeof(Health), typeof(AmmoSupply))]
    [RequireComponent(typeof(Score))]
    public sealed class PickupCollector : MonoBehaviour
    {
        private static readonly Collider[] Touching = new Collider[16];

        [SerializeField, Tooltip("Layers that items are on.")]
        private LayerMask pickupMask = Physics.DefaultRaycastLayers;

        private CharacterController body;
        private Health health;
        private AmmoSupply ammo;
        private Score score;
        private KeyRing keys;

        /// <summary>Raised whenever an item is collected.</summary>
        public event Action<PickupDefinition> Collected;

        private void Awake()
        {
            body = GetComponent<CharacterController>();
            health = GetComponent<Health>();
            ammo = GetComponent<AmmoSupply>();
            score = GetComponent<Score>();
            keys = GetComponent<KeyRing>();
        }

        // Checked every frame rather than on trigger events, so an item the player is already standing on is taken
        // the moment it becomes of use, such as a first aid kit once the player is hurt.
        private void Update()
        {
            if (health.IsDead)
                return;

            int count = OverlapBody();
            for (int i = 0; i < count; i++)
            {
                // A consumed item is inactive, and inactive components are not found.
                var pickup = Touching[i].GetComponentInParent<Pickup>();
                if (pickup != null && TryCollect(pickup.Definition))
                    pickup.Consume();
            }
        }

        // Whether collecting the item now would do the player any good.
        private bool CanUse(PickupDefinition item) =>
            item.Score > 0 ||
            (item.AccessKey != null && keys != null && !keys.Has(item.AccessKey)) ||
            (item.Health > 0 && health.Current < health.Max) ||
            (item.Ammo > 0 && ammo.Current < ammo.Max);

        // Gives the player everything the item holds, if it is of use.
        private bool TryCollect(PickupDefinition item)
        {
            if (!CanUse(item))
                return false;

            health.Heal(item.Health);
            ammo.Add(item.Ammo);
            score.Add(item.Score);
            if (item.AccessKey != null && keys != null)
                keys.Add(item.AccessKey);
            Collected?.Invoke(item);
            return true;
        }

        // The controller's own capsule at full size, rather than the smaller copy PhysX keeps for queries against it.
        private int OverlapBody()
        {
            Vector3 centre = transform.TransformPoint(body.center);
            Vector3 halfLine = transform.up * Mathf.Max(0f, body.height * 0.5f - body.radius);
            return Physics.OverlapCapsuleNonAlloc(centre + halfLine, centre - halfLine, body.radius, Touching, pickupMask,
                QueryTriggerInteraction.Collide);
        }
    }
}
