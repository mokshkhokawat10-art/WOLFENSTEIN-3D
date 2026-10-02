using UnityEngine;
using Wolfenstein.Audio;

namespace Wolfenstein.Pickups
{
    /// <summary>
    /// What one kind of item gives the player who collects it: health, ammo, points or any mix of them.
    /// A new item is a new asset rather than new code.
    /// </summary>
    [CreateAssetMenu(fileName = "Pickup", menuName = "Wolfenstein/Pickup Definition")]
    public sealed class PickupDefinition : ScriptableObject
    {
        [SerializeField, Min(0), Tooltip("Health restored, up to the maximum.")]
        private int health;

        [SerializeField, Min(0), Tooltip("Rounds added to the player's ammo, up to the maximum.")]
        private int ammo;

        [SerializeField, Min(0), Tooltip("Points added to the score.")]
        private int score;

        [SerializeField, Tooltip("A key the item gives the player, such as the security key. Empty for an item that is not a key.")]
        private AccessKey accessKey;

        [SerializeField, Tooltip("Played when the item is collected. Empty uses the usual sound for what it gives.")]
        private SoundCue collectSound;

        public int Health => health;
        public int Ammo => ammo;
        public int Score => score;
        public AccessKey AccessKey => accessKey;
        public SoundCue CollectSound => collectSound;
    }
}
