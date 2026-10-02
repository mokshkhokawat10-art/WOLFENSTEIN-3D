using System;
using UnityEngine;

namespace Wolfenstein.Audio
{
    /// <summary>One piece of music: its clip, how loud it plays and whether it repeats.</summary>
    [Serializable]
    public struct MusicTrack
    {
        [SerializeField] private AudioClip clip;
        [SerializeField, Range(0f, 1f)] private float volume;

        [SerializeField, Tooltip("Plays over and over. Otherwise it plays once, like a fanfare.")]
        private bool loop;

        public MusicTrack(AudioClip clip, float volume, bool loop)
        {
            this.clip = clip;
            this.volume = volume;
            this.loop = loop;
        }

        public AudioClip Clip => clip;
        public float Volume => volume;
        public bool Loop => loop;
    }

    /// <summary>
    /// The music of a level, one track for each <see cref="MusicState"/>. A track with no clip is an empty slot: the music
    /// falls silent in that state until a clip is added, with no change to code.
    /// </summary>
    [CreateAssetMenu(fileName = "Music", menuName = "Wolfenstein/Music Set")]
    public sealed class MusicSet : ScriptableObject
    {
        [SerializeField] private MusicTrack menu = new MusicTrack(null, 0.7f, true);
        [SerializeField] private MusicTrack exploration = new MusicTrack(null, 0.5f, true);
        [SerializeField] private MusicTrack combat = new MusicTrack(null, 0.6f, true);
        [SerializeField] private MusicTrack intense = new MusicTrack(null, 0.65f, true);
        [SerializeField] private MusicTrack victory = new MusicTrack(null, 0.7f, false);

        [SerializeField, Min(0.05f), Tooltip("Seconds one track takes to fade into the next.")]
        private float crossfade = 2f;

        public float Crossfade => crossfade;

        public MusicTrack Get(MusicState state) => state switch
        {
            MusicState.Menu => menu,
            MusicState.Exploration => exploration,
            MusicState.Combat => combat,
            MusicState.Intense => intense,
            MusicState.Victory => victory,
            _ => default
        };
    }
}
