using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Wolfenstein.Audio;
using Wolfenstein.Combat;
using Wolfenstein.Core;
using Wolfenstein.Editor.Synthesis;
using Wolfenstein.Enemies;
using Wolfenstein.Interaction;
using Wolfenstein.Monsters;
using Wolfenstein.Pickups;
using Wolfenstein.Player;
using Wolfenstein.UI;
using Wolfenstein.Waves;
using Wolfenstein.World;
using Object = UnityEngine.Object;

namespace Wolfenstein.Editor
{
    // Sound: the clips, synthesized by the recipes of the SoundLibrary; the cues that say how each one plays; the sound sets
    // of the creatures, doors and music; the sounds of the pistol, the items, the zones, the doors and the machines; and the
    // scene's audio manager with the directors that play it all. Clips, cues and sound sets are only created when missing,
    // so a clip can be swapped for a real recording or a cue retuned and survive a rebuild. Sound fields on existing tuning
    // assets, themes and prefabs are only filled in while empty. The audio objects in the scene are always regenerated.
    public static partial class TestLevelBuilder
    {
        private const string ClipFolder = "Assets/Audio/Clips";
        private const string CueFolder = "Assets/Audio/Cues";
        private const string SoundSetFolder = "Assets/Audio/Sets";

        // The clips, cues and sound fields every scene of one build shares, made or filled in once per build.
        private static bool audioAssetsReady;
        private static DoorSounds standardDoorSounds;

        private static void PrepareAudioAssets()
        {
            if (audioAssetsReady)
                return;

            GenerateMissingClips();
            GiveWeaponsSounds();
            GiveItemsSounds();
            GiveCreaturesSounds();
            standardDoorSounds = GiveDoorsSounds();
            GiveZonesSounds();
            GiveMachinesSounds();
            audioAssetsReady = true;
        }

        private static void CreateAudio(WorldManager world, PlayerController player, EnemyManager enemies, MonsterManager monsters,
            WaveManager waves, GameManager gameManager, LevelManager levelManager, Progress progress)
        {
            PrepareAudioAssets();
            DoorSounds standardDoor = standardDoorSounds;

            var audioRoot = new GameObject("Audio");
            var audioManager = audioRoot.AddComponent<AudioManager>();
            SetFields(audioManager, ("listener", player.GetComponentInChildren<Camera>().transform), ("world", world));

            CreatePlayerAudio(audioRoot.transform, audioManager, player, world);
            SetFields(AddChild<CreatureAudio>(audioRoot.transform, "Creatures"),
                ("audioManager", audioManager), ("monsters", monsters), ("enemies", enemies),
                ("spawnSound", Effect("Monsters/Spawn", 0.55f, 3f, 30f, voices: 3, priority: 100, cooldown: 0.15f)));
            SetFields(AddChild<DoorAudio>(audioRoot.transform, "Doors"),
                ("audioManager", audioManager), ("world", world), ("interaction", player.GetComponent<InteractionController>()),
                ("defaultSounds", standardDoor));
            WaveAudio waveAudio = CreateWaveAudio(audioRoot.transform, audioManager, waves);
            SetFields(AddChild<MusicDirector>(audioRoot.transform, "Music"),
                ("audioManager", audioManager), ("music", GetOrCreateMusic()), ("gameManager", gameManager), ("waves", waves),
                ("waveAudio", waveAudio), ("monsters", monsters), ("enemies", enemies), ("player", player.GetComponent<Health>()));
            CreateAmbience(audioRoot.transform, audioManager, world, player, waves);
            SetFields(AddChild<InterfaceAudio>(audioRoot.transform, "Interface"),
                ("audioManager", audioManager), ("gameManager", gameManager), ("levelManager", levelManager),
                ("gameOver", Flat("Interface/GameOver", AudioCategory.Ui, 0.8f, priority: 0, ducksMusic: true)),
                ("restartReady", Flat("Interface/RestartReady", AudioCategory.Ui, 0.5f, priority: 5)));
            if (progress != null)
                CreateProgressionAudio(audioRoot.transform, audioManager, levelManager, progress);
        }

        // The sounds of getting through a level: its locks, switch, objectives, exit and tally.
        private static void CreateProgressionAudio(Transform parent, AudioManager audioManager, LevelManager levelManager, Progress progress)
        {
            SetFields(AddChild<ProgressionAudio>(parent, "Progression"),
                ("audioManager", audioManager), ("level", levelManager), ("objectives", progress.Objectives), ("access", progress.Access),
                ("exit", progress.Exit), ("tally", Object.FindFirstObjectByType<TallyScreen>()),
                ("accessGranted", Flat("Interface/AccessGranted", AudioCategory.Ui, 0.7f, voices: 1, priority: 5, cooldown: 0.5f)),
                ("switchThrown", Effect("Doors/Switch", 0.8f, 3f, 30f, voices: 1, priority: 40)),
                ("objectiveComplete", Flat("Interface/ObjectiveComplete", AudioCategory.Ui, 0.7f, voices: 1, priority: 5, cooldown: 0.3f)),
                ("exitReady", Flat("Interface/ExitReady", AudioCategory.Ui, 0.75f, voices: 1, priority: 5)),
                ("levelComplete", Flat("Interface/LevelComplete", AudioCategory.Ui, 0.85f, voices: 1, priority: 0, ducksMusic: true)),
                ("tallyLine", Flat("Interface/TallyLine", AudioCategory.Ui, 0.5f, voices: 2, priority: 10)),
                ("levelTransition", Flat("Interface/LevelTransition", AudioCategory.Ui, 0.7f, voices: 1, priority: 5)));
        }

        // ---- Clips and cues ------------------------------------------------------------------------------------------------

        // Every clip in the sound library that has no file yet is synthesized and written out, then imported by AudioImportRules.
        private static void GenerateMissingClips()
        {
            int written = 0;
            foreach (SoundRecipe recipe in SoundLibrary.All)
            {
                string path = ClipPath(recipe.Path);
                if (File.Exists(path))
                    continue;

                WavFile.Write(path, recipe.Build());
                written++;
            }

            if (written == 0)
                return;

            AssetDatabase.Refresh();
            Debug.Log($"Synthesized {written} audio clips into {ClipFolder}.");
        }

        private static string ClipPath(string recipePath) => $"{ClipFolder}/{recipePath}.wav";

        /// <summary>
        /// The cue for a clip and its variations, created with the given settings if it does not exist yet. The cue lives at the
        /// same path under the cue folder as its clips do under the clip folder.
        /// </summary>
        private static SoundCue Cue(string clips, AudioCategory category, float volume, float spatialBlend, float minDistance,
            float maxDistance, int voices, int priority, float cooldown, float pitchVariation, bool loop = false, bool ducksMusic = false)
        {
            string path = $"{CueFolder}/{clips}.asset";
            var cue = AssetDatabase.LoadAssetAtPath<SoundCue>(path);
            if (cue != null)
                return cue;

            List<AudioClip> clipAssets = SoundLibrary.PathsOf(clips)
                .Select(clip => AssetDatabase.LoadAssetAtPath<AudioClip>(ClipPath(clip)))
                .Where(clip => clip != null)
                .ToList();
            if (clipAssets.Count == 0)
                Debug.LogWarning($"No clips were found for the sound cue '{clips}'. It is created empty, waiting for a sound.");

            EnsureFolderTree(Path.GetDirectoryName(path));
            cue = ScriptableObject.CreateInstance<SoundCue>();
            AssetDatabase.CreateAsset(cue, path);
            var serializedCue = new SerializedObject(cue);
            SetArray(serializedCue.FindProperty("clips"), clipAssets);
            Set(serializedCue,
                ("category", category), ("loop", loop), ("volume", volume), ("pitchVariation", pitchVariation),
                ("spatialBlend", spatialBlend), ("minDistance", minDistance), ("maxDistance", maxDistance),
                ("cooldown", cooldown), ("maxVoices", voices), ("priority", priority), ("ducksMusic", ducksMusic));
            serializedCue.ApplyModifiedPropertiesWithoutUndo();
            return cue;
        }

        /// <summary>A sound effect that comes from its place in the level.</summary>
        private static SoundCue Effect(string clips, float volume, float minDistance, float maxDistance, int voices = 3,
            int priority = 110, float cooldown = 0f, float pitchVariation = 0.05f) =>
            Cue(clips, AudioCategory.Sfx, volume, 1f, minDistance, maxDistance, voices, priority, cooldown, pitchVariation);

        /// <summary>A sound that belongs to no place: the player's own, or the interface's.</summary>
        private static SoundCue Flat(string clips, AudioCategory category, float volume, int voices = 2, int priority = 30,
            float cooldown = 0f, float pitchVariation = 0.03f, bool ducksMusic = false) =>
            Cue(clips, category, volume, 0f, 1f, 10f, voices, priority, cooldown, pitchVariation, ducksMusic: ducksMusic);

        /// <summary>A looping bed of ambience, heard the same everywhere in its zone.</summary>
        private static SoundCue Bed(string clips, float volume) =>
            Cue(clips, AudioCategory.Ambience, volume, 0f, 1f, 10f, 1, 16, 0f, 0f, loop: true);

        /// <summary>A sound of the background that comes from a place, such as a distant clank or a machine's hum.</summary>
        private static SoundCue Background(string clips, float volume, float minDistance, float maxDistance, bool loop = false) =>
            Cue(clips, AudioCategory.Ambience, volume, 1f, minDistance, maxDistance, 2, loop ? 220 : 180, 0f, loop ? 0f : 0.08f, loop);

        // ---- Tuning assets -------------------------------------------------------------------------------------------------

        private static void GiveWeaponsSounds()
        {
            var pistol = AssetDatabase.LoadAssetAtPath<Object>(PistolPath);
            if (pistol == null)
                return;

            FillIfEmpty(pistol,
                ("fireSound", Flat("Weapons/PistolShot", AudioCategory.Sfx, 0.85f, voices: 3, priority: 10)),
                ("mechanismSound", Flat("Weapons/PistolMechanism", AudioCategory.Sfx, 0.3f, priority: 40)),
                ("dryFireSound", Flat("Weapons/DryFire", AudioCategory.Sfx, 0.6f, voices: 1, priority: 30, cooldown: 0.1f)));
        }

        private static void GiveItemsSounds()
        {
            SoundCue firstAid = PickupSound("Pickups/FirstAid", 0.6f);
            SoundCue ammo = PickupSound("Pickups/Ammo", 0.65f);
            SoundCue treasure = PickupSound("Pickups/TreasureSmall", 0.55f);
            (string item, SoundCue sound)[] items =
            {
                ("FirstAidKit", firstAid),
                ("Food", PickupSound("Pickups/Food", 0.55f)),
                ("AmmoClip", ammo),
                ("DroppedAmmoClip", ammo),
                ("Cross", treasure),
                ("Chalice", treasure),
                ("Chest", PickupSound("Pickups/TreasureLarge", 0.65f)),
                ("Crown", PickupSound("Pickups/TreasureLarge", 0.65f)),
                ("SecurityKey", PickupSound("Pickups/Key", 0.75f)),
            };

            foreach ((string item, SoundCue sound) in items)
            {
                var definition = AssetDatabase.LoadAssetAtPath<PickupDefinition>($"{PickupDefinitionFolder}/{item}.asset");
                if (definition != null)
                    FillIfEmpty(definition, ("collectSound", sound));
            }
        }

        private static SoundCue PickupSound(string clips, float volume) =>
            Flat(clips, AudioCategory.Sfx, volume, voices: 2, priority: 20, cooldown: 0.05f);

        // Each creature's voice, as its sound set, given to its tuning asset.
        private static void GiveCreaturesSounds()
        {
            (string name, Func<CreatureSounds> create)[] creatures =
            {
                ("Stalker", StalkerSounds),
                ("Brute", BruteSounds),
                ("Spitter", SpitterSounds),
                ("Revenant", RevenantSounds),
                ("Demon", DemonSounds),
            };

            foreach ((string name, Func<CreatureSounds> create) in creatures)
            {
                var definition = AssetDatabase.LoadAssetAtPath<MonsterDefinition>($"{MonsterDefinitionFolder}/{name}.asset");
                if (definition != null)
                    FillIfEmpty(definition, ("sounds", create()));
            }

            var guard = AssetDatabase.LoadAssetAtPath<EnemyDefinition>(GuardDefinitionPath);
            if (guard != null)
                FillIfEmpty(guard, ("sounds", GuardSounds()));
        }

        // Quick and high: snarls, a shriek and the patter of claws.
        private static CreatureSounds StalkerSounds() => GetOrCreateSoundSet<CreatureSounds>("Creatures/Stalker",
            ("hunt", Effect("Stalker/Hunt", 0.55f, 2.5f, 26f, voices: 3, priority: 110)),
            ("alert", Effect("Stalker/Alert", 0.7f, 3f, 30f, voices: 2, priority: 70, cooldown: 0.25f)),
            ("meleeWindup", Effect("Stalker/MeleeWindup", 0.55f, 2f, 18f)),
            ("meleeStrike", Effect("Stalker/MeleeStrike", 0.6f, 2f, 18f)),
            ("pain", Effect("Stalker/Pain", 0.55f, 2f, 22f, cooldown: 0.08f)),
            ("death", Effect("Stalker/Death", 0.75f, 3f, 32f, priority: 60)),
            ("footstep", Effect("Stalker/Footstep", 0.35f, 1.5f, 14f, voices: 4, priority: 150, pitchVariation: 0.1f)),
            ("vocalInterval", new Vector2(2.5f, 5f)), ("stepDistance", 1.3f));

        // Heard from furthest away: a deep growl, a bellow, a slam that shakes the room and heavy steps.
        private static CreatureSounds BruteSounds() => GetOrCreateSoundSet<CreatureSounds>("Creatures/Brute",
            ("hunt", Effect("Brute/Hunt", 0.8f, 4f, 38f, voices: 2, priority: 100)),
            ("alert", Effect("Brute/Alert", 0.9f, 4f, 45f, voices: 2, priority: 60, cooldown: 0.3f)),
            ("meleeWindup", Effect("Brute/MeleeWindup", 0.7f, 3f, 24f)),
            ("meleeStrike", Effect("Brute/MeleeStrike", 0.7f, 3f, 24f)),
            ("slam", Effect("Brute/Slam", 0.95f, 4f, 40f, voices: 2, priority: 50)),
            ("pain", Effect("Brute/Pain", 0.7f, 3f, 28f, cooldown: 0.1f)),
            ("death", Effect("Brute/Death", 1f, 4f, 45f, priority: 55)),
            ("footstep", Effect("Brute/Footstep", 0.65f, 3f, 26f, voices: 3, priority: 130, pitchVariation: 0.06f)),
            ("vocalInterval", new Vector2(4f, 8f)), ("stepDistance", 2.4f));

        // Hovering and wet: warbling, bubbling and chittering, a charging whine and a spit of fire. It has no footsteps.
        private static CreatureSounds SpitterSounds() => GetOrCreateSoundSet<CreatureSounds>("Creatures/Spitter",
            ("idle", Effect("Spitter/Idle", 0.4f, 2f, 20f, voices: 2, priority: 140)),
            ("hunt", Effect("Spitter/Hunt", 0.55f, 2.5f, 28f, voices: 2, priority: 110)),
            ("alert", Effect("Spitter/Alert", 0.7f, 3f, 32f, voices: 2, priority: 70, cooldown: 0.25f)),
            ("rangedWindup", Effect("Spitter/RangedWindup", 0.6f, 2.5f, 26f, voices: 2)),
            ("rangedLaunch", Effect("Spitter/RangedLaunch", 0.7f, 3f, 32f)),
            ("projectileImpact", Effect("Spitter/ProjectileImpact", 0.65f, 2.5f, 28f)),
            ("pain", Effect("Spitter/Pain", 0.55f, 2f, 24f, cooldown: 0.08f)),
            ("death", Effect("Spitter/Death", 0.8f, 3f, 34f, priority: 60)),
            ("vocalInterval", new Vector2(3f, 6f)));

        // The elite, and the loudest: a deep voice ringing in its helm, blades, fans of fire and clanking armour.
        private static CreatureSounds RevenantSounds() => GetOrCreateSoundSet<CreatureSounds>("Creatures/Revenant",
            ("hunt", Effect("Revenant/Hunt", 0.85f, 5f, 42f, voices: 2, priority: 90)),
            ("alert", Effect("Revenant/Alert", 1f, 5f, 50f, voices: 1, priority: 40, cooldown: 0.5f)),
            ("charge", Effect("Revenant/Charge", 0.9f, 4f, 45f, voices: 1, priority: 45)),
            ("meleeWindup", Effect("Revenant/MeleeWindup", 0.65f, 3f, 24f)),
            ("meleeStrike", Effect("Revenant/MeleeStrike", 0.7f, 3f, 26f)),
            ("rangedWindup", Effect("Revenant/RangedWindup", 0.65f, 3f, 30f, voices: 2)),
            ("rangedLaunch", Effect("Revenant/RangedLaunch", 0.8f, 4f, 38f)),
            ("projectileImpact", Effect("Revenant/ProjectileImpact", 0.75f, 3f, 34f)),
            ("pain", Effect("Revenant/Pain", 0.7f, 3f, 30f, cooldown: 0.12f)),
            ("death", Effect("Revenant/Death", 1f, 5f, 50f, priority: 40)),
            ("footstep", Effect("Revenant/Footstep", 0.6f, 3f, 26f, voices: 2, priority: 120, pitchVariation: 0.06f)),
            ("vocalInterval", new Vector2(4f, 7f)), ("stepDistance", 2f));

        // Between the others, and unnatural: a doubled, ring-modulated voice, fire from its claws and hard hooves.
        private static CreatureSounds DemonSounds() => GetOrCreateSoundSet<CreatureSounds>("Creatures/Demon",
            ("hunt", Effect("Demon/Hunt", 0.7f, 3f, 34f, voices: 2, priority: 105)),
            ("alert", Effect("Demon/Alert", 0.85f, 4f, 40f, voices: 2, priority: 65, cooldown: 0.3f)),
            ("meleeWindup", Effect("Demon/MeleeWindup", 0.6f, 2.5f, 20f)),
            ("meleeStrike", Effect("Demon/MeleeStrike", 0.65f, 2.5f, 20f)),
            ("rangedWindup", Effect("Demon/RangedWindup", 0.6f, 3f, 28f, voices: 2)),
            ("rangedLaunch", Effect("Demon/RangedLaunch", 0.75f, 3f, 34f)),
            ("projectileImpact", Effect("Demon/ProjectileImpact", 0.7f, 3f, 32f)),
            ("pain", Effect("Demon/Pain", 0.6f, 2.5f, 26f, cooldown: 0.1f)),
            ("death", Effect("Demon/Death", 0.85f, 4f, 40f, priority: 60)),
            ("footstep", Effect("Demon/Footstep", 0.45f, 2f, 18f, voices: 3, priority: 140, pitchVariation: 0.08f)),
            ("vocalInterval", new Vector2(3.5f, 7f)), ("stepDistance", 1.8f));

        private static CreatureSounds GuardSounds() => GetOrCreateSoundSet<CreatureSounds>("Creatures/Guard",
            ("alert", Effect("Guards/Alert", 0.8f, 3f, 36f, voices: 2, priority: 60, cooldown: 0.2f)),
            ("rangedLaunch", Effect("Guards/Gunshot", 0.8f, 3f, 45f, voices: 3, priority: 40)),
            ("pain", Effect("Guards/Pain", 0.65f, 2f, 26f, voices: 2, cooldown: 0.1f)),
            ("death", Effect("Guards/Death", 0.8f, 3f, 34f, voices: 2, priority: 60)),
            ("footstep", Effect("Guards/Footstep", 0.4f, 1.5f, 16f, voices: 3, priority: 140, pitchVariation: 0.06f)),
            ("stepDistance", 1.5f));

        // Each kind of door sounds like what it is. Returns the usual door's sounds, for doors with none of their own.
        private static DoorSounds GiveDoorsSounds()
        {
            DoorSounds standard = GetOrCreateSoundSet<DoorSounds>("Doors/Standard",
                ("open", DoorSound("Doors/Standard/Open", 0.55f, 22f)), ("close", DoorSound("Doors/Standard/Close", 0.55f, 22f)),
                ("opened", DoorSound("Doors/Standard/Opened", 0.5f, 22f)), ("closed", DoorSound("Doors/Standard/Closed", 0.65f, 26f)),
                ("locked", DoorSound("Doors/Standard/Locked", 0.7f, 18f, cooldown: 0.3f)));
            DoorSounds blast = GetOrCreateSoundSet<DoorSounds>("Doors/Blast",
                ("open", DoorSound("Doors/Blast/Open", 0.75f, 36f)), ("close", DoorSound("Doors/Blast/Close", 0.75f, 36f)),
                ("opened", DoorSound("Doors/Blast/Opened", 0.7f, 36f)), ("closed", DoorSound("Doors/Blast/Closed", 0.85f, 40f)),
                ("locked", DoorSound("Doors/Blast/Locked", 0.6f, 20f, cooldown: 0.4f)),
                ("lockEngaged", DoorSound("Doors/Blast/Lock", 0.85f, 45f, priority: 50)),
                ("unlocked", DoorSound("Doors/Blast/Unlock", 0.85f, 45f, priority: 50)));
            DoorSounds security = GetOrCreateSoundSet<DoorSounds>("Doors/Security",
                ("open", DoorSound("Doors/Security/Open", 0.5f, 22f)), ("close", DoorSound("Doors/Security/Close", 0.5f, 22f)),
                ("opened", DoorSound("Doors/Security/Opened", 0.5f, 22f)), ("closed", DoorSound("Doors/Security/Closed", 0.6f, 26f)),
                ("locked", DoorSound("Doors/Security/Locked", 0.55f, 18f, cooldown: 0.4f)),
                ("lockEngaged", DoorSound("Doors/Security/Lock", 0.7f, 30f)),
                ("unlocked", DoorSound("Doors/Security/Unlock", 0.7f, 30f)));
            DoorSounds exit = GetOrCreateSoundSet<DoorSounds>("Doors/Exit",
                ("open", DoorSound("Doors/Exit/Open", 0.6f, 24f)), ("close", DoorSound("Doors/Exit/Close", 0.6f, 24f)),
                ("opened", DoorSound("Doors/Exit/Opened", 0.55f, 24f)), ("closed", DoorSound("Doors/Exit/Closed", 0.65f, 28f)),
                ("locked", DoorSound("Doors/Exit/Locked", 0.7f, 18f, cooldown: 0.3f)),
                ("lockEngaged", DoorSound("Doors/Exit/Lock", 0.75f, 36f)),
                ("unlocked", DoorSound("Doors/Exit/Unlock", 0.8f, 45f, priority: 45)));

            GiveDoorSounds(DoorPrefabPath, standard);
            GiveDoorSounds("Assets/Prefabs/BlastDoor.prefab", blast);
            GiveDoorSounds("Assets/Prefabs/SecurityDoor.prefab", security);
            GiveDoorSounds("Assets/Prefabs/ExitDoor.prefab", exit);
            return standard;
        }

        private static SoundCue DoorSound(string clips, float volume, float maxDistance, float cooldown = 0f, int priority = 80) =>
            Effect(clips, volume, 2.5f, maxDistance, voices: 3, priority: priority, cooldown: cooldown, pitchVariation: 0.03f);

        private static void GiveDoorSounds(string prefabPath, DoorSounds sounds) => EditPrefab(prefabPath, root =>
        {
            var door = root.GetComponent<Door>();
            if (door == null || door.Sounds != null)
                return false;

            SetFields(door, ("sounds", sounds));
            return true;
        });

        // What each zone's floor and walls are made of, its ambience and the distant sounds heard in it. A theme whose
        // ambience is set has been given its sound already and is left as it is.
        private static void GiveZonesSounds()
        {
            SoundCue stone = Bed("Ambience/Stone", 0.55f);
            SoundCue electrical = Bed("Ambience/Electrical", 0.45f);
            SoundCue roomTone = Bed("Ambience/RoomTone", 0.45f);
            SoundCue clank = DistantSound("Distant/MetalClank");
            SoundCue knock = DistantSound("Distant/PipeKnock");
            SoundCue groan = DistantSound("Distant/Groan");
            SoundCue creak = DistantSound("Distant/Creak");
            SoundCue buzz = DistantSound("Distant/ElectricBuzz");
            SoundCue drip = DistantSound("Distant/Drip");
            SoundCue rumble = DistantSound("Distant/Rumble");

            GiveZoneSound("Industrial", SurfaceKind.Metal, SurfaceKind.Metal, Bed("Ambience/Industrial", 0.5f), 1f, clank, knock, creak);
            GiveZoneSound("Fortress", SurfaceKind.Stone, SurfaceKind.Stone, stone, 1f, rumble, groan);
            GiveZoneSound("Military", SurfaceKind.Concrete, SurfaceKind.Concrete, electrical, 0.85f, clank, buzz);
            GiveZoneSound("Storage", SurfaceKind.Wood, SurfaceKind.Stone, roomTone, 1f, creak, knock);
            GiveZoneSound("Arena", SurfaceKind.Concrete, SurfaceKind.Concrete, Bed("Ambience/Arena", 0.6f), 1f, rumble, clank, groan);
            GiveZoneSound("SpawnPen", SurfaceKind.Grate, SurfaceKind.Metal, Bed("Ambience/Pens", 0.55f), 1f, groan, drip);
            GiveZoneSound("DarkCorridor", SurfaceKind.Stone, SurfaceKind.Stone, stone, 0.8f, groan, drip, creak);
            GiveZoneSound("Command", SurfaceKind.Carpet, SurfaceKind.Wood, roomTone, 0.8f);
            GiveZoneSound("Security", SurfaceKind.Metal, SurfaceKind.Metal, electrical, 1f, buzz, clank);
            GiveZoneSound("Exit", SurfaceKind.Stone, SurfaceKind.Metal, Bed("Ambience/Lift", 0.45f), 1f);
        }

        private static SoundCue DistantSound(string clips) => Background(clips, 0.5f, 6f, 40f);

        private static void GiveZoneSound(string themeName, SurfaceKind floor, SurfaceKind walls, SoundCue ambience, float volume,
            params SoundCue[] distantSounds)
        {
            var theme = AssetDatabase.LoadAssetAtPath<ZoneTheme>($"{ThemeFolder}/{themeName}.asset");
            if (theme == null || theme.Ambience != null)
                return;

            var serializedTheme = new SerializedObject(theme);
            Set(serializedTheme, ("floorSurface", floor), ("wallSurface", walls), ("ambience", ambience), ("ambienceVolume", volume));
            SetArray(serializedTheme.FindProperty("distantSounds"), distantSounds);
            serializedTheme.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(theme);
        }

        // The machines and fixtures that hum where they stand.
        private static void GiveMachinesSounds()
        {
            SoundCue generator = Background("Machines/Generator", 0.55f, 2f, 16f, loop: true);
            SoundCue hum = Background("Machines/ElectricHum", 0.35f, 1.5f, 10f, loop: true);
            SoundCue vent = Background("Machines/Vent", 0.35f, 1.5f, 10f, loop: true);
            GiveMachineSound($"{PropFolder}/Generator.prefab", generator, new Vector3(0f, 0.8f, 0f));
            GiveMachineSound($"{WallPrefabFolder}/Machinery.prefab", generator, new Vector3(0f, 1.5f, 0f));
            GiveMachineSound($"{PropFolder}/FuseBox.prefab", hum, new Vector3(0f, 0f, 0.2f));
            GiveMachineSound($"{FixtureFolder}/Vent.prefab", vent, new Vector3(0f, 0f, 0.2f));
        }

        private static void GiveMachineSound(string prefabPath, SoundCue sound, Vector3 offset) => EditPrefab(prefabPath, root =>
        {
            if (root.GetComponent<AmbientSoundSource>() != null)
                return false;

            SetFields(root.AddComponent<AmbientSoundSource>(), ("sound", sound), ("offset", offset));
            return true;
        });

        // Placeholder loops in every slot; each can be replaced by real music without any change to code.
        private static MusicSet GetOrCreateMusic() => GetOrCreateSoundSet<MusicSet>("Music",
            ("menu.clip", MusicClip("Music/Placeholder_Menu")), ("menu.volume", 0.5f), ("menu.loop", true),
            ("exploration.clip", MusicClip("Music/Placeholder_Exploration")), ("exploration.volume", 0.4f), ("exploration.loop", true),
            ("combat.clip", MusicClip("Music/Placeholder_Combat")), ("combat.volume", 0.45f), ("combat.loop", true),
            ("intense.clip", MusicClip("Music/Placeholder_Intense")), ("intense.volume", 0.5f), ("intense.loop", true),
            ("victory.clip", MusicClip("Music/Placeholder_Victory")), ("victory.volume", 0.55f), ("victory.loop", false),
            ("crossfade", 2.5f));

        private static AudioClip MusicClip(string clip) => AssetDatabase.LoadAssetAtPath<AudioClip>(ClipPath(clip));

        // ---- Scene ---------------------------------------------------------------------------------------------------------

        private static void CreatePlayerAudio(Transform parent, AudioManager audioManager, PlayerController player, WorldManager world)
        {
            var playerAudio = AddChild<PlayerAudio>(parent, "Player");
            var serializedAudio = new SerializedObject(playerAudio);
            Set(serializedAudio,
                ("audioManager", audioManager), ("player", player), ("world", world),
                ("jump", Flat("Player/Jump", AudioCategory.Sfx, 0.35f, voices: 1, priority: 50)),
                ("land", Flat("Player/Land", AudioCategory.Sfx, 0.6f, voices: 1, priority: 45)),
                ("hurt", Flat("Player/Hurt", AudioCategory.Sfx, 0.75f, voices: 1, priority: 15, cooldown: 0.3f)),
                ("death", Flat("Player/Death", AudioCategory.Sfx, 0.9f, voices: 1, priority: 5, ducksMusic: true)),
                ("fleshImpact", Effect("Impacts/Flesh", 0.7f, 2f, 25f, priority: 70)),
                ("healthPickup", PickupSound("Pickups/FirstAid", 0.6f)),
                ("ammoPickup", PickupSound("Pickups/Ammo", 0.65f)),
                ("treasurePickup", PickupSound("Pickups/TreasureSmall", 0.55f)));

            SoundCue stoneImpact = Effect("Impacts/Stone", 0.55f, 2f, 25f, voices: 4, priority: 90, pitchVariation: 0.08f);
            SoundCue metalImpact = Effect("Impacts/Metal", 0.5f, 2f, 28f, priority: 90);
            SoundCue woodImpact = Effect("Impacts/Wood", 0.55f, 2f, 22f, priority: 90);
            SetSurfaceSounds(serializedAudio.FindProperty("footsteps"),
                (SurfaceKind.Stone, Footsteps("Footsteps/Stone", 0.32f)),
                (SurfaceKind.Concrete, Footsteps("Footsteps/Concrete", 0.32f)),
                (SurfaceKind.Metal, Footsteps("Footsteps/Metal", 0.3f)),
                (SurfaceKind.Grate, Footsteps("Footsteps/Grate", 0.3f)),
                (SurfaceKind.Wood, Footsteps("Footsteps/Wood", 0.32f)),
                (SurfaceKind.Carpet, Footsteps("Footsteps/Carpet", 0.3f)));
            SetSurfaceSounds(serializedAudio.FindProperty("impacts"),
                (SurfaceKind.Stone, stoneImpact), (SurfaceKind.Concrete, stoneImpact),
                (SurfaceKind.Metal, metalImpact), (SurfaceKind.Grate, metalImpact),
                (SurfaceKind.Wood, woodImpact), (SurfaceKind.Carpet, woodImpact));
            serializedAudio.ApplyModifiedPropertiesWithoutUndo();
        }

        private static SoundCue Footsteps(string clips, float volume) =>
            Flat(clips, AudioCategory.Sfx, volume, voices: 2, priority: 60, pitchVariation: 0.06f);

        private static WaveAudio CreateWaveAudio(Transform parent, AudioManager audioManager, WaveManager waves)
        {
            var waveAudio = AddChild<WaveAudio>(parent, "Waves");
            SetFields(waveAudio,
                ("audioManager", audioManager), ("waves", waves),
                ("warning", WaveCue("Waves/Warning", 0.75f)),
                ("majorWarning", WaveCue("Waves/WarningMajor", 0.8f)),
                ("eliteWarning", WaveCue("Waves/WarningElite", 0.85f)),
                ("finalWarning", WaveCue("Waves/WarningFinal", 0.9f)),
                ("incoming", WaveCue("Waves/Incoming", 0.7f)),
                ("waveComplete", WaveCue("Waves/Complete", 0.7f)),
                ("areaCleared", WaveCue("Waves/AreaCleared", 0.85f)),
                ("majorWaveSize", 20));
            return waveAudio;
        }

        private static SoundCue WaveCue(string clips, float volume) =>
            Flat(clips, AudioCategory.Ui, volume, voices: 1, priority: 0, ducksMusic: true);

        private static void CreateAmbience(Transform parent, AudioManager audioManager, WorldManager world, PlayerController player,
            WaveManager waves)
        {
            var ambience = AddChild<AmbienceDirector>(parent, "Ambience");
            var serializedAmbience = new SerializedObject(ambience);
            Set(serializedAmbience,
                ("audioManager", audioManager), ("world", world), ("listener", player.transform), ("waves", waves),
                ("defaultBed", Bed("Ambience/Stone", 0.55f)), ("tensionBed", Bed("Ambience/Tension", 0.45f)));
            SetArray(serializedAmbience.FindProperty("defaultDistantSounds"), new[]
            {
                DistantSound("Distant/Groan"), DistantSound("Distant/Drip"), DistantSound("Distant/MetalClank"), DistantSound("Distant/Rumble")
            });
            serializedAmbience.ApplyModifiedPropertiesWithoutUndo();
        }

        // ---- Helpers -------------------------------------------------------------------------------------------------------

        private static T AddChild<T>(Transform parent, string childName) where T : Component
        {
            var child = new GameObject(childName);
            child.transform.SetParent(parent, false);
            return child.AddComponent<T>();
        }

        private static T GetOrCreateSoundSet<T>(string setName, params (string name, object value)[] fields) where T : ScriptableObject
        {
            string path = $"{SoundSetFolder}/{setName}.asset";
            var existing = AssetDatabase.LoadAssetAtPath<T>(path);
            if (existing != null)
                return existing;

            EnsureFolderTree(Path.GetDirectoryName(path));
            var set = ScriptableObject.CreateInstance<T>();
            AssetDatabase.CreateAsset(set, path);
            SetFields(set, fields);
            return set;
        }

        private static void SetSurfaceSounds(SerializedProperty table, params (SurfaceKind surface, SoundCue sound)[] entries)
        {
            table.arraySize = entries.Length;
            for (int i = 0; i < entries.Length; i++)
            {
                SerializedProperty entry = table.GetArrayElementAtIndex(i);
                entry.FindPropertyRelative("surface").intValue = (int)entries[i].surface;
                entry.FindPropertyRelative("sound").objectReferenceValue = entries[i].sound;
            }
        }

        /// <summary>Sets only those reference fields that are still empty, leaving anything already chosen as it is.</summary>
        private static void FillIfEmpty(Object target, params (string name, Object value)[] fields)
        {
            var serializedTarget = new SerializedObject(target);
            bool changed = false;
            foreach ((string name, Object value) in fields)
            {
                SerializedProperty property = serializedTarget.FindProperty(name);
                if (property == null)
                    throw new InvalidOperationException($"No serialized field named '{name}' on {target.name}.");
                if (property.objectReferenceValue != null)
                    continue;

                property.objectReferenceValue = value;
                changed = true;
            }

            if (!changed)
                return;

            serializedTarget.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(target);
        }

        /// <summary>Opens a prefab for editing and saves it if the edit reports a change.</summary>
        private static void EditPrefab(string path, Func<GameObject, bool> edit)
        {
            if (AssetDatabase.LoadAssetAtPath<GameObject>(path) == null)
                return;

            GameObject contents = PrefabUtility.LoadPrefabContents(path);
            try
            {
                if (edit(contents))
                    PrefabUtility.SaveAsPrefabAsset(contents, path);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(contents);
            }
        }

        private static void EnsureFolderTree(string folder)
        {
            folder = folder.Replace('\\', '/');
            if (AssetDatabase.IsValidFolder(folder))
                return;

            string parent = Path.GetDirectoryName(folder).Replace('\\', '/');
            EnsureFolderTree(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(folder));
        }
    }
}
