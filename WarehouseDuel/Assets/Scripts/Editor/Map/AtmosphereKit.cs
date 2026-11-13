using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace WarehouseDuel.EditorTools.Map
{
    /// <summary>
    /// Cinematic atmosphere shared by the 3D scenes: post-processing looks (ACES tonemapping, bloom, warm/teal
    /// split toning, vignette), additive light shafts, rising embers and drifting smoke. All cheap enough for
    /// mid-range phones; post-processing can be switched off in Settings (see CameraEffects).
    /// </summary>
    internal static class AtmosphereKit
    {
        public const string ProfileFolder = "Assets/Settings/PostFX";
        private const string TextureFolder = "Assets/Art/Textures/Effects";
        private const string MaterialFolder = "Assets/Art/Materials/Effects";

        public enum Look { Gameplay, Showroom }

        // ------------------------------------------------------------------ post-processing

        /// <summary>Creates (or recreates) the look's profile and a global Volume using it in the open scene.</summary>
        public static Volume AddGlobalVolume(Look look)
        {
            VolumeProfile profile = CreateProfile(look);
            var go = new GameObject($"Post Processing ({look})");
            Volume volume = go.AddComponent<Volume>();
            volume.isGlobal = true;
            volume.priority = 0f;
            volume.sharedProfile = profile;
            return volume;
        }

        private static VolumeProfile CreateProfile(Look look)
        {
            FolderStructure.EnsureFolder(ProfileFolder);
            string path = $"{ProfileFolder}/PFX_{look}.asset";
            if (AssetDatabase.LoadMainAssetAtPath(path) != null) AssetDatabase.DeleteAsset(path);
            var profile = ScriptableObject.CreateInstance<VolumeProfile>();
            AssetDatabase.CreateAsset(profile, path);

            bool showroom = look == Look.Showroom;
            Tonemapping tone = profile.Add<Tonemapping>(true);
            tone.mode.Override(TonemappingMode.ACES);

            Bloom bloom = profile.Add<Bloom>(true);
            bloom.threshold.Override(showroom ? 0.85f : 1.0f);
            bloom.intensity.Override(showroom ? 1.1f : 0.6f);
            bloom.scatter.Override(0.7f);
            bloom.tint.Override(new Color(1f, 0.86f, 0.7f));
            bloom.highQualityFiltering.Override(false);

            ColorAdjustments colour = profile.Add<ColorAdjustments>(true);
            colour.postExposure.Override(showroom ? 0.85f : 0.45f);   // ACES darkens; lift it back
            colour.contrast.Override(showroom ? 22f : 16f);
            colour.saturation.Override(showroom ? 12f : 8f);

            WhiteBalance white = profile.Add<WhiteBalance>(true);
            white.temperature.Override(showroom ? 10f : 6f);

            SplitToning split = profile.Add<SplitToning>(true);
            split.shadows.Override(new Color(0.32f, 0.46f, 0.58f));   // teal shadows
            split.highlights.Override(new Color(1f, 0.72f, 0.45f));   // warm highlights
            split.balance.Override(0f);

            Vignette vignette = profile.Add<Vignette>(true);
            vignette.intensity.Override(showroom ? 0.3f : 0.24f);
            vignette.smoothness.Override(0.45f);
            vignette.color.Override(new Color(0.06f, 0.03f, 0.02f));

            foreach (VolumeComponent component in profile.components) AssetDatabase.AddObjectToAsset(component, profile);
            EditorUtility.SetDirty(profile);
            AssetDatabase.SaveAssets();
            return profile;
        }

        /// <summary>Turns on post-processing + FXAA for a camera and adds the runtime on/off switch.</summary>
        public static void EnableCameraEffects(Camera camera)
        {
            var data = camera.GetComponent<UniversalAdditionalCameraData>();
            if (data == null) data = camera.gameObject.AddComponent<UniversalAdditionalCameraData>();
            data.renderPostProcessing = true;
            data.antialiasing = AntialiasingMode.FastApproximateAntialiasing;
            if (camera.GetComponent<WarehouseDuel.Core.CameraEffects>() == null) camera.gameObject.AddComponent<WarehouseDuel.Core.CameraEffects>();
        }

        // ------------------------------------------------------------------ light shafts

        /// <summary>
        /// A soft additive sheet of light: top edge at <paramref name="top"/>, falling along <paramref name="direction"/>
        /// for <paramref name="length"/> metres, <paramref name="width"/> wide (along <paramref name="widthAxis"/>).
        /// </summary>
        public static GameObject LightShaft(Transform parent, string name, Vector3 top, Vector3 direction, Vector3 widthAxis,
            float length, float width, Color color)
        {
            GameObject quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
            Object.DestroyImmediate(quad.GetComponent<Collider>());
            quad.name = name;
            quad.transform.SetParent(parent, false);
            direction.Normalize();
            widthAxis = Vector3.ProjectOnPlane(widthAxis, direction).normalized;
            Vector3 normal = Vector3.Cross(widthAxis, direction).normalized;
            // Quad: X = width, Y = length; its +Y should point back up the beam (towards the light source).
            quad.transform.rotation = Quaternion.LookRotation(normal, -direction);
            quad.transform.position = top + direction * (length * 0.5f);
            quad.transform.localScale = new Vector3(width, length, 1f);
            var renderer = quad.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = ShaftMaterial(color);
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            // Double-sided look: a second quad facing the other way.
            GameObject back = Object.Instantiate(quad, quad.transform.parent);
            back.name = name + " (back)";
            back.transform.rotation = Quaternion.LookRotation(-normal, -direction);
            return quad;
        }

        private static Material ShaftMaterial(Color color)
        {
            string key = ColorUtility.ToHtmlStringRGB(color);
            string path = $"{MaterialFolder}/M_FX_LightShaft_{key}.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                FolderStructure.EnsureFolder(MaterialFolder);
                material = new Material(Shader.Find("WarehouseDuel/Additive Unlit"));
                AssetDatabase.CreateAsset(material, path);
            }
            material.SetTexture("_BaseMap", ShaftTexture());
            material.SetColor("_BaseColor", color);
            EditorUtility.SetDirty(material);
            return material;
        }

        private static Texture2D ShaftTexture()
        {
            string path = $"{TextureFolder}/T_LightShaft.png";
            var existing = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            if (existing != null) return existing;
            const int w = 64, h = 256;
            var texture = new Texture2D(w, h, TextureFormat.RGB24, false);
            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    float u = x / (w - 1f), v = y / (h - 1f);
                    float across = Mathf.Sin(u * Mathf.PI);
                    across *= across;
                    float rays = 0.75f + 0.25f * Mathf.PerlinNoise(u * 9f, 0.3f);
                    float along = Mathf.SmoothStep(0f, 1f, v) * Mathf.Lerp(0.35f, 1f, v);  // bright at the top (v = 1)
                    float g = across * rays * along;
                    texture.SetPixel(x, y, new Color(g, g, g));
                }
            }
            texture.Apply();
            FolderStructure.EnsureFolder(TextureFolder);
            File.WriteAllBytes(path, texture.EncodeToPNG());
            Object.DestroyImmediate(texture);
            AssetDatabase.ImportAsset(path);
            if (AssetImporter.GetAtPath(path) is TextureImporter importer)
            {
                importer.wrapMode = TextureWrapMode.Clamp;
                importer.mipmapEnabled = false;
                importer.textureCompression = TextureImporterCompression.Compressed;
                importer.SaveAndReimport();
            }
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        // ------------------------------------------------------------------ particles

        /// <summary>Glowing embers drifting up through a box-shaped area.</summary>
        public static ParticleSystem Embers(Vector3 centre, Vector3 area, float rate)
        {
            var go = new GameObject("Embers");
            go.transform.position = centre;
            go.transform.rotation = Quaternion.Euler(-90f, 0f, 0f);
            var ps = go.AddComponent<ParticleSystem>();
            ParticleSystem.MainModule main = ps.main;
            main.loop = true;
            main.duration = 8f;
            main.prewarm = true;
            main.startLifetime = new ParticleSystem.MinMaxCurve(3.5f, 6.5f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.35f, 0.9f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.025f, 0.06f);
            main.startColor = new ParticleSystem.MinMaxGradient(new Color(1f, 0.55f, 0.15f), new Color(1f, 0.8f, 0.35f));
            main.maxParticles = 80;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            ParticleSystem.EmissionModule emission = ps.emission;
            emission.rateOverTime = rate;
            ParticleSystem.ShapeModule shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = new Vector3(area.x, area.z, area.y);
            ParticleSystem.NoiseModule noise = ps.noise;
            noise.enabled = true;
            noise.strength = 0.35f;
            noise.frequency = 0.6f;
            noise.scrollSpeed = 0.3f;
            ParticleSystem.ColorOverLifetimeModule colour = ps.colorOverLifetime;
            colour.enabled = true;
            var gradient = new Gradient();
            gradient.SetKeys(
                new[] { new GradientColorKey(new Color(1f, 0.85f, 0.4f), 0f), new GradientColorKey(new Color(1f, 0.35f, 0.08f), 1f) },
                new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.15f), new GradientAlphaKey(0.8f, 0.7f), new GradientAlphaKey(0f, 1f) });
            colour.color = gradient;
            var renderer = go.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = AssetDatabase.LoadAssetAtPath<Material>($"{MaterialFolder}/M_FX_Glow.mat");
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            return ps;
        }

        /// <summary>Large, slow, faint smoke puffs for depth haze.</summary>
        public static ParticleSystem Smoke(Vector3 centre, Vector3 area, Color tint, float rate)
        {
            var go = new GameObject("Smoke Haze");
            go.transform.position = centre;
            var ps = go.AddComponent<ParticleSystem>();
            ParticleSystem.MainModule main = ps.main;
            main.loop = true;
            main.duration = 12f;
            main.prewarm = true;
            main.startLifetime = new ParticleSystem.MinMaxCurve(10f, 16f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.05f, 0.2f);
            main.startSize = new ParticleSystem.MinMaxCurve(3f, 6f);
            main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            main.startColor = tint;
            main.maxParticles = 24;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            ParticleSystem.EmissionModule emission = ps.emission;
            emission.rateOverTime = rate;
            ParticleSystem.ShapeModule shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = area;
            ParticleSystem.RotationOverLifetimeModule spin = ps.rotationOverLifetime;
            spin.enabled = true;
            spin.z = new ParticleSystem.MinMaxCurve(-0.1f, 0.1f);
            ParticleSystem.ColorOverLifetimeModule colour = ps.colorOverLifetime;
            colour.enabled = true;
            var gradient = new Gradient();
            gradient.SetKeys(
                new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.3f), new GradientAlphaKey(1f, 0.7f), new GradientAlphaKey(0f, 1f) });
            colour.color = gradient;
            var renderer = go.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = AssetDatabase.LoadAssetAtPath<Material>($"{MaterialFolder}/M_FX_Smoke.mat");
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.sortingFudge = 10f;
            return ps;
        }
    }
}
