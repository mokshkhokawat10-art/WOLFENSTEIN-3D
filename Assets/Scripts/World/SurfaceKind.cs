namespace Wolfenstein.World
{
    /// <summary>
    /// What a floor or wall is made of, which decides how it sounds underfoot and when a shot strikes it.
    /// </summary>
    public enum SurfaceKind
    {
        Stone,
        Concrete,
        Metal,

        /// <summary>Open steel grating, which rattles.</summary>
        Grate,

        Wood,
        Carpet
    }
}
