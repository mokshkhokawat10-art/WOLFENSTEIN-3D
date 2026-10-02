using System;
using UnityEditor;
using UnityEngine;
using Wolfenstein.World;
using Object = UnityEngine.Object;

namespace Wolfenstein.Editor
{
    // Props that dress the rooms, placed by the level's decor map, and the prefab walls that stand in the arena as cover.
    // Everything is built from simple shapes in the level's own materials. Solid props carry one box collider round their
    // footprint and are kept within a metre of the wall they stand against, clear of the paths between tile centres.
    // Every prefab here is only created when missing.
    public static partial class TestLevelBuilder
    {
        private const string PropFolder = "Assets/Prefabs/Props";
        private const string WallPrefabFolder = "Assets/Prefabs/Walls";

        // Decals sit this far off the surface they lie on, so the two do not flicker against each other.
        private const float DecalLift = 0.012f;

        private static LevelDressing CreateLevelDressing(WorldManager world)
        {
            (char symbol, GameObject prefab, PropPlacement placement, float height, float cornerOffset, float yawJitter)[] props =
            {
                ('c', GetOrCreateCrate(), PropPlacement.InCorner, 0f, 0.7f, 8f),
                ('C', GetOrCreateCrateStack(), PropPlacement.InCorner, 0f, 1f, 4f),
                ('b', GetOrCreateBarrel(), PropPlacement.InCorner, 0f, 0.5f, 180f),
                ('B', GetOrCreateBarrelCluster(), PropPlacement.AgainstWall, 0f, 0f, 10f),
                ('s', GetOrCreateShelving(), PropPlacement.AgainstWall, 0f, 0f, 0f),
                ('k', GetOrCreateBunk(), PropPlacement.AgainstWall, 0f, 0f, 0f),
                ('L', GetOrCreateLockers(), PropPlacement.AgainstWall, 0f, 0f, 0f),
                ('f', GetOrCreateFootlocker(), PropPlacement.AgainstWall, 0f, 0f, 6f),
                ('r', GetOrCreateWeaponRack(), PropPlacement.AgainstWall, 0f, 0f, 0f),
                ('S', GetOrCreateSandbags(), PropPlacement.AgainstWall, 0f, 0f, 4f),
                ('t', GetOrCreateMapTable(), PropPlacement.AgainstWall, 0f, 0f, 0f),
                ('o', GetOrCreateBookcase(), PropPlacement.AgainstWall, 0f, 0f, 0f),
                ('n', GetOrCreateBanner(), PropPlacement.OnWall, 0.75f, 0f, 0f),
                ('x', GetOrCreateBrazier(), PropPlacement.InCorner, 0f, 0.65f, 90f),
                ('m', GetOrCreateGenerator(), PropPlacement.AgainstWall, 0f, 0f, 0f),
                ('e', GetOrCreateFuseBox(), PropPlacement.OnWall, 1.3f, 0f, 0f),
                ('h', GetOrCreateHangingCables(), PropPlacement.FromCeiling, 0f, 0f, 30f),
                ('p', GetOrCreateElevatorPanel(), PropPlacement.OnWall, 1.2f, 0f, 0f),
                ('X', GetOrCreateExitSign(), PropPlacement.OnWall, 1.65f, 0f, 0f),
                ('d', GetOrCreateBloodPool(), PropPlacement.OnFloor, 0f, 0f, 0f),
                ('D', GetOrCreateBloodSplatter(), PropPlacement.OnWall, 0.8f, 0f, 0f),
                ('u', GetOrCreateRubble(), PropPlacement.AgainstWall, 0f, 0f, 30f),
                ('g', GetOrCreateDrain(), PropPlacement.OnFloor, 0f, 0f, 0f),
                ('q', GetOrCreateOilStain(), PropPlacement.OnFloor, 0f, 0f, 0f),
                ('K', GetOrCreateCrackedWall(), PropPlacement.OnWall, 1.2f, 0f, 0f),
                ('Z', GetOrCreatePowerSwitch(), PropPlacement.OnWall, 1.3f, 0f, 0f),
            };

            var dressing = new GameObject("Dressing").AddComponent<LevelDressing>();
            var serializedDressing = new SerializedObject(dressing);
            serializedDressing.FindProperty("world").objectReferenceValue = world;
            SerializedProperty bindings = serializedDressing.FindProperty("props");
            bindings.arraySize = props.Length;
            for (int i = 0; i < props.Length; i++)
            {
                SerializedProperty binding = bindings.GetArrayElementAtIndex(i);
                binding.FindPropertyRelative("symbol").intValue = props[i].symbol;
                binding.FindPropertyRelative("prefab").objectReferenceValue = props[i].prefab;
                binding.FindPropertyRelative("placement").enumValueIndex = (int)props[i].placement;
                binding.FindPropertyRelative("height").floatValue = props[i].height;
                binding.FindPropertyRelative("cornerOffset").floatValue = props[i].cornerOffset;
                binding.FindPropertyRelative("yawJitter").floatValue = props[i].yawJitter;
            }

            serializedDressing.ApplyModifiedPropertiesWithoutUndo();
            return dressing;
        }

        // A prop's origin is on the floor at the middle of its back, and it faces +Z; hung props have their origin at the
        // back of their middle. The collider, when there is one, is a box given by its centre and size.
        private static GameObject GetOrCreateProp(string propName, Action<Transform> build, Vector3 colliderCentre = default,
            Vector3 colliderSize = default, string folder = PropFolder)
        {
            string path = $"{folder}/{propName}.prefab";
            var existing = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (existing != null)
                return existing;

            EnsureFolder(folder);
            var prop = new GameObject(propName);
            build(prop.transform);
            if (colliderSize != Vector3.zero)
            {
                var box = prop.AddComponent<BoxCollider>();
                box.center = colliderCentre;
                box.size = colliderSize;
            }

            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(prop, path);
            Object.DestroyImmediate(prop);
            return prefab;
        }

        private static void Part(Transform parent, string partName, PrimitiveType shape, Vector3 position, Vector3 size, Material material,
            float yaw = 0f, float pitch = 0f, float roll = 0f) =>
            CreateModelPart(parent, partName, shape, position, size, Quaternion.Euler(pitch, yaw, roll), material);

        private static void Box(Transform parent, string partName, Vector3 position, Vector3 size, Material material, float yaw = 0f) =>
            Part(parent, partName, PrimitiveType.Cube, position, size, material, yaw);

        // ---- Materials -----------------------------------------------------------------------------------------------------

        private static Material TintedSurface(string materialName, string surfaceName, Func<Surface> draw, float bumpiness, Color tint,
            float smoothness = 0f, float metallic = 0f)
        {
            (Texture2D albedo, Texture2D normals) = GetOrCreateSurface(surfaceName, draw, bumpiness);
            return GetOrCreateMaterial(materialName, tint, albedo, smoothness, metallic, normals);
        }

        private static Material CrateWood() =>
            TintedSurface("Prop_Crate", "Crate", () => DrawCrateFace(31), 4f, new Color(0.66f, 0.48f, 0.3f), smoothness: 0.1f);

        private static Material MilitaryCrate() =>
            TintedSurface("Prop_Crate_Olive", "Crate", () => DrawCrateFace(31), 4f, new Color(0.36f, 0.4f, 0.24f), smoothness: 0.15f);

        private static Material PaintedSteel(string materialName, Color colour) =>
            TintedSurface(materialName, "Armour_Plate", () => DrawArmourPlate(32), 3f, colour, smoothness: 0.4f, metallic: 0.3f);

        private static Material Cloth(string materialName, Color colour) =>
            GetOrCreateMaterial(materialName, colour, GetOrCreateFabricTexture(), smoothness: 0.05f);

        private static Material Paper() => GetOrCreateMaterial("Paper", new Color(0.82f, 0.8f, 0.7f), smoothness: 0.1f);

        private static Material Gilt() => GetOrCreateMaterial("Gilt", new Color(0.62f, 0.45f, 0.15f), smoothness: 0.55f, metallic: 0.5f);

        private static Material Iron() => GetOrCreateMaterial("Iron", new Color(0.08f, 0.08f, 0.08f), smoothness: 0.3f, metallic: 0.4f);

        private static Material BunkerConcrete() =>
            SurfaceMaterial("Wall_Bunker", () => DrawConcretePanels(17, new Color(0.36f, 0.35f, 0.34f), new Color(0.07f, 0.07f, 0.07f)), 4f,
                smoothness: 0.1f);

        // Lit, with the texture's transparent pixels cut away, so a stain darkens with the room around it.
        private static Material GetOrCreateDecalMaterial(string materialName, Texture2D texture, Color colour, float smoothness)
        {
            string path = $"{MaterialFolder}/{materialName}.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material != null)
                return material;

            material = new Material(Shader.Find(LitShaderName));
            material.SetTexture("_BaseMap", texture);
            material.SetColor("_BaseColor", colour);
            material.SetFloat("_Smoothness", smoothness);
            material.SetFloat("_EnvironmentReflections", 0f);
            material.EnableKeyword("_ENVIRONMENTREFLECTIONS_OFF");
            material.SetFloat("_AlphaClip", 1f);
            material.SetFloat("_Cutoff", 0.5f);
            material.EnableKeyword("_ALPHATEST_ON");
            material.SetOverrideTag("RenderType", "TransparentCutout");
            material.renderQueue = (int)UnityEngine.Rendering.RenderQueue.AlphaTest;
            AssetDatabase.CreateAsset(material, path);
            return material;
        }

        // ---- Storage -------------------------------------------------------------------------------------------------------

        private static void AddCrate(Transform parent, Vector3 floorCentre, float size, Material material, float yaw = 0f)
        {
            Box(parent, "Crate", floorCentre + Vector3.up * (size * 0.5f), Vector3.one * size, material, yaw);
        }

        private static GameObject GetOrCreateCrate() => GetOrCreateProp("Crate",
            root => AddCrate(root, new Vector3(0f, 0f, 0.48f), 0.95f, CrateWood()),
            new Vector3(0f, 0.475f, 0.48f), new Vector3(0.95f, 0.95f, 0.95f));

        private static GameObject GetOrCreateCrateStack() => GetOrCreateProp("CrateStack", root =>
        {
            AddCrate(root, new Vector3(-0.45f, 0f, 0.5f), 1f, CrateWood());
            AddCrate(root, new Vector3(0.52f, 0f, 0.45f), 0.85f, CrateWood(), 6f);
            AddCrate(root, new Vector3(-0.4f, 1f, 0.45f), 0.75f, CrateWood(), -12f);
        }, new Vector3(0.02f, 0.87f, 0.5f), new Vector3(1.96f, 1.75f, 1f));

        private static void AddBarrel(Transform parent, Vector3 floorCentre, Material paint)
        {
            const float height = 0.9f;
            const float diameter = 0.6f;
            Part(parent, "Barrel", PrimitiveType.Cylinder, floorCentre + Vector3.up * (height * 0.5f), new Vector3(diameter, height, diameter), paint);
            foreach (float y in new[] { 0.2f, 0.7f })
            {
                Part(parent, "Rib", PrimitiveType.Cylinder, floorCentre + Vector3.up * y, new Vector3(diameter + 0.03f, 0.04f, diameter + 0.03f),
                    TrimDarkSteel());
            }

            Part(parent, "Lid", PrimitiveType.Cylinder, floorCentre + Vector3.up * (height + 0.005f), new Vector3(diameter - 0.06f, 0.01f, diameter - 0.06f),
                TrimDarkSteel());
            Part(parent, "Bung", PrimitiveType.Cylinder, floorCentre + new Vector3(0.14f, height + 0.015f, 0.05f), new Vector3(0.07f, 0.02f, 0.07f),
                GunSteel());
        }

        private static Material BarrelRed() => PaintedSteel("Prop_Barrel_Red", new Color(0.5f, 0.1f, 0.07f));
        private static Material BarrelOlive() => PaintedSteel("Prop_Barrel_Olive", new Color(0.26f, 0.3f, 0.18f));
        private static Material BarrelBlue() => PaintedSteel("Prop_Barrel_Blue", new Color(0.14f, 0.2f, 0.34f));

        private static GameObject GetOrCreateBarrel() => GetOrCreateProp("Barrel",
            root => AddBarrel(root, new Vector3(0f, 0f, 0.33f), BarrelRed()),
            new Vector3(0f, 0.45f, 0.33f), new Vector3(0.6f, 0.9f, 0.6f));

        private static GameObject GetOrCreateBarrelCluster() => GetOrCreateProp("BarrelCluster", root =>
        {
            AddBarrel(root, new Vector3(-0.35f, 0f, 0.34f), BarrelOlive());
            AddBarrel(root, new Vector3(0.33f, 0f, 0.36f), BarrelRed());
            AddBarrel(root, new Vector3(0f, 0f, 0.88f), BarrelBlue());
        }, new Vector3(0f, 0.45f, 0.6f), new Vector3(1.3f, 0.9f, 1.2f));

        // Steel shelving loaded with boxes and cans.
        private static GameObject GetOrCreateShelving() => GetOrCreateProp("Shelving", root =>
        {
            Material frame = PaintedSteel("Prop_Shelf_Steel", new Color(0.3f, 0.33f, 0.36f));
            Material box = GetOrCreateMaterial("Prop_Cardboard", new Color(0.55f, 0.42f, 0.27f), smoothness: 0.05f);
            foreach (float x in new[] { -0.88f, 0.88f })
            {
                foreach (float z in new[] { 0.05f, 0.47f })
                    Box(root, "Upright", new Vector3(x, 1.05f, z), new Vector3(0.05f, 2.1f, 0.05f), frame);
            }

            for (int shelf = 0; shelf < 4; shelf++)
            {
                float y = 0.15f + shelf * 0.62f;
                Box(root, "Shelf", new Vector3(0f, y, 0.26f), new Vector3(1.8f, 0.03f, 0.48f), frame);
                if (shelf == 3)
                    continue;

                for (int i = 0; i < 3; i++)
                {
                    float width = 0.3f + 0.12f * ((shelf + i) % 3);
                    float height = 0.22f + 0.1f * ((shelf * 2 + i) % 3);
                    Box(root, "Box", new Vector3(-0.55f + i * 0.55f, y + 0.015f + height * 0.5f, 0.26f), new Vector3(width, height, 0.36f), box, (i - 1) * 7f);
                }
            }
        }, new Vector3(0f, 1.05f, 0.26f), new Vector3(1.85f, 2.1f, 0.52f));

        // ---- Military ------------------------------------------------------------------------------------------------------

        // A steel bunk bed with two made-up bunks.
        private static GameObject GetOrCreateBunk() => GetOrCreateProp("Bunk", root =>
        {
            Material frame = PaintedSteel("Prop_Bunk_Steel", new Color(0.24f, 0.27f, 0.2f));
            Material mattress = Cloth("Prop_Mattress", new Color(0.55f, 0.55f, 0.47f));
            Material blanket = Cloth("Prop_Blanket", new Color(0.3f, 0.32f, 0.22f));
            Material pillow = Cloth("Prop_Pillow", new Color(0.75f, 0.74f, 0.68f));
            foreach (float x in new[] { -0.97f, 0.97f })
            {
                foreach (float z in new[] { 0.06f, 0.84f })
                    Box(root, "Post", new Vector3(x, 0.85f, z), new Vector3(0.05f, 1.7f, 0.05f), frame);
            }

            foreach (float y in new[] { 0.35f, 1.25f })
            {
                Box(root, "Frame", new Vector3(0f, y, 0.45f), new Vector3(1.96f, 0.06f, 0.82f), frame);
                Box(root, "Mattress", new Vector3(0f, y + 0.09f, 0.45f), new Vector3(1.86f, 0.12f, 0.76f), mattress);
                Box(root, "Blanket", new Vector3(0.18f, y + 0.16f, 0.46f), new Vector3(1.4f, 0.03f, 0.78f), blanket);
                Box(root, "Pillow", new Vector3(-0.72f, y + 0.19f, 0.45f), new Vector3(0.34f, 0.08f, 0.5f), pillow);
            }

            Box(root, "Ladder", new Vector3(0.97f, 0.8f, 0.9f), new Vector3(0.04f, 1.2f, 0.04f), frame);
        }, new Vector3(0f, 0.85f, 0.45f), new Vector3(2f, 1.7f, 0.9f));

        // A row of three steel lockers, one left ajar.
        private static GameObject GetOrCreateLockers() => GetOrCreateProp("Lockers", root =>
        {
            Material steel = PaintedSteel("Prop_Locker", new Color(0.33f, 0.38f, 0.33f));
            Material dark = GetOrCreateMaterial("Vent_Dark", new Color(0.03f, 0.03f, 0.03f));
            for (int i = 0; i < 3; i++)
            {
                float x = (i - 1) * 0.46f;
                Box(root, "Locker", new Vector3(x, 0.95f, 0.25f), new Vector3(0.44f, 1.9f, 0.5f), steel);
                for (int slot = 0; slot < 3; slot++)
                    Box(root, "Vent", new Vector3(x, 1.6f + slot * 0.05f, 0.502f), new Vector3(0.26f, 0.015f, 0.01f), dark);
                Box(root, "Handle", new Vector3(x + 0.14f, 1f, 0.51f), new Vector3(0.03f, 0.14f, 0.03f), GunSteel());
            }

            Box(root, "Open Door", new Vector3(0.62f, 0.95f, 0.64f), new Vector3(0.02f, 1.8f, 0.42f), steel, -18f);
        }, new Vector3(0f, 0.95f, 0.25f), new Vector3(1.4f, 1.9f, 0.5f));

        // An olive ammunition chest with steel corners and a stencilled lid.
        private static GameObject GetOrCreateFootlocker() => GetOrCreateProp("Footlocker", root =>
        {
            Box(root, "Chest", new Vector3(0f, 0.22f, 0.26f), new Vector3(0.9f, 0.44f, 0.46f), MilitaryCrate());
            foreach (float x in new[] { -0.44f, 0.44f })
            {
                foreach (float z in new[] { 0.04f, 0.48f })
                    Box(root, "Corner", new Vector3(x, 0.22f, z), new Vector3(0.05f, 0.46f, 0.05f), TrimDarkSteel());
            }

            Box(root, "Handle", new Vector3(0f, 0.3f, 0.5f), new Vector3(0.2f, 0.03f, 0.03f), TrimDarkSteel());
            Box(root, "Stencil", new Vector3(0f, 0.442f, 0.26f), new Vector3(0.36f, 0.004f, 0.1f), Paper());
        }, new Vector3(0f, 0.22f, 0.26f), new Vector3(0.95f, 0.45f, 0.5f));

        // A wooden rack holding four rifles upright.
        private static GameObject GetOrCreateWeaponRack() => GetOrCreateProp("WeaponRack", root =>
        {
            Material wood = BeamWood();
            Box(root, "Back", new Vector3(0f, 0.8f, 0.03f), new Vector3(1.2f, 1.5f, 0.05f), wood);
            Box(root, "Base", new Vector3(0f, 0.08f, 0.16f), new Vector3(1.2f, 0.16f, 0.3f), wood);
            Box(root, "Top Rail", new Vector3(0f, 1.25f, 0.14f), new Vector3(1.2f, 0.06f, 0.2f), wood);
            for (int i = 0; i < 4; i++)
            {
                var rifle = new GameObject("Rifle").transform;
                rifle.SetParent(root, false);
                rifle.SetLocalPositionAndRotation(new Vector3(-0.42f + i * 0.28f, 0.18f, 0.16f), Quaternion.Euler(-88f, 0f, 0f));
                CreateGuardRifle(rifle);
            }
        }, new Vector3(0f, 0.75f, 0.16f), new Vector3(1.2f, 1.5f, 0.32f));

        // A low wall of sandbags, three courses high, each bag its own lumpy shape.
        private static GameObject GetOrCreateSandbags() => GetOrCreateProp("Sandbags", root =>
        {
            Material bag = Cloth("Prop_Sandbag", new Color(0.55f, 0.48f, 0.34f));
            for (int course = 0; course < 3; course++)
            {
                int bags = 4 - course % 2;
                float y = 0.14f + course * 0.26f;
                for (int i = 0; i < bags; i++)
                {
                    float x = (i - (bags - 1) * 0.5f) * 0.48f;
                    Part(root, "Bag", PrimitiveType.Sphere, new Vector3(x, y, 0.32f), new Vector3(0.52f, 0.28f, 0.58f), bag, (i + course) % 2 * 8f - 4f);
                }
            }
        }, new Vector3(0f, 0.42f, 0.32f), new Vector3(1.9f, 0.84f, 0.62f));

        // ---- Command -------------------------------------------------------------------------------------------------------

        // A heavy table with a campaign map spread on it, a brass lamp and a field telephone.
        private static GameObject GetOrCreateMapTable() => GetOrCreateProp("MapTable", root =>
        {
            Material wood = GetOrCreateMaterial("Trim_Wood", new Color(0.3f, 0.17f, 0.09f), GetOrCreateWoodTexture(), smoothness: 0.4f);
            Material map = GetOrCreateMaterial("Paper_Map", new Color(0.62f, 0.66f, 0.5f), smoothness: 0.1f);
            Material brass = BrassMaterial();
            Material glow = GetOrCreateGlowMaterial("Lamp_Bulb_Dim", new Color(1f, 0.55f, 0.25f) * 3f);
            Box(root, "Top", new Vector3(0f, 0.86f, 0.46f), new Vector3(1.7f, 0.07f, 0.88f), wood);
            foreach (float x in new[] { -0.78f, 0.78f })
            {
                foreach (float z in new[] { 0.08f, 0.84f })
                    Box(root, "Leg", new Vector3(x, 0.41f, z), new Vector3(0.08f, 0.82f, 0.08f), wood);
            }

            Box(root, "Map", new Vector3(-0.1f, 0.9f, 0.48f), new Vector3(1f, 0.004f, 0.7f), map, 4f);
            Box(root, "Marker", new Vector3(0.05f, 0.92f, 0.45f), new Vector3(0.04f, 0.04f, 0.04f), RedMaterial());
            Box(root, "Marker", new Vector3(-0.3f, 0.92f, 0.6f), new Vector3(0.04f, 0.04f, 0.04f), BlueMaterial());
            Part(root, "Lamp Base", PrimitiveType.Cylinder, new Vector3(0.62f, 0.92f, 0.25f), new Vector3(0.14f, 0.04f, 0.14f), brass);
            Part(root, "Lamp Stem", PrimitiveType.Cylinder, new Vector3(0.62f, 1.07f, 0.25f), new Vector3(0.025f, 0.3f, 0.025f), brass);
            Part(root, "Lamp Shade", PrimitiveType.Cylinder, new Vector3(0.62f, 1.22f, 0.3f), new Vector3(0.2f, 0.08f, 0.2f), GetOrCreateMaterial("Lamp_Enamel", new Color(0.16f, 0.27f, 0.19f), smoothness: 0.55f), pitch: 20f);
            Part(root, "Lamp Bulb", PrimitiveType.Sphere, new Vector3(0.62f, 1.18f, 0.32f), new Vector3(0.06f, 0.06f, 0.06f), glow);
            Box(root, "Telephone", new Vector3(0.6f, 0.95f, 0.66f), new Vector3(0.2f, 0.12f, 0.16f), MilitaryCrate());
        }, new Vector3(0f, 0.45f, 0.46f), new Vector3(1.72f, 0.9f, 0.9f));

        // A tall bookcase of leather-bound volumes.
        private static GameObject GetOrCreateBookcase() => GetOrCreateProp("Bookcase", root =>
        {
            Material wood = GetOrCreateMaterial("Trim_Wood", new Color(0.3f, 0.17f, 0.09f), GetOrCreateWoodTexture(), smoothness: 0.4f);
            Color[] bindings =
            {
                new Color(0.4f, 0.08f, 0.06f), new Color(0.1f, 0.2f, 0.12f), new Color(0.35f, 0.22f, 0.1f), new Color(0.12f, 0.12f, 0.22f)
            };

            Box(root, "Back", new Vector3(0f, 1.15f, 0.02f), new Vector3(1.4f, 2.3f, 0.04f), wood);
            foreach (float x in new[] { -0.68f, 0.68f })
                Box(root, "Side", new Vector3(x, 1.15f, 0.2f), new Vector3(0.04f, 2.3f, 0.4f), wood);

            for (int shelf = 0; shelf < 5; shelf++)
            {
                float y = 0.05f + shelf * 0.5f;
                Box(root, "Shelf", new Vector3(0f, y, 0.2f), new Vector3(1.34f, 0.04f, 0.38f), wood);
                if (shelf == 4)
                    continue;

                float x = -0.6f;
                for (int book = 0; x < 0.58f; book++)
                {
                    float width = 0.05f + 0.03f * ((book * 7 + shelf * 3) % 4) / 3f;
                    float height = 0.28f + 0.1f * ((book * 5 + shelf) % 3) / 2f;
                    int binding = (book * 3 + shelf) % bindings.Length;
                    Material cover = GetOrCreateMaterial($"Book_{binding}", bindings[binding], GetOrCreateFabricTexture(), smoothness: 0.25f);
                    Box(root, "Book", new Vector3(x + width * 0.5f, y + 0.02f + height * 0.5f, 0.22f), new Vector3(width, height, 0.26f), cover);
                    x += width + 0.005f;
                }
            }

            Box(root, "Crown", new Vector3(0f, 2.33f, 0.22f), new Vector3(1.48f, 0.08f, 0.44f), wood);
        }, new Vector3(0f, 1.15f, 0.2f), new Vector3(1.44f, 2.3f, 0.42f));

        // A long red banner on a rod, with a gold diamond on it and a fringe along the bottom. Its origin is the foot of its back.
        private static GameObject GetOrCreateBanner() => GetOrCreateProp("Banner", root =>
        {
            Material cloth = Cloth("Banner_Red", new Color(0.45f, 0.04f, 0.04f));
            Material gilt = Gilt();
            Box(root, "Rod", new Vector3(0f, 1.82f, 0.06f), new Vector3(1.05f, 0.04f, 0.04f), gilt);
            foreach (float x in new[] { -0.54f, 0.54f })
                Part(root, "Finial", PrimitiveType.Sphere, new Vector3(x, 1.82f, 0.06f), new Vector3(0.07f, 0.07f, 0.07f), gilt);

            Box(root, "Cloth", new Vector3(0f, 0.92f, 0.05f), new Vector3(0.9f, 1.8f, 0.02f), cloth);
            Box(root, "Border", new Vector3(0f, 0.92f, 0.058f), new Vector3(0.76f, 1.64f, 0.005f), gilt);
            Box(root, "Field", new Vector3(0f, 0.92f, 0.062f), new Vector3(0.7f, 1.58f, 0.005f), cloth);
            Part(root, "Diamond", PrimitiveType.Cube, new Vector3(0f, 1.1f, 0.066f), new Vector3(0.34f, 0.34f, 0.005f), gilt, roll: 45f);
            Box(root, "Fringe", new Vector3(0f, 0.02f, 0.05f), new Vector3(0.9f, 0.06f, 0.025f), gilt);
        });

        // An iron fire basket on three legs, heaped with glowing coals and flames. It glows without casting light.
        private static GameObject GetOrCreateBrazier() => GetOrCreateProp("Brazier", root =>
        {
            Material coals = GetOrCreateGlowMaterial("Coals", new Color(1f, 0.3f, 0.06f) * 3f);
            Material flame = GetOrCreateGlowMaterial("Torch_Flame", new Color(1f, 0.5f, 0.15f) * 6f);
            Material core = GetOrCreateGlowMaterial("Torch_Flame_Core", new Color(1f, 0.85f, 0.5f) * 8f);
            var centre = new Vector3(0f, 0f, 0.4f);
            for (int i = 0; i < 3; i++)
            {
                Quaternion turn = Quaternion.Euler(0f, i * 120f, 0f);
                CreateModelPart(root, "Leg", PrimitiveType.Cube, centre + turn * new Vector3(0f, 0.4f, 0.18f), new Vector3(0.04f, 0.85f, 0.04f),
                    turn * Quaternion.Euler(12f, 0f, 0f), Iron());
            }

            Part(root, "Bowl", PrimitiveType.Cylinder, centre + Vector3.up * 0.86f, new Vector3(0.55f, 0.2f, 0.55f), Iron());
            Part(root, "Coals", PrimitiveType.Sphere, centre + Vector3.up * 0.97f, new Vector3(0.48f, 0.14f, 0.48f), coals);
            Part(root, "Flame", PrimitiveType.Sphere, centre + Vector3.up * 1.15f, new Vector3(0.3f, 0.42f, 0.3f), flame);
            Part(root, "Flame Core", PrimitiveType.Sphere, centre + Vector3.up * 1.08f, new Vector3(0.16f, 0.24f, 0.16f), core);
        }, new Vector3(0f, 0.5f, 0.4f), new Vector3(0.6f, 1f, 0.6f));

        // ---- Industrial ----------------------------------------------------------------------------------------------------

        // A generator on a skid: a ribbed housing, an exhaust stack, a glowing dial panel and hazard-striped feet.
        private static GameObject GetOrCreateGenerator() => GetOrCreateProp("Generator", root =>
        {
            Material housing = PaintedSteel("Prop_Machine", new Color(0.28f, 0.34f, 0.3f));
            Material dial = GetOrCreateGlowMaterial("Gauge_Glow", new Color(0.3f, 1f, 0.35f) * 1.3f);
            Material pipe = GetOrCreateMaterial("Pipe_Green", new Color(0.18f, 0.3f, 0.2f), smoothness: 0.5f, metallic: 0.3f);
            Box(root, "Skid", new Vector3(0f, 0.06f, 0.45f), new Vector3(1.6f, 0.12f, 0.88f), HazardMaterial());
            Box(root, "Housing", new Vector3(-0.15f, 0.66f, 0.45f), new Vector3(1.2f, 1.08f, 0.82f), housing);
            for (int i = 0; i < 6; i++)
                Box(root, "Fin", new Vector3(-0.6f + i * 0.18f, 0.66f, 0.87f), new Vector3(0.05f, 0.9f, 0.04f), TrimDarkSteel());

            Part(root, "Motor", PrimitiveType.Cylinder, new Vector3(0.58f, 0.55f, 0.45f), new Vector3(0.5f, 0.36f, 0.5f), housing, roll: 90f);
            Part(root, "Stack", PrimitiveType.Cylinder, new Vector3(-0.5f, 1.45f, 0.25f), new Vector3(0.14f, 0.6f, 0.14f), pipe);
            Box(root, "Panel", new Vector3(0.25f, 1.28f, 0.56f), new Vector3(0.44f, 0.3f, 0.2f), TrimDarkSteel());
            foreach (float x in new[] { 0.14f, 0.36f })
                Part(root, "Dial", PrimitiveType.Cylinder, new Vector3(x, 1.3f, 0.665f), new Vector3(0.12f, 0.01f, 0.12f), dial, pitch: 90f);
        }, new Vector3(0f, 0.7f, 0.45f), new Vector3(1.6f, 1.4f, 0.9f));

        // A grey fuse box on the wall with a conduit running up out of it, and a red warning lamp.
        private static GameObject GetOrCreateFuseBox() => GetOrCreateProp("FuseBox", root =>
        {
            Material box = PaintedSteel("Prop_FuseBox", new Color(0.4f, 0.42f, 0.42f));
            Material lamp = GetOrCreateGlowMaterial("Lamp_Bulb_Red", AlarmRed * 5f);
            Box(root, "Box", new Vector3(0f, 0f, 0.1f), new Vector3(0.5f, 0.7f, 0.2f), box);
            Box(root, "Lever", new Vector3(0.28f, 0.05f, 0.12f), new Vector3(0.05f, 0.2f, 0.05f), TrimDarkSteel());
            Part(root, "Conduit", PrimitiveType.Cylinder, new Vector3(0f, 0.9f, 0.06f), new Vector3(0.05f, 1.1f, 0.05f), GetOrCreateMaterial("Pipe_Grey", new Color(0.3f, 0.31f, 0.32f), smoothness: 0.45f, metallic: 0.4f));
            Part(root, "Warning Lamp", PrimitiveType.Sphere, new Vector3(-0.15f, 0.25f, 0.21f), new Vector3(0.06f, 0.06f, 0.06f), lamp);
            Box(root, "Label", new Vector3(0.05f, -0.2f, 0.201f), new Vector3(0.2f, 0.08f, 0.004f), GetOrCreateMaterial("Sign_Yellow", HazardYellow, smoothness: 0.3f, emission: HazardYellow * 0.15f));
        });

        // Cables sagging from the ceiling in loose loops. Their origin is on the ceiling.
        private static GameObject GetOrCreateHangingCables() => GetOrCreateProp("HangingCables", root =>
        {
            Material cable = GetOrCreateMaterial("Cable", new Color(0.04f, 0.04f, 0.04f), smoothness: 0.5f);
            for (int i = 0; i < 3; i++)
            {
                float x = (i - 1) * 0.35f;
                float sag = 0.45f + i * 0.2f;
                float span = 1.2f + i * 0.3f;
                var left = new Vector3(x, 0f, -span * 0.5f);
                var right = new Vector3(x + 0.1f, 0f, span * 0.5f);
                var low = new Vector3(x + 0.05f, -sag, 0.1f * i);
                CreateLimb(root, "Cable", left, low, 0.025f, cable);
                CreateLimb(root, "Cable", low, right, 0.025f, cable);
            }

            CreateLimb(root, "Cable End", new Vector3(0.6f, 0f, 0.3f), new Vector3(0.55f, -1.1f, 0.25f), 0.03f, cable);
        });

        // ---- Exit ----------------------------------------------------------------------------------------------------------

        // A steel call panel with lit buttons, like the lift controls of the original game.
        private static GameObject GetOrCreateElevatorPanel() => GetOrCreateProp("ElevatorPanel", root =>
        {
            Material steel = GetOrCreateMaterial("Lamp_Housing", new Color(0.55f, 0.56f, 0.55f), smoothness: 0.4f, metallic: 0.2f);
            Material green = GetOrCreateGlowMaterial("Button_Green", new Color(0.3f, 1f, 0.35f) * 3f);
            Material amber = GetOrCreateGlowMaterial("Lamp_Bulb_Amber", DoorLampColour * 5f);
            Box(root, "Plate", new Vector3(0f, 0.3f, 0.02f), new Vector3(0.36f, 0.8f, 0.04f), steel);
            Box(root, "Bezel", new Vector3(0f, 0.5f, 0.045f), new Vector3(0.24f, 0.16f, 0.01f), TrimDarkSteel());
            Box(root, "Indicator", new Vector3(0f, 0.5f, 0.051f), new Vector3(0.18f, 0.1f, 0.004f), amber);
            foreach (float y in new[] { 0.2f, 0.05f })
                Part(root, "Button", PrimitiveType.Cylinder, new Vector3(0f, y, 0.05f), new Vector3(0.09f, 0.02f, 0.09f), green, pitch: 90f);
        });

        // A glowing green EXIT sign in the pixel lettering of the status bar.
        private static GameObject GetOrCreateExitSign() => GetOrCreateProp("ExitSign", root =>
        {
            Texture2D letters = GetOrCreateTexture("Sign_Exit", () => GenerateSignTexture("EXIT"), TextureWrapMode.Clamp);
            Material face = GetOrCreateCutoutMaterial("Sign_Exit", letters, new Color(0.4f, 3f, 0.6f));
            Material backing = GetOrCreateMaterial("Sign_Backing", new Color(0.03f, 0.06f, 0.035f), smoothness: 0.3f);
            Box(root, "Housing", new Vector3(0f, 0f, 0.06f), new Vector3(1.12f, 0.46f, 0.12f), TrimDarkSteel());
            Box(root, "Backing", new Vector3(0f, 0f, 0.121f), new Vector3(1.04f, 0.38f, 0.004f), backing);
            GameObject quad = CreateRenderOnlyPrimitive(PrimitiveType.Quad, "Letters", face);
            quad.transform.SetParent(root, false);
            quad.transform.SetLocalPositionAndRotation(new Vector3(0f, 0f, 0.126f), Quaternion.Euler(0f, 180f, 0f));
            quad.transform.localScale = new Vector3(0.96f, 0.3f, 1f);
        });

        // A word in the status bar's pixel font, white on clear, one texture pixel per font pixel plus a border.
        private static Texture2D GenerateSignTexture(string word)
        {
            const int border = 2;
            int width = word.Length * (GlyphWidth + 1) - 1 + border * 2;
            int height = GlyphHeight + border * 2;
            var pixels = new Color[width * height];
            for (int i = 0; i < word.Length; i++)
            {
                string[] lines = PixelGlyphs[word[i]].Split('|');
                for (int line = 0; line < GlyphHeight; line++)
                {
                    for (int x = 0; x < GlyphWidth; x++)
                    {
                        if (lines[line][x] == '#')
                            pixels[(border + GlyphHeight - 1 - line) * width + border + i * (GlyphWidth + 1) + x] = Color.white;
                    }
                }
            }

            return CreateTexture(pixels, width, height);
        }

        // ---- Decals and wear -----------------------------------------------------------------------------------------------

        private static GameObject GetOrCreateFloorDecal(string decalName, Material material, float size) => GetOrCreateProp(decalName, root =>
        {
            GameObject quad = CreateRenderOnlyPrimitive(PrimitiveType.Quad, "Decal", material);
            quad.transform.SetParent(root, false);
            quad.transform.SetLocalPositionAndRotation(Vector3.up * DecalLift, Quaternion.Euler(90f, 0f, 0f));
            quad.transform.localScale = new Vector3(size, size, 1f);
        });

        private static GameObject GetOrCreateWallDecal(string decalName, Material material, float size) => GetOrCreateProp(decalName, root =>
        {
            // A quad faces along -Z, so it is turned round to face out of the wall.
            GameObject quad = CreateRenderOnlyPrimitive(PrimitiveType.Quad, "Decal", material);
            quad.transform.SetParent(root, false);
            quad.transform.SetLocalPositionAndRotation(new Vector3(0f, size * 0.5f, DecalLift), Quaternion.Euler(0f, 180f, 0f));
            quad.transform.localScale = new Vector3(size, size, 1f);
        });

        private static Material BloodDecal() =>
            GetOrCreateDecalMaterial("Decal_Blood", GetOrCreateTexture("Blood", GenerateBloodTexture, TextureWrapMode.Clamp),
                new Color(0.32f, 0.02f, 0.02f), 0.6f);

        private static GameObject GetOrCreateBloodPool() => GetOrCreateFloorDecal("BloodPool", BloodDecal(), 1.6f);

        private static GameObject GetOrCreateBloodSplatter() => GetOrCreateWallDecal("BloodSplatter", BloodDecal(), 1.3f);

        private static GameObject GetOrCreateCrackedWall() => GetOrCreateWallDecal("CrackedWall",
            GetOrCreateDecalMaterial("Decal_Crack", GetOrCreateTexture("CrackedWall", GenerateCrackedWallTexture, TextureWrapMode.Clamp),
                Color.white, 0.05f), 1.2f);

        private static GameObject GetOrCreateDrain() => GetOrCreateFloorDecal("Drain",
            SurfaceMaterial("Floor_Grate", () => DrawGrate(22, new Color(0.32f, 0.3f, 0.28f)), 4f, smoothness: 0.3f, metallic: 0.4f), 0.9f);

        private static GameObject GetOrCreateOilStain() => GetOrCreateFloorDecal("OilStain", GetOrCreateBlobShadowMaterial(), 1.5f);

        // Chunks of masonry fallen from a broken patch of wall above them, with dust around them. No collider: the chunks are
        // small enough to walk over.
        private static GameObject GetOrCreateRubble() => GetOrCreateProp("Rubble", root =>
        {
            Material stone = GetOrCreateMaterial("Rubble", new Color(0.36f, 0.34f, 0.31f), smoothness: 0.05f);
            Material brick = GetOrCreateMaterial("Rubble_Brick", new Color(0.42f, 0.2f, 0.14f), smoothness: 0.05f);
            for (int i = 0; i < 9; i++)
            {
                float size = 0.1f + 0.05f * (i % 4);
                var position = new Vector3((i * 37 % 11 - 5) * 0.09f, size * 0.35f, 0.12f + (i * 17 % 7) * 0.08f);
                CreateModelPart(root, "Chunk", PrimitiveType.Cube, position, new Vector3(size, size * 0.7f, size * 1.2f),
                    Quaternion.Euler(i * 23f, i * 47f, i * 13f), i % 3 == 0 ? brick : stone);
            }

            GameObject dust = CreateRenderOnlyPrimitive(PrimitiveType.Quad, "Dust", GetOrCreateBlobShadowMaterial());
            dust.transform.SetParent(root, false);
            dust.transform.SetLocalPositionAndRotation(new Vector3(0f, DecalLift, 0.3f), Quaternion.Euler(90f, 0f, 0f));
            dust.transform.localScale = new Vector3(1.3f, 0.8f, 1f);

            GameObject crack = CreateRenderOnlyPrimitive(PrimitiveType.Quad, "Broken Wall", GetOrCreateDecalMaterial("Decal_Crack",
                GetOrCreateTexture("CrackedWall", GenerateCrackedWallTexture, TextureWrapMode.Clamp), Color.white, 0.05f));
            crack.transform.SetParent(root, false);
            crack.transform.SetLocalPositionAndRotation(new Vector3(0f, 1.1f, DecalLift), Quaternion.Euler(0f, 180f, 0f));
            crack.transform.localScale = new Vector3(1.1f, 1.1f, 1f);
        });

        // ---- Walls built from prefabs --------------------------------------------------------------------------------------

        // A heavy concrete column with steel angle on its corners, a hazard band round its foot and a steel cap.
        // Its origin is the middle of its tile at floor level.
        private static GameObject GetOrCreatePillar(float tileSize, float wallHeight) => GetOrCreateProp("Pillar", root =>
        {
            const float width = 1.2f;
            Box(root, "Column", new Vector3(0f, wallHeight * 0.5f, 0f), new Vector3(width, wallHeight, width), BunkerConcrete());
            Box(root, "Foot", new Vector3(0f, 0.25f, 0f), new Vector3(width + 0.06f, 0.5f, width + 0.06f), HazardMaterial());
            Box(root, "Cap", new Vector3(0f, wallHeight - 0.15f, 0f), new Vector3(width + 0.16f, 0.3f, width + 0.16f), TrimDarkSteel());
            foreach (float x in new[] { -1f, 1f })
            {
                foreach (float z in new[] { -1f, 1f })
                    Box(root, "Angle", new Vector3(x * width * 0.5f, wallHeight * 0.5f, z * width * 0.5f), new Vector3(0.1f, wallHeight, 0.1f), BeamSteel());
            }
        }, new Vector3(0f, wallHeight * 0.5f, 0f), new Vector3(1.26f, wallHeight, 1.26f), WallPrefabFolder);

        // A block of crates stacked two high, as cover. Its origin is the middle of its tile at floor level.
        private static GameObject GetOrCreateCoverCrates() => GetOrCreateProp("CoverCrates", root =>
        {
            AddCrate(root, new Vector3(-0.52f, 0f, -0.52f), 1f, CrateWood());
            AddCrate(root, new Vector3(0.52f, 0f, -0.5f), 1f, MilitaryCrate(), 4f);
            AddCrate(root, new Vector3(-0.5f, 0f, 0.52f), 1f, MilitaryCrate(), -3f);
            AddCrate(root, new Vector3(0.52f, 0f, 0.52f), 1f, CrateWood());
            AddCrate(root, new Vector3(-0.35f, 1f, -0.3f), 0.9f, CrateWood(), 10f);
            AddCrate(root, new Vector3(0.45f, 1f, 0.4f), 0.8f, MilitaryCrate(), -8f);
        }, new Vector3(0f, 0.95f, 0f), new Vector3(2.08f, 1.9f, 2.08f), WallPrefabFolder);

        // A bank of machinery: a humming cabinet with pipes rising into the ceiling, dials and a glowing sight glass.
        // Its origin is the middle of its tile at floor level.
        private static GameObject GetOrCreateMachinery(float wallHeight) => GetOrCreateProp("Machinery", root =>
        {
            Material housing = PaintedSteel("Prop_Machine", new Color(0.28f, 0.34f, 0.3f));
            Material pipe = GetOrCreateMaterial("Pipe_Green", new Color(0.18f, 0.3f, 0.2f), smoothness: 0.5f, metallic: 0.3f);
            Material glow = GetOrCreateGlowMaterial("Gauge_Glow", new Color(0.3f, 1f, 0.35f) * 1.3f);
            Material warning = GetOrCreateGlowMaterial("Lamp_Bulb_Red", AlarmRed * 5f);
            Box(root, "Plinth", new Vector3(0f, 0.1f, 0f), new Vector3(2.4f, 0.2f, 2.4f), HazardMaterial());
            Box(root, "Cabinet", new Vector3(0f, 1.1f, 0f), new Vector3(2.2f, 1.8f, 2.2f), housing);
            Box(root, "Top", new Vector3(0f, 2.05f, 0f), new Vector3(1.6f, 0.1f, 1.6f), TrimDarkSteel());
            foreach (Vector3 corner in new[] { new Vector3(-0.6f, 0f, -0.6f), new Vector3(0.6f, 0f, 0.6f), new Vector3(0.6f, 0f, -0.6f) })
            {
                Part(root, "Riser", PrimitiveType.Cylinder, corner + Vector3.up * ((wallHeight + 2f) * 0.5f),
                    new Vector3(0.2f, wallHeight - 2f, 0.2f), pipe);
            }

            for (int side = 0; side < 4; side++)
            {
                Quaternion turn = Quaternion.Euler(0f, side * 90f, 0f);
                CreateModelPart(root, "Grille", PrimitiveType.Cube, turn * new Vector3(-0.45f, 1.1f, 1.105f), new Vector3(0.8f, 1.1f, 0.02f), turn,
                    GetOrCreateMaterial("Vent_Dark", new Color(0.03f, 0.03f, 0.03f)));
                CreateModelPart(root, "Sight Glass", PrimitiveType.Cube, turn * new Vector3(0.55f, 1.4f, 1.11f), new Vector3(0.3f, 0.5f, 0.02f), turn, glow);
                CreateModelPart(root, "Warning", PrimitiveType.Sphere, turn * new Vector3(0.55f, 0.9f, 1.12f), new Vector3(0.08f, 0.08f, 0.05f), turn, warning);
            }
        }, new Vector3(0f, 1f, 0f), new Vector3(2.4f, 2f, 2.4f), WallPrefabFolder);
    }
}
