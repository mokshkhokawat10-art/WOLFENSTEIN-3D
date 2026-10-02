using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Wolfenstein.Combat;
using Wolfenstein.Core;
using Wolfenstein.Player;
using Wolfenstein.UI;
using Wolfenstein.World;

namespace Wolfenstein.Tests
{
    /// <summary>
    /// Phase 3 behaviour: the player taking damage, the HUD reacting, death ending the game and restarting the level.
    /// </summary>
    public sealed class PlayerDeathTests : LevelTestFixture
    {
        [UnityTest]
        public IEnumerator Damage_LowersHealthOnTheHudAndFlashesTheScreen()
        {
            PlayerHealth.TakeDamage(Damage(Mathf.RoundToInt(PlayerHealth.Max * 0.3f)));

            Assert.AreEqual("70%", Hud.HealthReadout);
            Assert.Greater(Hud.DamageFlashAlpha, 0f, "Taking damage should flash the screen.");

            yield return new WaitForSeconds(1f);
            Assert.AreEqual(0f, Hud.DamageFlashAlpha, "The damage flash should fade out.");
        }

        [UnityTest]
        public IEnumerator LethalDamage_EndsTheGameAndBlocksPlayerInput()
        {
            Vector3 start = Player.transform.position;
            int ammo = Player.Weapon.Ammo.Current;

            PlayerHealth.TakeDamage(Damage(PlayerHealth.Max * 2));

            Assert.IsTrue(PlayerHealth.IsDead);
            Assert.AreEqual(GameState.GameOver, GameManager.CurrentState);
            Assert.IsFalse(Player.InputEnabled);
            Assert.IsTrue(Hud.IsDeathScreenVisible);
            Assert.IsFalse(Hud.IsCrosshairVisible, "The crosshair should be hidden on the death screen.");
            Assert.AreEqual("0%", Hud.HealthReadout);
            Assert.IsFalse(Hud.IsRestartPromptVisible, "The restart prompt showed before Fire can restart.");

            // Both inputs arrive inside the restart delay, so neither may restart the level.
            yield return HoldKey(Keyboard.wKey, 0.3f);
            yield return PullTrigger();

            Assert.IsTrue(GameManager != null, "The level restarted before the restart delay.");
            Assert.IsFalse(Hud.IsRestartPromptVisible, "The restart prompt showed before Fire can restart.");
            Assert.AreEqual(GameState.GameOver, GameManager.CurrentState);
            Assert.AreEqual(0f, HorizontalDistance(Player.transform.position - start), PositionTolerance, "A dead player moved.");
            Assert.AreEqual(ammo, Player.Weapon.Ammo.Current, "A dead player fired.");
        }

        [UnityTest]
        public IEnumerator PressingFire_OnTheDeathScreen_RestartsTheLevel()
        {
            var levelManager = Object.FindFirstObjectByType<LevelManager>();
            GameManager oldGameManager = GameManager;
            PlayerHealth.TakeDamage(Damage(PlayerHealth.Max));
            yield return new WaitForSeconds(levelManager.RestartDelay + 0.1f);
            Assert.IsTrue(Hud.IsRestartPromptVisible, "The restart prompt did not appear once Fire could restart.");

            yield return PullTrigger();
            yield return WaitFor(() => oldGameManager == null, 2f);
            Assert.IsTrue(oldGameManager == null, "Pressing fire on the death screen did not restart the level.");

            GameManager newGameManager = null;
            yield return WaitFor(() =>
            {
                newGameManager = Object.FindFirstObjectByType<GameManager>();
                return newGameManager != null && newGameManager.CurrentState == GameState.Playing;
            }, 3f);
            Assert.AreEqual(GameState.Playing, newGameManager.CurrentState);

            var newPlayer = Object.FindFirstObjectByType<PlayerController>();
            var newHealth = newPlayer.GetComponent<Health>();
            Assert.AreEqual(newHealth.Max, newHealth.Current);
            Assert.IsFalse(Object.FindFirstObjectByType<PlayerHud>().IsDeathScreenVisible);

            // The restarted player must be controllable again, including Fire, which the level manager shares.
            yield return WaitFor(() => newPlayer.Movement.IsGrounded, 2f);
            Vector3 start = newPlayer.transform.position;
            yield return HoldKey(Keyboard.wKey, 0.3f);
            Assert.Greater(Vector3.Distance(start, newPlayer.transform.position), 0.5f, "The player cannot move after a restart.");

            int ammo = newPlayer.Weapon.Ammo.Current;
            yield return PullTrigger();
            Assert.AreEqual(ammo - 1, newPlayer.Weapon.Ammo.Current, "The player cannot fire after a restart.");
        }

        [UnityTest]
        public IEnumerator PressingFire_RestartsTheLevel_EvenWhileThePlayerIsDisabled()
        {
            // As it might be during a pause or a cutscene. The player shoots with the same Fire input that restarts the level.
            var levelManager = Object.FindFirstObjectByType<LevelManager>();
            GameManager oldGameManager = GameManager;
            PlayerHealth.TakeDamage(Damage(PlayerHealth.Max));
            Player.enabled = false;
            yield return new WaitForSeconds(levelManager.RestartDelay + 0.1f);

            yield return PullTrigger();
            yield return WaitFor(() => oldGameManager == null, 2f);
            Assert.IsTrue(oldGameManager == null, "Disabling the player switched off the restart input.");
        }

        private DamageInfo Damage(int amount) => new DamageInfo(amount, Player.transform.position, Vector3.zero, null);
    }
}
