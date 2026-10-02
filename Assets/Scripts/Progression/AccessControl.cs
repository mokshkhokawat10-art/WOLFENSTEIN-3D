using System;
using System.Collections.Generic;
using UnityEngine;
using Wolfenstein.Interaction;
using Wolfenstein.Pickups;
using Wolfenstein.World;

namespace Wolfenstein.Progression
{
    /// <summary>
    /// Holds the level's locked doors shut until the player earns their way through. Once the level is built it locks
    /// every door carrying one of the level's lock markers; picking up the right key, or throwing the right switch, unlocks
    /// them. It tells the player why a door will not open when they try it, and announces a door opening.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class AccessControl : MonoBehaviour
    {
        [SerializeField] private LevelDefinition level;
        [SerializeField] private WorldManager world;

        [SerializeField, Tooltip("Places the level's props, among them its wall switches.")]
        private LevelDressing dressing;

        [SerializeField, Tooltip("The player's keys.")]
        private KeyRing keys;

        [SerializeField, Tooltip("The player's attempts to use things, to explain a locked door.")]
        private InteractionController interaction;

        private readonly Dictionary<Door, DoorLock> lockedDoors = new Dictionary<Door, DoorLock>();
        private readonly HashSet<DoorLock> opened = new HashSet<DoorLock>();
        private readonly List<WallSwitch> switches = new List<WallSwitch>();
        private readonly HashSet<char> thrownChannels = new HashSet<char>();

        /// <summary>Raised when the player tries a door the lock still holds.</summary>
        public event Action<DoorLock> AccessDenied;

        /// <summary>Raised when a lock lets its doors open, by key or by switch.</summary>
        public event Action<DoorLock> AccessGranted;

        /// <summary>Raised when one of the level's wall switches is thrown.</summary>
        public event Action<WallSwitch> SwitchThrown;

        public IReadOnlyList<WallSwitch> Switches => switches;

        /// <summary>The doors a lock holds, whether or not it has let them open yet.</summary>
        public IEnumerable<Door> DoorsOf(DoorLock doorLock)
        {
            foreach (KeyValuePair<Door, DoorLock> entry in lockedDoors)
            {
                if (entry.Value == doorLock && entry.Key != null)
                    yield return entry.Key;
            }
        }

        public bool IsOpen(DoorLock doorLock) => opened.Contains(doorLock);

        public bool IsSwitchThrown(char channel) => thrownChannels.Contains(channel);

        private void OnEnable()
        {
            world.Built += LockDoors;
            if (dressing != null)
                dressing.Dressed += FindSwitches;
            if (keys != null)
                keys.KeyAdded += HandleKeyAdded;
            if (interaction != null)
                interaction.Refused += HandleRefused;
        }

        private void OnDisable()
        {
            world.Built -= LockDoors;
            if (dressing != null)
                dressing.Dressed -= FindSwitches;
            if (keys != null)
                keys.KeyAdded -= HandleKeyAdded;
            if (interaction != null)
                interaction.Refused -= HandleRefused;
            foreach (WallSwitch wallSwitch in switches)
            {
                if (wallSwitch != null)
                    wallSwitch.Thrown -= HandleSwitchThrown;
            }
        }

        private void LockDoors()
        {
            lockedDoors.Clear();
            opened.Clear();
            LevelLayout layout = world.Layout;
            foreach (KeyValuePair<Vector2Int, Door> entry in world.Doors)
            {
                DoorLock doorLock = level.FindLock(layout.GetDecor(entry.Key));
                if (doorLock == null)
                    continue;

                lockedDoors.Add(entry.Value, doorLock);
                if (IsEarned(doorLock))
                    opened.Add(doorLock);
                else
                    entry.Value.Lock();
            }
        }

        private void FindSwitches()
        {
            foreach (WallSwitch wallSwitch in switches)
            {
                if (wallSwitch != null)
                    wallSwitch.Thrown -= HandleSwitchThrown;
            }

            switches.Clear();
            thrownChannels.Clear();
            foreach (GameObject prop in dressing.PlacedProps)
            {
                var wallSwitch = prop != null ? prop.GetComponentInChildren<WallSwitch>() : null;
                if (wallSwitch == null)
                    continue;

                switches.Add(wallSwitch);
                wallSwitch.Thrown += HandleSwitchThrown;
            }
        }

        private bool IsEarned(DoorLock doorLock) =>
            doorLock.Key != null ? keys != null && keys.Has(doorLock.Key) : thrownChannels.Contains(doorLock.SwitchChannel);

        private void HandleKeyAdded(AccessKey key) => OpenEarnedLocks();

        private void HandleSwitchThrown(WallSwitch wallSwitch)
        {
            thrownChannels.Add(wallSwitch.Channel);
            SwitchThrown?.Invoke(wallSwitch);
            OpenEarnedLocks();
        }

        private void OpenEarnedLocks()
        {
            foreach (DoorLock doorLock in level.DoorLocks)
            {
                if (opened.Contains(doorLock) || !IsEarned(doorLock))
                    continue;

                opened.Add(doorLock);
                foreach (Door door in DoorsOf(doorLock))
                    door.Unlock();
                AccessGranted?.Invoke(doorLock);
            }
        }

        private void HandleRefused(IInteractable target)
        {
            if (target is Door door && door.IsLocked && lockedDoors.TryGetValue(door, out DoorLock doorLock) && !opened.Contains(doorLock))
                AccessDenied?.Invoke(doorLock);
        }
    }
}
