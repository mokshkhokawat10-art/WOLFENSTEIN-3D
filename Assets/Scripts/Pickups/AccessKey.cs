using UnityEngine;

namespace Wolfenstein.Pickups
{
    /// <summary>
    /// A key the player can carry, such as the security key. Doors that need it are named in the level's data; the key
    /// itself is collected as an item whose <see cref="PickupDefinition"/> carries it.
    /// </summary>
    [CreateAssetMenu(fileName = "Key", menuName = "Wolfenstein/Access Key")]
    public sealed class AccessKey : ScriptableObject
    {
        [SerializeField, Tooltip("Shown to the player, such as SECURITY KEY.")]
        private string displayName = "KEY";

        [SerializeField] private Color colour = new Color(1f, 0.8f, 0.2f);

        public string DisplayName => displayName;
        public Color Colour => colour;
    }
}
