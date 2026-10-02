using UnityEngine;
using Wolfenstein.Core;
using Wolfenstein.World;

namespace Wolfenstein.Audio
{
    /// <summary>
    /// Sounds that belong to the game rather than the world: a sting when the player dies, and a cue when the death screen
    /// starts accepting a restart. It only listens to the <see cref="GameManager"/> and the <see cref="LevelManager"/>.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class InterfaceAudio : MonoBehaviour
    {
        [SerializeField] private AudioManager audioManager;
        [SerializeField] private GameManager gameManager;
        [SerializeField] private LevelManager levelManager;

        [SerializeField, Tooltip("When the player dies and the game is over.")]
        private SoundCue gameOver;

        [SerializeField, Tooltip("When the death screen starts accepting a restart.")]
        private SoundCue restartReady;

        private void OnEnable()
        {
            gameManager.StateChanged += HandleStateChanged;
            levelManager.RestartAvailable += HandleRestartAvailable;
        }

        private void OnDisable()
        {
            gameManager.StateChanged -= HandleStateChanged;
            levelManager.RestartAvailable -= HandleRestartAvailable;
        }

        private void HandleStateChanged(GameState state)
        {
            if (state == GameState.GameOver)
                audioManager.Play(gameOver);
        }

        private void HandleRestartAvailable() => audioManager.Play(restartReady);
    }
}
