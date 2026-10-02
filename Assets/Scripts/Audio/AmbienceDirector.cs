using System;
using System.Collections.Generic;
using UnityEngine;
using Wolfenstein.Waves;
using Wolfenstein.World;
using Random = UnityEngine.Random;

namespace Wolfenstein.Audio
{
    /// <summary>
    /// The background of the level. Each zone has its own looping bed, such as machinery in the industrial halls or the hum
    /// of strip lights in the barracks, and the beds crossfade as the player moves from one zone to the next. A low pulse of
    /// tension joins them while an arena wave is fought. Now and then a distant sound comes from somewhere nearby, often
    /// muffled by the walls, with long silences between. Machines and vents hum where they stand, but only the few nearest
    /// the listener are given a voice. Beds play on three looping sources of its own; everything else goes through the
    /// pooled voices of the <see cref="AudioManager"/>.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class AmbienceDirector : MonoBehaviour
    {
        private const float ZoneCheckInterval = 0.25f;
        private const float MachineCheckInterval = 0.5f;

        private sealed class Bed
        {
            public AudioSource Source;
            public SoundCue Cue;
            public float Level;
            public float Target;
            public float Volume = 1f;
        }

        [SerializeField] private AudioManager audioManager;
        [SerializeField] private WorldManager world;

        [SerializeField, Tooltip("Where the ambience is heard from, normally the player.")]
        private Transform listener;

        [SerializeField, Tooltip("Brings in the tension layer while a wave is fought.")]
        private WaveManager waves;

        [Header("Beds")]
        [SerializeField, Tooltip("The bed where the zone sets none.")]
        private SoundCue defaultBed;

        [SerializeField, Tooltip("Laid over the zone's bed while an arena wave is fought.")]
        private SoundCue tensionBed;

        [SerializeField, Min(0.1f), Tooltip("Seconds one bed takes to fade into the next.")]
        private float crossfade = 3f;

        [Header("Distant Sounds")]
        [SerializeField, Tooltip("Heard where the zone sets none of its own.")]
        private SoundCue[] defaultDistantSounds = Array.Empty<SoundCue>();

        [SerializeField, Tooltip("Seconds between one chance of a distant sound and the next, picked at random between these.")]
        private Vector2 distantInterval = new Vector2(14f, 30f);

        [SerializeField, Tooltip("How far away distant sounds come from, in metres, picked at random between these.")]
        private Vector2 distantRange = new Vector2(7f, 16f);

        [SerializeField, Range(0f, 1f), Tooltip("Chance each time that a sound is heard; the rest of the time there is only silence.")]
        private float distantChance = 0.65f;

        [Header("Machines")]
        [SerializeField, Min(0), Tooltip("Most machines heard at once. The nearest are chosen.")]
        private int machineVoices = 4;

        [SerializeField, Min(0f), Tooltip("A machine stays silent while one making the same sound is heard within this distance of it, in metres, so a bank of machines does not stack one loop.")]
        private float machineSpacing = 4f;

        private readonly Bed[] beds = { new Bed(), new Bed() };
        private readonly Bed tension = new Bed();
        private readonly List<AmbientSoundSource> machines = new List<AmbientSoundSource>();
        private readonly Dictionary<AmbientSoundSource, SoundHandle> machineHandles = new Dictionary<AmbientSoundSource, SoundHandle>();
        private readonly List<AmbientSoundSource> machinesInEarshot = new List<AmbientSoundSource>();
        private readonly List<AmbientSoundSource> nearestMachines = new List<AmbientSoundSource>();
        private readonly List<AmbientSoundSource> silencedMachines = new List<AmbientSoundSource>();
        private int currentBed;
        private ZoneTheme currentTheme;
        private bool machinesFound;
        private float nextZoneCheckTime;
        private float nextDistantTime;
        private float nextMachineCheckTime;

        /// <summary>The bed playing or fading in for the listener's zone. Null while there is none.</summary>
        public SoundCue CurrentBed => beds[currentBed].Target > 0f ? beds[currentBed].Cue : null;

        public bool IsTensionPlaying => tension.Target > 0f;

        /// <summary>How many machines are being heard right now.</summary>
        public int AudibleMachineCount
        {
            get
            {
                int count = 0;
                foreach (SoundHandle handle in machineHandles.Values)
                {
                    if (audioManager.IsPlaying(handle))
                        count++;
                }

                return count;
            }
        }

        private void Awake()
        {
            beds[0].Source = CreateSource("Bed A");
            beds[1].Source = CreateSource("Bed B");
            tension.Source = CreateSource("Tension");
            nextDistantTime = Time.time + RandomBetween(distantInterval) * 0.5f;
        }

        private void OnEnable() => world.Built += HandleBuilt;

        private void OnDisable()
        {
            world.Built -= HandleBuilt;
            foreach (SoundHandle handle in machineHandles.Values)
                audioManager.Stop(handle);
            machineHandles.Clear();
        }

        private void Update()
        {
            float now = Time.time;
            if (listener == null || world.Layout == null)
                return;

            if (now >= nextZoneCheckTime)
            {
                nextZoneCheckTime = now + ZoneCheckInterval;
                UpdateZone();
            }

            bool fighting = waves != null && waves.State is WaveState.Announcing or WaveState.Fighting;
            if (fighting && tension.Target <= 0f && tensionBed != null && tensionBed.HasClips)
                StartBed(tension, tensionBed, 1f);
            else if (!fighting)
                tension.Target = 0f;

            if (now >= nextDistantTime)
            {
                nextDistantTime = now + RandomBetween(distantInterval);
                PlayDistantSound();
            }

            if (!machinesFound)
                FindMachines();
            if (now >= nextMachineCheckTime)
            {
                nextMachineCheckTime = now + MachineCheckInterval;
                UpdateMachines();
            }

            UpdateBeds(Time.deltaTime);
        }

        // Moving into another zone crossfades to its bed; a zone with the same bed as the last just keeps it.
        private void UpdateZone()
        {
            currentTheme = world.GetZoneTheme(world.WorldToCell(listener.position));
            bool themed = currentTheme != null && currentTheme.Ambience != null;
            SoundCue cue = themed ? currentTheme.Ambience : defaultBed;
            float volume = themed ? currentTheme.AmbienceVolume : 1f;

            Bed bed = beds[currentBed];
            if (cue == bed.Cue && bed.Target > 0f)
            {
                bed.Volume = volume;
                return;
            }

            bed.Target = 0f;
            if (cue == null || !cue.HasClips)
                return;

            currentBed = 1 - currentBed;
            Bed next = beds[currentBed];
            if (next.Cue == cue && next.Level > 0f)
            {
                next.Target = 1f;
                next.Volume = volume;
                return;
            }

            StartBed(next, cue, volume);
        }

        // A bed starts somewhere into its loop, so walking back into a zone does not replay it from the top.
        private static void StartBed(Bed bed, SoundCue cue, float volume)
        {
            AudioClip clip = cue.PickClip();
            bed.Cue = cue;
            bed.Volume = volume;
            bed.Level = 0f;
            bed.Target = 1f;
            bed.Source.clip = clip;
            bed.Source.pitch = cue.Pitch;
            bed.Source.volume = 0f;
            bed.Source.Play();
            bed.Source.time = Random.Range(0f, clip.length * 0.9f);
        }

        private void UpdateBeds(float deltaTime)
        {
            float step = deltaTime / crossfade;
            float gain = audioManager.Gain(AudioCategory.Ambience);
            UpdateBed(beds[0], step, gain);
            UpdateBed(beds[1], step, gain);
            UpdateBed(tension, step, gain);
        }

        private static void UpdateBed(Bed bed, float step, float gain)
        {
            bed.Level = Mathf.MoveTowards(bed.Level, bed.Target, step);
            if (bed.Level <= 0f && bed.Target <= 0f)
            {
                if (bed.Source.isPlaying)
                    bed.Source.Stop();
                return;
            }

            bed.Source.volume = bed.Level * bed.Cue.Volume * bed.Volume * gain;
        }

        private void PlayDistantSound()
        {
            if (Random.value > distantChance)
                return;

            IReadOnlyList<SoundCue> sounds = currentTheme != null && currentTheme.DistantSounds.Count > 0
                ? currentTheme.DistantSounds
                : defaultDistantSounds;
            if (sounds.Count == 0)
                return;

            SoundCue cue = sounds[Random.Range(0, sounds.Count)];
            Vector2 direction = Random.insideUnitCircle.normalized;
            float distance = RandomBetween(distantRange);
            Vector3 position = listener.position + new Vector3(direction.x, 0f, direction.y) * distance + Vector3.up * Random.Range(0f, 1.5f);
            audioManager.Play(cue, position);
        }

        private void HandleBuilt()
        {
            machinesFound = false;
            nextZoneCheckTime = 0f;
        }

        private void FindMachines()
        {
            machines.Clear();
            machines.AddRange(FindObjectsByType<AmbientSoundSource>(FindObjectsSortMode.None));
            machinesFound = true;
        }

        // The nearest machines in earshot get a voice, one for each bank of alike machines, and any that drop out of the
        // nearest fade away.
        private void UpdateMachines()
        {
            Vector3 position = listener.position;
            machinesInEarshot.Clear();
            foreach (AmbientSoundSource machine in machines)
            {
                if (machine != null && machine.Sound != null && Vector3.Distance(machine.Position, position) < machine.Sound.MaxDistance)
                    machinesInEarshot.Add(machine);
            }

            machinesInEarshot.Sort((a, b) => Vector3.Distance(a.Position, position).CompareTo(Vector3.Distance(b.Position, position)));
            nearestMachines.Clear();
            foreach (AmbientSoundSource machine in machinesInEarshot)
            {
                if (nearestMachines.Count >= machineVoices)
                    break;
                if (!HasAlikeNearby(machine))
                    nearestMachines.Add(machine);
            }

            silencedMachines.Clear();
            foreach (KeyValuePair<AmbientSoundSource, SoundHandle> entry in machineHandles)
            {
                if (!nearestMachines.Contains(entry.Key))
                {
                    audioManager.Stop(entry.Value, 0.8f);
                    silencedMachines.Add(entry.Key);
                }
            }

            foreach (AmbientSoundSource machine in silencedMachines)
                machineHandles.Remove(machine);

            foreach (AmbientSoundSource machine in nearestMachines)
            {
                if (!machineHandles.TryGetValue(machine, out SoundHandle handle) || !audioManager.IsPlaying(handle))
                    machineHandles[machine] = audioManager.Play(machine.Sound, machine.Position);
            }
        }

        private bool HasAlikeNearby(AmbientSoundSource machine)
        {
            foreach (AmbientSoundSource chosen in nearestMachines)
            {
                if (chosen.Sound == machine.Sound && Vector3.Distance(chosen.Position, machine.Position) < machineSpacing)
                    return true;
            }

            return false;
        }

        private AudioSource CreateSource(string sourceName)
        {
            var sourceObject = new GameObject(sourceName);
            sourceObject.transform.SetParent(transform, false);
            AudioSource source = sourceObject.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.loop = true;
            source.spatialBlend = 0f;
            source.priority = 16;
            source.outputAudioMixerGroup = audioManager.OutputFor(AudioCategory.Ambience);
            return source;
        }

        private static float RandomBetween(Vector2 range) => Random.Range(Mathf.Min(range.x, range.y), Mathf.Max(range.x, range.y));
    }
}
