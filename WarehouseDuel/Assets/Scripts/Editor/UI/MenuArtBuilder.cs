using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using WarehouseWar.Core;
using WarehouseWar.EditorTools.Gameplay;
using WarehouseWar.Profile;

namespace WarehouseWar.EditorTools.UI
{
    /// <summary>
    /// Real rendered art for the menus instead of flat pixel patterns: a picture of every map taken in the finished
    /// (lit, baked) map scenes, plus a soldier cut-out with a transparent background for the Wardrobe card.
    /// Runs after the maps and HUD are built and before the menu scenes.
    /// </summary>
    internal static class MenuArtBuilder
    {
        public const string Folder = "Assets/Art/UI/Menu";
        private const int Width = 1280, Height = 720;

        /// <summary>Index 0–3: Warehouse, Desert, Forest, Snow (same order as the map picker). 4: second warehouse view.</summary>
        private static readonly (string scene, Vector3 from, Vector3 to, float fov)[] Shots =
        {
            (GameConstants.Scenes.WarehouseMap, new Vector3(-15f, 7.5f, -25.5f), new Vector3(0f, 0.5f, 0f), 60f),
            (GameConstants.Scenes.DesertMap, new Vector3(-24f, 10f, -24f), new Vector3(2f, 1f, 2f), 58f),
            (GameConstants.Scenes.ForestMap, new Vector3(23f, 10f, -24f), new Vector3(-2f, 1f, 2f), 58f),
            (GameConstants.Scenes.SnowMap, new Vector3(24f, 10f, 24f), new Vector3(-2f, 1f, -2f), 58f),
            (GameConstants.Scenes.WarehouseMap, new Vector3(16.5f, 4.8f, -18f), new Vector3(0f, 1f, 0f), 66f),
        };

        public const int ShotCount = 5;
        public const int CutoutCharacter = 4;

        public static Sprite MapShot(int index) => AssetDatabase.LoadAssetAtPath<Sprite>($"{Folder}/Map_{index}.png");
        public static Sprite SoldierCutout => AssetDatabase.LoadAssetAtPath<Sprite>($"{Folder}/Soldier_Cutout.png");

        public static void Build(SetupReport report)
        {
            FolderStructure.EnsureFolder(Folder);
            int made = 0;
            for (int i = 0; i < Shots.Length; i++)
            {
                string scenePath = GameConstants.Scenes.PathOf(Shots[i].scene);
                if (AssetDatabase.LoadAssetAtPath<SceneAsset>(scenePath) == null) continue;
                EditorSceneManager.OpenScene(scenePath);
                string path = $"{Folder}/Map_{i}.png";
                RenderMap(Shots[i].from, Shots[i].to, Shots[i].fov, path); // first render warms up lighting/probes
                RenderMap(Shots[i].from, Shots[i].to, Shots[i].fov, path);
                Import(path);
                made++;
            }

            var characters = AssetDatabase.LoadAssetAtPath<CharacterCatalog>(CharacterBuilder.CatalogPath);
            if (characters != null)
            {
                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                string path = $"{Folder}/Soldier_Cutout.png";
                RenderCutout(CutoutCharacter, characters, path);
                Import(path);
            }

            if (made < Shots.Length || SoldierCutout == null) report.Warn($"Menu art: {made}/{Shots.Length} map pictures, cut-out {(SoldierCutout != null ? "ok" : "missing")}.");
            else report.Pass($"Menu art: {made} rendered map pictures and a soldier cut-out.");
        }

        private static void RenderMap(Vector3 from, Vector3 to, float fov, string path)
        {
            var go = new GameObject("Menu Art Camera");
            go.transform.position = from;
            go.transform.LookAt(to);
            Camera cam = go.AddComponent<Camera>();
            cam.fieldOfView = fov;
            cam.nearClipPlane = 0.1f;
            cam.farClipPlane = 400f;
            cam.clearFlags = RenderSettings.skybox != null ? CameraClearFlags.Skybox : CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.1f, 0.12f, 0.16f);
            WarehouseWar.EditorTools.Map.AtmosphereKit.EnableCameraEffects(cam);

            var rt = new RenderTexture(Width, Height, 24, RenderTextureFormat.ARGB32) { antiAliasing = 4 };
            cam.targetTexture = rt;
            cam.Render();
            RenderTexture.active = rt;
            var texture = new Texture2D(Width, Height, TextureFormat.RGB24, false);
            texture.ReadPixels(new Rect(0, 0, Width, Height), 0, 0);
            texture.Apply();
            RenderTexture.active = null;
            cam.targetTexture = null;
            File.WriteAllBytes(path, texture.EncodeToPNG());
            Object.DestroyImmediate(texture);
            rt.Release();
            Object.DestroyImmediate(rt);
            Object.DestroyImmediate(go);
        }

        /// <summary>
        /// Full-body soldier with a transparent background: rendered once on black and once on white; the difference
        /// between the two gives the exact coverage (alpha) of every pixel, including soft anti-aliased edges.
        /// </summary>
        private static void RenderCutout(int index, CharacterCatalog catalog, string path)
        {
            const int size = 512;
            var root = new GameObject("Cutout Model");
            root.transform.position = new Vector3(0f, -600f, 0f);
            root.transform.rotation = Quaternion.Euler(0f, 155f, 0f);
            SoldierModelBuilder.Rig rig = SoldierModelBuilder.Build(root.transform, "Soldier");
            rig.Look.Apply(index, catalog);

            var lights = new List<GameObject>();
            void AddLight(Quaternion rotation, float intensity, Color color)
            {
                var go = new GameObject("Cutout Light");
                go.transform.rotation = rotation;
                Light light = go.AddComponent<Light>();
                light.type = LightType.Directional;
                light.intensity = intensity;
                light.color = color;
                light.shadows = LightShadows.None;
                lights.Add(go);
            }
            AddLight(Quaternion.Euler(24f, 22f, 0f), 1.25f, new Color(1f, 0.93f, 0.84f));
            AddLight(Quaternion.Euler(12f, 196f, 0f), 1.3f, new Color(0.75f, 0.6f, 1f));
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.55f, 0.58f, 0.68f);
            RenderSettings.ambientEquatorColor = new Color(0.42f, 0.4f, 0.4f);
            RenderSettings.ambientGroundColor = new Color(0.22f, 0.2f, 0.18f);
            RenderSettings.fog = false;

            Vector3 focus = root.transform.position + new Vector3(0f, 0.98f, 0f);
            var camObject = new GameObject("Cutout Camera");
            camObject.transform.position = focus + new Vector3(0.2f, 0.15f, -4.3f);
            camObject.transform.LookAt(focus);
            Camera cam = camObject.AddComponent<Camera>();
            cam.fieldOfView = 30f;
            cam.nearClipPlane = 0.1f;
            cam.farClipPlane = 20f;
            cam.clearFlags = CameraClearFlags.SolidColor;

            Color[] RenderOn(Color background)
            {
                cam.backgroundColor = background;
                var rt = new RenderTexture(size, size, 24, RenderTextureFormat.ARGB32) { antiAliasing = 8 };
                cam.targetTexture = rt;
                cam.Render();
                cam.Render();
                RenderTexture.active = rt;
                var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
                tex.ReadPixels(new Rect(0, 0, size, size), 0, 0);
                RenderTexture.active = null;
                cam.targetTexture = null;
                Color[] pixels = tex.GetPixels();
                Object.DestroyImmediate(tex);
                rt.Release();
                Object.DestroyImmediate(rt);
                return pixels;
            }

            Color[] onBlack = RenderOn(Color.black);
            Color[] onWhite = RenderOn(Color.white);
            var result = new Color[onBlack.Length];
            for (int i = 0; i < result.Length; i++)
            {
                Color b = onBlack[i], w = onWhite[i];
                float coverage = 1f - Mathf.Clamp01(((w.r - b.r) + (w.g - b.g) + (w.b - b.b)) / 3f);
                if (coverage < 0.02f) { result[i] = new Color(0f, 0f, 0f, 0f); continue; }
                result[i] = new Color(Mathf.Clamp01(b.r / coverage), Mathf.Clamp01(b.g / coverage), Mathf.Clamp01(b.b / coverage), coverage);
            }
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            texture.SetPixels(result);
            texture.Apply();
            File.WriteAllBytes(path, texture.EncodeToPNG());
            Object.DestroyImmediate(texture);

            Object.DestroyImmediate(camObject);
            foreach (GameObject go in lights) Object.DestroyImmediate(go);
            Object.DestroyImmediate(root);
        }

        private static void Import(string path)
        {
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
            if (!(AssetImporter.GetAtPath(path) is TextureImporter importer)) return;
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.filterMode = FilterMode.Bilinear;
            importer.maxTextureSize = 2048;
            importer.SaveAndReimport();
        }
    }
}
