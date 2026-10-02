using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using Wolfenstein.Combat;
using Wolfenstein.Core;
using Wolfenstein.Enemies;
using Wolfenstein.Interaction;
using Wolfenstein.Monsters;
using Wolfenstein.Pickups;
using Wolfenstein.Player;
using Wolfenstein.Progression;
using Wolfenstein.Waves;
using Wolfenstein.Weapons;
using Wolfenstein.World;
using Object = UnityEngine.Object;

namespace Wolfenstein.Editor
{
    /// <summary>
    /// Generates the test level: procedural textures and materials, the level layout with its zones and decor, the zone
    /// themes, doors, lamps, fixtures and props, the pistol, the guard, the monsters and the arena's waves, the items, the
    /// player and HUD prefabs, the lighting and post-processing, synthesized audio with its cues and sound sets, and the
    /// scene that wires them together.
    /// Textures, materials, audio clips, cues and sound sets, the layout, the tuning assets, the wave set, the themes, the
    /// post-processing profile and the door, bullet mark, lamp, fixture, prop, projectile and item prefabs are only created
    /// when missing, so edits to them survive a rebuild. The player, HUD, guard and monster prefabs and the scene are always
    /// regenerated.
    /// </summary>
    public static partial class TestLevelBuilder
    {
        private const string ScenePath = "Assets/Scenes/TestLevel.unity";
        private const string PlayerPrefabPath = "Assets/Prefabs/Player.prefab";
        private const string DoorPrefabPath = "Assets/Prefabs/Door.prefab";
        private const string LayoutPath = "Assets/Levels/TestLevel.asset";
        private const string InputActionsPath = "Assets/InputSystem_Actions.inputactions";
        private const string TextureFolder = "Assets/Textures";
        private const string MaterialFolder = "Assets/Materials";
        private const string LitShaderName = "Universal Render Pipeline/Lit";

        private const float EyeHeight = 1.6f;
        private const float DoorThickness = 0.2f;

        // The original level is the southern half of the map, unchanged; the arena and the rooms around it lie to the north.
        // The zone map gives each room its theme, and the decor map places props, lurking monsters and the arena's markers.
        private static readonly string[] TestLevelMap =
        {
            "##########################",
            "##########.....###########",
            "##########.....###########",
            "#######.##.....###.#######",
            "#######.####-#####.#######",
            "####..................####",
            "####...........C......####",
            "##......................##",
            "####...P..........P...####",
            "####........MM........####",
            "####........MM........####",
            "####...P..........P...####",
            "####..................####",
            "##........C.............##",
            "####..................####",
            "#####-##############-#####",
            "#####.###########.......##",
            "#####.###########.......##",
            "#####.###########.......##",
            "#####-#########%%%%%-%%%%%",
            "#........f#####%........t%",
            "#.E.......#####%.........%",
            "#..%...%..#####%.........%",
            "#....g.........|....%..W.%",
            "#..%...%..#####%.........%",
            "#....S....#####%.........%",
            "#a........#####%h..S....a%",
            "#####-#########%%%%%-%%%%%",
            "#####.##############.#####",
            "#####.##############.#####",
            "#####.##############.#####",
            "#####.......a............#",
            "#####.###.##########-#####",
            "#####-###.####..c.....c..#",
            "#......f#.####...........#",
            "#.E.....#h####...........#",
            "#....N..######.....^.....#",
            "#k.....c######...........#",
            "##########################",
        };

        private static readonly string[] TestLevelZones =
        {
            "..........................",
            "..........xxxxx...........",
            "..........xxxxx...........",
            ".......p..xxxxx...p.......",
            ".......p....x.....p.......",
            "....aaaaaaaaaaaaaaaaaa....",
            "....aaaaaaaaaaaaaaaaaa....",
            "..ppaaaaaaaaaaaaaaaaaapp..",
            "....aaaaaaaaaaaaaaaaaa....",
            "....aaaaaaaaaaaaaaaaaa....",
            "....aaaaaaaaaaaaaaaaaa....",
            "....aaaaaaaaaaaaaaaaaa....",
            "....aaaaaaaaaaaaaaaaaa....",
            "..ppaaaaaaaaaaaaaaaaaapp..",
            "....aaaaaaaaaaaaaaaaaa....",
            ".....a..............a.....",
            ".....d...........rrrrrrr..",
            ".....d...........rrrrrrr..",
            ".....d...........rrrrrrr..",
            "....................r.....",
            ".fffffffff......ccccccccc.",
            ".fffffffff......ccccccccc.",
            ".fffffffff......ccccccccc.",
            ".fffffffffddddd.ccccccccc.",
            ".fffffffff......ccccccccc.",
            ".fffffffff......ccccccccc.",
            ".fffffffff......ccccccccc.",
            "..........................",
            ".....i..............i.....",
            ".....i..............i.....",
            ".....i..............i.....",
            ".....iiiiiiiiiiiiiiiiiiii.",
            ".........i................",
            ".........i....mmmmmmmmmmm.",
            ".wwwwwww.i....mmmmmmmmmmm.",
            ".wwwwwww.i....mmmmmmmmmmm.",
            ".wwwwwww......mmmmmmmmmmm.",
            ".wwwwwww......mmmmmmmmmmm.",
            "..........................",
        };

        private static readonly string[] TestLevelDecor =
        {
            "..........................",
            "..........p...p...........",
            "..........................",
            ".......@..b...L...@.......",
            ".......@....G.....@.......",
            "....C....K.X.X...S...B....",
            ".............+............",
            "..@@..d............u..@@..",
            "..........q..h.......e....",
            "................+.........",
            "....@....+...........@....",
            "............h.............",
            "....e..........d..........",
            "..@@........+.........@@..",
            "....B...S.......K....c....",
            "..........................",
            ".....1...........e.....L..",
            ".....1............1...3...",
            ".....D...........S.....S..",
            "..........................",
            ".x...n...........o....o...",
            "........u.......n.........",
            "........................K.",
            ".n..d......qd.............",
            "........................n.",
            "................t.........",
            "......K..x...........f....",
            "..........................",
            "..........................",
            ".....u..............h.....",
            "..........................",
            ".......m...b..g.B.....e.b.",
            "..........................",
            ".........q....S.........r.",
            ".C.s.c....................",
            "......u.......k.........f.",
            ".s.....B..................",
            "....b.........L..q......k.",
            "..........................",
        };

        // Prefabs every scene of one build shares. Each is made once per build: remaking one after a scene holding an
        // instance of it has been saved would give its parts new identities and break that scene's instance.
        private static GameObject playerPrefab;
        private static GameObject hudPrefab;

        [MenuItem("Tools/Wolfenstein/Rebuild Test Level")]
        private static void RebuildFromMenu()
        {
            bool confirmed = !File.Exists(ScenePath) || EditorUtility.DisplayDialog(
                "Rebuild Levels",
                "This regenerates the MainMenu, Level1, Level2 and TestLevel scenes and the Player, HUD, Guard and monster prefabs, " +
                "discarding any changes made to them.",
                "Rebuild",
                "Cancel");

            if (confirmed && EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                Build();
        }

        /// <summary>
        /// Batch mode entry point: Unity -batchmode -quit -executeMethod Wolfenstein.Editor.TestLevelBuilder.Build
        /// Builds the test level, both levels of the episode and the title screen, and lists them in the build settings with
        /// the title first.
        /// </summary>
        public static void Build()
        {
            playerPrefab = null;
            hudPrefab = null;
            guardPrefab = null;
            monsterBreeds = null;
            audioAssetsReady = false;

            // A new scene unloads assets that nothing in it holds, so each scene loads what it needs once it exists.
            BuildLevelScene(ScenePath, null);
            GetOrCreateLevel1(GetOrCreateLevel2());
            BuildLevelScene(Level1ScenePath, Level1DefinitionPath);
            BuildLevelScene(Level2ScenePath, Level2DefinitionPath);
            BuildTitleScene(Level1DefinitionPath);

            EditorBuildSettings.scenes = new[]
            {
                new EditorBuildSettingsScene(TitleScenePath, true),
                new EditorBuildSettingsScene(Level1ScenePath, true),
                new EditorBuildSettingsScene(Level2ScenePath, true),
                new EditorBuildSettingsScene(ScenePath, true),
            };
            AssetDatabase.SaveAssets();

            Debug.Log($"Levels built: {TitleScenePath}, {Level1ScenePath}, {Level2ScenePath}, {ScenePath}");
        }

        // One level's scene. With a level definition it is a level of the episode, with objectives, encounters, an exit and
        // a tally; without one it is the test range, with the arena's usual waves.
        private static void BuildLevelScene(string scenePath, string levelPath)
        {
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            LevelDefinition level = levelPath != null ? AssetDatabase.LoadAssetAtPath<LevelDefinition>(levelPath) : null;
            LevelLayout layout = level != null ? level.Layout : GetOrCreateLayout();
            WaveSet waveSet = level != null ? level.Waves : GetOrCreateArenaWaves();

            ConfigureEnvironmentLighting();
            CreateKeyLight();
            CreatePostProcessing();

            var gameManager = new GameObject("GameManager").AddComponent<GameManager>();
            var levelManager = new GameObject("LevelManager").AddComponent<LevelManager>();
            WorldManager worldManager = CreateWorld(layout);
            CreateLevelLighting(worldManager);
            LevelDressing dressing = CreateLevelDressing(worldManager);
            PlayerController player = InstantiatePlayer(worldManager.GetSpawnPose());
            PickupManager pickups = CreatePickupManager(worldManager);
            EnemyManager enemies = CreateEnemyManager(worldManager, player, pickups);
            MonsterManager monsters = CreateMonsterManager(worldManager, player, pickups);
            WaveManager waves = CreateWaveManager(worldManager, monsters, pickups, player, waveSet);
            Progress progress = level != null ? CreateProgress(level, worldManager, dressing, player, pickups, enemies, monsters, waves) : null;
            InstantiateHud(player, levelManager, waves, progress);
            CreateAudio(worldManager, player, enemies, monsters, waves, gameManager, levelManager, progress);

            SetFields(levelManager,
                ("gameManager", gameManager),
                ("worldManager", worldManager),
                ("player", player),
                ("playerHealth", player.GetComponent<Health>()),
                ("restartAction", FindActionReference("Player", "Attack")));
            if (progress != null)
            {
                SetFields(levelManager,
                    ("level", level),
                    ("objectives", progress.Objectives),
                    ("exit", progress.Exit),
                    ("statistics", progress.Statistics),
                    ("playerScore", player.GetComponent<Score>()),
                    ("playerAmmo", player.GetComponent<AmmoSupply>()),
                    ("continueAction", FindActionReference("UI", "Submit")),
                    ("titleScene", SceneName(TitleScenePath)));
            }

            EditorSceneManager.SaveScene(scene, scenePath);
        }

        private static WorldManager CreateWorld(LevelLayout layout)
        {
            // The blue brick of the original game for the corridors, rough grey stone for the rooms,
            // dark flagstones underfoot and concrete panels overhead.
            (Texture2D blueStone, Texture2D blueStoneNormals) = GetOrCreateSurface("Wall_BlueStone",
                () => DrawBrickwork(1, new Color(0.27f, 0.34f, 0.64f), new Color(0.06f, 0.07f, 0.14f), 32, 16), 5f);
            (Texture2D greyStone, Texture2D greyStoneNormals) = GetOrCreateSurface("Wall_GreyStone",
                () => DrawAshlar(2, new Color(0.5f, 0.5f, 0.48f), new Color(0.14f, 0.14f, 0.14f)), 5f);
            (Texture2D floor, Texture2D floorNormals) = GetOrCreateSurface("Floor",
                () => DrawFlagstones(5, new Color(0.36f, 0.34f, 0.31f), new Color(0.09f, 0.085f, 0.08f)), 4f);
            (Texture2D ceiling, Texture2D ceilingNormals) = GetOrCreateSurface("Ceiling",
                () => DrawCeilingPanels(6, new Color(0.3f, 0.3f, 0.32f), new Color(0.06f, 0.06f, 0.07f)), 3f);

            var worldManager = new GameObject("World").AddComponent<WorldManager>();
            var serializedWorld = new SerializedObject(worldManager);
            serializedWorld.FindProperty("layout").objectReferenceValue = layout;
            serializedWorld.FindProperty("floorMaterial").objectReferenceValue =
                GetOrCreateMaterial("Floor", Color.white, floor, smoothness: 0.25f, normalMap: floorNormals);
            serializedWorld.FindProperty("ceilingMaterial").objectReferenceValue =
                GetOrCreateMaterial("Ceiling", Color.white, ceiling, smoothness: 0.05f, normalMap: ceilingNormals);

            Material blueStoneWall = GetOrCreateMaterial("Wall_BlueStone", Color.white, blueStone, smoothness: 0.15f, normalMap: blueStoneNormals);
            (char symbol, Material material, GameObject prefab)[] styles =
            {
                ('#', blueStoneWall, null),
                ('%', GetOrCreateMaterial("Wall_GreyStone", Color.white, greyStone, smoothness: 0.1f, normalMap: greyStoneNormals), null),
                ('P', blueStoneWall, GetOrCreatePillar(worldManager.TileSize, worldManager.WallHeight)),
                ('C', blueStoneWall, GetOrCreateCoverCrates()),
                ('M', blueStoneWall, GetOrCreateMachinery(worldManager.WallHeight)),
            };

            SerializedProperty wallStyles = serializedWorld.FindProperty("wallStyles");
            wallStyles.arraySize = styles.Length;
            for (int i = 0; i < styles.Length; i++)
            {
                SerializedProperty style = wallStyles.GetArrayElementAtIndex(i);
                SetWallStyle(style, styles[i].symbol, styles[i].material);
                style.FindPropertyRelative("prefab").objectReferenceValue = styles[i].prefab;
            }

            serializedWorld.FindProperty("doorPrefab").objectReferenceValue =
                GetOrCreateDoorPrefab(worldManager.TileSize, worldManager.WallHeight);

            (char zone, ZoneTheme theme)[] themes = CreateZoneThemes(worldManager.TileSize, worldManager.WallHeight);
            SerializedProperty zoneThemes = serializedWorld.FindProperty("zoneThemes");
            zoneThemes.arraySize = themes.Length;
            for (int i = 0; i < themes.Length; i++)
            {
                SerializedProperty binding = zoneThemes.GetArrayElementAtIndex(i);
                binding.FindPropertyRelative("zone").intValue = themes[i].zone;
                binding.FindPropertyRelative("theme").objectReferenceValue = themes[i].theme;
            }

            serializedWorld.ApplyModifiedPropertiesWithoutUndo();
            return worldManager;
        }

        private static Door GetOrCreateDoorPrefab(float tileSize, float wallHeight)
        {
            var existing = AssetDatabase.LoadAssetAtPath<Door>(DoorPrefabPath);
            if (existing != null)
                return existing;

            (Texture2D doorTexture, Texture2D doorNormals) = GetOrCreateSurface("Door_Metal",
                () => DrawDoor(3, new Color(0.3f, 0.57f, 0.61f)), 4f);
            Material doorMaterial = GetOrCreateMaterial("Door_Metal", Color.white, doorTexture,
                smoothness: 0.45f, metallic: 0.3f, normalMap: doorNormals);

            var root = new GameObject("Door");

            // Covers the doorway in the panel's closed position, so the door can be targeted even when fully open.
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
            panelRenderer.sharedMaterial = doorMaterial;
            panelRenderer.shadowCastingMode = ShadowCastingMode.Off;

            // A collider that moves should belong to a kinematic rigidbody rather than be static.
            var body = panel.AddComponent<Rigidbody>();
            body.isKinematic = true;
            body.useGravity = false;

            // The panel slides its full width sideways, into the wall beside the doorway.
            SetFields(root.AddComponent<Door>(),
                ("panel", panel.transform),
                ("openOffset", new Vector3(tileSize, 0f, 0f)));

            DressDoorway(root.transform, tileSize, wallHeight);

            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, DoorPrefabPath);
            Object.DestroyImmediate(root);
            return prefab.GetComponent<Door>();
        }

        // Steel facing on the sides of the doorway, like the door jambs of the original game, a track in the floor and
        // ceiling for the panel to run in, and a small amber lamp on the ceiling to either side. None of it has a collider,
        // and none of it moves with the panel. Given lists for them, the lamps are signal lamps instead, which a lockable
        // door lights red or green, and their bulbs and lights are added to the lists.
        private static void DressDoorway(Transform door, float tileSize, float wallHeight,
            List<Renderer> signalBulbs = null, List<Light> signalLights = null)
        {
            // Just off the surfaces they cover, so the two do not flicker against each other.
            const float gap = 0.004f;

            (Texture2D jambTexture, Texture2D jambNormals) = GetOrCreateSurface("Door_Jamb",
                () => DrawDoorJamb(4, new Color(0.3f, 0.33f, 0.36f)), 4f);
            Material jamb = GetOrCreateMaterial("Door_Jamb", Color.white, jambTexture, smoothness: 0.35f, metallic: 0.3f, normalMap: jambNormals);
            Material track = GetOrCreateMaterial("Door_Track", new Color(0.12f, 0.13f, 0.14f), smoothness: 0.45f, metallic: 0.5f);

            // A quad faces along -Z, so turning it a quarter either way faces it into the doorway from that side.
            foreach (float side in new[] { -1f, 1f })
            {
                GameObject face = CreateRenderOnlyPrimitive(PrimitiveType.Quad, "Jamb", jamb);
                face.transform.SetParent(door, false);
                face.transform.SetLocalPositionAndRotation(
                    new Vector3(side * (tileSize * 0.5f - gap), wallHeight * 0.5f, 0f), Quaternion.Euler(0f, side * 90f, 0f));
                face.transform.localScale = new Vector3(tileSize, wallHeight, 1f);
            }

            var trackSize = new Vector3(tileSize, DoorThickness + 0.1f, 1f);
            GameObject floorTrack = CreateRenderOnlyPrimitive(PrimitiveType.Quad, "Floor Track", track);
            floorTrack.transform.SetParent(door, false);
            floorTrack.transform.SetLocalPositionAndRotation(Vector3.up * gap, Quaternion.Euler(90f, 0f, 0f));
            floorTrack.transform.localScale = trackSize;

            GameObject ceilingTrack = CreateRenderOnlyPrimitive(PrimitiveType.Quad, "Ceiling Track", track);
            ceilingTrack.transform.SetParent(door, false);
            ceilingTrack.transform.SetLocalPositionAndRotation(Vector3.up * (wallHeight - gap), Quaternion.Euler(-90f, 0f, 0f));
            ceilingTrack.transform.localScale = trackSize;

            foreach (float side in new[] { -1f, 1f })
            {
                var position = new Vector3(0f, wallHeight, side * tileSize * 0.3f);
                if (signalBulbs != null)
                    CreateSignalLamp(door, position, signalBulbs, signalLights);
                else
                    CreateDoorLamp(door, position);
            }
        }

        private static void SetWallStyle(SerializedProperty style, char symbol, Material material)
        {
            style.FindPropertyRelative("symbol").intValue = symbol;
            style.FindPropertyRelative("material").objectReferenceValue = material;
        }

        private static LevelLayout GetOrCreateLayout()
        {
            var layout = AssetDatabase.LoadAssetAtPath<LevelLayout>(LayoutPath);
            if (layout != null)
                return layout;

            layout = ScriptableObject.CreateInstance<LevelLayout>();
            AssetDatabase.CreateAsset(layout, LayoutPath);
            SetFields(layout,
                ("map", string.Join("\n", TestLevelMap)),
                ("zones", string.Join("\n", TestLevelZones)),
                ("decor", string.Join("\n", TestLevelDecor)));
            return layout;
        }

        private static void EnsureFolder(string folderPath)
        {
            // Path.GetDirectoryName gives Windows separators, which the asset database does not take.
            if (!AssetDatabase.IsValidFolder(folderPath))
                AssetDatabase.CreateFolder(Path.GetDirectoryName(folderPath).Replace('\\', '/'), Path.GetFileName(folderPath));
        }

        private static PlayerController InstantiatePlayer(Pose spawnPose)
        {
            if (playerPrefab == null)
                playerPrefab = BuildPlayerPrefab();
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(playerPrefab);
            instance.transform.SetPositionAndRotation(spawnPose.position, spawnPose.rotation);
            return instance.GetComponent<PlayerController>();
        }

        private static GameObject BuildPlayerPrefab()
        {
            var root = new GameObject("Player") { tag = "Player" };

            var characterController = root.AddComponent<CharacterController>();
            characterController.height = 1.8f;
            characterController.radius = 0.4f;
            characterController.center = new Vector3(0f, 0.9f, 0f);
            characterController.stepOffset = 0.3f;
            characterController.slopeLimit = 45f;
            characterController.skinWidth = 0.08f;
            characterController.minMoveDistance = 0f;

            var cameraPivot = new GameObject("CameraPivot").transform;
            cameraPivot.SetParent(root.transform, false);
            cameraPivot.localPosition = new Vector3(0f, EyeHeight, 0f);
            Transform playerCamera = CreatePlayerCamera(cameraPivot);

            root.AddComponent<PlayerMovement>();
            SetFields(root.AddComponent<PlayerLook>(), ("cameraPivot", cameraPivot));
            SetFields(root.AddComponent<InteractionController>(), ("viewPoint", playerCamera));
            root.AddComponent<Health>();
            AddWeapon(root, playerCamera);
            root.AddComponent<Score>();
            root.AddComponent<KeyRing>();
            root.AddComponent<PickupCollector>();
            SetFields(root.AddComponent<PlayerController>(),
                ("moveAction", FindActionReference("Player", "Move")),
                ("lookAction", FindActionReference("Player", "Look")),
                ("jumpAction", FindActionReference("Player", "Jump")),
                ("interactAction", FindActionReference("Player", "Interact")),
                ("attackAction", FindActionReference("Player", "Attack")));

            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, PlayerPrefabPath);
            Object.DestroyImmediate(root);
            return prefab;
        }

        private static Transform CreatePlayerCamera(Transform cameraPivot)
        {
            var cameraObject = new GameObject("PlayerCamera") { tag = "MainCamera" };
            cameraObject.transform.SetParent(cameraPivot, false);

            // A 60 degree vertical view is about 90 degrees across a widescreen display: wide enough for a shooter,
            // without stretching the edges of the screen. The near plane is close enough for the weapon model.
            var camera = cameraObject.AddComponent<Camera>();
            camera.fieldOfView = 60f;
            camera.nearClipPlane = 0.05f;
            camera.farClipPlane = 150f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = Color.black;

            // Anti-aliasing comes from the pipeline asset's MSAA, which keeps point-filtered textures crisp.
            UniversalAdditionalCameraData cameraData = camera.GetUniversalAdditionalCameraData();
            cameraData.renderPostProcessing = true;
            cameraData.antialiasing = AntialiasingMode.None;

            cameraObject.AddComponent<AudioListener>();
            return cameraObject.transform;
        }

        private static InputActionReference FindActionReference(string mapName, string actionName)
        {
            InputActionReference reference = AssetDatabase.LoadAllAssetsAtPath(InputActionsPath)
                .OfType<InputActionReference>()
                .FirstOrDefault(r => r.action != null && r.action.actionMap.name == mapName && r.action.name == actionName);

            if (reference == null)
                throw new InvalidOperationException($"Input action '{mapName}/{actionName}' was not found in {InputActionsPath}.");

            return reference;
        }

        // A lit material without environment reflections, which this enclosed level has nothing to show in.
        // Only surfaces with some shine keep specular highlights, and emission lets a surface glow where no lamp reaches.
        private static Material GetOrCreateMaterial(string materialName, Color baseColor, Texture2D baseMap = null,
            float smoothness = 0f, float metallic = 0f, Texture2D normalMap = null, Color emission = default)
        {
            string path = $"{MaterialFolder}/{materialName}.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material != null)
                return material;

            material = new Material(Shader.Find(LitShaderName));
            material.SetColor("_BaseColor", baseColor);
            material.SetTexture("_BaseMap", baseMap);
            material.SetFloat("_Smoothness", smoothness);
            material.SetFloat("_Metallic", metallic);
            material.SetFloat("_EnvironmentReflections", 0f);
            material.EnableKeyword("_ENVIRONMENTREFLECTIONS_OFF");

            if (smoothness <= 0f && metallic <= 0f)
            {
                material.SetFloat("_SpecularHighlights", 0f);
                material.EnableKeyword("_SPECULARHIGHLIGHTS_OFF");
            }

            if (normalMap != null)
            {
                material.SetTexture("_BumpMap", normalMap);
                material.EnableKeyword("_NORMALMAP");
            }

            if (emission.maxColorComponent > 0f)
            {
                material.SetColor("_EmissionColor", emission);
                material.EnableKeyword("_EMISSION");
                material.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
            }

            AssetDatabase.CreateAsset(material, path);
            return material;
        }

        private static Texture2D GetOrCreateTexture(string textureName, Func<Texture2D> generate,
            TextureWrapMode wrapMode = TextureWrapMode.Repeat, FilterMode filterMode = FilterMode.Point)
        {
            string path = $"{TextureFolder}/{textureName}.png";
            if (!File.Exists(path))
                WriteTexture(path, generate(), wrapMode, filterMode);

            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        private static Color Opaque(Color colour)
        {
            colour.a = 1f;
            return colour;
        }

        private static Texture2D CreateTexture(Color[] pixels, int size) => CreateTexture(pixels, size, size);

        private static Texture2D CreateTexture(Color[] pixels, int width, int height)
        {
            var texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
            texture.SetPixels(pixels);
            texture.Apply();
            return texture;
        }

        private static void SetFields(Object target, params (string name, object value)[] fields)
        {
            var serializedObject = new SerializedObject(target);
            Set(serializedObject, fields);
            serializedObject.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
