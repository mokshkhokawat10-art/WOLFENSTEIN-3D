using System;
using System.Collections.Generic;
using UnityEngine;
using Wolfenstein.World;

namespace Wolfenstein.Progression
{
    /// <summary>
    /// Knows which zone of the level's zone map the player is in, and says when they move into another: the one place
    /// objectives, encounters and the exit learn where the player has got to. A doorway belongs to neither side, so the
    /// player is only in a room once they are through its door. It checks one cell a frame and does nothing more unless the
    /// player has moved to another cell.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ZoneTracker : MonoBehaviour
    {
        private static readonly Vector2Int Nowhere = new Vector2Int(int.MinValue, int.MinValue);

        [SerializeField] private WorldManager world;

        [SerializeField, Tooltip("Whose zone is tracked, normally the player.")]
        private Transform player;

        private readonly HashSet<char> visited = new HashSet<char>();
        private Vector2Int lastCell = Nowhere;

        /// <summary>Raised with the zone's symbol when the player moves into another zone, or '\0' for nowhere in particular.</summary>
        public event Action<char> ZoneEntered;

        /// <summary>The zone the player is in, or '\0' where the zone map names none.</summary>
        public char CurrentZone { get; private set; }

        public bool HasVisited(char zone) => zone != '\0' && visited.Contains(zone);

        private void OnEnable() => world.Built += HandleBuilt;

        private void OnDisable() => world.Built -= HandleBuilt;

        private void Update()
        {
            if (world.Layout == null || player == null)
                return;

            Vector2Int cell = world.WorldToCell(player.position);
            if (cell == lastCell)
                return;

            lastCell = cell;
            if (world.Layout.TryGetDoor(cell, out _) || !world.Layout.IsWalkable(cell))
                return;

            char zone = world.Layout.GetZone(cell);
            if (zone == CurrentZone)
                return;

            CurrentZone = zone;
            if (zone != '\0')
                visited.Add(zone);
            ZoneEntered?.Invoke(zone);
        }

        private void HandleBuilt()
        {
            visited.Clear();
            lastCell = Nowhere;
            CurrentZone = '\0';
        }
    }
}
