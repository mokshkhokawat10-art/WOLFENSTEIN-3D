using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using Wolfenstein.Combat;
using Wolfenstein.Core;
using Wolfenstein.Monsters;
using Wolfenstein.Pickups;
using Wolfenstein.Player;
using Wolfenstein.Waves;
using Wolfenstein.Weapons;
using Wolfenstein.World;
using Object = UnityEngine.Object;

namespace Wolfenstein.Editor
{
    // The monsters: five kinds, each with its own tuning asset and its own low-poly model (see MonsterModels), the bolts the
    // ranged ones fire, the particle effects of the fight, the manager that runs them all and the arena's waves.
    // Every model hangs on the same kind of rig of pivots, any of which it may leave out, so one view animates them all.
    // Tuning assets, projectiles and the wave set are only created when missing; the monster prefabs and their meshes are
    // always regenerated.
    public static partial class TestLevelBuilder
    {
        private const string MonsterDefinitionFolder = "Assets/Monsters";
        private const string MonsterPrefabFolder = "Assets/Prefabs/Monsters";
        private const string WaveSetPath = "Assets/Monsters/ArenaWaves.asset";

        // The model's pivots, which the view moves, and the parts it lights up. A model may leave out any pivot it has no use
        // for, such as legs on something that floats.
        private sealed class MonsterRig
        {
            public Transform Model;
            public Transform Torso;
            public Transform LeftLeg;
            public Transform RightLeg;
            public Transform LeftArm;
            public Transform RightArm;
            public Transform Head;
            public Transform Jaw;
            public Transform Muzzle;
            public Transform ChargeGlow;
            public readonly List<Transform> Sway = new List<Transform>();
            public readonly List<Renderer> Glow = new List<Renderer>();
        }

        // How a kind of monster moves, attacks and falls, for its view.
        private struct MonsterMotion
        {
            public float StrideLength;
            public float LegSwing;
            public float ArmSwing;
            public float RunLean;
            public float BobHeight;
            public float StrikeLean;
            public float WindupArmPitch;
            public float WindupLean;

            // How far back the body sits over its collider, for one that leans far forward, in metres.
            public float BodyShift;

            public float HeadTurn;
            public float JawGape;
            public float HoverBob;
            public float HoverBobRate;
            public float SwayAngle;
            public float SwayRate;
            public float FallDirection;
            public float DeathDrop;
            public float LyingHeight;
            public Color Blood;
        }

        private static (MonsterDefinition definition, MonsterController prefab, char lurkerSymbol)[] monsterBreeds;

        private static MonsterManager CreateMonsterManager(WorldManager world, PlayerController player, PickupManager pickups)
        {
            monsterBreeds ??= BuildMonsterBreeds();
            (MonsterDefinition definition, MonsterController prefab, char lurkerSymbol)[] breeds = monsterBreeds;

            var managerObject = new GameObject("Monsters");
            var manager = managerObject.AddComponent<MonsterManager>();
            var serializedManager = new SerializedObject(manager);
            Set(serializedManager,
                ("world", world),
                ("pickups", pickups),
                ("effects", CreateMonsterEffects(managerObject.transform)),
                ("playerHealth", player.GetComponent<Health>()),
                ("playerScore", player.GetComponent<Score>()),
                ("playerWeapon", player.GetComponent<WeaponController>()));

            SerializedProperty breedList = serializedManager.FindProperty("breeds");
            SerializedProperty lurkerList = serializedManager.FindProperty("lurkers");
            breedList.arraySize = breeds.Length;
            lurkerList.arraySize = breeds.Length;
            for (int i = 0; i < breeds.Length; i++)
            {
                SerializedProperty breed = breedList.GetArrayElementAtIndex(i);
                breed.FindPropertyRelative("definition").objectReferenceValue = breeds[i].definition;
                breed.FindPropertyRelative("prefab").objectReferenceValue = breeds[i].prefab;

                SerializedProperty lurker = lurkerList.GetArrayElementAtIndex(i);
                lurker.FindPropertyRelative("symbol").intValue = breeds[i].lurkerSymbol;
                lurker.FindPropertyRelative("definition").objectReferenceValue = breeds[i].definition;
            }

            serializedManager.ApplyModifiedPropertiesWithoutUndo();
            return manager;
        }

        // The five kinds of monster, their tuning and their prefabs. Made once per build, as every scene holds the same monsters.
        private static (MonsterDefinition definition, MonsterController prefab, char lurkerSymbol)[] BuildMonsterBreeds()
        {
            MonsterDefinition stalker = GetOrCreateMonsterDefinition("Stalker", StalkerTuning);
            MonsterDefinition brute = GetOrCreateMonsterDefinition("Brute", BruteTuning);
            MonsterDefinition spitter = GetOrCreateMonsterDefinition("Spitter", SpitterTuning);
            MonsterDefinition revenant = GetOrCreateMonsterDefinition("Revenant", RevenantTuning);
            MonsterDefinition demon = GetOrCreateMonsterDefinition("Demon", DemonTuning);

            // Each body's collider is sized to the model the player aims at, and fits the 3 m doors and spawn pens.
            // The spitter's collider stands on the floor, as the one it walks with, and reaches up round its floating body.
            WriteMonsterPalette();
            return new[]
            {
                (stalker, BuildMonsterPrefab("Stalker", stalker, 0.4f, 2f, 1.45f, BuildStalkerModel, StalkerMotion()), '1'),
                (brute, BuildMonsterPrefab("Brute", brute, 0.72f, 2.7f, 2.05f, BuildBruteModel, BruteMotion()), '2'),
                (spitter, BuildMonsterPrefab("Spitter", spitter, 0.55f, 2.3f, 1.6f, BuildSpitterModel, SpitterMotion()), '3'),
                (revenant, BuildMonsterPrefab("Revenant", revenant, 0.8f, 2.6f, 2.05f, BuildRevenantModel, RevenantMotion()), '4'),
                (demon, BuildMonsterPrefab("Demon", demon, 0.45f, 2.3f, 1.75f, BuildDemonModel, DemonMotion()), '5'),
            };
        }

        private static WaveManager CreateWaveManager(WorldManager world, MonsterManager monsters, PickupManager pickups, PlayerController player,
            WaveSet waveSet)
        {
            var waves = new GameObject("Waves").AddComponent<WaveManager>();
            SetFields(waves,
                ("world", world),
                ("monsters", monsters),
                ("pickups", pickups),
                ("player", player.GetComponent<Health>()),
                ("waves", waveSet));
            return waves;
        }

        // ---- Tuning --------------------------------------------------------------------------------------------------------

        private static MonsterDefinition GetOrCreateMonsterDefinition(string monsterName, Action<SerializedObject> tune)
        {
            string path = $"{MonsterDefinitionFolder}/{monsterName}.asset";
            var definition = AssetDatabase.LoadAssetAtPath<MonsterDefinition>(path);
            if (definition != null)
                return definition;

            EnsureFolder(MonsterDefinitionFolder);
            definition = ScriptableObject.CreateInstance<MonsterDefinition>();
            AssetDatabase.CreateAsset(definition, path);
            var serializedDefinition = new SerializedObject(definition);
            tune(serializedDefinition);
            serializedDefinition.ApplyModifiedPropertiesWithoutUndo();
            return definition;
        }

        // Fast, frail and vicious: a pistol shot drops one, but they come in packs and run nearly as fast as the player.
        private static void StalkerTuning(SerializedObject definition) => Set(definition,
            ("displayName", "STALKER"), ("maxHealth", 16), ("painChance", 0.6f), ("painDuration", 0.25f),
            ("moveSpeed", 5.2f), ("turnSpeed", 600f), ("sightRange", 30f),
            ("hasMelee", true), ("meleeReach", 0.9f), ("meleeDamage", 6), ("meleeWindup", 0.3f), ("meleeCooldown", 0.8f),
            ("spawnDuration", 0.8f), ("killScore", 100), ("drop", GetOrCreateDroppedAmmoClip()), ("dropChance", 0.4f));

        // Slow and hugely tough, and its blows land with a shockwave that catches anyone close by.
        private static void BruteTuning(SerializedObject definition) => Set(definition,
            ("displayName", "BRUTE"), ("maxHealth", 160), ("painChance", 0.12f), ("painDuration", 0.35f),
            ("moveSpeed", 2.3f), ("turnSpeed", 240f), ("sightRange", 30f),
            ("hasMelee", true), ("meleeReach", 1.3f), ("meleeDamage", 28), ("meleeWindup", 0.7f), ("meleeCooldown", 1.6f),
            ("meleeSplashRadius", 1.6f),
            ("spawnDuration", 1.2f), ("killScore", 500), ("drop", GetOrCreateAmmoClip()), ("dropChance", 1f));

        // Keeps its distance and spits bolts of fire from its eye, backing away from anyone who closes in.
        private static void SpitterTuning(SerializedObject definition) => Set(definition,
            ("displayName", "SPITTER"), ("maxHealth", 50), ("painChance", 0.5f), ("painDuration", 0.3f),
            ("moveSpeed", 3.2f), ("turnSpeed", 420f), ("sightRange", 35f),
            ("hasMelee", false),
            ("hasRanged", true), ("projectile", GetOrCreateProjectile("EyeBolt", new Color(1f, 0.32f, 0.06f))), ("projectileSpeed", 13f),
            ("projectileDamage", 10), ("projectilesPerVolley", 1), ("minFireRange", 3f), ("maxFireRange", 24f), ("preferredDistance", 11f),
            ("fireWindup", 0.55f), ("fireCooldown", 2.2f),
            ("spawnDuration", 0.9f), ("killScore", 250), ("drop", GetOrCreateDroppedAmmoClip()), ("dropChance", 0.5f));

        // The elite: tough and fast, it charges, cuts with its arm blades and hurls fans of fire from range.
        private static void RevenantTuning(SerializedObject definition) => Set(definition,
            ("displayName", "REVENANT"), ("maxHealth", 260), ("painChance", 0.15f), ("painDuration", 0.25f),
            ("moveSpeed", 5.4f), ("turnSpeed", 540f), ("sightRange", 40f),
            ("hasMelee", true), ("meleeReach", 1.1f), ("meleeDamage", 24), ("meleeWindup", 0.4f), ("meleeCooldown", 1f),
            ("hasRanged", true), ("projectile", GetOrCreateProjectile("HellBolt", new Color(1f, 0.35f, 0.08f))), ("projectileSpeed", 16f),
            ("projectileDamage", 12), ("projectilesPerVolley", 3), ("volleySpread", 7f), ("minFireRange", 5f), ("maxFireRange", 26f),
            ("preferredDistance", 9f), ("fireWindup", 0.6f), ("fireCooldown", 3f),
            ("canCharge", true), ("chargeSpeedMultiplier", 2.2f), ("chargeMinRange", 5f), ("chargeMaxRange", 13f),
            ("chargeDuration", 0.9f), ("chargeCooldown", 5f),
            ("spawnDuration", 1.4f), ("killScore", 2000),
            ("drop", GetOrCreatePickup("FirstAidKit", health: 25, createModel: CreateFirstAidKitModel)), ("dropChance", 1f));

        // Between the others: tougher than a spitter and weaker than a brute, it claws up close and throws a fireball from
        // range, but never charges.
        private static void DemonTuning(SerializedObject definition) => Set(definition,
            ("displayName", "DEMON"), ("maxHealth", 70), ("painChance", 0.35f), ("painDuration", 0.3f),
            ("moveSpeed", 4.2f), ("turnSpeed", 480f), ("sightRange", 32f),
            ("hasMelee", true), ("meleeReach", 1f), ("meleeDamage", 14), ("meleeWindup", 0.4f), ("meleeCooldown", 1.1f),
            ("hasRanged", true), ("projectile", GetOrCreateProjectile("HellBolt", new Color(1f, 0.35f, 0.08f))), ("projectileSpeed", 14f),
            ("projectileDamage", 12), ("projectilesPerVolley", 1), ("minFireRange", 4f), ("maxFireRange", 22f), ("preferredDistance", 8f),
            ("fireWindup", 0.5f), ("fireCooldown", 3.5f),
            ("spawnDuration", 1f), ("killScore", 400), ("drop", GetOrCreateDroppedAmmoClip()), ("dropChance", 0.6f));

        // Six waves, each harder than the last through more monsters, a meaner mix, a quicker pace and a little more
        // toughness, speed and bite. Basic monsters stay a single pistol shot throughout; the scaling tells on the big ones.
        private static WaveSet GetOrCreateArenaWaves()
        {
            var existing = AssetDatabase.LoadAssetAtPath<WaveSet>(WaveSetPath);
            if (existing != null)
                return existing;

            var stalker = AssetDatabase.LoadAssetAtPath<MonsterDefinition>($"{MonsterDefinitionFolder}/Stalker.asset");
            var brute = AssetDatabase.LoadAssetAtPath<MonsterDefinition>($"{MonsterDefinitionFolder}/Brute.asset");
            var spitter = AssetDatabase.LoadAssetAtPath<MonsterDefinition>($"{MonsterDefinitionFolder}/Spitter.asset");
            var revenant = AssetDatabase.LoadAssetAtPath<MonsterDefinition>($"{MonsterDefinitionFolder}/Revenant.asset");
            var demon = AssetDatabase.LoadAssetAtPath<MonsterDefinition>($"{MonsterDefinitionFolder}/Demon.asset");
            Pickup clip = GetOrCreateAmmoClip();
            Pickup kit = GetOrCreatePickup("FirstAidKit", health: 25, createModel: CreateFirstAidKitModel);
            Pickup food = GetOrCreatePickup("Food", health: 10, createModel: CreateFoodModel);

            Wave[] waves =
            {
                new Wave(new[] { new WaveGroup(stalker, 8) },
                    new MonsterModifiers(1f, 1f, 1f), 0.7f, 8, new[] { clip, clip, food }),
                new Wave(new[] { new WaveGroup(stalker, 12), new WaveGroup(spitter, 2) },
                    new MonsterModifiers(1.05f, 1.02f, 1.05f), 0.55f, 10, new[] { clip, clip, kit }),
                new Wave(new[] { new WaveGroup(stalker, 15), new WaveGroup(spitter, 3), new WaveGroup(brute, 2) },
                    new MonsterModifiers(1.1f, 1.04f, 1.1f), 0.5f, 12, new[] { clip, clip, clip, kit }),
                new Wave(new[] { new WaveGroup(stalker, 20), new WaveGroup(spitter, 4), new WaveGroup(brute, 3), new WaveGroup(demon, 2) },
                    new MonsterModifiers(1.15f, 1.06f, 1.15f), 0.45f, 14, new[] { clip, clip, clip, kit, food }),
                new Wave(new[] { new WaveGroup(stalker, 22), new WaveGroup(spitter, 5), new WaveGroup(brute, 3), new WaveGroup(demon, 3),
                    new WaveGroup(revenant, 1, spawnLast: true) },
                    new MonsterModifiers(1.2f, 1.08f, 1.2f), 0.4f, 16, new[] { clip, clip, clip, clip, kit, kit }),
                new Wave(new[] { new WaveGroup(stalker, 28), new WaveGroup(spitter, 6), new WaveGroup(brute, 4), new WaveGroup(demon, 4),
                    new WaveGroup(revenant, 2, spawnLast: true) },
                    new MonsterModifiers(1.25f, 1.1f, 1.25f), 0.35f, 20, Array.Empty<Pickup>()),
            };

            WaveSet set = WaveSet.Create(2.5f, 6f, waves);
            AssetDatabase.CreateAsset(set, WaveSetPath);
            return set;
        }

        // ---- Prefabs -------------------------------------------------------------------------------------------------------

        private static MonsterController BuildMonsterPrefab(string monsterName, MonsterDefinition definition, float radius, float height,
            float eyeHeight, Action<MonsterRig> buildModel, MonsterMotion motion)
        {
            EnsureFolder(MonsterPrefabFolder);
            var root = new GameObject(monsterName);

            var body = root.AddComponent<CharacterController>();
            body.height = height;
            body.radius = radius;
            body.center = new Vector3(0f, height * 0.5f, 0f);
            body.stepOffset = 0.3f;
            body.slopeLimit = 45f;
            body.skinWidth = 0.05f;
            body.minMoveDistance = 0f;

            var eyes = new GameObject("Eyes").transform;
            eyes.SetParent(root.transform, false);
            eyes.localPosition = new Vector3(0f, eyeHeight, 0.15f);

            SetFields(root.AddComponent<Health>(), ("maxHealth", definition.MaxHealth));
            root.AddComponent<MonsterMotor>();
            var monster = root.AddComponent<MonsterController>();

            // A character controller rests one skin width above the floor, so the body is lowered by as much to stand on it,
            // and a body that leans far forward is set back to sit over its collider.
            var bodyRoot = new GameObject("Body").transform;
            bodyRoot.SetParent(root.transform, false);
            bodyRoot.localPosition = new Vector3(0f, -body.skinWidth, -motion.BodyShift);
            CreateBlobShadow(bodyRoot, radius * 3.2f);

            var rig = new MonsterRig { Model = new GameObject("Model").transform };
            rig.Model.SetParent(bodyRoot, false);
            buildModel(rig);

            SetFields(monster, ("definition", definition), ("eyes", eyes), ("muzzle", rig.Muzzle));

            var view = root.AddComponent<MonsterView>();
            var serializedView = new SerializedObject(view);
            Set(serializedView,
                ("monster", monster), ("model", rig.Model), ("torso", rig.Torso),
                ("leftLeg", rig.LeftLeg), ("rightLeg", rig.RightLeg), ("leftArm", rig.LeftArm), ("rightArm", rig.RightArm),
                ("head", rig.Head), ("jaw", rig.Jaw), ("chargeGlow", rig.ChargeGlow),
                ("strideLength", motion.StrideLength), ("legSwing", motion.LegSwing), ("armSwing", motion.ArmSwing),
                ("runLean", motion.RunLean), ("bobHeight", motion.BobHeight), ("strikeLean", motion.StrikeLean),
                ("windupArmPitch", motion.WindupArmPitch), ("windupLean", motion.WindupLean), ("headTurn", motion.HeadTurn), ("jawGape", motion.JawGape),
                ("hoverBob", motion.HoverBob), ("hoverBobRate", motion.HoverBobRate), ("swayAngle", motion.SwayAngle),
                ("swayRate", motion.SwayRate), ("riseDepth", height), ("fallDirection", motion.FallDirection),
                ("deathDrop", motion.DeathDrop), ("lyingHeight", motion.LyingHeight), ("bloodColour", motion.Blood));
            SetArray(serializedView.FindProperty("glowParts"), rig.Glow);
            SetArray(serializedView.FindProperty("swayParts"), rig.Sway);
            serializedView.ApplyModifiedPropertiesWithoutUndo();

            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, $"{MonsterPrefabFolder}/{monsterName}.prefab");
            Object.DestroyImmediate(root);
            return prefab.GetComponent<MonsterController>();
        }

        private static Transform Pivot(Transform parent, string pivotName, Vector3 position, Vector3 restAngles = default)
        {
            var pivot = new GameObject(pivotName).transform;
            pivot.SetParent(parent, false);
            pivot.SetLocalPositionAndRotation(position, Quaternion.Euler(restAngles));
            return pivot;
        }

        private static Renderer Glowing(MonsterRig rig, GameObject part)
        {
            var glowRenderer = part.GetComponent<Renderer>();
            rig.Glow.Add(glowRenderer);
            return glowRenderer;
        }

        // ---- Projectiles ---------------------------------------------------------------------------------------------------

        // A bolt: a white-hot core in a coloured glow, trailing a fading streak. It glows without casting light.
        private static MonsterProjectile GetOrCreateProjectile(string boltName, Color colour)
        {
            string path = $"{MonsterPrefabFolder}/{boltName}.prefab";
            var existing = AssetDatabase.LoadAssetAtPath<MonsterProjectile>(path);
            if (existing != null)
                return existing;

            EnsureFolder(MonsterPrefabFolder);
            var root = new GameObject(boltName);
            CreateModelPart(root.transform, "Glow", PrimitiveType.Sphere, Vector3.zero, Vector3.one * 0.34f, Quaternion.identity,
                GetOrCreateGlowMaterial($"{boltName}_Glow", colour * 4f));
            CreateModelPart(root.transform, "Core", PrimitiveType.Sphere, Vector3.zero, Vector3.one * 0.18f, Quaternion.identity,
                GetOrCreateGlowMaterial($"{boltName}_Core", Color.Lerp(colour, Color.white, 0.6f) * 8f));

            var trail = root.AddComponent<TrailRenderer>();
            trail.time = 0.2f;
            trail.minVertexDistance = 0.1f;
            trail.widthCurve = AnimationCurve.Linear(0f, 0.26f, 1f, 0f);
            trail.colorGradient = new Gradient
            {
                colorKeys = new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(colour, 1f) },
                alphaKeys = new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0f, 1f) }
            };
            trail.sharedMaterial = GetOrCreateSoftParticleMaterial($"Effect_Trail_{boltName}", colour * 3f, additive: true);
            trail.shadowCastingMode = ShadowCastingMode.Off;
            trail.receiveShadows = false;

            SetFields(root.AddComponent<MonsterProjectile>(), ("impactColour", colour * 3f), ("trail", trail));

            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, path);
            Object.DestroyImmediate(root);
            return prefab.GetComponent<MonsterProjectile>();
        }

        // ---- Effects -------------------------------------------------------------------------------------------------------

        // A few shared particle systems that the effects emit into, in world space.
        private static MonsterEffects CreateMonsterEffects(Transform parent)
        {
            var effectsObject = new GameObject("Effects");
            effectsObject.transform.SetParent(parent, false);
            var effects = effectsObject.AddComponent<MonsterEffects>();

            ParticleSystem embers = CreateBurstParticles(effectsObject.transform, "Embers",
                GetOrCreateSoftParticleMaterial("Effect_Ember", Color.white * 3f, additive: true), 1.1f, new Vector2(0.04f, 0.09f),
                Color.white, Color.white, -0.15f);
            ParticleSystem gore = CreateBurstParticles(effectsObject.transform, "Gore", GetOrCreateParticleMaterial("Effect_Gore", Color.white),
                1.3f, new Vector2(0.06f, 0.14f), Color.white, Color.white, 1.6f);
            ParticleSystem mist = CreateBurstParticles(effectsObject.transform, "Mist",
                GetOrCreateSoftParticleMaterial("Effect_Mist", new Color(1f, 1f, 1f, 0.55f), additive: false), 1.3f, new Vector2(0.5f, 0.9f),
                Color.white, Color.white, -0.03f);
            ParticleSystem sparks = CreateBurstParticles(effectsObject.transform, "Sparks",
                GetOrCreateSoftParticleMaterial("Effect_Bolt_Spark", Color.white * 3f, additive: true), 0.35f, new Vector2(0.03f, 0.07f),
                Color.white, Color.white, 0.8f);
            ParticleSystem rings = CreateBurstParticles(effectsObject.transform, "Rings",
                GetOrCreateSoftParticleMaterial("Effect_Ring", Color.white * 2.5f, additive: true,
                    GetOrCreateTexture("Ring", GenerateRingTexture, TextureWrapMode.Clamp, FilterMode.Bilinear)),
                0.8f, new Vector2(1f, 1f), Color.white, Color.white, 0f);

            // Mist swells as it fades; rings lie flat on the floor and spread outward as they fade.
            GrowAndFade(mist, 0.6f, 1.7f);
            GrowAndFade(rings, 0.25f, 1f);
            ParticleSystem.MainModule ringMain = rings.main;
            ringMain.startRotation3D = true;
            rings.GetComponent<ParticleSystemRenderer>().alignment = ParticleSystemRenderSpace.World;
            foreach (ParticleSystem system in new[] { embers, gore, mist, sparks, rings })
            {
                ParticleSystem.MainModule main = system.main;
                main.maxParticles = 1024;
            }

            SetFields(effects, ("embers", embers), ("gore", gore), ("mist", mist), ("sparks", sparks), ("rings", rings));
            return effects;
        }

        private static void GrowAndFade(ParticleSystem system, float startScale, float endScale)
        {
            ParticleSystem.SizeOverLifetimeModule size = system.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.EaseInOut(0f, startScale, 1f, endScale));

            ParticleSystem.ColorOverLifetimeModule colour = system.colorOverLifetime;
            colour.enabled = true;
            colour.color = new ParticleSystem.MinMaxGradient(new Gradient
            {
                colorKeys = new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                alphaKeys = new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0.8f, 0.4f), new GradientAlphaKey(0f, 1f) }
            });
        }

        // Unlit particles drawn with the transparent objects, soft-edged, either added to what is behind them so they glow
        // or blended over it.
        private static Material GetOrCreateSoftParticleMaterial(string materialName, Color colour, bool additive, Texture2D texture = null)
        {
            string path = $"{MaterialFolder}/{materialName}.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material != null)
                return material;

            texture ??= GetOrCreateTexture("SoftDot", GenerateSoftDotTexture, TextureWrapMode.Clamp, FilterMode.Bilinear);
            material = new Material(Shader.Find(ParticleShaderName));
            material.SetTexture("_BaseMap", texture);
            material.SetColor("_BaseColor", colour);
            material.SetFloat("_Surface", 1f);
            material.SetFloat("_Blend", additive ? 2f : 0f);
            material.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            material.SetFloat("_DstBlend", (float)(additive ? BlendMode.One : BlendMode.OneMinusSrcAlpha));
            material.SetFloat("_SrcBlendAlpha", (float)BlendMode.One);
            material.SetFloat("_DstBlendAlpha", (float)BlendMode.OneMinusSrcAlpha);
            material.SetFloat("_ZWrite", 0f);
            material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            material.SetOverrideTag("RenderType", "Transparent");
            material.renderQueue = (int)RenderQueue.Transparent;
            AssetDatabase.CreateAsset(material, path);
            return material;
        }
    }
}
