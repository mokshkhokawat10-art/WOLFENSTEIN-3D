using System;
using System.Collections;
using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Wolfenstein.Combat;
using Wolfenstein.Core;
using Wolfenstein.Player;
using Wolfenstein.UI;
using Wolfenstein.World;
using Object = UnityEngine.Object;

namespace Wolfenstein.Tests
{
    /// <summary>
    /// Base for tests that play the test level. The level runs against simulated keyboard and mouse devices,
    /// so tests cover the full path from input actions to gameplay.
    /// </summary>
    public abstract class LevelTestFixture
    {
        protected const float PositionTolerance = 0.05f;

        // Embedded rather than inherited so the input system is reset before the level loads
        // and restored only after the level has been unloaded.
        protected InputTestFixture InputFixture { get; } = new InputTestFixture();

        /// <summary>The scene the tests play: the test level, unless a fixture plays one of the episode's levels.</summary>
        protected virtual string LevelSceneName => "TestLevel";

        protected Keyboard Keyboard { get; private set; }
        protected Mouse Mouse { get; private set; }
        protected GameManager GameManager { get; private set; }
        protected WorldManager World { get; private set; }
        protected PlayerController Player { get; private set; }
        protected CharacterController PlayerBody { get; private set; }
        protected Health PlayerHealth { get; private set; }
        protected Transform PlayerView { get; private set; }
        protected PlayerHud Hud { get; private set; }

        [UnitySetUp]
        public IEnumerator LoadLevel()
        {
            InputFixture.Setup();
            Keyboard = InputSystem.AddDevice<Keyboard>();
            Mouse = InputSystem.AddDevice<Mouse>();

            yield return SceneManager.LoadSceneAsync(LevelSceneName);

            GameManager = Object.FindFirstObjectByType<GameManager>();
            World = Object.FindFirstObjectByType<WorldManager>();
            Player = Object.FindFirstObjectByType<PlayerController>();
            PlayerBody = Player.GetComponent<CharacterController>();
            PlayerHealth = Player.GetComponent<Health>();
            PlayerView = Player.GetComponentInChildren<Camera>().transform;
            Hud = Object.FindFirstObjectByType<PlayerHud>();

            yield return WaitFor(() => Player.Movement.IsGrounded, 2f);
        }

        [UnityTearDown]
        public IEnumerator UnloadLevel()
        {
            Scene level = SceneManager.GetActiveScene();
            SceneManager.SetActiveScene(SceneManager.CreateScene("Empty"));
            yield return SceneManager.UnloadSceneAsync(level);

            InputFixture.TearDown();
        }

        /// <summary>
        /// Length of a displacement ignoring height. A grounded CharacterController settles anywhere within its
        /// skin width of the floor, so a player standing still can shift vertically by up to that much.
        /// </summary>
        protected static float HorizontalDistance(Vector3 displacement) =>
            Vector3.ProjectOnPlane(displacement, Vector3.up).magnitude;

        protected void PlacePlayer(Vector3 position, float yaw) =>
            Player.PlaceAt(new Pose(position, Quaternion.Euler(0f, yaw, 0f)));

        protected IEnumerator TapKey(ButtonControl key)
        {
            InputFixture.Press(key, queueEventOnly: true);
            yield return null;
            InputFixture.Release(key, queueEventOnly: true);
            yield return null;
        }

        protected IEnumerator PullTrigger() => TapKey(Mouse.leftButton);

        protected IEnumerator HoldKey(ButtonControl key, float seconds)
        {
            InputFixture.Press(key, queueEventOnly: true);
            yield return new WaitForSeconds(seconds);
            InputFixture.Release(key, queueEventOnly: true);
            yield return null;
        }

        protected static Door[] FindDoors() => Object.FindObjectsByType<Door>(FindObjectsSortMode.None);

        /// <summary>
        /// The door that seals the room the player starts in.
        /// </summary>
        protected Door SpawnRoomDoor()
        {
            Vector3 spawn = World.GetSpawnPose().position;
            return FindDoors().OrderBy(door => Vector3.Distance(door.transform.position, spawn)).First();
        }

        /// <summary>
        /// Places the player on the approach side of a door, facing it.
        /// </summary>
        protected void StandInFrontOf(Door door, float distance)
        {
            Transform doorTransform = door.transform;
            PlacePlayer(doorTransform.position - doorTransform.forward * distance, doorTransform.eulerAngles.y);
        }

        // The panel is the door's only solid collider; the doorway trigger sits on the door root.
        protected static Transform PanelOf(Door door) =>
            door.GetComponentsInChildren<Collider>().First(collider => !collider.isTrigger).transform;

        /// <summary>
        /// Waits frame by frame until the condition holds or the timeout passes. The caller asserts the outcome.
        /// </summary>
        protected static IEnumerator WaitFor(Func<bool> condition, float timeoutSeconds)
        {
            float deadline = Time.time + timeoutSeconds;
            while (!condition() && Time.time < deadline)
                yield return null;
        }
    }
}
