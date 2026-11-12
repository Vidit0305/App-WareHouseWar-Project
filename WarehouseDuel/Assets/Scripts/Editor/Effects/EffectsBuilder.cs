using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using WarehouseDuel.Core;

namespace WarehouseDuel.EditorTools.Effects
{
    /// <summary>
    /// Generates the effect textures, materials and pooled particle prefabs:
    /// muzzle flash, environment impact (sparks + dust + bullet hole), player hit puff,
    /// elimination burst and spawn shimmer. All particle counts are small for mobile.
    /// </summary>
    internal static class EffectsBuilder
    {
        public const string PrefabFolder = "Assets/Prefabs/Effects";
        private const string TextureFolder = "Assets/Art/Textures/Effects";
        private const string MaterialFolder = "Assets/Art/Materials/Effects";

        public static GameObject MuzzleFlash => Load("FX_MuzzleFlash");
        public static GameObject ImpactEnvironment => Load("FX_ImpactEnvironment");
        public static GameObject ImpactPlayer => Load("FX_ImpactPlayer");
        public static GameObject BulletHole => Load("FX_BulletHole");
        public static GameObject Elimination => Load("FX_Elimination");
        public static GameObject SpawnShimmer => Load("FX_Spawn");

        public static void BuildAll(SetupReport report)
        {
            FolderStructure.EnsureFolder(PrefabFolder);
            FolderStructure.EnsureFolder(TextureFolder);
            FolderStructure.EnsureFolder(MaterialFolder);

            Texture2D glow = RadialTexture("T_Glow", 64, 2.2f, 0f);
            Texture2D star = StarTexture("T_FlashStar", 64);
            Texture2D smoke = RadialTexture("T_Smoke", 64, 1.4f, 0.35f);
            Texture2D hole = BulletHoleTexture("T_BulletHole", 64);

            Material additiveGlow = MaterialFor("M_FX_Glow", "WarehouseDuel/Additive Unlit", glow, Color.white);
            Material additiveStar = MaterialFor("M_FX_FlashStar", "WarehouseDuel/Additive Unlit", star, Color.white);
            Material smokeMaterial = MaterialFor("M_FX_Smoke", "WarehouseDuel/Alpha Unlit", smoke, Color.white);
            Material holeMaterial = MaterialFor("M_FX_BulletHole", "WarehouseDuel/Alpha Unlit", hole, Color.white);

            Save("FX_MuzzleFlash", BuildMuzzleFlash(additiveStar, additiveGlow), 0.12f);
            Save("FX_ImpactEnvironment", BuildImpact(additiveGlow, smokeMaterial, new Color(1f, 0.75f, 0.35f), new Color(0.55f, 0.52f, 0.48f, 0.6f)), 0.8f);
            Save("FX_ImpactPlayer", BuildImpact(additiveGlow, smokeMaterial, new Color(1f, 0.35f, 0.2f), new Color(0.9f, 0.3f, 0.2f, 0.55f)), 0.6f);
            Save("FX_BulletHole", BuildBulletHole(holeMaterial), 8f);
            Save("FX_Elimination", BuildElimination(additiveGlow, smokeMaterial), 1.6f);
            Save("FX_Spawn", BuildSpawn(additiveGlow), 1.2f);

            report.Pass($"Effects: 6 pooled particle prefabs in {PrefabFolder}.");
        }

        // ------------------------------------------------------------------ prefabs

        private static GameObject BuildMuzzleFlash(Material star, Material glow)
        {
            var root = new GameObject("FX_MuzzleFlash");

            ParticleSystem flash = CreateSystem(root, "Flash", star);
            var main = flash.main;
            main.startLifetime = 0.05f;
            main.startSpeed = 0f;
            main.startSize = new ParticleSystem.MinMaxCurve(0.22f, 0.32f);
            main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            main.startColor = new Color(1f, 0.85f, 0.5f);
            main.simulationSpace = ParticleSystemSimulationSpace.Local;
            Burst(flash, 1);

            ParticleSystem halo = CreateSystem(root, "Halo", glow);
            main = halo.main;
            main.startLifetime = 0.06f;
            main.startSpeed = 0f;
            main.startSize = 0.45f;
            main.startColor = new Color(1f, 0.6f, 0.25f, 0.6f);
            main.simulationSpace = ParticleSystemSimulationSpace.Local;
            Burst(halo, 1);

            ParticleSystem sparks = CreateSystem(root, "Sparks", glow);
            main = sparks.main;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.05f, 0.1f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(4f, 8f);
            main.startSize = 0.03f;
            main.startColor = new Color(1f, 0.8f, 0.4f);
            Burst(sparks, 4);
            var shape = sparks.shape;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = 12f;
            shape.radius = 0.01f;
            Stretch(sparks, 0.08f);
            return root;
        }

        private static GameObject BuildImpact(Material glow, Material smoke, Color sparkColor, Color dustColor)
        {
            var root = new GameObject("FX_Impact");

            ParticleSystem sparks = CreateSystem(root, "Sparks", glow);
            var main = sparks.main;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.15f, 0.3f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(2.5f, 6f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.025f, 0.045f);
            main.startColor = sparkColor;
            main.gravityModifier = 1.2f;
            Burst(sparks, 8);
            var shape = sparks.shape;
            shape.shapeType = ParticleSystemShapeType.Cone; // cone axis = +Z = surface normal
            shape.angle = 35f;
            shape.radius = 0.02f;
            Stretch(sparks, 0.06f);

            ParticleSystem dust = CreateSystem(root, "Dust", smoke);
            main = dust.main;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.4f, 0.7f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.4f, 1.0f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.15f, 0.3f);
            main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            main.startColor = dustColor;
            Burst(dust, 3);
            shape = dust.shape;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = 25f;
            shape.radius = 0.03f;
            FadeAndGrow(dust, 2.2f);
            return root;
        }

        private static GameObject BuildBulletHole(Material hole)
        {
            // A single quad facing +Z (the surface normal); pooled with a long lifetime.
            GameObject quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
            quad.name = "FX_BulletHole";
            Object.DestroyImmediate(quad.GetComponent<Collider>());
            var renderer = quad.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = hole;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            quad.transform.localScale = Vector3.one * 0.12f;

            // Quad faces -Z by default; parent it so the prefab's +Z points away from the wall.
            var root = new GameObject("FX_BulletHole");
            quad.transform.SetParent(root.transform, false);
            quad.transform.localPosition = new Vector3(0f, 0f, 0.01f);
            quad.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
            return root;
        }

        private static GameObject BuildElimination(Material glow, Material smoke)
        {
            var root = new GameObject("FX_Elimination");

            ParticleSystem flash = CreateSystem(root, "Flash", glow);
            var main = flash.main;
            main.startLifetime = 0.25f;
            main.startSpeed = 0f;
            main.startSize = 2.2f;
            main.startColor = new Color(1f, 0.6f, 0.25f, 0.8f);
            Burst(flash, 1);
            FadeAndGrow(flash, 1.4f);

            ParticleSystem sparks = CreateSystem(root, "Sparks", glow);
            main = sparks.main;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.3f, 0.6f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(4f, 9f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.04f, 0.08f);
            main.startColor = new Color(1f, 0.75f, 0.3f);
            main.gravityModifier = 1f;
            Burst(sparks, 24);
            var shape = sparks.shape;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = 0.3f;
            Stretch(sparks, 0.05f);

            ParticleSystem puff = CreateSystem(root, "Smoke", smoke);
            main = puff.main;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.8f, 1.4f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.5f, 1.6f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.6f, 1.1f);
            main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            main.startColor = new Color(0.35f, 0.33f, 0.32f, 0.7f);
            main.gravityModifier = -0.05f;
            Burst(puff, 8);
            shape = puff.shape;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = 0.4f;
            FadeAndGrow(puff, 2f);
            return root;
        }

        private static GameObject BuildSpawn(Material glow)
        {
            var root = new GameObject("FX_Spawn");
            ParticleSystem ring = CreateSystem(root, "Ring", glow);
            var main = ring.main;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.6f, 1.0f);
            main.startSpeed = 0.2f;
            main.startSize = new ParticleSystem.MinMaxCurve(0.08f, 0.16f);
            main.startColor = new Color(0.4f, 0.8f, 1f, 0.9f);
            main.gravityModifier = -0.6f;
            Burst(ring, 30);
            var shape = ring.shape;
            shape.shapeType = ParticleSystemShapeType.Circle;
            shape.radius = 0.55f;
            shape.rotation = new Vector3(90f, 0f, 0f);
            FadeAndGrow(ring, 0.3f);
            return root;
        }

        // ------------------------------------------------------------------ particle helpers

        private static ParticleSystem CreateSystem(GameObject root, string name, Material material)
        {
            var go = new GameObject(name);
            go.transform.SetParent(root.transform, false);
            ParticleSystem ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            var main = ps.main;
            main.duration = 0.2f;
            main.loop = false;
            main.playOnAwake = false;
            main.maxParticles = 40;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.scalingMode = ParticleSystemScalingMode.Hierarchy;

            var emission = ps.emission;
            emission.rateOverTime = 0f;

            var shape = ps.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = 0.01f;

            var renderer = go.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = material;
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            return ps;
        }

        private static void Burst(ParticleSystem ps, int count)
        {
            var emission = ps.emission;
            emission.SetBursts(new[] { new ParticleSystem.Burst(0f, (short)count) });
        }

        private static void Stretch(ParticleSystem ps, float velocityScale)
        {
            var renderer = ps.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = ParticleSystemRenderMode.Stretch;
            renderer.velocityScale = velocityScale;
            renderer.lengthScale = 1f;
            FadeAndGrow(ps, 1f);
        }

        private static void FadeAndGrow(ParticleSystem ps, float endSize)
        {
            var color = ps.colorOverLifetime;
            color.enabled = true;
            var gradient = new Gradient();
            gradient.SetKeys(
                new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0.8f, 0.4f), new GradientAlphaKey(0f, 1f) });
            color.color = gradient;

            var size = ps.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 1f, 1f, endSize));
        }

        // ------------------------------------------------------------------ assets

        private static void Save(string name, GameObject root, float lifetime)
        {
            root.name = name;
            PooledEffect effect = root.AddComponent<PooledEffect>();
            effect.lifetime = lifetime;
            PrefabUtility.SaveAsPrefabAsset(root, $"{PrefabFolder}/{name}.prefab");
            Object.DestroyImmediate(root);
        }

        private static GameObject Load(string name) => AssetDatabase.LoadAssetAtPath<GameObject>($"{PrefabFolder}/{name}.prefab");

        private static Material MaterialFor(string name, string shaderName, Texture2D texture, Color color)
        {
            string path = $"{MaterialFolder}/{name}.mat";
            Shader shader = Shader.Find(shaderName);
            if (shader == null) throw new System.InvalidOperationException($"Shader '{shaderName}' not found (Assets/Art/Shaders).");

            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(shader);
                AssetDatabase.CreateAsset(material, path);
            }
            material.shader = shader;
            material.SetTexture("_BaseMap", texture);
            material.SetColor("_BaseColor", color);
            material.enableInstancing = true;
            EditorUtility.SetDirty(material);
            return material;
        }

        private static Texture2D RadialTexture(string name, int size, float falloff, float noise)
        {
            var random = new System.Random(name.GetHashCode());
            return SaveTexture(name, size, (u, v) =>
            {
                float d = Mathf.Clamp01(Vector2.Distance(new Vector2(u, v), new Vector2(0.5f, 0.5f)) * 2f);
                float a = Mathf.Pow(1f - d, falloff);
                if (noise > 0f) a *= 1f - noise + noise * Mathf.PerlinNoise(u * 6f + 3.1f, v * 6f + 7.7f) * 1.3f;
                a *= 1f - noise * 0.2f * (float)random.NextDouble();
                return new Color(1f, 1f, 1f, Mathf.Clamp01(a));
            });
        }

        private static Texture2D StarTexture(string name, int size)
        {
            return SaveTexture(name, size, (u, v) =>
            {
                Vector2 p = new Vector2(u - 0.5f, v - 0.5f) * 2f;
                float r = p.magnitude;
                float angle = Mathf.Atan2(p.y, p.x);
                float spikes = Mathf.Pow(Mathf.Abs(Mathf.Cos(angle * 3f)), 8f); // 6 spikes
                float core = Mathf.Pow(Mathf.Clamp01(1f - r * 2.2f), 1.5f);
                float rays = spikes * Mathf.Clamp01(1f - r);
                return new Color(1f, 1f, 1f, Mathf.Clamp01(core + rays));
            });
        }

        private static Texture2D BulletHoleTexture(string name, int size)
        {
            return SaveTexture(name, size, (u, v) =>
            {
                float d = Vector2.Distance(new Vector2(u, v), new Vector2(0.5f, 0.5f)) * 2f;
                float hole = d < 0.35f ? 1f : 0f;
                float scorch = Mathf.Clamp01(1f - (d - 0.35f) / 0.6f) * 0.55f;
                float a = Mathf.Max(hole, scorch);
                float shade = d < 0.35f ? 0.05f : 0.18f;
                return new Color(shade, shade, shade, a);
            });
        }

        private static Texture2D SaveTexture(string name, int size, System.Func<float, float, Color> pixel)
        {
            string path = $"{TextureFolder}/{name}.png";
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    texture.SetPixel(x, y, pixel((x + 0.5f) / size, (y + 0.5f) / size));
                }
            }
            texture.Apply();
            File.WriteAllBytes(path, texture.EncodeToPNG());
            Object.DestroyImmediate(texture);
            AssetDatabase.ImportAsset(path);

            if (AssetImporter.GetAtPath(path) is TextureImporter importer)
            {
                importer.alphaIsTransparency = true;
                importer.wrapMode = TextureWrapMode.Clamp;
                importer.mipmapEnabled = false;
                importer.SaveAndReimport();
            }
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }
    }
}
