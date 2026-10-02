using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using Wolfenstein.Player;
using Wolfenstein.Weapons;
using Object = UnityEngine.Object;
using Random = System.Random;

namespace Wolfenstein.Editor
{
    // The player's pistol: its tuning asset, the first-person model held in the player's hand with its muzzle flash and sway,
    // the bullet mark, and the dust, sparks and blood thrown up where shots land.
    public static partial class TestLevelBuilder
    {
        private const string PistolPath = "Assets/Weapons/Pistol.asset";
        private const string BulletMarkPrefabPath = "Assets/Prefabs/BulletMark.prefab";
        private const string UnlitShaderName = "Universal Render Pipeline/Unlit";

        // Where the pistol sits in camera space: a little right of centre and seen from behind, pointing into the screen with
        // the hand resting on the status bar, as in the original game. The model is scaled down and held close, which looks
        // the same as a full-size pistol held further out, but keeps the whole model inside the player's collider radius,
        // so it never pokes through a wall the player is pressed against.
        private static readonly Vector3 WeaponViewPosition = new Vector3(0.045f, -0.063f, 0.21f);
        private static readonly Vector3 WeaponViewAngles = new Vector3(-1f, -2f, 0f);
        private const float WeaponViewScale = 0.7f;
        private static readonly Vector3 MuzzlePosition = new Vector3(0f, 0.03f, 0.2f);

        private static void AddWeapon(GameObject player, Transform playerCamera)
        {
            player.AddComponent<AmmoSupply>();

            var weapon = player.AddComponent<WeaponController>();
            SetFields(weapon,
                ("viewPoint", playerCamera),
                ("weapon", GetOrCreatePistol()));

            SetFields(player.AddComponent<BulletImpacts>(),
                ("weapon", weapon),
                ("markPrefab", GetOrCreateBulletMarkPrefab()));

            AddShotEffects(player, weapon);
            CreateWeaponView(playerCamera, weapon, player.GetComponent<PlayerMovement>());
        }

        // Three shared particle systems under the player. They simulate in world space, so particles stay where they
        // were thrown however the player moves.
        private static void AddShotEffects(GameObject player, WeaponController weapon)
        {
            var effects = new GameObject("Shot Effects").transform;
            effects.SetParent(player.transform, false);

            ParticleSystem dust = CreateBurstParticles(effects, "Dust", GetOrCreateParticleMaterial("Effect_Dust", Color.white),
                0.7f, new Vector2(0.025f, 0.055f), new Color(0.6f, 0.58f, 0.55f), new Color(0.32f, 0.31f, 0.3f), 0.5f);
            ParticleSystem sparks = CreateBurstParticles(effects, "Sparks", GetOrCreateParticleMaterial("Effect_Spark", new Color(4f, 2.5f, 1.1f)),
                0.22f, new Vector2(0.012f, 0.02f), Color.white, new Color(1f, 0.8f, 0.5f), 1f);
            ParticleSystem blood = CreateBurstParticles(effects, "Blood", GetOrCreateParticleMaterial("Effect_Blood", new Color(0.5f, 0.02f, 0.02f)),
                0.6f, new Vector2(0.025f, 0.06f), Color.white, new Color(0.55f, 0.55f, 0.55f), 1.4f);

            SetFields(player.AddComponent<ShotEffects>(),
                ("weapon", weapon),
                ("dust", dust),
                ("sparks", sparks),
                ("blood", blood));
        }

        // A system that emits nothing by itself: ShotEffects emits every particle, with its own position and velocity.
        // Each particle shrinks away over its life.
        private static ParticleSystem CreateBurstParticles(Transform parent, string systemName, Material material, float lifetime,
            Vector2 sizeRange, Color colour, Color otherColour, float gravity)
        {
            var system = new GameObject(systemName).AddComponent<ParticleSystem>();
            system.transform.SetParent(parent, false);

            ParticleSystem.MainModule main = system.main;
            main.duration = 1f;
            main.loop = true;
            main.playOnAwake = true;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.startLifetime = new ParticleSystem.MinMaxCurve(lifetime * 0.6f, lifetime);
            main.startSpeed = 0f;
            main.startSize = new ParticleSystem.MinMaxCurve(sizeRange.x, sizeRange.y);
            main.startColor = new ParticleSystem.MinMaxGradient(colour, otherColour);
            main.gravityModifier = gravity;
            main.maxParticles = 256;

            ParticleSystem.EmissionModule emission = system.emission;
            emission.rateOverTime = 0f;
            ParticleSystem.ShapeModule shape = system.shape;
            shape.enabled = false;
            ParticleSystem.SizeOverLifetimeModule shrink = system.sizeOverLifetime;
            shrink.enabled = true;
            shrink.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 1f, 1f, 0f));

            var particleRenderer = system.GetComponent<ParticleSystemRenderer>();
            particleRenderer.sharedMaterial = material;
            particleRenderer.shadowCastingMode = ShadowCastingMode.Off;
            particleRenderer.receiveShadows = false;
            return system;
        }

        private static WeaponDefinition GetOrCreatePistol()
        {
            var pistol = AssetDatabase.LoadAssetAtPath<WeaponDefinition>(PistolPath);
            if (pistol != null)
                return pistol;

            EnsureFolder("Assets/Weapons");
            pistol = ScriptableObject.CreateInstance<WeaponDefinition>();
            AssetDatabase.CreateAsset(pistol, PistolPath);
            return pistol;
        }

        private static void CreateWeaponView(Transform playerCamera, WeaponController weapon, PlayerMovement movement)
        {
            var view = new GameObject("WeaponView").transform;
            view.SetParent(playerCamera, false);
            view.SetLocalPositionAndRotation(WeaponViewPosition, Quaternion.Euler(WeaponViewAngles));
            view.localScale = Vector3.one * WeaponViewScale;

            Transform model = CreatePistolModel(view);
            GameObject muzzleFlash = CreateMuzzleFlash(model, MuzzlePosition, 0.09f);

            SetFields(view.gameObject.AddComponent<WeaponView>(),
                ("weapon", weapon),
                ("model", model),
                ("muzzleFlash", muzzleFlash));
            SetFields(view.gameObject.AddComponent<WeaponSway>(), ("movement", movement));
        }

        // A service pistol pointing along +Z, held in the player's right hand. The origin is the top of the grip, where the
        // hand holds it, so recoil tips the gun up around the grip.
        private static Transform CreatePistolModel(Transform parent)
        {
            Material steel = GunSteel();
            Material darkSteel = GunDarkSteel();
            Material wood = GunWood();

            var model = new GameObject("Pistol").transform;
            model.SetParent(parent, false);

            CreateModelPart(model, "Slide", PrimitiveType.Cube, new Vector3(0f, 0.03f, 0.07f), new Vector3(0.03f, 0.03f, 0.2f), Quaternion.identity, steel);
            for (int i = 0; i < 4; i++)
            {
                CreateModelPart(model, "Serration", PrimitiveType.Cube, new Vector3(0f, 0.03f, -0.022f + i * 0.008f),
                    new Vector3(0.032f, 0.022f, 0.003f), Quaternion.identity, darkSteel);
            }

            CreateModelPart(model, "Muzzle", PrimitiveType.Cylinder, new Vector3(0f, 0.03f, 0.18f), new Vector3(0.017f, 0.03f, 0.017f), Quaternion.Euler(90f, 0f, 0f), darkSteel);
            CreateModelPart(model, "Frame", PrimitiveType.Cube, new Vector3(0f, 0.008f, 0.055f), new Vector3(0.027f, 0.018f, 0.15f), Quaternion.identity, steel);
            CreateModelPart(model, "Trigger Guard", PrimitiveType.Cube, new Vector3(0f, -0.032f, 0.062f), new Vector3(0.01f, 0.007f, 0.055f), Quaternion.identity, steel);
            CreateModelPart(model, "Trigger Guard Front", PrimitiveType.Cube, new Vector3(0f, -0.015f, 0.088f), new Vector3(0.01f, 0.036f, 0.008f), Quaternion.identity, steel);
            CreateModelPart(model, "Trigger", PrimitiveType.Cube, new Vector3(0f, -0.013f, 0.058f), new Vector3(0.006f, 0.022f, 0.007f), Quaternion.Euler(20f, 0f, 0f), darkSteel);
            CreateModelPart(model, "Grip", PrimitiveType.Cube, new Vector3(0f, -0.052f, -0.006f), new Vector3(0.03f, 0.105f, 0.048f), Quaternion.Euler(15f, 0f, 0f), wood);
            CreateModelPart(model, "Magazine Base", PrimitiveType.Cube, new Vector3(0f, -0.105f, -0.021f), new Vector3(0.028f, 0.008f, 0.046f), Quaternion.Euler(15f, 0f, 0f), darkSteel);
            CreateModelPart(model, "Hammer", PrimitiveType.Cube, new Vector3(0f, 0.05f, -0.035f), new Vector3(0.01f, 0.018f, 0.012f), Quaternion.Euler(-20f, 0f, 0f), darkSteel);
            CreateModelPart(model, "Rear Sight", PrimitiveType.Cube, new Vector3(0f, 0.049f, -0.018f), new Vector3(0.022f, 0.009f, 0.008f), Quaternion.identity, darkSteel);
            CreateModelPart(model, "Front Sight", PrimitiveType.Cube, new Vector3(0f, 0.05f, 0.162f), new Vector3(0.005f, 0.011f, 0.012f), Quaternion.identity, darkSteel);

            CreateViewHand(model);
            return model;
        }

        // The player's right hand closed around the grip, thumb along the frame, with the cuff of a sleeve at the wrist.
        private static void CreateViewHand(Transform pistol)
        {
            Material skin = GetOrCreateMaterial("Hand_Skin", new Color(0.86f, 0.64f, 0.5f), smoothness: 0.2f);
            Material sleeve = GetOrCreateMaterial("Player_Sleeve", new Color(0.3f, 0.33f, 0.24f), GetOrCreateFabricTexture());

            var grip = Quaternion.Euler(15f, 0f, 0f);
            CreateModelPart(pistol, "Fist", PrimitiveType.Cube, new Vector3(0.003f, -0.058f, 0.004f), new Vector3(0.046f, 0.075f, 0.068f), grip, skin);
            for (int i = 0; i < 3; i++)
            {
                // Knuckles of the fingers wrapped around the front of the grip.
                CreateModelPart(pistol, "Knuckle", PrimitiveType.Cube, new Vector3(-0.012f, -0.033f - i * 0.022f, 0.036f - i * 0.006f),
                    new Vector3(0.028f, 0.017f, 0.012f), grip, skin);
            }

            CreateModelPart(pistol, "Thumb", PrimitiveType.Cube, new Vector3(-0.021f, -0.004f, 0.022f), new Vector3(0.014f, 0.016f, 0.056f), Quaternion.Euler(4f, 0f, 0f), skin);
            CreateModelPart(pistol, "Index Finger", PrimitiveType.Cube, new Vector3(-0.012f, -0.018f, 0.052f), new Vector3(0.013f, 0.013f, 0.04f), Quaternion.Euler(10f, 0f, 0f), skin);
            CreateModelPart(pistol, "Cuff", PrimitiveType.Cube, new Vector3(0.008f, -0.11f, -0.05f), new Vector3(0.062f, 0.06f, 0.1f), Quaternion.Euler(35f, 0f, 0f), sleeve);
        }

        // A star-shaped flash facing back along the barrel, and a second one side-on along it, so the flash has body from
        // any angle; plus a short-range light that lights the gun and nearby walls. The flashes are brighter than white,
        // so they bloom. The quads and the light are siblings so the quads' scale does not affect the light.
        private static GameObject CreateMuzzleFlash(Transform gun, Vector3 localPosition, float size)
        {
            Texture2D texture = GetOrCreateTexture("MuzzleFlash", GenerateMuzzleFlashTexture, TextureWrapMode.Clamp);
            Material material = GetOrCreateCutoutMaterial("MuzzleFlash", texture, new Color(3f, 2.6f, 2.2f), doubleSided: true);

            var muzzleFlash = new GameObject("MuzzleFlash");
            muzzleFlash.transform.SetParent(gun, false);
            muzzleFlash.transform.localPosition = localPosition;

            GameObject flash = CreateRenderOnlyPrimitive(PrimitiveType.Quad, "Flash", material);
            flash.transform.SetParent(muzzleFlash.transform, false);
            flash.transform.localScale = Vector3.one * size;

            GameObject sideFlash = CreateRenderOnlyPrimitive(PrimitiveType.Quad, "Side Flash", material);
            sideFlash.transform.SetParent(muzzleFlash.transform, false);
            sideFlash.transform.SetLocalPositionAndRotation(Vector3.forward * (size * 0.4f), Quaternion.Euler(0f, 90f, 0f));
            sideFlash.transform.localScale = new Vector3(size * 1.6f, size * 0.7f, 1f);

            // Ahead of the muzzle, so it lights the room in front rather than bleaching the gun and hand.
            var light = new GameObject("Light").AddComponent<Light>();
            light.transform.SetParent(muzzleFlash.transform, false);
            light.transform.localPosition = Vector3.forward * 0.4f;
            light.type = LightType.Point;
            light.color = new Color(1f, 0.75f, 0.4f);
            light.range = 6f;
            light.intensity = 3f;
            light.shadows = LightShadows.None;

            muzzleFlash.SetActive(false);
            return muzzleFlash;
        }

        private static GameObject GetOrCreateBulletMarkPrefab()
        {
            var existing = AssetDatabase.LoadAssetAtPath<GameObject>(BulletMarkPrefabPath);
            if (existing != null)
                return existing;

            Texture2D texture = GetOrCreateTexture("BulletHole", GenerateBulletHoleTexture, TextureWrapMode.Clamp);
            GameObject mark = CreateRenderOnlyPrimitive(PrimitiveType.Quad, "BulletMark", GetOrCreateCutoutMaterial("BulletHole", texture));
            mark.transform.localScale = Vector3.one * 0.12f;

            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(mark, BulletMarkPrefabPath);
            Object.DestroyImmediate(mark);
            return prefab;
        }

        private static void CreateModelPart(Transform model, string partName, Vector3 position, Vector3 size, float pitch, Material material) =>
            CreateModelPart(model, partName, PrimitiveType.Cube, position, size, Quaternion.Euler(pitch, 0f, 0f), material);

        // The size is the part's extent along each of its local axes, in metres.
        private static GameObject CreateModelPart(Transform model, string partName, PrimitiveType shape, Vector3 position, Vector3 size,
            Quaternion rotation, Material material)
        {
            GameObject part = CreateRenderOnlyPrimitive(shape, partName, material);
            part.transform.SetParent(model, false);
            part.transform.SetLocalPositionAndRotation(position, rotation);

            // Unity's cylinder and capsule are two units tall at unit scale; the other primitives are one unit across.
            bool twoUnitsTall = shape is PrimitiveType.Cylinder or PrimitiveType.Capsule;
            part.transform.localScale = twoUnitsTall ? new Vector3(size.x, size.y * 0.5f, size.z) : size;
            return part;
        }

        // A primitive without its collider, for visuals that must not block shots, interaction or movement.
        private static GameObject CreateRenderOnlyPrimitive(PrimitiveType type, string objectName, Material material)
        {
            GameObject primitive = GameObject.CreatePrimitive(type);
            primitive.name = objectName;
            Object.DestroyImmediate(primitive.GetComponent<Collider>());

            var meshRenderer = primitive.GetComponent<MeshRenderer>();
            meshRenderer.sharedMaterial = material;
            meshRenderer.shadowCastingMode = ShadowCastingMode.Off;
            return primitive;
        }

        // Unlit with alpha clipping: the texture's transparent pixels are cut away and the rest is drawn at full brightness,
        // times a tint that can be brighter than white so it blooms.
        private static Material GetOrCreateCutoutMaterial(string materialName, Texture2D baseMap, Color? tint = null, bool doubleSided = false)
        {
            string path = $"{MaterialFolder}/{materialName}.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material != null)
                return material;

            material = new Material(Shader.Find(UnlitShaderName));
            material.SetTexture("_BaseMap", baseMap);
            material.SetColor("_BaseColor", tint ?? Color.white);
            if (doubleSided)
                material.SetFloat("_Cull", (float)CullMode.Off);
            material.SetFloat("_AlphaClip", 1f);
            material.SetFloat("_Cutoff", 0.5f);
            material.EnableKeyword("_ALPHATEST_ON");
            material.SetOverrideTag("RenderType", "TransparentCutout");
            material.renderQueue = (int)RenderQueue.AlphaTest;

            AssetDatabase.CreateAsset(material, path);
            return material;
        }

        // A small ragged hole: a black centre inside a chipped, dark rim. Everything outside the rim is transparent.
        private static Texture2D GenerateBulletHoleTexture()
        {
            const int size = 16;
            var random = new Random(4);
            var centre = new Vector2(size - 1, size - 1) * 0.5f;

            var pixels = new Color[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float distance = Vector2.Distance(new Vector2(x, y), centre);
                    float rimRadius = 5f + 2f * (float)random.NextDouble();

                    if (distance <= 2.3f)
                        pixels[y * size + x] = new Color(0.02f, 0.02f, 0.02f, 1f);
                    else if (distance <= rimRadius)
                        pixels[y * size + x] = Opaque(Color.white * (0.1f + 0.12f * (float)random.NextDouble()));
                    else
                        pixels[y * size + x] = Color.clear;
                }
            }

            return CreateTexture(pixels, size);
        }

        // A four-pointed flash, white-hot at the centre and orange at the tips. Everything outside the star is transparent.
        private static Texture2D GenerateMuzzleFlashTexture()
        {
            const int size = 32;
            var centre = new Vector2(size - 1, size - 1) * 0.5f;
            var tipColour = new Color(1f, 0.45f, 0.1f);
            var coreColour = new Color(1f, 1f, 0.85f);

            var pixels = new Color[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    Vector2 offset = new Vector2(x, y) - centre;
                    float radius = offset.magnitude / (size * 0.5f);
                    float spike = Mathf.Pow(Mathf.Abs(Mathf.Cos(2f * Mathf.Atan2(offset.y, offset.x))), 4f);
                    float reach = 0.35f + 0.6f * spike;

                    pixels[y * size + x] = radius <= reach
                        ? Opaque(Color.Lerp(tipColour, coreColour, 1f - radius / reach))
                        : Color.clear;
                }
            }

            return CreateTexture(pixels, size);
        }

        // Blued steel, blackened fittings and oiled wood, shared by every gun model. The steel is only a little metallic:
        // with no reflections to show, a fully metallic surface would look black.
        private static Material GunSteel() =>
            GetOrCreateMaterial("Weapon_Steel", new Color(0.3f, 0.31f, 0.35f), smoothness: 0.55f, metallic: 0.3f);

        private static Material GunDarkSteel() =>
            GetOrCreateMaterial("Weapon_DarkSteel", new Color(0.13f, 0.13f, 0.15f), smoothness: 0.4f, metallic: 0.3f);

        private static Material GunWood() =>
            GetOrCreateMaterial("Weapon_Grip", new Color(0.62f, 0.4f, 0.24f), GetOrCreateWoodTexture(), smoothness: 0.3f);

        private static Texture2D GetOrCreateWoodTexture() => GetOrCreateTexture("Wood", GenerateWoodTexture);

        private static Texture2D GetOrCreateFabricTexture() => GetOrCreateTexture("Fabric", GenerateFabricTexture);
    }
}
