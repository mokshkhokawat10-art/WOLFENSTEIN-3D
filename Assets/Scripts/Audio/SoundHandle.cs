namespace Wolfenstein.Audio
{
    /// <summary>
    /// Refers to one sound started by the <see cref="AudioManager"/>, so it can be stopped early. A handle goes stale once its
    /// sound ends or its voice is taken for another sound; stopping a stale handle does nothing.
    /// </summary>
    public readonly struct SoundHandle
    {
        internal SoundHandle(int voice, int generation)
        {
            Voice = voice;
            Generation = generation;
        }

        internal int Voice { get; }
        internal int Generation { get; }

        /// <summary>Whether a sound was started. It may have finished since; the audio manager says whether it still plays.</summary>
        public bool IsValid => Generation > 0;
    }
}
