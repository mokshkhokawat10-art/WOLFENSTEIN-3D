using System;
using UnityEngine;

namespace Wolfenstein.Combat
{
    /// <summary>
    /// Hit points for anything that can be hurt and killed, the player and enemies alike.
    /// Health stays between zero and the maximum, and <see cref="Died"/> is raised exactly once.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class Health : MonoBehaviour, IDamageable
    {
        [SerializeField, Min(1)]
        private int maxHealth = 100;

        /// <summary>Raised with the new value whenever current health changes.</summary>
        public event Action<int> Changed;

        public event Action<DamageInfo> Damaged;

        public event Action Died;

        public int Current { get; private set; }
        public int Max => maxHealth;
        public bool IsDead => Current <= 0;

        private void Awake() => Current = maxHealth;

        public void TakeDamage(DamageInfo damage)
        {
            if (IsDead || damage.Amount <= 0)
                return;

            SetCurrent(Current - damage.Amount);
            Damaged?.Invoke(damage);

            if (IsDead)
                Died?.Invoke();
        }

        /// <summary>
        /// Brings the owner back at full health with a new maximum, for something returned to play such as an enemy
        /// taken from a pool. It can then be hurt and die again.
        /// </summary>
        public void Restore(int newMax) => Restore(newMax, newMax);

        /// <summary>Brings the owner back with a new maximum and a given health up to it, such as a level's starting health.</summary>
        public void Restore(int newMax, int current)
        {
            maxHealth = Mathf.Max(1, newMax);
            SetCurrent(Mathf.Clamp(current, 1, maxHealth));
        }

        /// <summary>
        /// Restores health up to the maximum and returns how much was restored. The dead cannot be healed.
        /// </summary>
        public int Heal(int amount)
        {
            if (IsDead || amount <= 0)
                return 0;

            int previous = Current;
            SetCurrent(Current + amount);
            return Current - previous;
        }

        private void SetCurrent(int value)
        {
            int clamped = Mathf.Clamp(value, 0, maxHealth);
            if (clamped == Current)
                return;

            Current = clamped;
            Changed?.Invoke(Current);
        }

        // Lets damage be tested from the Inspector without an enemy having to land a shot.
        [ContextMenu("Debug/Take 25 Damage")]
        private void TakeDebugDamage() => TakeDamage(new DamageInfo(25, transform.position, Vector3.zero, null));
    }
}
