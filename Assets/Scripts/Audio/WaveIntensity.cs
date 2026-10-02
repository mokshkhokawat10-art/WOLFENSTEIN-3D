namespace Wolfenstein.Audio
{
    /// <summary>How big an event a wave is, which sets how loudly it is announced and how hard the music drives.</summary>
    public enum WaveIntensity
    {
        Normal,

        /// <summary>A wave with a great many monsters.</summary>
        Major,

        /// <summary>A wave with an elite held back for a big entrance.</summary>
        Elite,

        /// <summary>The last wave of the fight.</summary>
        Final
    }
}
