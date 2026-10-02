using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Wolfenstein.World;

namespace Wolfenstein.Tests
{
    /// <summary>
    /// Phase 2 behaviour: sliding doors, used by looking at them and pressing the Interact key.
    /// </summary>
    public sealed class DoorTests : LevelTestFixture
    {
        // Standing this far from a door's panel keeps it within interaction range but out of the doorway.
        private const float NearDistance = 2f;
        private const float SlideTimeout = 3f;

        private static readonly Vector2Int[] NeighbourOffsets =
        {
            Vector2Int.up, Vector2Int.right, Vector2Int.down, Vector2Int.left
        };

        [Test]
        public void Level_HasAClosedDoorOnEveryDoorTile()
        {
            Door[] doors = FindDoors();
            int doorTiles = AllCells().Count(cell => World.Layout.TryGetDoor(cell, out _));

            Assert.GreaterOrEqual(doors.Length, 3);
            Assert.AreEqual(doorTiles, doors.Length, "Every door tile in the layout should have a door.");
            foreach (Door door in doors)
            {
                Assert.AreEqual(DoorState.Closed, door.State, door.name);
                Assert.AreEqual(0f, door.OpenAmount, door.name);
            }
        }

        [Test]
        public void SpawnRoom_CanOnlyBeLeftThroughADoor()
        {
            LevelLayout layout = World.Layout;
            layout.TryFindSpawn(out Vector2Int spawn, out _);

            // Flood fill from the spawn over open floor, stopping at doors.
            var reachable = new HashSet<Vector2Int> { spawn };
            var frontier = new Queue<Vector2Int>(reachable);
            bool bordersDoor = false;
            while (frontier.Count > 0)
            {
                Vector2Int cell = frontier.Dequeue();
                foreach (Vector2Int next in NeighbourOffsets.Select(offset => cell + offset))
                {
                    if (layout.TryGetDoor(next, out _))
                        bordersDoor = true;
                    else if (layout.IsWalkable(next) && reachable.Add(next))
                        frontier.Enqueue(next);
                }
            }

            int openFloorTiles = AllCells().Count(cell => layout.IsWalkable(cell) && !layout.TryGetDoor(cell, out _));
            Assert.IsTrue(bordersDoor, "The spawn area should lead to a door.");
            Assert.Less(reachable.Count, openFloorTiles, "The rest of the level can be reached without opening a door.");
        }

        [UnityTest]
        public IEnumerator ClosedDoor_BlocksThePlayer()
        {
            Door door = SpawnRoomDoor();
            StandInFrontOf(door, NearDistance + 1f);

            yield return HoldKey(Keyboard.wKey, 1.5f);

            float playerFront = DistancePastPanel(door, Player.transform.position) + PlayerBody.radius;
            Assert.AreEqual(DoorState.Closed, door.State);
            Assert.Greater(playerFront, -1f, "Player did not walk up to the door.");
            Assert.LessOrEqual(playerFront, -PanelOf(door).localScale.z * 0.5f + 0.01f, "Player penetrated the closed door.");
        }

        [UnityTest]
        public IEnumerator PressingE_SlidesTheDoorOpen()
        {
            Door door = SpawnRoomDoor();
            Transform panel = PanelOf(door);
            Vector3 closedPosition = panel.localPosition;
            StandInFrontOf(door, NearDistance);

            yield return TapKey(Keyboard.eKey);
            Assert.AreEqual(DoorState.Opening, door.State, "Interact did not start opening the door.");

            yield return WaitFor(() => door.OpenAmount >= 0.5f, SlideTimeout);
            Vector3 slide = panel.localPosition - closedPosition;
            Assert.Greater(slide.x, 0f, "The panel should slide sideways.");
            Assert.AreEqual(0f, slide.y, 0.001f);
            Assert.AreEqual(0f, slide.z, 0.001f);
            Assert.IsTrue(panel.gameObject.activeInHierarchy && panel.GetComponent<Collider>().enabled,
                "The panel should stay solid while it slides, not disappear.");

            yield return WaitFor(() => door.State == DoorState.Open, SlideTimeout);
            Assert.AreEqual(DoorState.Open, door.State);
            Assert.AreEqual(World.TileSize, (panel.localPosition - closedPosition).x, 0.001f, "An open door should clear the whole tile.");
        }

        [UnityTest]
        public IEnumerator OpenDoor_LetsThePlayerWalkThrough()
        {
            Door door = SpawnRoomDoor();
            StandInFrontOf(door, NearDistance);
            yield return OpenWithInteractKey(door);
            Assert.AreEqual(DoorState.Open, door.State);

            yield return HoldKey(Keyboard.wKey, 1f);

            Assert.Greater(DistancePastPanel(door, Player.transform.position), World.TileSize * 0.5f, "Player did not get through the doorway.");
        }

        [UnityTest]
        public IEnumerator OpenDoor_ClosesByItselfAfterOpenDuration()
        {
            Door door = SpawnRoomDoor();
            Transform panel = PanelOf(door);
            Vector3 closedPosition = panel.localPosition;
            StandInFrontOf(door, NearDistance);
            yield return OpenWithInteractKey(door);
            float openedAt = Time.time;

            yield return WaitFor(() => door.State == DoorState.Closing, door.OpenDuration + 1f);
            Assert.AreEqual(DoorState.Closing, door.State);
            Assert.GreaterOrEqual(Time.time - openedAt, door.OpenDuration - 0.1f, "Door started closing before its open duration.");

            yield return WaitFor(() => door.State == DoorState.Closed, SlideTimeout);
            Assert.AreEqual(DoorState.Closed, door.State);
            Assert.AreEqual(0f, Vector3.Distance(closedPosition, panel.localPosition), 0.001f);
        }

        [UnityTest]
        public IEnumerator PressingE_OnAnOpenDoor_ClosesIt()
        {
            Door door = SpawnRoomDoor();
            StandInFrontOf(door, NearDistance);
            yield return OpenWithInteractKey(door);

            yield return TapKey(Keyboard.eKey);
            Assert.AreEqual(DoorState.Closing, door.State);

            yield return WaitFor(() => door.State == DoorState.Closed, SlideTimeout);
            Assert.AreEqual(DoorState.Closed, door.State);
        }

        [UnityTest]
        public IEnumerator PressingE_DoesNothingOutOfRangeOrLookingAway()
        {
            Door door = SpawnRoomDoor();

            StandInFrontOf(door, Player.Interaction.InteractionDistance + 1.5f);
            yield return TapKey(Keyboard.eKey);
            Assert.AreEqual(DoorState.Closed, door.State, "Door opened from out of range.");

            StandInFrontOf(door, NearDistance);
            Player.Look.SetOrientation(door.transform.eulerAngles.y + 180f, 0f);
            yield return TapKey(Keyboard.eKey);
            Assert.AreEqual(DoorState.Closed, door.State, "Door opened while the player looked away from it.");
        }

        [UnityTest]
        public IEnumerator Door_WaitsWhileThePlayerStandsInTheDoorway()
        {
            Door door = SpawnRoomDoor();
            StandInFrontOf(door, NearDistance);
            yield return OpenWithInteractKey(door);

            PlacePlayer(door.transform.position, door.transform.eulerAngles.y);
            yield return WaitFor(() => Player.Movement.IsGrounded, 1f);
            Vector3 standingPosition = Player.transform.position;

            door.Close();
            Assert.AreEqual(DoorState.Open, door.State, "Door started closing on the player.");

            yield return new WaitForSeconds(door.OpenDuration + 1f);
            Assert.AreEqual(DoorState.Open, door.State, "Door closed while the player stood in the doorway.");
            Vector3 displacement = Player.transform.position - standingPosition;
            Assert.AreEqual(0f, HorizontalDistance(displacement), PositionTolerance, $"Player was pushed by {displacement:F3}.");

            StandInFrontOf(door, NearDistance + 1f);
            yield return WaitFor(() => door.State == DoorState.Closed, SlideTimeout);
            Assert.AreEqual(DoorState.Closed, door.State, "Door should close once the doorway is clear.");
        }

        [UnityTest]
        public IEnumerator ClosingDoor_ReopensWhenThePlayerStepsIntoTheDoorway()
        {
            Door door = SpawnRoomDoor();
            Renderer panelRenderer = PanelOf(door).GetComponent<Renderer>();
            StandInFrontOf(door, NearDistance);
            yield return OpenWithInteractKey(door);

            yield return TapKey(Keyboard.eKey);
            yield return WaitFor(() => door.OpenAmount < 0.7f, SlideTimeout);
            Assert.AreEqual(DoorState.Closing, door.State);

            // At 70% open the gap is still wide enough to stand in without touching the panel.
            PlacePlayer(door.transform.position, door.transform.eulerAngles.y);
            yield return null;
            yield return null;
            Assert.AreEqual(DoorState.Opening, door.State, "Closing door should reopen for the player.");

            float timeout = Time.time + SlideTimeout;
            while (door.State != DoorState.Open && Time.time < timeout)
            {
                Assert.IsFalse(panelRenderer.bounds.Intersects(PlayerBody.bounds), "Door panel overlapped the player.");
                yield return null;
            }

            Assert.AreEqual(DoorState.Open, door.State);
        }

        [UnityTest]
        public IEnumerator Doors_KeepIndependentState()
        {
            Door spawnDoor = SpawnRoomDoor();
            Door crossingDoor = FindDoors().FirstOrDefault(door => Mathf.Abs(Mathf.DeltaAngle(door.transform.eulerAngles.y, 90f)) < 1f);
            Assert.IsNotNull(crossingDoor, "The level should contain an east-west door.");

            StandInFrontOf(spawnDoor, NearDistance);
            yield return OpenWithInteractKey(spawnDoor);
            Assert.AreEqual(DoorState.Open, spawnDoor.State);
            Assert.IsTrue(FindDoors().Where(door => door != spawnDoor).All(door => door.State == DoorState.Closed),
                "Opening one door affected another.");

            StandInFrontOf(crossingDoor, NearDistance);
            yield return OpenWithInteractKey(crossingDoor);
            Assert.AreEqual(DoorState.Open, crossingDoor.State);
            Assert.AreEqual(DoorState.Open, spawnDoor.State);

            yield return TapKey(Keyboard.eKey);
            Assert.AreEqual(DoorState.Closing, crossingDoor.State);
            Assert.AreEqual(DoorState.Open, spawnDoor.State, "Closing one door affected another.");
            Assert.IsTrue(FindDoors().Where(door => door != spawnDoor && door != crossingDoor).All(door => door.State == DoorState.Closed));
        }

        private IEnumerator OpenWithInteractKey(Door door)
        {
            yield return TapKey(Keyboard.eKey);
            yield return WaitFor(() => door.State == DoorState.Open, SlideTimeout);
        }

        // Signed distance along the passage through the door; negative on the side the player approaches from.
        private static float DistancePastPanel(Door door, Vector3 point) =>
            Vector3.Dot(point - door.transform.position, door.transform.forward);

        private IEnumerable<Vector2Int> AllCells()
        {
            for (int y = 0; y < World.Layout.Height; y++)
            {
                for (int x = 0; x < World.Layout.Width; x++)
                    yield return new Vector2Int(x, y);
            }
        }
    }
}
