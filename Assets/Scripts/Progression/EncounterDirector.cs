using System;
using System.Collections.Generic;
using UnityEngine;
using Wolfenstein.Monsters;
using Wolfenstein.Waves;
using Wolfenstein.World;

namespace Wolfenstein.Progression
{
    /// <summary>
    /// Runs the level's planned encounters away from the arena. Each is set off once, the first time the player walks into
    /// its zone or the moment its switch is thrown, and raises its monsters through the <see cref="MonsterManager"/>, so they
    /// come from the same pools as every other monster. They rise at the encounter's marked points, at once where there is
    /// room and as soon as there is room otherwise, and hunt the player straight away.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class EncounterDirector : MonoBehaviour
    {
        private sealed class Encounter
        {
            public EncounterDefinition Definition;
            public readonly List<Vector3> Points = new List<Vector3>();
            public readonly Queue<MonsterDefinition> Pending = new Queue<MonsterDefinition>();
            public readonly HashSet<MonsterController> Alive = new HashSet<MonsterController>();
            public bool IsStarted;
            public bool IsCleared;
            public int NextPoint;
        }

        [SerializeField] private LevelDefinition level;
        [SerializeField] private WorldManager world;
        [SerializeField] private MonsterManager monsters;
        [SerializeField] private ZoneTracker zones;
        [SerializeField] private AccessControl access;

        [SerializeField, Tooltip("The player, whom rising monsters face.")]
        private Transform player;

        private readonly List<Encounter> encounters = new List<Encounter>();
        private int pendingEncounters;

        /// <summary>Raised when an encounter is set off.</summary>
        public event Action<EncounterDefinition> Started;

        /// <summary>Raised when every monster of an encounter is dead.</summary>
        public event Action<EncounterDefinition> Cleared;

        /// <summary>How many monsters all the level's encounters raise between them.</summary>
        public int TotalMonsters
        {
            get
            {
                int total = 0;
                foreach (EncounterDefinition encounter in level.Encounters)
                    total += encounter.TotalCount;
                return total;
            }
        }

        public bool IsStarted(string encounterName) => Find(encounterName)?.IsStarted ?? false;

        public bool IsCleared(string encounterName) => Find(encounterName)?.IsCleared ?? false;

        /// <summary>The monsters of an encounter that are alive in the level.</summary>
        public IReadOnlyCollection<MonsterController> AliveIn(string encounterName) =>
            (IReadOnlyCollection<MonsterController>)Find(encounterName)?.Alive ?? Array.Empty<MonsterController>();

        private void OnEnable()
        {
            world.Built += Prepare;
            zones.ZoneEntered += HandleZoneEntered;
            access.SwitchThrown += HandleSwitchThrown;
            monsters.Killed += HandleKilled;
        }

        private void OnDisable()
        {
            world.Built -= Prepare;
            zones.ZoneEntered -= HandleZoneEntered;
            access.SwitchThrown -= HandleSwitchThrown;
            monsters.Killed -= HandleKilled;
        }

        /// <summary>Sets an encounter off, if it has not been already.</summary>
        public void Trigger(string encounterName)
        {
            Encounter encounter = Find(encounterName);
            if (encounter != null)
                Trigger(encounter);
        }

        // Keeps trying for room only while some encounter still has monsters waiting to rise.
        private void Update()
        {
            if (pendingEncounters == 0)
                return;

            foreach (Encounter encounter in encounters)
            {
                if (encounter.Pending.Count > 0)
                    SpawnWaiting(encounter);
            }
        }

        private void Prepare()
        {
            encounters.Clear();
            pendingEncounters = 0;
            LevelLayout layout = world.Layout;
            foreach (EncounterDefinition definition in level.Encounters)
            {
                var encounter = new Encounter { Definition = definition };
                for (int y = 0; y < layout.Height; y++)
                {
                    for (int x = 0; x < layout.Width; x++)
                    {
                        var cell = new Vector2Int(x, y);
                        if (layout.GetDecor(cell) == definition.SpawnMarker && layout.IsWalkable(cell))
                            encounter.Points.Add(world.CellToWorld(cell));
                    }
                }

                if (encounter.Points.Count == 0)
                    Debug.LogError($"Encounter {definition.Name} of {level.name} has no points marked '{definition.SpawnMarker}' to rise at.", this);
                encounters.Add(encounter);
            }
        }

        private void Trigger(Encounter encounter)
        {
            if (encounter.IsStarted || encounter.Points.Count == 0)
                return;

            encounter.IsStarted = true;
            foreach (WaveGroup group in encounter.Definition.Monsters)
            {
                for (int i = 0; i < group.Count && group.Monster != null; i++)
                    encounter.Pending.Enqueue(group.Monster);
            }

            pendingEncounters++;
            Started?.Invoke(encounter.Definition);
            SpawnWaiting(encounter);
        }

        // Each monster rises at the next point with room for it; those without room wait for a later frame.
        private void SpawnWaiting(Encounter encounter)
        {
            int attempts = encounter.Points.Count;
            while (encounter.Pending.Count > 0 && attempts > 0)
            {
                MonsterDefinition kind = encounter.Pending.Peek();
                Vector3 point = encounter.Points[encounter.NextPoint];
                encounter.NextPoint = (encounter.NextPoint + 1) % encounter.Points.Count;
                if (!monsters.HasRoomFor(kind, point))
                {
                    attempts--;
                    continue;
                }

                MonsterController monster = monsters.Spawn(kind, point, YawTowardsPlayer(point), MonsterModifiers.None);
                if (monster == null)
                    return;

                encounter.Pending.Dequeue();
                encounter.Alive.Add(monster);
                attempts = encounter.Points.Count;
            }

            if (encounter.Pending.Count == 0)
                pendingEncounters--;
        }

        private float YawTowardsPlayer(Vector3 point)
        {
            Vector3 toPlayer = player != null ? Vector3.ProjectOnPlane(player.position - point, Vector3.up) : Vector3.zero;
            return toPlayer.sqrMagnitude > 0.01f ? Quaternion.LookRotation(toPlayer).eulerAngles.y : 0f;
        }

        private void HandleZoneEntered(char zone)
        {
            foreach (Encounter encounter in encounters)
            {
                if (encounter.Definition.TriggerZone == zone && zone != '\0')
                    Trigger(encounter);
            }
        }

        private void HandleSwitchThrown(WallSwitch wallSwitch)
        {
            foreach (Encounter encounter in encounters)
            {
                if (encounter.Definition.TriggerSwitch == wallSwitch.Channel && wallSwitch.Channel != '\0')
                    Trigger(encounter);
            }
        }

        private void HandleKilled(MonsterController monster)
        {
            foreach (Encounter encounter in encounters)
            {
                if (!encounter.Alive.Remove(monster) || encounter.Alive.Count > 0 || encounter.Pending.Count > 0 || encounter.IsCleared)
                    continue;

                encounter.IsCleared = true;
                Cleared?.Invoke(encounter.Definition);
            }
        }

        private Encounter Find(string encounterName)
        {
            foreach (Encounter encounter in encounters)
            {
                if (encounter.Definition.Name == encounterName)
                    return encounter;
            }

            return null;
        }
    }
}
