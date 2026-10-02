using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Wolfenstein.Combat;
using Wolfenstein.Weapons;
using Wolfenstein.World;

namespace Wolfenstein.Tests
{
    /// <summary>
    /// Phase 3 behaviour: firing the pistol with the Attack input, ammo, hitscan damage and bullet marks.
    /// </summary>
    public sealed class WeaponTests : LevelTestFixture
    {
        private const float TargetDistance = 4f;

        private int shotsFired;

        private WeaponController Weapon => Player.Weapon;
        private AmmoSupply Ammo => Player.Weapon.Ammo;
        private WeaponDefinition Pistol => Player.Weapon.Weapon;

        [UnitySetUp]
        public IEnumerator CountShots()
        {
            shotsFired = 0;
            Weapon.Fired += _ => shotsFired++;
            yield break;
        }

        [Test]
        public void Player_StartsWithFullHealthAndAmmo_ShownOnTheHud()
        {
            Assert.IsNotNull(Pistol, "The player has no weapon equipped.");
            Assert.AreEqual(PlayerHealth.Max, PlayerHealth.Current);
            Assert.Greater(Ammo.Current, 0);
            Assert.AreEqual("100%", Hud.HealthReadout);
            Assert.AreEqual(Ammo.Current.ToString(), Hud.AmmoReadout);
            Assert.IsFalse(Hud.IsDeathScreenVisible);
            Assert.IsTrue(Hud.IsCrosshairVisible);
        }

        [UnityTest]
        public IEnumerator PressingFire_ShootsOnceAndSpendsOneRound()
        {
            int startAmmo = Ammo.Current;

            yield return PullTrigger();

            Assert.AreEqual(1, shotsFired);
            Assert.AreEqual(startAmmo - Pistol.AmmoPerShot, Ammo.Current);
            Assert.AreEqual(Ammo.Current.ToString(), Hud.AmmoReadout, "The HUD did not show the new ammo count.");
        }

        [UnityTest]
        public IEnumerator HoldingFire_ThePistolFiresOnlyOncePerPress()
        {
            Assume.That(Pistol.Automatic, Is.False);

            yield return HoldKey(Mouse.leftButton, Pistol.FireInterval * 3f);
            Assert.AreEqual(1, shotsFired);

            yield return PullTrigger();
            Assert.AreEqual(2, shotsFired, "A new press after the fire interval should fire again.");
        }

        [UnityTest]
        public IEnumerator ClickToRecaptureTheMouse_DoesNotFire()
        {
            // The editor frees the cursor when Escape is pressed, and the next click in the Game view captures it again.
            yield return TapKey(Keyboard.escapeKey);
            Assert.IsFalse(GameManager.IsCursorCaptured, "Escape did not free the mouse.");

            yield return PullTrigger();
            Assert.IsTrue(GameManager.IsCursorCaptured, "Clicking did not capture the mouse again.");
            Assert.AreEqual(0, shotsFired, "The click that captured the mouse also fired.");

            yield return PullTrigger();
            Assert.AreEqual(1, shotsFired, "A click with the mouse captured should fire.");
        }

        [UnityTest]
        public IEnumerator DisablingTheLevelManager_DoesNotSwitchOffFire()
        {
            // The level manager restarts the level with the same Fire input the player shoots with.
            Object.FindFirstObjectByType<LevelManager>().enabled = false;

            yield return PullTrigger();

            Assert.AreEqual(1, shotsFired, "Disabling the level manager switched off the player's Fire input.");
        }

        [UnityTest]
        public IEnumerator Pistol_CannotFireFasterThanItsFireInterval()
        {
            Assert.IsTrue(Weapon.TryFire());
            Assert.IsFalse(Weapon.TryFire(), "Fired twice within one fire interval.");
            Assert.AreEqual(1, shotsFired);

            yield return new WaitForSeconds(Pistol.FireInterval);

            Assert.IsTrue(Weapon.TryFire());
            Assert.AreEqual(2, shotsFired);
        }

        [UnityTest]
        public IEnumerator EmptyPistol_DoesNotFire()
        {
            Ammo.TryConsume(Ammo.Current);

            yield return PullTrigger();

            Assert.AreEqual(0, shotsFired);
            Assert.AreEqual(0, Ammo.Current);
            Assert.AreEqual("0", Hud.AmmoReadout);
        }

        [UnityTest]
        public IEnumerator Firing_ShowsABriefMuzzleFlash()
        {
            var view = Player.GetComponentInChildren<WeaponView>();
            Assert.IsFalse(view.IsMuzzleFlashVisible);

            InputFixture.Press(Mouse.leftButton, queueEventOnly: true);
            yield return null;
            Assert.IsTrue(view.IsMuzzleFlashVisible, "No muzzle flash on the frame the shot was fired.");

            InputFixture.Release(Mouse.leftButton, queueEventOnly: true);
            yield return new WaitForSeconds(0.25f);
            Assert.IsFalse(view.IsMuzzleFlashVisible, "The muzzle flash stayed on.");
        }

        [UnityTest]
        public IEnumerator Shot_DamagesTheTargetInTheCrosshair()
        {
            Health target = CreateTarget(PlayerView.position + PlayerView.forward * TargetDistance);

            yield return PullTrigger();

            Assert.AreEqual(target.Max - Pistol.Damage, target.Current);
            Assert.IsEmpty(Player.GetComponent<BulletImpacts>().ActiveMarks, "A shot that hits a target should leave no bullet mark.");
        }

        [UnityTest]
        public IEnumerator Shot_MissesATargetOutsideTheCrosshair()
        {
            Health target = CreateTarget(PlayerView.position + PlayerView.forward * TargetDistance + PlayerView.right * 1.5f);

            yield return PullTrigger();

            Assert.AreEqual(1, shotsFired);
            Assert.AreEqual(target.Max, target.Current);
        }

        [UnityTest]
        public IEnumerator ClosedDoor_StopsShots_AndTheOpenDoorwayLetsThemThrough()
        {
            const float distanceToDoor = 2f;
            Door door = SpawnRoomDoor();
            StandInFrontOf(door, distanceToDoor);
            yield return null;
            Health target = CreateTarget(PlayerView.position + door.transform.forward * (distanceToDoor + 2f));

            yield return PullTrigger();
            Assert.AreEqual(1, shotsFired);
            Assert.AreEqual(target.Max, target.Current, "The shot went through the closed door.");

            door.Open();
            yield return WaitFor(() => door.State == DoorState.Open, 3f);
            Assert.AreEqual(DoorState.Open, door.State);

            yield return PullTrigger();
            Assert.AreEqual(2, shotsFired);
            Assert.AreEqual(target.Max - Pistol.Damage, target.Current, "The shot did not pass through the open doorway.");
        }

        [UnityTest]
        public IEnumerator Shot_LeavesAMarkOnTheSurface_ThatMovesWithASlidingDoor()
        {
            Door door = SpawnRoomDoor();
            Transform panel = PanelOf(door);
            StandInFrontOf(door, 2f);
            yield return null;

            yield return PullTrigger();

            Transform mark = Player.GetComponent<BulletImpacts>().ActiveMarks.Single();
            float depthFromPanelCentre = Vector3.Dot(mark.position - panel.position, door.transform.forward);
            Assert.AreEqual(-panel.localScale.z * 0.5f, depthFromPanelCentre, 0.02f, "The mark is not on the panel face the player shot.");
            Assert.Less(Vector3.Angle(mark.forward, door.transform.forward), 1f, "The mark should face away from the panel.");

            Vector3 panelStart = panel.position;
            Vector3 markStart = mark.position;
            door.Open();
            yield return WaitFor(() => door.State == DoorState.Open, 3f);

            // The door moves in Update and marks follow in LateUpdate, which has not run yet this frame.
            yield return null;

            Vector3 panelTravel = panel.position - panelStart;
            Assert.Greater(panelTravel.magnitude, 1f);
            Assert.AreEqual(0f, Vector3.Distance(markStart + panelTravel, mark.position), 0.001f, "The mark did not move with the door.");
        }

        private static Health CreateTarget(Vector3 position)
        {
            GameObject target = GameObject.CreatePrimitive(PrimitiveType.Cube);
            target.name = "Test Target";
            target.transform.position = position;

            // Physics sees the new position before the next simulation step only after an explicit sync.
            Physics.SyncTransforms();
            return target.AddComponent<Health>();
        }
    }
}
