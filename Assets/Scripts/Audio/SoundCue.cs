using System;
using UnityEngine;
using Random = UnityEngine.Random;

namespace Wolfenstein.Audio
{
    /// <summary>
    /// One sound the game can ask for, such as a pistol shot or a door slamming: its variations, how loud it is, how far it
    /// carries and how many copies of it may play at once. Code asks for cues and never for clips, so a clip can be replaced,
    /// or more variations added, without touching code. A cue with no clips is an empty slot: asking for it plays nothing.
    /// </summary>
    [CreateAssetMenu(fileName = "Sound", menuName = "Wolfenstein/Sound Cue")]
    public sealed class SoundCue : ScriptableObject
    {
        // How many points the distance curve is drawn through.
        private const int RolloffKeys = 12;

        [SerializeField, Tooltip("Variations. One is picked at random each time, never the same one twice running.")]
        private AudioClip[] clips = Array.Empty<AudioClip>();

        [SerializeField] private AudioCategory category = AudioCategory.Sfx;

        [SerializeField, Tooltip("Plays until it is stopped, such as the hum of a machine.")]
        private bool loop;

        [Header("Level")]
        [SerializeField, Range(0f, 1f)] private float volume = 1f;

        [SerializeField, Range(0f, 0.5f), Tooltip("Random spread of the volume, as a fraction of it.")]
        private float volumeVariation = 0.05f;

        [SerializeField, Range(0.1f, 3f)] private float pitch = 1f;

        [SerializeField, Range(0f, 0.5f), Tooltip("Random spread of the pitch, as a fraction of it.")]
        private float pitchVariation = 0.04f;

        [Header("Space")]
        [SerializeField, Range(0f, 1f), Tooltip("0 sounds the same in both ears wherever it comes from; 1 comes from its place in the level.")]
        private float spatialBlend = 1f;

        [SerializeField, Min(0.1f), Tooltip("Within this distance the sound plays at full volume, in metres.")]
        private float minDistance = 2f;

        [SerializeField, Min(0.2f), Tooltip("From this distance on the sound cannot be heard at all, in metres.")]
        private float maxDistance = 30f;

        [Header("Limits")]
        [SerializeField, Min(0f), Tooltip("Shortest time between two plays of this cue, in seconds. Requests sooner than that are dropped.")]
        private float cooldown;

        [SerializeField, Min(1), Tooltip("Most copies playing at once. Past that, a nearer request takes over the furthest copy.")]
        private int maxVoices = 4;

        [SerializeField, Range(0, 255), Tooltip("Lower is more important: when every voice is busy, the least important sound gives way.")]
        private int priority = 128;

        [SerializeField, Tooltip("Lowers the music while it plays, so the sound stands out.")]
        private bool ducksMusic;

        [NonSerialized] private int lastClip = -1;
        [NonSerialized] private AnimationCurve rolloff;

        public AudioCategory Category => category;
        public bool Loop => loop;
        public float Volume => volume;
        public float Pitch => pitch;
        public float SpatialBlend => spatialBlend;
        public bool IsSpatial => spatialBlend > 0f;
        public float MinDistance => minDistance;
        public float MaxDistance => Mathf.Max(maxDistance, minDistance + 0.1f);
        public float Cooldown => cooldown;
        public int MaxVoices => maxVoices;
        public int Priority => priority;
        public bool DucksMusic => ducksMusic;

        /// <summary>How many clips the cue has to choose from.</summary>
        public int ClipCount
        {
            get
            {
                int count = 0;
                foreach (AudioClip clip in clips)
                {
                    if (clip != null)
                        count++;
                }

                return count;
            }
        }

        /// <summary>Whether the cue has anything to play. One without clips is a slot waiting for its sound.</summary>
        public bool HasClips => ClipCount > 0;

        /// <summary>
        /// How loud the sound is by distance, as a fraction of the maximum distance: full volume up to the minimum distance,
        /// then falling away as sound does in the open, and easing down to silence at the maximum distance.
        /// </summary>
        public AnimationCurve Rolloff => rolloff ??= CreateRolloff(minDistance / MaxDistance);

        /// <summary>One of the clips at random, avoiding the last one picked when there is a choice. Null if there are none.</summary>
        public AudioClip PickClip()
        {
            int count = ClipCount;
            if (count == 0)
                return null;

            int pick = Random.Range(0, count);
            if (count > 1 && pick == lastClip)
                pick = (pick + 1 + Random.Range(0, count - 1)) % count;

            lastClip = pick;
            foreach (AudioClip clip in clips)
            {
                if (clip == null)
                    continue;
                if (pick-- == 0)
                    return clip;
            }

            return null;
        }

        public float PickVolume() => Mathf.Clamp01(volume * (1f + Random.Range(-volumeVariation, volumeVariation)));

        public float PickPitch() => pitch * (1f + Random.Range(-pitchVariation, pitchVariation));

        private void OnValidate() => rolloff = null;

        private static AnimationCurve CreateRolloff(float fullVolumeEdge)
        {
            float edge = Mathf.Clamp(fullVolumeEdge, 0.001f, 0.9f);
            var keys = new Keyframe[RolloffKeys + 1];
            keys[0] = new Keyframe(0f, 1f);

            // Points crowd in near the source, where the volume changes fastest.
            for (int i = 1; i <= RolloffKeys; i++)
            {
                float x = edge * Mathf.Pow(1f / edge, (i - 1) / (float)(RolloffKeys - 1));
                keys[i] = new Keyframe(x, Attenuation(x, edge));
            }

            // Each key's tangents follow the straight line to its neighbours, so the curve never overshoots full volume or
            // swells back up between keys.
            for (int i = 0; i < keys.Length; i++)
            {
                keys[i].inTangent = i > 0 ? Slope(keys[i - 1], keys[i]) : 0f;
                keys[i].outTangent = i < keys.Length - 1 ? Slope(keys[i], keys[i + 1]) : 0f;
            }

            return new AnimationCurve(keys);
        }

        private static float Slope(Keyframe from, Keyframe to) => (to.value - from.value) / Mathf.Max(0.0001f, to.time - from.time);

        // The inverse-distance fall of sound in the open, brought down to nothing over the last half of the range.
        private static float Attenuation(float x, float edge)
        {
            if (x <= edge)
                return 1f;

            float fade = 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.5f, 1f, x));
            return edge / x * fade;
        }
    }
}
