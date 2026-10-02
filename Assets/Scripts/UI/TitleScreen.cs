using System;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using Wolfenstein.Core;
using Wolfenstein.Progression;

namespace Wolfenstein.UI
{
    /// <summary>
    /// The title screen: the game's name, the controls, and a blinking prompt to start, which loads the first level of the
    /// episode with a fresh score. Back here after the last level, it shows that the episode is complete and its final score.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class TitleScreen : MonoBehaviour
    {
        [SerializeField] private GameManager gameManager;

        [SerializeField, Tooltip("The level a new game starts on.")]
        private LevelDefinition firstLevel;

        [SerializeField, Tooltip("Pressed to start the game.")]
        private InputActionReference startAction;

        [Header("Widgets")]
        [SerializeField] private Text title;
        [SerializeField] private Text message;
        [SerializeField] private GameObject prompt;

        [SerializeField, Min(0f), Tooltip("Seconds before the start input is accepted, so a key held from the last level does not skip the screen.")]
        private float inputDelay = 0.75f;

        [SerializeField, Min(0f), Tooltip("Seconds from starting to the first level loading, for the start to be heard.")]
        private float startDelay = 0.6f;

        [SerializeField, Min(0.1f), Tooltip("Blinks of the prompt per second.")]
        private float blinkRate = 1.2f;

        private InputAction startInput;
        private float readyTime;
        private float loadTime = float.PositiveInfinity;

        /// <summary>Raised when the player starts the game, just before the first level loads.</summary>
        public event Action GameStarting;

        public bool IsEpisodeComplete { get; private set; }
        public int FinalScore { get; private set; }
        public string Message => message.text;
        public LevelDefinition FirstLevel => firstLevel;

        private void Awake() => startInput = startAction.action.Clone();

        private void OnEnable() => startInput.Enable();

        private void OnDisable() => startInput.Disable();

        private void OnDestroy() => startInput.Dispose();

        private void Start()
        {
            if (gameManager != null)
                gameManager.SetState(GameState.Title);

            title.text = Application.productName.ToUpperInvariant();
            IsEpisodeComplete = GameSession.ClaimFinishedEpisode(out int score);
            FinalScore = score;
            message.text = IsEpisodeComplete ? $"EPISODE COMPLETE - FINAL SCORE {score}" : string.Empty;
            readyTime = Time.time + inputDelay;
        }

        private void Update()
        {
            if (Time.time >= loadTime)
            {
                loadTime = float.PositiveInfinity;
                GameSession.LoadLevel(firstLevel.SceneName, 0);
                return;
            }

            prompt.SetActive(loadTime < float.PositiveInfinity || Mathf.Repeat(Time.time * blinkRate, 1f) < 0.65f);
            if (Time.time >= readyTime && startInput.WasPressedThisFrame())
                StartGame();
        }

        /// <summary>Starts a new game on the first level.</summary>
        public void StartGame()
        {
            if (loadTime < float.PositiveInfinity)
                return;

            loadTime = Time.time + startDelay;
            GameStarting?.Invoke();
        }
    }
}
