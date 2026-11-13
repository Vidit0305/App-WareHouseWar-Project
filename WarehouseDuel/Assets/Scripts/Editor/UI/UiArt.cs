using System;
using System.Collections.Generic;
using System.IO;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.TextCore.LowLevel;

namespace WarehouseDuel.EditorTools.UI
{
    /// <summary>
    /// Generates every UI image the game uses (no external art needed):
    /// 9-sliced rounded panels, circles/rings for touch controls, control icons drawn from signed-distance
    /// shapes, weapon silhouettes rasterised from the weapon prefabs, and the TextMesh Pro display font.
    /// </summary>
    internal static class UiArt
    {
        public const string SpriteFolder = "Assets/UI/Sprites";
        public const string IconFolder = "Assets/UI/Icons";
        public const string FontFolder = "Assets/UI/Fonts";

        public static Sprite Rounded => LoadSprite(SpriteFolder, "UI_Rounded");
        public static Sprite RoundedSmall => LoadSprite(SpriteFolder, "UI_RoundedSmall");
        public static Sprite Circle => LoadSprite(SpriteFolder, "UI_Circle");
        public static Sprite Ring => LoadSprite(SpriteFolder, "UI_Ring");
        public static Sprite Gradient => LoadSprite(SpriteFolder, "UI_Vignette");
        public static Sprite Avatar => AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Art/Branding/AppIcon_Round.png");
        public static Sprite SplashBackground => AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Art/Branding/Splash_Background.png");
        public static Sprite BottomFade => LoadSprite(SpriteFolder, "UI_BottomFade");
        public static Sprite Icon(string name) => LoadSprite(IconFolder, $"Icon_{name}");
        public static TMP_FontAsset DisplayFont => AssetDatabase.LoadAssetAtPath<TMP_FontAsset>($"{FontFolder}/RussoOne SDF.asset");

        public static void BuildAll(SetupReport report)
        {
            FolderStructure.EnsureFolder(SpriteFolder);
            FolderStructure.EnsureFolder(IconFolder);

            SaveSprite(SpriteFolder, "UI_Rounded", 64, (x, y) => RoundedRect(x, y, 64, 20f), border: 24);
            SaveSprite(SpriteFolder, "UI_RoundedSmall", 32, (x, y) => RoundedRect(x, y, 32, 8f), border: 10);
            SaveSprite(SpriteFolder, "UI_Circle", 128, (x, y) => Disc(x, y, 128, 62f));
            SaveSprite(SpriteFolder, "UI_Ring", 128, (x, y) => Mathf.Min(Disc(x, y, 128, 62f), 1f - Disc(x, y, 128, 52f)));
            SaveSprite(SpriteFolder, "UI_Vignette", 128, Vignette);
            SaveSprite(SpriteFolder, "UI_BottomFade", 64, (x, y) => Mathf.SmoothStep(1f, 0f, y / 63f));

            foreach (KeyValuePair<string, Func<Vector2, float>> icon in Icons)
            {
                SaveSprite(IconFolder, $"Icon_{icon.Key}", 128, (x, y) =>
                {
                    // 2×2 supersampling for smooth edges.
                    float sum = 0f;
                    for (int sy = 0; sy < 2; sy++)
                    for (int sx = 0; sx < 2; sx++)
                    {
                        var p = new Vector2((x + 0.25f + sx * 0.5f) / 64f - 1f, (y + 0.25f + sy * 0.5f) / 64f - 1f);
                        sum += Mathf.Clamp01(0.5f - icon.Value(p) * 64f);
                    }
                    return sum / 4f;
                });
            }

            BuildFont(report);
            report.Pass("UI art: panels, circles, control icons and display font generated.");
        }

        // ------------------------------------------------------------------ weapon silhouettes

        /// <summary>Side-view white silhouette of a weapon prefab, rasterised on the CPU from its mesh bounds.</summary>
        public static Texture2D WeaponSilhouette(GameObject prefab, string name)
        {
            const int width = 256, height = 128, pad = 10;
            var polygons = new List<Vector2[]>();
            Vector2 min = new Vector2(float.MaxValue, float.MaxValue), max = new Vector2(float.MinValue, float.MinValue);

            Matrix4x4 rootInverse = prefab.transform.worldToLocalMatrix;
            foreach (MeshFilter filter in prefab.GetComponentsInChildren<MeshFilter>(true))
            {
                if (filter.sharedMesh == null) continue;
                Bounds b = filter.sharedMesh.bounds;
                Matrix4x4 m = rootInverse * filter.transform.localToWorldMatrix;
                var corners = new Vector2[8];
                int i = 0;
                for (int cx = -1; cx <= 1; cx += 2)
                for (int cy = -1; cy <= 1; cy += 2)
                for (int cz = -1; cz <= 1; cz += 2)
                {
                    Vector3 local = b.center + Vector3.Scale(b.extents, new Vector3(cx, cy, cz));
                    Vector3 p = m.MultiplyPoint3x4(local);
                    var projected = new Vector2(p.z, p.y); // side view: barrel points right
                    corners[i++] = projected;
                    min = Vector2.Min(min, projected);
                    max = Vector2.Max(max, projected);
                }
                polygons.Add(ConvexHull(corners));
            }

            Vector2 size = max - min;
            float scale = Mathf.Min((width - pad * 2) / size.x, (height - pad * 2) / size.y);
            Vector2 offset = new Vector2((width - size.x * scale) / 2f, (height - size.y * scale) / 2f);

            string path = $"{IconFolder}/Weapon_{name}.png";
            var texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    int inside = 0;
                    for (int s = 0; s < 4; s++)
                    {
                        var sample = new Vector2(x + 0.25f + (s % 2) * 0.5f, y + 0.25f + (s / 2) * 0.5f);
                        Vector2 world = (sample - offset) / scale + min;
                        foreach (Vector2[] polygon in polygons)
                        {
                            if (InsideConvex(polygon, world)) { inside++; break; }
                        }
                    }
                    texture.SetPixel(x, y, new Color(1f, 1f, 1f, inside / 4f));
                }
            }
            texture.Apply();
            File.WriteAllBytes(path, texture.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(texture);
            ImportSprite(path, 0);
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        private static Vector2[] ConvexHull(Vector2[] points)
        {
            var sorted = new List<Vector2>(points);
            sorted.Sort((a, b) => a.x != b.x ? a.x.CompareTo(b.x) : a.y.CompareTo(b.y));
            var hull = new List<Vector2>();
            for (int pass = 0; pass < 2; pass++)
            {
                int start = hull.Count;
                foreach (Vector2 p in sorted)
                {
                    while (hull.Count >= start + 2 && Cross(hull[hull.Count - 2], hull[hull.Count - 1], p) <= 0f) hull.RemoveAt(hull.Count - 1);
                    hull.Add(p);
                }
                hull.RemoveAt(hull.Count - 1);
                sorted.Reverse();
            }
            return hull.ToArray();
        }

        private static float Cross(Vector2 o, Vector2 a, Vector2 b) => (a.x - o.x) * (b.y - o.y) - (a.y - o.y) * (b.x - o.x);

        private static bool InsideConvex(Vector2[] hull, Vector2 p)
        {
            if (hull.Length < 3) return false;
            for (int i = 0; i < hull.Length; i++)
            {
                if (Cross(hull[i], hull[(i + 1) % hull.Length], p) < 0f) return false;
            }
            return true;
        }

        // ------------------------------------------------------------------ icon shapes (signed distance, p in [-1,1])

        private static readonly Dictionary<string, Func<Vector2, float>> Icons = new Dictionary<string, Func<Vector2, float>>
        {
            ["Fire"] = p => Mathf.Min(SdRing(p, Vector2.zero, 0.5f, 0.08f),
                Mathf.Min(SdCircle(p, Vector2.zero, 0.13f),
                Mathf.Min(Mathf.Min(SdBox(p, new Vector2(0f, 0.75f), new Vector2(0.06f, 0.17f)), SdBox(p, new Vector2(0f, -0.75f), new Vector2(0.06f, 0.17f))),
                          Mathf.Min(SdBox(p, new Vector2(0.75f, 0f), new Vector2(0.17f, 0.06f)), SdBox(p, new Vector2(-0.75f, 0f), new Vector2(0.17f, 0.06f)))))),
            ["Aim"] = p => Mathf.Min(SdRing(p, Vector2.zero, 0.62f, 0.07f),
                Mathf.Min(SdBox(p, Vector2.zero, new Vector2(0.62f, 0.035f)), SdBox(p, Vector2.zero, new Vector2(0.035f, 0.62f)))),
            ["Bullets"] = p =>
            {
                float d = 10f;
                foreach (float x in new[] { -0.42f, 0f, 0.42f })
                {
                    float body = SdBox(p, new Vector2(x, -0.1f), new Vector2(0.125f, 0.34f));
                    float tip = SdCircle(p, new Vector2(x, 0.24f), 0.125f);
                    float rim = SdBox(p, new Vector2(x, -0.5f), new Vector2(0.155f, 0.05f));
                    d = Mathf.Min(d, Mathf.Min(body, Mathf.Min(tip, rim)));
                }
                return d;
            },
            ["Reload"] = p =>
            {
                float arc = SdRing(p, Vector2.zero, 0.5f, 0.1f);
                float angle = Mathf.Repeat(Mathf.Atan2(p.y, p.x) * Mathf.Rad2Deg, 360f);
                if ((angle > 18f && angle < 75f) || (angle > 198f && angle < 255f)) arc = Mathf.Max(arc, 0.2f);
                return Mathf.Min(arc, Mathf.Min(ArrowHead(p, 18f), ArrowHead(p, 198f)));
            },
            ["Jump"] = p => Mathf.Min(SdChevron(p, new Vector2(0f, 0.66f), 0.52f, 0.12f), SdBox(p, new Vector2(0f, -0.08f), new Vector2(0.1f, 0.62f))),
            ["Sprint"] = p =>
            {
                float d = 10f;
                foreach (float x in new[] { -0.2f, 0.26f })
                {
                    d = Mathf.Min(d, Mathf.Min(SdSegment(p, new Vector2(x - 0.24f, 0.46f), new Vector2(x + 0.16f, 0f), 0.11f),
                                               SdSegment(p, new Vector2(x + 0.16f, 0f), new Vector2(x - 0.24f, -0.46f), 0.11f)));
                }
                return d;
            },
            ["Swap"] = p => Mathf.Min(
                Mathf.Min(SdBox(p, new Vector2(-0.1f, 0.25f), new Vector2(0.45f, 0.07f)), SdTriangle(p, new Vector2(0.65f, 0.25f), new Vector2(0.3f, 0.5f), new Vector2(0.3f, 0f))),
                Mathf.Min(SdBox(p, new Vector2(0.1f, -0.25f), new Vector2(0.45f, 0.07f)), SdTriangle(p, new Vector2(-0.65f, -0.25f), new Vector2(-0.3f, 0f), new Vector2(-0.3f, -0.5f)))),
            ["Pause"] = p => Mathf.Min(SdBox(p, new Vector2(-0.22f, 0f), new Vector2(0.11f, 0.48f)), SdBox(p, new Vector2(0.22f, 0f), new Vector2(0.11f, 0.48f))),
            ["Copy"] = p => Mathf.Min(
                Mathf.Abs(SdBox(p, new Vector2(0.12f, -0.12f), new Vector2(0.4f, 0.48f))) - 0.06f,
                Mathf.Max(Mathf.Abs(SdBox(p, new Vector2(-0.16f, 0.16f), new Vector2(0.4f, 0.48f))) - 0.06f, -SdBox(p, new Vector2(0.12f, -0.12f), new Vector2(0.46f, 0.54f)))),
            ["Back"] = p => SdChevron(new Vector2(p.y, -p.x), new Vector2(0f, 0.1f), 0.55f, 0.11f),
            ["Settings"] = p =>
            {
                float angle = Mathf.Atan2(p.y, p.x);
                float teeth = 0.62f + 0.12f * Mathf.Clamp(Mathf.Cos(angle * 8f) * 3f, -1f, 1f);
                return Mathf.Max(p.magnitude - teeth, 0.25f - p.magnitude);
            },
            ["Profile"] = p => Mathf.Min(SdCircle(p, new Vector2(0f, 0.3f), 0.28f),
                Mathf.Max(SdCircle(p, new Vector2(0f, -0.62f), 0.55f), -p.y - 0.62f)),
            ["Trophy"] = p =>
            {
                float bowl = Mathf.Max(SdCircle(p, new Vector2(0f, 0.55f), 0.44f), p.y - 0.55f);
                float rim = SdBox(p, new Vector2(0f, 0.6f), new Vector2(0.48f, 0.07f));
                float handleL = Mathf.Max(SdRing(p, new Vector2(-0.44f, 0.38f), 0.17f, 0.055f), p.x + 0.4f);
                float handleR = Mathf.Max(SdRing(p, new Vector2(0.44f, 0.38f), 0.17f, 0.055f), 0.4f - p.x);
                float stem = SdBox(p, new Vector2(0f, -0.08f), new Vector2(0.075f, 0.22f));
                float neck = SdBox(p, new Vector2(0f, -0.32f), new Vector2(0.2f, 0.05f));
                float foot = SdBox(p, new Vector2(0f, -0.48f), new Vector2(0.36f, 0.1f));
                return Mathf.Min(Mathf.Min(Mathf.Min(bowl, rim), Mathf.Min(handleL, handleR)), Mathf.Min(stem, Mathf.Min(neck, foot)));
            },
            ["Plus"] = p => Mathf.Min(SdBox(p, Vector2.zero, new Vector2(0.52f, 0.1f)), SdBox(p, Vector2.zero, new Vector2(0.1f, 0.52f))),
            ["Lock"] = p => Mathf.Min(SdBox(p, new Vector2(0f, -0.22f), new Vector2(0.42f, 0.32f)) - 0.04f,
                Mathf.Min(Mathf.Max(SdRing(p, new Vector2(0f, 0.2f), 0.26f, 0.08f), 0.2f - p.y),
                          Mathf.Min(SdBox(p, new Vector2(-0.26f, 0.12f), new Vector2(0.08f, 0.1f)), SdBox(p, new Vector2(0.26f, 0.12f), new Vector2(0.08f, 0.1f))))),
            ["Play"] = p => SdTriangle(p, new Vector2(-0.38f, 0.55f), new Vector2(-0.38f, -0.55f), new Vector2(0.55f, 0f)) - 0.04f,
            ["Upload"] = p => Mathf.Min(Mathf.Min(SdChevron(p, new Vector2(0f, 0.62f), 0.36f, 0.1f), SdBox(p, new Vector2(0f, 0.12f), new Vector2(0.09f, 0.45f))),
                Mathf.Min(SdBox(p, new Vector2(0f, -0.58f), new Vector2(0.6f, 0.08f)),
                          Mathf.Min(SdBox(p, new Vector2(-0.6f, -0.42f), new Vector2(0.08f, 0.2f)), SdBox(p, new Vector2(0.6f, -0.42f), new Vector2(0.08f, 0.2f))))),
            ["Podium"] = p => Mathf.Min(SdBox(p, new Vector2(0f, -0.12f), new Vector2(0.2f, 0.52f)),
                Mathf.Min(SdBox(p, new Vector2(-0.46f, -0.3f), new Vector2(0.2f, 0.34f)), SdBox(p, new Vector2(0.46f, -0.42f), new Vector2(0.2f, 0.22f)))),
            ["Close"] = p => Mathf.Min(SdSegment(p, new Vector2(-0.42f, -0.42f), new Vector2(0.42f, 0.42f), 0.1f),
                SdSegment(p, new Vector2(-0.42f, 0.42f), new Vector2(0.42f, -0.42f), 0.1f)),
            ["Star"] = p =>
            {
                // Tips every 72°, starting straight up; radius blends from the valley (0.3) to the tip (0.68).
                float angle = Mathf.Atan2(p.x, p.y);
                float sector = Mathf.Abs(Mathf.Repeat(angle, Mathf.PI * 2f / 5f) - Mathf.PI / 5f) / (Mathf.PI / 5f);
                return p.magnitude - Mathf.Lerp(0.3f, 0.68f, sector * sector);
            },
        };

        /// <summary>Arrowhead at the end of a circular arc (radius 0.5) at <paramref name="degrees"/>, pointing clockwise.</summary>
        private static float ArrowHead(Vector2 p, float degrees)
        {
            float a = degrees * Mathf.Deg2Rad;
            var radial = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
            var tangent = new Vector2(radial.y, -radial.x);
            Vector2 c = radial * 0.5f;
            return SdTriangle(p, c + tangent * 0.26f, c - radial * 0.24f - tangent * 0.04f, c + radial * 0.24f - tangent * 0.04f);
        }

        private static float SdCircle(Vector2 p, Vector2 c, float r) => (p - c).magnitude - r;
        private static float SdRing(Vector2 p, Vector2 c, float r, float thickness) => Mathf.Abs((p - c).magnitude - r) - thickness;

        private static float SdBox(Vector2 p, Vector2 c, Vector2 half)
        {
            Vector2 d = new Vector2(Mathf.Abs(p.x - c.x), Mathf.Abs(p.y - c.y)) - half;
            return new Vector2(Mathf.Max(d.x, 0f), Mathf.Max(d.y, 0f)).magnitude + Mathf.Min(Mathf.Max(d.x, d.y), 0f);
        }

        private static float SdSegment(Vector2 p, Vector2 a, Vector2 b, float r)
        {
            Vector2 pa = p - a, ba = b - a;
            float h = Mathf.Clamp01(Vector2.Dot(pa, ba) / Vector2.Dot(ba, ba));
            return (pa - ba * h).magnitude - r;
        }

        private static float SdChevron(Vector2 p, Vector2 tip, float width, float thickness) =>
            Mathf.Min(SdSegment(p, tip, tip + new Vector2(-width, -width * 0.75f), thickness),
                      SdSegment(p, tip, tip + new Vector2(width, -width * 0.75f), thickness));

        private static float SdTriangle(Vector2 p, Vector2 a, Vector2 b, Vector2 c)
        {
            // Negative inside: distance to the edges, sign from barycentric test.
            float d = Mathf.Min(SdSegment(p, a, b, 0f), Mathf.Min(SdSegment(p, b, c, 0f), SdSegment(p, c, a, 0f)));
            bool inside = Mathf.Sign(Cross(a, b, p)) == Mathf.Sign(Cross(b, c, p)) && Mathf.Sign(Cross(b, c, p)) == Mathf.Sign(Cross(c, a, p));
            return inside ? -d : d;
        }

        // ------------------------------------------------------------------ base sprites

        private static float RoundedRect(int x, int y, int size, float radius)
        {
            float half = size / 2f;
            Vector2 p = new Vector2(x + 0.5f - half, y + 0.5f - half);
            Vector2 q = new Vector2(Mathf.Abs(p.x), Mathf.Abs(p.y)) - new Vector2(half - radius, half - radius);
            float d = new Vector2(Mathf.Max(q.x, 0f), Mathf.Max(q.y, 0f)).magnitude + Mathf.Min(Mathf.Max(q.x, q.y), 0f) - radius;
            return Mathf.Clamp01(0.5f - d);
        }

        private static float Disc(int x, int y, int size, float radius)
        {
            float d = new Vector2(x + 0.5f - size / 2f, y + 0.5f - size / 2f).magnitude - radius;
            return Mathf.Clamp01(0.5f - d);
        }

        private static float Vignette(int x, int y)
        {
            float d = new Vector2(x / 127f - 0.5f, y / 127f - 0.5f).magnitude * 2f;
            return Mathf.Clamp01((d - 0.55f) / 0.6f);
        }

        // ------------------------------------------------------------------ assets

        private static void SaveSprite(string folder, string name, int size, Func<int, int, float> alpha, int border = 0)
        {
            string path = $"{folder}/{name}.png";
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++) texture.SetPixel(x, y, new Color(1f, 1f, 1f, alpha(x, y)));
            }
            texture.Apply();
            File.WriteAllBytes(path, texture.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(texture);
            ImportSprite(path, border);
        }

        private static void ImportSprite(string path, int border)
        {
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
            if (!(AssetImporter.GetAtPath(path) is TextureImporter importer)) return;
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.filterMode = FilterMode.Bilinear;
            importer.spriteBorder = new Vector4(border, border, border, border);
            importer.SaveAndReimport();
        }

        private static Sprite LoadSprite(string folder, string name) => AssetDatabase.LoadAssetAtPath<Sprite>($"{folder}/{name}.png");

        private static void BuildFont(SetupReport report)
        {
            string fontPath = $"{FontFolder}/RussoOne-Regular.ttf";
            string assetPath = $"{FontFolder}/RussoOne SDF.asset";
            var font = AssetDatabase.LoadAssetAtPath<Font>(fontPath);
            if (font == null)
            {
                report.Warn($"Display font not found at {fontPath}; UI will use the default TMP font.");
                return;
            }
            if (AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(assetPath) != null) return;

            TMP_FontAsset fontAsset = TMP_FontAsset.CreateFontAsset(font, 72, 8, GlyphRenderMode.SDFAA, 1024, 1024, AtlasPopulationMode.Dynamic);
            fontAsset.name = "RussoOne SDF";
            AssetDatabase.CreateAsset(fontAsset, assetPath);
            fontAsset.atlasTextures[0].name = "RussoOne SDF Atlas";
            AssetDatabase.AddObjectToAsset(fontAsset.atlasTextures[0], fontAsset);
            fontAsset.material.name = "RussoOne SDF Material";
            AssetDatabase.AddObjectToAsset(fontAsset.material, fontAsset);
            // Pre-bake common characters so the first frame doesn't rasterise glyphs.
            fontAsset.TryAddCharacters(" ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789:/!?.,-–—+()%…'\"#&•<>[]");
            EditorUtility.SetDirty(fontAsset);
            AssetDatabase.SaveAssets();
        }
    }
}
