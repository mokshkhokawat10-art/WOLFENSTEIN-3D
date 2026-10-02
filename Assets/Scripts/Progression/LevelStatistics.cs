using UnityEngine;
using Wolfenstein.Core;
using Wolfenstein.Enemies;
using Wolfenstein.Monsters;
using Wolfenstein.Pickups;
using Wolfenstein.Waves;
using Wolfenstein.Weapons;

namespace Wolfenstein.Progression
{
    /// <summary>
    /// Keeps the figures of the end-of-level tally, each counted from what really happens: the time from the start of the
    /// level to reaching the exit, the enemies killed out of every enemy the level holds or will send, the treasure picked up
    /// out of the treasure lying in the level, and how many of the player's shots struck an enemy. Every figure starts again
    /// with the level; the score carried in from earlier levels is kept apart from the points scored in this one.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class LevelStatistics : MonoBehaviour
    {
        [SerializeField] private LevelDefinition level;

        [Header("Player")]
        [SerializeField] private Score score;
        [SerializeField] private WeaponController weapon;
        [SerializeField] private PickupCollector collector;

        [Header("Level")]
        [SerializeField] private PickupManager pickups;
        [SerializeField] private EnemyManager enemies;
        [SerializeField] private MonsterManager monsters;
        [SerializeField] private EncounterDirector encounters;

        private float startTime;
        private float endTime;

        public bool IsRunning { get; private set; }

        /// <summary>Seconds from the start of the level to now, or to reaching the exit once it is reached.</summary>
        public float ElapsedSeconds => IsRunning ? Time.time - startTime : endTime - startTime;

        public int Kills => score.Kills;

        /// <summary>Every enemy of the level: guards, lurking monsters, and those its encounters and waves will send.</summary>
        public int TotalEnemies { get; private set; }

        public int TreasureFound { get; private set; }
        public int TreasureTotal { get; private set; }
        public int ShotsFired { get; private set; }

        /// <summary>Shots that struck something that could be hurt: a guard or a monster.</summary>
        public int ShotsHit { get; private set; }

        /// <summary>The score the player brought into the level from earlier ones.</summary>
        public int StartingScore { get; private set; }

        /// <summary>Points scored in this level alone.</summary>
        public int LevelScore => score.Current - StartingScore;

        /// <summary>The whole score so far, earlier levels included.</summary>
        public int TotalScore => score.Current;

        /// <summary>Treasure found as a whole percentage, or null in a level with no treasure.</summary>
        public int? TreasurePercent => TreasureTotal > 0 ? Mathf.RoundToInt(100f * TreasureFound / TreasureTotal) : (int?)null;

        /// <summary>Shots that hit as a whole percentage, or null if the player never fired.</summary>
        public int? AccuracyPercent => ShotsFired > 0 ? Mathf.RoundToInt(100f * ShotsHit / ShotsFired) : (int?)null;

        private void OnEnable()
        {
            weapon.Fired += HandleFired;
            collector.Collected += HandleCollected;
        }

        private void OnDisable()
        {
            weapon.Fired -= HandleFired;
            collector.Collected -= HandleCollected;
        }

        /// <summary>
        /// Starts the clock and counts what the level holds. Called once the level is built, its enemies and items placed,
        /// and any score carried in from earlier levels added.
        /// </summary>
        public void Begin()
        {
            startTime = Time.time;
            IsRunning = true;
            StartingScore = score.Current;

            int waveMonsters = 0;
            WaveSet waves = level != null ? level.Waves : null;
            if (waves != null)
            {
                foreach (Wave wave in waves.Waves)
                    waveMonsters += wave.TotalCount;
            }

            TotalEnemies = (enemies != null ? enemies.Enemies.Count : 0) + (monsters != null ? monsters.Alive.Count : 0) +
                           (encounters != null ? encounters.TotalMonsters : 0) + waveMonsters;

            TreasureTotal = 0;
            if (pickups != null)
            {
                foreach (Pickup item in pickups.LevelItems)
                {
                    if (item != null && item.Definition.Score > 0)
                        TreasureTotal++;
                }
            }
        }

        /// <summary>Stops the clock, when the level is complete.</summary>
        public void Stop()
        {
            if (!IsRunning)
                return;

            endTime = Time.time;
            IsRunning = false;
        }

        private void HandleFired(ShotResult shot)
        {
            if (!IsRunning)
                return;

            ShotsFired++;
            if (shot.Target != null)
                ShotsHit++;
        }

        private void HandleCollected(PickupDefinition item)
        {
            if (IsRunning && item.Score > 0)
                TreasureFound++;
        }
    }
}
