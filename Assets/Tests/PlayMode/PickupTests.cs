using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Wolfenstein.Combat;
using Wolfenstein.Core;
using Wolfenstein.Enemies;
using Wolfenstein.Pickups;
using Wolfenstein.Weapons;
using Wolfenstein.World;
using Object = UnityEngine.Object;

namespace Wolfenstein.Tests
{
    /// <summary>
    /// Phase 5 behaviour: items placed from the level layout, collecting them by walking over them, health and ammo
    /// only being taken when of use, treasure and kills adding to the score, and the clip a dead guard drops.
    /// Tests bring copies of the level's items into the spawn room, where the level's guards cannot see or hear what happens.
    /// </summary>
    public sealed class PickupTests : LevelTestFixture
    {
        private const float North = 0f;

        private PickupManager pickups;
        private EnemyManager enemies;
        private Score score;

        private AmmoSupply Ammo => Player.Weapon.Ammo;
        private WeaponDefinition Pistol => Player.Weapon.Weapon;
        private Vector2Int SpawnCell => World.WorldToCell(World.GetSpawnPose().position);

        [UnitySetUp]
        public IEnumerator FindManagers()
        {
            pickups = Object.FindFirstObjectByType<PickupManager>();
            enemies = Object.FindFirstObjectByType<EnemyManager>();
            score = Player.GetComponent<Score>();
            yield break;
        }

        [Test]
        public void Level_PlacesAnItemOnEveryItemTile_InTheMiddleOfTheTileOnTheFloor()
        {
            LevelLayout layout = World.Layout;
            var itemTiles = new HashSet<Vector2Int>();
            for (int y = 0; y < layout.Height; y++)
            {
                for (int x = 0; x < layout.Width; x++)
                {
                    if (layout.TryGetItem(new Vector2Int(x, y), out _))
                        itemTiles.Add(new Vector2Int(x, y));
                }
            }

            Pickup[] items = FindItems();
            Assert.IsNotEmpty(itemTiles, "The level has no items.");
            Assert.AreEqual(itemTiles.Count, items.Length);

            foreach (Pickup item in items)
            {
                Vector2Int cell = World.WorldToCell(item.transform.position);
                Assert.IsTrue(itemTiles.Remove(cell), $"{item.name} is not on an item tile, or shares one.");
                Assert.AreEqual(0f, HorizontalDistance(World.CellToWorld(cell) - item.transform.position), PositionTolerance, item.name);
                Assert.AreEqual(World.transform.position.y, item.transform.position.y, PositionTolerance, $"{item.name} is not on the floor.");
            }

            Assert.IsTrue(items.Any(item => item.Definition.Health > 0), "The level has no health items.");
            Assert.IsTrue(items.Any(item => item.Definition.Ammo > 0), "The level has no ammo.");
            Assert.IsTrue(items.Any(item => item.Definition.Score > 0), "The level has no treasure.");
        }

        [Test]
        public void Player_StartsWithNoPoints_ShownOnTheHud()
        {
            Assert.AreEqual(0, score.Current);
            Assert.AreEqual("0", Hud.ScoreReadout);
            Assert.AreEqual(0f, Hud.PickupFlashAlpha);
        }

        [UnityTest]
        public IEnumerator WalkingOverAnAmmoClip_AddsItsRounds_AndFlashesTheScreen()
        {
            Pickup clip = PlaceCopy(ItemGivingMost(item => item.Ammo), World.CellToWorld(Ahead(1)));
            int rounds = clip.Definition.Ammo;
            int startAmmo = Ammo.Current;

            // The HUD reacts first, as it subscribed when the level loaded.
            float flashOnCollection = 0f;
            Player.GetComponent<PickupCollector>().Collected += _ => flashOnCollection = Hud.PickupFlashAlpha;

            yield return null;
            Assert.IsTrue(clip != null, "The clip was collected from a tile away.");

            yield return WalkForwardUntil(() => clip == null, 2f);
            Assert.IsTrue(clip == null, "Walking over the clip did not collect it.");
            Assert.AreEqual(startAmmo + rounds, Ammo.Current);
            Assert.AreEqual(Ammo.Current.ToString(), Hud.AmmoReadout, "The HUD did not show the new ammo count.");
            Assert.Greater(flashOnCollection, 0f, "Collecting an item should flash the screen.");

            yield return new WaitForSeconds(1f);
            Assert.AreEqual(0f, Hud.PickupFlashAlpha, "The pickup flash should fade out.");
        }

        [UnityTest]
        public IEnumerator AmmoClip_IsLeftBehindWhileAmmoIsFull_AndTakenOnceARoundIsSpent()
        {
            Ammo.Add(Ammo.Max);
            Pickup clip = PlaceCopy(ItemGivingMost(item => item.Ammo), Player.transform.position);

            yield return null;
            yield return null;
            Assert.IsTrue(clip != null, "The clip was taken although the player's ammo was full.");
            Assert.AreEqual(Ammo.Max, Ammo.Current);

            yield return PullTrigger();
            yield return WaitFor(() => clip == null, 0.5f);
            Assert.IsTrue(clip == null, "The clip was not taken once there was room for it.");
            Assert.AreEqual(Ammo.Max, Ammo.Current, "Ammo should be topped up to the maximum and no further.");
        }

        [UnityTest]
        public IEnumerator HealthItem_IsLeftBehindAtFullHealth_AndTakenOnceHurt()
        {
            Pickup kit = PlaceCopy(ItemGivingMost(item => item.Health), Player.transform.position);
            int healing = kit.Definition.Health;

            yield return null;
            yield return null;
            Assert.IsTrue(kit != null, "The health item was taken although the player was unhurt.");
            Assert.AreEqual(PlayerHealth.Max, PlayerHealth.Current);

            PlayerHealth.TakeDamage(Damage(40));
            int hurtHealth = PlayerHealth.Current;
            yield return WaitFor(() => kit == null, 0.5f);

            Assert.IsTrue(kit == null, "The health item was not taken once the player was hurt.");
            Assert.AreEqual(Mathf.Min(PlayerHealth.Max, hurtHealth + healing), PlayerHealth.Current);
            Assert.AreEqual($"{PlayerHealth.Current}%", Hud.HealthReadout, "The HUD did not show the restored health.");
        }

        [UnityTest]
        public IEnumerator Treasure_AddsItsPointsToTheScore()
        {
            Pickup treasure = PlaceCopy(ItemGivingMost(item => item.Score), World.CellToWorld(Ahead(1)));
            int points = treasure.Definition.Score;

            yield return WalkForwardUntil(() => treasure == null, 2f);

            Assert.IsTrue(treasure == null, "Walking over the treasure did not collect it.");
            Assert.AreEqual(points, score.Current);
            Assert.AreEqual(points.ToString(), Hud.ScoreReadout, "The HUD did not show the new score.");
        }

        [UnityTest]
        public IEnumerator DeadPlayer_CollectsNothing()
        {
            PlayerHealth.TakeDamage(Damage(PlayerHealth.Max));
            Pickup treasure = PlaceCopy(ItemGivingMost(item => item.Score), Player.transform.position);

            yield return null;
            yield return null;

            Assert.IsTrue(treasure != null, "A dead player collected an item.");
            Assert.AreEqual(0, score.Current);
        }

        [UnityTest]
        public IEnumerator KillingAGuard_ScoresItsPoints_AndLeavesAClipWhereItFell()
        {
            EnemyController guard = SpawnGuard(Ahead(2), North);
            int shotsToKill = Mathf.CeilToInt((float)guard.Health.Max / Pistol.Damage);

            for (int shot = 0; shot < shotsToKill; shot++)
            {
                yield return new WaitForSeconds(Pistol.FireInterval);
                yield return PullTrigger();
            }

            Assert.IsTrue(guard.Health.IsDead, "The guard survived the shots.");
            Assert.AreEqual(guard.Definition.KillScore, score.Current);
            Assert.AreEqual(score.Current.ToString(), Hud.ScoreReadout);

            Pickup clip = DropOf(guard);
            Assert.Greater(clip.Definition.Ammo, 0, "The guard should drop ammo.");
            Assert.AreEqual(0f, HorizontalDistance(clip.transform.position - guard.transform.position), PositionTolerance, "The clip is not where the guard fell.");
            Assert.AreEqual(World.transform.position.y, clip.transform.position.y, PositionTolerance, "The clip is not on the floor.");

            int ammo = Ammo.Current;
            yield return WalkForwardUntil(() => clip == null, 3f);
            Assert.IsTrue(clip == null, "Walking over the dropped clip did not collect it.");
            Assert.AreEqual(ammo + guard.Definition.Drop.Definition.Ammo, Ammo.Current);
        }

        [UnityTest]
        public IEnumerator GuardKilledInADoorway_LeavesItsClipBesideTheDoor()
        {
            Door door = SpawnRoomDoor();
            Vector2Int doorCell = World.WorldToCell(door.transform.position);
            door.Open();
            yield return WaitFor(() => door.State == DoorState.Open, 3f);

            EnemyController guard = SpawnGuard(doorCell, door.transform.eulerAngles.y);
            yield return null;
            guard.Health.TakeDamage(new DamageInfo(guard.Health.Max, guard.transform.position, Vector3.zero, null));

            Vector2Int dropCell = World.WorldToCell(DropOf(guard).transform.position);
            Vector2Int[] besideTheDoor =
            {
                World.WorldToCell(door.transform.position + door.transform.forward * World.TileSize),
                World.WorldToCell(door.transform.position - door.transform.forward * World.TileSize)
            };
            Assert.AreNotEqual(doorCell, dropCell, "The clip was left in the doorway, where the door closes over it.");
            CollectionAssert.Contains(besideTheDoor, dropCell, "The clip should land on a tile the doorway leads to.");
        }

        [Test]
        public void Drop_InADoorway_LandsInTheMiddleOfTheTileOnTheSideItFellOn()
        {
            Door door = SpawnRoomDoor();
            Pickup prefab = enemies.Enemies[0].Definition.Drop;

            foreach (float side in new[] { 1f, -1f })
            {
                Vector3 passage = door.transform.forward * side;
                Pickup item = pickups.Drop(prefab, door.transform.position + passage * 0.4f);

                Vector3 expected = door.transform.position + passage * World.TileSize;
                Assert.AreEqual(0f, HorizontalDistance(item.transform.position - expected), PositionTolerance,
                    $"A clip dropped {(side > 0f ? "past" : "short of")} the panel landed in the wrong place.");
            }
        }

        // The spawn faces north, into a room four tiles deep.
        private Vector2Int Ahead(int tiles) => SpawnCell + Vector2Int.up * tiles;

        private static Pickup[] FindItems() => Object.FindObjectsByType<Pickup>(FindObjectsSortMode.None);

        // The level's item that gives the most of something, such as the biggest health item.
        private static Pickup ItemGivingMost(Func<PickupDefinition, int> amount)
        {
            Pickup best = FindItems().OrderByDescending(item => amount(item.Definition)).First();
            Assume.That(amount(best.Definition), Is.GreaterThan(0), "The level has no such item.");
            return best;
        }

        private Pickup PlaceCopy(Pickup original, Vector3 position)
        {
            Pickup copy = pickups.Spawn(original, position);

            // Physics sees a new collider's position before the next simulation step only after an explicit sync.
            Physics.SyncTransforms();
            return copy;
        }

        private EnemyController SpawnGuard(Vector2Int cell, float yaw)
        {
            EnemyController guard = enemies.SpawnGuard(cell, yaw);
            Physics.SyncTransforms();
            return guard;
        }

        // The only item of the guard's drop kind: the level itself places none.
        private static Pickup DropOf(EnemyController guard)
        {
            PickupDefinition drop = guard.Definition.Drop.Definition;
            return FindItems().Single(item => item.Definition == drop);
        }

        private IEnumerator WalkForwardUntil(Func<bool> condition, float timeoutSeconds)
        {
            InputFixture.Press(Keyboard.wKey, queueEventOnly: true);
            yield return WaitFor(condition, timeoutSeconds);
            InputFixture.Release(Keyboard.wKey, queueEventOnly: true);
            yield return null;
        }

        private DamageInfo Damage(int amount) => new DamageInfo(amount, Player.transform.position, Vector3.zero, null);
    }
}
