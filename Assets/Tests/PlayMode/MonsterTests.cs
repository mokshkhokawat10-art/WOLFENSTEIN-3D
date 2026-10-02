using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Wolfenstein.Combat;
using Wolfenstein.Core;
using Wolfenstein.Monsters;
using Wolfenstein.Waves;
using Wolfenstein.Weapons;
using Wolfenstein.World;

namespace Wolfenstein.Tests
{
    /// <summary>
    /// The monsters: lurkers placed from the level's decor map, spawning and pooling, health and difficulty, melee and
    /// ranged attacks, death, points for kills, and the limit on how many are alive at once. Most tests spawn their own
    /// monsters in the spawn room, where the level's lurkers cannot see or hear what happens.
    /// </summary>
    public sealed class MonsterTests : LevelTestFixture
    {
        private const float North = 0f;
        private const float South = 180f;

        private MonsterManager monsters;
        private WaveManager waves;
        private Score score;

        private WeaponDefinition Pistol => Player.Weapon.Weapon;

        [UnitySetUp]
        public IEnumerator FindManagers()
        {
            monsters = Object.FindFirstObjectByType<MonsterManager>();
            waves = Object.FindFirstObjectByType<WaveManager>();
            score = Player.GetComponent<Score>();
            yield break;
        }

        [UnityTest]
        public IEnumerator Level_PlacesALurkingMonsterOnEveryLurkerMark()
        {
            LevelLayout layout = World.Layout;
            var marks = new List<Vector2Int>();
            for (int y = 0; y < layout.Height; y++)
            {
                for (int x = 0; x < layout.Width; x++)
                {
                    var cell = new Vector2Int(x, y);
                    if ("1234".IndexOf(layout.GetDecor(cell)) >= 0 && layout.IsWalkable(cell))
                        marks.Add(cell);
                }
            }

            Assert.IsNotEmpty(marks, "The level has no lurking monsters.");
            Assert.AreEqual(marks.Count, monsters.Alive.Count);
            foreach (MonsterController monster in monsters.Alive)
            {
                Vector2Int cell = World.WorldToCell(monster.transform.position);
                Assert.IsTrue(marks.Remove(cell), $"{monster.name} is not on a lurker mark, or shares one.");
                Assert.AreEqual(0f, HorizontalDistance(World.CellToWorld(cell) - monster.transform.position), PositionTolerance, monster.name);
            }

            // Behind closed doors, they rise and then lurk rather than hunt.
            yield return new WaitForSeconds(MaxSpawnDuration() + 0.3f);
            foreach (MonsterController monster in monsters.Alive)
                Assert.AreEqual(MonsterState.Idle, monster.State, $"{monster.name} should lurk until it notices the player.");
        }

        [UnityTest]
        public IEnumerator Spawn_RaisesAMonsterAtFullHealth_WhichThenHuntsThePlayer()
        {
            MonsterDefinition stalker = Definition("STALKER");
            MonsterController monster = Spawn(stalker, Ahead(3), South);

            Assert.IsNotNull(monster);
            Assert.AreEqual(MonsterState.Spawning, monster.State, "A new monster should rise out of the floor first.");
            Assert.AreEqual(stalker.MaxHealth, monster.Health.Max);
            Assert.AreEqual(stalker.MaxHealth, monster.Health.Current);
            Assert.AreEqual(0f, HorizontalDistance(World.CellToWorld(Ahead(3)) - monster.transform.position), PositionTolerance);

            yield return new WaitForSeconds(stalker.SpawnDuration * 0.5f);
            Assert.AreEqual(MonsterState.Spawning, monster.State, "The monster stopped rising too soon.");

            yield return WaitFor(() => monster.State != MonsterState.Spawning, stalker.SpawnDuration);
            Assert.That(monster.State, Is.EqualTo(MonsterState.Chase).Or.EqualTo(MonsterState.Attack), "A spawned monster should hunt.");
            Assert.AreEqual(PlayerHealth, monster.Target, "The monster should hunt the player.");
        }

        [Test]
        public void Difficulty_ScalesAMonstersHealth()
        {
            MonsterDefinition brute = Definition("BRUTE");
            MonsterController monster = Spawn(brute, Ahead(3), South, new MonsterModifiers(1.5f, 1f, 1f));

            Assert.AreEqual(Mathf.RoundToInt(brute.MaxHealth * 1.5f), monster.Health.Max);
            Assert.AreEqual(monster.Health.Max, monster.Health.Current);
            Assert.AreEqual(brute.MoveSpeed, monster.MoveSpeed, 0.001f);
        }

        [UnityTest]
        public IEnumerator MeleeMonster_RunsUpAndStrikesThePlayer()
        {
            MonsterDefinition stalker = Definition("STALKER");
            MonsterController monster = Spawn(stalker, Ahead(2), South);
            int blowsLanded = 0;
            monster.Attacked += (attack, landed) =>
            {
                if (attack == MonsterAttack.Melee && landed)
                    blowsLanded++;
            };

            yield return WaitFor(() => PlayerHealth.Current < PlayerHealth.Max, 6f);

            Assert.Greater(blowsLanded, 0, "The monster never landed a blow.");
            Assert.AreEqual(blowsLanded * stalker.MeleeDamage, PlayerHealth.Max - PlayerHealth.Current, "Each blow should do the monster's damage.");
            Assert.Greater(Hud.DamageFlashAlpha, 0f, "Being struck should flash the screen.");
        }

        [UnityTest]
        public IEnumerator RangedMonster_FiresBoltsThatHurtThePlayer_FromADistance()
        {
            MonsterDefinition spitter = Definition("SPITTER");
            Assert.IsTrue(spitter.HasRanged && !spitter.HasMelee);
            MonsterController monster = Spawn(spitter, Ahead(3), South);
            int volleys = 0;
            monster.Attacked += (attack, landed) =>
            {
                if (attack == MonsterAttack.Ranged)
                    volleys++;
            };

            yield return WaitFor(() => monsters.Projectiles.Count > 0, 6f);
            Assert.Greater(monsters.Projectiles.Count, 0, "The monster never fired.");
            Assert.AreEqual(spitter.ProjectileDamage, monsters.Projectiles[0].Damage);

            yield return WaitFor(() => PlayerHealth.Current < PlayerHealth.Max, 4f);
            Assert.Less(PlayerHealth.Current, PlayerHealth.Max, "The bolt did not hurt the player.");
            Assert.AreEqual(0, (PlayerHealth.Max - PlayerHealth.Current) % spitter.ProjectileDamage, "Each bolt should do the monster's damage.");
            Assert.Greater(HorizontalDistance(monster.transform.position - Player.transform.position), spitter.MinFireRange,
                "The monster came too close for a ranged fighter.");

            // Volleys come no quicker than the cooldown allows.
            int firedSoFar = volleys;
            yield return new WaitForSeconds(spitter.FireCooldown * 0.6f);
            Assert.LessOrEqual(volleys - firedSoFar, 1, "The monster fired without waiting for its cooldown.");
        }

        [UnityTest]
        public IEnumerator Pistol_DropsABasicMonsterWithOneShot_ButAHeavyOneTakesMany()
        {
            MonsterController stalker = Spawn(Definition("STALKER"), Ahead(3), South, hunt: false);
            yield return PullTrigger();
            Assert.IsTrue(stalker.Health.IsDead, "A pistol shot should drop a basic monster.");
            Assert.AreEqual(MonsterState.Dead, stalker.State);

            MonsterController brute = Spawn(Definition("BRUTE"), Ahead(3), South, hunt: false);
            const int shots = 3;
            for (int shot = 0; shot < shots; shot++)
            {
                yield return new WaitForSeconds(Pistol.FireInterval);
                yield return PullTrigger();
            }

            Assert.IsFalse(brute.Health.IsDead, "A heavy monster should shrug off a few pistol shots.");
            Assert.AreEqual(brute.Health.Max - shots * Pistol.Damage, brute.Health.Current);
            Assert.GreaterOrEqual(Mathf.CeilToInt((float)brute.Health.Max / Pistol.Damage), 8, "A heavy monster should take many shots to kill.");
        }

        [UnityTest]
        public IEnumerator Death_LeavesABodyThatNoLongerBlocks_AndThePoolReusesIt()
        {
            MonsterDefinition stalker = Definition("STALKER");
            MonsterController monster = Spawn(stalker, Ahead(3), South, hunt: false);
            MonsterController killed = null;
            monsters.Killed += dead => killed = dead;
            int created = monsters.MonstersCreated;

            Kill(monster);
            yield return null;

            Assert.AreEqual(monster, killed, "The kill was not reported.");
            Assert.AreEqual(MonsterState.Dead, monster.State);
            Assert.IsFalse(monster.IsAlive);
            Assert.IsFalse(monster.GetComponent<CharacterController>().enabled, "The body should no longer be solid.");
            Assert.IsFalse(monsters.Alive.Contains(monster));
            Assert.IsTrue(monsters.Bodies.Contains(monster), "The body should lie where it fell for a while.");

            // Deaths stop an attack in progress: nothing more comes from a dead monster.
            int attacks = 0;
            monster.Attacked += (_, _) => attacks++;
            yield return new WaitForSeconds(1f);
            Assert.AreEqual(0, attacks);

            monsters.Despawn(monster);
            Assert.IsFalse(monsters.Bodies.Contains(monster));
            Assert.IsFalse(monster.gameObject.activeSelf, "A cleared body should go back to its pool.");

            MonsterController reused = Spawn(stalker, Ahead(2), South);
            Assert.AreEqual(monster, reused, "A new monster should be taken from the pool.");
            Assert.AreEqual(created, monsters.MonstersCreated, "Reusing a monster should not make a new one.");
            Assert.AreEqual(reused.Health.Max, reused.Health.Current, "A reused monster should come back at full health.");
            Assert.AreEqual(MonsterState.Spawning, reused.State);
            Assert.IsTrue(reused.GetComponent<CharacterController>().enabled);
        }

        [Test]
        public void Kill_ScoresTheMonstersPoints_AndCountsTheKill()
        {
            MonsterDefinition revenant = Definition("REVENANT");
            MonsterDefinition stalker = Definition("STALKER");
            Assert.Greater(revenant.KillScore, stalker.KillScore, "The elite should be worth more than a basic monster.");

            Kill(Spawn(stalker, Ahead(3), South, hunt: false));
            Assert.AreEqual(stalker.KillScore, score.Current);
            Assert.AreEqual(1, score.Kills);

            Kill(Spawn(revenant, Ahead(2), South, hunt: false));
            Assert.AreEqual(stalker.KillScore + revenant.KillScore, score.Current);
            Assert.AreEqual(2, score.Kills);
            Assert.AreEqual(score.Current.ToString(), Hud.ScoreReadout, "The HUD did not show the points.");
        }

        [Test]
        public void MonsterKinds_LookAndFightDifferently()
        {
            MonsterDefinition stalker = Definition("STALKER");
            MonsterDefinition brute = Definition("BRUTE");
            MonsterDefinition spitter = Definition("SPITTER");
            MonsterDefinition revenant = Definition("REVENANT");

            Assert.Greater(stalker.MoveSpeed, brute.MoveSpeed, "Basic monsters should be quicker than heavy ones.");
            Assert.Greater(stalker.MoveSpeed, spitter.MoveSpeed, "Basic monsters should be quicker than ranged ones.");
            Assert.Less(stalker.MaxHealth, spitter.MaxHealth);
            Assert.GreaterOrEqual(brute.MaxHealth, stalker.MaxHealth * 5, "Heavy monsters should be far tougher.");
            Assert.Greater(brute.MeleeDamage, stalker.MeleeDamage * 3, "Heavy monsters should hit far harder.");
            Assert.Greater(brute.MeleeSplashRadius, 0f, "Heavy blows should land with a shockwave.");
            Assert.IsTrue(spitter.HasRanged, "Ranged monsters should fire projectiles.");
            Assert.Greater(spitter.PreferredDistance, stalker.MeleeReach * 4f, "Ranged monsters should keep their distance.");
            Assert.IsTrue(revenant.HasMelee && revenant.HasRanged && revenant.CanCharge, "The elite should have every attack.");
            Assert.Greater(revenant.MaxHealth, brute.MaxHealth);
            Assert.Greater(revenant.MoveSpeed, stalker.MoveSpeed);
            Assert.Greater(revenant.KillScore, brute.KillScore);

            // Each kind has its own model rather than one model recoloured.
            var models = new HashSet<string>();
            foreach (MonsterDefinition kind in new[] { stalker, brute, spitter, revenant })
            {
                MonsterController monster = Spawn(kind, Ahead(3), South, hunt: false);
                models.Add(string.Join(",", monster.GetComponentsInChildren<MeshRenderer>(true).Select(part => part.name).OrderBy(part => part)));
                Assert.IsNotNull(monster.GetComponent<MonsterView>(), $"{kind.name} has no view.");
                monsters.Despawn(monster);
            }

            Assert.AreEqual(4, models.Count, "Every kind of monster should be built differently.");
        }

        [Test]
        public void Demon_FightsUpCloseAndFromRange_BetweenTheOthersInToughness()
        {
            MonsterDefinition demon = Definition("DEMON");
            MonsterDefinition spitter = Definition("SPITTER");
            MonsterDefinition brute = Definition("BRUTE");
            MonsterDefinition stalker = Definition("STALKER");

            Assert.IsTrue(demon.HasMelee && demon.HasRanged, "The demon should both claw and throw fire.");
            Assert.IsFalse(demon.CanCharge, "Charging is the elite's.");
            Assert.That(demon.MaxHealth, Is.GreaterThan(spitter.MaxHealth).And.LessThan(brute.MaxHealth));
            Assert.That(demon.KillScore, Is.GreaterThan(spitter.KillScore).And.LessThan(brute.KillScore));
            Assert.That(demon.MoveSpeed, Is.GreaterThan(brute.MoveSpeed).And.LessThan(stalker.MoveSpeed));

            MonsterController monster = Spawn(demon, Ahead(3), South);
            Assert.AreEqual(demon.MaxHealth, monster.Health.Current);
            Assert.AreEqual(MonsterState.Spawning, monster.State);
        }

        [UnityTest]
        public IEnumerator EveryMonsterKind_HasItsOwnBody_LightEnoughForCrowds_AndClearOfTheCeiling()
        {
            // Along the corridor outside the spawn room, where the closed door hides them from the player.
            MonsterDefinition[] kinds = waves.Waves.Waves.SelectMany(wave => wave.Groups).Select(group => group.Monster).Distinct().ToArray();
            Assert.AreEqual(5, kinds.Length, "The waves should bring five kinds of monster.");
            int[] columns = { 10, 13, 15, 18, 22 };
            MonsterController[] spawned = kinds.Select((kind, i) => Spawn(kind, new Vector2Int(columns[i], 7), South, hunt: false)).ToArray();

            yield return WaitFor(() => spawned.All(monster => monster.State != MonsterState.Spawning), MaxSpawnDuration() + 0.5f);

            var bodies = new HashSet<string>();
            foreach (MonsterController monster in spawned)
            {
                Renderer[] parts = monster.GetComponentsInChildren<Renderer>().Where(part => !(part is ParticleSystemRenderer)).ToArray();
                Bounds bounds = parts[0].bounds;
                foreach (Renderer part in parts)
                    bounds.Encapsulate(part.bounds);

                string kind = monster.Definition.DisplayName;
                float top = bounds.max.y - World.transform.position.y;
                Assert.LessOrEqual(top, World.WallHeight - 0.35f, $"The {kind} would reach into the lamps and beams under the ceiling.");
                Assert.Greater(top, 1.4f, $"The {kind} is too small to read from across a room.");
                Assert.LessOrEqual(parts.Length, 24, $"The {kind} is drawn with too many renderers for a crowd of them.");
                bodies.Add(string.Join(",", monster.GetComponentsInChildren<MeshRenderer>(true).Select(part => part.name).OrderBy(part => part)));
            }

            Assert.AreEqual(kinds.Length, bodies.Count, "Every kind of monster should have a body of its own.");
        }

        [UnityTest]
        public IEnumerator SpawnLimit_KeepsNoMoreThanTheMaximumAlive()
        {
            MonsterDefinition stalker = Definition("STALKER");
            int before = monsters.Alive.Count;
            int spawned = 0;
            foreach (Vector2Int cell in SpawnRoomCells().Where(cell => HorizontalDistance(World.CellToWorld(cell) - Player.transform.position) > 4f))
            {
                if (Spawn(stalker, cell, North, hunt: false) != null)
                    spawned++;
            }

            Assert.AreEqual(monsters.MaxActiveMonsters - before, spawned);
            Assert.AreEqual(monsters.MaxActiveMonsters, monsters.Alive.Count);
            Assert.IsNull(monsters.Spawn(stalker, World.CellToWorld(Ahead(3)), 0f, MonsterModifiers.None),
                "A monster spawned beyond the limit.");

            // A kill makes room for one more.
            Kill(monsters.Alive.Last());
            Assert.IsNotNull(monsters.Spawn(stalker, World.CellToWorld(Ahead(3)), 0f, MonsterModifiers.None));
            yield return null;
            Assert.LessOrEqual(monsters.Alive.Count, monsters.MaxActiveMonsters);
        }

        [UnityTest]
        public IEnumerator Monsters_StopAttacking_OnceThePlayerIsDead()
        {
            MonsterController monster = Spawn(Definition("STALKER"), Ahead(1), South);
            yield return WaitFor(() => monster.State == MonsterState.Attack, 4f);
            Assert.AreEqual(MonsterState.Attack, monster.State, "The monster never went for the player.");

            int attacks = 0;
            monster.Attacked += (_, _) => attacks++;
            PlayerHealth.TakeDamage(new DamageInfo(PlayerHealth.Max, Player.transform.position, Vector3.zero, null));

            // A monster that was still running when it began its attack takes a moment to come to a stop.
            yield return new WaitForSeconds(0.3f);
            Vector3 position = monster.transform.position;

            yield return new WaitForSeconds(1.2f);
            Assert.AreEqual(0, attacks, "The monster kept attacking a dead player.");
            Assert.AreEqual(0f, HorizontalDistance(monster.transform.position - position), 0.3f, "The monster kept moving after the player died.");
        }

        private Vector2Int SpawnCell => World.WorldToCell(World.GetSpawnPose().position);

        // The spawn faces north, into a room four tiles deep.
        private Vector2Int Ahead(int tiles) => SpawnCell + Vector2Int.up * tiles;

        // The open floor of the room the player starts in.
        private IEnumerable<Vector2Int> SpawnRoomCells()
        {
            var room = new HashSet<Vector2Int> { SpawnCell };
            var frontier = new Queue<Vector2Int>(room);
            while (frontier.Count > 0)
            {
                Vector2Int cell = frontier.Dequeue();
                foreach (Vector2Int step in new[] { Vector2Int.up, Vector2Int.right, Vector2Int.down, Vector2Int.left })
                {
                    Vector2Int next = cell + step;
                    if (World.Layout.IsWalkable(next) && !World.Layout.TryGetDoor(next, out _) && room.Add(next))
                        frontier.Enqueue(next);
                }
            }

            return room.OrderBy(cell => cell.y).ThenBy(cell => cell.x);
        }

        private MonsterController Spawn(MonsterDefinition definition, Vector2Int cell, float yaw, MonsterModifiers? difficulty = null,
            bool hunt = true) =>
            monsters.Spawn(definition, World.CellToWorld(cell), yaw, difficulty ?? MonsterModifiers.None, hunt);

        private void Kill(MonsterController monster) =>
            monster.Health.TakeDamage(new DamageInfo(100000, monster.transform.position, Vector3.forward, Player.gameObject));

        // Every kind of monster appears in the arena's waves, which name them.
        private MonsterDefinition Definition(string displayName) =>
            waves.Waves.Waves.SelectMany(wave => wave.Groups).Select(group => group.Monster).First(monster => monster.DisplayName == displayName);

        private float MaxSpawnDuration() =>
            waves.Waves.Waves.SelectMany(wave => wave.Groups).Max(group => group.Monster.SpawnDuration);
    }
}
