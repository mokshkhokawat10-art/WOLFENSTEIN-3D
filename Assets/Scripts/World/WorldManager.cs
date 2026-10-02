using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Wolfenstein.World
{
    /// <summary>
    /// Builds playable geometry and doors from a <see cref="LevelLayout"/>, converts between tile and world space
    /// and gives access to the doors and navigation of the built level.
    /// Cell (0, 0) starts at this transform's position and the grid extends along +X and +Z.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class WorldManager : MonoBehaviour
    {
        [Serializable]
        private struct WallStyle
        {
            public char symbol;
            public Material material;

            [Tooltip("Placed on the tile instead of a plain block, such as a pillar or a stack of crates. Its origin is the " +
                     "middle of the tile at floor level. It brings its own colliders; the tile still blocks routes like any wall.")]
            public GameObject prefab;
        }

        [Serializable]
        private struct ZoneBinding
        {
            public char zone;
            public ZoneTheme theme;
        }

        private const float SlabThickness = 0.2f;

        private static readonly int BaseMapTilingId = Shader.PropertyToID("_BaseMap_ST");

        private static readonly Vector2Int[] NeighbourOffsets =
        {
            Vector2Int.up, Vector2Int.right, Vector2Int.down, Vector2Int.left
        };

        [SerializeField] private LevelLayout layout;

        [Header("Dimensions")]
        [SerializeField, Min(0.5f)] private float tileSize = 3f;
        [SerializeField, Min(0.5f)] private float wallHeight = 3f;

        [Header("Materials")]
        [SerializeField, Tooltip("Material for each wall symbol. The first entry is also used for any unlisted solid tile.")]
        private WallStyle[] wallStyles = Array.Empty<WallStyle>();

        [SerializeField] private Material floorMaterial;
        [SerializeField] private Material ceilingMaterial;

        [Header("Zones")]
        [SerializeField, Tooltip("The theme each zone of the layout's zone map takes. Zones not listed keep the usual look.")]
        private ZoneBinding[] zoneThemes = Array.Empty<ZoneBinding>();

        [Header("Doors")]
        [SerializeField, Tooltip("Placed on every door tile. Its panel should match the tile size and wall height.")]
        private Door doorPrefab;

        private readonly Dictionary<Vector2Int, Door> doorsByCell = new Dictionary<Vector2Int, Door>();
        private Transform geometryRoot;
        private Transform doorRoot;

        /// <summary>Raised once the level has been built, so its contents can be placed.</summary>
        public event Action Built;

        public LevelLayout Layout => layout;
        public float TileSize => tileSize;
        public float WallHeight => wallHeight;

        /// <summary>Routes and sound travel for the built level. Null until the level is built.</summary>
        public LevelNavigator Navigator { get; private set; }

        public void Build()
        {
            if (geometryRoot != null)
                Destroy(geometryRoot.gameObject);
            if (doorRoot != null)
                Destroy(doorRoot.gameObject);

            geometryRoot = CreateRoot("Geometry");
            BuildFloorAndCeiling();
            BuildWalls();
            StaticBatchingUtility.Combine(geometryRoot.gameObject);

            // Doors move, so they are kept apart from the statically batched geometry.
            doorRoot = CreateRoot("Doors");
            BuildDoors();

            // The new colliders were moved into place after they were created. Physics only sees those moves at the
            // next simulation step, so without a sync, raycasts made before then would pass through the new walls.
            Physics.SyncTransforms();

            Navigator = new LevelNavigator(layout, doorsByCell);
            Built?.Invoke();
        }

        public Pose GetSpawnPose()
        {
            if (!layout.TryFindSpawn(out Vector2Int cell, out float yaw))
            {
                Debug.LogError($"Level layout '{layout.name}' has no player spawn tile.", layout);
                return new Pose(transform.position, transform.rotation);
            }

            return new Pose(CellToWorld(cell), transform.rotation * Quaternion.Euler(0f, yaw, 0f));
        }

        public Vector3 CellToWorld(Vector2Int cell) => transform.TransformPoint(CellToLocal(cell));

        /// <summary>The cell containing a world position.</summary>
        public Vector2Int WorldToCell(Vector3 position)
        {
            Vector3 local = transform.InverseTransformPoint(position);
            return new Vector2Int(Mathf.FloorToInt(local.x / tileSize), Mathf.FloorToInt(local.z / tileSize));
        }

        /// <summary>Finds the door built on a cell.</summary>
        public bool TryGetDoorAt(Vector2Int cell, out Door door) => doorsByCell.TryGetValue(cell, out door);

        /// <summary>Every door of the built level, by the cell it stands on.</summary>
        public IReadOnlyDictionary<Vector2Int, Door> Doors => doorsByCell;

        /// <summary>The theme of the zone a cell belongs to, or null where the zone has none.</summary>
        public ZoneTheme GetZoneTheme(Vector2Int cell)
        {
            char zone = layout.GetZone(cell);
            if (zone == '\0')
                return null;

            foreach (ZoneBinding binding in zoneThemes)
            {
                if (binding.zone == zone)
                    return binding.theme;
            }

            return null;
        }

        /// <summary>
        /// True for a wall tile built as a plain block, as opposed to open floor or a wall built from a prefab.
        /// Cells outside the map count as plain wall.
        /// </summary>
        public bool IsPlainWall(Vector2Int cell) => !layout.IsWalkable(cell) && GetWallStyle(layout.GetTile(cell)).prefab == null;

        private Vector3 CellToLocal(Vector2Int cell) =>
            new Vector3((cell.x + 0.5f) * tileSize, 0f, (cell.y + 0.5f) * tileSize);

        private Transform CreateRoot(string rootName)
        {
            Transform root = new GameObject(rootName).transform;
            root.SetParent(transform, false);
            return root;
        }

        // Every map cell, plus an optional ring of cells around the map.
        private IEnumerable<Vector2Int> EnumerateCells(int border = 0)
        {
            for (int y = -border; y < layout.Height + border; y++)
            {
                for (int x = -border; x < layout.Width + border; x++)
                    yield return new Vector2Int(x, y);
            }
        }

        private void BuildFloorAndCeiling()
        {
            var size = new Vector3(layout.Width * tileSize, SlabThickness, layout.Height * tileSize);
            var centre = new Vector3(size.x * 0.5f, 0f, size.z * 0.5f);

            // The floor and ceiling are single slabs over the whole map, so their textures repeat once per tile, as on the walls.
            var tiling = new Vector4(layout.Width, layout.Height, 0f, 0f);
            MeshRenderer floor = CreateBlock("Floor", centre + Vector3.down * (SlabThickness * 0.5f), size, floorMaterial);
            MeshRenderer ceiling = CreateBlock("Ceiling", centre + Vector3.up * (wallHeight + SlabThickness * 0.5f), size, ceilingMaterial);
            SetTextureTiling(floor, tiling);
            SetTextureTiling(ceiling, tiling);

            // With zone themes, each tile shows its own zone's floor and ceiling. The slabs stay as the solid floor and
            // ceiling, so movement is the same either way; only their own drawing is replaced.
            if (zoneThemes.Length == 0)
                return;

            BuildZoneSurfaces();
            floor.enabled = false;
            ceiling.enabled = false;
        }

        // A square of floor and one of ceiling for every tile that can be seen: open floor, and the tiles of walls built
        // from prefabs, which stand on the floor rather than filling their tile.
        private void BuildZoneSurfaces()
        {
            var surfaces = new MeshBatch(tileSize);
            foreach (Vector2Int cell in EnumerateCells())
            {
                if (IsPlainWall(cell))
                    continue;

                ZoneTheme theme = GetZoneTheme(cell);
                Material floor = theme != null && theme.FloorMaterial != null ? theme.FloorMaterial : floorMaterial;
                Material ceiling = theme != null && theme.CeilingMaterial != null ? theme.CeilingMaterial : ceilingMaterial;
                Vector3 middle = CellToLocal(cell);
                surfaces.AddQuad(floor, middle, Vector3.up, Vector3.right, tileSize, tileSize);
                surfaces.AddQuad(ceiling, middle + Vector3.up * wallHeight, Vector3.down, Vector3.right, tileSize, tileSize);
            }

            surfaces.Build("Zone Surfaces", geometryRoot);
        }

        private static void SetTextureTiling(Renderer slab, Vector4 tiling)
        {
            var properties = new MaterialPropertyBlock();
            properties.SetVector(BaseMapTilingId, tiling);
            slab.SetPropertyBlock(properties);
        }

        // Each plain wall is a solid box, drawn only where a face can be seen. A face takes the wall material of the zone it
        // looks into, so the two sides of a wall between different rooms each match their own room.
        private void BuildWalls()
        {
            var wallSize = new Vector3(tileSize, wallHeight, tileSize);
            var halfHeight = Vector3.up * (wallHeight * 0.5f);
            var faces = new MeshBatch(tileSize);

            // The ring outside the map means a floor tile on the map edge is still enclosed.
            foreach (Vector2Int cell in EnumerateCells(border: 1))
            {
                if (!IsVisibleWall(cell))
                    continue;

                WallStyle style = GetWallStyle(layout.GetTile(cell));
                if (style.prefab != null)
                {
                    GameObject wall = Instantiate(style.prefab, geometryRoot);
                    wall.name = $"{style.prefab.name} ({cell.x}, {cell.y})";
                    wall.transform.SetLocalPositionAndRotation(CellToLocal(cell), Quaternion.Euler(0f, PrefabYaw(cell), 0f));
                    continue;
                }

                var block = new GameObject($"Wall ({cell.x}, {cell.y})").AddComponent<BoxCollider>();
                block.transform.SetParent(geometryRoot, false);
                block.transform.localPosition = CellToLocal(cell) + halfHeight;
                block.size = wallSize;

                foreach (Vector2Int offset in NeighbourOffsets)
                {
                    Vector2Int neighbour = cell + offset;
                    if (IsPlainWall(neighbour))
                        continue;

                    ZoneTheme theme = GetZoneTheme(neighbour);
                    Material material = theme != null && theme.WallMaterial != null ? theme.WallMaterial : style.material;
                    var normal = new Vector3(offset.x, 0f, offset.y);
                    faces.AddQuad(material, block.transform.localPosition + normal * (tileSize * 0.5f), normal,
                        Vector3.Cross(Vector3.up, normal), tileSize, wallHeight);
                }
            }

            faces.Build("Wall Faces", geometryRoot);
        }

        // Prefab walls are turned a different way on each tile, so repeated pillars and crate stacks do not look stamped out.
        private static float PrefabYaw(Vector2Int cell) => (cell.x * 7 + cell.y * 13) % 4 * 90f;

        private void BuildDoors()
        {
            doorsByCell.Clear();
            foreach (Vector2Int cell in EnumerateCells())
            {
                if (!layout.TryGetDoor(cell, out float yaw))
                    continue;

                if (doorPrefab == null)
                {
                    Debug.LogError($"Level layout '{layout.name}' has door tiles but no door prefab is assigned.", this);
                    return;
                }

                ZoneTheme theme = GetZoneTheme(cell);
                Door door = Instantiate(theme != null && theme.DoorPrefab != null ? theme.DoorPrefab : doorPrefab, doorRoot);
                door.name = $"Door ({cell.x}, {cell.y})";
                door.transform.SetLocalPositionAndRotation(CellToLocal(cell), Quaternion.Euler(0f, yaw, 0f));
                doorsByCell.Add(cell, door);
            }
        }

        // Walls buried between other walls can never be seen or touched, so they are not built. A wall built from a prefab
        // stands on the floor rather than filling its tile, so the walls beside it can be seen.
        private bool IsVisibleWall(Vector2Int cell)
        {
            if (layout.IsWalkable(cell))
                return false;

            foreach (Vector2Int offset in NeighbourOffsets)
            {
                if (!IsPlainWall(cell + offset))
                    return true;
            }

            return false;
        }

        private WallStyle GetWallStyle(char symbol)
        {
            foreach (WallStyle style in wallStyles)
            {
                if (style.symbol == symbol)
                    return style;
            }

            return wallStyles.Length > 0 ? wallStyles[0] : default;
        }

        private MeshRenderer CreateBlock(string blockName, Vector3 localCentre, Vector3 size, Material material)
        {
            GameObject block = GameObject.CreatePrimitive(PrimitiveType.Cube);
            block.name = blockName;
            block.transform.SetParent(geometryRoot, false);
            block.transform.localPosition = localCentre;
            block.transform.localScale = size;

            var meshRenderer = block.GetComponent<MeshRenderer>();
            meshRenderer.sharedMaterial = material;
            meshRenderer.shadowCastingMode = ShadowCastingMode.Off;
            return meshRenderer;
        }

        private void OnDrawGizmos()
        {
            if (layout == null || geometryRoot != null)
                return;

            Gizmos.matrix = transform.localToWorldMatrix;
            DrawWallGizmos();
            DrawSpawnGizmo();
            DrawGuardGizmos();
            DrawItemGizmos();
            DrawDoorGizmos();
        }

        private void DrawWallGizmos()
        {
            Gizmos.color = new Color(0.35f, 0.55f, 1f, 0.8f);
            var wallSize = new Vector3(tileSize, wallHeight, tileSize);
            var halfHeight = Vector3.up * (wallHeight * 0.5f);

            foreach (Vector2Int cell in EnumerateCells(border: 1))
            {
                if (IsVisibleWall(cell))
                    Gizmos.DrawWireCube(CellToLocal(cell) + halfHeight, wallSize);
            }
        }

        private void DrawSpawnGizmo()
        {
            if (!layout.TryFindSpawn(out Vector2Int cell, out float yaw))
                return;

            Vector3 position = CellToLocal(cell) + Vector3.up;
            Gizmos.color = Color.green;
            Gizmos.DrawSphere(position, 0.3f);
            Gizmos.DrawLine(position, position + Quaternion.Euler(0f, yaw, 0f) * Vector3.forward * tileSize * 0.5f);
        }

        private void DrawGuardGizmos()
        {
            Gizmos.color = Color.red;
            foreach (Vector2Int cell in EnumerateCells())
            {
                if (!layout.TryGetGuardSpawn(cell, out float yaw))
                    continue;

                Vector3 position = CellToLocal(cell) + Vector3.up;
                Gizmos.DrawSphere(position, 0.3f);
                Gizmos.DrawLine(position, position + Quaternion.Euler(0f, yaw, 0f) * Vector3.forward * tileSize * 0.5f);
            }
        }

        private void DrawItemGizmos()
        {
            Gizmos.color = new Color(0.2f, 0.9f, 1f, 0.9f);
            var itemSize = Vector3.one * 0.5f;

            foreach (Vector2Int cell in EnumerateCells())
            {
                if (layout.TryGetItem(cell, out _))
                    Gizmos.DrawWireCube(CellToLocal(cell) + Vector3.up * (itemSize.y * 0.5f), itemSize);
            }
        }

        private void DrawDoorGizmos()
        {
            Gizmos.color = new Color(1f, 0.8f, 0.2f, 0.9f);
            var panelSize = new Vector3(tileSize, wallHeight, tileSize * 0.1f);
            var halfHeight = Vector3.up * (wallHeight * 0.5f);

            foreach (Vector2Int cell in EnumerateCells())
            {
                if (!layout.TryGetDoor(cell, out float yaw))
                    continue;

                Gizmos.matrix = transform.localToWorldMatrix *
                    Matrix4x4.TRS(CellToLocal(cell) + halfHeight, Quaternion.Euler(0f, yaw, 0f), Vector3.one);
                Gizmos.DrawWireCube(Vector3.zero, panelSize);
            }

            Gizmos.matrix = transform.localToWorldMatrix;
        }
    }
}
