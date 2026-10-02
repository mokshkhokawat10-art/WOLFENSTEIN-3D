using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Wolfenstein.World;
using Object = UnityEngine.Object;

namespace Wolfenstein.Editor
{
    // The mood of the level: dim ambient light so nothing is ever pitch black, dark haze in the distance, a cool fill light
    // that shades wall faces by direction, warm ceiling lamps hung over the floor, amber lamps at every doorway,
    // and restrained post-processing. No light casts shadows; soft blob shadows and ambient occlusion ground things instead.
    public static partial class TestLevelBuilder
    {
        private const string LampPrefabPath = "Assets/Prefabs/CeilingLamp.prefab";
        private const string PostProcessingProfilePath = "Assets/Settings/TestLevelPostProcessing.asset";
        private const string ParticleShaderName = "Universal Render Pipeline/Particles/Unlit";

        private static readonly Color LampColour = new Color(1f, 0.84f, 0.64f);
        private static readonly Color DoorLampColour = new Color(1f, 0.6f, 0.28f);

        private static void ConfigureEnvironmentLighting()
        {
            RenderSettings.skybox = null;

            // Light from every side: a little more on the floor than on the walls, and least on the ceiling.
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.2f, 0.2f, 0.24f);
            RenderSettings.ambientEquatorColor = new Color(0.17f, 0.17f, 0.21f);
            RenderSettings.ambientGroundColor = new Color(0.11f, 0.11f, 0.13f);

            // Distant surfaces fade into dark haze, which gives long corridors depth.
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogDensity = 0.018f;
            RenderSettings.fogColor = new Color(0.025f, 0.028f, 0.04f);
        }

        // A single shadowless directional light. Each wall face receives a different amount of it, which gives the classic
        // look of walls facing one axis being darker than the other. It is cool, against the warm lamps.
        private static void CreateKeyLight()
        {
            var light = new GameObject("Key Light").AddComponent<Light>();
            light.type = LightType.Directional;
            light.color = new Color(0.72f, 0.8f, 1f);
            light.intensity = 0.35f;
            light.shadows = LightShadows.None;
            light.transform.rotation = Quaternion.Euler(50f, 30f, 0f);
        }

        private static void CreateLevelLighting(WorldManager world)
        {
            SetFields(new GameObject("Lighting").AddComponent<LevelLighting>(),
                ("world", world),
                ("lampPrefab", GetOrCreateLampPrefab()));
        }

        // An enamelled industrial shade hanging from the ceiling on a short rod, with a bright bulb under it and a warm light
        // reaching a couple of tiles. The origin is where it meets the ceiling.
        private static GameObject GetOrCreateLampPrefab()
        {
            var existing = AssetDatabase.LoadAssetAtPath<GameObject>(LampPrefabPath);
            if (existing != null)
                return existing;

            Material metal = GetOrCreateMaterial("Lamp_Metal", new Color(0.1f, 0.1f, 0.11f), smoothness: 0.4f, metallic: 0.5f);
            Material enamel = GetOrCreateMaterial("Lamp_Enamel", new Color(0.16f, 0.27f, 0.19f), smoothness: 0.55f);
            Material glow = GetOrCreateGlowMaterial("Lamp_Glow", new Color(1.3f, 1.15f, 0.95f));
            Material bulb = GetOrCreateGlowMaterial("Lamp_Bulb", LampColour * 6f);

            var lamp = new GameObject("CeilingLamp");
            Transform root = lamp.transform;
            CreateModelPart(root, "Canopy", PrimitiveType.Cylinder, new Vector3(0f, -0.015f, 0f), new Vector3(0.14f, 0.03f, 0.14f), Quaternion.identity, metal);
            CreateModelPart(root, "Rod", PrimitiveType.Cylinder, new Vector3(0f, -0.17f, 0f), new Vector3(0.025f, 0.3f, 0.025f), Quaternion.identity, metal);
            CreateModelPart(root, "Shade Top", PrimitiveType.Cylinder, new Vector3(0f, -0.34f, 0f), new Vector3(0.24f, 0.08f, 0.24f), Quaternion.identity, enamel);
            CreateModelPart(root, "Shade", PrimitiveType.Cylinder, new Vector3(0f, -0.4f, 0f), new Vector3(0.52f, 0.06f, 0.52f), Quaternion.identity, enamel);
            CreateModelPart(root, "Reflector", PrimitiveType.Cylinder, new Vector3(0f, -0.432f, 0f), new Vector3(0.46f, 0.01f, 0.46f), Quaternion.identity, glow);
            CreateModelPart(root, "Bulb", PrimitiveType.Sphere, new Vector3(0f, -0.46f, 0f), new Vector3(0.12f, 0.12f, 0.12f), Quaternion.identity, bulb);
            AddPointLight(root, new Vector3(0f, -0.55f, 0f), LampColour, 9f, 2.6f);

            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(lamp, LampPrefabPath);
            Object.DestroyImmediate(lamp);
            return prefab;
        }

        // A small caged bulb on a ceiling mount, lighting the doorway amber. The position is where it meets the ceiling.
        private static void CreateDoorLamp(Transform door, Vector3 position)
        {
            Material metal = GetOrCreateMaterial("Lamp_Metal", new Color(0.1f, 0.1f, 0.11f), smoothness: 0.4f, metallic: 0.5f);
            Material bulb = GetOrCreateGlowMaterial("Lamp_Bulb_Amber", DoorLampColour * 5f);

            var lamp = new GameObject("Door Lamp").transform;
            lamp.SetParent(door, false);
            lamp.localPosition = position;
            CreateModelPart(lamp, "Mount", PrimitiveType.Cube, new Vector3(0f, -0.02f, 0f), new Vector3(0.2f, 0.04f, 0.14f), Quaternion.identity, metal);
            CreateModelPart(lamp, "Bulb", PrimitiveType.Sphere, new Vector3(0f, -0.09f, 0f), new Vector3(0.09f, 0.1f, 0.09f), Quaternion.identity, bulb);
            foreach (float angle in new[] { 0f, 90f })
            {
                CreateModelPart(lamp, "Cage", PrimitiveType.Cube, new Vector3(0f, -0.09f, 0f), new Vector3(0.12f, 0.12f, 0.012f),
                    Quaternion.Euler(0f, angle, 0f), metal);
            }

            AddPointLight(lamp, new Vector3(0f, -0.2f, 0f), DoorLampColour, 4.5f, 1.6f);
        }

        // The door lamp's shape with a white bulb and light, both coloured at runtime by the door's lock lights.
        private static void CreateSignalLamp(Transform door, Vector3 position, List<Renderer> bulbs, List<Light> lights)
        {
            Material metal = GetOrCreateMaterial("Lamp_Metal", new Color(0.1f, 0.1f, 0.11f), smoothness: 0.4f, metallic: 0.5f);
            Material bulb = GetOrCreateGlowMaterial("Lamp_Signal", Color.white * 3f);

            var lamp = new GameObject("Signal Lamp").transform;
            lamp.SetParent(door, false);
            lamp.localPosition = position;
            CreateModelPart(lamp, "Mount", PrimitiveType.Cube, new Vector3(0f, -0.02f, 0f), new Vector3(0.24f, 0.04f, 0.16f), Quaternion.identity, metal);
            CreateModelPart(lamp, "Bulb", PrimitiveType.Sphere, new Vector3(0f, -0.1f, 0f), new Vector3(0.13f, 0.13f, 0.13f), Quaternion.identity, bulb);
            bulbs.Add(lamp.Find("Bulb").GetComponent<Renderer>());
            lights.Add(AddPointLight(lamp, new Vector3(0f, -0.25f, 0f), Color.white, 4.5f, 1.8f));
        }

        private static Light AddPointLight(Transform parent, Vector3 position, Color colour, float range, float intensity)
        {
            var light = new GameObject("Light").AddComponent<Light>();
            light.transform.SetParent(parent, false);
            light.transform.localPosition = position;
            light.type = LightType.Point;
            light.color = colour;
            light.range = range;
            light.intensity = intensity;
            light.shadows = LightShadows.None;
            return light;
        }

        // Unlit, so it shows the same colour whatever lights it. Colours brighter than white bloom.
        private static Material GetOrCreateGlowMaterial(string materialName, Color colour)
        {
            string path = $"{MaterialFolder}/{materialName}.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material != null)
                return material;

            material = new Material(Shader.Find(UnlitShaderName));
            material.SetColor("_BaseColor", colour);
            AssetDatabase.CreateAsset(material, path);
            return material;
        }

        // A soft dark patch on the floor under something, so it looks grounded without a real shadow.
        // It lies just above the floor and has no collider.
        private static void CreateBlobShadow(Transform parent, float diameter)
        {
            GameObject blob = CreateRenderOnlyPrimitive(PrimitiveType.Quad, "Blob Shadow", GetOrCreateBlobShadowMaterial());
            blob.transform.SetParent(parent, false);
            blob.transform.SetLocalPositionAndRotation(Vector3.up * 0.012f, Quaternion.Euler(90f, 0f, 0f));
            blob.transform.localScale = new Vector3(diameter, diameter, 1f);
        }

        // Unlit and alpha blended, drawn with the transparent objects so it darkens whatever floor is beneath it.
        private static Material GetOrCreateBlobShadowMaterial()
        {
            const string materialName = "BlobShadow";
            string path = $"{MaterialFolder}/{materialName}.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material != null)
                return material;

            Texture2D texture = GetOrCreateTexture(materialName, GenerateBlobShadowTexture, TextureWrapMode.Clamp, FilterMode.Bilinear);
            material = new Material(Shader.Find(UnlitShaderName));
            material.SetTexture("_BaseMap", texture);
            material.SetColor("_BaseColor", new Color(0f, 0f, 0f, 0.7f));
            material.SetFloat("_Surface", 1f);
            material.SetFloat("_Blend", 0f);
            material.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            material.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
            material.SetFloat("_SrcBlendAlpha", (float)BlendMode.One);
            material.SetFloat("_DstBlendAlpha", (float)BlendMode.OneMinusSrcAlpha);
            material.SetFloat("_ZWrite", 0f);
            material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            material.SetOverrideTag("RenderType", "Transparent");
            material.renderQueue = (int)RenderQueue.Transparent;

            AssetDatabase.CreateAsset(material, path);
            return material;
        }

        // Opaque square particles, retro rather than soft; each shrinks away instead of fading.
        private static Material GetOrCreateParticleMaterial(string materialName, Color colour)
        {
            string path = $"{MaterialFolder}/{materialName}.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material != null)
                return material;

            material = new Material(Shader.Find(ParticleShaderName));
            material.SetColor("_BaseColor", colour);
            AssetDatabase.CreateAsset(material, path);
            return material;
        }

        // Filmic tone mapping that keeps colours true, a little extra contrast with slightly muted colour, bloom that only
        // catches lights and flashes brighter than white, and a gentle vignette.
        private static void CreatePostProcessing()
        {
            var volume = new GameObject("Post Processing").AddComponent<Volume>();
            volume.isGlobal = true;
            volume.sharedProfile = GetOrCreatePostProcessingProfile();
        }

        private static VolumeProfile GetOrCreatePostProcessingProfile()
        {
            var profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(PostProcessingProfilePath);
            if (profile != null)
                return profile;

            profile = ScriptableObject.CreateInstance<VolumeProfile>();
            AssetDatabase.CreateAsset(profile, PostProcessingProfilePath);

            AddOverride<Tonemapping>(profile).mode.Override(TonemappingMode.Neutral);

            var colour = AddOverride<ColorAdjustments>(profile);
            colour.postExposure.Override(0.3f);
            colour.contrast.Override(12f);
            colour.saturation.Override(-10f);

            var bloom = AddOverride<Bloom>(profile);
            bloom.threshold.Override(1f);
            bloom.intensity.Override(0.6f);
            bloom.scatter.Override(0.6f);

            var vignette = AddOverride<Vignette>(profile);
            vignette.intensity.Override(0.3f);
            vignette.smoothness.Override(0.45f);

            return profile;
        }

        private static T AddOverride<T>(VolumeProfile profile) where T : VolumeComponent
        {
            var component = profile.Add<T>(overrides: true);
            component.name = typeof(T).Name;
            AssetDatabase.AddObjectToAsset(component, profile);
            return component;
        }
    }
}
