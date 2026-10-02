using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Wolfenstein.Editor
{
    // Procedural retro textures. Each surface is drawn as colour plus a height field, and the heights become a normal map,
    // so mortar, rivets and seams catch the light of nearby lamps. Textures are small and point-filtered for crisp pixels:
    // a 128-pixel surface texture spans one 3 m tile, as walls, floor and ceiling all do.
    public static partial class TestLevelBuilder
    {
        private const int SurfaceSize = 128;

        // A surface being drawn: a colour and a height for every pixel, with (0, 0) at the bottom left.
        private sealed class Surface
        {
            public Surface(int size)
            {
                Size = size;
                Albedo = new Color[size * size];
                Height = new float[size * size];
            }

            public int Size { get; }
            public Color[] Albedo { get; }
            public float[] Height { get; }

            public void Set(int x, int y, Color colour, float height)
            {
                Albedo[y * Size + x] = Opaque(colour);
                Height[y * Size + x] = height;
            }
        }

        // The colour texture and normal map of a surface, each only created when missing.
        private static (Texture2D albedo, Texture2D normals) GetOrCreateSurface(string textureName, Func<Surface> draw, float bumpiness)
        {
            string albedoPath = $"{TextureFolder}/{textureName}.png";
            string normalPath = $"{TextureFolder}/{textureName}_Normal.png";
            if (!File.Exists(albedoPath) || !File.Exists(normalPath))
            {
                Surface surface = draw();
                WriteTexture(albedoPath, CreateTexture(surface.Albedo, surface.Size), TextureWrapMode.Repeat, FilterMode.Point);
                WriteTexture(normalPath, CreateTexture(HeightsToNormals(surface, bumpiness), surface.Size), TextureWrapMode.Repeat,
                    FilterMode.Point, TextureImporterType.NormalMap);
            }

            return (AssetDatabase.LoadAssetAtPath<Texture2D>(albedoPath), AssetDatabase.LoadAssetAtPath<Texture2D>(normalPath));
        }

        private static void WriteTexture(string path, Texture2D texture, TextureWrapMode wrapMode, FilterMode filterMode,
            TextureImporterType type = TextureImporterType.Default, bool mipmaps = true)
        {
            File.WriteAllBytes(path, texture.EncodeToPNG());
            Object.DestroyImmediate(texture);

            AssetDatabase.ImportAsset(path);
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType = type;
            importer.filterMode = filterMode;
            importer.wrapMode = wrapMode;
            importer.mipmapEnabled = mipmaps;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.SaveAndReimport();
        }

        // Slopes of the height field, wrapping at the edges so the normal map tiles like its texture.
        private static Color[] HeightsToNormals(Surface surface, float bumpiness)
        {
            int size = surface.Size;
            float HeightAt(int x, int y) => surface.Height[(y + size) % size * size + (x + size) % size];

            var normals = new Color[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float slopeX = HeightAt(x + 1, y) - HeightAt(x - 1, y);
                    float slopeY = HeightAt(x, y + 1) - HeightAt(x, y - 1);
                    Vector3 normal = new Vector3(-slopeX * bumpiness, -slopeY * bumpiness, 1f).normalized;
                    normals[y * size + x] = new Color(normal.x * 0.5f + 0.5f, normal.y * 0.5f + 0.5f, normal.z * 0.5f + 0.5f, 1f);
                }
            }

            return normals;
        }

        // Running-bond brickwork: offset courses of bricks in dark mortar, each brick its own shade, bevelled and pitted,
        // with grime gathered along the bottom courses.
        private static Surface DrawBrickwork(int seed, Color brick, Color mortar, int brickWidth, int brickHeight)
        {
            const int mortarWidth = 2;
            var surface = new Surface(SurfaceSize);
            for (int y = 0; y < SurfaceSize; y++)
            {
                int course = y / brickHeight;
                int inCourseY = y % brickHeight;
                for (int x = 0; x < SurfaceSize; x++)
                {
                    int shiftedX = (x + course % 2 * (brickWidth / 2)) % SurfaceSize;
                    int inBrickX = shiftedX % brickWidth;
                    float grain = Grain(x, y, SurfaceSize, seed);

                    if (inBrickX < mortarWidth || inCourseY < mortarWidth)
                    {
                        surface.Set(x, y, mortar * (0.8f + 0.4f * grain) * Grime(y, SurfaceSize), 0.1f * grain);
                        continue;
                    }

                    float shade = (0.78f + 0.3f * Hash(shiftedX / brickWidth, course, seed)) * (0.84f + 0.32f * grain);
                    shade *= Bevel(inBrickX - mortarWidth, inCourseY - mortarWidth, brickWidth - mortarWidth, brickHeight - mortarWidth);
                    float height = 0.6f + 0.4f * EdgeRise(inBrickX - mortarWidth, inCourseY - mortarWidth,
                        brickWidth - mortarWidth, brickHeight - mortarWidth) - 0.2f * grain;

                    Pit(x, y, seed, ref shade, ref height);
                    surface.Set(x, y, brick * (shade * Grime(y, SurfaceSize)), height);
                }
            }

            return surface;
        }

        // Courses of large rough-cut stone blocks of uneven length, each course laid from its own starting point.
        private static Surface DrawAshlar(int seed, Color stone, Color mortar)
        {
            const int courseHeight = 32;
            const int mortarWidth = 2;
            int[] blockWidths = { 32, 40, 48, 56 };

            var surface = new Surface(SurfaceSize);
            for (int course = 0; course < SurfaceSize / courseHeight; course++)
            {
                // Block edges along the course, from a random start that wraps around the texture.
                int start = (int)(Hash(course, 0, seed) * SurfaceSize);
                var edges = new List<int> { 0 };
                for (int i = 1; edges[edges.Count - 1] < SurfaceSize; i++)
                    edges.Add(edges[edges.Count - 1] + blockWidths[(int)(Hash(course, i, seed) * blockWidths.Length)]);
                edges[edges.Count - 1] = SurfaceSize;
                if (edges.Count > 2 && edges[edges.Count - 1] - edges[edges.Count - 2] < 20)
                    edges.RemoveAt(edges.Count - 2);

                for (int inCourseY = 0; inCourseY < courseHeight; inCourseY++)
                {
                    int y = course * courseHeight + inCourseY;
                    for (int x = 0; x < SurfaceSize; x++)
                    {
                        int along = (x - start + SurfaceSize) % SurfaceSize;
                        int block = 0;
                        while (along >= edges[block + 1])
                            block++;

                        int inBlockX = along - edges[block];
                        int blockWidth = edges[block + 1] - edges[block];
                        float grain = Grain(x, y, SurfaceSize, seed);

                        if (inBlockX < mortarWidth || inCourseY < mortarWidth)
                        {
                            surface.Set(x, y, mortar * (0.8f + 0.4f * grain) * Grime(y, SurfaceSize), 0.05f * grain);
                            continue;
                        }

                        float shade = (0.8f + 0.28f * Hash(course, block + 17, seed)) * (0.8f + 0.4f * grain);
                        shade *= Bevel(inBlockX - mortarWidth, inCourseY - mortarWidth, blockWidth - mortarWidth, courseHeight - mortarWidth);
                        float height = 0.55f + 0.45f * EdgeRise(inBlockX - mortarWidth, inCourseY - mortarWidth,
                            blockWidth - mortarWidth, courseHeight - mortarWidth) - 0.3f * grain;

                        Pit(x, y, seed, ref shade, ref height);
                        surface.Set(x, y, stone * (shade * Grime(y, SurfaceSize)), height);
                    }
                }
            }

            return surface;
        }

        // Worn square flagstones in dark grout, each a slightly different shade.
        private static Surface DrawFlagstones(int seed, Color stone, Color grout)
        {
            const int slabSize = 32;
            const int groutWidth = 2;
            var surface = new Surface(SurfaceSize);
            for (int y = 0; y < SurfaceSize; y++)
            {
                for (int x = 0; x < SurfaceSize; x++)
                {
                    int inSlabX = x % slabSize;
                    int inSlabY = y % slabSize;
                    float grain = Grain(x, y, SurfaceSize, seed);

                    if (inSlabX < groutWidth || inSlabY < groutWidth)
                    {
                        surface.Set(x, y, grout * (0.85f + 0.3f * grain), 0f);
                        continue;
                    }

                    float slabShade = 0.82f + 0.26f * Hash(x / slabSize, y / slabSize, seed);
                    float shade = slabShade * (0.86f + 0.28f * grain);
                    shade *= Bevel(inSlabX - groutWidth, inSlabY - groutWidth, slabSize - groutWidth, slabSize - groutWidth);
                    float height = 0.7f + 0.3f * EdgeRise(inSlabX - groutWidth, inSlabY - groutWidth,
                        slabSize - groutWidth, slabSize - groutWidth) - 0.15f * grain;

                    Pit(x, y, seed, ref shade, ref height);
                    surface.Set(x, y, stone * shade, height);
                }
            }

            return surface;
        }

        // Concrete ceiling panels between dark seams, with a rivet near each corner.
        private static Surface DrawCeilingPanels(int seed, Color concrete, Color seam)
        {
            const int panelSize = 64;
            const int seamWidth = 3;
            var surface = new Surface(SurfaceSize);
            for (int y = 0; y < SurfaceSize; y++)
            {
                for (int x = 0; x < SurfaceSize; x++)
                {
                    int inPanelX = x % panelSize;
                    int inPanelY = y % panelSize;
                    float grain = Grain(x, y, SurfaceSize, seed);

                    if (inPanelX < seamWidth || inPanelY < seamWidth)
                    {
                        surface.Set(x, y, seam, 0f);
                        continue;
                    }

                    int fromEdgeX = Mathf.Min(inPanelX - seamWidth, panelSize - 1 - inPanelX);
                    int fromEdgeY = Mathf.Min(inPanelY - seamWidth, panelSize - 1 - inPanelY);
                    bool rivet = fromEdgeX is >= 4 and <= 6 && fromEdgeY is >= 4 and <= 6;

                    float shade = (0.85f + 0.2f * Hash(x / panelSize, y / panelSize, seed)) * (0.82f + 0.36f * grain);
                    surface.Set(x, y, rivet ? concrete * 1.35f : concrete * shade, rivet ? 1f : 0.5f - 0.2f * grain);
                }
            }

            return surface;
        }

        // A riveted steel door in the teal of the original game: a raised frame, vertical ribs, a horizontal brace
        // across the middle and a dark handle plate by the edge it opens from.
        private static Surface DrawDoor(int seed, Color steel)
        {
            const int frameWidth = 7;
            const int ribSpacing = 16;
            var brace = new RangeInt(58, 12);
            var handle = new RectInt(102, 50, 12, 28);

            var surface = new Surface(SurfaceSize);
            for (int y = 0; y < SurfaceSize; y++)
            {
                for (int x = 0; x < SurfaceSize; x++)
                {
                    float grain = Grain(x, y, SurfaceSize, seed);
                    int fromEdge = Mathf.Min(Mathf.Min(x, SurfaceSize - 1 - x), Mathf.Min(y, SurfaceSize - 1 - y));
                    bool inFrame = fromEdge < frameWidth;
                    bool onBrace = y >= brace.start && y < brace.end;

                    float shade;
                    float height;
                    if (handle.Contains(new Vector2Int(x, y)))
                    {
                        bool slot = x >= handle.x + 4 && x < handle.x + 8 && y >= handle.y + 5 && y < handle.yMax - 5;
                        shade = slot ? 0.2f : 0.45f;
                        height = slot ? 0.6f : 1.1f;
                    }
                    else if (inFrame || onBrace)
                    {
                        // Rivets sit in the middle of the frame and the brace, every rib spacing.
                        int across = inFrame ? fromEdge : y - brace.start;
                        int along = inFrame && (x < frameWidth || x >= SurfaceSize - frameWidth) ? y : x;
                        int centre = inFrame ? frameWidth / 2 : brace.length / 2;
                        bool rivet = Mathf.Abs(across - centre) <= 1 && Mathf.Abs(along % ribSpacing - ribSpacing / 2) <= 1;
                        shade = rivet ? 1.3f : 0.72f;
                        height = rivet ? 1.2f : 0.9f;
                        if (!rivet && across == 0)
                            shade *= 1.2f;
                    }
                    else
                    {
                        int inRib = x % ribSpacing;
                        bool rib = inRib < 4;
                        shade = rib ? (inRib == 0 ? 1.25f : 1.1f) : (inRib == 4 ? 0.75f : 0.95f);
                        height = rib ? 0.75f : 0.4f;
                    }

                    shade *= 0.9f + 0.2f * grain;
                    surface.Set(x, y, steel * shade, height - 0.05f * grain);
                }
            }

            return surface;
        }

        // Steel plating for the sides of a doorway: riveted horizontal plates, and down the middle the slot the door slides into.
        private static Surface DrawDoorJamb(int seed, Color steel)
        {
            const int plateHeight = 32;
            const int slotHalfWidth = 3;
            var surface = new Surface(SurfaceSize);
            for (int y = 0; y < SurfaceSize; y++)
            {
                for (int x = 0; x < SurfaceSize; x++)
                {
                    float grain = Grain(x, y, SurfaceSize, seed);
                    int inPlate = y % plateHeight;
                    int fromSlot = Mathf.Abs(x - SurfaceSize / 2);

                    if (fromSlot < slotHalfWidth)
                    {
                        surface.Set(x, y, steel * 0.15f, 0f);
                        continue;
                    }

                    bool seam = inPlate < 2;
                    bool rivet = inPlate is >= 5 and <= 7 && (x % 16) is >= 7 and <= 9;
                    float shade = seam ? 0.45f : rivet ? 1.35f : 0.9f + 0.2f * grain;
                    if (fromSlot < slotHalfWidth + 3)
                        shade *= 0.7f;

                    surface.Set(x, y, steel * shade, seam ? 0.2f : rivet ? 1f : 0.6f - 0.1f * grain);
                }
            }

            return surface;
        }

        // Woven cloth: a fine diagonal twill over soft blotches, near white so a material's colour tints it.
        private static Texture2D GenerateFabricTexture()
        {
            const int size = 32;
            var pixels = new Color[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float twill = (x + y) % 4 < 2 ? 1f : 0.9f;
                    float value = twill * (0.84f + 0.16f * Grain(x, y, size, 7));
                    pixels[y * size + x] = Opaque(Color.white * value);
                }
            }

            return CreateTexture(pixels, size);
        }

        // Wood grain running along the texture, near white so a material's colour tints it.
        private static Texture2D GenerateWoodTexture()
        {
            const int size = 64;
            var pixels = new Color[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float warp = Noise(x, y, size, 4, 8) * 10f;
                    float rings = 0.5f + 0.5f * Mathf.Sin((y + warp) * 0.7f);
                    float value = 0.72f + 0.2f * rings + 0.08f * Noise(x, y, size, 32, 9);
                    pixels[y * size + x] = Opaque(Color.white * value);
                }
            }

            return CreateTexture(pixels, size);
        }

        // A soft round shadow, darkest in the middle and fading to nothing at the edge.
        private static Texture2D GenerateBlobShadowTexture()
        {
            const int size = 64;
            var centre = new Vector2(size - 1, size - 1) * 0.5f;
            var pixels = new Color[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float radius = Vector2.Distance(new Vector2(x, y), centre) / (size * 0.5f);
                    float alpha = Mathf.Pow(Mathf.Clamp01(1f - radius), 1.5f);
                    pixels[y * size + x] = new Color(0f, 0f, 0f, alpha);
                }
            }

            return CreateTexture(pixels, size);
        }

        // Light catches the top and left edges of a raised block and misses the bottom and right.
        private static float Bevel(int x, int y, int width, int height)
        {
            if (y == height - 1 || x == 0)
                return 1.14f;
            if (y == 0 || x == width - 1)
                return 0.8f;
            return 1f;
        }

        // Rises from 0 at a block's edge to 1 a few pixels in, so blocks have rounded shoulders in the normal map.
        private static float EdgeRise(int x, int y, int width, int height)
        {
            int fromEdge = Mathf.Min(Mathf.Min(x, width - 1 - x), Mathf.Min(y, height - 1 - y));
            return Mathf.Clamp01(fromEdge / 3f);
        }

        // An occasional small pit in the stone.
        private static void Pit(int x, int y, int seed, ref float shade, ref float height)
        {
            if (Hash(x, y, seed + 31) < 0.985f)
                return;

            shade *= 0.6f;
            height -= 0.35f;
        }

        // Walls darken toward the floor, where dirt collects, and a little toward the ceiling.
        private static float Grime(int y, int size)
        {
            float fromFloor = y / (size * 0.18f);
            float fromCeiling = (size - 1 - y) / (size * 0.06f);
            return Mathf.Lerp(0.7f, 1f, Mathf.Clamp01(fromFloor)) * Mathf.Lerp(0.88f, 1f, Mathf.Clamp01(fromCeiling));
        }

        // Fine, tileable irregularity from 0 to 1, mixing coarse blotches with finer speckle.
        private static float Grain(int x, int y, int size, int seed) =>
            0.5f * Noise(x, y, size, Mathf.Max(1, size / 16), seed) +
            0.3f * Noise(x, y, size, Mathf.Max(1, size / 8), seed + 1) +
            0.2f * Noise(x, y, size, Mathf.Max(1, size / 2), seed + 2);

        // Smooth value noise from 0 to 1 that tiles across a texture of the given size. Cells must divide the size.
        private static float Noise(int x, int y, int size, int cells, int seed)
        {
            float cellX = (float)x * cells / size;
            float cellY = (float)y * cells / size;
            int x0 = Mathf.FloorToInt(cellX);
            int y0 = Mathf.FloorToInt(cellY);
            float tx = Mathf.SmoothStep(0f, 1f, cellX - x0);
            float ty = Mathf.SmoothStep(0f, 1f, cellY - y0);
            int x1 = (x0 + 1) % cells;
            int y1 = (y0 + 1) % cells;

            float bottom = Mathf.Lerp(Hash(x0 % cells, y0 % cells, seed), Hash(x1, y0 % cells, seed), tx);
            float top = Mathf.Lerp(Hash(x0 % cells, y1, seed), Hash(x1, y1, seed), tx);
            return Mathf.Lerp(bottom, top, ty);
        }

        // A repeatable pseudo-random value from 0 to 1 for a pair of integers.
        private static float Hash(int x, int y, int seed)
        {
            unchecked
            {
                uint h = (uint)(x * 374761393 + y * 668265263 + seed * 144269507);
                h = (h ^ (h >> 13)) * 1274126177u;
                return ((h ^ (h >> 16)) & 0xFFFFFF) / 16777216f;
            }
        }
    }
}
