namespace Wolfenstein.Enemies
{
    public enum EnemyState
    {
        /// <summary>Standing guard, watching for the player.</summary>
        Idle,

        /// <summary>Hunting the player down, and closing in while waiting for the next shot.</summary>
        Chase,

        /// <summary>Standing still, aiming at the player before a shot.</summary>
        Attack,

        /// <summary>Stopped in its tracks by a hit.</summary>
        Pain,

        Dead
    }
}
