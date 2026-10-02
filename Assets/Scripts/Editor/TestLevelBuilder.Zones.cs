using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using Wolfenstein.World;
using Object = UnityEngine.Object;

namespace Wolfenstein.Editor
{
    // The look of each zone of the level: its surfaces, trim, lamps, wall fixtures and doors. Every zone is built from the
    // same few ideas in different materials and proportions, so rooms read as different places without new code for each.
    // Themes, lamps, fixtures, door prefabs and their materials are only created when missing.
    public static partial class TestLevelBuilder
    {
        private const string ThemeFolder = "Assets/Levels/Themes";
        private const string LampFolder = "Assets/Prefabs/Lamps";
        private const string FixtureFolder = "Assets/Prefabs/Fixtures";

        private static readonly Color CoolLampColour = new Color(0.82f, 0.92f, 1f);
        private static readonly Color CandleColour = new Color(1f, 0.62f, 0.3f);
        private static readonly Color FloodColour = new Color(1f, 0.9f, 0.74f);
        private static readonly Color AlarmRed = new Color(1f, 0.12f, 0.08f);

        // The zone map symbol each theme is bound to.
        private static (char zone, ZoneTheme theme)[] CreateZoneThemes(float tileSize, float wallHeight)
        {
            return new[]
            {
                ('i', GetOrCreateZoneTheme("Industrial", IndustrialTheme)),
                ('f', GetOrCreateZoneTheme("Fortress", FortressTheme)),
                ('m', GetOrCreateZoneTheme("Military", MilitaryTheme)),
                ('w', GetOrCreateZoneTheme("Storage", StorageTheme)),
                ('a', GetOrCreateZoneTheme("Arena", theme => ArenaTheme(theme, tileSize, wallHeight))),
                ('p', GetOrCreateZoneTheme("SpawnPen", PenTheme)),
                ('d', GetOrCreateZoneTheme("DarkCorridor", DarkCorridorTheme)),
                ('c', GetOrCreateZoneTheme("Command", CommandTheme)),
                ('r', GetOrCreateZoneTheme("Security", theme => SecurityTheme(theme, tileSize, wallHeight))),
                ('x', GetOrCreateZoneTheme("Exit", theme => ExitTheme(theme, tileSize, wallHeight))),
            };
        }

        private static ZoneTheme GetOrCreateZoneTheme(string themeName, Action<SerializedObject> configure)
        {
            string path = $"{ThemeFolder}/{themeName}.asset";
            var theme = AssetDatabase.LoadAssetAtPath<ZoneTheme>(path);
            if (theme != null)
                return theme;

            EnsureFolder(ThemeFolder);
            theme = ScriptableObject.CreateInstance<ZoneTheme>();
            AssetDatabase.CreateAsset(theme, path);
            var serializedTheme = new SerializedObject(theme);
            configure(serializedTheme);
            serializedTheme.ApplyModifiedPropertiesWithoutUndo();
            return theme;
        }

        // Green-grey riveted steel, diamond plate underfoot, pipes along the walls and girders across the ceiling.
        private static void IndustrialTheme(SerializedObject theme)
        {
            Set(theme,
                ("wallMaterial", SurfaceMaterial("Wall_Industrial", () => DrawSteelPanels(11, new Color(0.34f, 0.39f, 0.35f),
                    new Color(0.06f, 0.07f, 0.06f), 64, 64, 0.45f, hazardBand: false), 4f, smoothness: 0.3f, metallic: 0.3f)),
                ("floorMaterial", FloorTread()),
                ("lampPrefab", GetOrCreateCageLamp()),
                ("tilesPerLamp", 6f),
                ("skirtingMaterial", TrimDarkSteel()), ("skirtingHeight", 0.22f), ("skirtingDepth", 0.05f),
                ("pipeMaterial", GetOrCreateMaterial("Pipe_Green", new Color(0.18f, 0.3f, 0.2f), smoothness: 0.5f, metallic: 0.3f)),
                ("pipeCount", 2), ("pipeRadius", 0.075f), ("pipeHeight", 2.55f),
                ("beamMaterial", BeamSteel()), ("beamWidth", 0.24f), ("beamDepth", 0.3f), ("beamSpacing", 2), ("beamsRunNorthSouth", true),
                ("wallFixturePrefab", GetOrCreateVent()), ("wallFixtureSpacing", 5), ("wallFixtureHeight", 2f));
        }

        // Great grey ashlar with stone skirting, cornice and pilasters, timber beams overhead and torches on the walls.
        private static void FortressTheme(SerializedObject theme)
        {
            Material stone = GetOrCreateMaterial("Trim_Stone", new Color(0.46f, 0.45f, 0.43f), GetOrCreateSurface("Wall_GreyStone",
                () => DrawAshlar(2, new Color(0.5f, 0.5f, 0.48f), new Color(0.14f, 0.14f, 0.14f)), 5f).albedo, smoothness: 0.05f);
            Set(theme,
                ("wallMaterial", AssetDatabase.LoadAssetAtPath<Material>($"{MaterialFolder}/Wall_GreyStone.mat")),
                ("ceilingMaterial", CeilingWood()),
                ("lampPrefab", GetOrCreateChandelier()),
                ("tilesPerLamp", 16f),
                ("skirtingMaterial", stone), ("skirtingHeight", 0.4f), ("skirtingDepth", 0.1f),
                ("corniceMaterial", stone), ("corniceHeight", 0.3f), ("corniceDepth", 0.16f),
                ("pilasterMaterial", stone), ("pilasterWidth", 0.55f), ("pilasterDepth", 0.2f), ("pilasterSpacing", 2),
                ("beamMaterial", BeamWood()), ("beamWidth", 0.36f), ("beamDepth", 0.32f), ("beamSpacing", 2),
                ("wallFixturePrefab", GetOrCreateSconce()), ("wallFixtureSpacing", 3), ("wallFixtureHeight", 1.9f));
        }

        // Barracks: olive and pale painted blockwork over bare concrete, strip lights and notice boards.
        private static void MilitaryTheme(SerializedObject theme)
        {
            Set(theme,
                ("wallMaterial", SurfaceMaterial("Wall_Military", () => DrawPaintedBlockwork(13, new Color(0.27f, 0.3f, 0.19f),
                    new Color(0.56f, 0.57f, 0.5f), new Color(0.12f, 0.12f, 0.1f)), 3f, smoothness: 0.15f)),
                ("floorMaterial", SurfaceMaterial("Floor_Concrete", () => DrawConcreteFloor(14, new Color(0.42f, 0.42f, 0.4f), 32), 3f,
                    smoothness: 0.2f)),
                ("lampPrefab", GetOrCreateFluorescentLamp()),
                ("tilesPerLamp", 9f),
                ("skirtingMaterial", GetOrCreateMaterial("Trim_Olive", new Color(0.16f, 0.18f, 0.11f), smoothness: 0.3f)),
                ("skirtingHeight", 0.2f), ("skirtingDepth", 0.04f),
                ("pipeMaterial", GetOrCreateMaterial("Pipe_Grey", new Color(0.3f, 0.31f, 0.32f), smoothness: 0.45f, metallic: 0.4f)),
                ("pipeCount", 1), ("pipeRadius", 0.035f), ("pipeHeight", 2.72f),
                ("wallFixturePrefab", GetOrCreateNoticeBoard()), ("wallFixtureSpacing", 4), ("wallFixtureHeight", 1.45f));
        }

        // A store room: old red brick, bare floorboards, timber posts and joists, and the plain enamel lamp.
        private static void StorageTheme(SerializedObject theme)
        {
            Set(theme,
                ("wallMaterial", SurfaceMaterial("Wall_RedBrick", () => DrawBrickwork(15, new Color(0.5f, 0.23f, 0.16f),
                    new Color(0.2f, 0.18f, 0.16f), 32, 12), 5f, smoothness: 0.1f)),
                ("floorMaterial", SurfaceMaterial("Floor_Planks", () => DrawPlanks(16, new Color(0.46f, 0.32f, 0.2f), 16), 3f,
                    smoothness: 0.2f)),
                ("ceilingMaterial", CeilingWood()),
                ("lampPrefab", GetOrCreateLampPrefab()),
                ("tilesPerLamp", 10f),
                ("pilasterMaterial", BeamWood()), ("pilasterWidth", 0.3f), ("pilasterDepth", 0.22f), ("pilasterSpacing", 2),
                ("beamMaterial", BeamWood()), ("beamWidth", 0.25f), ("beamDepth", 0.25f), ("beamSpacing", 1), ("beamsRunNorthSouth", true));
        }

        // The arena: dark cast concrete, hazard-striped skirting, steel columns and girders, rusted pipes, flood lamps high
        // overhead, red cage lights on the walls, and blast doors that lock while the fight lasts.
        private static void ArenaTheme(SerializedObject theme, float tileSize, float wallHeight)
        {
            Set(theme,
                ("wallMaterial", SurfaceMaterial("Wall_Bunker", () => DrawConcretePanels(17, new Color(0.36f, 0.35f, 0.34f),
                    new Color(0.07f, 0.07f, 0.07f)), 4f, smoothness: 0.1f)),
                ("floorMaterial", SurfaceMaterial("Floor_Arena", () => DrawConcreteFloor(18, new Color(0.3f, 0.29f, 0.28f), 64), 3f,
                    smoothness: 0.25f)),
                ("ceilingMaterial", CeilingGrate()),
                ("doorPrefab", GetOrCreateThemedDoor("BlastDoor", tileSize, wallHeight,
                    SurfaceMaterial("Door_Blast", () => DrawSteelPanels(20, new Color(0.34f, 0.33f, 0.3f), new Color(0.05f, 0.05f, 0.05f),
                        32, 128, 0.25f, hazardBand: true), 4f, smoothness: 0.35f, metallic: 0.35f),
                    HazardMaterial(), lockLights: true)),
                ("lampPrefab", GetOrCreateFloodLamp()),
                ("tilesPerLamp", 13f),
                ("skirtingMaterial", HazardMaterial()), ("skirtingHeight", 0.38f), ("skirtingDepth", 0.05f),
                ("corniceMaterial", TrimDarkSteel()), ("corniceHeight", 0.3f), ("corniceDepth", 0.22f),
                ("pilasterMaterial", BeamSteel()), ("pilasterWidth", 0.42f), ("pilasterDepth", 0.26f), ("pilasterSpacing", 3),
                ("pipeMaterial", GetOrCreateMaterial("Pipe_Rust", new Color(0.36f, 0.2f, 0.12f), smoothness: 0.25f, metallic: 0.2f)),
                ("pipeCount", 3), ("pipeRadius", 0.09f), ("pipeHeight", 2.6f),
                ("beamMaterial", BeamSteel()), ("beamWidth", 0.36f), ("beamDepth", 0.36f), ("beamSpacing", 3), ("beamsRunNorthSouth", true),
                ("wallFixturePrefab", GetOrCreateCagedRedLight()), ("wallFixtureSpacing", 4), ("wallFixtureHeight", 2.2f));
        }

        // The pens monsters come out of: rusted plate, grating underfoot and overhead, and a red emergency lamp in each.
        private static void PenTheme(SerializedObject theme)
        {
            Set(theme,
                ("wallMaterial", SurfaceMaterial("Wall_Rust", () => DrawSteelPanels(21, new Color(0.3f, 0.24f, 0.2f),
                    new Color(0.05f, 0.04f, 0.03f), 32, 64, 1f, hazardBand: false), 4f, smoothness: 0.2f, metallic: 0.2f)),
                ("floorMaterial", SurfaceMaterial("Floor_Grate", () => DrawGrate(22, new Color(0.32f, 0.3f, 0.28f)), 4f,
                    smoothness: 0.3f, metallic: 0.4f)),
                ("ceilingMaterial", CeilingGrate()),
                ("lampPrefab", GetOrCreateEmergencyLamp()),
                ("tilesPerLamp", 2f), ("maxStepsToLamp", 1));
        }

        // The old blue stone of the original game, lit only by the odd bare bulb.
        private static void DarkCorridorTheme(SerializedObject theme)
        {
            Set(theme,
                ("wallMaterial", AssetDatabase.LoadAssetAtPath<Material>($"{MaterialFolder}/Wall_BlueStone.mat")),
                ("lampPrefab", GetOrCreateDimLamp()),
                ("tilesPerLamp", 8f), ("maxStepsToLamp", 4),
                ("skirtingMaterial", GetOrCreateMaterial("Trim_Soot", new Color(0.06f, 0.06f, 0.07f), smoothness: 0.1f)),
                ("skirtingHeight", 0.25f), ("skirtingDepth", 0.03f));
        }

        // An officers' room: dark wood panelling, red carpet, a coffered timber ceiling, chandeliers and paintings.
        private static void CommandTheme(SerializedObject theme)
        {
            Material wood = GetOrCreateMaterial("Trim_Wood", new Color(0.3f, 0.17f, 0.09f), GetOrCreateWoodTexture(), smoothness: 0.4f);
            Set(theme,
                ("wallMaterial", SurfaceMaterial("Wall_WoodPanel", () => DrawWoodPanelling(23, new Color(0.42f, 0.25f, 0.14f)), 3f,
                    smoothness: 0.35f)),
                ("floorMaterial", SurfaceMaterial("Floor_Carpet", () => DrawCarpet(24, new Color(0.42f, 0.06f, 0.06f),
                    new Color(0.62f, 0.45f, 0.14f)), 2f, smoothness: 0f)),
                ("ceilingMaterial", CeilingWood()),
                ("lampPrefab", GetOrCreateChandelier()),
                ("tilesPerLamp", 16f),
                ("skirtingMaterial", wood), ("skirtingHeight", 0.24f), ("skirtingDepth", 0.05f),
                ("corniceMaterial", wood), ("corniceHeight", 0.26f), ("corniceDepth", 0.14f),
                ("pilasterMaterial", wood), ("pilasterWidth", 0.32f), ("pilasterDepth", 0.08f), ("pilasterSpacing", 3),
                ("beamMaterial", BeamWood()), ("beamWidth", 0.3f), ("beamDepth", 0.22f), ("beamSpacing", 2),
                ("wallFixturePrefab", GetOrCreatePainting()), ("wallFixtureSpacing", 3), ("wallFixtureHeight", 1.75f));
        }

        // A checkpoint: blue-grey steel plate striped for danger, tread plate, strip lights and a lockable steel door.
        private static void SecurityTheme(SerializedObject theme, float tileSize, float wallHeight)
        {
            Set(theme,
                ("wallMaterial", SurfaceMaterial("Wall_Security", () => DrawSteelPanels(25, new Color(0.3f, 0.35f, 0.42f),
                    new Color(0.04f, 0.05f, 0.07f), 64, 54, 0.15f, hazardBand: true), 4f, smoothness: 0.35f, metallic: 0.35f)),
                ("floorMaterial", FloorTread()),
                ("doorPrefab", GetOrCreateThemedDoor("SecurityDoor", tileSize, wallHeight,
                    SurfaceMaterial("Door_Security", () => DrawDoor(26, new Color(0.3f, 0.36f, 0.46f)), 4f, smoothness: 0.45f, metallic: 0.3f),
                    TrimDarkSteel(), lockLights: true)),
                ("lampPrefab", GetOrCreateFluorescentLamp()),
                ("tilesPerLamp", 7f),
                ("corniceMaterial", TrimDarkSteel()), ("corniceHeight", 0.2f), ("corniceDepth", 0.12f),
                ("pipeMaterial", GetOrCreateMaterial("Pipe_Grey", new Color(0.3f, 0.31f, 0.32f), smoothness: 0.45f, metallic: 0.4f)),
                ("pipeCount", 1), ("pipeRadius", 0.1f), ("pipeHeight", 2.6f),
                ("wallFixturePrefab", GetOrCreateWarningSign()), ("wallFixtureSpacing", 3), ("wallFixtureHeight", 1.7f));
        }

        // The way out: bright brushed steel, a chequered floor, brass trim and the gate that opens once the arena is cleared.
        private static void ExitTheme(SerializedObject theme, float tileSize, float wallHeight)
        {
            Set(theme,
                ("wallMaterial", SurfaceMaterial("Wall_Elevator", () => DrawSteelPanels(27, new Color(0.55f, 0.56f, 0.58f),
                    new Color(0.1f, 0.1f, 0.11f), 32, 128, 0f, hazardBand: false), 3f, smoothness: 0.55f, metallic: 0.5f)),
                ("floorMaterial", SurfaceMaterial("Floor_Checker", () => DrawCheckerTiles(28, new Color(0.62f, 0.6f, 0.55f),
                    new Color(0.1f, 0.1f, 0.1f)), 3f, smoothness: 0.4f)),
                ("doorPrefab", GetOrCreateThemedDoor("ExitDoor", tileSize, wallHeight,
                    SurfaceMaterial("Door_Exit", () => DrawDoor(29, new Color(0.24f, 0.4f, 0.26f)), 4f, smoothness: 0.45f, metallic: 0.3f),
                    GetOrCreateMaterial("Trim_Brass", new Color(0.7f, 0.52f, 0.2f), smoothness: 0.6f, metallic: 0.5f), lockLights: true)),
                ("lampPrefab", GetOrCreateFluorescentLamp()),
                ("tilesPerLamp", 6f),
                ("pilasterMaterial", GetOrCreateMaterial("Trim_Brass", new Color(0.7f, 0.52f, 0.2f), smoothness: 0.6f, metallic: 0.5f)),
                ("pilasterWidth", 0.14f), ("pilasterDepth", 0.05f), ("pilasterSpacing", 1));
        }

        private static void Set(SerializedObject target, params (string name, object value)[] fields)
        {
            foreach ((string name, object value) in fields)
                SetProperty(target.FindProperty(name), name, value);
        }

        private static void SetProperty(SerializedProperty property, string name, object value)
        {
            if (property == null)
                throw new InvalidOperationException($"No serialized field named '{name}'.");

            switch (value)
            {
                case string text:
                    property.stringValue = text;
                    break;
                case int number:
                    property.intValue = number;
                    break;
                case float number:
                    property.floatValue = number;
                    break;
                case bool flag:
                    property.boolValue = flag;
                    break;
                case char symbol:
                    property.intValue = symbol;
                    break;
                case Color colour:
                    property.colorValue = colour;
                    break;
                case Vector3 vector:
                    property.vector3Value = vector;
                    break;
                case Vector2 vector:
                    property.vector2Value = vector;
                    break;
                case Enum option:
                    property.intValue = Convert.ToInt32(option);
                    break;
                case Component component:
                    property.objectReferenceValue = component;
                    break;
                default:
                    property.objectReferenceValue = (Object)value;
                    break;
            }
        }

        // A lit material over a procedural surface with its normal map.
        private static Material SurfaceMaterial(string surfaceName, Func<Surface> draw, float bumpiness, float smoothness = 0f,
            float metallic = 0f)
        {
            (Texture2D albedo, Texture2D normals) = GetOrCreateSurface(surfaceName, draw, bumpiness);
            return GetOrCreateMaterial(surfaceName, Color.white, albedo, smoothness, metallic, normals);
        }

        private static Material FloorTread() =>
            SurfaceMaterial("Floor_Tread", () => DrawTreadPlate(12, new Color(0.4f, 0.41f, 0.42f)), 3f, smoothness: 0.45f, metallic: 0.4f);

        private static Material CeilingGrate() =>
            SurfaceMaterial("Ceiling_Grate", () => DrawGrate(19, new Color(0.26f, 0.26f, 0.27f)), 4f, smoothness: 0.3f, metallic: 0.4f);

        private static Material CeilingWood() =>
            SurfaceMaterial("Ceiling_Wood", () => DrawPlanks(30, new Color(0.3f, 0.2f, 0.12f), 21), 3f, smoothness: 0.1f);

        private static Material TrimDarkSteel() =>
            GetOrCreateMaterial("Trim_DarkSteel", new Color(0.14f, 0.15f, 0.16f), smoothness: 0.45f, metallic: 0.45f);

        private static Material BeamSteel() =>
            GetOrCreateMaterial("Beam_Steel", new Color(0.2f, 0.21f, 0.22f), smoothness: 0.35f, metallic: 0.45f);

        private static Material BeamWood() =>
            GetOrCreateMaterial("Beam_Wood", new Color(0.36f, 0.22f, 0.12f), GetOrCreateWoodTexture(), smoothness: 0.15f);

        private static Material HazardMaterial() =>
            GetOrCreateMaterial("Trim_Hazard", Color.white, GetOrCreateTexture("Hazard", GenerateHazardTexture), smoothness: 0.3f);

        // ---- Doors -------------------------------------------------------------------------------------------------------

        // A door like the usual one in another finish, with a frame around the doorway on both faces. Lockable doors carry
        // signal lamps in place of the amber ones, lit red while locked and green while free.
        private static Door GetOrCreateThemedDoor(string doorName, float tileSize, float wallHeight, Material panelMaterial,
            Material frameMaterial, bool lockLights)
        {
            string path = $"Assets/Prefabs/{doorName}.prefab";
            var existing = AssetDatabase.LoadAssetAtPath<Door>(path);
            if (existing != null)
                return existing;

            var root = new GameObject(doorName);
            var doorway = root.AddComponent<BoxCollider>();
            doorway.isTrigger = true;
            doorway.center = new Vector3(0f, wallHeight * 0.5f, 0f);
            doorway.size = new Vector3(tileSize, wallHeight, DoorThickness);

            GameObject panel = GameObject.CreatePrimitive(PrimitiveType.Cube);
            panel.name = "Panel";
            panel.transform.SetParent(root.transform, false);
            panel.transform.localPosition = new Vector3(0f, wallHeight * 0.5f, 0f);
            panel.transform.localScale = new Vector3(tileSize, wallHeight, DoorThickness);
            var panelRenderer = panel.GetComponent<MeshRenderer>();
            panelRenderer.sharedMaterial = panelMaterial;
            panelRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

            var body = panel.AddComponent<Rigidbody>();
            body.isKinematic = true;
            body.useGravity = false;

            var door = root.AddComponent<Door>();
            SetFields(door, ("panel", panel.transform), ("openOffset", new Vector3(tileSize, 0f, 0f)));

            var bulbs = new List<Renderer>();
            var lights = new List<Light>();
            DressDoorway(root.transform, tileSize, wallHeight, lockLights ? bulbs : null, lockLights ? lights : null);
            FrameDoorway(root.transform, tileSize, wallHeight, frameMaterial);

            if (lockLights)
            {
                var signals = root.AddComponent<DoorLockLights>();
                var serializedSignals = new SerializedObject(signals);
                serializedSignals.FindProperty("door").objectReferenceValue = door;
                SetArray(serializedSignals.FindProperty("lamps"), bulbs);
                SetArray(serializedSignals.FindProperty("lights"), lights);
                serializedSignals.ApplyModifiedPropertiesWithoutUndo();
            }

            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, path);
            Object.DestroyImmediate(root);
            return prefab.GetComponent<Door>();
        }

        // Posts up each side of the opening and a lintel across the top, standing just proud of the wall on both faces.
        // They sit on the wall beside the opening rather than in it, so nothing passing through touches them.
        private static void FrameDoorway(Transform door, float tileSize, float wallHeight, Material material)
        {
            const float width = 0.28f;
            const float depth = 0.08f;
            const float lintel = 0.3f;
            foreach (float face in new[] { -1f, 1f })
            {
                float z = face * (tileSize * 0.5f + depth * 0.5f);
                foreach (float side in new[] { -1f, 1f })
                {
                    CreateModelPart(door, "Frame Post", PrimitiveType.Cube, new Vector3(side * (tileSize * 0.5f + width * 0.5f), wallHeight * 0.5f, z),
                        new Vector3(width, wallHeight, depth), Quaternion.identity, material);
                }

                CreateModelPart(door, "Lintel", PrimitiveType.Cube, new Vector3(0f, wallHeight - lintel * 0.5f, z),
                    new Vector3(tileSize + width * 2f, lintel, depth), Quaternion.identity, material);
            }
        }

        private static void SetArray<T>(SerializedProperty array, IReadOnlyList<T> values) where T : Object
        {
            array.arraySize = values.Count;
            for (int i = 0; i < values.Count; i++)
                array.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
        }

        // ---- Lamps -------------------------------------------------------------------------------------------------------

        // Each lamp's origin is where it meets the ceiling.
        private static GameObject GetOrCreateLamp(string lampName, Action<Transform> build)
        {
            string path = $"{LampFolder}/{lampName}.prefab";
            var existing = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (existing != null)
                return existing;

            EnsureFolder(LampFolder);
            var lamp = new GameObject(lampName);
            build(lamp.transform);
            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(lamp, path);
            Object.DestroyImmediate(lamp);
            return prefab;
        }

        private static Material LampMetal() =>
            GetOrCreateMaterial("Lamp_Metal", new Color(0.1f, 0.1f, 0.11f), smoothness: 0.4f, metallic: 0.5f);

        // A bulb in a wire cage on a conduit box.
        private static GameObject GetOrCreateCageLamp() => GetOrCreateLamp("CageLamp", root =>
        {
            Material bulb = GetOrCreateGlowMaterial("Lamp_Bulb_White", new Color(1f, 0.95f, 0.85f) * 6f);
            CreateModelPart(root, "Box", PrimitiveType.Cube, new Vector3(0f, -0.04f, 0f), new Vector3(0.2f, 0.08f, 0.2f), Quaternion.identity, LampMetal());
            CreateModelPart(root, "Bulb", PrimitiveType.Sphere, new Vector3(0f, -0.15f, 0f), new Vector3(0.12f, 0.14f, 0.12f), Quaternion.identity, bulb);
            foreach (float angle in new[] { 0f, 60f, 120f })
            {
                CreateModelPart(root, "Cage", PrimitiveType.Cube, new Vector3(0f, -0.15f, 0f), new Vector3(0.17f, 0.18f, 0.012f),
                    Quaternion.Euler(0f, angle, 0f), LampMetal());
            }

            AddPointLight(root, new Vector3(0f, -0.3f, 0f), new Color(1f, 0.93f, 0.8f), 7.5f, 2.2f);
        });

        // An iron ring of candles hanging on a chain.
        private static GameObject GetOrCreateChandelier() => GetOrCreateLamp("Chandelier", root =>
        {
            Material iron = GetOrCreateMaterial("Iron", new Color(0.08f, 0.08f, 0.08f), smoothness: 0.3f, metallic: 0.4f);
            Material wax = GetOrCreateMaterial("Candle_Wax", new Color(0.85f, 0.8f, 0.68f), smoothness: 0.2f, emission: new Color(0.2f, 0.16f, 0.1f));
            Material flame = GetOrCreateGlowMaterial("Candle_Flame", CandleColour * 5f);
            CreateModelPart(root, "Chain", PrimitiveType.Cylinder, new Vector3(0f, -0.3f, 0f), new Vector3(0.03f, 0.6f, 0.03f), Quaternion.identity, iron);
            CreateModelPart(root, "Hub", PrimitiveType.Sphere, new Vector3(0f, -0.62f, 0f), new Vector3(0.12f, 0.12f, 0.12f), Quaternion.identity, iron);
            const int candles = 6;
            const float radius = 0.42f;
            for (int i = 0; i < candles; i++)
            {
                Quaternion turn = Quaternion.Euler(0f, 360f * i / candles, 0f);
                Vector3 rim = turn * new Vector3(0f, -0.66f, radius);
                CreateModelPart(root, "Ring", PrimitiveType.Cube, turn * new Vector3(0f, -0.66f, radius * Mathf.Cos(Mathf.PI / candles)),
                    new Vector3(2f * radius * Mathf.Sin(Mathf.PI / candles) + 0.03f, 0.035f, 0.035f), turn, iron);
                CreateModelPart(root, "Arm", PrimitiveType.Cube, turn * new Vector3(0f, -0.64f, radius * 0.5f), new Vector3(0.025f, 0.025f, radius), turn, iron);
                CreateModelPart(root, "Candle", PrimitiveType.Cylinder, rim + Vector3.up * 0.08f, new Vector3(0.05f, 0.14f, 0.05f), Quaternion.identity, wax);
                CreateModelPart(root, "Flame", PrimitiveType.Sphere, rim + Vector3.up * 0.18f, new Vector3(0.035f, 0.07f, 0.035f), Quaternion.identity, flame);
            }

            AddPointLight(root, new Vector3(0f, -0.6f, 0f), CandleColour, 10f, 2.4f);
        });

        // A strip light: a long steel housing on two rods with a glowing tube underneath.
        private static GameObject GetOrCreateFluorescentLamp() => GetOrCreateLamp("FluorescentLamp", root =>
        {
            Material tube = GetOrCreateGlowMaterial("Lamp_Tube", CoolLampColour * 5f);
            Material housing = GetOrCreateMaterial("Lamp_Housing", new Color(0.55f, 0.56f, 0.55f), smoothness: 0.4f, metallic: 0.2f);
            foreach (float side in new[] { -0.45f, 0.45f })
                CreateModelPart(root, "Rod", PrimitiveType.Cylinder, new Vector3(side, -0.12f, 0f), new Vector3(0.015f, 0.24f, 0.015f), Quaternion.identity, LampMetal());

            CreateModelPart(root, "Housing", PrimitiveType.Cube, new Vector3(0f, -0.27f, 0f), new Vector3(1.3f, 0.07f, 0.24f), Quaternion.identity, housing);
            CreateModelPart(root, "Tube", PrimitiveType.Cylinder, new Vector3(0f, -0.32f, 0f), new Vector3(0.05f, 1.2f, 0.05f), Quaternion.Euler(0f, 0f, 90f), tube);
            AddPointLight(root, new Vector3(0f, -0.45f, 0f), CoolLampColour, 8.5f, 2.3f);
        });

        // A big industrial high-bay lamp: a wide dome shade over a bright bulb, throwing light a long way.
        private static GameObject GetOrCreateFloodLamp() => GetOrCreateLamp("FloodLamp", root =>
        {
            Material shade = GetOrCreateMaterial("Lamp_Shade_Grey", new Color(0.22f, 0.23f, 0.24f), smoothness: 0.5f, metallic: 0.4f);
            Material glow = GetOrCreateGlowMaterial("Lamp_Flood_Glow", FloodColour * 4f);
            CreateModelPart(root, "Mount", PrimitiveType.Cylinder, new Vector3(0f, -0.08f, 0f), new Vector3(0.1f, 0.16f, 0.1f), Quaternion.identity, LampMetal());
            CreateModelPart(root, "Dome", PrimitiveType.Sphere, new Vector3(0f, -0.22f, 0f), new Vector3(0.62f, 0.3f, 0.62f), Quaternion.identity, shade);
            CreateModelPart(root, "Lens", PrimitiveType.Cylinder, new Vector3(0f, -0.25f, 0f), new Vector3(0.56f, 0.02f, 0.56f), Quaternion.identity, glow);
            AddPointLight(root, new Vector3(0f, -0.5f, 0f), FloodColour, 13f, 3f);
        });

        // A bare bulb hanging on its flex, giving a weak orange light.
        private static GameObject GetOrCreateDimLamp() => GetOrCreateLamp("DimLamp", root =>
        {
            Material bulb = GetOrCreateGlowMaterial("Lamp_Bulb_Dim", new Color(1f, 0.55f, 0.25f) * 3f);
            CreateModelPart(root, "Flex", PrimitiveType.Cylinder, new Vector3(0f, -0.3f, 0f), new Vector3(0.012f, 0.6f, 0.012f), Quaternion.identity, LampMetal());
            CreateModelPart(root, "Holder", PrimitiveType.Cylinder, new Vector3(0f, -0.62f, 0f), new Vector3(0.05f, 0.06f, 0.05f), Quaternion.identity, LampMetal());
            CreateModelPart(root, "Bulb", PrimitiveType.Sphere, new Vector3(0f, -0.7f, 0f), new Vector3(0.09f, 0.11f, 0.09f), Quaternion.identity, bulb);
            AddPointLight(root, new Vector3(0f, -0.8f, 0f), new Color(1f, 0.6f, 0.32f), 6.5f, 1.2f);
        });

        // A red caged lamp, as over a hatch that should not be opened.
        private static GameObject GetOrCreateEmergencyLamp() => GetOrCreateLamp("EmergencyLamp", root =>
        {
            Material bulb = GetOrCreateGlowMaterial("Lamp_Bulb_Red", AlarmRed * 5f);
            CreateModelPart(root, "Base", PrimitiveType.Cylinder, new Vector3(0f, -0.03f, 0f), new Vector3(0.18f, 0.06f, 0.18f), Quaternion.identity, LampMetal());
            CreateModelPart(root, "Dome", PrimitiveType.Sphere, new Vector3(0f, -0.1f, 0f), new Vector3(0.14f, 0.14f, 0.14f), Quaternion.identity, bulb);
            foreach (float angle in new[] { 0f, 90f })
            {
                CreateModelPart(root, "Cage", PrimitiveType.Cube, new Vector3(0f, -0.1f, 0f), new Vector3(0.17f, 0.16f, 0.012f),
                    Quaternion.Euler(0f, angle, 0f), LampMetal());
            }

            AddPointLight(root, new Vector3(0f, -0.3f, 0f), AlarmRed, 5f, 2.2f);
        });

        // ---- Wall fixtures -----------------------------------------------------------------------------------------------

        // Each fixture's origin is its back, against the wall, and it faces +Z. None has a collider.
        private static GameObject GetOrCreateFixture(string fixtureName, Action<Transform> build)
        {
            string path = $"{FixtureFolder}/{fixtureName}.prefab";
            var existing = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (existing != null)
                return existing;

            EnsureFolder(FixtureFolder);
            var fixture = new GameObject(fixtureName);
            build(fixture.transform);
            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(fixture, path);
            Object.DestroyImmediate(fixture);
            return prefab;
        }

        // A louvred air vent in a steel frame.
        private static GameObject GetOrCreateVent() => GetOrCreateFixture("Vent", root =>
        {
            Material dark = GetOrCreateMaterial("Vent_Dark", new Color(0.03f, 0.03f, 0.03f));
            CreateModelPart(root, "Frame", PrimitiveType.Cube, new Vector3(0f, 0f, 0.03f), new Vector3(0.7f, 0.45f, 0.06f), Quaternion.identity, TrimDarkSteel());
            CreateModelPart(root, "Hole", PrimitiveType.Cube, new Vector3(0f, 0f, 0.035f), new Vector3(0.6f, 0.36f, 0.06f), Quaternion.identity, dark);
            for (int i = 0; i < 5; i++)
            {
                CreateModelPart(root, "Louvre", PrimitiveType.Cube, new Vector3(0f, -0.14f + i * 0.07f, 0.065f), new Vector3(0.6f, 0.05f, 0.01f),
                    Quaternion.Euler(-35f, 0f, 0f), TrimDarkSteel());
            }
        });

        // An iron bracket holding a burning torch. It glows without casting light, so a row of them costs nothing.
        private static GameObject GetOrCreateSconce() => GetOrCreateFixture("Sconce", root =>
        {
            Material iron = GetOrCreateMaterial("Iron", new Color(0.08f, 0.08f, 0.08f), smoothness: 0.3f, metallic: 0.4f);
            Material wood = GunWood();
            Material flame = GetOrCreateGlowMaterial("Torch_Flame", new Color(1f, 0.5f, 0.15f) * 6f);
            Material core = GetOrCreateGlowMaterial("Torch_Flame_Core", new Color(1f, 0.85f, 0.5f) * 8f);
            CreateModelPart(root, "Plate", PrimitiveType.Cube, new Vector3(0f, 0f, 0.015f), new Vector3(0.14f, 0.3f, 0.03f), Quaternion.identity, iron);
            CreateModelPart(root, "Arm", PrimitiveType.Cube, new Vector3(0f, -0.02f, 0.12f), new Vector3(0.04f, 0.04f, 0.2f), Quaternion.Euler(-20f, 0f, 0f), iron);
            CreateModelPart(root, "Cup", PrimitiveType.Cylinder, new Vector3(0f, 0.04f, 0.22f), new Vector3(0.09f, 0.06f, 0.09f), Quaternion.identity, iron);
            CreateModelPart(root, "Torch", PrimitiveType.Cylinder, new Vector3(0f, 0.14f, 0.22f), new Vector3(0.05f, 0.28f, 0.05f), Quaternion.Euler(-8f, 0f, 0f), wood);
            CreateModelPart(root, "Flame", PrimitiveType.Sphere, new Vector3(0f, 0.34f, 0.24f), new Vector3(0.11f, 0.2f, 0.11f), Quaternion.identity, flame);
            CreateModelPart(root, "Flame Core", PrimitiveType.Sphere, new Vector3(0f, 0.31f, 0.24f), new Vector3(0.06f, 0.11f, 0.06f), Quaternion.identity, core);
        });

        // A cork board with orders and a map pinned to it.
        private static GameObject GetOrCreateNoticeBoard() => GetOrCreateFixture("NoticeBoard", root =>
        {
            Material cork = GetOrCreateMaterial("Cork", new Color(0.55f, 0.4f, 0.24f), GetOrCreateFabricTexture(), smoothness: 0.05f);
            Material paper = GetOrCreateMaterial("Paper", new Color(0.82f, 0.8f, 0.7f), smoothness: 0.1f);
            Material map = GetOrCreateMaterial("Paper_Map", new Color(0.62f, 0.66f, 0.5f), smoothness: 0.1f);
            CreateModelPart(root, "Frame", PrimitiveType.Cube, new Vector3(0f, 0f, 0.02f), new Vector3(0.95f, 0.7f, 0.04f), Quaternion.identity, BeamWood());
            CreateModelPart(root, "Cork", PrimitiveType.Cube, new Vector3(0f, 0f, 0.03f), new Vector3(0.85f, 0.6f, 0.04f), Quaternion.identity, cork);
            CreateModelPart(root, "Map", PrimitiveType.Cube, new Vector3(0.14f, 0.02f, 0.052f), new Vector3(0.44f, 0.36f, 0.004f), Quaternion.Euler(0f, 0f, 2f), map);
            CreateModelPart(root, "Orders", PrimitiveType.Cube, new Vector3(-0.26f, 0.08f, 0.052f), new Vector3(0.18f, 0.24f, 0.004f), Quaternion.Euler(0f, 0f, -4f), paper);
            CreateModelPart(root, "Note", PrimitiveType.Cube, new Vector3(-0.25f, -0.17f, 0.052f), new Vector3(0.16f, 0.12f, 0.004f), Quaternion.Euler(0f, 0f, 6f), paper);
        });

        // A dark oil painting of a stormy sky over hills, in a heavy gilt frame.
        private static GameObject GetOrCreatePainting() => GetOrCreateFixture("Painting", root =>
        {
            Material gilt = GetOrCreateMaterial("Gilt", new Color(0.62f, 0.45f, 0.15f), smoothness: 0.55f, metallic: 0.5f);
            Material sky = GetOrCreateMaterial("Paint_Sky", new Color(0.35f, 0.14f, 0.1f), smoothness: 0.3f);
            Material land = GetOrCreateMaterial("Paint_Land", new Color(0.08f, 0.1f, 0.07f), smoothness: 0.3f);
            CreateModelPart(root, "Frame", PrimitiveType.Cube, new Vector3(0f, 0f, 0.03f), new Vector3(0.9f, 0.7f, 0.06f), Quaternion.identity, gilt);
            CreateModelPart(root, "Sky", PrimitiveType.Cube, new Vector3(0f, 0.08f, 0.055f), new Vector3(0.76f, 0.4f, 0.02f), Quaternion.identity, sky);
            CreateModelPart(root, "Hills", PrimitiveType.Cube, new Vector3(0f, -0.17f, 0.056f), new Vector3(0.76f, 0.16f, 0.02f), Quaternion.identity, land);
            CreateModelPart(root, "Hill", PrimitiveType.Sphere, new Vector3(0.12f, -0.1f, 0.057f), new Vector3(0.5f, 0.2f, 0.02f), Quaternion.identity, land);
        });

        // A small red bulb in a wire cage, glowing without casting light.
        private static GameObject GetOrCreateCagedRedLight() => GetOrCreateFixture("CagedRedLight", root =>
        {
            Material bulb = GetOrCreateGlowMaterial("Lamp_Bulb_Red", AlarmRed * 5f);
            CreateModelPart(root, "Plate", PrimitiveType.Cube, new Vector3(0f, 0f, 0.02f), new Vector3(0.2f, 0.2f, 0.04f), Quaternion.identity, TrimDarkSteel());
            CreateModelPart(root, "Bulb", PrimitiveType.Sphere, new Vector3(0f, 0f, 0.1f), new Vector3(0.12f, 0.12f, 0.12f), Quaternion.identity, bulb);
            foreach (float angle in new[] { 0f, 90f })
            {
                CreateModelPart(root, "Cage", PrimitiveType.Cube, new Vector3(0f, 0f, 0.1f), new Vector3(0.15f, 0.012f, 0.15f),
                    Quaternion.Euler(0f, 0f, angle), TrimDarkSteel());
            }
        });

        // A yellow warning sign with a black exclamation mark.
        private static GameObject GetOrCreateWarningSign() => GetOrCreateFixture("WarningSign", root =>
        {
            Material yellow = GetOrCreateMaterial("Sign_Yellow", HazardYellow, smoothness: 0.3f, emission: HazardYellow * 0.15f);
            Material black = GetOrCreateMaterial("Sign_Black", HazardBlack, smoothness: 0.3f);
            CreateModelPart(root, "Sign", PrimitiveType.Cube, new Vector3(0f, 0f, 0.01f), new Vector3(0.42f, 0.42f, 0.02f), Quaternion.Euler(0f, 0f, 45f), yellow);
            CreateModelPart(root, "Bar", PrimitiveType.Cube, new Vector3(0f, 0.04f, 0.022f), new Vector3(0.05f, 0.2f, 0.005f), Quaternion.identity, black);
            CreateModelPart(root, "Dot", PrimitiveType.Cube, new Vector3(0f, -0.11f, 0.022f), new Vector3(0.05f, 0.05f, 0.005f), Quaternion.identity, black);
        });
    }
}
