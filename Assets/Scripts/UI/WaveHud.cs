using UnityEngine;
using UnityEngine.UI;
using Wolfenstein.Waves;

namespace Wolfenstein.UI
{
    /// <summary>
    /// The arena fight on the heads-up display: a panel with the wave counter and the number of monsters left, and a banner
    /// across the screen that announces each wave, marks each one beaten and declares the arena cleared. The banner punches
    /// in, holds, and fades away. It only listens to the <see cref="WaveManager"/>.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class WaveHud : MonoBehaviour
    {
        [SerializeField] private WaveManager waves;

        [Header("Panel")]
        [SerializeField] private GameObject panel;
        [SerializeField] private Text waveText;
        [SerializeField] private Text enemiesText;

        [Header("Banner")]
        [SerializeField] private RectTransform banner;
        [SerializeField] private CanvasGroup bannerGroup;
        [SerializeField] private Text bannerTitle;
        [SerializeField] private Text bannerSubtitle;

        [SerializeField] private Color waveColour = new Color(1f, 0.35f, 0.2f);
        [SerializeField] private Color finalWaveColour = new Color(1f, 0.12f, 0.1f);
        [SerializeField] private Color completeColour = new Color(1f, 0.82f, 0.3f);
        [SerializeField] private Color clearedColour = new Color(0.45f, 1f, 0.5f);

        [SerializeField, Min(0.1f), Tooltip("Seconds the banner stays up before it starts to fade.")]
        private float bannerHold = 2.2f;

        [SerializeField, Min(0.01f)] private float bannerFade = 0.6f;

        [SerializeField, Min(0.01f), Tooltip("Seconds the banner takes to shrink from its first punch to its resting size.")]
        private float bannerPunch = 0.25f;

        [SerializeField, Min(1f)] private float bannerPunchScale = 1.4f;

        private float bannerShownTime = float.NegativeInfinity;

        public bool IsPanelVisible => panel.activeSelf;
        public string WaveReadout => waveText.text;
        public string EnemiesReadout => enemiesText.text;
        public bool IsBannerVisible => banner.gameObject.activeSelf && bannerGroup.alpha > 0f;
        public string BannerTitle => bannerTitle.text;
        public string BannerSubtitle => bannerSubtitle.text;

        private void OnEnable()
        {
            waves.WaveAnnounced += HandleWaveAnnounced;
            waves.RemainingChanged += ShowRemaining;
            waves.WaveCompleted += HandleWaveCompleted;
            waves.AreaCleared += HandleAreaCleared;
        }

        private void OnDisable()
        {
            waves.WaveAnnounced -= HandleWaveAnnounced;
            waves.RemainingChanged -= ShowRemaining;
            waves.WaveCompleted -= HandleWaveCompleted;
            waves.AreaCleared -= HandleAreaCleared;
        }

        private void Start()
        {
            panel.SetActive(waves.State is WaveState.Announcing or WaveState.Fighting or WaveState.Complete);
            banner.gameObject.SetActive(false);
        }

        private void Update()
        {
            if (!banner.gameObject.activeSelf)
                return;

            float age = Time.time - bannerShownTime;
            float punch = Mathf.Clamp01(age / bannerPunch);
            banner.localScale = Vector3.one * Mathf.Lerp(bannerPunchScale, 1f, 1f - (1f - punch) * (1f - punch));
            bannerGroup.alpha = 1f - Mathf.Clamp01((age - bannerHold) / bannerFade);
            if (bannerGroup.alpha <= 0f)
                banner.gameObject.SetActive(false);
        }

        private void HandleWaveAnnounced(int number, int enemies, bool final)
        {
            panel.SetActive(true);
            waveText.text = $"{number} OF {waves.WaveCount}";
            ShowRemaining(enemies);
            ShowBanner($"WAVE {number}", final ? $"FINAL WAVE - ENEMIES: {enemies}" : $"ENEMIES: {enemies}",
                final ? finalWaveColour : waveColour);
        }

        private void HandleWaveCompleted(int number)
        {
            if (number >= waves.WaveCount)
                return;

            bool supplies = waves.CurrentWave != null && waves.CurrentWave.Supplies.Count > 0;
            ShowBanner("WAVE COMPLETE", supplies ? "SUPPLIES DROPPED" : string.Empty, completeColour);
        }

        private void HandleAreaCleared()
        {
            panel.SetActive(false);
            ShowBanner("AREA CLEARED", "THE WAY OUT IS OPEN", clearedColour);
        }

        private void ShowRemaining(int remaining) => enemiesText.text = remaining.ToString();

        private void ShowBanner(string title, string subtitle, Color colour)
        {
            bannerTitle.text = title;
            bannerTitle.color = colour;
            bannerSubtitle.text = subtitle;
            bannerShownTime = Time.time;
            bannerGroup.alpha = 1f;
            banner.localScale = Vector3.one * bannerPunchScale;
            banner.gameObject.SetActive(true);
        }
    }
}
