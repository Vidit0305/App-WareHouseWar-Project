using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using WarehouseDuel.EditorTools.Gameplay;
using WarehouseDuel.Profile;

namespace WarehouseDuel.EditorTools.UI
{
    /// <summary>
    /// Generates the profile art: preset avatars (portraits of the voxel soldier rendered in different camo,
    /// skin tones, team colours and gear) and procedural banners, then stores them in Resources/ProfileCatalog.
    /// </summary>
    internal static class ProfileArtBuilder
    {
        public const string AvatarFolder = "Assets/UI/Avatars";
        public const string BannerFolder = "Assets/UI/Banners";
        public const string CatalogPath = "Assets/Resources/ProfileCatalog.asset";

        private const int AvatarSize = 256;
        private const int BannerWidth = 640, BannerHeight = 160;

        private struct AvatarStyle
        {
            public Color team, camo, skin, goggles, background;
            public bool mask;
            public float yaw;
        }

        private static readonly Color Woodland = Color.white;
        private static readonly Color Desert = new Color(1f, 0.82f, 0.58f);
        private static readonly Color Urban = new Color(0.72f, 0.76f, 0.8f);
        private static readonly Color Navy = new Color(0.6f, 0.7f, 0.95f);
        private static readonly Color Night = new Color(0.5f, 0.52f, 0.55f);
        private static readonly Color SkinLight = new Color32(236, 182, 140, 255);
        private static readonly Color SkinMedium = new Color32(198, 134, 90, 255);
        private static readonly Color SkinDark = new Color32(132, 82, 52, 255);
        private static readonly Color LensBlue = new Color32(60, 170, 255, 255);
        private static readonly Color LensAmber = new Color32(255, 170, 40, 255);
        private static readonly Color LensGreen = new Color32(80, 230, 120, 255);
        private static readonly Color LensRed = new Color32(255, 70, 60, 255);

        private static readonly AvatarStyle[] Avatars =
        {
            new AvatarStyle { team = new Color(0.2f, 0.55f, 1f), camo = Woodland, skin = SkinLight, goggles = LensBlue, mask = true, yaw = -14f, background = new Color32(28, 70, 140, 255) },
            new AvatarStyle { team = new Color(1f, 0.3f, 0.25f), camo = Desert, skin = SkinMedium, goggles = LensAmber, mask = false, yaw = 12f, background = new Color32(140, 40, 34, 255) },
            new AvatarStyle { team = new Color(0.3f, 0.85f, 0.4f), camo = Woodland, skin = SkinDark, goggles = LensGreen, mask = true, yaw = -8f, background = new Color32(30, 100, 52, 255) },
            new AvatarStyle { team = new Color(1f, 0.6f, 0.15f), camo = Urban, skin = SkinLight, goggles = LensAmber, mask = false, yaw = 16f, background = new Color32(150, 80, 20, 255) },
            new AvatarStyle { team = new Color(0.65f, 0.4f, 1f), camo = Navy, skin = SkinMedium, goggles = LensBlue, mask = true, yaw = -16f, background = new Color32(72, 40, 130, 255) },
            new AvatarStyle { team = new Color(0.2f, 0.9f, 0.95f), camo = Urban, skin = SkinDark, goggles = LensBlue, mask = false, yaw = 10f, background = new Color32(20, 100, 112, 255) },
            new AvatarStyle { team = new Color(1f, 0.85f, 0.2f), camo = Desert, skin = SkinLight, goggles = LensRed, mask = true, yaw = -10f, background = new Color32(140, 110, 20, 255) },
            new AvatarStyle { team = new Color(1f, 0.45f, 0.75f), camo = Woodland, skin = SkinMedium, goggles = LensAmber, mask = false, yaw = 14f, background = new Color32(130, 40, 90, 255) },
            new AvatarStyle { team = new Color(0.95f, 0.95f, 0.95f), camo = Night, skin = SkinDark, goggles = LensRed, mask = true, yaw = -12f, background = new Color32(60, 66, 78, 255) },
            new AvatarStyle { team = new Color(0.15f, 0.15f, 0.18f), camo = Navy, skin = SkinLight, goggles = LensGreen, mask = true, yaw = 8f, background = new Color32(22, 26, 36, 255) },
        };

        public static readonly string[] BannerNames = { "Steel", "Hazard", "Woodland", "Desert", "Sunset", "Arctic", "Inferno", "Neon" };

        public static void Build(SetupReport report)
        {
            FolderStructure.EnsureFolder(AvatarFolder);
            FolderStructure.EnsureFolder(BannerFolder);
            FolderStructure.EnsureFolder("Assets/Resources");

            var avatars = new List<Sprite>();
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            for (int i = 0; i < Avatars.Length; i++)
            {
                string path = $"{AvatarFolder}/Avatar_{i:00}.png";
                RenderAvatar(Avatars[i], path);
                avatars.Add(ImportSprite(path));
            }

            var banners = new List<Sprite>();
            for (int i = 0; i < BannerNames.Length; i++)
            {
                string path = $"{BannerFolder}/Banner_{i:00}_{BannerNames[i]}.png";
                SaveTexture(path, BannerWidth, BannerHeight, (x, y) => BannerPixel(i, x, y));
                banners.Add(ImportSprite(path));
            }

            var catalog = AssetDatabase.LoadAssetAtPath<ProfileCatalog>(CatalogPath);
            if (catalog == null)
            {
                catalog = ScriptableObject.CreateInstance<ProfileCatalog>();
                AssetDatabase.CreateAsset(catalog, CatalogPath);
            }
            catalog.avatars = avatars.ToArray();
            catalog.banners = banners.ToArray();
            catalog.bannerNames = (string[])BannerNames.Clone();
            EditorUtility.SetDirty(catalog);
            AssetDatabase.SaveAssets();

            int missing = avatars.FindAll(s => s == null).Count + banners.FindAll(s => s == null).Count;
            if (missing > 0) report.Error($"Profile art: {missing} sprite(s) failed to import.");
            else report.Pass($"Profile art: {avatars.Count} avatars and {banners.Count} banners generated.");
        }

        // ------------------------------------------------------------------ avatars

        private static void RenderAvatar(AvatarStyle style, string path)
        {
            var root = new GameObject("Avatar Model");
            root.transform.position = new Vector3(0f, -500f, 0f);
            root.transform.rotation = Quaternion.Euler(0f, 180f + style.yaw, 0f);
            SoldierModelBuilder.Rig rig = SoldierModelBuilder.Build(root.transform, "Soldier");

            // Per-avatar material copies (the shared soldier materials are left untouched).
            var copies = new Dictionary<Material, Material>();
            Material Copy(Material source, Color? color)
            {
                if (copies.TryGetValue(source, out Material existing)) return existing;
                var copy = new Material(source);
                if (color.HasValue)
                {
                    copy.SetColor("_BaseColor", color.Value);
                    if (copy.IsKeywordEnabled("_EMISSION")) copy.SetColor("_EmissionColor", color.Value * 1.4f);
                }
                copies[source] = copy;
                return copy;
            }

            foreach (Renderer r in root.GetComponentsInChildren<Renderer>())
            {
                Material m = r.sharedMaterial;
                string n = m.name;
                bool lowerFaceMask = n == "M_Soldier_Mask" && r.transform.parent == rig.Head && r.transform.localScale.y > 0.15f;
                if (n == "M_Soldier_Camo") r.sharedMaterial = Copy(m, style.camo);
                else if (n == "M_Soldier_CamoDark") r.sharedMaterial = Copy(m, style.camo * 0.75f);
                else if (n == "M_Soldier_Skin") r.sharedMaterial = Copy(m, style.skin);
                else if (n == "M_Soldier_Team") r.sharedMaterial = Copy(m, style.team);
                else if (n == "M_Soldier_Goggles") r.sharedMaterial = Copy(m, style.goggles);
                if (lowerFaceMask && !style.mask) r.sharedMaterial = Copy(rig.Head.GetComponentInChildren<Renderer>().sharedMaterial, style.skin);
            }

            var lightObject = new GameObject("Avatar Key Light");
            lightObject.transform.rotation = Quaternion.Euler(28f, 200f, 0f);
            Light key = lightObject.AddComponent<Light>();
            key.type = LightType.Directional;
            key.intensity = 1.25f;
            key.color = new Color(1f, 0.96f, 0.9f);
            key.shadows = LightShadows.None;
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.7f, 0.74f, 0.82f);
            RenderSettings.ambientEquatorColor = new Color(0.5f, 0.5f, 0.52f);
            RenderSettings.ambientGroundColor = new Color(0.3f, 0.28f, 0.26f);
            RenderSettings.fog = false;

            var camObject = new GameObject("Avatar Camera");
            Vector3 focus = root.transform.position + new Vector3(0f, 1.66f, 0f);
            camObject.transform.position = focus + new Vector3(0.12f, 0.08f, -1.75f);
            camObject.transform.LookAt(focus);
            Camera cam = camObject.AddComponent<Camera>();
            cam.fieldOfView = 26f;
            cam.nearClipPlane = 0.1f;
            cam.farClipPlane = 10f;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = style.background;

            var rt = new RenderTexture(AvatarSize, AvatarSize, 24, RenderTextureFormat.ARGB32) { antiAliasing = 4 };
            cam.targetTexture = rt;
            cam.Render();
            cam.Render();
            RenderTexture.active = rt;
            var texture = new Texture2D(AvatarSize, AvatarSize, TextureFormat.RGBA32, false);
            texture.ReadPixels(new Rect(0, 0, AvatarSize, AvatarSize), 0, 0);
            RenderTexture.active = null;
            cam.targetTexture = null;

            // Studio backdrop: lighter centre glow, darker corners.
            for (int y = 0; y < AvatarSize; y++)
            {
                for (int x = 0; x < AvatarSize; x++)
                {
                    float d = new Vector2(x / (float)AvatarSize - 0.5f, y / (float)AvatarSize - 0.62f).magnitude;
                    float shade = Mathf.Lerp(1.25f, 0.55f, Mathf.SmoothStep(0.05f, 0.75f, d));
                    Color c = texture.GetPixel(x, y) * shade;
                    c.a = 1f;
                    texture.SetPixel(x, y, c);
                }
            }
            texture.Apply();
            File.WriteAllBytes(path, texture.EncodeToPNG());

            Object.DestroyImmediate(texture);
            rt.Release();
            Object.DestroyImmediate(rt);
            Object.DestroyImmediate(camObject);
            Object.DestroyImmediate(lightObject);
            Object.DestroyImmediate(root);
            foreach (Material copy in copies.Values) Object.DestroyImmediate(copy);
        }

        // ------------------------------------------------------------------ banners

        private static Color BannerPixel(int style, int x, int y)
        {
            float u = x / (float)(BannerWidth - 1), v = y / (float)(BannerHeight - 1);
            switch (style)
            {
                case 0: // Steel: brushed metal with a diagonal sheen
                {
                    Color a = Hex(0x1E2633), b = Hex(0x3A4658);
                    Color c = Color.Lerp(a, b, u * 0.7f + v * 0.3f);
                    c *= 0.93f + 0.07f * Hash(0, y);
                    float sheen = Mathf.Exp(-Mathf.Pow((u - v * 0.35f - 0.55f) * 7f, 2f));
                    return Color.Lerp(c, Hex(0x7F93AE), sheen * 0.35f);
                }
                case 1: // Hazard stripes
                {
                    bool yellow = Mathf.Repeat((x + y) / 44f, 1f) < 0.5f;
                    Color c = yellow ? Hex(0xF5B21E) : Hex(0x17181B);
                    return c * (0.85f + 0.15f * Noise(x * 0.05f, y * 0.05f));
                }
                case 2: // Woodland camo (pixel blocks)
                    return Camo(x, y, 7, Hex(0x2F3E22), Hex(0x4C6232), Hex(0x6E7A44), Hex(0x3B2D20));
                case 3: // Desert camo
                    return Camo(x, y, 11, Hex(0xB89A6A), Hex(0xD2B98A), Hex(0x8C7150), Hex(0xE3D2AA));
                case 4: // Sunset with a striped sun
                {
                    Color top = Hex(0xFF9A3C), mid = Hex(0xD9406A), bottom = Hex(0x2B1640);
                    Color sky = v > 0.5f ? Color.Lerp(mid, top, (v - 0.5f) * 2f) : Color.Lerp(bottom, mid, v * 2f);
                    Vector2 p = new Vector2((u - 0.78f) * BannerWidth / BannerHeight, v - 0.42f);
                    float sun = p.magnitude;
                    bool cut = v < 0.42f && Mathf.Repeat(v * 22f, 1f) < 0.35f;
                    if (sun < 0.36f && !cut) return Color.Lerp(Hex(0xFFE27A), Hex(0xFF7A3C), Mathf.InverseLerp(0.78f, 0.06f, v));
                    float glow = Mathf.Exp(-sun * 3.2f) * 0.35f;
                    return sky + new Color(glow, glow * 0.6f, glow * 0.2f, 0f);
                }
                case 5: // Arctic digital camo
                    return Camo(x, y, 9, Hex(0xE8EEF3), Hex(0xBFCCD8), Hex(0x8FA3B6), Hex(0xF8FBFD));
                case 6: // Inferno
                {
                    float n = Noise(x * 0.018f, y * 0.045f - u * 2f) * 0.6f + Noise(x * 0.05f, y * 0.11f) * 0.4f;
                    float heat = Mathf.Clamp01((1f - v) * 0.9f + (n - 0.5f) * 0.9f);
                    Color c = heat < 0.5f ? Color.Lerp(Hex(0x120606), Hex(0x8A1A10), heat * 2f) : Color.Lerp(Hex(0x8A1A10), Hex(0xFF8A2A), (heat - 0.5f) * 2f);
                    return c;
                }
                default: // Neon grid
                {
                    Color c = Color.Lerp(Hex(0x0B0618), Hex(0x2A0F45), v);
                    float gx = Mathf.Abs(Mathf.Repeat(x / 40f, 1f) - 0.5f) * 40f;
                    float gy = Mathf.Abs(Mathf.Repeat(y / 40f, 1f) - 0.5f) * 40f;
                    float line = Mathf.Max(Mathf.Clamp01(1.5f - (20f - gx)), Mathf.Clamp01(1.5f - (20f - gy)));
                    c = Color.Lerp(c, Hex(0x2EE6FF), line * 0.55f * (1f - v * 0.6f));
                    float band = Mathf.Exp(-Mathf.Pow((u - v * 0.8f - 0.45f) * 5f, 2f));
                    return Color.Lerp(c, Hex(0xFF3FA4), band * 0.45f);
                }
            }
        }

        private static Color Camo(int x, int y, int seed, Color a, Color b, Color c, Color d)
        {
            const int block = 8;
            int bx = x / block, by = y / block;
            float n = Noise(bx * 0.22f + seed, by * 0.22f) * 0.65f + Noise(bx * 0.6f, by * 0.6f + seed) * 0.35f;
            Color col = n < 0.36f ? a : n < 0.5f ? b : n < 0.64f ? c : d;
            return col * (0.95f + 0.05f * Hash(bx, by));
        }

        // ------------------------------------------------------------------ helpers

        private static float Hash(int x, int y)
        {
            unchecked
            {
                uint h = (uint)(x * 374761393 + y * 668265263);
                h = (h ^ (h >> 13)) * 1274126177u;
                return ((h ^ (h >> 16)) & 0xFFFF) / 65535f;
            }
        }

        private static float Noise(float x, float y)
        {
            int ix = Mathf.FloorToInt(x), iy = Mathf.FloorToInt(y);
            float fx = x - ix, fy = y - iy;
            fx = fx * fx * (3f - 2f * fx);
            fy = fy * fy * (3f - 2f * fy);
            float a = Mathf.Lerp(Hash(ix, iy), Hash(ix + 1, iy), fx);
            float b = Mathf.Lerp(Hash(ix, iy + 1), Hash(ix + 1, iy + 1), fx);
            return Mathf.Lerp(a, b, fy);
        }

        private static Color Hex(int rgb) => new Color(((rgb >> 16) & 0xFF) / 255f, ((rgb >> 8) & 0xFF) / 255f, (rgb & 0xFF) / 255f, 1f);

        private static void SaveTexture(string path, int width, int height, System.Func<int, int, Color> pixel)
        {
            var texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    Color c = pixel(x, y);
                    c.a = 1f;
                    texture.SetPixel(x, y, c);
                }
            }
            texture.Apply();
            File.WriteAllBytes(path, texture.EncodeToPNG());
            Object.DestroyImmediate(texture);
        }

        private static Sprite ImportSprite(string path)
        {
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
            if (AssetImporter.GetAtPath(path) is TextureImporter importer)
            {
                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.mipmapEnabled = false;
                importer.wrapMode = TextureWrapMode.Clamp;
                importer.filterMode = FilterMode.Bilinear;
                importer.textureCompression = TextureImporterCompression.Uncompressed;
                importer.SaveAndReimport();
            }
            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }
    }
}
