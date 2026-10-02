using System;
using System.Collections.Generic;
using UnityEngine;

namespace Wolfenstein.World
{
    /// <summary>
    /// Route finding and sound travel across a level's tile grid.
    /// Routes treat every unlocked door as passable, since whoever follows them can open it, and only pass through a door
    /// along its passage. Sound only carries through doors that are not closed: as in the original game,
    /// a shot is heard in every area joined to it by open doors.
    /// For crowds heading to the same place, a cost field gives the way from every cell at once.
    /// </summary>
    public sealed class LevelNavigator
    {
        private const float DiagonalCost = 1.41421356f;

        private static readonly Vector2Int[] StepOffsets =
        {
            Vector2Int.up, Vector2Int.right, Vector2Int.down, Vector2Int.left,
            new Vector2Int(1, 1), new Vector2Int(1, -1), new Vector2Int(-1, -1), new Vector2Int(-1, 1)
        };

        private readonly LevelLayout layout;
        private readonly IReadOnlyDictionary<Vector2Int, Door> doors;
        private readonly int width;
        private readonly int height;

        // Search state, kept between searches so finding a route allocates nothing.
        private readonly float[] costFromStart;
        private readonly int[] cameFrom;
        private readonly bool[] closed;
        private readonly List<int> open = new List<int>();
        private readonly Queue<Vector2Int> frontier = new Queue<Vector2Int>();
        private readonly List<(float cost, int index)> heap = new List<(float cost, int index)>();

        public LevelNavigator(LevelLayout layout, IReadOnlyDictionary<Vector2Int, Door> doors)
        {
            this.layout = layout;
            this.doors = doors;
            width = layout.Width;
            height = layout.Height;

            costFromStart = new float[width * height];
            cameFrom = new int[width * height];
            closed = new bool[width * height];
        }

        /// <summary>
        /// Finds the shortest route between two cells, moving diagonally only across open floor so no wall corner is cut.
        /// On success the path holds every cell after the start, ending with the goal.
        /// </summary>
        public bool TryFindPath(Vector2Int start, Vector2Int goal, List<Vector2Int> path)
        {
            path.Clear();
            if (!IsInside(start) || !IsInside(goal) || !layout.IsWalkable(goal))
                return false;

            Array.Fill(costFromStart, float.PositiveInfinity);
            Array.Fill(closed, false);
            open.Clear();

            int startIndex = ToIndex(start);
            int goalIndex = ToIndex(goal);
            costFromStart[startIndex] = 0f;
            cameFrom[startIndex] = -1;
            open.Add(startIndex);

            while (open.Count > 0)
            {
                int current = PopCheapest(goal);
                if (current == goalIndex)
                {
                    BuildPath(goalIndex, path);
                    return true;
                }

                closed[current] = true;
                Vector2Int cell = ToCell(current);

                foreach (Vector2Int offset in StepOffsets)
                {
                    Vector2Int next = cell + offset;
                    if (!IsInside(next) || closed[ToIndex(next)] || !CanStep(cell, offset))
                        continue;

                    int nextIndex = ToIndex(next);
                    float cost = costFromStart[current] + StepCost(offset);
                    if (cost >= costFromStart[nextIndex])
                        continue;

                    if (float.IsPositiveInfinity(costFromStart[nextIndex]))
                        open.Add(nextIndex);

                    costFromStart[nextIndex] = cost;
                    cameFrom[nextIndex] = current;
                }
            }

            return false;
        }

        /// <summary>The number of cells in the grid, which is the size a cost field needs.</summary>
        public int CellCount => width * height;

        /// <summary>
        /// Fills a cost field with the cost of the shortest route from every cell to the goal, taking the same steps as
        /// routes do. Anything can then find its way by stepping to the neighbour with the lowest cost, so one search serves
        /// a whole crowd heading for the same goal. Cells with no route to the goal cost infinity.
        /// </summary>
        public void FindCostsTo(Vector2Int goal, float[] costs)
        {
            Array.Fill(costs, float.PositiveInfinity);
            if (!IsInside(goal) || !layout.IsWalkable(goal))
                return;

            heap.Clear();
            costs[ToIndex(goal)] = 0f;
            Push(0f, ToIndex(goal));

            while (heap.Count > 0)
            {
                (float cost, int index) = Pop();
                if (cost > costs[index])
                    continue;

                Vector2Int cell = ToCell(index);
                foreach (Vector2Int offset in StepOffsets)
                {
                    // Searching outward from the goal, so each step is checked in the direction it would be walked.
                    Vector2Int previous = cell - offset;
                    if (!IsInside(previous) || !CanStep(previous, offset))
                        continue;

                    float previousCost = cost + StepCost(offset);
                    int previousIndex = ToIndex(previous);
                    if (previousCost >= costs[previousIndex])
                        continue;

                    costs[previousIndex] = previousCost;
                    Push(previousCost, previousIndex);
                }
            }
        }

        /// <summary>
        /// The neighbouring cell that a cost field leads to from a cell. False at the goal itself and where there is no route.
        /// </summary>
        public bool TryGetNextStep(Vector2Int from, float[] costs, out Vector2Int next)
        {
            next = from;
            if (!IsInside(from) || costs[ToIndex(from)] <= 0f || float.IsPositiveInfinity(costs[ToIndex(from)]))
                return false;

            float best = costs[ToIndex(from)];
            foreach (Vector2Int offset in StepOffsets)
            {
                Vector2Int candidate = from + offset;
                if (!IsInside(candidate) || !CanStep(from, offset))
                    continue;

                float cost = costs[ToIndex(candidate)] + StepCost(offset);
                if (cost < best - 0.0001f)
                {
                    best = cost;
                    next = candidate;
                }
            }

            return next != from;
        }

        /// <summary>
        /// Collects every cell a sound made at the source can be heard in: all walkable cells joined to it
        /// without passing through a closed door.
        /// </summary>
        public void FindAudibleCells(Vector2Int source, HashSet<Vector2Int> audible)
        {
            audible.Clear();
            audible.Add(source);
            frontier.Clear();
            frontier.Enqueue(source);

            while (frontier.Count > 0)
            {
                Vector2Int cell = frontier.Dequeue();
                for (int i = 0; i < 4; i++)
                {
                    Vector2Int next = cell + StepOffsets[i];
                    if (layout.IsWalkable(next) && !IsClosedDoor(next) && audible.Add(next))
                        frontier.Enqueue(next);
                }
            }
        }

        private bool CanStep(Vector2Int from, Vector2Int offset)
        {
            Vector2Int to = from + offset;
            if (!layout.IsWalkable(to) || IsLockedDoor(to))
                return false;

            if (offset.x == 0 || offset.y == 0)
                return IsAlongPassage(from, offset) && IsAlongPassage(to, offset);

            // A diagonal step needs open floor on all four cells it touches, so it clips no corner or door frame.
            return IsOpenFloor(from) && IsOpenFloor(to) &&
                   IsOpenFloor(from + new Vector2Int(offset.x, 0)) &&
                   IsOpenFloor(from + new Vector2Int(0, offset.y));
        }

        // A door is only passed through along its passage, never entered or left through the frame beside the panel.
        private bool IsAlongPassage(Vector2Int cell, Vector2Int offset)
        {
            if (!layout.TryGetDoor(cell, out float yaw))
                return true;

            bool passageRunsNorthSouth = Mathf.Approximately(Mathf.Repeat(yaw, 180f), 0f);
            return passageRunsNorthSouth ? offset.x == 0 : offset.y == 0;
        }

        private bool IsOpenFloor(Vector2Int cell) => layout.IsWalkable(cell) && !layout.TryGetDoor(cell, out _);

        private bool IsClosedDoor(Vector2Int cell) =>
            doors.TryGetValue(cell, out Door door) && door != null && door.State == DoorState.Closed;

        private bool IsLockedDoor(Vector2Int cell) => doors.TryGetValue(cell, out Door door) && door != null && door.IsLocked;

        private static float StepCost(Vector2Int offset) => offset.x != 0 && offset.y != 0 ? DiagonalCost : 1f;

        // A binary heap of cells by cost, for the cost field search.
        private void Push(float cost, int index)
        {
            heap.Add((cost, index));
            int child = heap.Count - 1;
            while (child > 0)
            {
                int parent = (child - 1) / 2;
                if (heap[parent].cost <= heap[child].cost)
                    break;

                (heap[parent], heap[child]) = (heap[child], heap[parent]);
                child = parent;
            }
        }

        private (float cost, int index) Pop()
        {
            (float cost, int index) top = heap[0];
            int last = heap.Count - 1;
            heap[0] = heap[last];
            heap.RemoveAt(last);

            int parent = 0;
            while (true)
            {
                int smallest = parent;
                int left = parent * 2 + 1;
                int right = left + 1;
                if (left < heap.Count && heap[left].cost < heap[smallest].cost)
                    smallest = left;
                if (right < heap.Count && heap[right].cost < heap[smallest].cost)
                    smallest = right;
                if (smallest == parent)
                    break;

                (heap[parent], heap[smallest]) = (heap[smallest], heap[parent]);
                parent = smallest;
            }

            return top;
        }

        // Removes and returns the open cell with the lowest estimated total cost. The grid is small enough for a linear scan.
        private int PopCheapest(Vector2Int goal)
        {
            int bestSlot = 0;
            float bestScore = float.PositiveInfinity;
            for (int slot = 0; slot < open.Count; slot++)
            {
                int index = open[slot];
                float score = costFromStart[index] + EstimateCost(ToCell(index), goal);
                if (score < bestScore)
                {
                    bestScore = score;
                    bestSlot = slot;
                }
            }

            int cheapest = open[bestSlot];
            open[bestSlot] = open[open.Count - 1];
            open.RemoveAt(open.Count - 1);
            return cheapest;
        }

        // The exact cost of the shortest route on an open grid with diagonal steps, so it never overestimates.
        private static float EstimateCost(Vector2Int from, Vector2Int to)
        {
            int dx = Mathf.Abs(to.x - from.x);
            int dy = Mathf.Abs(to.y - from.y);
            return Mathf.Max(dx, dy) + (DiagonalCost - 1f) * Mathf.Min(dx, dy);
        }

        private void BuildPath(int goalIndex, List<Vector2Int> path)
        {
            for (int index = goalIndex; cameFrom[index] >= 0; index = cameFrom[index])
                path.Add(ToCell(index));

            path.Reverse();
        }

        private bool IsInside(Vector2Int cell) => cell.x >= 0 && cell.x < width && cell.y >= 0 && cell.y < height;

        private int ToIndex(Vector2Int cell) => cell.y * width + cell.x;

        private Vector2Int ToCell(int index) => new Vector2Int(index % width, index / width);
    }
}
