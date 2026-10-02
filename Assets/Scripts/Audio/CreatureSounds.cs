using UnityEngine;

namespace Wolfenstein.Audio
{
    /// <summary>
    /// The voice and noises of one kind of monster or guard. Each kind sounds its own, so the player can tell what is coming
    /// before it comes into view. Any cue can be left empty for a creature that makes no such sound, such as footsteps for
    /// one that floats.
    /// </summary>
    [CreateAssetMenu(fileName = "CreatureSounds", menuName = "Wolfenstein/Creature Sounds")]
    public sealed class CreatureSounds : ScriptableObject
    {
        [Header("Voice")]
        [SerializeField, Tooltip("Now and then while it lurks, before it has noticed anyone.")]
        private SoundCue idle;

        [SerializeField, Tooltip("Now and then while it hunts.")]
        private SoundCue hunt;

        [SerializeField, Tooltip("When it notices its prey.")]
        private SoundCue alert;

        [SerializeField] private SoundCue pain;
        [SerializeField] private SoundCue death;

        [SerializeField, Tooltip("Seconds between one growl and the next, picked at random between these.")]
        private Vector2 vocalInterval = new Vector2(3f, 7f);

        [Header("Attacks")]
        [SerializeField, Tooltip("As it winds up a blow.")]
        private SoundCue meleeWindup;

        [SerializeField, Tooltip("As a blow is struck, whether it lands or not.")]
        private SoundCue meleeStrike;

        [SerializeField, Tooltip("Where a blow with a shockwave strikes the floor.")]
        private SoundCue slam;

        [SerializeField, Tooltip("As it charges a shot.")]
        private SoundCue rangedWindup;

        [SerializeField, Tooltip("As a shot leaves: a gunshot, or a projectile launched.")]
        private SoundCue rangedLaunch;

        [SerializeField, Tooltip("Where its projectiles burst.")]
        private SoundCue projectileImpact;

        [SerializeField, Tooltip("As it starts to charge.")]
        private SoundCue charge;

        [Header("Movement")]
        [SerializeField, Tooltip("Each step it takes. Empty for something that floats.")]
        private SoundCue footstep;

        [SerializeField, Min(0.2f), Tooltip("Distance covered between one step and the next, in metres.")]
        private float stepDistance = 1.6f;

        public SoundCue Idle => idle;
        public SoundCue Hunt => hunt;
        public SoundCue Alert => alert;
        public SoundCue Pain => pain;
        public SoundCue Death => death;
        public Vector2 VocalInterval => vocalInterval;

        public SoundCue MeleeWindup => meleeWindup;
        public SoundCue MeleeStrike => meleeStrike;
        public SoundCue Slam => slam;
        public SoundCue RangedWindup => rangedWindup;
        public SoundCue RangedLaunch => rangedLaunch;
        public SoundCue ProjectileImpact => projectileImpact;
        public SoundCue Charge => charge;

        public SoundCue Footstep => footstep;
        public float StepDistance => stepDistance;

        /// <summary>Seconds until the next growl, at random within the vocal interval.</summary>
        public float PickVocalDelay() => Random.Range(Mathf.Min(vocalInterval.x, vocalInterval.y), Mathf.Max(vocalInterval.x, vocalInterval.y));
    }
}
