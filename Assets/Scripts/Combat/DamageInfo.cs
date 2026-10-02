using UnityEngine;

namespace Wolfenstein.Combat
{
    /// <summary>
    /// One instance of damage: how much, where it landed, which way it travelled and what caused it.
    /// </summary>
    public readonly struct DamageInfo
    {
        public DamageInfo(int amount, Vector3 point, Vector3 direction, GameObject source)
        {
            Amount = amount;
            Point = point;
            Direction = direction;
            Source = source;
        }

        public int Amount { get; }
        public Vector3 Point { get; }
        public Vector3 Direction { get; }

        /// <summary>The object responsible, such as the player who fired. Can be null.</summary>
        public GameObject Source { get; }
    }
}
