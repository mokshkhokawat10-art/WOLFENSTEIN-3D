namespace Wolfenstein.Waves
{
    public enum WaveState
    {
        /// <summary>Waiting for the player to step into the arena.</summary>
        Waiting,

        /// <summary>A wave has been announced, and its monsters are about to come.</summary>
        Announcing,

        /// <summary>A wave's monsters are spawning and fighting.</summary>
        Fighting,

        /// <summary>A wave has been beaten, and the next one has not been announced yet.</summary>
        Complete,

        /// <summary>Every wave has been beaten, and the arena is open again.</summary>
        Cleared
    }
}
