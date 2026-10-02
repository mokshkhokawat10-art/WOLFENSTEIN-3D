using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Wolfenstein.Audio;
using Wolfenstein.Core;
using Wolfenstein.Enemies;
using Wolfenstein.Interaction;
using Wolfenstein.Monsters;
using Wolfenstein.Pickups;
using Wolfenstein.Player;
using Wolfenstein.Progression;
using Wolfenstein.UI;
using Wolfenstein.Waves;
using Wolfenstein.Weapons;
using Wolfenstein.World;
using Object = UnityEngine.Object;

namespace Wolfenstein.Editor
{
    // The episode: the title screen and two levels, each a layout, a wave set and a level definition, played in scenes of
    // their own. The test level stays as it is, the range the systems' own tests run in. A level's scene is the test level's
    // scene plus the progression that makes it a level: zones tracked, doors locked until earned, objectives, encounters,
    // an exit and the statistics of the tally.
    // Decor markers of the levels, beside the props and the arena's markers:
    //   'Y' 'W' a door held by the security key    'P' a door held by the power switch    'Z' the power switch on a wall
    //   'E'     the exit door                      '6' '7' '8' points an encounter's monsters rise at
    // Layouts, wave sets, definitions, the key and the switch are only created when missing; the scenes are always rebuilt.
    public static partial class TestLevelBuilder
    {
        private const string TitleScenePath = "Assets/Scenes/MainMenu.unity";
        private const string Level1ScenePath = "Assets/Scenes/Level1.unity";
        private const string Level2ScenePath = "Assets/Scenes/Level2.unity";
        private const string Level1LayoutPath = "Assets/Levels/Level1_Layout.asset";
        private const string Level2LayoutPath = "Assets/Levels/Level2_Layout.asset";
        private const string Level1DefinitionPath = "Assets/Levels/Level1.asset";
        private const string Level2DefinitionPath = "Assets/Levels/Level2.asset";
        private const string Level1WavesPath = "Assets/Monsters/Level1Waves.asset";
        private const string Level2WavesPath = "Assets/Monsters/Level2Waves.asset";
        private const string KeyFolder = "Assets/Levels/Keys";
        private const string PowerSwitchPath = "Assets/Prefabs/Props/PowerSwitch.prefab";

        // Level 1, THE BUNKER: the test level's map made into a level. The security key lies in the officers' quarters (the
        // command room, east), and both ways into the security wing and on to the arena are locked until the player has it.
        // Ambushes wait in the corridor outside the spawn, in the storage room, and in the security wing; lurkers in the dark
        // corridor and the security room; the arena's four waves; and the lift to the north once they are beaten.
        private static readonly string[] Level1Map =
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
            "#.E.......#####%........y%",
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

        private static readonly string[] Level1Decor =
        {
            "..........................",
            "..........p...p...........",
            "..........................",
            ".......@..b...L...@.......",
            ".......@....E.....@.......",
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
            ".....1...........81...38..",
            ".....D...........S.....S..",
            ".....W..............Y.....",
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
            ".......m...b..g.B.....e6b.",
            ".........6................",
            ".........q....S.........r.",
            ".C.s.c...6................",
            ".7....u.......k.........f.",
            ".s.....B..................",
            "...7b.7.......L..q......k.",
            "..........................",
        };

        // Level 2, THE POWER STATION: the power is out. The switch that restores it is in the generator hall, east of the
        // arrival lift; throwing it opens the power door north of the corridor and wakes what was waiting in the dark. Beyond
        // the door, a brute and a spitter lurk before the arena; after its four waves, the lift north is the way out.
        private static readonly string[] Level2Map =
        {
            "########################",
            "########.....###########",
            "########.....###########",
            "########.....###########",
            "##########-#############",
            "###.................####",
            "###..P.........P....####",
            "###.................####",
            "###......MM.........####",
            "###.................####",
            "###..P.........P....####",
            "###.................####",
            "###.................####",
            "##########-#############",
            "########.....###########",
            "########f....###########",
            "##########-#############",
            "###c.......g###a......##",
            "###.#######.###...W...##",
            "###-#######...|.......##",
            "#a...c#########......t##",
            "#..^..#########h....W.##",
            "########################",
        };

        private static readonly string[] Level2Zones =
        {
            "........................",
            "........xxxxx...........",
            "........xxxxx...........",
            "........xxxxx...........",
            "..........x.............",
            "...aaaaaaaaaaaaaaaaa....",
            "...aaaaaaaaaaaaaaaaa....",
            "...aaaaaaaaaaaaaaaaa....",
            "...aaaaaaaaaaaaaaaaa....",
            "...aaaaaaaaaaaaaaaaa....",
            "...aaaaaaaaaaaaaaaaa....",
            "...aaaaaaaaaaaaaaaaa....",
            "...aaaaaaaaaaaaaaaaa....",
            "..........a.............",
            "........rrrrr...........",
            "........rrrrr...........",
            "..........r.............",
            "...ddddddddd...iiiiiii..",
            "...d.......d...iiiiiii..",
            "...........ddd.iiiiiii..",
            ".mmmmm.........iiiiiii..",
            ".mmmmm.........iiiiiii..",
            "........................",
        };

        private static readonly string[] Level2Decor =
        {
            "........................",
            "........p...............",
            "..........h.............",
            "........L...L...........",
            "..........E.............",
            "...@.....X.X.......@....",
            "..........+.............",
            "...@....................",
            "...................@....",
            ".....+.............+....",
            "...................@....",
            "...@.......+............",
            "...@......S........@....",
            "........................",
            "........3...2...........",
            "........................",
            "..........P.............",
            "....7...7.......e..6m...",
            "...h.......h.........6..",
            ".....................Z..",
            ".k......................",
            ".L.................6....",
            "........................",
        };

        // What makes a scene a level rather than a test range.
        private sealed class Progress
        {
            public LevelDefinition Level;
            public ZoneTracker Zones;
            public AccessControl Access;
            public ObjectiveTracker Objectives;
            public EncounterDirector Encounters;
            public LevelExit Exit;
            public LevelStatistics Statistics;
        }

        // ---- Level data ----------------------------------------------------------------------------------------------------

        private static LevelDefinition GetOrCreateLevel2() =>
            GetOrCreateLevelDefinition(Level2DefinitionPath, () =>
            {
                MonsterDefinition stalker = MonsterDefinitionAsset("Stalker"), brute = MonsterDefinitionAsset("Brute");
                MonsterDefinition spitter = MonsterDefinitionAsset("Spitter"), demon = MonsterDefinitionAsset("Demon");
                return LevelDefinition.Create(2, "THE POWER STATION", SceneName(Level2ScenePath),
                    GetOrCreateLevelLayout(Level2LayoutPath, Level2Map, Level2Zones, Level2Decor), 100, 24,
                    new[]
                    {
                        new ObjectiveDefinition(ObjectiveKind.ThrowSwitch, "RESTORE THE POWER", target: 'Z'),
                        new ObjectiveDefinition(ObjectiveKind.ReachZone, "REACH THE ARENA", target: 'a'),
                        new ObjectiveDefinition(ObjectiveKind.ClearArena, "SURVIVE THE ARENA"),
                        new ObjectiveDefinition(ObjectiveKind.ReachExit, "REACH THE LIFT"),
                    },
                    new[] { new DoorLock('P', "POWER DOOR", null, 'Z', "NO POWER - RESTORE THE POWER", "POWER RESTORED") },
                    new[]
                    {
                        new EncounterDefinition("GENERATOR HALL", 'i', '\0', '6', new WaveGroup(brute, 1), new WaveGroup(spitter, 2)),
                        new EncounterDefinition("POWER SURGE", '\0', 'Z', '7', new WaveGroup(demon, 2), new WaveGroup(stalker, 2)),
                    },
                    GetOrCreateLevel2Waves(), 'x', 'E', null);
            });

        private static LevelDefinition GetOrCreateLevel1(LevelDefinition next) =>
            GetOrCreateLevelDefinition(Level1DefinitionPath, () =>
            {
                AccessKey securityKey = GetOrCreateSecurityKey();
                MonsterDefinition stalker = MonsterDefinitionAsset("Stalker"), brute = MonsterDefinitionAsset("Brute");
                MonsterDefinition spitter = MonsterDefinitionAsset("Spitter"), demon = MonsterDefinitionAsset("Demon");
                return LevelDefinition.Create(1, "THE BUNKER", SceneName(Level1ScenePath),
                    GetOrCreateLevelLayout(Level1LayoutPath, Level1Map, TestLevelZones, Level1Decor), 100, 24,
                    new[]
                    {
                        new ObjectiveDefinition(ObjectiveKind.CollectKey, "FIND THE SECURITY KEY", securityKey),
                        new ObjectiveDefinition(ObjectiveKind.ReachZone, "REACH THE ARENA", target: 'a'),
                        new ObjectiveDefinition(ObjectiveKind.ClearArena, "SURVIVE THE ARENA"),
                        new ObjectiveDefinition(ObjectiveKind.ReachExit, "REACH THE EXIT LIFT"),
                    },
                    new[]
                    {
                        new DoorLock('Y', "SECURITY DOOR", securityKey, '\0', "LOCKED - FIND THE SECURITY KEY", "SECURITY ACCESS GRANTED"),
                        new DoorLock('W', "SECURITY WING", securityKey, '\0', "LOCKED - FIND THE SECURITY KEY", string.Empty),
                    },
                    new[]
                    {
                        new EncounterDefinition("CORRIDOR AMBUSH", 'i', '\0', '6', new WaveGroup(stalker, 3)),
                        new EncounterDefinition("STORAGE", 'w', '\0', '7', new WaveGroup(stalker, 2), new WaveGroup(spitter, 1)),
                        new EncounterDefinition("SECURITY WING", 'r', '\0', '8', new WaveGroup(brute, 1), new WaveGroup(demon, 1)),
                    },
                    GetOrCreateLevel1Waves(), 'x', 'E', next);
            });

        private static LevelDefinition GetOrCreateLevelDefinition(string path, Func<LevelDefinition> create)
        {
            var existing = AssetDatabase.LoadAssetAtPath<LevelDefinition>(path);
            if (existing != null)
                return existing;

            LevelDefinition level = create();
            AssetDatabase.CreateAsset(level, path);
            return level;
        }

        private static LevelLayout GetOrCreateLevelLayout(string path, string[] map, string[] zones, string[] decor)
        {
            var layout = AssetDatabase.LoadAssetAtPath<LevelLayout>(path);
            if (layout != null)
                return layout;

            layout = ScriptableObject.CreateInstance<LevelLayout>();
            AssetDatabase.CreateAsset(layout, path);
            SetFields(layout,
                ("map", string.Join("\n", map)),
                ("zones", string.Join("\n", zones)),
                ("decor", string.Join("\n", decor)));
            return layout;
        }

        // A first taste: stalkers, then spitters among them, then the first brutes, and a final wave led in by a revenant.
        private static WaveSet GetOrCreateLevel1Waves() => GetOrCreateWaveSet(Level1WavesPath, () =>
        {
            MonsterDefinition stalker = MonsterDefinitionAsset("Stalker"), brute = MonsterDefinitionAsset("Brute");
            MonsterDefinition spitter = MonsterDefinitionAsset("Spitter"), demon = MonsterDefinitionAsset("Demon");
            MonsterDefinition revenant = MonsterDefinitionAsset("Revenant");
            Pickup clip = GetOrCreateAmmoClip();
            Pickup kit = GetOrCreatePickup("FirstAidKit", health: 25, createModel: CreateFirstAidKitModel);
            Pickup food = GetOrCreatePickup("Food", health: 10, createModel: CreateFoodModel);
            return WaveSet.Create(2.5f, 6f,
                new Wave(new[] { new WaveGroup(stalker, 6) }, MonsterModifiers.None, 0.8f, 6, new[] { clip, food }),
                new Wave(new[] { new WaveGroup(stalker, 8), new WaveGroup(spitter, 2) }, MonsterModifiers.None, 0.7f, 8, new[] { clip, clip, kit }),
                new Wave(new[] { new WaveGroup(stalker, 6), new WaveGroup(spitter, 3), new WaveGroup(brute, 2) },
                    new MonsterModifiers(1.05f, 1.02f, 1.05f), 0.6f, 10, new[] { clip, clip, kit }),
                new Wave(new[] { new WaveGroup(stalker, 8), new WaveGroup(spitter, 2), new WaveGroup(brute, 2), new WaveGroup(demon, 2),
                        new WaveGroup(revenant, 1, spawnLast: true) },
                    new MonsterModifiers(1.1f, 1.04f, 1.1f), 0.5f, 12, Array.Empty<Pickup>()));
        });

        // Harder from the first wave: brutes and demons early, and two revenants to finish.
        private static WaveSet GetOrCreateLevel2Waves() => GetOrCreateWaveSet(Level2WavesPath, () =>
        {
            MonsterDefinition stalker = MonsterDefinitionAsset("Stalker"), brute = MonsterDefinitionAsset("Brute");
            MonsterDefinition spitter = MonsterDefinitionAsset("Spitter"), demon = MonsterDefinitionAsset("Demon");
            MonsterDefinition revenant = MonsterDefinitionAsset("Revenant");
            Pickup clip = GetOrCreateAmmoClip();
            Pickup kit = GetOrCreatePickup("FirstAidKit", health: 25, createModel: CreateFirstAidKitModel);
            Pickup food = GetOrCreatePickup("Food", health: 10, createModel: CreateFoodModel);
            return WaveSet.Create(2.5f, 6f,
                new Wave(new[] { new WaveGroup(stalker, 8), new WaveGroup(spitter, 2) }, new MonsterModifiers(1.1f, 1.03f, 1.1f), 0.6f, 10,
                    new[] { clip, clip, food }),
                new Wave(new[] { new WaveGroup(stalker, 8), new WaveGroup(spitter, 3), new WaveGroup(brute, 2) },
                    new MonsterModifiers(1.15f, 1.05f, 1.15f), 0.55f, 12, new[] { clip, clip, kit }),
                new Wave(new[] { new WaveGroup(stalker, 10), new WaveGroup(spitter, 3), new WaveGroup(brute, 2), new WaveGroup(demon, 2) },
                    new MonsterModifiers(1.2f, 1.07f, 1.2f), 0.5f, 14, new[] { clip, clip, clip, kit }),
                new Wave(new[] { new WaveGroup(stalker, 10), new WaveGroup(spitter, 3), new WaveGroup(brute, 3), new WaveGroup(demon, 2),
                        new WaveGroup(revenant, 2, spawnLast: true) },
                    new MonsterModifiers(1.25f, 1.1f, 1.25f), 0.45f, 16, Array.Empty<Pickup>()));
        });

        private static WaveSet GetOrCreateWaveSet(string path, Func<WaveSet> create)
        {
            var existing = AssetDatabase.LoadAssetAtPath<WaveSet>(path);
            if (existing != null)
                return existing;

            WaveSet set = create();
            AssetDatabase.CreateAsset(set, path);
            return set;
        }

        private static MonsterDefinition MonsterDefinitionAsset(string monsterName) =>
            AssetDatabase.LoadAssetAtPath<MonsterDefinition>($"{MonsterDefinitionFolder}/{monsterName}.asset");

        private static string SceneName(string scenePath) => System.IO.Path.GetFileNameWithoutExtension(scenePath);

        // ---- The key and the switch ----------------------------------------------------------------------------------------

        private static AccessKey GetOrCreateSecurityKey()
        {
            string path = $"{KeyFolder}/SecurityKey.asset";
            var key = AssetDatabase.LoadAssetAtPath<AccessKey>(path);
            if (key != null)
                return key;

            EnsureFolderTree(KeyFolder);
            key = ScriptableObject.CreateInstance<AccessKey>();
            AssetDatabase.CreateAsset(key, path);
            SetFields(key, ("displayName", "SECURITY KEY"), ("colour", new Color(0.9f, 0.12f, 0.1f)));
            return key;
        }

        // The key item: collected like any other, it goes on the player's key ring.
        private static Pickup GetOrCreateSecurityKeyPickup()
        {
            Pickup pickup = GetOrCreatePickup("SecurityKey", CreateKeyModel);
            FillIfEmpty(pickup.Definition, ("accessKey", GetOrCreateSecurityKey()));
            return pickup;
        }

        // A large brass key with a red bow, standing on its bit so it reads at a distance.
        private static void CreateKeyModel(Transform model)
        {
            Material brass = GetOrCreateGoldMaterial();
            Material red = RedMaterial();
            Material dark = TrimDarkSteel();

            CreateModelPart(model, "Bow", PrimitiveType.Cylinder, new Vector3(0f, 0.38f, 0f), new Vector3(0.22f, 0.03f, 0.22f), Quaternion.Euler(90f, 0f, 0f), red);
            CreateModelPart(model, "Bow Hole", PrimitiveType.Cylinder, new Vector3(0f, 0.38f, 0f), new Vector3(0.08f, 0.035f, 0.08f), Quaternion.Euler(90f, 0f, 0f), dark);
            CreateModelPart(model, "Shaft", PrimitiveType.Cube, new Vector3(0f, 0.17f, 0f), new Vector3(0.05f, 0.3f, 0.04f), Quaternion.identity, brass);
            CreateModelPart(model, "Tooth", PrimitiveType.Cube, new Vector3(0.05f, 0.05f, 0f), new Vector3(0.06f, 0.05f, 0.035f), Quaternion.identity, brass);
            CreateModelPart(model, "Tooth", PrimitiveType.Cube, new Vector3(0.045f, 0.13f, 0f), new Vector3(0.05f, 0.04f, 0.035f), Quaternion.identity, brass);
        }

        // A heavy lever on a steel plate with a signal lamp: the power switch, red until thrown and green after. Its origin is the
        // middle of its back, and it faces +Z out of the wall. A trigger box over it lets the player use it without it
        // stopping shots.
        private static GameObject GetOrCreatePowerSwitch()
        {
            var existing = AssetDatabase.LoadAssetAtPath<GameObject>(PowerSwitchPath);
            if (existing != null)
                return existing;

            EnsureFolder(PropFolder);
            var root = new GameObject("PowerSwitch");
            Material plate = PaintedSteel("Prop_SwitchPlate", new Color(0.3f, 0.32f, 0.3f));
            Material hazard = GetOrCreateMaterial("Sign_Yellow", HazardYellow, smoothness: 0.3f, emission: HazardYellow * 0.15f);
            Box(root.transform, "Plate", new Vector3(0f, 0f, 0.03f), new Vector3(0.5f, 0.72f, 0.06f), plate);
            Box(root.transform, "Label", new Vector3(0f, -0.26f, 0.061f), new Vector3(0.3f, 0.08f, 0.004f), hazard);

            var pivot = new GameObject("Lever").transform;
            pivot.SetParent(root.transform, false);
            pivot.SetLocalPositionAndRotation(new Vector3(0f, -0.02f, 0.08f), Quaternion.Euler(20f, 0f, 0f));
            CreateModelPart(pivot, "Arm", PrimitiveType.Cube, new Vector3(0f, 0.15f, 0f), new Vector3(0.05f, 0.3f, 0.05f), Quaternion.identity, TrimDarkSteel());
            CreateModelPart(pivot, "Handle", PrimitiveType.Cylinder, new Vector3(0f, 0.3f, 0f), new Vector3(0.07f, 0.09f, 0.07f),
                Quaternion.Euler(0f, 0f, 90f), RedMaterial());

            GameObject lamp = CreateModelPart(root.transform, "Lamp", PrimitiveType.Sphere, new Vector3(0.17f, 0.26f, 0.07f), Vector3.one * 0.07f,
                Quaternion.identity, GetOrCreateGlowMaterial("Lamp_Signal", Color.white * 3f));
            var light = new GameObject("Lamp Light").AddComponent<Light>();
            light.transform.SetParent(root.transform, false);
            light.transform.localPosition = new Vector3(0.17f, 0.26f, 0.25f);
            light.type = LightType.Point;
            light.range = 2.5f;
            light.intensity = 1.2f;
            light.shadows = LightShadows.None;

            var trigger = root.AddComponent<BoxCollider>();
            trigger.isTrigger = true;
            trigger.center = new Vector3(0f, 0f, 0.2f);
            trigger.size = new Vector3(0.7f, 0.9f, 0.4f);

            SetFields(root.AddComponent<WallSwitch>(),
                ("channel", 'Z'), ("lever", pivot), ("thrownAngles", new Vector3(140f, 0f, 0f)),
                ("lamp", lamp.GetComponent<Renderer>()), ("lampLight", light));

            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, PowerSwitchPath);
            Object.DestroyImmediate(root);
            return prefab;
        }

        // ---- Scene -------------------------------------------------------------------------------------------------------

        private static Progress CreateProgress(LevelDefinition level, WorldManager world, LevelDressing dressing, PlayerController player,
            PickupManager pickups, EnemyManager enemies, MonsterManager monsters, WaveManager waves)
        {
            var root = new GameObject("Progression");
            var progress = new Progress
            {
                Level = level,
                Zones = AddChild<ZoneTracker>(root.transform, "Zones"),
                Access = AddChild<AccessControl>(root.transform, "Access"),
                Objectives = AddChild<ObjectiveTracker>(root.transform, "Objectives"),
                Encounters = AddChild<EncounterDirector>(root.transform, "Encounters"),
                Exit = AddChild<LevelExit>(root.transform, "Exit"),
                Statistics = AddChild<LevelStatistics>(root.transform, "Statistics"),
            };

            var interaction = player.GetComponent<InteractionController>();
            var keys = player.GetComponent<KeyRing>();
            SetFields(progress.Zones, ("world", world), ("player", player.transform));
            SetFields(progress.Access, ("level", level), ("world", world), ("dressing", dressing), ("keys", keys), ("interaction", interaction));
            SetFields(progress.Objectives, ("level", level), ("keys", keys), ("access", progress.Access), ("zones", progress.Zones),
                ("waves", waves), ("exit", progress.Exit));
            SetFields(progress.Encounters, ("level", level), ("world", world), ("monsters", monsters), ("zones", progress.Zones),
                ("access", progress.Access), ("player", player.transform));
            SetFields(progress.Exit, ("level", level), ("world", world), ("zones", progress.Zones), ("objectives", progress.Objectives),
                ("interaction", interaction));
            SetFields(progress.Statistics, ("level", level), ("score", player.GetComponent<Score>()),
                ("weapon", player.GetComponent<WeaponController>()), ("collector", player.GetComponent<PickupCollector>()),
                ("pickups", pickups), ("enemies", enemies), ("monsters", monsters), ("encounters", progress.Encounters));
            return progress;
        }

        // The title screen: the game's name and a prompt to start, over the menu music, in the heads-up display's lettering.
        private static void BuildTitleScene(string firstLevelPath)
        {
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var firstLevel = AssetDatabase.LoadAssetAtPath<LevelDefinition>(firstLevelPath);
            var gameManager = new GameObject("GameManager").AddComponent<GameManager>();

            var cameraObject = new GameObject("Camera") { tag = "MainCamera" };
            var camera = cameraObject.AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = Color.black;
            cameraObject.AddComponent<AudioListener>();

            var canvasObject = new GameObject("Title Screen");
            var canvas = canvasObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.pixelPerfect = true;
            var scaler = canvasObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = HudReferenceResolution;
            scaler.matchWidthOrHeight = 1f;
            var canvasRect = (RectTransform)canvasObject.transform;
            Font font = GetOrCreatePixelFont();

            RectTransform band = CreateRect(canvasRect, "Band", new Vector2(0f, 0.5f), new Vector2(1f, 0.5f), new Vector2(0f, 160f), new Vector2(0f, 300f));
            AddImage(band, new Color(0.25f, 0.02f, 0.01f, 0.9f));
            AddImage(CreateEdge(band, "Top Edge", Vector2.up, 6f, 0f), new Color(0.6f, 0.08f, 0.04f));
            AddImage(CreateEdge(band, "Bottom Edge", Vector2.down, 6f, 0f), new Color(0.6f, 0.08f, 0.04f));
            Text title = AddText(CreateRect(band, "Title", ScreenCentre, ScreenCentre, new Vector2(0f, 20f), new Vector2(1800f, 160f)), font,
                string.Empty, TitleSize, new Color(1f, 0.92f, 0.85f));
            AddText(CreateRect(band, "Subtitle", ScreenCentre, ScreenCentre, new Vector2(0f, -95f), new Vector2(1800f, 50f)), font,
                "EPISODE 1", PromptSize * 3 / 2, CaptionColour);

            Text message = AddText(CreateRect(canvasRect, "Message", ScreenCentre, ScreenCentre, new Vector2(0f, -60f), new Vector2(1800f, 60f)),
                font, string.Empty, PromptSize * 3 / 2, new Color(1f, 0.82f, 0.3f));
            Text prompt = AddText(CreateRect(canvasRect, "Prompt", ScreenCentre, ScreenCentre, new Vector2(0f, -170f), new Vector2(1800f, 60f)),
                font, "PRESS ENTER TO START", PromptSize * 3 / 2, Color.white);
            AddText(CreateRect(canvasRect, "Controls", new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 90f), new Vector2(1800f, 40f)),
                font, "WASD MOVE - MOUSE LOOK - CLICK FIRE - E USE - SPACE JUMP", PromptSize, CaptionColour);

            var titleScreen = canvasObject.AddComponent<TitleScreen>();
            SetFields(titleScreen,
                ("gameManager", gameManager), ("firstLevel", firstLevel), ("startAction", FindActionReference("UI", "Submit")),
                ("title", title), ("message", message), ("prompt", prompt.gameObject));

            var audioRoot = new GameObject("Audio");
            var audioManager = audioRoot.AddComponent<AudioManager>();
            SetFields(audioManager, ("listener", cameraObject.transform));
            SetFields(AddChild<MusicDirector>(audioRoot.transform, "Music"),
                ("audioManager", audioManager), ("music", GetOrCreateMusic()), ("menuScreen", true));
            SetFields(AddChild<ProgressionAudio>(audioRoot.transform, "Progression"),
                ("audioManager", audioManager), ("title", titleScreen),
                ("levelTransition", Flat("Interface/LevelTransition", AudioCategory.Ui, 0.7f, voices: 1, priority: 5)));

            EditorSceneManager.SaveScene(scene, TitleScenePath);
        }
    }
}
