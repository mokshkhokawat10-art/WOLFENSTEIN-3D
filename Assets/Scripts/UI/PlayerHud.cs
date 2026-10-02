using UnityEngine;
using UnityEngine.UI;
using Wolfenstein.Combat;
using Wolfenstein.Core;
using Wolfenstein.Pickups;
using Wolfenstein.Weapons;
using Wolfenstein.World;

namespace Wolfenstein.UI
{
    /// <summary>
    /// The heads-up display: health, ammo and score readouts, a red flash when the player is hurt, a gold flash when they
    /// collect an item, and the death screen, which comes up when the level manager ends the game and whose restart prompt
    /// appears only once the level manager accepts the restart input.
    /// It only listens to the player's components and the level manager, and never changes them.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PlayerHud : MonoBehaviour
    {
        [Header("Sources")]
        [SerializeField] private Health health;
        [SerializeField] private AmmoSupply ammo;
        [SerializeField] private Score score;
        [SerializeField] private PickupCollector pickups;
        [SerializeField] private LevelManager level;

        [Header("Widgets")]
        [SerializeField] private Text healthText;
        [SerializeField] private Text ammoText;
        [SerializeField] private Text scoreText;
        [SerializeField] private Image damageFlash;
        [SerializeField] private Image pickupFlash;
        [SerializeField] private GameObject crosshair;
        [SerializeField] private GameObject deathScreen;
        [SerializeField] private GameObject restartPrompt;

        [Header("Health Readout")]
        [SerializeField, Range(0f, 1f), Tooltip("At or below this fraction of full health the readout shows the warning colour.")]
        private float lowHealthFraction = 0.25f;

        [SerializeField] private Color healthColour = Color.white;
        [SerializeField] private Color lowHealthColour = new Color(1f, 0.3f, 0.25f);

        [Header("Damage Flash")]
        [SerializeField, Range(0f, 1f), Tooltip("Opacity of the red flash at the moment the player is hurt.")]
        private float damageFlashAlpha = 0.4f;

        [SerializeField, Min(0.01f), Tooltip("Seconds the red flash takes to fade out.")]
        private float damageFlashDuration = 0.35f;

        [Header("Pickup Flash")]
        [SerializeField, Range(0f, 1f), Tooltip("Opacity of the gold flash at the moment an item is collected.")]
        private float pickupFlashAlpha = 0.25f;

        [SerializeField, Min(0.01f), Tooltip("Seconds the gold flash takes to fade out.")]
        private float pickupFlashDuration = 0.3f;

        private float damageFlashStrength;
        private float pickupFlashStrength;

        public string HealthReadout => healthText.text;
        public string AmmoReadout => ammoText.text;
        public string ScoreReadout => scoreText.text;
        public bool IsDeathScreenVisible => deathScreen.activeSelf;
        public bool IsRestartPromptVisible => restartPrompt.activeInHierarchy;
        public bool IsCrosshairVisible => crosshair.activeSelf;
        public float DamageFlashAlpha => damageFlash.enabled ? damageFlash.color.a : 0f;
        public float PickupFlashAlpha => pickupFlash.enabled ? pickupFlash.color.a : 0f;

        private void OnEnable()
        {
            health.Changed += ShowHealth;
            health.Damaged += HandleDamaged;
            level.PlayerDown += HandleDied;
            ammo.Changed += ShowAmmo;
            score.Changed += ShowScore;
            pickups.Collected += HandleCollected;
            level.RestartAvailable += ShowRestartPrompt;
        }

        private void OnDisable()
        {
            health.Changed -= ShowHealth;
            health.Damaged -= HandleDamaged;
            level.PlayerDown -= HandleDied;
            ammo.Changed -= ShowAmmo;
            score.Changed -= ShowScore;
            pickups.Collected -= HandleCollected;
            level.RestartAvailable -= ShowRestartPrompt;
        }

        // In Start rather than OnEnable, so the player's components have initialised their values first.
        private void Start()
        {
            ShowHealth(health.Current);
            ShowAmmo(ammo.Current);
            ShowScore(score.Current);
            ShowDeathScreen(health.IsDead);
            damageFlashStrength = ShowFlash(damageFlash, damageFlashAlpha, 0f);
            pickupFlashStrength = ShowFlash(pickupFlash, pickupFlashAlpha, 0f);
        }

        private void Update()
        {
            if (damageFlashStrength > 0f)
                damageFlashStrength = ShowFlash(damageFlash, damageFlashAlpha, damageFlashStrength - Time.deltaTime / damageFlashDuration);

            if (pickupFlashStrength > 0f)
                pickupFlashStrength = ShowFlash(pickupFlash, pickupFlashAlpha, pickupFlashStrength - Time.deltaTime / pickupFlashDuration);
        }

        private void ShowHealth(int current)
        {
            // Shown as a percentage like the original status bar. Rounding up keeps a living player above 0%.
            int percent = Mathf.CeilToInt(100f * current / health.Max);
            healthText.text = $"{percent}%";
            healthText.color = current <= health.Max * lowHealthFraction ? lowHealthColour : healthColour;
        }

        private void ShowAmmo(int current) => ammoText.text = current.ToString();

        private void ShowScore(int current) => scoreText.text = current.ToString();

        private void HandleDamaged(DamageInfo damage) => damageFlashStrength = ShowFlash(damageFlash, damageFlashAlpha, 1f);

        private void HandleCollected(PickupDefinition item) => pickupFlashStrength = ShowFlash(pickupFlash, pickupFlashAlpha, 1f);

        private void HandleDied() => ShowDeathScreen(true);

        private void ShowDeathScreen(bool visible)
        {
            deathScreen.SetActive(visible);
            crosshair.SetActive(!visible);
            restartPrompt.SetActive(false);
        }

        private void ShowRestartPrompt() => restartPrompt.SetActive(true);

        // Shows a full-screen flash at a strength from 0 (hidden) to 1 (its peak opacity), and returns that strength.
        private static float ShowFlash(Image flash, float peakAlpha, float strength)
        {
            strength = Mathf.Clamp01(strength);

            Color colour = flash.color;
            colour.a = peakAlpha * strength;
            flash.color = colour;
            flash.enabled = strength > 0f;
            return strength;
        }
    }
}
