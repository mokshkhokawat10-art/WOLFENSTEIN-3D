using UnityEngine;
using Wolfenstein.Combat;
using Wolfenstein.Core;
using Wolfenstein.Enemies;
using Wolfenstein.Monsters;
using Wolfenstein.Waves;

namespace Wolfenstein.Audio
{
    /// <summary>
    /// Chooses the music from what is happening: exploration music while nothing hunts the player, combat music while
    /// something does or an ordinary wave is on, intense music for an elite or final wave, a victory piece when the arena is
    /// cleared or the level is complete, and silence once the player is dead; on a menu screen it plays the menu music.
    /// Tracks crossfade on two decks of its own, the only audio sources the music uses. Every track comes from the
    /// <see cref="MusicSet"/>; a state whose track has no clip is silent.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class MusicDirector : MonoBehaviour
    {
        [SerializeField] private AudioManager audioManager;
        [SerializeField] private MusicSet music;

        [SerializeField, Tooltip("Plays the menu music and nothing else, as on the title screen.")]
        private bool menuScreen;

        [Header("What It Follows")]
        [SerializeField, Tooltip("Says when the level is complete. Optional.")]
        private GameManager gameManager;

        [SerializeField] private WaveManager waves;

        [SerializeField, Tooltip("Says how big an event the current wave is.")]
        private WaveAudio waveAudio;

        [SerializeField] private MonsterManager monsters;
        [SerializeField] private EnemyManager enemies;
        [SerializeField] private Health player;

        [SerializeField, Min(1f), Tooltip("A monster or guard hunting the player from within this distance brings on the combat music, in metres.")]
        private float threatRange = 28f;

        [SerializeField, Min(0f), Tooltip("Seconds the combat music holds after the last threat, so it does not flicker in and out.")]
        private float combatHold = 8f;

        [SerializeField, Min(0.05f), Tooltip("Seconds between checks for threats.")]
        private float checkInterval = 0.5f;

        private readonly AudioSource[] decks = new AudioSource[2];
        private readonly float[] levels = new float[2];
        private readonly float[] targets = new float[2];
        private readonly MusicTrack[] tracks = new MusicTrack[2];
        private int currentDeck;
        private float lastThreatTime = float.NegativeInfinity;
        private float victoryEndTime = float.NegativeInfinity;
        private float nextCheckTime;

        /// <summary>What the music is playing for right now.</summary>
        public MusicState State { get; private set; } = MusicState.Silent;

        /// <summary>The clip on the deck fading in or playing, or null while the music is silent.</summary>
        public AudioClip CurrentClip => targets[currentDeck] > 0f ? decks[currentDeck].clip : null;

        public MusicSet Music => music;

        private void Awake()
        {
            for (int i = 0; i < decks.Length; i++)
            {
                var deckObject = new GameObject($"Deck {i}");
                deckObject.transform.SetParent(transform, false);
                AudioSource deck = deckObject.AddComponent<AudioSource>();
                deck.playOnAwake = false;
                deck.spatialBlend = 0f;
                deck.priority = 0;
                deck.outputAudioMixerGroup = audioManager.OutputFor(AudioCategory.Music);
                decks[i] = deck;
            }
        }

        private void OnEnable()
        {
            if (waves != null)
                waves.AreaCleared += HandleAreaCleared;
        }

        private void OnDisable()
        {
            if (waves != null)
                waves.AreaCleared -= HandleAreaCleared;
        }

        private void Update()
        {
            float now = Time.time;
            if (now >= nextCheckTime)
            {
                nextCheckTime = now + checkInterval;
                if (IsPlayerHunted())
                    lastThreatTime = now;
            }

            MusicState wanted = ChooseState(now);
            if (wanted != State)
                SwitchTo(wanted);

            UpdateDecks(Time.deltaTime);
        }

        private MusicState ChooseState(float now)
        {
            if (music == null)
                return MusicState.Silent;
            if (menuScreen)
                return MusicState.Menu;
            if (gameManager != null && gameManager.CurrentState is GameState.LevelComplete or GameState.Tally)
                return MusicState.Victory;
            if (player != null && player.IsDead)
                return MusicState.Silent;
            if (now < victoryEndTime)
                return MusicState.Victory;

            if (waves != null && waves.State is WaveState.Announcing or WaveState.Fighting or WaveState.Complete)
            {
                bool intense = waveAudio != null && waveAudio.CurrentIntensity >= WaveIntensity.Elite;
                return intense ? MusicState.Intense : MusicState.Combat;
            }

            return now - lastThreatTime < combatHold ? MusicState.Combat : MusicState.Exploration;
        }

        // The playing deck fades out while the other starts the new track from its beginning and fades in.
        private void SwitchTo(MusicState state)
        {
            State = state;
            targets[currentDeck] = 0f;

            MusicTrack track = music != null ? music.Get(state) : default;
            if (track.Clip == null)
                return;

            currentDeck = 1 - currentDeck;
            AudioSource deck = decks[currentDeck];
            tracks[currentDeck] = track;
            deck.clip = track.Clip;
            deck.loop = track.Loop;
            levels[currentDeck] = 0f;
            targets[currentDeck] = 1f;
            deck.volume = 0f;
            deck.Play();
        }

        private void UpdateDecks(float deltaTime)
        {
            float step = deltaTime / (music != null ? music.Crossfade : 1f);
            float gain = audioManager.Gain(AudioCategory.Music) * (1f - audioManager.MusicDuck);
            for (int i = 0; i < decks.Length; i++)
            {
                AudioSource deck = decks[i];
                levels[i] = Mathf.MoveTowards(levels[i], targets[i], step);
                if (levels[i] <= 0f && targets[i] <= 0f)
                {
                    if (deck.isPlaying)
                        deck.Stop();
                    continue;
                }

                deck.volume = levels[i] * tracks[i].Volume * gain;
            }
        }

        // Anything close by that is hunting the player: a monster after them, or a guard chasing or aiming.
        private bool IsPlayerHunted()
        {
            if (player == null || player.IsDead)
                return false;

            Vector3 position = player.transform.position;
            float range = threatRange * threatRange;
            if (monsters != null)
            {
                foreach (MonsterController monster in monsters.Alive)
                {
                    if (monster.Target == player && (monster.State is MonsterState.Chase or MonsterState.Attack or MonsterState.Charge) &&
                        (monster.transform.position - position).sqrMagnitude <= range)
                        return true;
                }
            }

            if (enemies != null)
            {
                foreach (EnemyController guard in enemies.Enemies)
                {
                    if (guard != null && (guard.State is EnemyState.Chase or EnemyState.Attack) &&
                        (guard.transform.position - position).sqrMagnitude <= range)
                        return true;
                }
            }

            return false;
        }

        private void HandleAreaCleared()
        {
            MusicTrack victory = music != null ? music.Get(MusicState.Victory) : default;
            float length = victory.Clip != null ? victory.Clip.length : 0f;
            victoryEndTime = Time.time + Mathf.Max(4f, length);
        }
    }
}
