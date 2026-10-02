using System;
using System.Collections.Generic;
using UnityEngine;
using Wolfenstein.Interaction;

namespace Wolfenstein.World
{
    /// <summary>
    /// Dresses the built level so its rooms read as designed places rather than bare boxes. Each zone's theme adds trim
    /// along its walls (skirting, cornices, pilasters and pipes), beams across its ceiling and fixtures on its walls,
    /// all merged into one mesh. The level's decor map then places props, stains and markers on their tiles.
    /// Only props bring colliders of their own, and they keep to the edges of their tiles, clear of the paths between
    /// tile centres that everything walking the level follows.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class LevelDressing : MonoBehaviour
    {
        [Serializable]
        private struct PropBinding
        {
            public char symbol;
            public GameObject prefab;
            public PropPlacement placement;

            [Tooltip("Height above the floor, for props hung on a wall, in metres.")]
            public float height;

            [Tooltip("How far a prop in a corner sits from the corner, measured to its middle, in metres.")]
            public float cornerOffset;

            [Tooltip("The largest random turn given to each copy, in degrees, so repeated props do not look stamped out.")]
            public float yawJitter;
        }

        private static readonly Vector2Int[] Directions =
        {
            Vector2Int.up, Vector2Int.right, Vector2Int.down, Vector2Int.left
        };

        [SerializeField] private WorldManager world;

        [SerializeField, Tooltip("The prop each symbol of the level's decor map places. Symbols not listed are markers for other systems.")]
        private PropBinding[] props = Array.Empty<PropBinding>();

        private readonly List<GameObject> placedProps = new List<GameObject>();
        private Transform root;

        /// <summary>Raised once the level is dressed and its props placed.</summary>
        public event Action Dressed;

        /// <summary>Every prop placed from the decor map.</summary>
        public IReadOnlyList<GameObject> PlacedProps => placedProps;

        private LevelLayout Layout => world.Layout;
        private float TileSize => world.TileSize;
        private float WallHeight => world.WallHeight;

        private void OnEnable() => world.Built += Dress;

        private void OnDisable() => world.Built -= Dress;

        private void Dress()
        {
            if (root != null)
                Destroy(root.gameObject);

            placedProps.Clear();

            // Built in the world's own grid space, so it lines up with the level however the world is placed.
            root = new GameObject("Dressing").transform;
            root.SetParent(world.transform, false);

            var trim = new MeshBatch(TileSize);
            foreach (Vector2Int cell in EnumerateFloor())
            {
                ZoneTheme theme = world.GetZoneTheme(cell);
                if (theme == null)
                    continue;

                foreach (Vector2Int toWall in Directions)
                {
                    if (world.IsPlainWall(cell + toWall))
                        AddWallTrim(trim, theme, cell, toWall);
                }

                AddBeams(trim, theme, cell);
            }

            trim.Build("Trim", root);
            PlaceProps();

            // Props the player can use, such as switches, move, so they are kept out of the static batch.
            foreach (GameObject prop in placedProps)
            {
                if (prop.GetComponentInChildren<IInteractable>() != null)
                    prop.transform.SetParent(world.transform, true);
            }

            StaticBatchingUtility.Combine(root.gameObject);
            Dressed?.Invoke();
        }

        // Open floor that is not a doorway: doorways are dressed by their doors.
        private IEnumerable<Vector2Int> EnumerateFloor()
        {
            for (int y = 0; y < Layout.Height; y++)
            {
                for (int x = 0; x < Layout.Width; x++)
                {
                    var cell = new Vector2Int(x, y);
                    if (Layout.IsWalkable(cell) && !Layout.TryGetDoor(cell, out _))
                        yield return cell;
                }
            }
        }

        // The trim on one wall face: the face of the wall beside a floor tile, in the direction given.
        private void AddWallTrim(MeshBatch trim, ZoneTheme theme, Vector2Int cell, Vector2Int toWall)
        {
            Vector3 intoRoom = -Flat(toWall);
            Vector3 along = Vector3.Cross(Vector3.up, intoRoom);
            Vector3 faceCentre = CellCentre(cell) - intoRoom * (TileSize * 0.5f);
            var rotation = Quaternion.LookRotation(intoRoom);
            var alongStep = new Vector2Int(Mathf.RoundToInt(along.x), Mathf.RoundToInt(along.z));

            if (theme.SkirtingMaterial != null)
                AddStrip(trim, theme.SkirtingMaterial, cell, toWall, alongStep, faceCentre, intoRoom, rotation,
                    theme.SkirtingHeight * 0.5f, theme.SkirtingHeight, theme.SkirtingDepth);

            if (theme.CorniceMaterial != null)
                AddStrip(trim, theme.CorniceMaterial, cell, toWall, alongStep, faceCentre, intoRoom, rotation,
                    WallHeight - theme.CorniceHeight * 0.5f, theme.CorniceHeight, theme.CorniceDepth);

            if (theme.PipeMaterial != null)
                AddPipes(trim, theme, cell, toWall, alongStep, faceCentre, intoRoom, along);

            // A pilaster stands at the far end of the face, where it meets the next face of the same straight wall.
            if (theme.PilasterMaterial != null && Continues(cell, toWall, alongStep) &&
                Modulo(cell.x + cell.y, theme.PilasterSpacing) == 0)
            {
                Vector3 centre = faceCentre + along * (TileSize * 0.5f) + intoRoom * (theme.PilasterDepth * 0.5f) +
                                 Vector3.up * (WallHeight * 0.5f);
                trim.AddBox(theme.PilasterMaterial, centre, new Vector3(theme.PilasterWidth, WallHeight, theme.PilasterDepth), rotation);
            }

            if (theme.WallFixturePrefab != null && IsWholeFace(cell, toWall, alongStep) &&
                Hash(cell.x * 4 + toWall.x, cell.y * 4 + toWall.y, 17) * theme.WallFixtureSpacing < 1f)
            {
                GameObject fixture = Instantiate(theme.WallFixturePrefab, root);
                fixture.transform.SetLocalPositionAndRotation(faceCentre + Vector3.up * theme.WallFixtureHeight, rotation);
            }
        }

        // A strip along the face, lengthened or shortened at each end so strips meet cleanly at corners: in an inside
        // corner the strip on a north or south face stops short of the strip on the east or west face, and round an
        // outside corner the strip on the east or west face wraps round, so each corner is covered exactly once.
        private void AddStrip(MeshBatch trim, Material material, Vector2Int cell, Vector2Int toWall, Vector2Int alongStep,
            Vector3 faceCentre, Vector3 intoRoom, Quaternion rotation, float centreHeight, float height, float depth)
        {
            float start = -TileSize * 0.5f;
            float end = TileSize * 0.5f;
            foreach (int side in new[] { -1, 1 })
            {
                float change = EndChange(cell, toWall, alongStep * side, depth);
                if (side < 0)
                    start -= change;
                else
                    end += change;
            }

            Vector3 along = Vector3.Cross(Vector3.up, intoRoom);
            Vector3 centre = faceCentre + along * ((start + end) * 0.5f) + intoRoom * (depth * 0.5f) + Vector3.up * centreHeight;
            trim.AddBox(material, centre, new Vector3(end - start, height, depth), rotation);
        }

        private float EndChange(Vector2Int cell, Vector2Int toWall, Vector2Int step, float depth)
        {
            Vector2Int next = cell + step;
            bool facesAcrossX = toWall.x != 0;

            // An inside corner: the wall turns toward the room.
            if (world.IsPlainWall(next))
                return facesAcrossX ? 0f : -depth;

            // An outside corner: the wall ends and the floor wraps round it. Doorways have jambs of their own.
            if (!world.IsPlainWall(next + toWall) && !Layout.TryGetDoor(next + toWall, out _))
                return facesAcrossX ? depth : 0f;

            return 0f;
        }

        private void AddPipes(MeshBatch trim, ZoneTheme theme, Vector2Int cell, Vector2Int toWall, Vector2Int alongStep,
            Vector3 faceCentre, Vector3 intoRoom, Vector3 along)
        {
            float radius = theme.PipeRadius;
            float standOff = radius + 0.04f;
            float start = -TileSize * 0.5f;
            float end = TileSize * 0.5f;

            // Pipes stop clear of an inside corner, where the other wall's pipes run past, and end in a flange at an outside one.
            bool shortenAtCorners = toWall.y != 0;
            if (shortenAtCorners && world.IsPlainWall(cell - alongStep))
                start += standOff + radius * 2f;
            if (shortenAtCorners && world.IsPlainWall(cell + alongStep))
                end -= standOff + radius * 2f;

            Material material = theme.PipeMaterial;
            for (int i = 0; i < theme.PipeCount; i++)
            {
                Vector3 height = Vector3.up * (theme.PipeHeight - i * radius * 2.8f);
                Vector3 line = faceCentre + intoRoom * standOff + height;
                trim.AddCylinder(material, line + along * start, line + along * end, radius);

                foreach ((float at, bool open) in new[] { (start, !Continues(cell, toWall, -alongStep)), (end, !Continues(cell, toWall, alongStep)) })
                {
                    if (open)
                        trim.AddCylinder(material, line + along * (at - 0.03f), line + along * (at + 0.03f), radius * 1.35f);
                }
            }

            // A bracket holds the pipes to the wall in the middle of each face.
            float bracketHeight = theme.PipeCount * radius * 2.8f;
            Vector3 bracketCentre = faceCentre + intoRoom * (standOff * 0.5f) +
                                    Vector3.up * (theme.PipeHeight - (theme.PipeCount - 1) * radius * 1.4f);
            trim.AddBox(material, bracketCentre, new Vector3(0.05f, bracketHeight, standOff), Quaternion.LookRotation(intoRoom));
        }

        // Beams cross the ceiling along tile edges inside the zone, so they run from wall to wall of a room.
        private void AddBeams(MeshBatch trim, ZoneTheme theme, Vector2Int cell)
        {
            if (theme.BeamMaterial == null)
                return;

            Vector2Int across = theme.BeamsRunNorthSouth ? Vector2Int.left : Vector2Int.down;
            int line = theme.BeamsRunNorthSouth ? cell.x : cell.y;
            Vector2Int neighbour = cell + across;
            if (Modulo(line, theme.BeamSpacing) != 0 || !Layout.IsWalkable(neighbour) || Layout.TryGetDoor(neighbour, out _) ||
                world.GetZoneTheme(neighbour) != theme)
                return;

            Vector3 edge = CellCentre(cell) + Flat(across) * (TileSize * 0.5f) + Vector3.up * (WallHeight - theme.BeamDepth * 0.5f);
            Vector3 size = theme.BeamsRunNorthSouth
                ? new Vector3(theme.BeamWidth, theme.BeamDepth, TileSize)
                : new Vector3(TileSize, theme.BeamDepth, theme.BeamWidth);
            trim.AddBox(theme.BeamMaterial, edge, size, Quaternion.identity);
        }

        private void PlaceProps()
        {
            for (int y = 0; y < Layout.Height; y++)
            {
                for (int x = 0; x < Layout.Width; x++)
                {
                    var cell = new Vector2Int(x, y);
                    char symbol = Layout.GetDecor(cell);
                    if (symbol == '\0' || !TryGetBinding(symbol, out PropBinding binding))
                        continue;

                    GameObject prop = Instantiate(binding.prefab, root);
                    prop.name = $"{binding.prefab.name} ({x}, {y})";
                    Place(prop.transform, binding, cell);
                    placedProps.Add(prop);
                }
            }
        }

        private void Place(Transform prop, PropBinding binding, Vector2Int cell)
        {
            float random = Hash(cell.x, cell.y, 5);
            float jitter = (random * 2f - 1f) * binding.yawJitter;
            Vector3 centre = CellCentre(cell);

            switch (binding.placement)
            {
                case PropPlacement.AgainstWall:
                case PropPlacement.InCorner:
                case PropPlacement.OnWall:
                    if (TryFindWall(cell, out Vector2Int toWall))
                    {
                        Vector3 intoRoom = -Flat(toWall);
                        Vector3 position = centre - intoRoom * (TileSize * 0.5f);
                        if (binding.placement == PropPlacement.OnWall)
                            position += Vector3.up * binding.height;
                        else if (binding.placement == PropPlacement.InCorner && TryFindCorner(cell, toWall, out Vector2Int toCorner))
                            position += Flat(toCorner) * (TileSize * 0.5f - binding.cornerOffset);

                        prop.SetLocalPositionAndRotation(position, Quaternion.LookRotation(intoRoom) * Quaternion.Euler(0f, jitter, 0f));
                        return;
                    }

                    break;

                case PropPlacement.OnFloor:
                    var scatter = new Vector3(Hash(cell.x, cell.y, 11) - 0.5f, 0f, Hash(cell.x, cell.y, 13) - 0.5f) * (TileSize * 0.4f);
                    prop.SetLocalPositionAndRotation(centre + scatter, Quaternion.Euler(0f, random * 360f, 0f));
                    return;

                case PropPlacement.FromCeiling:
                    prop.SetLocalPositionAndRotation(centre + Vector3.up * WallHeight, Quaternion.Euler(0f, Mathf.Round(random * 4f) * 90f + jitter, 0f));
                    return;
            }

            // In the middle of the tile, or anywhere that has no wall to stand against.
            prop.SetLocalPositionAndRotation(centre, Quaternion.Euler(0f, Mathf.Round(random * 4f) * 90f + jitter, 0f));
        }

        // A plain wall beside the tile, looking round from a different side on each tile so props do not all face one way.
        private bool TryFindWall(Vector2Int cell, out Vector2Int toWall)
        {
            int first = Mathf.FloorToInt(Hash(cell.x, cell.y, 3) * Directions.Length);
            for (int i = 0; i < Directions.Length; i++)
            {
                toWall = Directions[(first + i) % Directions.Length];
                if (world.IsPlainWall(cell + toWall))
                    return true;
            }

            toWall = default;
            return false;
        }

        private bool TryFindCorner(Vector2Int cell, Vector2Int toWall, out Vector2Int toCorner)
        {
            foreach (Vector2Int side in new[] { new Vector2Int(-toWall.y, toWall.x), new Vector2Int(toWall.y, -toWall.x) })
            {
                if (world.IsPlainWall(cell + side))
                {
                    toCorner = side;
                    return true;
                }
            }

            toCorner = default;
            return false;
        }

        private bool TryGetBinding(char symbol, out PropBinding binding)
        {
            foreach (PropBinding candidate in props)
            {
                if (candidate.symbol == symbol && candidate.prefab != null)
                {
                    binding = candidate;
                    return true;
                }
            }

            binding = default;
            return false;
        }

        // The wall carries on past the end of the face, as a straight run of the same zone.
        private bool Continues(Vector2Int cell, Vector2Int toWall, Vector2Int step)
        {
            Vector2Int next = cell + step;
            return Layout.IsWalkable(next) && !Layout.TryGetDoor(next, out _) && world.IsPlainWall(next + toWall) &&
                   Layout.GetZone(next) == Layout.GetZone(cell);
        }

        private bool IsWholeFace(Vector2Int cell, Vector2Int toWall, Vector2Int alongStep) =>
            Continues(cell, toWall, alongStep) && Continues(cell, toWall, -alongStep);

        private Vector3 CellCentre(Vector2Int cell) => new Vector3((cell.x + 0.5f) * TileSize, 0f, (cell.y + 0.5f) * TileSize);

        private static Vector3 Flat(Vector2Int direction) => new Vector3(direction.x, 0f, direction.y);

        private static int Modulo(int value, int divisor) => (value % divisor + divisor) % divisor;

        // A repeatable pseudo-random value from 0 to 1 for a tile, so the level dresses the same way every time.
        private static float Hash(int x, int y, int salt)
        {
            unchecked
            {
                uint h = (uint)(x * 73856093 ^ y * 19349663 ^ salt * 83492791);
                h = (h ^ (h >> 13)) * 1274126177u;
                return ((h ^ (h >> 16)) & 0xFFFFFF) / 16777216f;
            }
        }
    }
}
