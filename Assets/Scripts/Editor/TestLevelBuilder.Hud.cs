using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using Wolfenstein.Combat;
using Wolfenstein.Core;
using Wolfenstein.Pickups;
using Wolfenstein.Player;
using Wolfenstein.UI;
using Wolfenstein.Waves;
using Wolfenstein.Weapons;
using Wolfenstein.World;
using Object = UnityEngine.Object;

namespace Wolfenstein.Editor
{
    // The heads-up display: crosshair, a status bar with health, score and ammo panels, the damage and pickup flashes,
    // the death screen, the arena's wave panel and banner, the level's objective panel and event line, and the end-of-level
    // tally, all lettered in a generated pixel font.
    public static partial class TestLevelBuilder
    {
        private const string HudPrefabPath = "Assets/Prefabs/HUD.prefab";
        private const string FontFolder = "Assets/Fonts";
        private const string PixelFontName = "HudPixel";

        // Text sizes are whole multiples of the font's 7-pixel letters, so at 1080p every font pixel is the same size.
        private const int GlyphWidth = 5;
        private const int GlyphHeight = 7;
        private const int CaptionSize = 21;
        private const int ValueSize = 56;
        private const int TitleSize = 112;
        private const int PromptSize = 28;

        private const float StatusBarHeight = 150f;

        private static readonly Vector2 HudReferenceResolution = new Vector2(1920f, 1080f);
        private static readonly Vector2 ScreenCentre = new Vector2(0.5f, 0.5f);

        // The navy steel and pale blue of the original game's status bar.
        private static readonly Color BarColour = new Color(0.07f, 0.09f, 0.21f);
        private static readonly Color BarHighlight = new Color(0.38f, 0.46f, 0.82f);
        private static readonly Color BarShadow = new Color(0.015f, 0.02f, 0.06f);
        private static readonly Color InsetColour = new Color(0.02f, 0.025f, 0.07f);
        private static readonly Color CaptionColour = new Color(0.62f, 0.7f, 1f);

        // A 5 by 7 pixel font, one row per line from the top: '#' is ink.
        private static readonly Dictionary<char, string> PixelGlyphs = new Dictionary<char, string>
        {
            ['0'] = ".###.|#...#|#..##|#.#.#|##..#|#...#|.###.",
            ['1'] = "..#..|.##..|..#..|..#..|..#..|..#..|.###.",
            ['2'] = ".###.|#...#|....#|...#.|..#..|.#...|#####",
            ['3'] = "#####|...#.|..#..|...#.|....#|#...#|.###.",
            ['4'] = "...#.|..##.|.#.#.|#..#.|#####|...#.|...#.",
            ['5'] = "#####|#....|####.|....#|....#|#...#|.###.",
            ['6'] = "..##.|.#...|#....|####.|#...#|#...#|.###.",
            ['7'] = "#####|....#|...#.|..#..|.#...|.#...|.#...",
            ['8'] = ".###.|#...#|#...#|.###.|#...#|#...#|.###.",
            ['9'] = ".###.|#...#|#...#|.####|....#|...#.|.##..",
            ['%'] = "##...|##..#|...#.|..#..|.#...|#..##|...##",
            ['A'] = ".###.|#...#|#...#|#####|#...#|#...#|#...#",
            ['B'] = "####.|#...#|#...#|####.|#...#|#...#|####.",
            ['C'] = ".###.|#...#|#....|#....|#....|#...#|.###.",
            ['D'] = "####.|#...#|#...#|#...#|#...#|#...#|####.",
            ['E'] = "#####|#....|#....|####.|#....|#....|#####",
            ['F'] = "#####|#....|#....|####.|#....|#....|#....",
            ['G'] = ".###.|#...#|#....|#.###|#...#|#...#|.####",
            ['H'] = "#...#|#...#|#...#|#####|#...#|#...#|#...#",
            ['I'] = ".###.|..#..|..#..|..#..|..#..|..#..|.###.",
            ['J'] = "..###|...#.|...#.|...#.|...#.|#..#.|.##..",
            ['K'] = "#...#|#..#.|#.#..|##...|#.#..|#..#.|#...#",
            ['L'] = "#....|#....|#....|#....|#....|#....|#####",
            ['M'] = "#...#|##.##|#.#.#|#.#.#|#...#|#...#|#...#",
            ['N'] = "#...#|#...#|##..#|#.#.#|#..##|#...#|#...#",
            ['O'] = ".###.|#...#|#...#|#...#|#...#|#...#|.###.",
            ['P'] = "####.|#...#|#...#|####.|#....|#....|#....",
            ['Q'] = ".###.|#...#|#...#|#...#|#.#.#|#..#.|.##.#",
            ['R'] = "####.|#...#|#...#|####.|#.#..|#..#.|#...#",
            ['S'] = ".####|#....|#....|.###.|....#|....#|####.",
            ['T'] = "#####|..#..|..#..|..#..|..#..|..#..|..#..",
            ['U'] = "#...#|#...#|#...#|#...#|#...#|#...#|.###.",
            ['V'] = "#...#|#...#|#...#|#...#|#...#|.#.#.|..#..",
            ['W'] = "#...#|#...#|#...#|#.#.#|#.#.#|#.#.#|.#.#.",
            ['X'] = "#...#|#...#|.#.#.|..#..|.#.#.|#...#|#...#",
            ['Y'] = "#...#|#...#|.#.#.|..#..|..#..|..#..|..#..",
            ['Z'] = "#####|....#|...#.|..#..|.#...|#....|#####",
            ['!'] = "..#..|..#..|..#..|..#..|..#..|.....|..#..",
            ['.'] = ".....|.....|.....|.....|.....|.....|..#..",
            [':'] = ".....|..#..|.....|.....|.....|..#..|.....",
            ['-'] = ".....|.....|.....|#####|.....|.....|.....",
            ['/'] = "....#|...#.|...#.|..#..|.#...|.#...|#....",
            [','] = ".....|.....|.....|.....|.....|..#..|.#...",
            ['+'] = ".....|..#..|..#..|#####|..#..|..#..|.....",
            ['('] = "...#.|..#..|.#...|.#...|.#...|..#..|...#.",
            [')'] = ".#...|..#..|...#.|...#.|...#.|..#..|.#...",
            ['\''] = "..#..|..#..|.#...|.....|.....|.....|.....",
            ['?'] = ".###.|#...#|....#|...#.|..#..|.....|..#..",
        };

        private static void InstantiateHud(PlayerController player, LevelManager levelManager, WaveManager waves, Progress progress)
        {
            if (hudPrefab == null)
                hudPrefab = BuildHudPrefab();
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(hudPrefab);
            SetFields(instance.GetComponent<PlayerHud>(),
                ("health", player.GetComponent<Health>()),
                ("ammo", player.GetComponent<AmmoSupply>()),
                ("score", player.GetComponent<Score>()),
                ("pickups", player.GetComponent<PickupCollector>()),
                ("level", levelManager));
            SetFields(instance.GetComponent<WaveHud>(), ("waves", waves));
            SetFields(instance.GetComponent<LevelHud>(), ("level", levelManager));
            SetFields(instance.GetComponent<TallyScreen>(), ("level", levelManager));
            if (progress == null)
                return;

            SetFields(instance.GetComponent<LevelHud>(),
                ("objectives", progress.Objectives), ("access", progress.Access), ("exit", progress.Exit));
            SetFields(instance.GetComponent<TallyScreen>(), ("statistics", progress.Statistics));
        }

        private static GameObject BuildHudPrefab()
        {
            var root = new GameObject("HUD");
            var canvas = root.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;

            // Keeps the pixel font and the thin bevels on whole screen pixels.
            canvas.pixelPerfect = true;

            // Scaling by screen height keeps the HUD the same relative size at any resolution.
            var scaler = root.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = HudReferenceResolution;
            scaler.matchWidthOrHeight = 1f;

            var canvasRect = (RectTransform)root.transform;
            Font font = GetOrCreatePixelFont();

            GameObject crosshair = CreateCrosshair(canvasRect);
            RectTransform bar = CreateStatusBar(canvasRect);
            Text healthText = CreateReadout(bar, "Health", "HEALTH", 0f, 320f, font);
            Text scoreText = CreateReadout(bar, "Score", "SCORE", 0.5f, 460f, font);
            Text ammoText = CreateReadout(bar, "Ammo", "AMMO", 1f, 320f, font);
            Image damageFlash = AddImage(CreateStretchedRect(canvasRect, "Damage Flash"), new Color(0.8f, 0f, 0f, 0f));
            Image pickupFlash = AddImage(CreateStretchedRect(canvasRect, "Pickup Flash"), new Color(1f, 0.82f, 0.3f, 0f));
            GameObject wavePanel = CreateWavePanel(canvasRect, font, out Text waveText, out Text enemiesText);
            RectTransform banner = CreateWaveBanner(canvasRect, font, out Text bannerTitle, out Text bannerSubtitle);
            GameObject objectivePanel = CreateObjectivePanel(canvasRect, font, out Text objectiveCaption, out Text objectiveText);
            RectTransform message = CreateLevelMessage(canvasRect, font, out Text messageTitle, out Text messageSubtitle);
            GameObject deathScreen = CreateDeathScreen(canvasRect, font, out GameObject restartPrompt);
            GameObject tallyScreen = CreateTallyScreen(canvasRect, font, out Text tallyTitle, out Text tallySubtitle,
                out (TallyStat stat, GameObject root, Text value)[] tallyRows, out GameObject tallyPrompt);

            SetFields(root.AddComponent<PlayerHud>(),
                ("healthText", healthText),
                ("ammoText", ammoText),
                ("scoreText", scoreText),
                ("damageFlash", damageFlash),
                ("pickupFlash", pickupFlash),
                ("crosshair", crosshair),
                ("deathScreen", deathScreen),
                ("restartPrompt", restartPrompt));

            SetFields(root.AddComponent<WaveHud>(),
                ("panel", wavePanel),
                ("waveText", waveText),
                ("enemiesText", enemiesText),
                ("banner", banner),
                ("bannerGroup", banner.GetComponent<CanvasGroup>()),
                ("bannerTitle", bannerTitle),
                ("bannerSubtitle", bannerSubtitle));

            SetFields(root.AddComponent<LevelHud>(),
                ("objectivePanel", objectivePanel),
                ("objectiveCaption", objectiveCaption),
                ("objectiveText", objectiveText),
                ("message", message.gameObject),
                ("messageGroup", message.GetComponent<CanvasGroup>()),
                ("messageTitle", messageTitle),
                ("messageSubtitle", messageSubtitle));

            var tally = root.AddComponent<TallyScreen>();
            var serializedTally = new SerializedObject(tally);
            Set(serializedTally,
                ("screen", tallyScreen),
                ("title", tallyTitle),
                ("subtitle", tallySubtitle),
                ("prompt", tallyPrompt));
            SerializedProperty rows = serializedTally.FindProperty("rows");
            rows.arraySize = tallyRows.Length;
            for (int i = 0; i < tallyRows.Length; i++)
            {
                SerializedProperty row = rows.GetArrayElementAtIndex(i);
                row.FindPropertyRelative("stat").intValue = (int)tallyRows[i].stat;
                row.FindPropertyRelative("root").objectReferenceValue = tallyRows[i].root;
                row.FindPropertyRelative("value").objectReferenceValue = tallyRows[i].value;
            }

            serializedTally.ApplyModifiedPropertiesWithoutUndo();

            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, HudPrefabPath);
            Object.DestroyImmediate(root);
            return prefab;
        }

        // Four short bars around a small gap, so the aim point itself stays visible. Even sizes keep them on whole pixels.
        private static GameObject CreateCrosshair(RectTransform canvas)
        {
            const float gap = 6f;
            const float length = 10f;
            const float thickness = 4f;

            RectTransform crosshair = CreateRect(canvas, "Crosshair", ScreenCentre, ScreenCentre, Vector2.zero, Vector2.zero);
            foreach (Vector2 direction in new[] { Vector2.up, Vector2.down, Vector2.left, Vector2.right })
            {
                Vector2 size = direction.x == 0f ? new Vector2(thickness, length) : new Vector2(length, thickness);
                RectTransform bar = CreateRect(crosshair, "Bar", ScreenCentre, ScreenCentre, direction * (gap + length * 0.5f), size);
                AddImage(bar, new Color(1f, 1f, 1f, 0.9f));
                bar.gameObject.AddComponent<Outline>().effectColor = new Color(0f, 0f, 0f, 0.7f);
            }

            return crosshair.gameObject;
        }

        // A full-width status bar along the bottom of the screen, as in the original game, with a bright bevelled top edge.
        private static RectTransform CreateStatusBar(RectTransform canvas)
        {
            RectTransform bar = CreateRect(canvas, "Status Bar", Vector2.zero, new Vector2(1f, 0f), Vector2.zero, new Vector2(0f, StatusBarHeight));
            bar.pivot = new Vector2(0.5f, 0f);
            AddImage(bar, BarColour);
            AddImage(CreateEdge(bar, "Top Edge", Vector2.up, 4f, 0f), BarHighlight);
            AddImage(CreateEdge(bar, "Top Edge Shadow", Vector2.up, 3f, 4f), BarShadow);
            AddImage(CreateEdge(bar, "Bottom Edge", Vector2.down, 3f, 0f), BarShadow);
            return bar;
        }

        // A recessed panel in the status bar with a small caption above a large value. Returns the value text.
        // Along is how far across the bar it sits, from 0 at the left end to 1 at the right; panels at the ends keep clear of them.
        private static Text CreateReadout(RectTransform bar, string readoutName, string caption, float along, float width, Font font)
        {
            const float height = 118f;
            const float endMargin = 40f;
            const float bevel = 3f;

            var anchor = new Vector2(along, 0.5f);
            var inset = new Vector2((0.5f - along) * 2f * endMargin, -4f);
            RectTransform panel = CreateRect(bar, $"{readoutName} Panel", anchor, anchor, inset, new Vector2(width, height));
            panel.pivot = anchor;
            AddImage(panel, InsetColour);

            // Shadowed along the top and left and lit along the bottom and right, so the panel looks sunk into the bar.
            AddImage(CreateEdge(panel, "Top Edge", Vector2.up, bevel, 0f), BarShadow);
            AddImage(CreateEdge(panel, "Left Edge", Vector2.left, bevel, 0f), BarShadow);
            AddImage(CreateEdge(panel, "Bottom Edge", Vector2.down, bevel, 0f), BarHighlight * 0.8f);
            AddImage(CreateEdge(panel, "Right Edge", Vector2.right, bevel, 0f), BarHighlight * 0.8f);

            RectTransform captionRect = CreateRect(panel, "Caption", new Vector2(0f, 1f), Vector2.one, new Vector2(0f, -24f), new Vector2(0f, 30f));
            AddText(captionRect, font, caption, CaptionSize, CaptionColour);

            RectTransform valueRect = CreateStretchedRect(panel, "Value");
            valueRect.offsetMin = new Vector2(0f, 8f);
            valueRect.offsetMax = new Vector2(0f, -40f);
            return AddText(valueRect, font, string.Empty, ValueSize, Color.white);
        }

        // A bar at the top of the screen, like a second status bar, with the wave counter and the monsters left.
        // It starts hidden; the wave display shows it once the first wave is announced.
        private static GameObject CreateWavePanel(RectTransform canvas, Font font, out Text waveText, out Text enemiesText)
        {
            const float height = 118f;
            var top = new Vector2(0.5f, 1f);
            RectTransform panel = CreateRect(canvas, "Wave Panel", top, top, new Vector2(0f, -16f), new Vector2(700f, height));
            panel.pivot = top;
            AddImage(panel, BarColour);
            AddImage(CreateEdge(panel, "Bottom Edge", Vector2.down, 4f, 0f), BarHighlight);
            AddImage(CreateEdge(panel, "Top Edge", Vector2.up, 3f, 0f), BarShadow);

            waveText = CreateReadout(panel, "Wave", "WAVE", 0f, 300f, font);
            enemiesText = CreateReadout(panel, "Enemies", "ENEMIES", 1f, 300f, font);
            foreach (Text value in new[] { waveText, enemiesText })
            {
                var panelRect = (RectTransform)value.transform.parent;
                panelRect.sizeDelta = new Vector2(300f, height - 20f);
                panelRect.anchoredPosition = new Vector2(panelRect.anchoredPosition.x * 0.5f, 0f);
                value.fontSize = ValueSize * 3 / 4;
            }

            enemiesText.color = new Color(1f, 0.45f, 0.35f);
            panel.gameObject.SetActive(false);
            return panel.gameObject;
        }

        // A band across the upper middle of the screen with a big title and a line under it, for the wave announcements.
        private static RectTransform CreateWaveBanner(RectTransform canvas, Font font, out Text title, out Text subtitle)
        {
            RectTransform banner = CreateRect(canvas, "Wave Banner", new Vector2(0f, 0.5f), new Vector2(1f, 0.5f), new Vector2(0f, 190f),
                new Vector2(0f, 230f));
            AddImage(banner, new Color(0f, 0f, 0f, 0.55f));
            AddImage(CreateEdge(banner, "Top Edge", Vector2.up, 4f, 0f), new Color(0.5f, 0.05f, 0.03f, 0.9f));
            AddImage(CreateEdge(banner, "Bottom Edge", Vector2.down, 4f, 0f), new Color(0.5f, 0.05f, 0.03f, 0.9f));
            banner.gameObject.AddComponent<CanvasGroup>().blocksRaycasts = false;

            RectTransform titleRect = CreateRect(banner, "Title", ScreenCentre, ScreenCentre, new Vector2(0f, 30f), new Vector2(1600f, 130f));
            title = AddText(titleRect, font, string.Empty, TitleSize, Color.white);
            RectTransform subtitleRect = CreateRect(banner, "Subtitle", ScreenCentre, ScreenCentre, new Vector2(0f, -70f), new Vector2(1600f, 50f));
            subtitle = AddText(subtitleRect, font, string.Empty, PromptSize * 3 / 2, new Color(0.95f, 0.9f, 0.85f));

            banner.gameObject.SetActive(false);
            return banner;
        }

        // The prompt starts hidden; the HUD shows it once the restart input is accepted.
        private static GameObject CreateDeathScreen(RectTransform canvas, Font font, out GameObject restartPrompt)
        {
            RectTransform screen = CreateStretchedRect(canvas, "Death Screen");
            AddImage(screen, new Color(0.4f, 0f, 0f, 0.62f));

            RectTransform title = CreateRect(screen, "Title", ScreenCentre, ScreenCentre, new Vector2(0f, 50f), new Vector2(1000f, 140f));
            AddText(title, font, "PLAYER DOWN", TitleSize, new Color(1f, 0.92f, 0.88f));

            RectTransform prompt = CreateRect(screen, "Prompt", ScreenCentre, ScreenCentre, new Vector2(0f, -70f), new Vector2(1000f, 50f));
            AddText(prompt, font, "PRESS FIRE TO RESTART LEVEL", PromptSize, CaptionColour);
            restartPrompt = prompt.gameObject;
            restartPrompt.SetActive(false);

            screen.gameObject.SetActive(false);
            return screen.gameObject;
        }

        // A small panel in the top left corner, in the steel of the status bar: a caption over the current objective.
        // It starts hidden; the level display shows it once the level has objectives.
        private static GameObject CreateObjectivePanel(RectTransform canvas, Font font, out Text caption, out Text objective)
        {
            var corner = new Vector2(0f, 1f);
            RectTransform panel = CreateRect(canvas, "Objective Panel", corner, corner, new Vector2(24f, -24f), new Vector2(600f, 96f));
            panel.pivot = corner;
            AddImage(panel, new Color(BarColour.r, BarColour.g, BarColour.b, 0.88f));
            AddImage(CreateEdge(panel, "Bottom Edge", Vector2.down, 3f, 0f), BarHighlight);
            AddImage(CreateEdge(panel, "Left Edge", Vector2.left, 3f, 0f), BarHighlight);

            RectTransform captionRect = CreateRect(panel, "Caption", corner, Vector2.one, new Vector2(20f, -20f), new Vector2(-40f, 28f));
            captionRect.pivot = corner;
            caption = AddText(captionRect, font, "OBJECTIVE", CaptionSize, CaptionColour);
            caption.alignment = TextAnchor.MiddleLeft;

            RectTransform objectiveRect = CreateRect(panel, "Objective", corner, Vector2.one, new Vector2(20f, -50f), new Vector2(-40f, 36f));
            objectiveRect.pivot = corner;
            objective = AddText(objectiveRect, font, string.Empty, PromptSize, Color.white);
            objective.alignment = TextAnchor.MiddleLeft;

            panel.gameObject.SetActive(false);
            return panel.gameObject;
        }

        // A narrow band below the crosshair for the level's events, such as a locked door or the exit opening. It fades away.
        private static RectTransform CreateLevelMessage(RectTransform canvas, Font font, out Text title, out Text subtitle)
        {
            RectTransform message = CreateRect(canvas, "Level Message", ScreenCentre, ScreenCentre, new Vector2(0f, -210f), new Vector2(1300f, 118f));
            AddImage(message, new Color(0f, 0f, 0f, 0.5f));
            message.gameObject.AddComponent<CanvasGroup>().blocksRaycasts = false;

            title = AddText(CreateRect(message, "Title", ScreenCentre, ScreenCentre, new Vector2(0f, 20f), new Vector2(1280f, 56f)), font,
                string.Empty, PromptSize * 3 / 2, Color.white);
            subtitle = AddText(CreateRect(message, "Subtitle", ScreenCentre, ScreenCentre, new Vector2(0f, -30f), new Vector2(1280f, 36f)), font,
                string.Empty, PromptSize, new Color(0.95f, 0.9f, 0.85f));

            message.gameObject.SetActive(false);
            return message;
        }

        // The end-of-level results over the whole screen: a title, the level's name, a line for each figure with its caption
        // on the left and its value on the right, and the prompt to continue. It starts hidden.
        private static GameObject CreateTallyScreen(RectTransform canvas, Font font, out Text title, out Text subtitle,
            out (TallyStat stat, GameObject root, Text value)[] rows, out GameObject prompt)
        {
            (TallyStat stat, string caption)[] lines =
            {
                (TallyStat.Time, "TIME"),
                (TallyStat.Kills, "KILLS"),
                (TallyStat.Treasure, "TREASURE"),
                (TallyStat.Accuracy, "ACCURACY"),
                (TallyStat.Score, "SCORE"),
                (TallyStat.TotalScore, "TOTAL SCORE"),
            };

            RectTransform screen = CreateStretchedRect(canvas, "Tally Screen");
            AddImage(screen, new Color(0.02f, 0.03f, 0.09f, 0.94f));

            RectTransform band = CreateRect(screen, "Title Band", new Vector2(0f, 0.5f), new Vector2(1f, 0.5f), new Vector2(0f, 330f), new Vector2(0f, 190f));
            AddImage(band, BarColour);
            AddImage(CreateEdge(band, "Top Edge", Vector2.up, 4f, 0f), BarHighlight);
            AddImage(CreateEdge(band, "Bottom Edge", Vector2.down, 4f, 0f), BarHighlight);
            title = AddText(CreateRect(band, "Title", ScreenCentre, ScreenCentre, new Vector2(0f, 25f), new Vector2(1600f, 120f)), font,
                "LEVEL COMPLETE", TitleSize, new Color(1f, 0.82f, 0.3f));
            subtitle = AddText(CreateRect(band, "Subtitle", ScreenCentre, ScreenCentre, new Vector2(0f, -58f), new Vector2(1600f, 40f)), font,
                string.Empty, PromptSize, CaptionColour);

            const float rowHeight = 64f;
            rows = new (TallyStat, GameObject, Text)[lines.Length];
            for (int i = 0; i < lines.Length; i++)
            {
                RectTransform row = CreateRect(screen, $"{lines[i].caption} Row", ScreenCentre, ScreenCentre,
                    new Vector2(0f, 150f - i * rowHeight), new Vector2(1100f, rowHeight));
                Text caption = AddText(CreateRect(row, "Caption", new Vector2(0f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(-30f, 0f), new Vector2(0f, rowHeight)),
                    font, lines[i].caption, PromptSize * 3 / 2, CaptionColour);
                caption.alignment = TextAnchor.MiddleRight;
                Text value = AddText(CreateRect(row, "Value", new Vector2(0.5f, 0.5f), new Vector2(1f, 0.5f), new Vector2(30f, 0f), new Vector2(0f, rowHeight)),
                    font, string.Empty, PromptSize * 3 / 2, Color.white);
                value.alignment = TextAnchor.MiddleLeft;
                rows[i] = (lines[i].stat, row.gameObject, value);
            }

            RectTransform promptRect = CreateRect(screen, "Prompt", ScreenCentre, ScreenCentre, new Vector2(0f, -330f), new Vector2(1600f, 50f));
            AddText(promptRect, font, "PRESS ENTER TO CONTINUE", PromptSize, Color.white);
            prompt = promptRect.gameObject;
            prompt.SetActive(false);

            screen.gameObject.SetActive(false);
            return screen.gameObject;
        }

        // A strip along one side of a rect, on the inside of it, a given distance in from that side.
        private static RectTransform CreateEdge(RectTransform parent, string edgeName, Vector2 side, float thickness, float inset)
        {
            Vector2 anchorMin = side.x > 0f ? new Vector2(1f, 0f) : side.y > 0f ? new Vector2(0f, 1f) : Vector2.zero;
            Vector2 anchorMax = side.x < 0f ? new Vector2(0f, 1f) : side.y < 0f ? new Vector2(1f, 0f) : Vector2.one;
            Vector2 size = side.x != 0f ? new Vector2(thickness, 0f) : new Vector2(0f, thickness);

            RectTransform edge = CreateRect(parent, edgeName, anchorMin, anchorMax, -side * inset, size);
            edge.pivot = new Vector2(0.5f + side.x * 0.5f, 0.5f + side.y * 0.5f);
            return edge;
        }

        private static RectTransform CreateRect(RectTransform parent, string objectName, Vector2 anchorMin, Vector2 anchorMax,
            Vector2 anchoredPosition, Vector2 sizeDelta)
        {
            var rect = (RectTransform)new GameObject(objectName, typeof(RectTransform)).transform;
            rect.SetParent(parent, false);
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.anchoredPosition = anchoredPosition;
            rect.sizeDelta = sizeDelta;
            return rect;
        }

        private static RectTransform CreateStretchedRect(RectTransform parent, string objectName) =>
            CreateRect(parent, objectName, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);

        private static Image AddImage(RectTransform rect, Color colour)
        {
            var image = rect.gameObject.AddComponent<Image>();
            image.color = colour;
            image.raycastTarget = false;
            return image;
        }

        // Pixel-font text with a hard drop shadow one font pixel down and to the right.
        private static Text AddText(RectTransform rect, Font font, string content, int fontSize, Color colour)
        {
            var text = rect.gameObject.AddComponent<Text>();
            text.font = font;
            text.text = content;
            text.fontSize = fontSize;
            text.color = colour;
            text.alignment = TextAnchor.MiddleCenter;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            text.raycastTarget = false;

            float fontPixel = (float)fontSize / GlyphHeight;
            var shadow = rect.gameObject.AddComponent<Shadow>();
            shadow.effectColor = new Color(0f, 0f, 0f, 0.8f);
            shadow.effectDistance = new Vector2(fontPixel, -fontPixel);
            return text;
        }

        // A bitmap font drawn from the glyphs above, one texture pixel per font pixel. UI text scales a font like this
        // by its font size relative to the font's own, so each text picks its size as usual.
        private static Font GetOrCreatePixelFont()
        {
            string fontPath = $"{FontFolder}/{PixelFontName}.fontsettings";
            var existing = AssetDatabase.LoadAssetAtPath<Font>(fontPath);
            if (existing != null)
                return existing;

            EnsureFolder(FontFolder);

            // Each glyph sits in an 8 by 8 cell of the atlas, sixteen cells to a row.
            const int cell = 8;
            const int columns = 16;
            const int atlasWidth = cell * columns;
            int rows = (PixelGlyphs.Count + columns - 1) / columns;
            int atlasHeight = Mathf.NextPowerOfTwo(rows * cell);

            var pixels = new Color[atlasWidth * atlasHeight];
            var characters = new List<CharacterInfo>();
            int index = 0;
            foreach (KeyValuePair<char, string> glyph in PixelGlyphs)
            {
                int cellX = index % columns * cell;
                int cellY = index / columns * cell;
                string[] lines = glyph.Value.Split('|');
                for (int line = 0; line < GlyphHeight; line++)
                {
                    for (int x = 0; x < GlyphWidth; x++)
                    {
                        if (lines[line][x] == '#')
                            pixels[(cellY + GlyphHeight - 1 - line) * atlasWidth + cellX + x] = Color.white;
                    }
                }

                characters.Add(CreateGlyph(glyph.Key, new RectInt(cellX, cellY, GlyphWidth, GlyphHeight), atlasWidth, atlasHeight));
                index++;
            }

            characters.Add(new CharacterInfo { index = ' ', advance = GlyphWidth - 1 });

            string atlasPath = $"{FontFolder}/{PixelFontName}.png";
            WriteTexture(atlasPath, CreateTexture(pixels, atlasWidth, atlasHeight), TextureWrapMode.Clamp, FilterMode.Point, mipmaps: false);

            var material = new Material(Shader.Find("UI/Default")) { mainTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(atlasPath) };
            AssetDatabase.CreateAsset(material, $"{FontFolder}/{PixelFontName}.mat");

            var font = new Font(PixelFontName) { material = material, characterInfo = characters.ToArray() };
            AssetDatabase.CreateAsset(font, fontPath);

            var serializedFont = new SerializedObject(font);
            SetNumber(serializedFont.FindProperty("m_FontSize"), GlyphHeight);
            SetNumber(serializedFont.FindProperty("m_LineSpacing"), GlyphHeight + 2);
            SetNumber(serializedFont.FindProperty("m_Ascent"), GlyphHeight);
            serializedFont.ApplyModifiedPropertiesWithoutUndo();
            return font;
        }

        // One glyph standing on the baseline, a font pixel wider in its advance than it is drawn.
        private static CharacterInfo CreateGlyph(char character, RectInt pixels, int atlasWidth, int atlasHeight)
        {
            var info = new CharacterInfo { index = character, advance = pixels.width + 1 };
            info.minX = 0;
            info.maxX = pixels.width;
            info.maxY = pixels.height;
            info.minY = 0;
            info.uvBottomLeft = new Vector2((float)pixels.xMin / atlasWidth, (float)pixels.yMin / atlasHeight);
            info.uvBottomRight = new Vector2((float)pixels.xMax / atlasWidth, (float)pixels.yMin / atlasHeight);
            info.uvTopLeft = new Vector2((float)pixels.xMin / atlasWidth, (float)pixels.yMax / atlasHeight);
            info.uvTopRight = new Vector2((float)pixels.xMax / atlasWidth, (float)pixels.yMax / atlasHeight);
            return info;
        }

        private static void SetNumber(SerializedProperty property, float value)
        {
            if (property == null)
                return;

            if (property.propertyType == SerializedPropertyType.Integer)
                property.intValue = Mathf.RoundToInt(value);
            else
                property.floatValue = value;
        }
    }
}
