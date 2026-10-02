using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace Wolfenstein.Editor
{
    // A small kit for building monster bodies as faceted low-poly meshes instead of stacks of primitive shapes: lumpy
    // masses of muscle, tapering limbs, ridged curving horns, hooked claws, fangs and spikes. Every face is flat-shaded
    // and takes one shade from a palette texture, picked at random within a tone, which gives the mottled, painterly
    // look of the reference art on the ordinary lit shader. All the parts on one pivot of a monster's rig are merged into
    // a single mesh, so each monster costs a handful of renderers however detailed it is.
    public static partial class TestLevelBuilder
    {
        private const string MonsterMeshFolder = "Assets/Meshes/Monsters";
        private const string MonsterPalettePath = "Assets/Textures/Monster_Palette.png";
        private const int PaletteSize = 16;

        // The palette's rows, each a ramp of 16 shades from dark to light.
        private static readonly (Color dark, Color light)[] PaletteRows =
        {
            (new Color(0.26f, 0.05f, 0.04f), new Color(0.72f, 0.24f, 0.17f)), // 0 red flesh
            (new Color(0.4f, 0.22f, 0.17f), new Color(0.86f, 0.6f, 0.48f)),   // 1 tan flesh
            (new Color(0.06f, 0.03f, 0.03f), new Color(0.26f, 0.1f, 0.08f)),  // 2 dark hide
            (new Color(0.34f, 0.25f, 0.17f), new Color(0.88f, 0.77f, 0.6f)),  // 3 bone and horn
            (new Color(0.07f, 0.07f, 0.08f), new Color(0.34f, 0.33f, 0.34f)), // 4 iron
            (new Color(0.28f, 0.01f, 0.01f), new Color(0.62f, 0.05f, 0.04f)), // 5 blood
            (new Color(0.04f, 0f, 0f), new Color(0.2f, 0.03f, 0.02f)),        // 6 the inside of a mouth
            (new Color(0.03f, 0.02f, 0.02f), new Color(0.3f, 0.23f, 0.19f)),  // 7 claw
            (new Color(0.68f, 0.62f, 0.48f), new Color(0.98f, 0.94f, 0.84f)), // 8 teeth
            (new Color(0.17f, 0.14f, 0.13f), new Color(0.55f, 0.48f, 0.42f)), // 9 shell plating
            (new Color(0.05f, 0.04f, 0.04f), new Color(0.23f, 0.17f, 0.15f)), // 10 charcoal armour
            (new Color(0.34f, 0.04f, 0.03f), new Color(0.86f, 0.18f, 0.1f)),  // 11 demon red
            (new Color(0.01f, 0.01f, 0.01f), new Color(0.05f, 0.04f, 0.04f)), // 12 black
        };

        // A range of shades on one palette row, with a chance of a shade from a second row for patches of another colour.
        private readonly struct Tone
        {
            public readonly int Row;
            public readonly int Darkest;
            public readonly int Lightest;
            public readonly int PatchRow;
            public readonly float PatchChance;

            public Tone(int row, int darkest, int lightest, int patchRow = -1, float patchChance = 0f)
            {
                Row = row;
                Darkest = darkest;
                Lightest = lightest;
                PatchRow = patchRow;
                PatchChance = patchChance;
            }

            public Vector2 Pick(float shade, float patch)
            {
                int row = PatchRow >= 0 && patch < PatchChance ? PatchRow : Row;
                int column = Mathf.Clamp(Darkest + Mathf.FloorToInt(shade * (Lightest - Darkest + 1)), 0, PaletteSize - 1);
                return new Vector2((column + 0.5f) / PaletteSize, (row + 0.5f) / PaletteSize);
            }
        }

        private static readonly Tone RedFlesh = new Tone(0, 3, 13, 1, 0.3f);
        private static readonly Tone TanFlesh = new Tone(1, 2, 12, 0, 0.35f);
        private static readonly Tone DarkHide = new Tone(2, 4, 15, 0, 0.3f);
        private static readonly Tone BoneTone = new Tone(3, 4, 15);
        private static readonly Tone IronTone = new Tone(4, 2, 12);
        private static readonly Tone BloodTone = new Tone(5, 3, 15);
        private static readonly Tone MouthTone = new Tone(6, 0, 9);
        private static readonly Tone ClawTone = new Tone(7, 0, 10);
        private static readonly Tone TeethTone = new Tone(8, 5, 15);
        private static readonly Tone ShellTone = new Tone(9, 3, 14, 5, 0.18f);
        private static readonly Tone CharcoalTone = new Tone(10, 2, 14, 0, 0.18f);
        private static readonly Tone DemonSkin = new Tone(11, 3, 14, 5, 0.3f);
        private static readonly Tone BlackTone = new Tone(12, 0, 15);

        private sealed class MeshKit
        {
            private readonly List<Vector3> vertices = new List<Vector3>();
            private readonly List<Vector3> normals = new List<Vector3>();
            private readonly List<Vector2> uvs = new List<Vector2>();
            private readonly List<int> triangles = new List<int>();
            private readonly int seed;
            private int faces;

            public MeshKit(int seed) => this.seed = seed;

            /// <summary>
            /// A lumpy low-poly ellipsoid, such as a mass of muscle or a skull. Its surface is pushed in and out at random
            /// by up to the lumpiness, as a share of its radius.
            /// </summary>
            public void Blob(Vector3 centre, Vector3 radii, Tone tone, Vector3 angles = default, float lumpiness = 0.1f,
                int rings = 6, int segments = 10)
            {
                Quaternion rotation = Quaternion.Euler(angles);
                var grid = new Vector3[rings + 1, segments];
                for (int ring = 0; ring <= rings; ring++)
                {
                    float theta = Mathf.PI * ring / rings;
                    for (int segment = 0; segment < segments; segment++)
                    {
                        float phi = 2f * Mathf.PI * segment / segments;
                        var direction = new Vector3(Mathf.Sin(theta) * Mathf.Cos(phi), Mathf.Cos(theta), Mathf.Sin(theta) * Mathf.Sin(phi));

                        // The poles are single points, so every segment of a pole shares one lump.
                        bool pole = ring == 0 || ring == rings;
                        float lump = 1f + lumpiness * (Random01(ring, pole ? 0 : segment, 3) * 2f - 1f);
                        grid[ring, segment] = centre + rotation * Vector3.Scale(direction * lump, radii);
                    }
                }

                for (int ring = 0; ring < rings; ring++)
                {
                    for (int segment = 0; segment < segments; segment++)
                    {
                        int next = (segment + 1) % segments;
                        Vector3 a = grid[ring, segment];
                        Vector3 b = grid[ring, next];
                        Vector3 c = grid[ring + 1, next];
                        Vector3 d = grid[ring + 1, segment];
                        if (ring > 0)
                            Triangle(a, b, c, tone, (a + b + c) / 3f - centre);
                        if (ring < rings - 1)
                            Triangle(a, c, d, tone, (a + c + d) / 3f - centre);
                    }
                }
            }

            /// <summary>
            /// A tube swept along a path, its radius changing along the way, closed at any end that has width.
            /// A radius of 0 at the end draws it to a point. Ridges make every other ring that much wider, as on a horn.
            /// </summary>
            public void Tube(IReadOnlyList<Vector3> path, IReadOnlyList<float> radii, Tone tone, int sides = 7, float ridges = 0f)
            {
                int count = path.Count;
                var rings = new Vector3[count][];
                var tangents = new Vector3[count];
                for (int i = 0; i < count; i++)
                    tangents[i] = (path[Mathf.Min(i + 1, count - 1)] - path[Mathf.Max(i - 1, 0)]).normalized;

                Vector3 normal = Vector3.Cross(tangents[0], Mathf.Abs(tangents[0].y) < 0.9f ? Vector3.up : Vector3.right).normalized;
                for (int i = 0; i < count; i++)
                {
                    // The ring's frame is carried along the path, so the tube does not twist.
                    normal = (normal - Vector3.Dot(normal, tangents[i]) * tangents[i]).normalized;
                    Vector3 binormal = Vector3.Cross(tangents[i], normal);
                    float radius = radii[i] * (ridges > 0f && i % 2 == 1 && i < count - 1 ? 1f + ridges : 1f);
                    rings[i] = new Vector3[sides];
                    for (int side = 0; side < sides; side++)
                    {
                        float angle = 2f * Mathf.PI * side / sides;
                        rings[i][side] = path[i] + (normal * Mathf.Cos(angle) + binormal * Mathf.Sin(angle)) * radius;
                    }
                }

                for (int i = 0; i < count - 1; i++)
                {
                    Vector3 axis = (path[i] + path[i + 1]) * 0.5f;
                    for (int side = 0; side < sides; side++)
                    {
                        int next = (side + 1) % sides;
                        Vector3 a = rings[i][side];
                        Vector3 b = rings[i][next];
                        Vector3 c = rings[i + 1][next];
                        Vector3 d = rings[i + 1][side];
                        Triangle(a, b, c, tone, (a + b + c) / 3f - axis);
                        Triangle(a, c, d, tone, (a + c + d) / 3f - axis);
                    }
                }

                if (radii[0] > 0.001f)
                    Cap(path[0], rings[0], -tangents[0], tone);
                if (radii[count - 1] > 0.001f)
                    Cap(path[count - 1], rings[count - 1], tangents[count - 1], tone);
            }

            /// <summary>A limb or neck: a tube from one point to another, tapering from one radius to the other.</summary>
            public void Limb(Vector3 from, Vector3 to, float fromRadius, float toRadius, Tone tone, int sides = 7) =>
                Tube(new[] { from, to }, new[] { fromRadius, toRadius }, tone, sides);

            /// <summary>A straight spike, fang or thorn from its root to its point.</summary>
            public void Spike(Vector3 root, Vector3 tip, float radius, Tone tone, int sides = 5) =>
                Tube(new[] { root, tip }, new[] { radius, 0f }, tone, sides);

            /// <summary>
            /// A curving point, such as a claw or a horn: a tube along a curve from its root through a bend to its tip,
            /// tapering to nothing. Ridges ring it like a ram's horn.
            /// </summary>
            public void Hook(Vector3 root, Vector3 bend, Vector3 tip, float radius, Tone tone, int segments = 5, int sides = 5,
                float ridges = 0f) =>
                Hook(root, bend, bend, tip, radius, tone, segments, sides, ridges);

            /// <summary>A curving point along a curve with two bends, for horns that sweep out and then up.</summary>
            public void Hook(Vector3 root, Vector3 firstBend, Vector3 secondBend, Vector3 tip, float radius, Tone tone, int segments,
                int sides, float ridges)
            {
                var path = new Vector3[segments + 1];
                var radii = new float[segments + 1];
                for (int i = 0; i <= segments; i++)
                {
                    float t = (float)i / segments;
                    float u = 1f - t;
                    path[i] = u * u * u * root + 3f * u * u * t * firstBend + 3f * u * t * t * secondBend + t * t * t * tip;
                    radii[i] = radius * (1f - t);
                }

                Tube(path, radii, tone, sides, ridges);
            }

            /// <summary>A box, such as an armour plate, turned to the given angles.</summary>
            public void Slab(Vector3 centre, Vector3 size, Tone tone, Vector3 angles = default)
            {
                Quaternion rotation = Quaternion.Euler(angles);
                Vector3 half = size * 0.5f;
                Vector3 Corner(int x, int y, int z) => centre + rotation * Vector3.Scale(half, new Vector3(x, y, z));
                (int, int, int)[][] quads =
                {
                    new[] { (1, -1, -1), (1, 1, -1), (1, 1, 1), (1, -1, 1) },
                    new[] { (-1, -1, 1), (-1, 1, 1), (-1, 1, -1), (-1, -1, -1) },
                    new[] { (-1, 1, -1), (-1, 1, 1), (1, 1, 1), (1, 1, -1) },
                    new[] { (-1, -1, 1), (-1, -1, -1), (1, -1, -1), (1, -1, 1) },
                    new[] { (-1, -1, 1), (1, -1, 1), (1, 1, 1), (-1, 1, 1) },
                    new[] { (1, -1, -1), (-1, -1, -1), (-1, 1, -1), (1, 1, -1) },
                };

                foreach ((int, int, int)[] quad in quads)
                {
                    Vector3 a = Corner(quad[0].Item1, quad[0].Item2, quad[0].Item3);
                    Vector3 b = Corner(quad[1].Item1, quad[1].Item2, quad[1].Item3);
                    Vector3 c = Corner(quad[2].Item1, quad[2].Item2, quad[2].Item3);
                    Vector3 d = Corner(quad[3].Item1, quad[3].Item2, quad[3].Item3);
                    Vector3 outward = (a + b + c + d) * 0.25f - centre;
                    Triangle(a, b, c, tone, outward);
                    Triangle(a, c, d, tone, outward);
                }
            }

            public Mesh Build(string meshName)
            {
                var mesh = new Mesh { name = meshName };
                mesh.indexFormat = vertices.Count > 65000 ? IndexFormat.UInt32 : IndexFormat.UInt16;
                mesh.SetVertices(vertices);
                mesh.SetNormals(normals);
                mesh.SetUVs(0, uvs);
                mesh.SetTriangles(triangles, 0);
                mesh.RecalculateBounds();
                return mesh;
            }

            // One flat-shaded face in a single shade, turned to face the given outward direction.
            private void Triangle(Vector3 a, Vector3 b, Vector3 c, Tone tone, Vector3 outward)
            {
                Vector3 normal = Vector3.Cross(b - a, c - a);
                if (normal.sqrMagnitude < 1e-12f)
                    return;

                if (Vector3.Dot(normal, outward) < 0f)
                {
                    (b, c) = (c, b);
                    normal = -normal;
                }

                faces++;
                Vector2 uv = tone.Pick(Random01(faces, 0, 1), Random01(faces, 0, 2));
                int start = vertices.Count;
                foreach (Vector3 corner in new[] { a, b, c })
                {
                    vertices.Add(corner);
                    normals.Add(normal.normalized);
                    uvs.Add(uv);
                }

                triangles.Add(start);
                triangles.Add(start + 1);
                triangles.Add(start + 2);
            }

            private void Cap(Vector3 centre, Vector3[] ring, Vector3 outward, Tone tone)
            {
                for (int side = 0; side < ring.Length; side++)
                    Triangle(centre, ring[side], ring[(side + 1) % ring.Length], tone, outward);
            }

            private float Random01(int x, int y, int salt) => Hash(x * 31 + seed, y * 17 + salt, seed * 7 + salt);
        }

        // Builds a kit's mesh onto a pivot of the rig, saved as an asset so the prefab can use it. The mesh asset is updated
        // in place on every rebuild, so references to it stay good.
        private static void Skin(Transform pivot, string monsterName, string partName, MeshKit kit)
        {
            EnsureFolder("Assets/Meshes");
            EnsureFolder(MonsterMeshFolder);

            string path = $"{MonsterMeshFolder}/{monsterName}_{partName.Replace(" ", string.Empty)}.asset";
            Mesh mesh = kit.Build($"{monsterName} {partName}");
            var existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (existing == null)
            {
                AssetDatabase.CreateAsset(mesh, path);
            }
            else
            {
                existing.Clear();
                EditorUtility.CopySerialized(mesh, existing);
                Object.DestroyImmediate(mesh);
                mesh = existing;
            }

            var holder = new GameObject($"{monsterName} {partName}");
            holder.transform.SetParent(pivot, false);
            holder.AddComponent<MeshFilter>().sharedMesh = mesh;
            var meshRenderer = holder.AddComponent<MeshRenderer>();
            meshRenderer.sharedMaterial = MonsterPaletteMaterial();
            meshRenderer.shadowCastingMode = ShadowCastingMode.Off;
        }

        // Every monster body shares this one material.
        private static Material MonsterPaletteMaterial()
        {
            if (AssetDatabase.LoadAssetAtPath<Texture2D>(MonsterPalettePath) == null)
                WriteMonsterPalette();

            var palette = AssetDatabase.LoadAssetAtPath<Texture2D>(MonsterPalettePath);
            return GetOrCreateMaterial("Monster_Flesh", Color.white, palette, smoothness: 0.35f);
        }

        // Written on every rebuild, before the monsters are built, so the colours in code always win.
        private static void WriteMonsterPalette()
        {
            var pixels = new Color[PaletteSize * PaletteSize];
            for (int row = 0; row < PaletteSize; row++)
            {
                (Color dark, Color light) = row < PaletteRows.Length ? PaletteRows[row] : (Color.magenta, Color.magenta);
                for (int column = 0; column < PaletteSize; column++)
                {
                    float t = column / (PaletteSize - 1f);
                    pixels[row * PaletteSize + column] = Opaque(Color.Lerp(dark, light, t * t * (3f - 2f * t)));
                }
            }

            // Point filtering without mipmaps keeps every face exactly one shade at any distance.
            WriteTexture(MonsterPalettePath, CreateTexture(pixels, PaletteSize), TextureWrapMode.Clamp, FilterMode.Point, mipmaps: false);
        }
    }
}
