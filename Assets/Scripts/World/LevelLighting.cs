using System.Collections.Generic;
using UnityEngine;

namespace Wolfenstein.World
{
    /// <summary>
    /// Hangs ceiling lamps over the built level. The open floor is split into areas at the doors and where one zone meets
    /// another, each area gets a lamp for every few tiles of its floor, and the lamps are then spread out until each hangs
    /// over the middle of the floor it lights. An area whose floor still strays too far from a lamp gets another one.
    /// A zone's theme can choose its own lamp and spacing. Rooms and corridors are lit in pools, with dimmer floor between.
    /// It is purely visual: lamps have no colliders and change nothing about the level.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class LevelLighting : MonoBehaviour
    {
        private static readonly Vector2Int[] StepOffsets =
        {
            Vector2Int.up, Vector2Int.right, Vector2Int.down, Vector2Int.left
        };

        [SerializeField] private WorldManager world;

        [SerializeField, Tooltip("Placed on the ceiling over each lamp tile, unless the zone's theme has its own lamp. " +
                                 "Its origin is where it meets the ceiling.")]
        private GameObject lampPrefab;

        [SerializeField, Min(1f), Tooltip("Tiles of floor each lamp lights. Fewer tiles give more lamps.")]
        private float tilesPerLamp = 10f;

        [SerializeField, Min(1), Tooltip("No floor tile is more than this many steps from a lamp, so long corridors get lamps too.")]
        private int maxStepsToLamp = 3;

        [SerializeField, Min(0), Tooltip("Rounds of moving each lamp to the middle of the floor it lights.")]
        private int spreadingRounds = 20;

        private readonly List<Vector2Int> lampCells = new List<Vector2Int>();

        /// <summary>The tiles lamps hang over, once the level is built.</summary>
        public IReadOnlyList<Vector2Int> LampCells => lampCells;

        private void OnEnable() => world.Built += HangLamps;

        private void OnDisable() => world.Built -= HangLamps;

        private void HangLamps()
        {
            foreach (Transform lamp in transform)
                Destroy(lamp.gameObject);

            lampCells.Clear();
            Vector3 ceiling = world.transform.up * world.WallHeight;
            foreach (List<Vector2Int> area in FindAreas())
            {
                ZoneTheme theme = world.GetZoneTheme(area[0]);
                GameObject prefab = theme != null && theme.LampPrefab != null ? theme.LampPrefab : lampPrefab;
                float areaTilesPerLamp = theme != null ? theme.TilesPerLamp : tilesPerLamp;
                int areaMaxSteps = theme != null ? theme.MaxStepsToLamp : maxStepsToLamp;

                int lampCount = Mathf.Max(1, Mathf.RoundToInt(area.Count / areaTilesPerLamp));
                List<Vector2Int> lamps = PlaceLamps(area, lampCount);
                while (FurthestFromLamps(area, lamps) > areaMaxSteps)
                    lamps = PlaceLamps(area, ++lampCount);

                foreach (Vector2Int cell in lamps)
                {
                    GameObject lamp = Instantiate(prefab, world.CellToWorld(cell) + ceiling, world.transform.rotation, transform);
                    lamp.name = $"Lamp ({cell.x}, {cell.y})";
                }

                lampCells.AddRange(lamps);
            }
        }

        // Open floor of one zone joined up without passing through a door. Areas and their cells come out in a fixed order.
        private List<List<Vector2Int>> FindAreas()
        {
            LevelLayout layout = world.Layout;
            var areas = new List<List<Vector2Int>>();
            var assigned = new HashSet<Vector2Int>();
            var frontier = new Queue<Vector2Int>();

            for (int y = 0; y < layout.Height; y++)
            {
                for (int x = 0; x < layout.Width; x++)
                {
                    var start = new Vector2Int(x, y);
                    if (!IsOpenFloor(start) || !assigned.Add(start))
                        continue;

                    var area = new List<Vector2Int>();
                    frontier.Enqueue(start);
                    while (frontier.Count > 0)
                    {
                        Vector2Int cell = frontier.Dequeue();
                        area.Add(cell);
                        foreach (Vector2Int offset in StepOffsets)
                        {
                            Vector2Int next = cell + offset;
                            if (IsOpenFloor(next) && layout.GetZone(next) == layout.GetZone(cell) && assigned.Add(next))
                                frontier.Enqueue(next);
                        }
                    }

                    areas.Add(area);
                }
            }

            return areas;
        }

        // Starts from the middle of the area and the points furthest from it, then repeatedly moves each lamp to the middle
        // of the floor nearer to it than to any other lamp, measured by walking so walls keep areas apart.
        private List<Vector2Int> PlaceLamps(List<Vector2Int> area, int lampCount)
        {
            var lamps = new List<Vector2Int> { NearestTo(area, Centre(area)) };
            var distances = new List<Dictionary<Vector2Int, int>> { WalkingDistances(lamps[0], area) };

            while (lamps.Count < lampCount)
            {
                Vector2Int furthest = area[0];
                int furthestDistance = -1;
                foreach (Vector2Int cell in area)
                {
                    int distance = NearestLampDistance(cell, distances, out _);
                    if (distance > furthestDistance)
                    {
                        furthest = cell;
                        furthestDistance = distance;
                    }
                }

                lamps.Add(furthest);
                distances.Add(WalkingDistances(furthest, area));
            }

            var shares = new List<Vector2Int>[lampCount];
            for (int round = 0; round < spreadingRounds; round++)
            {
                for (int i = 0; i < lampCount; i++)
                    shares[i] = new List<Vector2Int>();

                foreach (Vector2Int cell in area)
                {
                    NearestLampDistance(cell, distances, out int nearest);
                    shares[nearest].Add(cell);
                }

                bool moved = false;
                for (int i = 0; i < lampCount; i++)
                {
                    Vector2Int middle = NearestTo(shares[i], Centre(shares[i]));
                    if (middle == lamps[i])
                        continue;

                    lamps[i] = middle;
                    distances[i] = WalkingDistances(middle, area);
                    moved = true;
                }

                if (!moved)
                    break;
            }

            return lamps;
        }

        // How many steps the floor furthest from every lamp is from the nearest one.
        private static int FurthestFromLamps(List<Vector2Int> area, List<Vector2Int> lamps)
        {
            var distances = new List<Dictionary<Vector2Int, int>>();
            foreach (Vector2Int lamp in lamps)
                distances.Add(WalkingDistances(lamp, area));

            int furthest = 0;
            foreach (Vector2Int cell in area)
                furthest = Mathf.Max(furthest, NearestLampDistance(cell, distances, out _));

            return furthest;
        }

        private static int NearestLampDistance(Vector2Int cell, List<Dictionary<Vector2Int, int>> distances, out int nearest)
        {
            nearest = 0;
            int best = int.MaxValue;
            for (int i = 0; i < distances.Count; i++)
            {
                if (distances[i].TryGetValue(cell, out int distance) && distance < best)
                {
                    best = distance;
                    nearest = i;
                }
            }

            return best;
        }

        private static Dictionary<Vector2Int, int> WalkingDistances(Vector2Int from, List<Vector2Int> area)
        {
            var inArea = new HashSet<Vector2Int>(area);
            var distances = new Dictionary<Vector2Int, int> { [from] = 0 };
            var frontier = new Queue<Vector2Int>();
            frontier.Enqueue(from);

            while (frontier.Count > 0)
            {
                Vector2Int cell = frontier.Dequeue();
                foreach (Vector2Int offset in StepOffsets)
                {
                    Vector2Int next = cell + offset;
                    if (inArea.Contains(next) && !distances.ContainsKey(next))
                    {
                        distances[next] = distances[cell] + 1;
                        frontier.Enqueue(next);
                    }
                }
            }

            return distances;
        }

        private static Vector2 Centre(List<Vector2Int> cells)
        {
            Vector2 sum = Vector2.zero;
            foreach (Vector2Int cell in cells)
                sum += cell;
            return sum / cells.Count;
        }

        // The first of the closest cells, so the result does not depend on floating-point ties.
        private static Vector2Int NearestTo(List<Vector2Int> cells, Vector2 point)
        {
            Vector2Int nearest = cells[0];
            float nearestDistance = float.PositiveInfinity;
            foreach (Vector2Int cell in cells)
            {
                float distance = (cell - point).sqrMagnitude;
                if (distance < nearestDistance - 0.0001f)
                {
                    nearest = cell;
                    nearestDistance = distance;
                }
            }

            return nearest;
        }

        private bool IsOpenFloor(Vector2Int cell) => world.Layout.IsWalkable(cell) && !world.Layout.TryGetDoor(cell, out _);
    }
}
