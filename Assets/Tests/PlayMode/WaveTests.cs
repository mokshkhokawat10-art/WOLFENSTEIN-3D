using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Wolfenstein.Combat;
using Wolfenstein.Monsters;
using Wolfenstein.Pickups;
using Wolfenstein.UI;
using Wolfenstein.Waves;
using Wolfenstein.World;

namespace Wolfenstein.Tests
{
    /// <summary>
    /// The arena fight: its spawn points, the waves starting when the player steps in, staggered spawning within the limits,
    /// waves being beaten and followed by the next, supplies between waves, the arena clearing after the final wave, and
    /// the waves of the level growing harder. Tests that need a quick fight run a small wave set of their own.
    /// </summary>
    public sealed class WaveTests : LevelTestFixture
    {
        private WaveManager waves;
        private MonsterManager monsters;
        private WaveHud waveHud;

        [UnitySetUp]
        public IEnumerator FindManagers()
        {
            waves = Object.FindFirstObjectByType<WaveManager>();
            monsters = Object.FindFirstObjectByType<MonsterManager>();
            waveHud = Object.FindFirstObjectByType<WaveHud>();
            yield break;
        }

        [Test]
        public void Arena_HasSpawnPointsOnEverySide_OnOpenFloorWithRoomToStand()
        {
            MonsterDefinition brute = Definition("BRUTE");
            Assert.GreaterOrEqual(waves.SpawnPoints.Count, 8, "The arena needs spawn points for large waves.");
            Assert.IsNotEmpty(waves.SupplyPoints, "The arena has nowhere to leave supplies.");
            Assert.IsNotEmpty(waves.ArenaDoors, "The arena has no doors to lock.");
            Assert.IsNotEmpty(waves.Gates, "The way on from the arena is not held shut.");

            Vector3 centre = ArenaCentre();
            var sides = new HashSet<Vector2Int>();
            foreach (Vector3 point in waves.SpawnPoints)
            {
                Vector2Int cell = World.WorldToCell(point);
                Assert.IsTrue(World.Layout.IsWalkable(cell) && !World.Layout.TryGetDoor(cell, out _), $"Spawn point {cell} is not open floor.");
                Assert.IsTrue(monsters.HasRoomFor(brute, point), $"Spawn point {cell} has no room for the biggest monster.");

                Vector3 away = point - centre;
                sides.Add(Mathf.Abs(away.x) > Mathf.Abs(away.z)
                    ? new Vector2Int((int)Mathf.Sign(away.x), 0)
                    : new Vector2Int(0, (int)Mathf.Sign(away.z)));
            }

            Assert.GreaterOrEqual(sides.Count, 3, "Monsters should come at the player from several directions.");
            Assert.IsTrue(waves.Gates.All(gate => gate.IsLocked), "The gate should stay locked until the arena is cleared.");
            Assert.IsTrue(waves.ArenaDoors.All(door => !door.IsLocked), "The arena's doors should be open to enter before the fight.");
        }

        [UnityTest]
        public IEnumerator EnteringTheArena_StartsWaveOne_LocksTheDoors_AndAnnouncesIt()
        {
            Assert.AreEqual(WaveState.Waiting, waves.State);
            Assert.IsFalse(waveHud.IsPanelVisible, "The wave panel should stay hidden until the fight starts.");
            int firstWave = waves.Waves.Waves[0].TotalCount;

            PlacePlayer(ArenaCentre(), 0f);
            yield return null;
            yield return null;

            Assert.AreEqual(WaveState.Announcing, waves.State, "Stepping into the arena did not start the fight.");
            Assert.AreEqual(1, waves.WaveNumber);
            Assert.AreEqual(firstWave, waves.Remaining);
            Assert.IsTrue(waves.ArenaDoors.All(door => door.IsLocked), "The arena's doors should lock during the fight.");
            Assert.IsTrue(waveHud.IsBannerVisible);
            Assert.AreEqual("WAVE 1", waveHud.BannerTitle);
            Assert.AreEqual($"ENEMIES: {firstWave}", waveHud.BannerSubtitle);
            Assert.IsTrue(waveHud.IsPanelVisible);
            Assert.AreEqual($"1 OF {waves.WaveCount}", waveHud.WaveReadout);
            Assert.AreEqual(firstWave.ToString(), waveHud.EnemiesReadout);

            yield return WaitFor(() => waves.AliveCount > 0, waves.Waves.AnnouncementDuration + 2f);
            Assert.AreEqual(WaveState.Fighting, waves.State);
            Assert.Greater(waves.AliveCount, 0, "No monsters came after the announcement.");
        }

        [UnityTest]
        public IEnumerator Wave_SpawnsOneMonsterAtATime_AwayFromThePlayer_AndNoMoreThanItsLimit()
        {
            const float interval = 0.3f;
            const int maxAlive = 3;
            MonsterDefinition stalker = Definition("STALKER");
            var spawns = new List<(float time, Vector3 position)>();
            monsters.Spawned += monster => spawns.Add((Time.time, monster.transform.position));

            PlacePlayer(ArenaCentre(), 0f);
            waves.Begin(WaveSet.Create(0.1f, 1f, new Wave(new[] { new WaveGroup(stalker, 7) }, MonsterModifiers.None, interval, maxAlive)));

            int mostAlive = 0;
            float end = Time.time + 3f;
            while (Time.time < end)
            {
                mostAlive = Mathf.Max(mostAlive, waves.AliveCount);
                yield return null;
            }

            Assert.AreEqual(maxAlive, mostAlive, "The wave should fill up to its limit and no further.");
            Assert.AreEqual(maxAlive, spawns.Count, "More monsters spawned while the wave was at its limit.");
            Assert.AreEqual(7 - maxAlive, waves.QueuedCount);

            // Killing them lets the rest come.
            yield return KillWaveMonstersUntil(() => waves.QueuedCount == 0, 6f);
            Assert.AreEqual(7, spawns.Count);

            for (int i = 0; i < spawns.Count; i++)
            {
                Vector3 position = spawns[i].position;
                Assert.IsTrue(waves.SpawnPoints.Any(point => HorizontalDistance(point - position) < 0.01f), "A monster spawned away from the spawn points.");
                Assert.GreaterOrEqual(HorizontalDistance(position - Player.transform.position), waves.MinSpawnDistance - 0.5f,
                    "A monster spawned too close to the player.");
                if (i > 0)
                    Assert.GreaterOrEqual(spawns[i].time - spawns[i - 1].time, interval - 0.02f, "Monsters spawned together rather than in turn.");
            }
        }

        [UnityTest]
        public IEnumerator Spawning_NeverPutsAMonsterOnThePlayer_OrOnAnotherMonster()
        {
            MonsterDefinition brute = Definition("BRUTE");
            foreach (Vector3 point in waves.SpawnPoints.Take(4))
            {
                PlacePlayer(point, 0f);
                Physics.SyncTransforms();
                Assert.IsFalse(monsters.HasRoomFor(brute, point), "A monster would have room to stand inside the player.");

                for (int i = 0; i < 20; i++)
                {
                    Assert.IsTrue(waves.TryFindSpawnPoint(brute, out Vector3 position));
                    Assert.GreaterOrEqual(HorizontalDistance(position - point), waves.MinSpawnDistance, "A spawn point was picked right by the player.");
                }
            }

            yield return null;

            // A monster standing on a spawn point takes it out of use until it moves off.
            PlacePlayer(ArenaCentre(), 0f);
            Vector3 taken = waves.SpawnPoints[0];
            MonsterController monster = monsters.Spawn(brute, taken, 0f, MonsterModifiers.None, hunt: false);
            Assert.IsNotNull(monster);
            Assert.IsFalse(monsters.HasRoomFor(Definition("STALKER"), taken), "Two monsters could spawn on top of each other.");
            for (int i = 0; i < 30; i++)
            {
                Assert.IsTrue(waves.TryFindSpawnPoint(brute, out Vector3 position));
                Assert.Greater(HorizontalDistance(position - taken), 0.5f, "A spawn point with a monster on it was picked.");
            }
        }

        [UnityTest]
        public IEnumerator Wave_WaitsForRoom_WhileTheLevelIsFullOfMonsters()
        {
            MonsterDefinition stalker = Definition("STALKER");
            Vector3 spawnRoom = World.GetSpawnPose().position;
            while (monsters.Alive.Count < monsters.MaxActiveMonsters)
                monsters.Spawn(stalker, spawnRoom + Vector3.forward * 6f, 0f, MonsterModifiers.None, hunt: false);

            PlacePlayer(ArenaCentre(), 0f);
            waves.Begin(WaveSet.Create(0f, 1f, new Wave(new[] { new WaveGroup(stalker, 4) }, MonsterModifiers.None, 0.1f, 4)));
            yield return new WaitForSeconds(0.6f);
            Assert.AreEqual(4, waves.QueuedCount, "The wave spawned past the level's limit.");
            Assert.AreEqual(monsters.MaxActiveMonsters, monsters.Alive.Count);

            // Clearing the others out lets the wave in.
            foreach (MonsterController other in monsters.Alive.ToList())
                monsters.Despawn(other);
            yield return WaitFor(() => waves.QueuedCount == 0, 3f);
            Assert.AreEqual(0, waves.QueuedCount);
            Assert.AreEqual(4, waves.AliveCount);
        }

        [UnityTest]
        public IEnumerator BeatingAWave_CompletesIt_LeavesSupplies_AndBringsTheNextWave()
        {
            MonsterDefinition stalker = Definition("STALKER");
            MonsterDefinition spitter = Definition("SPITTER");
            Pickup clip = LevelItem(item => item.Definition.Ammo > 0);
            var completed = new List<int>();
            var announced = new List<int>();
            var secondWaveKinds = new List<MonsterDefinition>();
            waves.WaveCompleted += number => completed.Add(number);
            waves.WaveAnnounced += (number, _, _) => announced.Add(number);
            monsters.Spawned += monster =>
            {
                if (waves.WaveNumber == 2)
                    secondWaveKinds.Add(monster.Definition);
            };

            PlacePlayer(ArenaCentre(), 0f);
            waves.Begin(WaveSet.Create(0.1f, 1f,
                new Wave(new[] { new WaveGroup(stalker, 3) }, MonsterModifiers.None, 0.2f, 3, new[] { clip, clip }),
                new Wave(new[] { new WaveGroup(stalker, 2), new WaveGroup(spitter, 1) }, new MonsterModifiers(1.5f, 1f, 1f), 0.2f, 3)));

            yield return KillWaveMonstersUntil(() => waves.State == WaveState.Complete, 8f);
            Assert.AreEqual(WaveState.Complete, waves.State, "Killing every monster did not beat the wave.");
            Assert.AreEqual(new[] { 1 }, completed);
            Assert.AreEqual(0, waves.Remaining);
            Assert.AreEqual("WAVE COMPLETE", waveHud.BannerTitle);
            Assert.AreEqual("0", waveHud.EnemiesReadout);

            Pickup[] supplies = Object.FindObjectsByType<Pickup>(FindObjectsSortMode.None)
                .Where(item => waves.SupplyPoints.Any(point => HorizontalDistance(point - item.transform.position) < 0.1f)).ToArray();
            Assert.AreEqual(2, supplies.Length, "The wave's supplies were not left at the supply points.");

            // After a short break the next wave is announced, and it brings its own mix of monsters.
            Assert.AreEqual(new[] { 1 }, announced);
            yield return WaitFor(() => waves.WaveNumber == 2, 2f);
            Assert.AreEqual(2, waves.WaveNumber);
            Assert.AreEqual(3, waves.Remaining);
            Assert.AreEqual("WAVE 2", waveHud.BannerTitle);

            yield return WaitFor(() => secondWaveKinds.Count == 3, 3f);
            Assert.AreEqual(2, secondWaveKinds.Count(kind => kind == stalker));
            Assert.AreEqual(1, secondWaveKinds.Count(kind => kind == spitter));
            MonsterController tougher = waves.WaveMonsters.First(monster => monster.Definition == stalker);
            Assert.AreEqual(Mathf.RoundToInt(stalker.MaxHealth * 1.5f), tougher.Health.Max, "The later wave's monsters should be tougher.");
        }

        [UnityTest]
        public IEnumerator BeatingTheFinalWave_ClearsTheArena_AndOpensTheWayOn()
        {
            MonsterDefinition stalker = Definition("STALKER");
            int cleared = 0;
            waves.AreaCleared += () => cleared++;

            PlacePlayer(ArenaCentre(), 0f);
            waves.Begin(WaveSet.Create(0.1f, 0.3f,
                new Wave(new[] { new WaveGroup(stalker, 2) }, MonsterModifiers.None, 0.2f, 2),
                new Wave(new[] { new WaveGroup(stalker, 2) }, MonsterModifiers.None, 0.2f, 2)));
            Assert.IsTrue(waves.ArenaDoors.All(door => door.IsLocked));

            yield return KillWaveMonstersUntil(() => waves.State == WaveState.Cleared, 10f);
            Assert.AreEqual(WaveState.Cleared, waves.State, "Beating the final wave did not clear the arena.");
            Assert.AreEqual(1, cleared);
            Assert.AreEqual(2, waves.WaveNumber);
            Assert.IsTrue(waves.ArenaDoors.All(door => !door.IsLocked), "The arena's doors should unlock once it is cleared.");
            Assert.IsTrue(waves.Gates.All(gate => !gate.IsLocked), "The way on should open once the arena is cleared.");
            Assert.AreEqual("AREA CLEARED", waveHud.BannerTitle);
            Assert.IsFalse(waveHud.IsPanelVisible);

            // Nothing more comes, and the gate can now be opened.
            yield return new WaitForSeconds(1f);
            Assert.AreEqual(0, waves.AliveCount);
            Assert.AreEqual(WaveState.Cleared, waves.State);
            Door gate = waves.Gates[0];
            gate.Open();
            yield return WaitFor(() => gate.State == DoorState.Open, 3f);
            Assert.AreEqual(DoorState.Open, gate.State);
        }

        [UnityTest]
        public IEnumerator PlayerShutOutOfTheArenaMidFight_CanGetBackIn()
        {
            MonsterDefinition stalker = Definition("STALKER");
            PlacePlayer(ArenaCentre(), 0f);
            waves.Begin(WaveSet.Create(5f, 1f, new Wave(new[] { new WaveGroup(stalker, 2) }, MonsterModifiers.None, 0.5f, 2)));
            yield return null;
            Assert.IsTrue(waves.ArenaDoors.All(door => door.IsLocked));

            // Just outside an arena door, as if the player had slipped out before it shut.
            Door door = waves.ArenaDoors[waves.ArenaDoors.Count - 1];
            PlacePlayer(door.transform.position - door.transform.forward * World.TileSize, 0f);
            yield return null;
            Assert.IsFalse(waves.IsInArena(Player.transform.position));
            Assert.IsTrue(waves.ArenaDoors.All(arenaDoor => !arenaDoor.IsLocked), "The player was shut out of the fight.");

            PlacePlayer(ArenaCentre(), 0f);
            yield return null;
            Assert.IsTrue(waves.ArenaDoors.All(arenaDoor => arenaDoor.IsLocked), "The doors did not lock again once the player was back.");
        }

        [Test]
        public void Demons_JoinOnlyTheLaterWaves_InGrowingNumbers()
        {
            IReadOnlyList<Wave> arena = waves.Waves.Waves;
            MonsterDefinition demon = Definition("DEMON");
            for (int i = 0; i < 3; i++)
                Assert.AreEqual(0, arena[i].CountOf(demon), $"Wave {i + 1} should hold no demons.");
            for (int i = 3; i < arena.Count; i++)
            {
                Assert.Greater(arena[i].CountOf(demon), 0, $"Wave {i + 1} should bring demons.");
                Assert.GreaterOrEqual(arena[i].CountOf(demon), arena[i - 1].CountOf(demon));
            }
        }

        [Test]
        public void ArenaWaves_GrowInSizeAndDanger_EndingInALargeFinalWave()
        {
            IReadOnlyList<Wave> arena = waves.Waves.Waves;
            MonsterDefinition stalker = Definition("STALKER");
            MonsterDefinition brute = Definition("BRUTE");
            MonsterDefinition spitter = Definition("SPITTER");
            MonsterDefinition revenant = Definition("REVENANT");

            Assert.GreaterOrEqual(arena.Count, 5);
            Assert.AreEqual(8, arena[0].TotalCount, "The first wave should be eight basic monsters.");
            Assert.AreEqual(8, arena[0].CountOf(stalker));
            Assert.AreEqual(12, arena[1].CountOf(stalker));
            Assert.AreEqual(2, arena[1].CountOf(spitter));
            Assert.AreEqual(15, arena[2].CountOf(stalker));
            Assert.AreEqual(3, arena[2].CountOf(spitter));
            Assert.AreEqual(2, arena[2].CountOf(brute));
            Assert.GreaterOrEqual(arena[3].TotalCount, 20);
            Assert.GreaterOrEqual(arena[4].CountOf(revenant), 1, "The fifth wave should bring an elite.");

            for (int i = 1; i < arena.Count; i++)
            {
                Wave previous = arena[i - 1];
                Wave wave = arena[i];
                Assert.Greater(wave.TotalCount, previous.TotalCount, $"Wave {i + 1} is no bigger than the one before.");
                Assert.GreaterOrEqual(wave.Difficulty.health, previous.Difficulty.health);
                Assert.GreaterOrEqual(wave.Difficulty.speed, previous.Difficulty.speed);
                Assert.GreaterOrEqual(wave.Difficulty.damage, previous.Difficulty.damage);
                Assert.LessOrEqual(wave.SpawnInterval, previous.SpawnInterval, $"Wave {i + 1} comes in no faster.");
                Assert.GreaterOrEqual(wave.MaxAlive, previous.MaxAlive);
                Assert.LessOrEqual(wave.MaxAlive, monsters.MaxActiveMonsters);
            }

            Wave final = arena[arena.Count - 1];
            Assert.GreaterOrEqual(final.TotalCount, 30, "The final wave should be a large one.");
            Assert.Greater(final.CountOf(revenant), arena[arena.Count - 2].CountOf(revenant), "The final wave should bring more elites.");
            Assert.Less(final.Difficulty.health, 2f, "Monsters should not grow endlessly tougher.");

            // Basic monsters stay a single pistol shot however late the wave.
            int pistol = Player.Weapon.Weapon.Damage;
            Assert.LessOrEqual(final.Difficulty.ScaleHealth(stalker.MaxHealth), pistol);
        }

        // Kills every monster of the current wave as it appears, until the condition holds or the time runs out.
        private IEnumerator KillWaveMonstersUntil(System.Func<bool> condition, float timeout)
        {
            float deadline = Time.time + timeout;
            while (!condition() && Time.time < deadline)
            {
                foreach (MonsterController monster in waves.WaveMonsters.ToList())
                    monster.Health.TakeDamage(new DamageInfo(100000, monster.transform.position, Vector3.forward, Player.gameObject));

                // Keeps the player alive through a long fight, so the waves never pause.
                PlayerHealth.Heal(PlayerHealth.Max);
                yield return null;
            }
        }

        // The open floor nearest the middle of the arena.
        private Vector3 ArenaCentre()
        {
            LevelLayout layout = World.Layout;
            var cells = new List<Vector2Int>();
            for (int y = 0; y < layout.Height; y++)
            {
                for (int x = 0; x < layout.Width; x++)
                {
                    var cell = new Vector2Int(x, y);
                    if (layout.GetZone(cell) == waves.ArenaZone && layout.IsWalkable(cell) && !layout.TryGetDoor(cell, out _))
                        cells.Add(cell);
                }
            }

            Vector2 middle = cells.Aggregate(Vector2.zero, (sum, cell) => sum + cell) / cells.Count;
            return World.CellToWorld(cells.OrderBy(cell => (cell - middle).sqrMagnitude).First());
        }

        private static Pickup LevelItem(System.Func<Pickup, bool> match) =>
            Object.FindObjectsByType<Pickup>(FindObjectsSortMode.None).First(match);

        private MonsterDefinition Definition(string displayName) =>
            waves.Waves.Waves.SelectMany(wave => wave.Groups).Select(group => group.Monster).First(monster => monster.DisplayName == displayName);
    }
}
