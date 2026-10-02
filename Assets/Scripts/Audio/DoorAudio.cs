using System;
using System.Collections.Generic;
using UnityEngine;
using Wolfenstein.Interaction;
using Wolfenstein.World;

namespace Wolfenstein.Audio
{
    /// <summary>
    /// The sounds of every door in the level: its drive as it opens and closes, the thud as it comes to rest, the bolts as it
    /// locks and unlocks, and a refusal when the player tries a locked one. Each door sounds like its kind, from its own
    /// <see cref="DoorSounds"/>. It listens to the doors of the built level and to the player's attempts to use things.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class DoorAudio : MonoBehaviour
    {
        private sealed class Tracker
        {
            public Door Door;
            public DoorSounds Sounds;
            public SoundHandle Moving;
            public Action<DoorState> StateChanged;
            public Action<bool> LockChanged;
        }

        [SerializeField] private AudioManager audioManager;
        [SerializeField] private WorldManager world;

        [SerializeField, Tooltip("Whose attempts to use a locked door are refused with a sound, normally the player's.")]
        private InteractionController interaction;

        [SerializeField, Tooltip("For doors with no sounds of their own.")]
        private DoorSounds defaultSounds;

        private readonly Dictionary<Door, Tracker> trackers = new Dictionary<Door, Tracker>();

        private void OnEnable()
        {
            world.Built += TrackDoors;
            if (interaction != null)
                interaction.Refused += HandleRefused;
            if (world.Doors.Count > 0)
                TrackDoors();
        }

        private void OnDisable()
        {
            world.Built -= TrackDoors;
            if (interaction != null)
                interaction.Refused -= HandleRefused;
            Untrack();
        }

        /// <summary>How a door sounds: its own sounds, or the level's usual ones.</summary>
        public DoorSounds SoundsOf(Door door) => door.Sounds != null ? door.Sounds : defaultSounds;

        private void TrackDoors()
        {
            Untrack();
            foreach (Door door in world.Doors.Values)
            {
                var tracker = new Tracker { Door = door, Sounds = SoundsOf(door) };
                tracker.StateChanged = state => HandleStateChanged(tracker, state);
                tracker.LockChanged = locked => HandleLockChanged(tracker, locked);
                door.StateChanged += tracker.StateChanged;
                door.LockChanged += tracker.LockChanged;
                trackers.Add(door, tracker);
            }
        }

        private void Untrack()
        {
            foreach (Tracker tracker in trackers.Values)
            {
                if (tracker.Door == null)
                    continue;

                tracker.Door.StateChanged -= tracker.StateChanged;
                tracker.Door.LockChanged -= tracker.LockChanged;
            }

            trackers.Clear();
        }

        // The drive runs while the door moves and is cut short when it stops or turns back.
        private void HandleStateChanged(Tracker tracker, DoorState state)
        {
            DoorSounds sounds = tracker.Sounds;
            if (sounds == null)
                return;

            audioManager.Stop(tracker.Moving, 0.12f);
            tracker.Moving = default;
            Vector3 position = SoundPosition(tracker);
            switch (state)
            {
                case DoorState.Opening:
                    tracker.Moving = audioManager.Play(sounds.Open, position);
                    break;
                case DoorState.Open:
                    audioManager.Play(sounds.Opened, position);
                    break;
                case DoorState.Closing:
                    tracker.Moving = audioManager.Play(sounds.Close, position);
                    break;
                case DoorState.Closed:
                    audioManager.Play(sounds.Closed, position);
                    break;
            }
        }

        private void HandleLockChanged(Tracker tracker, bool locked)
        {
            if (tracker.Sounds != null)
                audioManager.Play(locked ? tracker.Sounds.LockEngaged : tracker.Sounds.Unlocked, SoundPosition(tracker));
        }

        private void HandleRefused(IInteractable target)
        {
            if (target is Door door && door.IsLocked && trackers.TryGetValue(door, out Tracker tracker) && tracker.Sounds != null)
                audioManager.Play(tracker.Sounds.Locked, SoundPosition(tracker));
        }

        private static Vector3 SoundPosition(Tracker tracker) => tracker.Door.transform.position + Vector3.up * tracker.Sounds.Height;
    }
}
