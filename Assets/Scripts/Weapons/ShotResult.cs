using UnityEngine;
using Wolfenstein.Combat;

namespace Wolfenstein.Weapons
{
    /// <summary>
    /// The outcome of one hitscan shot, handed to everything that shows or reacts to it.
    /// </summary>
    public readonly struct ShotResult
    {
        public ShotResult(Ray ray, RaycastHit? hit, IDamageable target)
        {
            Ray = ray;
            HasHit = hit.HasValue;
            Hit = hit.GetValueOrDefault();
            Target = target;
        }

        public Ray Ray { get; }
        public bool HasHit { get; }

        /// <summary>The surface the shot struck. Only meaningful when <see cref="HasHit"/> is true.</summary>
        public RaycastHit Hit { get; }

        /// <summary>The damageable object that was hit, or null if the shot struck scenery or nothing.</summary>
        public IDamageable Target { get; }
    }
}
