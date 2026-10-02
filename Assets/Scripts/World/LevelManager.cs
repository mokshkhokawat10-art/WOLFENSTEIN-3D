using System;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using Wolfenstein.Combat;
using Wolfenstein.Core;
using Wolfenstein.Player;
using Wolfenstein.Progression;
using Wolfenstein.Weapons;

namespace Wolfenstein.World
{
    /// <summary>
    /// Runs the lifecycle of the loaded level: builds the world, places the player at the spawn point,
    /// gives the player control only while the game is in the Playing state, ends the game when the
    /// player dies and restarts the level from the death screen.
    /// A level with a <see cref="LevelDefinition"/> is also a step of the episode: the player starts with its health and
    /// ammo and with the score carried from earlier levels, its objectives and statistics start, reaching its exit
    /// completes it, the end-of-level tally follows, and continuing from the tally loads the next level, or after the last
    /// returns to the title. Without one the scene is a test range, with none of that.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class LevelManager : MonoBehaviour
    {
        [SerializeField] private GameManager gameManager;
        [SerializeField] private WorldManager worldManager;
        [SerializeField] private PlayerController player;
        [SerializeField] private Health playerHealth;

        [Header("Game Over")]
        [SerializeField, Tooltip("Pressed on the death screen to play the level again.")]
        private InputActionReference restartAction;

        [SerializeField, Min(0f), Tooltip("Seconds after death before a restart is accepted, so a shot fired at the moment of death does not skip the death screen.")]
        private float restartDelay = 1f;

        [Header("Progression")]
        [SerializeField, Tooltip("The level being played. Leave empty for a test range with no objectives, exit or tally.")]
        private LevelDefinition level;

        [SerializeField] private ObjectiveTracker objectives;
        [SerializeField] private LevelExit exit;
        [SerializeField] private LevelStatistics statistics;
        [SerializeField] private Score playerScore;
        [SerializeField] private AmmoSupply playerAmmo;

        [SerializeField, Tooltip("Pressed on the tally to go on to the next level.")]
        private InputActionReference continueAction;

        [SerializeField, Min(0f), Tooltip("Seconds the level's end is announced before the tally.")]
        private float completeDuration = 2.5f;

        [SerializeField, Min(0f), Tooltip("Seconds the tally is shown before it can be left, so every figure is seen.")]
        private float tallyDelay = 2.5f;

        [SerializeField, Min(0f), Tooltip("Seconds from continuing to the next level loading, for the way out to be heard.")]
        private float transitionDelay = 0.8f;

        [SerializeField, Tooltip("The scene returned to after the last level of the episode.")]
        private string titleScene = "MainMenu";

        // Private copies of the actions. The player fires with the same action and enables and disables it
        // along with its other input, so sharing one instance would let either side switch off the other's input.
        private InputAction restartInput;
        private InputAction continueInput;
        private float restartAllowedTime;
        private bool canRestart;
        private float stateStartTime;
        private float leaveTime = float.PositiveInfinity;

        /// <summary>Raised when the death screen starts accepting the restart input.</summary>
        public event Action RestartAvailable;

        /// <summary>Raised when the player dies during play and the game is over.</summary>
        public event Action PlayerDown;

        /// <summary>Raised when the player reaches the exit and the level is complete.</summary>
        public event Action LevelCompleted;

        /// <summary>Raised when the end-of-level tally comes up.</summary>
        public event Action TallyShown;

        /// <summary>Raised when the tally starts accepting the continue input.</summary>
        public event Action ContinueAvailable;

        /// <summary>Raised when the player continues from the tally, just before the next level loads.</summary>
        public event Action LeavingLevel;

        public float RestartDelay => restartDelay;
        public LevelDefinition Level => level;
        public bool CanContinue { get; private set; }
        public float CompleteDuration => completeDuration;
        public float TallyDelay => tallyDelay;
        public float TransitionDelay => transitionDelay;

        private void Awake()
        {
            restartInput = restartAction.action.Clone();
            if (continueAction != null)
                continueInput = continueAction.action.Clone();
        }

        private void OnEnable()
        {
            gameManager.StateChanged += HandleGameStateChanged;
            gameManager.CursorCaptureChanged += HandleCursorCaptureChanged;
            playerHealth.Died += HandlePlayerDied;
            if (exit != null)
                exit.Reached += CompleteLevel;
            restartInput.Enable();
            continueInput?.Enable();
            UpdatePlayerControl();
        }

        private void OnDisable()
        {
            gameManager.StateChanged -= HandleGameStateChanged;
            gameManager.CursorCaptureChanged -= HandleCursorCaptureChanged;
            playerHealth.Died -= HandlePlayerDied;
            if (exit != null)
                exit.Reached -= CompleteLevel;
            restartInput.Disable();
            continueInput?.Disable();
        }

        private void OnDestroy()
        {
            restartInput.Dispose();
            continueInput?.Dispose();
        }

        private void Start()
        {
            worldManager.Build();
            RespawnPlayer();
            if (level != null)
                StartLevel();
            gameManager.SetState(GameState.Playing);
        }

        private void Update()
        {
            switch (gameManager.CurrentState)
            {
                case GameState.GameOver:
                    UpdateGameOver();
                    break;

                case GameState.LevelComplete:
                    if (Time.time >= stateStartTime + completeDuration)
                        ShowTally();
                    break;

                case GameState.Tally:
                    UpdateTally();
                    break;
            }
        }

        public void RespawnPlayer() => player.PlaceAt(worldManager.GetSpawnPose());

        /// <summary>
        /// Reloads the level scene, which resets the world, the doors and the player together. A level of the episode
        /// starts again from the score it was started with.
        /// </summary>
        public void RestartLevel()
        {
            if (level != null)
                GameSession.LoadLevel(gameObject.scene.name, statistics != null ? statistics.StartingScore : 0);
            else
                SceneManager.LoadScene(gameObject.scene.buildIndex);
        }

        /// <summary>Ends the level as complete: the player's control and the clock stop, and the tally follows.</summary>
        public void CompleteLevel()
        {
            if (gameManager.CurrentState != GameState.Playing)
                return;

            if (statistics != null)
                statistics.Stop();
            stateStartTime = Time.time;
            gameManager.SetState(GameState.LevelComplete);
            LevelCompleted?.Invoke();
        }

        /// <summary>Leaves the tally for the next level, once the tally accepts it.</summary>
        public void Continue()
        {
            if (!CanContinue || leaveTime < float.PositiveInfinity)
                return;

            leaveTime = Time.time + transitionDelay;
            LeavingLevel?.Invoke();
        }

        // The player starts the level with its health and ammo and the score they carried in, and then its objectives and
        // statistics start, now that the level holds everything they count.
        private void StartLevel()
        {
            playerHealth.Restore(playerHealth.Max, level.StartingHealth);
            if (playerAmmo != null)
                playerAmmo.ResetTo(level.StartingAmmo);
            if (playerScore != null)
                playerScore.Add(GameSession.ClaimCarriedScore(gameObject.scene.name));
            if (statistics != null)
                statistics.Begin();
            if (objectives != null)
                objectives.Begin();
        }

        private void UpdateGameOver()
        {
            if (!canRestart && Time.time >= restartAllowedTime)
            {
                canRestart = true;
                RestartAvailable?.Invoke();
            }

            if (canRestart && restartInput.WasPressedThisFrame())
                RestartLevel();
        }

        private void ShowTally()
        {
            stateStartTime = Time.time;
            CanContinue = false;
            gameManager.SetState(GameState.Tally);
            TallyShown?.Invoke();
        }

        private void UpdateTally()
        {
            if (Time.time >= leaveTime)
            {
                leaveTime = float.PositiveInfinity;
                LoadNextLevel();
                return;
            }

            if (!CanContinue && Time.time >= stateStartTime + tallyDelay)
            {
                CanContinue = true;
                ContinueAvailable?.Invoke();
            }

            if (CanContinue && continueInput != null && continueInput.WasPressedThisFrame())
                Continue();
        }

        private void LoadNextLevel()
        {
            int score = playerScore != null ? playerScore.Current : 0;
            if (level != null && level.NextLevel != null)
                GameSession.LoadLevel(level.NextLevel.SceneName, score);
            else
                GameSession.FinishEpisode(score, titleScene);
        }

        private void HandleGameStateChanged(GameState state) => UpdatePlayerControl();

        private void HandleCursorCaptureChanged(bool captured) => UpdatePlayerControl();

        // The player cannot fire while the mouse is free, so the click that captures it again is not also a shot.
        private void UpdatePlayerControl()
        {
            player.InputEnabled = gameManager.CurrentState == GameState.Playing;
            player.FireEnabled = gameManager.IsCursorCaptured;
        }

        // Once the level is complete the game is no longer lost, whatever happens to the player.
        private void HandlePlayerDied()
        {
            if (gameManager.CurrentState is GameState.LevelComplete or GameState.Tally)
                return;

            restartAllowedTime = Time.time + restartDelay;
            canRestart = false;
            gameManager.SetState(GameState.GameOver);
            PlayerDown?.Invoke();
        }
    }
}
