using System;
using UnityEngine;

namespace Wolfenstein.World
{
    /// <summary>
    /// A level's tile map, written as text like the grid-based maps of the original game.
    /// The first line is the northern edge; each character is one tile.
    /// '.' is floor, '^' '&gt;' 'v' '&lt;' mark the player spawn facing north, east, south or west,
    /// 'N' 'E' 'S' 'W' place a guard facing north, east, south or west on the floor of that tile,
    /// any other lower-case letter puts an item on the floor of that tile (the PickupManager sets which item each letter is),
    /// '-' is a door passed through north-south and '|' a door passed through east-west,
    /// and every other character is a solid wall.
    /// Two optional maps of the same shape lie over it: the zone map names the zone each tile belongs to, such as the room
    /// whose look it takes, and the decor map places decorations and markers. In both, '.' and ' ' mean nothing.
    /// </summary>
    [CreateAssetMenu(fileName = "LevelLayout", menuName = "Wolfenstein/Level Layout")]
    public sealed class LevelLayout : ScriptableObject
    {
        public const char FloorTile = '.';
        private const char EmptyTile = '\0';
        private const string SpawnTiles = "^>v<";
        private const string GuardTiles = "NESW";
        private const string DoorTiles = "-|";

        [SerializeField, TextArea(12, 40)]
        private string map = string.Empty;

        [SerializeField, TextArea(12, 40), Tooltip("Optional. The zone each tile belongs to, one character per tile.")]
        private string zones = string.Empty;

        [SerializeField, TextArea(12, 40), Tooltip("Optional. Decorations and markers, one character per tile.")]
        private string decor = string.Empty;

        private string[] rows;
        private string[] zoneRows;
        private string[] decorRows;
        private int width;

        public int Width
        {
            get
            {
                EnsureParsed();
                return width;
            }
        }

        public int Height
        {
            get
            {
                EnsureParsed();
                return rows.Length;
            }
        }

        /// <summary>
        /// Returns the tile at a cell, where x runs east and y runs north from the bottom-left corner.
        /// Cells outside the map return '\0', which counts as solid.
        /// </summary>
        public char GetTile(Vector2Int cell)
        {
            EnsureParsed();
            return GetCharacter(rows, cell);
        }

        /// <summary>
        /// The zone a cell belongs to, or '\0' where the zone map names none.
        /// </summary>
        public char GetZone(Vector2Int cell)
        {
            EnsureParsed();
            return Meaningful(GetCharacter(zoneRows, cell));
        }

        /// <summary>
        /// The decoration or marker placed on a cell, or '\0' where the decor map places none.
        /// </summary>
        public char GetDecor(Vector2Int cell)
        {
            EnsureParsed();
            return Meaningful(GetCharacter(decorRows, cell));
        }

        /// <summary>
        /// True for tiles that are not solid wall. Door tiles count as walkable; while closed,
        /// the door blocks movement with its own collider.
        /// </summary>
        public bool IsWalkable(Vector2Int cell)
        {
            char tile = GetTile(cell);
            return tile == FloorTile || SpawnTiles.IndexOf(tile) >= 0 || GuardTiles.IndexOf(tile) >= 0 ||
                   IsItemTile(tile) || DoorTiles.IndexOf(tile) >= 0;
        }

        /// <summary>
        /// Finds a door at a cell. The yaw orients the door so its panel spans the tile along local X
        /// and the passage through it runs along local Z.
        /// </summary>
        public bool TryGetDoor(Vector2Int cell, out float yaw)
        {
            int orientation = DoorTiles.IndexOf(GetTile(cell));
            if (orientation < 0)
            {
                yaw = 0f;
                return false;
            }

            yaw = orientation * 90f;
            return true;
        }

        /// <summary>
        /// Finds a guard placed at a cell, and the yaw it starts out facing.
        /// </summary>
        public bool TryGetGuardSpawn(Vector2Int cell, out float yaw)
        {
            int direction = GuardTiles.IndexOf(GetTile(cell));
            yaw = Mathf.Max(direction, 0) * 90f;
            return direction >= 0;
        }

        /// <summary>
        /// Finds an item placed at a cell, and the letter that stands for it.
        /// </summary>
        public bool TryGetItem(Vector2Int cell, out char symbol)
        {
            char tile = GetTile(cell);
            bool isItem = IsItemTile(tile);
            symbol = isItem ? tile : EmptyTile;
            return isItem;
        }

        public bool TryFindSpawn(out Vector2Int cell, out float yaw)
        {
            for (int y = 0; y < Height; y++)
            {
                for (int x = 0; x < Width; x++)
                {
                    int direction = SpawnTiles.IndexOf(GetTile(new Vector2Int(x, y)));
                    if (direction < 0)
                        continue;

                    cell = new Vector2Int(x, y);
                    yaw = direction * 90f;
                    return true;
                }
            }

            cell = default;
            yaw = 0f;
            return false;
        }

        // Lower-case letters, apart from the 'v' of a spawn facing south.
        private static bool IsItemTile(char tile) => tile >= 'a' && tile <= 'z' && SpawnTiles.IndexOf(tile) < 0;

        // Lines are counted from the top of the text, the northern edge, while y counts north from the bottom.
        // The overlay maps line up with the tile map along its northern edge.
        private char GetCharacter(string[] lines, Vector2Int cell)
        {
            if (cell.y < 0 || cell.y >= rows.Length)
                return EmptyTile;

            int line = rows.Length - 1 - cell.y;
            if (line >= lines.Length)
                return EmptyTile;

            string row = lines[line];
            return cell.x >= 0 && cell.x < row.Length ? row[cell.x] : EmptyTile;
        }

        private static char Meaningful(char symbol) => symbol is '.' or ' ' ? EmptyTile : symbol;

        private static string[] SplitLines(string text) =>
            (text ?? string.Empty).Replace("\r", string.Empty).Split('\n', StringSplitOptions.RemoveEmptyEntries);

        private void EnsureParsed()
        {
            if (rows != null)
                return;

            rows = SplitLines(map);
            zoneRows = SplitLines(zones);
            decorRows = SplitLines(decor);
            width = 0;
            foreach (string row in rows)
                width = Mathf.Max(width, row.Length);
        }

        private void OnValidate() => rows = null;
    }
}
