namespace Wolfenstein.Progression
{
    /// <summary>What an objective asks of the player, and so what completes it.</summary>
    public enum ObjectiveKind
    {
        /// <summary>Pick up a key.</summary>
        CollectKey,

        /// <summary>Throw a wall switch.</summary>
        ThrowSwitch,

        /// <summary>Set foot in a zone of the level.</summary>
        ReachZone,

        /// <summary>Beat every wave of the arena.</summary>
        ClearArena,

        /// <summary>Walk into the exit once it is open. It is always the last objective.</summary>
        ReachExit
    }
}
