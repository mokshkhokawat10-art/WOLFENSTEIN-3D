using UnityEngine;

namespace Wolfenstein.Audio
{
    /// <summary>
    /// Something in the level that makes a steady sound where it stands, such as a generator or a vent. It plays nothing
    /// itself and has no audio source: the <see cref="AmbienceDirector"/> gives a voice to the few nearest the listener.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class AmbientSoundSource : MonoBehaviour
    {
        [SerializeField, Tooltip("A looping cue.")]
        private SoundCue sound;

        [SerializeField, Tooltip("Where the sound comes from, relative to the object.")]
        private Vector3 offset = new Vector3(0f, 1f, 0f);

        public SoundCue Sound => sound;
        public Vector3 Position => transform.TransformPoint(offset);
    }
}
