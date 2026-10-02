namespace Wolfenstein.World
{
    /// <summary>How a decoration from the level's decor map is set on its tile.</summary>
    public enum PropPlacement
    {
        /// <summary>Standing on the floor with its back to a wall of the tile, facing into the room.</summary>
        AgainstWall,

        /// <summary>Against a wall like <see cref="AgainstWall"/>, slid along it toward a corner where there is one.</summary>
        InCorner,

        /// <summary>Standing in the middle of the tile, turned any way.</summary>
        Centre,

        /// <summary>Lying flat on the floor somewhere on the tile, such as a stain.</summary>
        OnFloor,

        /// <summary>Hung on a wall of the tile at a height, such as a banner or a vent.</summary>
        OnWall,

        /// <summary>Hanging from the ceiling in the middle of the tile.</summary>
        FromCeiling
    }
}
