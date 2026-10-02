using System;
using UnityEngine;

namespace Wolfenstein.Interaction
{
    /// <summary>
    /// Finds the interactable the player is looking at by casting a ray from the view point, and uses it.
    /// It reads no input itself; its owner decides when an interaction is attempted.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class InteractionController : MonoBehaviour
    {
        private static readonly RaycastHit[] Hits = new RaycastHit[16];

        [SerializeField, Tooltip("Where the interaction ray starts and which way it points, normally the player camera.")]
        private Transform viewPoint;

        [SerializeField, Min(0f), Tooltip("How far away an object can be used, in metres.")]
        private float interactionDistance = 2.5f;

        [SerializeField, Tooltip("Layers that can be interacted with or that block the interaction ray.")]
        private LayerMask interactionMask = Physics.DefaultRaycastLayers;

        /// <summary>Raised when the target in view cannot be used right now, such as a locked door.</summary>
        public event Action<IInteractable> Refused;

        public float InteractionDistance => interactionDistance;

        public bool TryInteract()
        {
            if (!TryFindTarget(out IInteractable target))
                return false;

            if (!target.CanInteract(gameObject))
            {
                Refused?.Invoke(target);
                return false;
            }

            target.Interact(gameObject);
            return true;
        }

        /// <summary>
        /// Finds the nearest interactable in view. Interactables can be solid or triggers, such as the doorway
        /// of an open door. Any other solid surface blocks the ray, and triggers that are not interactable are ignored.
        /// </summary>
        public bool TryFindTarget(out IInteractable target)
        {
            var ray = new Ray(viewPoint.position, viewPoint.forward);
            int hitCount = Physics.RaycastNonAlloc(ray, Hits, interactionDistance, interactionMask, QueryTriggerInteraction.Collide);

            target = null;
            float nearestDistance = float.PositiveInfinity;
            for (int i = 0; i < hitCount; i++)
            {
                RaycastHit hit = Hits[i];
                if (hit.distance >= nearestDistance)
                    continue;

                var interactable = hit.collider.GetComponentInParent<IInteractable>();
                if (interactable == null && hit.collider.isTrigger)
                    continue;

                nearestDistance = hit.distance;
                target = interactable;
            }

            return target != null;
        }
    }
}
