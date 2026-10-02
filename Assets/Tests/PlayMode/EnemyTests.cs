using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Wolfenstein.Combat;
using Wolfenstein.Enemies;
using Wolfenstein.Weapons;
using Wolfenstein.World;

namespace Wolfenstein.Tests
{
    /// <summary>
    /// Phase 4 behaviour: guards placed from the level layout, spotting and hearing the player, chasing them through
    /// doors, shooting them, and being hurt and killed by the pistol. Most tests set up their own guard in the spawn room
    /// or just outside its door, where the level's own guards cannot see or hear what happens.
    /// </summary>
    public sealed class EnemyTests : LevelTestFixture
    {
        private const float North = 0f;
        private const float South = 180f;

        private EnemyManager enemies;

        private WeaponDefinition Pistol => Player.Weapon.Weapon;

        [UnitySetUp]
        public IEnumerator FindEnemies()
        {
            enemies = Object.FindFirstObjectByType<EnemyManager>();
            yield break;
        }

        [Test]
        public void Level_PlacesAnIdleGuardOnEveryGuardTile()
        {
            LevelLayout layout = World.Layout;
            int guardTiles = 0;
            for (int y = 0; y < layout.Height; y++)
            {
                for (int x = 0; x < layout.Width; x++)
                {
                    if (layout.TryGetGuardSpawn(new Vector2Int(x, y), out _))
                        guardTiles++;
                }
            }

            Assert.Greater(guardTiles, 0, "The level has no guards.");
            Assert.AreEqual(guardTiles, enemies.Enemies.Count);

            foreach (EnemyController guard in enemies.Enemies)
            {
                Vector2Int cell = World.WorldToCell(guard.transform.position);
                Assert.IsTrue(layout.TryGetGuardSpawn(cell, out float yaw), $"{guard.name} is not on a guard tile.");
                Assert.AreEqual(0f, HorizontalDistance(World.CellToWorld(cell) - guard.transform.position), PositionTolerance, guard.name);
                Assert.AreEqual(0f, Mathf.DeltaAngle(yaw, guard.transform.eulerAngles.y), 0.1f, $"{guard.name} faces the wrong way.");
                Assert.AreEqual(EnemyState.Idle, guard.State, $"{guard.name} should stand guard until it notices the player.");
                Assert.AreEqual(guard.Health.Max, guard.Health.Current);
            }
        }

        [Test]
        public void GuardShots_GetLessAccurateAndWeaker_WithDistance()
        {
            EnemyDefinition guard = enemies.Enemies[0].Definition;
            float previousChance = 1f;
            int previousDamage = int.MaxValue;

            for (float distance = 0f; distance <= guard.AttackRange; distance += guard.AttackRange / 5f)
            {
                float chance = guard.HitChanceAt(distance);
                int damage = guard.DamageAt(distance);
                Assert.That(chance, Is.InRange(0f, 1f));
                Assert.LessOrEqual(chance, previousChance, $"Shots got more accurate at {distance} m.");
                Assert.LessOrEqual(damage, previousDamage, $"Shots got stronger at {distance} m.");
                Assert.Greater(damage, 0, $"A hit at {distance} m did no damage.");
                previousChance = chance;
                previousDamage = damage;
            }

            Assert.Greater(guard.HitChanceAt(0f), guard.HitChanceAt(guard.AttackRange));
        }

        [UnityTest]
        public IEnumerator Guard_SpotsThePlayerInFront_ButNotBehindIt()
        {
            EnemyController guard = SpawnGuard(Ahead(3), North);

            yield return new WaitForSeconds(1f);
            Assert.IsTrue(guard.CanSeeTarget(), "Nothing should stand between the guard and the player.");
            Assert.AreEqual(EnemyState.Idle, guard.State, "The guard noticed the player behind its back.");

            guard.transform.rotation = Quaternion.Euler(0f, South, 0f);
            yield return WaitFor(() => guard.State != EnemyState.Idle, 0.5f);
            Assert.AreNotEqual(EnemyState.Idle, guard.State, "The guard did not notice the player in front of it.");
        }

        [UnityTest]
        public IEnumerator Guard_CannotSeeThroughAClosedDoor()
        {
            Door door = SpawnRoomDoor();
            StandInFrontOf(door, 2f);
            EnemyController guard = SpawnGuard(Beyond(door, 2), door.transform.eulerAngles.y + 180f);

            yield return new WaitForSeconds(1f);
            Assert.AreEqual(EnemyState.Idle, guard.State, "The guard saw the player through the closed door.");

            door.Open();
            yield return WaitFor(() => guard.State != EnemyState.Idle, 3f);
            Assert.AreNotEqual(EnemyState.Idle, guard.State, "The guard did not see the player through the open doorway.");
        }

        [UnityTest]
        public IEnumerator Gunfire_AlertsGuardsInTheSameArea_ButNotBehindAClosedDoor()
        {
            Door door = SpawnRoomDoor();

            // Neither guard can see the player: one has its back to them across the room, the other is outside the door.
            EnemyController inRoom = SpawnGuard(SpawnCell + new Vector2Int(-4, 3), North);
            EnemyController outside = SpawnGuard(Beyond(door, 2), door.transform.eulerAngles.y);
            yield return null;
            Assert.AreEqual(EnemyState.Idle, inRoom.State, "The guard in the room noticed the player before any shot.");

            yield return PullTrigger();
            Assert.AreNotEqual(EnemyState.Idle, inRoom.State, "A guard in the same room did not hear the shot.");
            Assert.AreEqual(EnemyState.Idle, outside.State, "A guard heard the shot through the closed door.");

            door.Open();
            yield return new WaitForSeconds(Pistol.FireInterval);
            yield return PullTrigger();
            Assert.AreNotEqual(EnemyState.Idle, outside.State, "A guard did not hear the shot through the open door.");
        }

        [UnityTest]
        public IEnumerator AlertedGuard_OpensADoorToReachThePlayer()
        {
            Door door = SpawnRoomDoor();
            EnemyController guard = SpawnGuard(Beyond(door, 2), door.transform.eulerAngles.y);
            guard.Alert();

            yield return WaitFor(() => door.State != DoorState.Closed, 3f);
            Assert.AreNotEqual(DoorState.Closed, door.State, "The guard did not open the door in its way.");

            // The player's side of the door is behind it, against the door's forward direction.
            yield return WaitFor(() => DistancePastDoor(door, guard) < 0f, 8f);
            Assert.Less(DistancePastDoor(door, guard), 0f, "The guard did not come through the doorway.");
        }

        [UnityTest]
        public IEnumerator AlertedGuard_ShootsThePlayer()
        {
            EnemyController guard = SpawnGuard(Ahead(1), South);
            int hits = 0;
            int misses = 0;
            int timesHurt = 0;
            guard.Fired += hit =>
            {
                if (hit)
                    hits++;
                else
                    misses++;
            };
            PlayerHealth.Damaged += _ => timesHurt++;

            // Shots at this range usually hit, but each one can miss, so wait for the first hit.
            yield return WaitFor(() => hits > 0, 15f);

            Assert.Greater(hits, 0, $"The guard fired {misses} shots and none of them hit.");
            Assert.AreEqual(hits, timesHurt, "Every hit, and only a hit, should hurt the player.");
            Assert.Less(PlayerHealth.Current, PlayerHealth.Max);
            Assert.AreNotEqual("100%", Hud.HealthReadout, "The HUD did not show the damage.");
            Assert.Greater(Hud.DamageFlashAlpha, 0f, "Being shot should flash the screen.");
        }

        [UnityTest]
        public IEnumerator ShotInTheBack_HurtsAndAlertsTheGuard_WhichTurnsToFaceThePlayer()
        {
            EnemyController guard = SpawnGuard(Ahead(2), North);

            yield return PullTrigger();
            Assert.AreEqual(guard.Health.Max - Pistol.Damage, guard.Health.Current);
            Assert.AreEqual(EnemyState.Pain, guard.State, "A hit should stop the guard in its tracks.");

            yield return WaitFor(() => AngleToPlayer(guard) < 10f, 2f);
            Assert.Less(AngleToPlayer(guard), 10f, "The guard did not turn to face the player.");
            Assert.AreNotEqual(EnemyState.Idle, guard.State);
        }

        [UnityTest]
        public IEnumerator Pistol_KillsAGuard_WhoseBodyNoLongerStopsShots()
        {
            EnemyController guard = SpawnGuard(Ahead(2), North);
            var impacts = Player.GetComponent<BulletImpacts>();
            int shotsToKill = Mathf.CeilToInt((float)guard.Health.Max / Pistol.Damage);

            for (int shot = 0; shot < shotsToKill; shot++)
            {
                yield return new WaitForSeconds(Pistol.FireInterval);
                yield return PullTrigger();
            }

            Assert.IsTrue(guard.Health.IsDead, "The guard survived the shots.");
            Assert.AreEqual(EnemyState.Dead, guard.State);
            Assert.IsFalse(guard.GetComponent<CharacterController>().enabled, "The body should no longer be solid.");
            Assert.IsEmpty(impacts.ActiveMarks, "Shots that hit the guard should leave no mark.");

            yield return new WaitForSeconds(Pistol.FireInterval);
            yield return PullTrigger();
            Assert.AreEqual(1, impacts.ActiveMarks.Count(), "The shot did not pass over the body to the wall behind it.");
        }

        [UnityTest]
        public IEnumerator Guards_HoldFireOnceThePlayerIsDead()
        {
            EnemyController guard = SpawnGuard(Ahead(1), South);
            yield return WaitFor(() => guard.State == EnemyState.Attack, 3f);
            Assert.AreEqual(EnemyState.Attack, guard.State, "The guard never took aim.");

            int shots = 0;
            guard.Fired += _ => shots++;
            PlayerHealth.TakeDamage(new DamageInfo(PlayerHealth.Max, Player.transform.position, Vector3.zero, null));
            Vector3 position = guard.transform.position;

            yield return new WaitForSeconds(guard.Definition.AimTime + guard.Definition.MaxAttackInterval);
            Assert.AreEqual(0, shots, "The guard kept shooting at a dead player.");
            Assert.AreEqual(0f, HorizontalDistance(guard.transform.position - position), PositionTolerance, "The guard kept moving after the player died.");
        }

        private Vector2Int SpawnCell => World.WorldToCell(World.GetSpawnPose().position);

        // The spawn faces north, into a room four tiles deep.
        private Vector2Int Ahead(int tiles) => SpawnCell + Vector2Int.up * tiles;

        // A cell on the far side of a door from the spawn room, along the passage through it.
        private Vector2Int Beyond(Door door, int tiles)
        {
            Vector3 forward = door.transform.forward;
            var direction = new Vector2Int(Mathf.RoundToInt(forward.x), Mathf.RoundToInt(forward.z));
            return World.WorldToCell(door.transform.position) + direction * tiles;
        }

        private EnemyController SpawnGuard(Vector2Int cell, float yaw)
        {
            EnemyController guard = enemies.SpawnGuard(cell, yaw);

            // Physics sees a new collider's position before the next simulation step only after an explicit sync.
            Physics.SyncTransforms();
            return guard;
        }

        private float AngleToPlayer(EnemyController guard) =>
            Vector3.Angle(guard.transform.forward, Vector3.ProjectOnPlane(Player.transform.position - guard.transform.position, Vector3.up));

        private static float DistancePastDoor(Door door, EnemyController guard) =>
            Vector3.Dot(guard.transform.position - door.transform.position, door.transform.forward);
    }
}
