using System;
using UnityEditor;
using UnityEngine;
using Wolfenstein.Pickups;
using Wolfenstein.World;
using Object = UnityEngine.Object;

namespace Wolfenstein.Editor
{
    // The items: their tuning assets, blocky placeholder models and the manager that places them in the level.
    // Items take their values from the original game.
    public static partial class TestLevelBuilder
    {
        private const string PickupDefinitionFolder = "Assets/Pickups";
        private const string PickupPrefabFolder = "Assets/Prefabs/Pickups";

        // The space the player must touch to collect an item: with the player's 0.4 m radius,
        // an item is taken once the player's centre is within about 0.85 m of it.
        private static readonly Vector3 PickupTriggerSize = new Vector3(0.9f, 1f, 0.9f);

        // How high an item's model floats above the floor at the bottom of its bob.
        private const float PickupHoverHeight = 0.12f;

        // Items are modelled life-size, then drawn half as large again so they stand out across the 3 m tiles.
        private const float PickupModelScale = 1.5f;

        private static PickupManager CreatePickupManager(WorldManager world)
        {
            // The letters of the level layout's item tiles, and the item each one places.
            (char symbol, Pickup prefab)[] items =
            {
                ('a', GetOrCreateAmmoClip()),
                ('f', GetOrCreatePickup("Food", health: 10, createModel: CreateFoodModel)),
                ('h', GetOrCreatePickup("FirstAidKit", health: 25, createModel: CreateFirstAidKitModel)),
                ('c', GetOrCreatePickup("Cross", score: 100, createModel: CreateCrossModel)),
                ('g', GetOrCreatePickup("Chalice", score: 500, createModel: CreateChaliceModel)),
                ('t', GetOrCreatePickup("Chest", score: 1000, createModel: CreateChestModel)),
                ('k', GetOrCreatePickup("Crown", score: 5000, createModel: CreateCrownModel)),
                ('y', GetOrCreateSecurityKeyPickup()),
            };

            var manager = new GameObject("Pickups").AddComponent<PickupManager>();
            var serializedManager = new SerializedObject(manager);
            serializedManager.FindProperty("world").objectReferenceValue = world;

            SerializedProperty itemTiles = serializedManager.FindProperty("itemTiles");
            itemTiles.arraySize = items.Length;
            for (int i = 0; i < items.Length; i++)
            {
                SerializedProperty tile = itemTiles.GetArrayElementAtIndex(i);
                tile.FindPropertyRelative("symbol").intValue = items[i].symbol;
                tile.FindPropertyRelative("prefab").objectReferenceValue = items[i].prefab;
            }

            serializedManager.ApplyModifiedPropertiesWithoutUndo();
            return manager;
        }

        private static Pickup GetOrCreateAmmoClip() => GetOrCreatePickup("AmmoClip", ammo: 8, createModel: CreateAmmoClipModel);

        // What a guard leaves where it falls: the same clip, only half full.
        private static Pickup GetOrCreateDroppedAmmoClip() =>
            GetOrCreatePickup("DroppedAmmoClip", ammo: 4, createModel: CreateAmmoClipModel);

        // The definition and the prefab are each only created when missing, so edits to either survive a rebuild.
        private static Pickup GetOrCreatePickup(string itemName, Action<Transform> createModel, int health = 0, int ammo = 0, int score = 0)
        {
            string path = $"{PickupPrefabFolder}/{itemName}.prefab";
            var existing = AssetDatabase.LoadAssetAtPath<Pickup>(path);
            if (existing != null)
                return existing;

            EnsureFolder(PickupPrefabFolder);
            var root = new GameObject(itemName);

            var trigger = root.AddComponent<BoxCollider>();
            trigger.isTrigger = true;
            trigger.center = Vector3.up * (PickupTriggerSize.y * 0.5f);
            trigger.size = PickupTriggerSize;

            // On the item itself rather than its model, so it stays on the floor while the model bobs.
            CreateBlobShadow(root.transform, 0.6f);

            var model = new GameObject("Model").transform;
            model.SetParent(root.transform, false);
            model.localPosition = Vector3.up * PickupHoverHeight;
            model.localScale = Vector3.one * PickupModelScale;
            createModel(model);

            SetFields(root.AddComponent<Pickup>(), ("definition", GetOrCreatePickupDefinition(itemName, health, ammo, score)));
            SetFields(root.AddComponent<PickupView>(), ("model", model));

            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, path);
            Object.DestroyImmediate(root);
            return prefab.GetComponent<Pickup>();
        }

        private static PickupDefinition GetOrCreatePickupDefinition(string itemName, int health, int ammo, int score)
        {
            string path = $"{PickupDefinitionFolder}/{itemName}.asset";
            var definition = AssetDatabase.LoadAssetAtPath<PickupDefinition>(path);
            if (definition != null)
                return definition;

            EnsureFolder(PickupDefinitionFolder);
            definition = ScriptableObject.CreateInstance<PickupDefinition>();
            AssetDatabase.CreateAsset(definition, path);
            SetFields(definition, ("health", health), ("ammo", ammo), ("score", score));
            return definition;
        }

        // Item models stand on the floor at the origin and face +Z.

        // A pistol magazine standing on its base plate, with the top round lying in its lips.
        private static void CreateAmmoClipModel(Transform model)
        {
            Material steel = GunSteel();
            Material brass = BrassMaterial();

            CreateModelPart(model, "Base Plate", PrimitiveType.Cube, new Vector3(0f, 0.015f, 0f), new Vector3(0.1f, 0.03f, 0.19f), Quaternion.identity, steel);
            CreateModelPart(model, "Magazine", PrimitiveType.Cube, new Vector3(0f, 0.18f, 0f), new Vector3(0.07f, 0.32f, 0.16f), Quaternion.identity, steel);
            CreateModelPart(model, "Round", PrimitiveType.Cylinder, new Vector3(0f, 0.36f, 0.02f), new Vector3(0.05f, 0.18f, 0.05f), Quaternion.Euler(90f, 0f, 0f), brass);
        }

        // A roast on a plate.
        private static void CreateFoodModel(Transform model)
        {
            Material plate = WhiteMaterial();
            Material roast = GetOrCreateMaterial("Pickup_Roast", new Color(0.55f, 0.3f, 0.12f), smoothness: 0.4f);

            CreateModelPart(model, "Plate", PrimitiveType.Cylinder, new Vector3(0f, 0.015f, 0f), new Vector3(0.46f, 0.03f, 0.46f), Quaternion.identity, plate);
            CreateModelPart(model, "Roast", PrimitiveType.Sphere, new Vector3(0f, 0.1f, -0.02f), new Vector3(0.28f, 0.16f, 0.22f), Quaternion.identity, roast);
            CreateModelPart(model, "Left Leg", PrimitiveType.Cube, new Vector3(-0.08f, 0.08f, 0.12f), new Vector3(0.06f, 0.06f, 0.14f), Quaternion.Euler(0f, -20f, 0f), roast);
            CreateModelPart(model, "Right Leg", PrimitiveType.Cube, new Vector3(0.08f, 0.08f, 0.12f), new Vector3(0.06f, 0.06f, 0.14f), Quaternion.Euler(0f, 20f, 0f), roast);
        }

        // A white case with a red cross on its lid and on both faces.
        private static void CreateFirstAidKitModel(Transform model)
        {
            const float height = 0.28f;
            const float depth = 0.3f;
            Material white = WhiteMaterial();
            Material red = RedMaterial();

            CreateModelPart(model, "Case", PrimitiveType.Cube, new Vector3(0f, height * 0.5f, 0f), new Vector3(0.44f, height, depth), Quaternion.identity, white);
            foreach (float face in new[] { 1f, -1f })
            {
                var centre = new Vector3(0f, height * 0.5f, face * (depth * 0.5f + 0.005f));
                CreateModelPart(model, "Cross Upright", PrimitiveType.Cube, centre, new Vector3(0.07f, 0.2f, 0.01f), Quaternion.identity, red);
                CreateModelPart(model, "Cross Bar", PrimitiveType.Cube, centre, new Vector3(0.2f, 0.07f, 0.01f), Quaternion.identity, red);
            }

            var lid = new Vector3(0f, height + 0.005f, 0f);
            CreateModelPart(model, "Lid Cross Upright", PrimitiveType.Cube, lid, new Vector3(0.07f, 0.01f, 0.2f), Quaternion.identity, red);
            CreateModelPart(model, "Lid Cross Bar", PrimitiveType.Cube, lid, new Vector3(0.2f, 0.01f, 0.07f), Quaternion.identity, red);
        }

        // A gold cross on a small foot, with a ruby where the arms meet.
        private static void CreateCrossModel(Transform model)
        {
            Material gold = GetOrCreateGoldMaterial();
            Material ruby = RedMaterial();

            CreateModelPart(model, "Foot", PrimitiveType.Cube, new Vector3(0f, 0.03f, 0f), new Vector3(0.2f, 0.06f, 0.12f), Quaternion.identity, gold);
            CreateModelPart(model, "Upright", PrimitiveType.Cube, new Vector3(0f, 0.27f, 0f), new Vector3(0.07f, 0.44f, 0.05f), Quaternion.identity, gold);
            CreateModelPart(model, "Arms", PrimitiveType.Cube, new Vector3(0f, 0.36f, 0f), new Vector3(0.28f, 0.07f, 0.05f), Quaternion.identity, gold);
            CreateModelPart(model, "Ruby", PrimitiveType.Cube, new Vector3(0f, 0.36f, 0f), new Vector3(0.04f, 0.04f, 0.07f), Quaternion.identity, ruby);
        }

        // A gold goblet: foot, stem and cup, set with a ruby and a sapphire.
        private static void CreateChaliceModel(Transform model)
        {
            Material gold = GetOrCreateGoldMaterial();
            Material ruby = RedMaterial();
            Material sapphire = BlueMaterial();

            CreateModelPart(model, "Foot", PrimitiveType.Cylinder, new Vector3(0f, 0.015f, 0f), new Vector3(0.2f, 0.03f, 0.2f), Quaternion.identity, gold);
            CreateModelPart(model, "Stem", PrimitiveType.Cylinder, new Vector3(0f, 0.11f, 0f), new Vector3(0.05f, 0.16f, 0.05f), Quaternion.identity, gold);
            CreateModelPart(model, "Knot", PrimitiveType.Sphere, new Vector3(0f, 0.12f, 0f), new Vector3(0.08f, 0.06f, 0.08f), Quaternion.identity, gold);
            CreateModelPart(model, "Cup", PrimitiveType.Cylinder, new Vector3(0f, 0.28f, 0f), new Vector3(0.24f, 0.18f, 0.24f), Quaternion.identity, gold);
            CreateModelPart(model, "Ruby", PrimitiveType.Cube, new Vector3(0f, 0.28f, 0.12f), new Vector3(0.05f, 0.05f, 0.02f), Quaternion.identity, ruby);
            CreateModelPart(model, "Sapphire", PrimitiveType.Cube, new Vector3(0f, 0.28f, -0.12f), new Vector3(0.05f, 0.05f, 0.02f), Quaternion.identity, sapphire);
        }

        // A wooden chest bound with two gold bands, with a gold lock on the front.
        private static void CreateChestModel(Transform model)
        {
            Material wood = GetOrCreateMaterial("Pickup_Wood", new Color(0.62f, 0.38f, 0.2f), GetOrCreateWoodTexture(), smoothness: 0.25f);
            Material gold = GetOrCreateGoldMaterial();

            CreateModelPart(model, "Body", PrimitiveType.Cube, new Vector3(0f, 0.12f, 0f), new Vector3(0.46f, 0.24f, 0.3f), Quaternion.identity, wood);
            CreateModelPart(model, "Lid", PrimitiveType.Cube, new Vector3(0f, 0.29f, 0f), new Vector3(0.48f, 0.1f, 0.32f), Quaternion.identity, wood);
            foreach (float side in new[] { -0.15f, 0.15f })
                CreateModelPart(model, "Band", PrimitiveType.Cube, new Vector3(side, 0.17f, 0f), new Vector3(0.05f, 0.35f, 0.33f), Quaternion.identity, gold);

            CreateModelPart(model, "Lock", PrimitiveType.Cube, new Vector3(0f, 0.23f, 0.165f), new Vector3(0.07f, 0.08f, 0.02f), Quaternion.identity, gold);
        }

        // A gold band crowned with points and set with jewels, around a red velvet cap.
        private static void CreateCrownModel(Transform model)
        {
            const int points = 6;
            const float bandRadius = 0.16f;
            Material gold = GetOrCreateGoldMaterial();
            Material velvet = RedMaterial();
            Material sapphire = BlueMaterial();

            CreateModelPart(model, "Cap", PrimitiveType.Sphere, new Vector3(0f, 0.13f, 0f), new Vector3(0.28f, 0.2f, 0.28f), Quaternion.identity, velvet);
            CreateModelPart(model, "Band", PrimitiveType.Cylinder, new Vector3(0f, 0.07f, 0f), new Vector3(bandRadius * 2f, 0.14f, bandRadius * 2f), Quaternion.identity, gold);

            for (int i = 0; i < points; i++)
            {
                // Each point is a square turned onto its corner, standing on the rim and facing outward.
                Quaternion facing = Quaternion.Euler(0f, 360f * i / points, 0f);
                Vector3 rim = facing * new Vector3(0f, 0.16f, bandRadius - 0.01f);
                CreateModelPart(model, "Point", PrimitiveType.Cube, rim, new Vector3(0.09f, 0.09f, 0.02f), facing * Quaternion.Euler(0f, 0f, 45f), gold);

                Vector3 jewel = facing * new Vector3(0f, 0.07f, bandRadius);
                CreateModelPart(model, "Jewel", PrimitiveType.Cube, jewel, new Vector3(0.035f, 0.035f, 0.02f), facing, i % 2 == 0 ? sapphire : velvet);
            }
        }

        // Item materials catch the lamp light, and glow faintly so an item can still be spotted in a dim corner.
        private static Material GetOrCreateGoldMaterial() =>
            GetOrCreateMaterial("Pickup_Gold", new Color(1f, 0.76f, 0.18f), smoothness: 0.65f, metallic: 0.15f, emission: new Color(0.5f, 0.34f, 0.05f));

        private static Material BrassMaterial() =>
            GetOrCreateMaterial("Pickup_Brass", new Color(0.85f, 0.62f, 0.25f), smoothness: 0.55f, metallic: 0.15f, emission: new Color(0.22f, 0.15f, 0.04f));

        private static Material WhiteMaterial() =>
            GetOrCreateMaterial("Pickup_White", new Color(0.92f, 0.92f, 0.88f), smoothness: 0.3f, emission: new Color(0.14f, 0.14f, 0.13f));

        private static Material RedMaterial() =>
            GetOrCreateMaterial("Pickup_Red", new Color(0.8f, 0.06f, 0.06f), smoothness: 0.5f, emission: new Color(0.35f, 0.02f, 0.02f));

        private static Material BlueMaterial() =>
            GetOrCreateMaterial("Pickup_Blue", new Color(0.12f, 0.25f, 0.85f), smoothness: 0.7f, emission: new Color(0.04f, 0.09f, 0.4f));
    }
}
