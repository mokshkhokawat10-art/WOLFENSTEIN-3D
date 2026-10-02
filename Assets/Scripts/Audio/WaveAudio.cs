using UnityEngine;
using Wolfenstein.Waves;

namespace Wolfenstein.Audio
{
    /// <summary>
    /// Makes each arena wave an event alongside the banner on the heads-up display: a warning as it is announced, stronger
    /// for a major wave and distinct for an elite or the final one, a cue as its monsters start to come, a sting when it is
    /// beaten and a fanfare when the arena is cleared. It only listens to the <see cref="WaveManager"/>.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class WaveAudio : MonoBehaviour
    {
        [SerializeField] private AudioManager audioManager;
        [SerializeField] private WaveManager waves;

        [Header("Warnings")]
        [SerializeField] private SoundCue warning;
        [SerializeField] private SoundCue majorWarning;
        [SerializeField] private SoundCue eliteWarning;
        [SerializeField] private SoundCue finalWarning;

        [SerializeField, Min(1), Tooltip("A wave with at least this many monsters is a major wave.")]
        private int majorWaveSize = 20;

        [Header("Fight")]
        [SerializeField, Tooltip("As a wave's monsters start to come.")]
        private SoundCue incoming;

        [SerializeField, Tooltip("When a wave other than the last is beaten.")]
        private SoundCue waveComplete;

        [SerializeField, Tooltip("When the last wave is beaten and the arena opens.")]
        private SoundCue areaCleared;

        /// <summary>How big an event the current wave is.</summary>
        public WaveIntensity CurrentIntensity => Classify(waves.CurrentWave, waves.IsFinalWave, majorWaveSize);

        public int MajorWaveSize => majorWaveSize;
        public SoundCue Incoming => incoming;
        public SoundCue WaveComplete => waveComplete;
        public SoundCue AreaCleared => areaCleared;

        private void OnEnable()
        {
            waves.WaveAnnounced += HandleWaveAnnounced;
            waves.WaveStarted += HandleWaveStarted;
            waves.WaveCompleted += HandleWaveCompleted;
            waves.AreaCleared += HandleAreaCleared;
        }

        private void OnDisable()
        {
            waves.WaveAnnounced -= HandleWaveAnnounced;
            waves.WaveStarted -= HandleWaveStarted;
            waves.WaveCompleted -= HandleWaveCompleted;
            waves.AreaCleared -= HandleAreaCleared;
        }

        /// <summary>
        /// How big an event a wave is: the final wave is always the final one; a wave that holds back an elite for a big
        /// entrance is an elite wave; one with at least the major size of monsters is a major wave.
        /// </summary>
        public static WaveIntensity Classify(Wave wave, bool isFinal, int majorWaveSize)
        {
            if (isFinal)
                return WaveIntensity.Final;
            if (wave == null)
                return WaveIntensity.Normal;

            foreach (WaveGroup group in wave.Groups)
            {
                if (group.SpawnLast && group.Monster != null && group.Count > 0)
                    return WaveIntensity.Elite;
            }

            return wave.TotalCount >= majorWaveSize ? WaveIntensity.Major : WaveIntensity.Normal;
        }

        /// <summary>The warning for a wave of this size, or the nearest smaller one when it has none of its own.</summary>
        public SoundCue WarningFor(WaveIntensity intensity)
        {
            SoundCue[] ladder = { warning, majorWarning, eliteWarning, finalWarning };
            for (int i = (int)intensity; i >= 0; i--)
            {
                if (ladder[i] != null)
                    return ladder[i];
            }

            return null;
        }

        private void HandleWaveAnnounced(int number, int enemies, bool final) => audioManager.Play(WarningFor(CurrentIntensity));

        private void HandleWaveStarted(int number) => audioManager.Play(incoming);

        private void HandleWaveCompleted(int number)
        {
            if (number < waves.WaveCount)
                audioManager.Play(waveComplete);
        }

        private void HandleAreaCleared() => audioManager.Play(areaCleared);
    }
}
