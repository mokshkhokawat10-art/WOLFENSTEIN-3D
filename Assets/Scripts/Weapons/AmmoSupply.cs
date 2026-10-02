using System;
using UnityEngine;

namespace Wolfenstein.Weapons
{
    /// <summary>
    /// The rounds the player carries. As in the original game, every gun draws from one shared supply.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class AmmoSupply : MonoBehaviour
    {
        [SerializeField, Min(0)]
        private int startingAmmo = 24;

        [SerializeField, Min(1)]
        private int maxAmmo = 99;

        /// <summary>Raised with the new value whenever the number of rounds changes.</summary>
        public event Action<int> Changed;

        public int Current { get; private set; }
        public int Max => maxAmmo;

        private void Awake() => Current = Mathf.Min(startingAmmo, maxAmmo);

        /// <summary>
        /// Takes the rounds only if all of them are available.
        /// </summary>
        public bool TryConsume(int amount)
        {
            if (amount < 0 || amount > Current)
                return false;

            SetCurrent(Current - amount);
            return true;
        }

        /// <summary>Sets the rounds carried outright, up to the maximum, such as at the start of a level.</summary>
        public void ResetTo(int rounds) => SetCurrent(rounds);

        /// <summary>
        /// Adds rounds up to the maximum and returns how many were actually taken.
        /// </summary>
        public int Add(int amount)
        {
            int previous = Current;
            SetCurrent(Current + Mathf.Max(0, amount));
            return Current - previous;
        }

        private void SetCurrent(int value)
        {
            int clamped = Mathf.Clamp(value, 0, maxAmmo);
            if (clamped == Current)
                return;

            Current = clamped;
            Changed?.Invoke(Current);
        }
    }
}
