using System;
using System.Collections.Generic;
using UnityEngine;
using Wolfenstein.Monsters;
using Wolfenstein.Pickups;

namespace Wolfenstein.Waves
{
    /// <summary>
    /// One group of monsters in a wave: which kind, and how many.
    /// </summary>
    [Serializable]
    public struct WaveGroup
    {
        [SerializeField] private MonsterDefinition monster;
        [SerializeField, Min(0)] private int count;

        [SerializeField, Tooltip("Held back until the rest of the wave has spawned, for a big entrance such as an elite's.")]
        private bool spawnLast;

        public WaveGroup(MonsterDefinition monster, int count, bool spawnLast = false)
        {
            this.monster = monster;
            this.count = count;
            this.spawnLast = spawnLast;
        }

        public MonsterDefinition Monster => monster;
        public int Count => count;
        public bool SpawnLast => spawnLast;
    }

    /// <summary>
    /// One wave of an arena fight: the monsters in it, how much tougher than usual they are, how quickly they pour in,
    /// how many may be loose at once, and the supplies left for the player once it is beaten.
    /// </summary>
    [Serializable]
    public sealed class Wave
    {
        [SerializeField] private WaveGroup[] groups = Array.Empty<WaveGroup>();

        [SerializeField, Tooltip("How much tougher, faster and harder-hitting than usual this wave's monsters are.")]
        private MonsterModifiers difficulty = MonsterModifiers.None;

        [SerializeField, Min(0.05f), Tooltip("Seconds between one monster spawning and the next.")]
        private float spawnInterval = 0.5f;

        [SerializeField, Min(1), Tooltip("The most of this wave's monsters alive at once. The rest wait their turn.")]
        private int maxAlive = 12;

        [SerializeField, Tooltip("Left at the arena's supply points once the wave is beaten.")]
        private Pickup[] supplies = Array.Empty<Pickup>();

        public Wave(WaveGroup[] groups, MonsterModifiers difficulty, float spawnInterval, int maxAlive, Pickup[] supplies = null)
        {
            this.groups = groups ?? Array.Empty<WaveGroup>();
            this.difficulty = difficulty;
            this.spawnInterval = Mathf.Max(0.05f, spawnInterval);
            this.maxAlive = Mathf.Max(1, maxAlive);
            this.supplies = supplies ?? Array.Empty<Pickup>();
        }

        public IReadOnlyList<WaveGroup> Groups => groups;
        public MonsterModifiers Difficulty => difficulty;
        public float SpawnInterval => spawnInterval;
        public int MaxAlive => maxAlive;
        public IReadOnlyList<Pickup> Supplies => supplies;

        /// <summary>How many monsters the wave holds in all.</summary>
        public int TotalCount
        {
            get
            {
                int total = 0;
                foreach (WaveGroup group in groups)
                    total += group.Monster != null ? group.Count : 0;
                return total;
            }
        }

        /// <summary>How many monsters of one kind the wave holds.</summary>
        public int CountOf(MonsterDefinition monster)
        {
            int total = 0;
            foreach (WaveGroup group in groups)
            {
                if (group.Monster == monster)
                    total += group.Count;
            }

            return total;
        }
    }

    /// <summary>
    /// The waves of an arena fight, in order, and the pauses around them. The last wave is the final one.
    /// Each wave is tuned by hand rather than by a formula, so difficulty can rise through the mix of monsters and the pace
    /// of the fight as well as their numbers and toughness.
    /// </summary>
    [CreateAssetMenu(fileName = "WaveSet", menuName = "Wolfenstein/Wave Set")]
    public sealed class WaveSet : ScriptableObject
    {
        [SerializeField] private Wave[] waves = Array.Empty<Wave>();

        [SerializeField, Min(0f), Tooltip("Seconds a wave is announced before its first monster spawns.")]
        private float announcementDuration = 2.5f;

        [SerializeField, Min(0f), Tooltip("Seconds from one wave being beaten to the next being announced.")]
        private float intermission = 5f;

        public IReadOnlyList<Wave> Waves => waves;
        public float AnnouncementDuration => announcementDuration;
        public float Intermission => intermission;

        /// <summary>Makes a wave set in code rather than as an asset, such as for a quick test fight.</summary>
        public static WaveSet Create(float announcementDuration, float intermission, params Wave[] waves)
        {
            var set = CreateInstance<WaveSet>();
            set.announcementDuration = Mathf.Max(0f, announcementDuration);
            set.intermission = Mathf.Max(0f, intermission);
            set.waves = waves ?? Array.Empty<Wave>();
            return set;
        }
    }
}
