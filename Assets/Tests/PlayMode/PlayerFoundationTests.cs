using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem.Controls;
using UnityEngine.TestTools;
using Wolfenstein.Core;

namespace Wolfenstein.Tests
{
    /// <summary>
    /// Phase 1 behaviour: spawning, walking, looking, jumping, gravity and wall collision.
    /// </summary>
    public sealed class PlayerFoundationTests : LevelTestFixture
    {
        [Test]
        public void Level_EntersPlayingStateWithGeometryBuilt()
        {
            Assert.AreEqual(GameState.Playing, GameManager.CurrentState);
            Assert.IsNotNull(World.transform.Find("Geometry"), "The world geometry was not built.");
            Assert.IsTrue(Player.InputEnabled);
        }

        [Test]
        public void Player_StartsAtLayoutSpawnPoint()
        {
            Pose spawn = World.GetSpawnPose();

            Assert.AreEqual(spawn.position.x, Player.transform.position.x, PositionTolerance);
            Assert.AreEqual(spawn.position.z, Player.transform.position.z, PositionTolerance);
            // A CharacterController rests up to one skin width above the surface it stands on.
            Assert.AreEqual(spawn.position.y, Player.transform.position.y, PlayerBody.skinWidth + 0.01f, "Player is not standing on the floor.");
            Assert.AreEqual(0f, Mathf.DeltaAngle(spawn.rotation.eulerAngles.y, Player.Look.Yaw), 0.01f);
            Assert.IsTrue(Player.Movement.IsGrounded);
        }

        [UnityTest]
        public IEnumerator HoldingW_MovesForward() => AssertKeyMovesPlayer(Keyboard.wKey, Vector3.forward);

        [UnityTest]
        public IEnumerator HoldingS_MovesBackward() => AssertKeyMovesPlayer(Keyboard.sKey, Vector3.back);

        [UnityTest]
        public IEnumerator HoldingA_StrafesLeft() => AssertKeyMovesPlayer(Keyboard.aKey, Vector3.left);

        [UnityTest]
        public IEnumerator HoldingD_StrafesRight() => AssertKeyMovesPlayer(Keyboard.dKey, Vector3.right);

        [UnityTest]
        public IEnumerator MouseMovement_TurnsAndTiltsView()
        {
            float startYaw = Player.Look.Yaw;
            float sensitivity = Player.Look.MouseSensitivity;

            yield return MoveMouse(new Vector2(200f, 0f));
            Assert.AreEqual(200f * sensitivity, Mathf.DeltaAngle(startYaw, Player.Look.Yaw), 0.01f, "Horizontal mouse movement should turn the player.");

            yield return MoveMouse(new Vector2(0f, 100f));
            Assert.AreEqual(-100f * sensitivity, Player.Look.Pitch, 0.01f, "Moving the mouse up should tilt the view up.");
        }

        [UnityTest]
        public IEnumerator MouseMovement_PitchIsClamped()
        {
            yield return MoveMouse(new Vector2(0f, 100000f));
            Assert.AreEqual(-Player.Look.MaxPitch, Player.Look.Pitch, 0.001f);

            yield return MoveMouse(new Vector2(0f, -200000f));
            Assert.AreEqual(Player.Look.MaxPitch, Player.Look.Pitch, 0.001f);
        }

        [UnityTest]
        public IEnumerator Jump_ReachesJumpHeightAndLands()
        {
            float floorHeight = Player.transform.position.y;
            float highestPoint = floorHeight;

            InputFixture.Press(Keyboard.spaceKey, queueEventOnly: true);
            yield return null;
            yield return null;
            Assert.IsFalse(Player.Movement.IsGrounded, "Pressing jump should leave the ground.");

            InputFixture.Release(Keyboard.spaceKey, queueEventOnly: true);
            yield return null;

            // A second press in mid-air must not jump again.
            InputFixture.Press(Keyboard.spaceKey, queueEventOnly: true);

            float timeout = Time.time + 2f;
            while (!Player.Movement.IsGrounded && Time.time < timeout)
            {
                highestPoint = Mathf.Max(highestPoint, Player.transform.position.y);
                yield return null;
            }

            InputFixture.Release(Keyboard.spaceKey, queueEventOnly: true);

            Assert.IsTrue(Player.Movement.IsGrounded, "Player never landed.");
            Assert.AreEqual(Player.Movement.JumpHeight, highestPoint - floorHeight, 0.1f);
            Assert.AreEqual(floorHeight, Player.transform.position.y, PositionTolerance);
        }

        [UnityTest]
        public IEnumerator Gravity_BringsAirbornePlayerBackToFloor()
        {
            Vector3 floorPosition = Player.transform.position;
            Player.PlaceAt(new Pose(floorPosition + Vector3.up * 2f, Player.transform.rotation));
            yield return null;

            Assert.IsFalse(Player.Movement.IsGrounded);
            yield return WaitFor(() => Player.Movement.IsGrounded, 2f);

            Assert.IsTrue(Player.Movement.IsGrounded, "Player never landed.");
            Assert.AreEqual(floorPosition.y, Player.transform.position.y, PositionTolerance);
        }

        [UnityTest]
        public IEnumerator Walls_StopThePlayer()
        {
            // The spawn faces north; walking straight ahead leads into the room's north wall.
            Vector2Int wallCell = FindWallAhead(Vector2Int.up);
            float wallFaceZ = World.CellToWorld(wallCell).z - World.TileSize * 0.5f;
            float startZ = Player.transform.position.z;

            yield return HoldKey(Keyboard.wKey, 3f);

            float playerFrontZ = Player.transform.position.z + PlayerBody.radius;
            Assert.Greater(Player.transform.position.z - startZ, World.TileSize * 2f, "Player did not walk up to the wall.");
            Assert.LessOrEqual(playerFrontZ, wallFaceZ + 0.01f, "Player penetrated the wall.");
        }

        [UnityTest]
        public IEnumerator Input_IsIgnoredOutsidePlayingState()
        {
            GameManager.SetState(GameState.Loading);
            Vector3 start = Player.transform.position;

            yield return HoldKey(Keyboard.wKey, 0.3f);

            Assert.IsFalse(Player.InputEnabled);
            Assert.AreEqual(0f, Vector3.Distance(start, Player.transform.position), PositionTolerance);
        }

        private IEnumerator AssertKeyMovesPlayer(ButtonControl key, Vector3 expectedLocalDirection)
        {
            Vector3 start = Player.transform.position;
            Vector3 expectedDirection = Player.transform.TransformDirection(expectedLocalDirection);

            yield return HoldKey(key, 0.4f);

            Vector3 displacement = Player.transform.position - start;
            float alongExpected = Vector3.Dot(displacement, expectedDirection);
            float sideways = Vector3.ProjectOnPlane(displacement, expectedDirection).magnitude;

            Assert.Greater(alongExpected, 1f, $"Holding {key.name} should move the player {expectedLocalDirection}.");
            Assert.Less(sideways, PositionTolerance, "Player drifted off the expected direction.");
        }

        private IEnumerator MoveMouse(Vector2 delta)
        {
            InputFixture.Set(Mouse.delta, delta, queueEventOnly: true);
            yield return null;
            yield return null;
        }

        private Vector2Int FindWallAhead(Vector2Int direction)
        {
            World.Layout.TryFindSpawn(out Vector2Int cell, out _);
            while (World.Layout.IsWalkable(cell))
                cell += direction;
            return cell;
        }
    }
}
