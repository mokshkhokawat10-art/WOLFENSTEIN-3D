using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Audio;
using Wolfenstein.World;

namespace Wolfenstein.Audio
{
    /// <summary>
    /// Plays every sound effect in the game through one fixed set of pooled voices, so a crowded fight costs no more
    /// audio sources than a quiet corridor. Each request names a <see cref="SoundCue"/>; the manager enforces the cue's
    /// cooldown and copy limit, skips sounds too far away to hear, and when every voice is busy lets the least important,
    /// quietest sound give way. It keeps the master, effects, ambience, music and interface volumes, muffles sounds behind
    /// closed doors, turns effects down a little when many play at once so the mix does not clip, and tells the music
    /// when to duck. Music and ambience beds play through their own directors, which read their volumes from here.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class AudioManager : MonoBehaviour
    {
        private const float OpenCutoff = 22000f;
        private const float OcclusionCheckInterval = 0.2f;
        private const float RegionRefreshInterval = 0.25f;

        private static readonly Vector2Int[] Neighbours = { Vector2Int.up, Vector2Int.right, Vector2Int.down, Vector2Int.left };

        private sealed class Voice
        {
            public int Index;
            public AudioSource Source;
            public AudioLowPassFilter Muffle;
            public SoundCue Cue;
            public Transform Follow;
            public Vector3 Position;
            public float Distance;
            public float Audibility;
            public int Generation;
            public bool Active;
            public float BaseVolume;
            public float EndTime;
            public float Fade = 1f;
            public float FadeRate;
            public float Occlusion;
            public bool Occluded;
            public float NextOcclusionCheck;
        }

        [SerializeField, Tooltip("Hears every sound, normally the player's camera. The scene's AudioListener is used when empty.")]
        private Transform listener;

        [SerializeField, Tooltip("Lets sounds behind closed doors be muffled. Optional.")]
        private WorldManager world;

        [SerializeField, Min(4), Tooltip("Sound effects that can play at once. The rest are dropped by importance.")]
        private int voiceCount = 26;

        [Header("Volumes")]
        [SerializeField, Range(0f, 1f)] private float masterVolume = 1f;
        [SerializeField, Range(0f, 1f)] private float sfxVolume = 1f;
        [SerializeField, Range(0f, 1f)] private float ambienceVolume = 1f;
        [SerializeField, Range(0f, 1f)] private float musicVolume = 1f;
        [SerializeField, Range(0f, 1f)] private float uiVolume = 1f;

        [Header("Mixer Outputs (optional)")]
        [SerializeField] private AudioMixerGroup sfxOutput;
        [SerializeField] private AudioMixerGroup ambienceOutput;
        [SerializeField] private AudioMixerGroup musicOutput;
        [SerializeField] private AudioMixerGroup uiOutput;

        [Header("Closed Doors")]
        [SerializeField, Range(0f, 1f), Tooltip("Volume of a sound behind a closed door, as a fraction of its usual volume.")]
        private float occludedVolume = 0.4f;

        [SerializeField, Min(100f), Tooltip("Frequencies above this are cut from a sound behind a closed door, in hertz.")]
        private float occludedCutoff = 900f;

        [SerializeField, Min(0.01f), Tooltip("Seconds a sound takes to become muffled or clear again as doors close and open.")]
        private float occlusionFade = 0.3f;

        [Header("Headroom")]
        [SerializeField, Min(1), Tooltip("Sound effects playing at once before all of them are turned down a little, so the mix does not clip.")]
        private int crowdThreshold = 10;

        [SerializeField, Range(0.3f, 1f), Tooltip("The furthest effects are ever turned down in a crowd.")]
        private float crowdFloor = 0.6f;

        [SerializeField, Range(0f, 1f), Tooltip("How far the music drops while a sound that ducks it plays.")]
        private float musicDuckDepth = 0.45f;

        private readonly Dictionary<SoundCue, float> cooldowns = new Dictionary<SoundCue, float>();
        private readonly HashSet<Vector2Int> audibleCells = new HashSet<Vector2Int>();
        private Voice[] voices = Array.Empty<Voice>();
        private int generation;
        private float crowdGain = 1f;
        private Vector2Int regionCell = new Vector2Int(int.MinValue, int.MinValue);
        private float nextRegionRefresh;
        private bool regionKnown;

        /// <summary>Raised for every sound asked for, whether or not it could be played, with where it was asked for.</summary>
        public event Action<SoundCue, Vector3> CueRequested;

        /// <summary>Raised when any volume changes.</summary>
        public event Action VolumesChanged;

        /// <summary>The voices sound effects are played through, which never grow in number.</summary>
        public int VoiceCount => voices.Length;

        public int ActiveVoiceCount
        {
            get
            {
                int count = 0;
                foreach (Voice voice in voices)
                {
                    if (voice.Active)
                        count++;
                }

                return count;
            }
        }

        public float MasterVolume
        {
            get => masterVolume;
            set => SetMaster(value);
        }

        /// <summary>How far the music is lowered right now, from 0 for not at all.</summary>
        public float MusicDuck { get; private set; }

        /// <summary>The fraction effects are turned down by in a crowd, 1 when there is no crowd.</summary>
        public float CrowdGain => crowdGain;

        public Vector3 ListenerPosition => listener != null ? listener.position : transform.position;

        private void Awake()
        {
            if (listener == null)
            {
                var found = FindFirstObjectByType<AudioListener>();
                if (found != null)
                    listener = found.transform;
            }

            voices = new Voice[voiceCount];
            for (int i = 0; i < voiceCount; i++)
                voices[i] = CreateVoice(i);
        }

        private void OnDisable()
        {
            foreach (Voice voice in voices)
                Release(voice);
        }

        /// <summary>Plays a sound that belongs to no place, such as an interface sound or the player's own gun.</summary>
        public SoundHandle Play(SoundCue cue) => Begin(cue, ListenerPosition, null);

        /// <summary>Plays a sound from a point in the level. A cue that is not spatial plays as if at the listener.</summary>
        public SoundHandle Play(SoundCue cue, Vector3 position) => Begin(cue, position, null);

        /// <summary>Plays a sound that moves with something, such as a monster's growl. It stays where it was if that goes away.</summary>
        public SoundHandle Play(SoundCue cue, Transform follow) =>
            follow != null ? Begin(cue, follow.position, follow) : Begin(cue, ListenerPosition, null);

        /// <summary>Stops a sound, fading it out over the given time. Does nothing if it has already ended.</summary>
        public void Stop(SoundHandle handle, float fadeSeconds = 0.08f)
        {
            if (!TryGetVoice(handle, out Voice voice))
                return;

            if (fadeSeconds <= 0f)
                Release(voice);
            else
                voice.FadeRate = Mathf.Max(voice.FadeRate, 1f / fadeSeconds);
        }

        public bool IsPlaying(SoundHandle handle) => TryGetVoice(handle, out _);

        /// <summary>The volume a sound is playing at right now, after every volume, fade and muffling. 0 once it has ended.</summary>
        public float VolumeOf(SoundHandle handle) => TryGetVoice(handle, out Voice voice) ? voice.Source.volume : 0f;

        /// <summary>Whether a sound is muffled right now by a closed door between it and the listener.</summary>
        public bool IsMuffled(SoundHandle handle) => TryGetVoice(handle, out Voice voice) && voice.Occluded;

        /// <summary>How many copies of a cue are playing right now.</summary>
        public int CountPlaying(SoundCue cue)
        {
            int count = 0;
            foreach (Voice voice in voices)
            {
                if (voice.Active && voice.Cue == cue)
                    count++;
            }

            return count;
        }

        public float GetVolume(AudioCategory category) => category switch
        {
            AudioCategory.Sfx => sfxVolume,
            AudioCategory.Ambience => ambienceVolume,
            AudioCategory.Music => musicVolume,
            _ => uiVolume
        };

        public void SetVolume(AudioCategory category, float volume)
        {
            volume = Mathf.Clamp01(volume);
            switch (category)
            {
                case AudioCategory.Sfx:
                    sfxVolume = volume;
                    break;
                case AudioCategory.Ambience:
                    ambienceVolume = volume;
                    break;
                case AudioCategory.Music:
                    musicVolume = volume;
                    break;
                default:
                    uiVolume = volume;
                    break;
            }

            VolumesChanged?.Invoke();
        }

        /// <summary>The master volume times a category's volume: what everything in the category is scaled by.</summary>
        public float Gain(AudioCategory category) => masterVolume * GetVolume(category);

        public AudioMixerGroup OutputFor(AudioCategory category) => category switch
        {
            AudioCategory.Sfx => sfxOutput,
            AudioCategory.Ambience => ambienceOutput,
            AudioCategory.Music => musicOutput,
            _ => uiOutput
        };

        /// <summary>
        /// Whether a closed door stands between a point and the listener: the point is not in any cell joined to the
        /// listener's without passing a closed door. A closed door is heard from either side of it.
        /// </summary>
        public bool IsOccluded(Vector3 position)
        {
            RefreshRegion(Time.unscaledTime);
            if (!regionKnown)
                return false;

            Vector2Int cell = world.WorldToCell(position);
            if (audibleCells.Contains(cell))
                return false;

            if (!world.Layout.TryGetDoor(cell, out _))
                return true;

            foreach (Vector2Int offset in Neighbours)
            {
                if (audibleCells.Contains(cell + offset))
                    return false;
            }

            return true;
        }

        private void LateUpdate()
        {
            float now = Time.unscaledTime;
            float deltaTime = Time.unscaledDeltaTime;
            RefreshRegion(now);

            int effects = 0;
            bool ducking = false;
            Vector3 listenerPosition = ListenerPosition;
            foreach (Voice voice in voices)
            {
                if (!voice.Active)
                    continue;

                if (now >= voice.EndTime)
                {
                    Release(voice);
                    continue;
                }

                if (voice.FadeRate > 0f)
                {
                    voice.Fade -= voice.FadeRate * deltaTime;
                    if (voice.Fade <= 0f)
                    {
                        Release(voice);
                        continue;
                    }
                }

                if (voice.Follow != null)
                {
                    if (voice.Follow.gameObject.activeInHierarchy)
                        voice.Position = voice.Follow.position;
                    else
                        voice.Follow = null;
                }

                if (voice.Cue.IsSpatial)
                {
                    voice.Source.transform.position = voice.Position;
                    voice.Distance = Vector3.Distance(voice.Position, listenerPosition);
                    UpdateOcclusion(voice, now, deltaTime);
                }

                if (voice.Cue.Category == AudioCategory.Sfx)
                    effects++;
                ducking |= voice.Cue.DucksMusic;
            }

            float crowdTarget = effects <= crowdThreshold ? 1f : Mathf.Max(crowdFloor, Mathf.Sqrt(crowdThreshold / (float)effects));
            crowdGain = Mathf.MoveTowards(crowdGain, crowdTarget, deltaTime * 2f);
            MusicDuck = Mathf.MoveTowards(MusicDuck, ducking ? musicDuckDepth : 0f, deltaTime * (ducking ? 4f : 1f));

            foreach (Voice voice in voices)
            {
                if (voice.Active)
                    ApplyVolume(voice);
            }
        }

        private SoundHandle Begin(SoundCue cue, Vector3 position, Transform follow)
        {
            if (cue == null)
                return default;

            CueRequested?.Invoke(cue, position);
            if (!cue.HasClips || !isActiveAndEnabled)
                return default;

            float now = Time.unscaledTime;
            if (cue.Cooldown > 0f && cooldowns.TryGetValue(cue, out float ready) && now < ready)
                return default;

            bool spatial = cue.IsSpatial;
            float distance = spatial ? Vector3.Distance(position, ListenerPosition) : 0f;
            if (spatial && distance >= cue.MaxDistance)
                return default;

            float audibility = cue.Volume * (spatial ? cue.Rolloff.Evaluate(distance / cue.MaxDistance) : 1f);
            Voice voice = FindVoiceFor(cue, spatial, distance, audibility);
            if (voice == null)
                return default;

            if (cue.Cooldown > 0f)
                cooldowns[cue] = now + cue.Cooldown;

            Release(voice);
            StartVoice(voice, cue, position, follow, distance, audibility, now);
            return new SoundHandle(voice.Index, voice.Generation);
        }

        // Past the cue's own limit, a nearer copy takes over the furthest one. Otherwise a free voice is used, or failing
        // that the voice whose sound matters least, if it matters less than the new one.
        private Voice FindVoiceFor(SoundCue cue, bool spatial, float distance, float audibility)
        {
            int copies = 0;
            Voice furthest = null;
            foreach (Voice voice in voices)
            {
                if (!voice.Active || voice.Cue != cue)
                    continue;

                copies++;
                if (furthest == null || voice.Distance > furthest.Distance)
                    furthest = voice;
            }

            if (copies >= cue.MaxVoices)
                return spatial && furthest != null && distance < furthest.Distance - 0.5f ? furthest : null;

            Voice weakest = null;
            float weakestScore = Importance(cue.Priority, audibility);
            foreach (Voice voice in voices)
            {
                if (!voice.Active)
                    return voice;

                float score = Importance(voice.Cue.Priority, voice.Audibility * voice.Fade);
                if (score < weakestScore)
                {
                    weakest = voice;
                    weakestScore = score;
                }
            }

            return weakest;
        }

        // Higher is more important: the cue's priority first, then how loud it reaches the listener.
        private static float Importance(int priority, float audibility) => (255 - priority) + audibility * 64f;

        private void StartVoice(Voice voice, SoundCue cue, Vector3 position, Transform follow, float distance, float audibility, float now)
        {
            AudioSource source = voice.Source;
            AudioClip clip = cue.PickClip();
            float pitch = cue.PickPitch();

            voice.Generation = ++generation;
            voice.Active = true;
            voice.Cue = cue;
            voice.Follow = follow;
            voice.Position = position;
            voice.Distance = distance;
            voice.Audibility = audibility;
            voice.BaseVolume = cue.PickVolume();
            voice.Fade = 1f;
            voice.FadeRate = 0f;
            voice.EndTime = cue.Loop ? float.PositiveInfinity : now + clip.length / Mathf.Max(0.01f, Mathf.Abs(pitch)) + 0.05f;

            source.clip = clip;
            source.loop = cue.Loop;
            source.pitch = pitch;
            source.priority = cue.Priority;
            source.spatialBlend = cue.SpatialBlend;
            source.outputAudioMixerGroup = OutputFor(cue.Category);
            if (cue.IsSpatial)
            {
                source.transform.position = position;
                source.rolloffMode = AudioRolloffMode.Custom;
                source.minDistance = cue.MinDistance;
                source.maxDistance = cue.MaxDistance;
                source.SetCustomCurve(AudioSourceCurveType.CustomRolloff, cue.Rolloff);
            }

            // A sound starts muffled or clear straight away; only later changes ease in.
            voice.Occluded = cue.IsSpatial && IsOccluded(position);
            voice.Occlusion = voice.Occluded ? 1f : 0f;
            voice.NextOcclusionCheck = now + OcclusionCheckInterval;
            ApplyMuffle(voice);
            ApplyVolume(voice);
            source.Play();
        }

        private void UpdateOcclusion(Voice voice, float now, float deltaTime)
        {
            if (now >= voice.NextOcclusionCheck)
            {
                voice.NextOcclusionCheck = now + OcclusionCheckInterval;
                voice.Occluded = IsOccluded(voice.Position);
            }

            voice.Occlusion = Mathf.MoveTowards(voice.Occlusion, voice.Occluded ? 1f : 0f, deltaTime / occlusionFade);
            ApplyMuffle(voice);
        }

        // The cutoff moves evenly in pitch rather than in hertz, as the ear hears it.
        private void ApplyMuffle(Voice voice)
        {
            bool muffled = voice.Occlusion > 0.001f;
            voice.Muffle.enabled = muffled;
            if (muffled)
                voice.Muffle.cutoffFrequency = Mathf.Exp(Mathf.Lerp(Mathf.Log(OpenCutoff), Mathf.Log(occludedCutoff), voice.Occlusion));
        }

        private void ApplyVolume(Voice voice)
        {
            AudioCategory category = voice.Cue.Category;
            float volume = voice.BaseVolume * Gain(category) * voice.Fade * Mathf.Lerp(1f, occludedVolume, voice.Occlusion);
            if (category == AudioCategory.Sfx)
                volume *= crowdGain;
            voice.Source.volume = volume;
        }

        private void Release(Voice voice)
        {
            if (!voice.Active)
                return;

            voice.Active = false;
            voice.Source.Stop();
            voice.Source.clip = null;
            voice.Muffle.enabled = false;
            voice.Cue = null;
            voice.Follow = null;
        }

        private bool TryGetVoice(SoundHandle handle, out Voice voice)
        {
            voice = handle.IsValid && handle.Voice >= 0 && handle.Voice < voices.Length ? voices[handle.Voice] : null;
            if (voice != null && voice.Active && voice.Generation == handle.Generation)
                return true;

            voice = null;
            return false;
        }

        // The cells the listener can hear into, worked out again whenever the listener moves to another cell, and every so
        // often in case a door has opened or closed.
        private void RefreshRegion(float now)
        {
            if (world == null || world.Navigator == null)
            {
                regionKnown = false;
                return;
            }

            Vector2Int cell = world.WorldToCell(ListenerPosition);
            if (regionKnown && cell == regionCell && now < nextRegionRefresh)
                return;

            world.Navigator.FindAudibleCells(cell, audibleCells);
            regionCell = cell;
            nextRegionRefresh = now + RegionRefreshInterval;
            regionKnown = true;
        }

        private void SetMaster(float volume)
        {
            masterVolume = Mathf.Clamp01(volume);
            VolumesChanged?.Invoke();
        }

        private Voice CreateVoice(int index)
        {
            var voiceObject = new GameObject($"Voice {index:00}");
            voiceObject.transform.SetParent(transform, false);

            var source = voiceObject.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.dopplerLevel = 0f;

            // Added after the source, so it filters what the source plays.
            var muffle = voiceObject.AddComponent<AudioLowPassFilter>();
            muffle.cutoffFrequency = OpenCutoff;
            muffle.lowpassResonanceQ = 1f;
            muffle.enabled = false;

            return new Voice { Index = index, Source = source, Muffle = muffle };
        }
    }
}
