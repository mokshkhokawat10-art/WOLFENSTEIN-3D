using System;
using System.Collections.Generic;
using UnityEngine;
using Wolfenstein.Combat;
using Wolfenstein.Monsters;
using Wolfenstein.Pickups;
using Wolfenstein.World;
using Random = UnityEngine.Random;

namespace Wolfenstein.Waves
{
    /// <summary>
    /// Runs the arena fight. When the player steps into the arena zone, its doors lock and the waves of its
    /// <see cref="WaveSet"/> begin: each is announced, then its monsters pour in one at a time from spawn points around the
    /// arena, never in sight of more than the wave allows at once, never on top of the player, inside a wall or on top of
    /// one another. Once every monster of a wave is dead, supplies are left for the player and the next wave follows a
    /// short while later. Beating the final wave clears the arena: its doors open again, and so do the gates held shut
    /// until then, such as the way on to the exit.
    /// The level's decor map marks the spawn points, the supply points and the gates.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class WaveManager : MonoBehaviour
    {
        [SerializeField] private WorldManager world;
        [SerializeField] private MonsterManager monsters;

        [SerializeField, Tooltip("Leaves the supplies between waves.")]
        private PickupManager pickups;

        [SerializeField, Tooltip("Stepping into the arena starts the fight, and the waves pause while they are dead.")]
        private Health player;

        [SerializeField] private WaveSet waves;

        [Header("Level Markers")]
        [SerializeField, Tooltip("The zone of the level's zone map the fight takes place in. Its doors lock while it lasts.")]
        private char arenaZone = 'a';

        [SerializeField, Tooltip("Decor map symbol for the points monsters spawn at.")]
        private char spawnMarker = '@';

        [SerializeField, Tooltip("Decor map symbol for the points supplies are left at between waves.")]
        private char supplyMarker = '+';

        [SerializeField, Tooltip("Decor map symbol for doors held locked until the arena is cleared.")]
        private char gateMarker = 'G';

        [Header("Spawning")]
        [SerializeField, Min(0f), Tooltip("Monsters never spawn closer to the player than this, in metres.")]
        private float minSpawnDistance = 7f;

        [SerializeField, Min(0.5f), Tooltip("A monster stuck for this long out of sight of the player is moved to a spawn point, in seconds.")]
        private float relocateAfterStuck = 5f;

        private readonly List<Vector3> spawnPoints = new List<Vector3>();
        private readonly List<Vector3> supplyPoints = new List<Vector3>();
        private readonly List<Pickup> supplyItems = new List<Pickup>();
        private readonly List<Door> arenaDoors = new List<Door>();
        private readonly List<Door> gates = new List<Door>();
        private readonly List<MonsterDefinition> spawnQueue = new List<MonsterDefinition>();
        private readonly HashSet<MonsterController> waveMonsters = new HashSet<MonsterController>();
        private readonly List<MonsterController> stuck = new List<MonsterController>();
        private readonly List<int> candidates = new List<int>();
        private WaveSet activeWaves;
        private int waveIndex = -1;
        private float stateEndTime;
        private float nextSpawnTime;
        private int supplyCursor;

        /// <summary>Raised when a wave is announced, with its number from 1, how many monsters it holds, and whether it is the last.</summary>
        public event Action<int, int, bool> WaveAnnounced;

        /// <summary>Raised when a wave's monsters start to spawn, with its number from 1.</summary>
        public event Action<int> WaveStarted;

        /// <summary>Raised when every monster of a wave is dead, with its number from 1.</summary>
        public event Action<int> WaveCompleted;

        /// <summary>Raised once the final wave is beaten and the arena is open again.</summary>
        public event Action AreaCleared;

        /// <summary>Raised with the new count whenever the number of monsters left in the current wave changes.</summary>
        public event Action<int> RemainingChanged;

        public event Action<WaveState> StateChanged;

        public WaveState State { get; private set; } = WaveState.Waiting;

        /// <summary>The waves the fight runs, or will run once it starts.</summary>
        public WaveSet Waves => activeWaves != null ? activeWaves : waves;

        /// <summary>The current wave's number, counting from 1. 0 before the first wave.</summary>
        public int WaveNumber => waveIndex + 1;

        public int WaveCount => Waves != null ? Waves.Waves.Count : 0;

        /// <summary>The wave being announced, fought or just beaten. Null before the first.</summary>
        public Wave CurrentWave => activeWaves != null && waveIndex >= 0 && waveIndex < activeWaves.Waves.Count ? activeWaves.Waves[waveIndex] : null;

        public bool IsFinalWave => waveIndex >= 0 && waveIndex == WaveCount - 1;

        /// <summary>Monsters of the current wave still to spawn or still alive.</summary>
        public int Remaining => spawnQueue.Count + waveMonsters.Count;

        /// <summary>Monsters of the current wave alive in the arena.</summary>
        public int AliveCount => waveMonsters.Count;

        /// <summary>Monsters of the current wave alive in the arena.</summary>
        public IReadOnlyCollection<MonsterController> WaveMonsters => waveMonsters;

        /// <summary>Monsters of the current wave yet to spawn.</summary>
        public int QueuedCount => spawnQueue.Count;

        /// <summary>Monsters never spawn closer to the player than this, in metres.</summary>
        public float MinSpawnDistance => minSpawnDistance;

        public IReadOnlyList<Vector3> SpawnPoints => spawnPoints;
        public IReadOnlyList<Vector3> SupplyPoints => supplyPoints;
        public IReadOnlyList<Door> ArenaDoors => arenaDoors;
        public IReadOnlyList<Door> Gates => gates;
        public char ArenaZone => arenaZone;

        private void OnEnable()
        {
            world.Built += FindArena;
            monsters.Killed += HandleMonsterKilled;
        }

        private void OnDisable()
        {
            world.Built -= FindArena;
            monsters.Killed -= HandleMonsterKilled;
        }

        /// <summary>Whether a point lies on the arena's floor, as opposed to its doorways or anywhere else.</summary>
        public bool IsInArena(Vector3 position)
        {
            Vector2Int cell = world.WorldToCell(position);
            return world.Layout.GetZone(cell) == arenaZone && world.Layout.IsWalkable(cell) && !world.Layout.TryGetDoor(cell, out _);
        }

        /// <summary>
        /// Starts the fight at once, whether or not the player is in the arena, with the given waves or the usual ones.
        /// It does nothing once the fight has started.
        /// </summary>
        public void Begin(WaveSet waveSet = null)
        {
            if (State != WaveState.Waiting)
                return;

            activeWaves = waveSet != null ? waveSet : waves;
            if (activeWaves == null || activeWaves.Waves.Count == 0)
            {
                Debug.LogError("The arena has no waves to run.", this);
                return;
            }

            foreach (Door door in arenaDoors)
                door.Lock();

            StartWave(0);
        }

        private void Update()
        {
            float now = Time.time;
            switch (State)
            {
                case WaveState.Waiting:
                    if (player != null && !player.IsDead && world.Navigator != null && IsInArena(player.transform.position))
                        Begin();
                    break;

                case WaveState.Announcing:
                    GuardDoors();
                    if (now >= stateEndTime)
                    {
                        nextSpawnTime = now;
                        SetState(WaveState.Fighting);
                        WaveStarted?.Invoke(WaveNumber);
                    }

                    break;

                case WaveState.Fighting:
                    if (player != null && player.IsDead)
                        break;

                    GuardDoors();
                    SpawnDue(now);
                    RescueStuckMonsters();
                    if (spawnQueue.Count == 0 && waveMonsters.Count == 0)
                        CompleteWave();
                    break;

                case WaveState.Complete:
                    GuardDoors();
                    if (now >= stateEndTime)
                        StartWave(waveIndex + 1);
                    break;
            }
        }

        // The doors hold the player in while the fight lasts. Should the player slip out through one still closing, they open
        // to the player again rather than shutting them out of a fight they could never finish, and lock once they are back.
        private void GuardDoors()
        {
            if (player == null)
                return;

            bool inside = IsInArena(player.transform.position);
            foreach (Door door in arenaDoors)
            {
                if (inside)
                    door.Lock();
                else
                    door.Unlock();
            }
        }

        private void StartWave(int index)
        {
            waveIndex = index;
            Wave wave = CurrentWave;
            BuildSpawnQueue(wave);
            stateEndTime = Time.time + activeWaves.AnnouncementDuration;
            SetState(WaveState.Announcing);
            WaveAnnounced?.Invoke(WaveNumber, wave.TotalCount, IsFinalWave);
            RemainingChanged?.Invoke(Remaining);

            // Pools are filled before the fight, so no monster has to be made mid-wave.
            foreach (WaveGroup group in wave.Groups)
            {
                if (group.Monster != null)
                    monsters.Prewarm(group.Monster, Mathf.Min(group.Count, wave.MaxAlive));
            }
        }

        // Every monster of the wave in a random order, apart from groups held back to spawn last.
        private void BuildSpawnQueue(Wave wave)
        {
            spawnQueue.Clear();
            foreach (WaveGroup group in wave.Groups)
            {
                if (group.Monster == null || group.SpawnLast)
                    continue;
                for (int i = 0; i < group.Count; i++)
                    spawnQueue.Add(group.Monster);
            }

            for (int i = spawnQueue.Count - 1; i > 0; i--)
            {
                int other = Random.Range(0, i + 1);
                (spawnQueue[i], spawnQueue[other]) = (spawnQueue[other], spawnQueue[i]);
            }

            foreach (WaveGroup group in wave.Groups)
            {
                if (group.Monster == null || !group.SpawnLast)
                    continue;
                for (int i = 0; i < group.Count; i++)
                    spawnQueue.Add(group.Monster);
            }
        }

        // Spawns the next monster once its turn comes, if the wave and the level have room for another. With no spawn point
        // free right now, it tries again next frame.
        private void SpawnDue(float now)
        {
            Wave wave = CurrentWave;
            if (spawnQueue.Count == 0 || now < nextSpawnTime || waveMonsters.Count >= wave.MaxAlive ||
                monsters.Alive.Count >= monsters.MaxActiveMonsters)
                return;

            MonsterDefinition next = spawnQueue[0];
            if (!TryFindSpawnPoint(next, out Vector3 position))
                return;

            Vector3 toPlayer = Vector3.ProjectOnPlane(player.transform.position - position, Vector3.up);
            float yaw = toPlayer.sqrMagnitude > 0.01f ? Quaternion.LookRotation(toPlayer).eulerAngles.y : 0f;
            MonsterController monster = monsters.Spawn(next, position, yaw, wave.Difficulty);
            if (monster == null)
                return;

            spawnQueue.RemoveAt(0);
            waveMonsters.Add(monster);
            nextSpawnTime = now + wave.SpawnInterval;
        }

        /// <summary>
        /// A random spawn point where a monster of this kind would have room to stand, far enough from the player.
        /// </summary>
        public bool TryFindSpawnPoint(MonsterDefinition monster, out Vector3 position)
        {
            candidates.Clear();
            Vector3 playerPosition = player != null ? player.transform.position : Vector3.positiveInfinity;
            for (int i = 0; i < spawnPoints.Count; i++)
            {
                Vector3 point = spawnPoints[i];
                if (Vector3.ProjectOnPlane(point - playerPosition, Vector3.up).magnitude >= minSpawnDistance &&
                    monsters.HasRoomFor(monster, point))
                    candidates.Add(i);
            }

            if (candidates.Count == 0)
            {
                position = default;
                return false;
            }

            position = spawnPoints[candidates[Random.Range(0, candidates.Count)]];
            return true;
        }

        // A monster that has made no headway for a long while, and cannot see the player to fight them, is moved to a spawn
        // point to come at them again.
        private void RescueStuckMonsters()
        {
            stuck.Clear();
            foreach (MonsterController monster in waveMonsters)
            {
                if (monster.IsAlive && monster.Motor.StuckTime >= relocateAfterStuck && !monster.CanSeeTarget)
                    stuck.Add(monster);
            }

            foreach (MonsterController monster in stuck)
            {
                if (TryFindSpawnPoint(monster.Definition, out Vector3 position))
                    monsters.Relocate(monster, position);
            }
        }

        private void CompleteWave()
        {
            LeaveSupplies(CurrentWave);
            int number = WaveNumber;
            if (IsFinalWave)
            {
                SetState(WaveState.Cleared);
                WaveCompleted?.Invoke(number);
                foreach (Door door in arenaDoors)
                    door.Unlock();
                foreach (Door gate in gates)
                    gate.Unlock();

                AreaCleared?.Invoke();
                return;
            }

            stateEndTime = Time.time + activeWaves.Intermission;
            SetState(WaveState.Complete);
            WaveCompleted?.Invoke(number);
        }

        // Each supply goes to the next supply point that has nothing left lying on it from before.
        private void LeaveSupplies(Wave wave)
        {
            if (pickups == null || supplyPoints.Count == 0)
                return;

            foreach (Pickup supply in wave.Supplies)
            {
                if (supply == null)
                    continue;

                for (int tried = 0; tried < supplyPoints.Count; tried++)
                {
                    int point = (supplyCursor + tried) % supplyPoints.Count;
                    if (supplyItems[point] != null)
                        continue;

                    supplyItems[point] = pickups.Spawn(supply, supplyPoints[point]);
                    supplyCursor = point + 1;
                    break;
                }
            }
        }

        private void HandleMonsterKilled(MonsterController monster)
        {
            if (waveMonsters.Remove(monster))
                RemainingChanged?.Invoke(Remaining);
        }

        // Finds the arena's doors, gates, spawn points and supply points in the built level, and holds the gates shut.
        private void FindArena()
        {
            spawnPoints.Clear();
            supplyPoints.Clear();
            supplyItems.Clear();
            arenaDoors.Clear();
            gates.Clear();
            spawnQueue.Clear();
            waveMonsters.Clear();
            waveIndex = -1;
            activeWaves = null;
            SetState(WaveState.Waiting);

            LevelLayout layout = world.Layout;
            for (int y = 0; y < layout.Height; y++)
            {
                for (int x = 0; x < layout.Width; x++)
                {
                    var cell = new Vector2Int(x, y);
                    char marker = layout.GetDecor(cell);
                    if (world.TryGetDoorAt(cell, out Door door))
                    {
                        if (marker == gateMarker)
                            gates.Add(door);
                        else if (layout.GetZone(cell) == arenaZone)
                            arenaDoors.Add(door);
                        continue;
                    }

                    if (!layout.IsWalkable(cell))
                        continue;

                    if (marker == spawnMarker)
                        spawnPoints.Add(world.CellToWorld(cell));
                    else if (marker == supplyMarker)
                    {
                        supplyPoints.Add(world.CellToWorld(cell));
                        supplyItems.Add(null);
                    }
                }
            }

            foreach (Door gate in gates)
                gate.Lock();
        }

        private void SetState(WaveState state)
        {
            if (state == State)
                return;

            State = state;
            StateChanged?.Invoke(state);
        }
    }
}
