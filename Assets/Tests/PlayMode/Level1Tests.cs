using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Wolfenstein.Audio;
using Wolfenstein.Combat;
using Wolfenstein.Core;
using Wolfenstein.Monsters;
using Wolfenstein.Pickups;
using Wolfenstein.Player;
using Wolfenstein.Progression;
using Wolfenstein.UI;
using Wolfenstein.Waves;
using Wolfenstein.World;

namespace Wolfenstein.Tests
{
    /// <summary>
    /// Phase 8 on the first level of the episode: its objectives worked through in order, the security key and the doors
    /// it opens, the planned encounters, the arena, the exit, the end-of-level tally with its real figures, going on to the
    /// second level with the score carried over, and a restart after death that puts the whole level back.
    /// </summary>
    public sealed class Level1Tests : LevelTestFixture
    {
        private readonly List<SoundCue> requested = new List<SoundCue>();
        private LevelManager levelManager;
        private LevelDefinition level;
        private ObjectiveTracker objectives;
        private AccessControl access;
        private EncounterDirector encounters;
        private LevelExit exit;
        private LevelStatistics statistics;
        private WaveManager waves;
        private MonsterManager monsters;
        private LevelHud levelHud;
        private TallyScreen tally;
        private ProgressionAudio progressionAudio;

        protected override string LevelSceneName => "Level1";

        [UnitySetUp]
        public IEnumerator FindLevel()
        {
            levelManager = Object.FindFirstObjectByType<LevelManager>();
            level = levelManager.Level;
            objectives = Object.FindFirstObjectByType<ObjectiveTracker>();
            access = Object.FindFirstObjectByType<AccessControl>();
            encounters = Object.FindFirstObjectByType<EncounterDirector>();
            exit = Object.FindFirstObjectByType<LevelExit>();
            statistics = Object.FindFirstObjectByType<LevelStatistics>();
            waves = Object.FindFirstObjectByType<WaveManager>();
            monsters = Object.FindFirstObjectByType<MonsterManager>();
            levelHud = Object.FindFirstObjectByType<LevelHud>();
            tally = Object.FindFirstObjectByType<TallyScreen>();
            progressionAudio = Object.FindFirstObjectByType<ProgressionAudio>();
            requested.Clear();
            Object.FindFirstObjectByType<AudioManager>().CueRequested += (cue, position) => requested.Add(cue);
            yield break;
        }

        [Test]
        public void Level1_StartsOnItsFirstObjective_WithItsLoadout_AndAnnouncesItself()
        {
            Assert.IsNotNull(level, "The scene has no level definition.");
            Assert.AreEqual(1, level.Number);
            Assert.AreEqual(GameState.Playing, GameManager.CurrentState);
            Assert.AreEqual(level.StartingHealth, PlayerHealth.Current);
            Assert.AreEqual(level.StartingAmmo, Player.Weapon.Ammo.Current);
            Assert.AreEqual(0, Player.GetComponent<Score>().Current, "A level loaded on its own starts from no score.");

            Assert.AreEqual(ObjectiveKind.CollectKey, objectives.Current.Kind);
            Assert.IsTrue(levelHud.IsObjectiveVisible, "The objective should be on the HUD.");
            Assert.AreEqual("OBJECTIVE", levelHud.ObjectiveCaption);
            Assert.AreEqual("FIND THE SECURITY KEY", levelHud.ObjectiveReadout);
            Assert.IsTrue(levelHud.IsMessageVisible, "The level should announce itself.");
            Assert.AreEqual("LEVEL 1", levelHud.MessageTitle);
            Assert.AreEqual(level.DisplayName, levelHud.MessageSubtitle);
        }

        [Test]
        public void LevelData_ChainsTheEpisode_AndEachLevelCanBeWonOnlyInOrder()
        {
            LevelDefinition level2 = level.NextLevel;
            Assert.IsNotNull(level2, "Level 1 should lead on to Level 2.");
            Assert.AreEqual(2, level2.Number);
            Assert.IsTrue(level2.IsLastLevel, "Level 2 is the last level of the episode.");

            foreach (LevelDefinition definition in new[] { level, level2 })
            {
                Assert.AreEqual(ObjectiveKind.ReachExit, definition.Objectives.Last().Kind, $"{definition.name} should end at its exit.");
                Assert.IsNotNull(definition.Waves, $"{definition.name} has no arena waves.");
                LevelLayout layout = definition.Layout;
                layout.TryFindSpawn(out Vector2Int spawn, out _);

                // With every door open the whole level can be crossed, from the spawn to the arena and on to the exit.
                HashSet<Vector2Int> open = Reachable(layout, spawn, _ => true);
                Assert.IsTrue(Cells(layout).Any(cell => layout.GetZone(cell) == 'a' && open.Contains(cell)), $"{definition.name}'s arena cannot be reached.");
                Assert.IsTrue(Cells(layout).Any(cell => layout.GetZone(cell) == definition.ExitZone && open.Contains(cell)), $"{definition.name}'s exit cannot be reached.");

                // With the locked doors shut, what unlocks them can still be reached, but the arena and exit cannot.
                HashSet<Vector2Int> locked = Reachable(layout, spawn, cell => definition.FindLock(layout.GetDecor(cell)) == null && layout.GetDecor(cell) != definition.ExitDoorMarker);
                Assert.IsFalse(Cells(layout).Any(cell => layout.GetZone(cell) == 'a' && locked.Contains(cell)), $"{definition.name}'s arena can be reached without unlocking anything.");
                foreach (DoorLock doorLock in definition.DoorLocks)
                    Assert.IsTrue(Cells(layout).Any(cell => layout.GetDecor(cell) == doorLock.Marker && layout.TryGetDoor(cell, out _)), $"{doorLock.DoorName} marks no door.");
                foreach (EncounterDefinition encounter in definition.Encounters)
                    Assert.IsTrue(Cells(layout).Any(cell => layout.GetDecor(cell) == encounter.SpawnMarker && layout.IsWalkable(cell)), $"{encounter.Name} has nowhere to rise.");
            }

            Vector2Int key = Cells(level.Layout).Single(cell => level.Layout.TryGetItem(cell, out char item) && item == 'y');
            level.Layout.TryFindSpawn(out Vector2Int start, out _);
            Assert.IsTrue(Reachable(level.Layout, start, cell => level.FindLock(level.Layout.GetDecor(cell)) == null).Contains(key),
                "The security key must be reachable without it.");
        }

        [UnityTest]
        public IEnumerator SecurityDoors_StayLocked_AndTellThePlayerWhy()
        {
            Door security = DoorMarked('Y');
            Door wing = DoorMarked('W');
            Assert.IsTrue(security.IsLocked && wing.IsLocked, "Both ways into the security wing start locked.");
            Assert.IsTrue(DoorMarked('E').IsLocked, "The exit starts locked.");
            Assert.IsFalse(SpawnRoomDoor().IsLocked, "Not every door is locked.");
            Assert.LessOrEqual(FindDoors().Count(door => door.IsLocked), 3);

            StandInFrontOf(security, 2f);
            yield return null;
            yield return TapKey(Keyboard.eKey);

            Assert.AreEqual(DoorState.Closed, security.State, "A locked door opened.");
            Assert.IsTrue(levelHud.IsMessageVisible);
            Assert.AreEqual("SECURITY DOOR", levelHud.MessageTitle);
            Assert.AreEqual("LOCKED - FIND THE SECURITY KEY", levelHud.MessageSubtitle);
            Assert.IsTrue(requested.Contains(Object.FindFirstObjectByType<DoorAudio>().SoundsOf(security).Locked), "A locked door should sound locked.");
        }

        [UnityTest]
        public IEnumerator TheSecurityKey_CompletesItsObjective_AndGrantsAccessThroughBothDoors()
        {
            Door security = DoorMarked('Y');
            Pickup key = Object.FindObjectsByType<Pickup>(FindObjectsSortMode.None).Single(item => item.Definition.AccessKey != null);
            AccessKey securityKey = key.Definition.AccessKey;

            PlacePlayer(key.transform.position, 0f);
            yield return WaitFor(() => objectives.Current?.Kind != ObjectiveKind.CollectKey, 1f);

            Assert.IsTrue(Player.GetComponent<KeyRing>().Has(securityKey), "The key was not picked up.");
            Assert.AreEqual(ObjectiveKind.ReachZone, objectives.Current.Kind, "Picking up the key should complete its objective.");
            Assert.IsFalse(security.IsLocked || DoorMarked('W').IsLocked, "The key should unlock both security doors.");
            Assert.AreEqual("SECURITY ACCESS GRANTED", levelHud.MessageTitle);
            Assert.AreEqual("OBJECTIVE COMPLETE", levelHud.ObjectiveCaption);
            Assert.IsTrue(requested.Contains(key.Definition.CollectSound), "The key made no sound.");
            Assert.IsTrue(requested.Contains(progressionAudio.AccessGrantedSound));
            Assert.IsTrue(requested.Contains(progressionAudio.ObjectiveCompleteSound));

            yield return new WaitForSeconds(2.2f);
            Assert.AreEqual("OBJECTIVE", levelHud.ObjectiveCaption);
            Assert.AreEqual("REACH THE ARENA", levelHud.ObjectiveReadout, "The next objective should follow.");

            StandInFrontOf(security, 2f);
            yield return null;
            yield return TapKey(Keyboard.eKey);
            Assert.AreEqual(DoorState.Opening, security.State, "The unlocked security door should open.");
        }

        [UnityTest]
        public IEnumerator Encounter_RisesOnce_WhenThePlayerFirstEntersItsZone()
        {
            Assert.IsFalse(encounters.IsStarted("CORRIDOR AMBUSH"));
            int before = monsters.Alive.Count;

            Door spawnDoor = SpawnRoomDoor();
            PlacePlayer(spawnDoor.transform.position + spawnDoor.transform.forward * World.TileSize, 0f);
            yield return null;
            yield return null;

            Assert.IsTrue(encounters.IsStarted("CORRIDOR AMBUSH"), "Stepping into the corridor should spring its ambush.");
            IReadOnlyCollection<MonsterController> ambush = encounters.AliveIn("CORRIDOR AMBUSH");
            Assert.AreEqual(3, ambush.Count);
            Assert.IsTrue(ambush.All(monster => monster.Definition.DisplayName == "STALKER"));
            Assert.AreEqual(before + 3, monsters.Alive.Count);
            Assert.IsTrue(ambush.All(monster => HorizontalDistance(monster.transform.position - Player.transform.position) >= 6f),
                "An ambush should not rise on top of the player.");

            PlacePlayer(World.GetSpawnPose().position, 0f);
            yield return null;
            PlacePlayer(spawnDoor.transform.position + spawnDoor.transform.forward * World.TileSize, 0f);
            yield return null;
            yield return null;
            Assert.AreEqual(before + 3, monsters.Alive.Count, "An encounter should only rise once.");

            foreach (MonsterController monster in ambush.ToList())
                monster.Health.TakeDamage(new DamageInfo(1000, monster.transform.position, Vector3.forward, Player.gameObject));
            Assert.IsTrue(encounters.IsCleared("CORRIDOR AMBUSH"));
        }

        [UnityTest]
        public IEnumerator Arena_StartsWhenEntered_SealsItsDoors_AndClearingItOpensTheExit()
        {
            yield return CollectTheKey();
            Assert.IsTrue(DoorMarked('E').IsLocked);

            PlacePlayer(ArenaCentre(), 0f);
            waves.Begin(QuickWaves());
            yield return WaitFor(() => objectives.Current?.Kind == ObjectiveKind.ClearArena, 1f);

            Assert.AreEqual(ObjectiveKind.ClearArena, objectives.Current.Kind, "Entering the arena should complete REACH THE ARENA.");
            Assert.AreNotEqual(WaveState.Waiting, waves.State, "The arena should start its waves.");
            Assert.IsTrue(waves.ArenaDoors.All(door => door.IsLocked), "The arena should seal its doors during the fight.");
            Assert.IsTrue(DoorMarked('E').IsLocked, "The exit stays locked while the arena is being fought.");

            yield return KillWaveUntil(() => waves.State == WaveState.Cleared, 8f);
            yield return null;

            Assert.AreEqual(ObjectiveKind.ReachExit, objectives.Current.Kind, "Clearing the arena should complete SURVIVE THE ARENA.");
            Assert.IsTrue(exit.IsReady);
            Assert.IsFalse(DoorMarked('E').IsLocked, "The exit should unlock once the arena is clear.");
            Assert.IsTrue(waves.ArenaDoors.All(door => !door.IsLocked), "The arena should open again.");
            Assert.AreEqual("EXIT", levelHud.MessageTitle);
            Assert.AreEqual("READY", levelHud.MessageSubtitle);
            Assert.IsTrue(requested.Contains(progressionAudio.ExitReadySound));
        }

        [UnityTest]
        public IEnumerator Exit_StaysShut_UntilEveryOtherObjectiveIsDone()
        {
            Door exitDoor = DoorMarked('E');
            StandInFrontOf(exitDoor, 2f);
            yield return null;
            yield return TapKey(Keyboard.eKey);
            Assert.AreEqual(DoorState.Closed, exitDoor.State);
            Assert.AreEqual("EXIT", levelHud.MessageTitle);
            Assert.AreEqual("LOCKED", levelHud.MessageSubtitle);

            // Even standing inside the lift, the level is not over until the exit is ready.
            PlacePlayer(ExitRoom(), 0f);
            yield return null;
            yield return null;
            Assert.IsFalse(exit.IsReached);
            Assert.AreEqual(GameState.Playing, GameManager.CurrentState);
        }

        [UnityTest]
        public IEnumerator ReachingTheExit_CompletesTheLevel_AndTheTallyShowsWhatReallyHappened()
        {
            // One shot that hits and one that misses, and one treasure out of the level's.
            MonsterDefinition stalker = waves.Waves.Waves.SelectMany(wave => wave.Groups).First(group => group.Monster.DisplayName == "STALKER").Monster;
            MonsterController target = monsters.Spawn(stalker, Player.transform.position + Player.transform.forward * 5f, 180f, MonsterModifiers.None, hunt: false);
            yield return WaitFor(() => target.State == MonsterState.Idle, stalker.SpawnDuration + 1f);
            Physics.SyncTransforms();
            yield return PullTrigger();
            Assert.IsTrue(target.Health.IsDead, "The shot at the stalker should kill it.");
            Player.Look.SetOrientation(Player.transform.eulerAngles.y + 90f, 0f);
            yield return new WaitForSeconds(Player.Weapon.Weapon.FireInterval + 0.05f);
            yield return PullTrigger();

            Pickup treasure = Object.FindObjectsByType<Pickup>(FindObjectsSortMode.None).OrderBy(item => Vector3.Distance(item.transform.position, Player.transform.position))
                .First(item => item.Definition.Score > 0);
            PlacePlayer(treasure.transform.position, 0f);
            yield return WaitFor(() => statistics.TreasureFound == 1, 1f);

            yield return CollectTheKey();
            PlacePlayer(ArenaCentre(), 0f);
            waves.Begin(QuickWaves());
            yield return KillWaveUntil(() => waves.State == WaveState.Cleared, 8f);
            yield return WaitFor(() => exit.IsReady, 1f);

            PlacePlayer(ExitRoom(), 0f);
            yield return WaitFor(() => GameManager.CurrentState == GameState.LevelComplete, 1f);

            Assert.AreEqual(GameState.LevelComplete, GameManager.CurrentState, "Walking into the ready exit should complete the level.");
            Assert.IsTrue(objectives.IsFinished);
            Assert.IsFalse(Player.InputEnabled, "The player stops at the end of the level.");
            Assert.IsFalse(statistics.IsRunning, "The clock stops at the exit.");
            Assert.AreEqual("LEVEL COMPLETE", levelHud.MessageTitle);
            Assert.IsTrue(requested.Contains(progressionAudio.LevelCompleteSound));

            float elapsed = statistics.ElapsedSeconds;
            yield return WaitFor(() => GameManager.CurrentState == GameState.Tally, levelManager.CompleteDuration + 1f);
            Assert.AreEqual(GameState.Tally, GameManager.CurrentState);
            Assert.IsTrue(tally.IsVisible);
            Assert.AreEqual("LEVEL COMPLETE", tally.Title);
            Assert.AreEqual("LEVEL 1 - THE BUNKER", tally.Subtitle);

            yield return WaitFor(() => levelManager.CanContinue, levelManager.TallyDelay + 1f);
            Assert.AreEqual(6, tally.RevealedRows);
            Assert.IsTrue(tally.IsPromptVisible);
            Assert.IsTrue(requested.Contains(progressionAudio.TallyLineSound));

            int seconds = Mathf.FloorToInt(elapsed);
            Assert.AreEqual($"{seconds / 60:00}:{seconds % 60:00}", tally.ValueOf(TallyStat.Time));
            Assert.AreEqual(elapsed, statistics.ElapsedSeconds, 0.001f, "The clock should stay stopped on the tally.");
            Assert.AreEqual(2, statistics.ShotsFired);
            Assert.AreEqual(1, statistics.ShotsHit);
            Assert.AreEqual("50%", tally.ValueOf(TallyStat.Accuracy));
            int kills = Player.GetComponent<Score>().Kills;
            Assert.GreaterOrEqual(kills, 2);
            Assert.AreEqual($"{kills} / {statistics.TotalEnemies}", tally.ValueOf(TallyStat.Kills));
            Assert.AreEqual($"{Mathf.RoundToInt(100f / statistics.TreasureTotal)}%", tally.ValueOf(TallyStat.Treasure));
            Assert.AreEqual(Player.GetComponent<Score>().Current.ToString(), tally.ValueOf(TallyStat.Score));
        }

        [UnityTest]
        public IEnumerator ContinuingFromTheTally_LoadsLevel2_WithTheScoreCarriedOver()
        {
            Player.GetComponent<Score>().Add(1234);
            levelManager.CompleteLevel();
            yield return WaitFor(() => levelManager.CanContinue, levelManager.CompleteDuration + levelManager.TallyDelay + 1f);

            yield return TapKey(Keyboard.enterKey);
            Assert.IsTrue(requested.Contains(progressionAudio.LevelTransitionSound), "Leaving the level should be heard.");
            yield return WaitFor(() => SceneManager.GetActiveScene().name == "Level2", levelManager.TransitionDelay + 2f);
            Assert.AreEqual("Level2", SceneManager.GetActiveScene().name, "Continuing should load the next level.");

            LevelManager next = null;
            yield return WaitFor(() =>
            {
                next = Object.FindFirstObjectByType<LevelManager>();
                return next != null && Object.FindFirstObjectByType<GameManager>().CurrentState == GameState.Playing;
            }, 3f);

            Assert.AreEqual(2, next.Level.Number);
            Assert.AreEqual(1234, Object.FindFirstObjectByType<PlayerController>().GetComponent<Score>().Current, "The score should carry into Level 2.");
            Assert.AreEqual(ObjectiveKind.ThrowSwitch, Object.FindFirstObjectByType<ObjectiveTracker>().Current.Kind);
        }

        [UnityTest]
        public IEnumerator DeathAndRestart_PutTheWholeLevelBack()
        {
            yield return CollectTheKey();
            Door spawnDoor = SpawnRoomDoor();
            PlacePlayer(spawnDoor.transform.position + spawnDoor.transform.forward * World.TileSize, 0f);
            yield return null;
            yield return null;
            Assert.IsTrue(encounters.IsStarted("CORRIDOR AMBUSH"));
            Player.GetComponent<Score>().Add(500);

            PlayerHealth.TakeDamage(new DamageInfo(1000, Player.transform.position, Vector3.zero, null));
            Assert.AreEqual(GameState.GameOver, GameManager.CurrentState);
            Assert.IsTrue(Hud.IsDeathScreenVisible, "The death screen should come up.");
            yield return new WaitForSeconds(levelManager.RestartDelay + 0.1f);
            Assert.IsTrue(Hud.IsRestartPromptVisible);

            GameManager oldGameManager = GameManager;
            yield return PullTrigger();
            yield return WaitFor(() => oldGameManager == null, 2f);
            GameManager newGameManager = null;
            yield return WaitFor(() =>
            {
                newGameManager = Object.FindFirstObjectByType<GameManager>();
                return newGameManager != null && newGameManager.CurrentState == GameState.Playing;
            }, 3f);

            Assert.AreEqual("Level1", SceneManager.GetActiveScene().name);
            var player = Object.FindFirstObjectByType<PlayerController>();
            Assert.AreEqual(0, player.GetComponent<Score>().Current, "The level should start again from the score it was started with.");
            Assert.AreEqual(0, player.GetComponent<KeyRing>().Keys.Count, "Keys should not survive a restart.");
            Assert.AreEqual(ObjectiveKind.CollectKey, Object.FindFirstObjectByType<ObjectiveTracker>().Current.Kind, "Objectives should start again.");
            Assert.IsFalse(Object.FindFirstObjectByType<EncounterDirector>().IsStarted("CORRIDOR AMBUSH"), "Encounters should wait again.");
            Assert.AreEqual(WaveState.Waiting, Object.FindFirstObjectByType<WaveManager>().State);
            Assert.IsTrue(Object.FindObjectsByType<Door>(FindObjectsSortMode.None).Count(door => door.IsLocked) >= 3, "The locked doors should be locked again.");
        }

        // ---- Helpers -------------------------------------------------------------------------------------------------------

        private IEnumerator CollectTheKey()
        {
            Pickup key = Object.FindObjectsByType<Pickup>(FindObjectsSortMode.None).Single(item => item.Definition.AccessKey != null);
            PlacePlayer(key.transform.position, 0f);
            yield return WaitFor(() => objectives.Current?.Kind != ObjectiveKind.CollectKey, 1f);
            Assert.AreNotEqual(ObjectiveKind.CollectKey, objectives.Current.Kind, "The key could not be collected.");
        }

        private WaveSet QuickWaves()
        {
            MonsterDefinition stalker = waves.Waves.Waves.SelectMany(wave => wave.Groups).First(group => group.Monster.DisplayName == "STALKER").Monster;
            return WaveSet.Create(0.1f, 0.2f, new Wave(new[] { new WaveGroup(stalker, 1) }, MonsterModifiers.None, 0.1f, 1));
        }

        private IEnumerator KillWaveUntil(System.Func<bool> condition, float timeout)
        {
            float deadline = Time.time + timeout;
            while (!condition() && Time.time < deadline)
            {
                foreach (MonsterController monster in waves.WaveMonsters.ToList())
                    monster.Health.TakeDamage(new DamageInfo(100000, monster.transform.position, Vector3.forward, Player.gameObject));
                PlayerHealth.Heal(PlayerHealth.Max);
                yield return null;
            }
        }

        private Door DoorMarked(char marker) =>
            World.Doors.Single(entry => World.Layout.GetDecor(entry.Key) == marker).Value;

        private Vector3 ExitRoom() =>
            World.CellToWorld(Cells(World.Layout).First(cell => World.Layout.GetZone(cell) == level.ExitZone && World.Layout.IsWalkable(cell) &&
                                                                 !World.Layout.TryGetDoor(cell, out _)));

        private Vector3 ArenaCentre()
        {
            List<Vector2Int> cells = Cells(World.Layout)
                .Where(cell => World.Layout.GetZone(cell) == waves.ArenaZone && World.Layout.IsWalkable(cell) && !World.Layout.TryGetDoor(cell, out _))
                .ToList();
            Vector2 middle = cells.Aggregate(Vector2.zero, (sum, cell) => sum + (Vector2)cell) / cells.Count;
            return World.CellToWorld(cells.OrderBy(cell => (cell - middle).sqrMagnitude).First());
        }

        private static IEnumerable<Vector2Int> Cells(LevelLayout layout)
        {
            for (int y = 0; y < layout.Height; y++)
            {
                for (int x = 0; x < layout.Width; x++)
                    yield return new Vector2Int(x, y);
            }
        }

        // Every cell reachable from a start, stepping only onto walkable cells the rule lets through.
        private static HashSet<Vector2Int> Reachable(LevelLayout layout, Vector2Int start, System.Func<Vector2Int, bool> passable)
        {
            var seen = new HashSet<Vector2Int> { start };
            var frontier = new Queue<Vector2Int>(seen);
            Vector2Int[] steps = { Vector2Int.up, Vector2Int.right, Vector2Int.down, Vector2Int.left };
            while (frontier.Count > 0)
            {
                Vector2Int cell = frontier.Dequeue();
                foreach (Vector2Int step in steps)
                {
                    Vector2Int next = cell + step;
                    if (layout.IsWalkable(next) && passable(next) && seen.Add(next))
                        frontier.Enqueue(next);
                }
            }

            return seen;
        }
    }
}
