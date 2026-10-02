using System;
using UnityEngine;
using UnityEngine.UI;
using Wolfenstein.Progression;
using Wolfenstein.World;

namespace Wolfenstein.UI
{
    /// <summary>
    /// The end-of-level results, in the manner of the original game: the level's name, then its figures revealed one line
    /// at a time, and a prompt to continue once the level manager accepts it. Every figure comes from the level's
    /// <see cref="LevelStatistics"/>; one that cannot be worked out, such as accuracy when no shot was fired, shows a dash.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class TallyScreen : MonoBehaviour
    {
        [Serializable]
        private struct Row
        {
            public TallyStat stat;
            public GameObject root;
            public Text value;
        }

        [SerializeField] private LevelManager level;
        [SerializeField] private LevelStatistics statistics;

        [Header("Widgets")]
        [SerializeField] private GameObject screen;
        [SerializeField] private Text title;
        [SerializeField] private Text subtitle;
        [SerializeField] private Row[] rows = Array.Empty<Row>();
        [SerializeField] private GameObject prompt;

        [SerializeField, Min(0.05f), Tooltip("Seconds between one line of figures appearing and the next.")]
        private float rowInterval = 0.35f;

        private int revealed;
        private float nextRevealTime;

        /// <summary>Raised as each line of figures appears.</summary>
        public event Action RowRevealed;

        public bool IsVisible => screen.activeSelf;
        public bool IsPromptVisible => prompt.activeInHierarchy;
        public string Title => title.text;
        public string Subtitle => subtitle.text;
        public int RevealedRows => revealed;

        private void OnEnable()
        {
            if (level == null)
                return;

            level.TallyShown += Show;
            level.ContinueAvailable += ShowPrompt;
        }

        private void OnDisable()
        {
            if (level == null)
                return;

            level.TallyShown -= Show;
            level.ContinueAvailable -= ShowPrompt;
        }

        private void Start()
        {
            if (!IsVisible)
                screen.SetActive(false);
        }

        /// <summary>The figure shown on the line of a statistic, or null if the tally has no such line.</summary>
        public string ValueOf(TallyStat stat)
        {
            foreach (Row row in rows)
            {
                if (row.stat == stat)
                    return row.value.text;
            }

            return null;
        }

        /// <summary>A statistic as the tally writes it.</summary>
        public static string Format(TallyStat stat, LevelStatistics statistics)
        {
            switch (stat)
            {
                case TallyStat.Time:
                    int seconds = Mathf.FloorToInt(statistics.ElapsedSeconds);
                    return $"{seconds / 60:00}:{seconds % 60:00}";
                case TallyStat.Kills:
                    return $"{statistics.Kills} / {statistics.TotalEnemies}";
                case TallyStat.Treasure:
                    return statistics.TreasurePercent.HasValue ? $"{statistics.TreasurePercent.Value}%" : "-";
                case TallyStat.Accuracy:
                    return statistics.AccuracyPercent.HasValue ? $"{statistics.AccuracyPercent.Value}%" : "-";
                case TallyStat.Score:
                    return statistics.LevelScore.ToString();
                default:
                    return statistics.TotalScore.ToString();
            }
        }

        // Reveals the lines one at a time while the tally is up.
        private void Update()
        {
            if (!screen.activeSelf || revealed >= rows.Length || Time.time < nextRevealTime)
                return;

            rows[revealed].root.SetActive(true);
            revealed++;
            nextRevealTime = Time.time + rowInterval;
            RowRevealed?.Invoke();
        }

        private void Show()
        {
            LevelDefinition definition = level.Level;
            title.text = "LEVEL COMPLETE";
            subtitle.text = definition != null ? $"{definition.Title} - {definition.DisplayName}" : string.Empty;
            foreach (Row row in rows)
            {
                row.value.text = statistics != null ? Format(row.stat, statistics) : "-";
                row.root.SetActive(false);
            }

            revealed = 0;
            nextRevealTime = Time.time + rowInterval;
            prompt.SetActive(false);
            screen.SetActive(true);
        }

        private void ShowPrompt() => prompt.SetActive(true);
    }
}
