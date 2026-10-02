namespace Wolfenstein.Audio
{
    /// <summary>What the music is playing for.</summary>
    public enum MusicState
    {
        Silent,

        /// <summary>A menu screen. Nothing in the level asks for it; it waits for a menu scene.</summary>
        Menu,

        /// <summary>Moving through the level with nothing hunting the player.</summary>
        Exploration,

        /// <summary>Something is hunting the player, or an ordinary wave is on.</summary>
        Combat,

        /// <summary>An elite or final wave.</summary>
        Intense,

        /// <summary>The arena has just been cleared.</summary>
        Victory
    }
}
