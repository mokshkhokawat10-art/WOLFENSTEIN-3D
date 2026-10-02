using UnityEditor;
using UnityEngine;
using Wolfenstein.Combat;
using Wolfenstein.Core;
using Wolfenstein.Enemies;
using Wolfenstein.Pickups;
using Wolfenstein.Player;
using Wolfenstein.Weapons;
using Wolfenstein.World;
using Object = UnityEngine.Object;

namespace Wolfenstein.Editor
{
    // The guard: its tuning asset, which drops a clip of ammo, a blocky low-poly soldier with a rifle,
    // and the manager that places guards in the level.
    public static partial class TestLevelBuilder
    {
        private const string GuardDefinitionPath = "Assets/Enemies/Guard.asset";
        private const string GuardPrefabPath = "Assets/Prefabs/Guard.prefab";

        private const int GuardHealth = 25;

        // PhysX gives raycasts and overlap tests a copy of a character controller's capsule scaled down to 80%.
        // The guard's controller is sized so that copy covers the 1.75 m model, and the player's eye line with it,
        // so level shots at the guard's head hit. It moves with the full size, which is still well below the ceiling.
        private const float GuardHeight = 2.2f;
        private const float GuardRadius = 0.35f;
        private static readonly Vector3 GuardEyePosition = new Vector3(0f, 1.55f, 0.12f);

        // Made once per build, as every scene of the build holds the same guard.
        private static EnemyController guardPrefab;

        private static EnemyManager CreateEnemyManager(WorldManager world, PlayerController player, PickupManager pickups)
        {
            if (guardPrefab == null)
                guardPrefab = BuildGuardPrefab();

            var enemies = new GameObject("Enemies").AddComponent<EnemyManager>();
            SetFields(enemies,
                ("world", world),
                ("guardPrefab", guardPrefab),
                ("pickups", pickups),
                ("playerHealth", player.GetComponent<Health>()),
                ("playerWeapon", player.GetComponent<WeaponController>()),
                ("playerScore", player.GetComponent<Score>()));
            return enemies;
        }

        private static EnemyDefinition GetOrCreateGuardDefinition()
        {
            var definition = AssetDatabase.LoadAssetAtPath<EnemyDefinition>(GuardDefinitionPath);
            if (definition != null)
                return definition;

            EnsureFolder("Assets/Enemies");
            definition = ScriptableObject.CreateInstance<EnemyDefinition>();
            AssetDatabase.CreateAsset(definition, GuardDefinitionPath);
            SetFields(definition, ("drop", GetOrCreateDroppedAmmoClip()));
            return definition;
        }

        private static EnemyController BuildGuardPrefab()
        {
            var root = new GameObject("Guard");

            var body = root.AddComponent<CharacterController>();
            body.height = GuardHeight;
            body.radius = GuardRadius;
            body.center = new Vector3(0f, GuardHeight * 0.5f, 0f);
            body.stepOffset = 0.3f;
            body.slopeLimit = 45f;
            body.skinWidth = 0.05f;
            body.minMoveDistance = 0f;

            var eyes = new GameObject("Eyes").transform;
            eyes.SetParent(root.transform, false);
            eyes.localPosition = GuardEyePosition;

            SetFields(root.AddComponent<Health>(), ("maxHealth", GuardHealth));
            root.AddComponent<EnemyMotor>();
            var enemy = root.AddComponent<EnemyController>();
            SetFields(enemy,
                ("definition", GetOrCreateGuardDefinition()),
                ("eyes", eyes));

            CreateGuardModel(root.transform, enemy, body.skinWidth);

            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, GuardPrefabPath);
            Object.DestroyImmediate(root);
            return prefab.GetComponent<EnemyController>();
        }

        // A soldier in the tan uniform and helmet of the original game's guard, facing +Z with a rifle held ready in both
        // hands. The legs and the rifle hang from pivots at the hips and shoulders, so the view can swing and raise them.
        // A character controller rests one skin width above the floor, so the body is lowered by as much to stand on it.
        private static void CreateGuardModel(Transform guard, EnemyController enemy, float skinWidth)
        {
            Texture2D fabric = GetOrCreateFabricTexture();
            Material uniform = GetOrCreateMaterial("Guard_Uniform", new Color(0.62f, 0.49f, 0.3f), fabric, smoothness: 0.05f);
            Material trim = GetOrCreateMaterial("Guard_Uniform_Trim", new Color(0.4f, 0.3f, 0.17f), fabric, smoothness: 0.05f);
            Material leather = GetOrCreateMaterial("Guard_Leather", new Color(0.13f, 0.09f, 0.06f), smoothness: 0.45f);
            Material skin = GetOrCreateMaterial("Guard_Skin", new Color(0.86f, 0.64f, 0.5f), smoothness: 0.2f);
            Material eyes = GetOrCreateMaterial("Guard_Eyes", new Color(0.04f, 0.04f, 0.05f), smoothness: 0.6f);
            Material helmet = GetOrCreateMaterial("Guard_Helmet", new Color(0.34f, 0.37f, 0.27f), smoothness: 0.35f, metallic: 0.2f);
            Material brass = BrassMaterial();

            var body = new GameObject("Body").transform;
            body.SetParent(guard, false);
            body.localPosition = Vector3.down * skinWidth;
            CreateBlobShadow(body, 0.95f);

            var model = new GameObject("Model").transform;
            model.SetParent(body, false);

            Transform leftLeg = CreateGuardLeg(model, "Left Leg", -0.11f, uniform, leather);
            Transform rightLeg = CreateGuardLeg(model, "Right Leg", 0.11f, uniform, leather);
            CreateModelPart(model, "Torso", new Vector3(0f, 1.16f, 0f), new Vector3(0.44f, 0.56f, 0.26f), 0f, uniform);
            CreateModelPart(model, "Collar", new Vector3(0f, 1.43f, 0f), new Vector3(0.24f, 0.05f, 0.2f), 0f, trim);
            foreach (float side in new[] { -1f, 1f })
            {
                CreateModelPart(model, "Pocket", new Vector3(side * 0.11f, 1.27f, 0.133f), new Vector3(0.12f, 0.1f, 0.012f), 0f, trim);
                CreateModelPart(model, "Shoulder Board", new Vector3(side * 0.19f, 1.44f, 0f), new Vector3(0.07f, 0.02f, 0.14f), 0f, trim);
                CreateModelPart(model, "Eye", new Vector3(side * 0.05f, 1.575f, 0.111f), new Vector3(0.04f, 0.024f, 0.01f), 0f, eyes);
            }

            CreateModelPart(model, "Belt", new Vector3(0f, 0.9f, 0f), new Vector3(0.46f, 0.08f, 0.28f), 0f, leather);
            CreateModelPart(model, "Buckle", new Vector3(0f, 0.9f, 0.142f), new Vector3(0.07f, 0.055f, 0.012f), 0f, brass);
            CreateModelPart(model, "Strap", PrimitiveType.Cube, new Vector3(0f, 1.17f, 0.134f), new Vector3(0.045f, 0.6f, 0.01f),
                Quaternion.Euler(0f, 0f, -38f), leather);
            CreateModelPart(model, "Head", new Vector3(0f, 1.55f, 0f), new Vector3(0.22f, 0.24f, 0.22f), 0f, skin);
            CreateModelPart(model, "Nose", new Vector3(0f, 1.54f, 0.118f), new Vector3(0.03f, 0.05f, 0.02f), 0f, skin);
            CreateModelPart(model, "Helmet", PrimitiveType.Sphere, new Vector3(0f, 1.655f, -0.005f), new Vector3(0.28f, 0.2f, 0.3f), Quaternion.identity, helmet);
            CreateModelPart(model, "Helmet Brim", PrimitiveType.Cylinder, new Vector3(0f, 1.62f, -0.005f), new Vector3(0.33f, 0.015f, 0.35f), Quaternion.identity, helmet);

            var weapon = new GameObject("Weapon").transform;
            weapon.SetParent(model, false);
            weapon.localPosition = new Vector3(0f, 1.36f, 0.05f);
            Vector3 muzzle = CreateGuardRifle(weapon);

            // The right hand holds the wrist of the stock and the left the fore-end, each arm running from its shoulder.
            var rightHand = new Vector3(0.06f, -0.045f, 0.12f);
            var leftHand = new Vector3(0.01f, -0.04f, 0.42f);
            CreateLimb(weapon, "Right Arm", new Vector3(0.2f, 0f, 0f), rightHand, 0.1f, uniform);
            CreateLimb(weapon, "Left Arm", new Vector3(-0.2f, 0f, 0f), leftHand, 0.1f, uniform);
            CreateModelPart(weapon, "Right Hand", rightHand, new Vector3(0.08f, 0.08f, 0.08f), 0f, skin);
            CreateModelPart(weapon, "Left Hand", leftHand, new Vector3(0.08f, 0.08f, 0.08f), 0f, skin);

            // The flash faces forward, toward whoever is being shot at. Its light is dimmer than the pistol's,
            // because the guard's own body is right behind it and would otherwise be washed out.
            GameObject muzzleFlash = CreateMuzzleFlash(weapon, muzzle, 0.4f);
            muzzleFlash.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
            Light flashLight = muzzleFlash.GetComponentInChildren<Light>(true);
            flashLight.intensity = 1.2f;
            flashLight.range = 4f;

            SetFields(guard.gameObject.AddComponent<EnemyView>(),
                ("enemy", enemy),
                ("model", model),
                ("leftLeg", leftLeg),
                ("rightLeg", rightLeg),
                ("weapon", weapon),
                ("muzzleFlash", muzzleFlash));
        }

        // A bolt-action rifle pointing along +Z: wooden stock and fore-end, steel receiver and barrel. Returns the muzzle.
        private static Vector3 CreateGuardRifle(Transform weapon)
        {
            Material steel = GunSteel();
            Material darkSteel = GunDarkSteel();
            Material wood = GunWood();

            CreateModelPart(weapon, "Stock", new Vector3(0.02f, -0.03f, 0.03f), new Vector3(0.06f, 0.1f, 0.22f), 0f, wood);
            CreateModelPart(weapon, "Receiver", new Vector3(0.02f, 0f, 0.22f), new Vector3(0.05f, 0.065f, 0.2f), 0f, steel);
            CreateModelPart(weapon, "Bolt Handle", new Vector3(0.055f, 0.02f, 0.2f), new Vector3(0.05f, 0.012f, 0.012f), 0f, darkSteel);
            CreateModelPart(weapon, "Fore-end", new Vector3(0.02f, -0.01f, 0.43f), new Vector3(0.05f, 0.05f, 0.24f), 0f, wood);
            CreateModelPart(weapon, "Barrel", PrimitiveType.Cylinder, new Vector3(0.02f, 0.005f, 0.63f), new Vector3(0.022f, 0.2f, 0.022f),
                Quaternion.Euler(90f, 0f, 0f), darkSteel);
            CreateModelPart(weapon, "Front Sight", new Vector3(0.02f, 0.03f, 0.71f), new Vector3(0.008f, 0.02f, 0.01f), 0f, darkSteel);
            return new Vector3(0.02f, 0.005f, 0.74f);
        }

        // A box from one point to another, such as an arm from the shoulder to the hand.
        private static void CreateLimb(Transform parent, string limbName, Vector3 from, Vector3 to, float thickness, Material material)
        {
            Vector3 along = to - from;
            CreateModelPart(parent, limbName, PrimitiveType.Cube, (from + to) * 0.5f, new Vector3(thickness, thickness, along.magnitude),
                Quaternion.LookRotation(along), material);
        }

        // A leg hanging from a pivot at the hip, in a tall boot.
        private static Transform CreateGuardLeg(Transform model, string legName, float side, Material trousers, Material boot)
        {
            var pivot = new GameObject(legName).transform;
            pivot.SetParent(model, false);
            pivot.localPosition = new Vector3(side, 0.86f, 0f);

            CreateModelPart(pivot, "Trousers", new Vector3(0f, -0.3f, 0f), new Vector3(0.15f, 0.6f, 0.17f), 0f, trousers);
            CreateModelPart(pivot, "Boot Shaft", new Vector3(0f, -0.66f, 0f), new Vector3(0.16f, 0.3f, 0.18f), 0f, boot);
            CreateModelPart(pivot, "Boot", new Vector3(0f, -0.81f, 0.04f), new Vector3(0.16f, 0.1f, 0.26f), 0f, boot);
            return pivot;
        }
    }
}
