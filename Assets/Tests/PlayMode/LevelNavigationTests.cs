using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using Wolfenstein.World;

namespace Wolfenstein.Tests
{
    /// <summary>
    /// Phase 4 behaviour: routes across the tile grid for enemies to follow, and how far the sound of a shot carries.
    /// </summary>
    public sealed class LevelNavigationTests : LevelTestFixture
    {
        [Test]
        public void EveryGuard_HasARouteToThePlayerSpawn_ThroughDoorsAndAroundCorners()
        {
            LevelLayout layout = World.Layout;
            layout.TryFindSpawn(out Vector2Int spawn, out _);
            var route = new List<Vector2Int>();
            List<Vector2Int> guardTiles = AllCells().Where(cell => layout.TryGetGuardSpawn(cell, out _)).ToList();
            Assert.IsNotEmpty(guardTiles, "The level has no guards.");

            foreach (Vector2Int start in guardTiles)
            {
                Assert.IsTrue(World.Navigator.TryFindPath(start, spawn, route), $"No route from the guard at {start}.");
                Assert.AreEqual(spawn, route.Last());
                Assert.IsTrue(route.Any(cell => layout.TryGetDoor(cell, out _)), "The spawn room can only be reached through a door.");

                Vector2Int previous = start;
                foreach (Vector2Int cell in route)
                {
                    AssertValidStep(layout, previous, cell);
                    previous = cell;
                }
            }
        }

        [Test]
        public void NoRoute_LeadsIntoAWall()
        {
            World.Layout.TryFindSpawn(out Vector2Int spawn, out _);
            Vector2Int wall = AllCells().First(cell => !World.Layout.IsWalkable(cell));
            var route = new List<Vector2Int>();

            Assert.IsFalse(World.Navigator.TryFindPath(spawn, wall, route));
            Assert.IsEmpty(route);
        }

        [Test]
        public void Sound_CarriesThroughAnOpenDoor_ButNotAClosedOne()
        {
            World.Layout.TryFindSpawn(out Vector2Int spawn, out _);
            Door door = SpawnRoomDoor();
            Vector2Int doorCell = World.WorldToCell(door.transform.position);
            var audible = new HashSet<Vector2Int>();

            World.Navigator.FindAudibleCells(spawn, audible);
            Assert.IsFalse(audible.Contains(doorCell), "Sound passed through the closed door.");
            int closedRoomSize = audible.Count;

            door.Open();
            World.Navigator.FindAudibleCells(spawn, audible);
            Assert.IsTrue(audible.Contains(doorCell), "Sound did not pass through the opening door.");
            Assert.Greater(audible.Count, closedRoomSize, "Sound did not carry beyond the open door.");

            foreach (Door other in FindDoors().Where(other => other != door))
                Assert.IsFalse(audible.Contains(World.WorldToCell(other.transform.position)), $"Sound passed through {other.name}, which is closed.");
        }

        private static void AssertValidStep(LevelLayout layout, Vector2Int from, Vector2Int to)
        {
            Vector2Int step = to - from;
            string description = $"Step from {from} to {to}";
            Assert.AreEqual(1, Mathf.Max(Mathf.Abs(step.x), Mathf.Abs(step.y)), $"{description} is not to a neighbouring cell.");
            Assert.IsTrue(layout.IsWalkable(to), $"{description} walks into a wall.");

            if (step.x != 0 && step.y != 0)
            {
                // A diagonal step must not clip the corner of a wall or door frame.
                foreach (Vector2Int cell in new[] { from, to, from + new Vector2Int(step.x, 0), from + new Vector2Int(0, step.y) })
                    Assert.IsTrue(layout.IsWalkable(cell) && !layout.TryGetDoor(cell, out _), $"{description} cuts a corner at {cell}.");
                return;
            }

            // Doors are only passed through along their passage: '-' north-south, '|' east-west.
            foreach (Vector2Int cell in new[] { from, to })
            {
                if (!layout.TryGetDoor(cell, out float yaw))
                    continue;

                int sideways = Mathf.Approximately(yaw, 0f) ? step.x : step.y;
                Assert.AreEqual(0, sideways, $"{description} enters or leaves the door at {cell} from the side.");
            }
        }

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
