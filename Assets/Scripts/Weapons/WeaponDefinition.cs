using UnityEngine;
using Wolfenstein.Audio;

namespace Wolfenstein.Weapons
{
    /// <summary>
    /// Tuning for one hitscan weapon. A new gun is a new asset rather than new code.
    /// </summary>
    [CreateAssetMenu(fileName = "Weapon", menuName = "Wolfenstein/Weapon Definition")]
    public sealed class WeaponDefinition : ScriptableObject
    {
        [SerializeField, Min(0), Tooltip("Damage dealt by each shot that hits something damageable.")]
        private int damage = 20;

        [SerializeField, Min(0.1f), Tooltip("How far a shot reaches, in metres.")]
        private float range = 100f;

        [SerializeField, Min(0.01f), Tooltip("Shortest time between two shots, in seconds.")]
        private float fireInterval = 0.3f;

        [SerializeField, Tooltip("Keep firing while the trigger is held. Otherwise every shot needs a new press.")]
        private bool automatic;

        [SerializeField, Min(0), Tooltip("Rounds used by each shot. A weapon that uses none never runs out.")]
        private int ammoPerShot = 1;

        [Header("Sound")]
        [SerializeField, Tooltip("The shot itself.")]
        private SoundCue fireSound;

        [SerializeField, Tooltip("The action cycling after each shot, played along with it.")]
        private SoundCue mechanismSound;

        [SerializeField, Tooltip("The trigger pulled without the ammo for a shot.")]
        private SoundCue dryFireSound;

        public int Damage => damage;
        public float Range => range;
        public float FireInterval => fireInterval;
        public bool Automatic => automatic;
        public int AmmoPerShot => ammoPerShot;
        public SoundCue FireSound => fireSound;
        public SoundCue MechanismSound => mechanismSound;
        public SoundCue DryFireSound => dryFireSound;
    }
}
