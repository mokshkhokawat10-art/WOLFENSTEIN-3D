using System;
using System.Collections.Generic;
using UnityEngine;
using Wolfenstein.Interaction;
using Wolfenstein.World;

namespace Wolfenstein.Progression
{
    /// <summary>
    /// The way out of the level. Its doors, marked in the level's decor map, stay locked until the player's current
    /// objective is to reach the exit, that is until every other objective is done; then they open and the exit is ready.
    /// Walking into the exit's zone once it is ready completes the level. Trying the exit early tells the player it is locked.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class LevelExit : MonoBehaviour
    {
        [SerializeField] private LevelDefinition level;
        [SerializeField] private WorldManager world;
        [SerializeField] private ZoneTracker zones;
        [SerializeField] private ObjectiveTracker objectives;

        [SerializeField, Tooltip("The player's attempts to use things, to explain a locked exit.")]
        private InteractionController interaction;

        private readonly List<Door> doors = new List<Door>();

        /// <summary>Raised when the exit opens, once every other objective is done.</summary>
        public event Action Ready;

        /// <summary>Raised when the player tries the exit before it is ready.</summary>
        public event Action Denied;

        /// <summary>Raised once, when the player walks into the ready exit.</summary>
        public event Action Reached;

        public bool IsReady { get; private set; }
        public bool IsReached { get; private set; }
        public IReadOnlyList<Door> Doors => doors;

        private void OnEnable()
        {
            world.Built += FindDoors;
            zones.ZoneEntered += HandleZoneEntered;
            objectives.Started += HandleObjectiveStarted;
            if (interaction != null)
                interaction.Refused += HandleRefused;
        }

        private void OnDisable()
        {
            world.Built -= FindDoors;
            zones.ZoneEntered -= HandleZoneEntered;
            objectives.Started -= HandleObjectiveStarted;
            if (interaction != null)
                interaction.Refused -= HandleRefused;
        }

        /// <summary>Opens the exit, if it is not open already.</summary>
        public void MakeReady()
        {
            if (IsReady)
                return;

            IsReady = true;
            foreach (Door door in doors)
                door.Unlock();
            Ready?.Invoke();
        }

        private void FindDoors()
        {
            doors.Clear();
            IsReady = false;
            IsReached = false;
            foreach (KeyValuePair<Vector2Int, Door> entry in world.Doors)
            {
                if (world.Layout.GetDecor(entry.Key) != level.ExitDoorMarker)
                    continue;

                doors.Add(entry.Value);
                entry.Value.Lock();
            }
        }

        private void HandleObjectiveStarted(ObjectiveDefinition objective)
        {
            if (objective.Kind == ObjectiveKind.ReachExit)
                MakeReady();
        }

        private void HandleZoneEntered(char zone)
        {
            if (!IsReady || IsReached || zone != level.ExitZone)
                return;

            IsReached = true;
            Reached?.Invoke();
        }

        private void HandleRefused(IInteractable target)
        {
            if (!IsReady && target is Door door && doors.Contains(door))
                Denied?.Invoke();
        }
    }
}
