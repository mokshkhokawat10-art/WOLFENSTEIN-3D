using UnityEngine;

namespace Wolfenstein.Editor
{
    // More procedural surfaces, for the zones of the level and for the props and monsters in them. Like the first set,
    // each is drawn as colour plus a height field that becomes a normal map, at 128 pixels to a 3 m tile.
    // Surfaces meant to be tinted by their material are drawn in near-white greys.
    public static partial class TestLevelBuilder
    {
        private static readonly Color RustColour = new Color(0.42f, 0.2f, 0.09f);
        private static readonly Color HazardYellow = new Color(0.86f, 0.64f, 0.08f);
        private static readonly Color HazardBlack = new Color(0.07f, 0.07f, 0.07f);

        // Riveted steel plates between dark seams, with a line of rivets along every seam, blotches of rust that gather
        // toward the floor, and optionally a band of hazard stripes along the foot of the wall.
        private static Surface DrawSteelPanels(int seed, Color steel, Color seam, int panelWidth, int panelHeight, float rust,
            bool hazardBand)
        {
            const int seamWidth = 2;
            const int bandHeight = 20;
            var surface = new Surface(SurfaceSize);
            for (int y = 0; y < SurfaceSize; y++)
            {
                for (int x = 0; x < SurfaceSize; x++)
                {
                    float grain = Grain(x, y, SurfaceSize, seed);
                    if (hazardBand && y < bandHeight)
                    {
                        bool top = y >= bandHeight - 2;
                        Color stripe = (x + y) / 11 % 2 == 0 ? HazardYellow : HazardBlack;
                        surface.Set(x, y, (top ? seam : stripe) * (0.8f + 0.3f * grain) * Grime(y, SurfaceSize), top ? 0.2f : 0.7f);
                        continue;
                    }

                    int inX = x % panelWidth;
                    int inY = y % panelHeight;
                    if (inX < seamWidth || inY < seamWidth)
                    {
                        surface.Set(x, y, seam * (0.8f + 0.4f * grain) * Grime(y, SurfaceSize), 0.05f);
                        continue;
                    }

                    int fromLeft = inX - seamWidth;
                    int fromBottom = inY - seamWidth;
                    int fromRight = panelWidth - 1 - inX;
                    int fromTop = panelHeight - 1 - inY;
                    bool rivet = ((fromBottom is >= 3 and <= 4 || fromTop is >= 3 and <= 4) && inX % 16 is >= 7 and <= 8) ||
                                 ((fromLeft is >= 3 and <= 4 || fromRight is >= 3 and <= 4) && inY % 16 is >= 7 and <= 8);

                    float shade = (0.85f + 0.2f * Hash(x / panelWidth, y / panelHeight, seed)) * (0.86f + 0.28f * grain);
                    shade *= Bevel(fromLeft, fromBottom, panelWidth - seamWidth, panelHeight - seamWidth);
                    Color colour = steel * shade;

                    float lowness = 1f - (float)y / SurfaceSize;
                    float rustAmount = rust * Mathf.Clamp01(Noise(x, y, SurfaceSize, 8, seed + 5) * 1.8f - 0.75f + lowness * 0.5f);
                    colour = Color.Lerp(colour, RustColour * (0.7f + 0.5f * grain), rustAmount);
                    float height = 0.6f + 0.2f * EdgeRise(fromLeft, fromBottom, panelWidth - seamWidth, panelHeight - seamWidth) -
                                   0.15f * rustAmount;

                    if (rivet)
                    {
                        colour = Color.Lerp(steel * 1.35f, RustColour, rust * 0.5f);
                        height = 1f;
                    }

                    surface.Set(x, y, colour * Grime(y, SurfaceSize), height);
                }
            }

            return surface;
        }

        // Concrete blockwork painted two-tone, as in barracks: a dark lower band, a thin stripe, and pale paint above,
        // chipped here and there down to bare concrete.
        private static Surface DrawPaintedBlockwork(int seed, Color lower, Color upper, Color stripe)
        {
            const int blockWidth = 32;
            const int blockHeight = 16;
            const int joint = 2;
            const int dado = 46;

            var bare = new Color(0.45f, 0.44f, 0.42f);
            var surface = new Surface(SurfaceSize);
            for (int y = 0; y < SurfaceSize; y++)
            {
                int course = y / blockHeight;
                int inY = y % blockHeight;
                for (int x = 0; x < SurfaceSize; x++)
                {
                    int shifted = (x + course % 2 * (blockWidth / 2)) % SurfaceSize;
                    int inX = shifted % blockWidth;
                    float grain = Grain(x, y, SurfaceSize, seed);
                    bool isJoint = inX < joint || inY < joint;

                    Color paint = y < dado ? lower : y < dado + 4 ? stripe : upper;
                    float shade = isJoint ? 0.7f : (0.9f + 0.14f * Hash(shifted / blockWidth, course, seed)) * (0.9f + 0.2f * grain);
                    bool chipped = Noise(x, y, SurfaceSize, 16, seed + 3) > 0.74f && Hash(x, y, seed + 9) > 0.35f;
                    Color colour = chipped ? bare * (0.8f + 0.3f * grain) : paint * shade;
                    float height = isJoint ? 0.2f : 0.7f + 0.1f * grain - (chipped ? 0.25f : 0f);
                    surface.Set(x, y, colour * Grime(y, SurfaceSize), height);
                }
            }

            return surface;
        }

        // Dark wood panelling: raised panels in a frame below a chair rail, and tongue-and-groove boards above it.
        private static Surface DrawWoodPanelling(int seed, Color wood)
        {
            const int railTop = 58;
            const int railBottom = 52;
            const int panelWidth = 32;
            const int frame = 5;
            const int boardWidth = 16;

            var surface = new Surface(SurfaceSize);
            for (int y = 0; y < SurfaceSize; y++)
            {
                for (int x = 0; x < SurfaceSize; x++)
                {
                    float shade;
                    float height;
                    if (y >= railBottom && y < railTop)
                    {
                        // The chair rail: a rounded moulding catching the light along its top.
                        shade = y == railTop - 1 ? 1.3f : y == railBottom ? 0.7f : 1.05f;
                        shade *= WoodGrain(x, y, seed, vertical: false);
                        height = 1f;
                    }
                    else if (y >= railTop)
                    {
                        int inBoard = x % boardWidth;
                        shade = (inBoard == 0 ? 0.45f : inBoard == 1 ? 1.15f : 0.95f) *
                                (0.9f + 0.18f * Hash(x / boardWidth, 3, seed)) * WoodGrain(x, y, seed, vertical: true);
                        height = inBoard == 0 ? 0.3f : 0.7f;
                    }
                    else
                    {
                        int inPanel = x % panelWidth;
                        int fromEdge = Mathf.Min(Mathf.Min(inPanel, panelWidth - 1 - inPanel), Mathf.Min(y, railBottom - 1 - y));
                        bool inFrame = fromEdge < frame;
                        bool railPart = inFrame && (y < frame || y >= railBottom - frame);
                        shade = WoodGrain(x, y, seed, vertical: !railPart) * (inFrame ? 0.85f : 1f);
                        if (fromEdge == frame)
                            shade *= 0.6f;
                        else if (fromEdge == frame + 1)
                            shade *= 1.2f;
                        height = inFrame ? 0.6f : 0.8f;
                    }

                    surface.Set(x, y, wood * shade * Grime(y, SurfaceSize), height);
                }
            }

            return surface;
        }

        // Grain running along a board, near 1 so it only shades the wood's colour.
        private static float WoodGrain(int x, int y, int seed, bool vertical)
        {
            int across = vertical ? x : y;
            float warp = Noise(x, y, SurfaceSize, 4, seed + 8) * 10f;
            float rings = 0.5f + 0.5f * Mathf.Sin((across + warp) * 0.9f);
            return 0.78f + 0.18f * rings + 0.1f * Noise(x, y, SurfaceSize, 32, seed + 9);
        }

        // Big cast concrete panels between recessed joints, with the holes the formwork ties left and streaks of damp
        // running down from them.
        private static Surface DrawConcretePanels(int seed, Color concrete, Color seam)
        {
            const int panelSize = 64;
            const int joint = 3;
            var surface = new Surface(SurfaceSize);
            for (int y = 0; y < SurfaceSize; y++)
            {
                for (int x = 0; x < SurfaceSize; x++)
                {
                    int inX = x % panelSize;
                    int inY = y % panelSize;
                    float grain = Grain(x, y, SurfaceSize, seed);
                    if (inX < joint || inY < joint)
                    {
                        surface.Set(x, y, seam * (0.8f + 0.4f * grain), 0f);
                        continue;
                    }

                    bool tieHole = (inX - joint) % 20 is >= 9 and <= 11 && (inY - joint) % 30 is >= 14 and <= 16;
                    float streak = Mathf.Clamp01(Noise(x, y / 4, SurfaceSize, 16, seed + 4) * 2f - 1.1f);
                    float shade = (0.86f + 0.18f * Hash(x / panelSize, y / panelSize, seed)) * (0.8f + 0.36f * grain) * (1f - 0.35f * streak);
                    float height = 0.6f + 0.2f * EdgeRise(inX - joint, inY - joint, panelSize - joint, panelSize - joint) - 0.2f * grain;
                    if (tieHole)
                    {
                        shade = 0.25f;
                        height = 0.2f;
                    }

                    Pit(x, y, seed, ref shade, ref height);
                    surface.Set(x, y, concrete * shade * Grime(y, SurfaceSize), height);
                }
            }

            return surface;
        }

        // Diamond tread steel plate: raised lozenges in alternating directions, in plates with welded seams.
        private static Surface DrawTreadPlate(int seed, Color steel)
        {
            const int cell = 16;
            const int plate = 64;
            var surface = new Surface(SurfaceSize);
            for (int y = 0; y < SurfaceSize; y++)
            {
                for (int x = 0; x < SurfaceSize; x++)
                {
                    float grain = Grain(x, y, SurfaceSize, seed);
                    if (x % plate < 2 || y % plate < 2)
                    {
                        surface.Set(x, y, steel * (0.4f + 0.2f * grain), 0.1f);
                        continue;
                    }

                    bool rising = (x / cell + y / cell) % 2 == 0;
                    float u = x % cell - 7.5f;
                    float v = y % cell - 7.5f;
                    float along = rising ? u + v : u - v;
                    float across = rising ? u - v : u + v;
                    bool raised = Mathf.Abs(along) < 8.5f && Mathf.Abs(across) < 2.4f;

                    // Raised treads are worn bright, and the plate between them is duller and dirtier.
                    float wear = Noise(x, y, SurfaceSize, 8, seed + 2);
                    float shade = raised ? 1.15f + 0.2f * wear : 0.78f + 0.2f * grain;
                    surface.Set(x, y, steel * shade, raised ? 1f : 0.4f - 0.1f * grain);
                }
            }

            return surface;
        }

        // Poured concrete slabs with thin expansion joints, hairline cracks and dark stains.
        private static Surface DrawConcreteFloor(int seed, Color concrete, int slabSize)
        {
            var surface = new Surface(SurfaceSize);
            for (int y = 0; y < SurfaceSize; y++)
            {
                for (int x = 0; x < SurfaceSize; x++)
                {
                    float grain = Grain(x, y, SurfaceSize, seed);
                    if (x % slabSize < 1 || y % slabSize < 1)
                    {
                        surface.Set(x, y, concrete * 0.45f, 0.1f);
                        continue;
                    }

                    bool crack = Mathf.Abs(Noise(x, y, SurfaceSize, 8, seed + 7) - 0.5f) < 0.012f;
                    float stain = Mathf.Clamp01(Noise(x, y, SurfaceSize, 4, seed + 6) * 2.2f - 1.2f);
                    float shade = (0.86f + 0.12f * Hash(x / slabSize, y / slabSize, seed)) * (0.84f + 0.3f * grain) * (1f - 0.45f * stain);
                    float height = 0.6f - 0.15f * grain;
                    if (crack)
                    {
                        shade *= 0.45f;
                        height = 0.2f;
                    }

                    Pit(x, y, seed, ref shade, ref height);
                    surface.Set(x, y, concrete * shade, height);
                }
            }

            return surface;
        }

        // Floorboards running east-west, each row of boards joined end to end at its own places, with nail heads at the ends.
        private static Surface DrawPlanks(int seed, Color wood, int plankWidth)
        {
            var surface = new Surface(SurfaceSize);
            for (int y = 0; y < SurfaceSize; y++)
            {
                int row = y / plankWidth;
                int inRow = y % plankWidth;
                int offset = (int)(Hash(row, 1, seed) * SurfaceSize);
                for (int x = 0; x < SurfaceSize; x++)
                {
                    int along = (x + offset) % 64;
                    int board = (x + offset) / 64;
                    bool gap = inRow == 0 || along == 0;
                    bool nail = (along is 3 or 60) && (inRow == plankWidth / 3 || inRow == plankWidth * 2 / 3);

                    float shade = (0.8f + 0.28f * Hash(board, row, seed)) * WoodGrain(y, x + offset, seed, vertical: true);
                    if (inRow == 1)
                        shade *= 1.12f;
                    float height = 0.7f - 0.1f * Grain(x, y, SurfaceSize, seed);
                    if (gap)
                    {
                        shade = 0.22f;
                        height = 0f;
                    }
                    else if (nail)
                    {
                        shade = 0.4f;
                        height = 0.9f;
                    }

                    surface.Set(x, y, wood * shade, height);
                }
            }

            return surface;
        }

        // Square steel grating over darkness, with heavier bars framing each section.
        private static Surface DrawGrate(int seed, Color steel)
        {
            const int pitch = 8;
            const int bar = 3;
            const int section = 64;
            var surface = new Surface(SurfaceSize);
            for (int y = 0; y < SurfaceSize; y++)
            {
                for (int x = 0; x < SurfaceSize; x++)
                {
                    float grain = Grain(x, y, SurfaceSize, seed);
                    bool frame = x % section < 4 || y % section < 4;
                    bool onBar = x % pitch < bar || y % pitch < bar;
                    float rust = Mathf.Clamp01(Noise(x, y, SurfaceSize, 8, seed + 3) * 2f - 1f);
                    if (!frame && !onBar)
                    {
                        surface.Set(x, y, steel * 0.05f, 0f);
                        continue;
                    }

                    float shade = (frame ? 1.05f : 0.95f) * (0.82f + 0.3f * grain);
                    Color colour = Color.Lerp(steel * shade, RustColour * (0.8f + 0.3f * grain), rust * 0.6f);
                    surface.Set(x, y, colour, frame ? 1f : 0.75f);
                }
            }

            return surface;
        }

        // Thick carpet: a soft pile with a diamond lattice woven in, worn paler along a path.
        private static Surface DrawCarpet(int seed, Color pile, Color thread)
        {
            const int lattice = 32;
            var surface = new Surface(SurfaceSize);
            for (int y = 0; y < SurfaceSize; y++)
            {
                for (int x = 0; x < SurfaceSize; x++)
                {
                    float fibre = 0.85f + 0.25f * Hash(x, y, seed + 4) * Hash(x / 2, y / 2, seed + 5);
                    int u = x % lattice;
                    int v = y % lattice;
                    bool line = Mathf.Abs(Mathf.Abs(u - lattice / 2) + Mathf.Abs(v - lattice / 2) - lattice / 4) < 1;
                    bool knot = Mathf.Abs(u - lattice / 2) + Mathf.Abs(v - lattice / 2) < 3;
                    float wear = Noise(x, y, SurfaceSize, 4, seed + 6);
                    Color colour = line || knot ? thread : pile;
                    colour = Color.Lerp(colour * fibre, colour * 1.25f, Mathf.Clamp01(wear * 1.6f - 0.9f));
                    surface.Set(x, y, colour, 0.5f + 0.2f * fibre + (line ? 0.1f : 0f));
                }
            }

            return surface;
        }

        // Worn square tiles in two colours laid as a checkerboard, grimy along the grout.
        private static Surface DrawCheckerTiles(int seed, Color light, Color dark)
        {
            const int tile = 32;
            var surface = new Surface(SurfaceSize);
            for (int y = 0; y < SurfaceSize; y++)
            {
                for (int x = 0; x < SurfaceSize; x++)
                {
                    float grain = Grain(x, y, SurfaceSize, seed);
                    int inX = x % tile;
                    int inY = y % tile;
                    if (inX < 1 || inY < 1)
                    {
                        surface.Set(x, y, dark * 0.5f, 0.1f);
                        continue;
                    }

                    Color colour = (x / tile + y / tile) % 2 == 0 ? light : dark;
                    float shade = (0.9f + 0.12f * Hash(x / tile, y / tile, seed)) * (0.88f + 0.22f * grain);
                    shade *= Bevel(inX - 1, inY - 1, tile - 1, tile - 1);
                    float height = 0.6f + 0.2f * EdgeRise(inX - 1, inY - 1, tile - 1, tile - 1);
                    Pit(x, y, seed, ref shade, ref height);
                    surface.Set(x, y, colour * shade, height);
                }
            }

            return surface;
        }

        // A wooden crate face: a frame of boards around a panel of planks, with a diagonal brace across it.
        private static Surface DrawCrateFace(int seed)
        {
            const int frame = 16;
            var surface = new Surface(SurfaceSize);
            for (int y = 0; y < SurfaceSize; y++)
            {
                for (int x = 0; x < SurfaceSize; x++)
                {
                    int fromEdge = Mathf.Min(Mathf.Min(x, SurfaceSize - 1 - x), Mathf.Min(y, SurfaceSize - 1 - y));
                    bool inFrame = fromEdge < frame;
                    bool brace = !inFrame && Mathf.Abs(x - y) < 9;
                    bool vertical = inFrame ? x < frame || x >= SurfaceSize - frame : !brace;

                    float shade = WoodGrain(x, y, seed, vertical) * (0.9f + 0.15f * Hash(x / 24, y / 24, seed));
                    float height = inFrame || brace ? 0.9f : 0.55f;
                    if (!inFrame && !brace && x % 24 == 0)
                    {
                        shade *= 0.45f;
                        height = 0.3f;
                    }
                    else if (fromEdge == frame || (!inFrame && Mathf.Abs(x - y) == 9))
                    {
                        shade *= 0.6f;
                    }

                    bool nail = inFrame && fromEdge is >= 6 and <= 8 && (x % 32 is >= 14 and <= 16 || y % 32 is >= 14 and <= 16) &&
                                (x < frame || x >= SurfaceSize - frame) != (y < frame || y >= SurfaceSize - frame);
                    if (nail)
                    {
                        shade = 0.35f;
                        height = 1f;
                    }

                    surface.Set(x, y, Color.white * shade, height);
                }
            }

            return surface;
        }

        // Battered armour plate with rivets round the edge and scratches across it, near white for a material to tint.
        private static Surface DrawArmourPlate(int seed)
        {
            const int plate = 64;
            var surface = new Surface(SurfaceSize);
            for (int y = 0; y < SurfaceSize; y++)
            {
                for (int x = 0; x < SurfaceSize; x++)
                {
                    int inX = x % plate;
                    int inY = y % plate;
                    int fromEdge = Mathf.Min(Mathf.Min(inX, plate - 1 - inX), Mathf.Min(inY, plate - 1 - inY));
                    bool rivet = fromEdge is >= 4 and <= 6 && (inX % 12 is >= 5 and <= 7 || inY % 12 is >= 5 and <= 7) &&
                                 (inX < 8 || inX > plate - 9) != (inY < 8 || inY > plate - 9);
                    bool scratch = Mathf.Abs((x * 3 + y * 7) % 41 - 20) < 1 && Hash(x / 8, y / 8, seed) > 0.6f;
                    float shade = (0.8f + 0.3f * Grain(x, y, SurfaceSize, seed)) * (fromEdge < 2 ? 0.6f : 1f);
                    float height = fromEdge < 2 ? 0.3f : 0.7f;
                    if (rivet)
                    {
                        shade = 1.3f;
                        height = 1f;
                    }
                    else if (scratch)
                    {
                        shade *= 1.35f;
                        height -= 0.15f;
                    }

                    surface.Set(x, y, Color.white * shade, height);
                }
            }

            return surface;
        }

        // Yellow and black warning stripes on the diagonal, for skirting, door frames and floor markings.
        private static Texture2D GenerateHazardTexture()
        {
            var pixels = new Color[SurfaceSize * SurfaceSize];
            for (int y = 0; y < SurfaceSize; y++)
            {
                for (int x = 0; x < SurfaceSize; x++)
                {
                    float grain = Grain(x, y, SurfaceSize, 21);
                    Color stripe = (x + y) % 32 < 16 ? HazardYellow : HazardBlack;
                    pixels[y * SurfaceSize + x] = Opaque(stripe * (0.75f + 0.35f * grain));
                }
            }

            return CreateTexture(pixels, SurfaceSize);
        }

        // A soft white dot for particles: bright in the middle, fading to nothing at the edge.
        private static Texture2D GenerateSoftDotTexture()
        {
            const int size = 32;
            var centre = new Vector2(size - 1, size - 1) * 0.5f;
            var pixels = new Color[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float radius = Vector2.Distance(new Vector2(x, y), centre) / (size * 0.5f);
                    pixels[y * size + x] = new Color(1f, 1f, 1f, Mathf.Pow(Mathf.Clamp01(1f - radius), 1.6f));
                }
            }

            return CreateTexture(pixels, size);
        }

        // A thin bright ring that fades toward its inside, for shockwaves and summoning circles lying on the floor.
        private static Texture2D GenerateRingTexture()
        {
            const int size = 64;
            var centre = new Vector2(size - 1, size - 1) * 0.5f;
            var pixels = new Color[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float radius = Vector2.Distance(new Vector2(x, y), centre) / (size * 0.5f);
                    float rim = Mathf.Clamp01(1f - Mathf.Abs(radius - 0.85f) / 0.12f);
                    float inner = radius < 0.85f ? Mathf.Pow(radius / 0.85f, 4f) * 0.35f : 0f;
                    pixels[y * size + x] = new Color(1f, 1f, 1f, Mathf.Clamp01(rim + inner));
                }
            }

            return CreateTexture(pixels, size);
        }

        // A splash of blood for the floor or a wall: a ragged pool with droplets flung around it.
        private static Texture2D GenerateBloodTexture()
        {
            const int size = 64;
            var centre = new Vector2(size - 1, size - 1) * 0.5f;
            var pixels = new Color[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    Vector2 offset = new Vector2(x, y) - centre;
                    float angle = Mathf.Atan2(offset.y, offset.x);
                    float edge = 0.45f + 0.12f * Mathf.Sin(angle * 5f + 1f) + 0.08f * Mathf.Sin(angle * 11f);
                    float radius = offset.magnitude / (size * 0.5f);
                    bool pool = radius < edge;
                    bool droplet = !pool && radius < 0.95f && Hash(x / 3, y / 3, 44) > 0.93f;
                    float shade = 0.7f + 0.3f * Noise(x, y, size, 8, 45);
                    pixels[y * size + x] = pool || droplet ? new Color(shade, shade, shade, 1f) : Color.clear;
                }
            }

            return CreateTexture(pixels, size);
        }

        // Cracked plaster: a jagged hole broken through the wall's surface down to the brick, with cracks spreading out.
        private static Texture2D GenerateCrackedWallTexture()
        {
            const int size = 64;
            var centre = new Vector2(size - 1, size - 1) * 0.5f;
            var pixels = new Color[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    Vector2 offset = new Vector2(x, y) - centre;
                    float angle = Mathf.Atan2(offset.y, offset.x);
                    float radius = offset.magnitude / (size * 0.5f);
                    float hole = 0.42f + 0.1f * Mathf.Sin(angle * 7f) + 0.06f * Mathf.Sin(angle * 13f + 2f);
                    bool crack = radius < 0.98f && Mathf.Abs(Mathf.Sin(angle * 6f + radius * 3f)) < 0.05f;
                    if (radius < hole)
                    {
                        // Brick showing through the hole, in running courses.
                        bool mortar = y % 8 == 0 || (x + y / 8 % 2 * 8) % 16 == 0;
                        float shade = mortar ? 0.12f : 0.3f + 0.1f * Hash(x / 16, y / 8, 46);
                        pixels[y * size + x] = new Color(shade * 1.4f, shade * 0.75f, shade * 0.6f, 1f);
                    }
                    else if (radius < hole + 0.06f || crack)
                    {
                        pixels[y * size + x] = new Color(0.08f, 0.07f, 0.06f, 1f);
                    }
                    else
                    {
                        pixels[y * size + x] = Color.clear;
                    }
                }
            }

            return CreateTexture(pixels, size);
        }
    }
}
