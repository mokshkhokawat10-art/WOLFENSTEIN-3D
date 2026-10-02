namespace Wolfenstein.Core
{
    public enum GameState
    {
        Loading,
        Playing,
        GameOver,

        /// <summary>The player has reached the exit; the level's end is being announced.</summary>
        LevelComplete,

        /// <summary>The end-of-level results are on screen, waiting for the player to continue.</summary>
        Tally,

        /// <summary>The title screen, before a level is started.</summary>
        Title
    }
}
