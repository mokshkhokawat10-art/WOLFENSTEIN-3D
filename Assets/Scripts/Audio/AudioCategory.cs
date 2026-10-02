namespace Wolfenstein.Audio
{
    /// <summary>
    /// The groups every sound belongs to, each with its own volume under the master volume.
    /// </summary>
    public enum AudioCategory
    {
        /// <summary>Sounds of the world: weapons, footsteps, monsters, doors and pickups.</summary>
        Sfx,

        /// <summary>The background of a place: room tone, machinery, distant noises.</summary>
        Ambience,

        Music,

        /// <summary>Sounds that belong to the game rather than the world, such as wave warnings and the death sting.</summary>
        Ui
    }
}
