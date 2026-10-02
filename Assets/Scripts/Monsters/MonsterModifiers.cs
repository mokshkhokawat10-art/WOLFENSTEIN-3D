using System;
using UnityEngine;

namespace Wolfenstein.Monsters
{
    /// <summary>
    /// How much tougher, faster and harder-hitting than its definition a monster is, such as in a later wave.
    /// </summary>
    [Serializable]
    public struct MonsterModifiers
    {
        public static readonly MonsterModifiers None = new MonsterModifiers(1f, 1f, 1f);

        [Min(0.1f)] public float health;
        [Min(0.1f)] public float speed;
        [Min(0.1f)] public float damage;

        public MonsterModifiers(float health, float speed, float damage)
        {
            this.health = health;
            this.speed = speed;
            this.damage = damage;
        }

        public int ScaleHealth(int value) => Mathf.Max(1, Mathf.RoundToInt(value * health));

        public int ScaleDamage(int value) => Mathf.Max(0, Mathf.RoundToInt(value * damage));
    }
}
