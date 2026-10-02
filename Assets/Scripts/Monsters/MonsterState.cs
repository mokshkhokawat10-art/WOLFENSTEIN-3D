namespace Wolfenstein.Monsters
{
    public enum MonsterState
    {
        /// <summary>Waiting in a pool, out of the level.</summary>
        Inactive,

        /// <summary>Rising into the level. It cannot attack yet.</summary>
        Spawning,

        /// <summary>Lurking until it notices the player.</summary>
        Idle,

        /// <summary>Hunting its target down, keeping its distance if it only fights from range.</summary>
        Chase,

        /// <summary>Winding up an attack, which lands or flies when the wind-up ends.</summary>
        Attack,

        /// <summary>Rushing straight at its target.</summary>
        Charge,

        /// <summary>Stopped in its tracks by a hit.</summary>
        Pain,

        Dead
    }
}
