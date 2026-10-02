using UnityEngine;

namespace Wolfenstein.Pickups
{
    /// <summary>
    /// An item lying in the level, waiting to be collected. A trigger collider on it, or on a child, marks the space the
    /// player must touch to collect it; being a trigger, it never blocks movement, shots, sight, interaction or a closing door.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class Pickup : MonoBehaviour
    {
        [SerializeField]
        private PickupDefinition definition;

        public PickupDefinition Definition => definition;

        /// <summary>
        /// Removes the item from the level. It stops being found by physics queries at once.
        /// </summary>
        public void Consume()
        {
            gameObject.SetActive(false);
            Destroy(gameObject);
        }
    }
}
