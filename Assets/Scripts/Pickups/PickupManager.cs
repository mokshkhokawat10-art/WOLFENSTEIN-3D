using System;
using System.Collections.Generic;
using UnityEngine;
using Wolfenstein.World;

namespace Wolfenstein.Pickups
{
    /// <summary>
    /// Places items in the level: once the world is built it puts the item each item tile of the layout stands for
    /// on the floor in the middle of that tile. It also leaves items where things fall, such as the ammo a dead enemy drops.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PickupManager : MonoBehaviour
    {
        [Serializable]
        private struct ItemTile
        {
            public char symbol;
            public Pickup prefab;
        }

        [SerializeField] private WorldManager world;

        [SerializeField, Tooltip("The item each item symbol of the level layout places.")]
        private ItemTile[] itemTiles = Array.Empty<ItemTile>();

        private readonly List<Pickup> levelItems = new List<Pickup>();

        /// <summary>The items the level layout placed when it was built. Those since collected are null.</summary>
        public IReadOnlyList<Pickup> LevelItems => levelItems;

        private void OnEnable() => world.Built += SpawnLevelItems;

        private void OnDisable() => world.Built -= SpawnLevelItems;

        /// <summary>
        /// Places an item on the floor below a point.
        /// </summary>
        public Pickup Spawn(Pickup prefab, Vector3 position)
        {
            // The floor is level with the world's origin.
            position.y = world.transform.position.y;
            Pickup item = Instantiate(prefab, position, world.transform.rotation, transform);
            item.name = prefab.name;
            return item;
        }

        /// <summary>
        /// Leaves an item where something fell, such as an enemy's drop. An item never lands in a doorway, where the door
        /// would close over it: one that would is moved to the middle of the tile beside the door, on the side it fell on.
        /// </summary>
        public Pickup Drop(Pickup prefab, Vector3 position)
        {
            if (world.TryGetDoorAt(world.WorldToCell(position), out Door door))
            {
                Vector3 passage = door.transform.forward;
                float side = Vector3.Dot(position - door.transform.position, passage) < 0f ? -1f : 1f;
                position = door.transform.position + passage * (side * world.TileSize);
            }

            return Spawn(prefab, position);
        }

        private void SpawnLevelItems()
        {
            foreach (Transform item in transform)
                Destroy(item.gameObject);
            levelItems.Clear();

            LevelLayout layout = world.Layout;
            for (int y = 0; y < layout.Height; y++)
            {
                for (int x = 0; x < layout.Width; x++)
                {
                    var cell = new Vector2Int(x, y);
                    if (!layout.TryGetItem(cell, out char symbol))
                        continue;

                    if (!TryGetPrefab(symbol, out Pickup prefab))
                    {
                        Debug.LogError($"Level layout '{layout.name}' has item tile '{symbol}' at {cell}, but no item is assigned to it.", this);
                        continue;
                    }

                    Pickup item = Spawn(prefab, world.CellToWorld(cell));
                    item.name = $"{prefab.name} ({x}, {y})";
                    levelItems.Add(item);
                }
            }
        }

        private bool TryGetPrefab(char symbol, out Pickup prefab)
        {
            foreach (ItemTile tile in itemTiles)
            {
                if (tile.symbol == symbol && tile.prefab != null)
                {
                    prefab = tile.prefab;
                    return true;
                }
            }

            prefab = null;
            return false;
        }
    }
}
