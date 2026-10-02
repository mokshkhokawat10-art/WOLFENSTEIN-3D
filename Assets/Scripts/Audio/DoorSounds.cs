using UnityEngine;

namespace Wolfenstein.Audio
{
    /// <summary>
    /// How one kind of door sounds as it moves, locks and refuses to open, from a light sliding door to a blast door.
    /// </summary>
    [CreateAssetMenu(fileName = "DoorSounds", menuName = "Wolfenstein/Door Sounds")]
    public sealed class DoorSounds : ScriptableObject
    {
        [SerializeField, Tooltip("As the door starts to open: the latch and the drive.")]
        private SoundCue open;

        [SerializeField, Tooltip("As it comes to rest fully open.")]
        private SoundCue opened;

        [SerializeField, Tooltip("As it starts to close.")]
        private SoundCue close;

        [SerializeField, Tooltip("As it shuts.")]
        private SoundCue closed;

        [SerializeField, Tooltip("When someone tries to open it while it is locked.")]
        private SoundCue locked;

        [SerializeField, Tooltip("As it locks.")]
        private SoundCue lockEngaged;

        [SerializeField, Tooltip("As it unlocks.")]
        private SoundCue unlocked;

        [SerializeField, Min(0f), Tooltip("How far above the foot of the door its sounds come from, in metres.")]
        private float height = 1.5f;

        public SoundCue Open => open;
        public SoundCue Opened => opened;
        public SoundCue Close => close;
        public SoundCue Closed => closed;
        public SoundCue Locked => locked;
        public SoundCue LockEngaged => lockEngaged;
        public SoundCue Unlocked => unlocked;
        public float Height => height;
    }
}
