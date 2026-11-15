using System;
using System.Collections.Generic;
using System.IO;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.TextCore.LowLevel;

namespace WarehouseWar.EditorTools.UI
{
    /// <summary>
    /// Generates every UI image the game uses (no external art needed):
    /// 9-sliced rounded panels, circles/rings for touch controls, control icons drawn from signed-distance
    /// shapes, weapon silhouettes rasterised from the weapon prefabs, and the TextMesh Pro display font.
    /// </summary>
    internal static class UiArt
    {
        public const string SpriteFolder = "Assets/Art/UI/Sprites";
        public const string IconFolder = "Assets/Art/UI/Icons";
        public const string FontFolder = "Assets/Art/Fonts";

        public static Sprite Rounded => LoadSprite(SpriteFolder, "UI_Rounded");
        public static Sprite RoundedSmall => LoadSprite(SpriteFolder, "UI_RoundedSmall");
        public static Sprite Circle => LoadSprite(SpriteFolder, "UI_Circle");
        public static Sprite Ring => LoadSprite(SpriteFolder, "UI_Ring");
        /// <summary>270° ring segment whose tail fades out — the loading spinner.</summary>
        public static Sprite Arc => LoadSprite(SpriteFolder, "UI_Arc");
        public static Sprite Scope => LoadSprite(SpriteFolder, "UI_Scope");
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
            SaveSprite(SpriteFolder, "UI_Scope", 512, ScopeMask);
            SaveSprite(SpriteFolder, "UI_BottomFade", 64, (x, y) => Mathf.SmoothStep(1f, 0f, y / 63f));
            SaveSprite(SpriteFolder, "UI_Arc", 128, (x, y) =>
            {
                float ring = Mathf.Min(Disc(x, y, 128, 62f), 1f - Disc(x, y, 128, 50f));
                float angle = Mathf.Repeat(Mathf.Atan2(y + 0.5f - 64f, x + 0.5f - 64f) * Mathf.Rad2Deg, 360f) / 360f;
                float tail = angle < 0.75f ? Mathf.Pow(angle / 0.75f, 1.6f) : 0f;          // head at 270°, tail fades to 0°
                float cap = angle >= 0.75f && angle < 0.765f ? 1f : 0f;                   // rounded-ish head
                return ring * Mathf.Max(tail, cap);
            });

            foreach (KeyValuePair<string, Func<Vector2, float>> icon in Icons)
            {
                // Match HUD icons are drawn at 256 px (shown large on high-DPI phones), the rest at 128 px.
                int size = icon.Key.StartsWith("Hud") ? 256 : 128;
                float half = size / 2f;
                SaveSprite(IconFolder, $"Icon_{icon.Key}", size, (x, y) =>
                {
                    // 2×2 supersampling for smooth edges.
                    float sum = 0f;
                    for (int sy = 0; sy < 2; sy++)
                    for (int sx = 0; sx < 2; sx++)
                    {
                        var p = new Vector2((x + 0.25f + sx * 0.5f) / half - 1f, (y + 0.25f + sy * 0.5f) / half - 1f);
                        sum += Mathf.Clamp01(0.5f - icon.Value(p) * half);
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
            // ---- match HUD (256 px)
            ["HudFire"] = p =>
            {
                // A rifle cartridge flying up-right, with three speed lines behind it.
                Vector2 q = Rot(p, -45f);
                float body = SdBox(q, new Vector2(0f, -0.1f), new Vector2(0.19f, 0.36f));
                float nose = Mathf.Min(SdCircle(q, new Vector2(0f, 0.3f), 0.19f), SdTriangle(q, new Vector2(-0.19f, 0.3f), new Vector2(0.19f, 0.3f), new Vector2(0f, 0.7f)));
                float rim = SdBox(q, new Vector2(0f, -0.52f), new Vector2(0.23f, 0.06f));
                float groove = SdBox(q, new Vector2(0f, -0.42f), new Vector2(0.25f, 0.025f));
                float neck = SdBox(q, new Vector2(0f, 0.25f), new Vector2(0.25f, 0.02f));
                float bullet = Mathf.Max(Mathf.Min(Mathf.Min(body, nose), rim), -Mathf.Min(groove, neck));
                float lines = Mathf.Min(SdSegment(q, new Vector2(-0.42f, -0.2f), new Vector2(-0.42f, -0.72f), 0.055f),
                    Mathf.Min(SdSegment(q, new Vector2(0.42f, -0.2f), new Vector2(0.42f, -0.72f), 0.055f), SdSegment(q, new Vector2(0f, -0.68f), new Vector2(0f, -0.92f), 0.055f)));
                return Mathf.Min(bullet, lines);
            },
            ["HudAim"] = p =>
            {
                // Scope reticle: ring, broken crosshair, centre dot, mil ticks.
                float ring = SdRing(p, Vector2.zero, 0.66f, 0.07f);
                float cross = Mathf.Min(Mathf.Min(SdBox(p, new Vector2(0f, 0.43f), new Vector2(0.04f, 0.21f)), SdBox(p, new Vector2(0f, -0.43f), new Vector2(0.04f, 0.21f))),
                                        Mathf.Min(SdBox(p, new Vector2(0.43f, 0f), new Vector2(0.21f, 0.04f)), SdBox(p, new Vector2(-0.43f, 0f), new Vector2(0.21f, 0.04f))));
                float dot = SdCircle(p, Vector2.zero, 0.08f);
                float ticks = Mathf.Min(Mathf.Min(SdBox(p, new Vector2(0.2f, 0f), new Vector2(0.015f, 0.07f)), SdBox(p, new Vector2(-0.2f, 0f), new Vector2(0.015f, 0.07f))),
                                        Mathf.Min(SdBox(p, new Vector2(0f, 0.2f), new Vector2(0.07f, 0.015f)), SdBox(p, new Vector2(0f, -0.2f), new Vector2(0.07f, 0.015f))));
                return Mathf.Min(Mathf.Min(ring, cross), Mathf.Min(dot, ticks));
            },
            ["HudReload"] = p =>
            {
                // A curved magazine inside a circular "reload" arrow.
                float arc = SdRing(p, Vector2.zero, 0.7f, 0.075f);
                float angle = Mathf.Repeat(Mathf.Atan2(p.y, p.x) * Mathf.Rad2Deg, 360f);
                if (angle > 40f && angle < 95f) arc = Mathf.Max(arc, 0.2f);
                float head = SdTriangle(p, new Vector2(0.38f, 0.66f), new Vector2(0.66f, 0.27f), new Vector2(0.78f, 0.62f));
                Vector2 m = Rot(p - new Vector2(0f, -0.04f), 12f);
                float mag = SdBox(m, new Vector2(0f, -0.04f), new Vector2(0.17f, 0.3f)) - 0.03f;
                float lip = SdBox(m, new Vector2(0f, 0.3f), new Vector2(0.11f, 0.06f));
                float slot = SdBox(m, new Vector2(0f, -0.04f), new Vector2(0.05f, 0.2f));
                float magazine = Mathf.Max(Mathf.Min(mag, lip), -slot);
                return Mathf.Min(Mathf.Min(arc, head), magazine);
            },
            ["HudJump"] = p => Mathf.Min(Mathf.Min(SdChevron(p, new Vector2(0f, 0.62f), 0.5f, 0.1f), SdChevron(p, new Vector2(0f, 0.18f), 0.5f, 0.1f)),
                SdBox(p, new Vector2(0f, -0.55f), new Vector2(0.5f, 0.07f))),
            ["HudSprint"] = p =>
            {
                // Running soldier silhouette with two speed lines.
                float head = SdCircle(p, new Vector2(0.16f, 0.62f), 0.14f);
                float torso = SdSegment(p, new Vector2(0.08f, 0.4f), new Vector2(-0.06f, 0.0f), 0.1f);
                float armFront = Mathf.Min(SdSegment(p, new Vector2(0.06f, 0.34f), new Vector2(0.3f, 0.18f), 0.07f), SdSegment(p, new Vector2(0.3f, 0.18f), new Vector2(0.48f, 0.32f), 0.065f));
                float armBack = Mathf.Min(SdSegment(p, new Vector2(0.04f, 0.34f), new Vector2(-0.2f, 0.24f), 0.07f), SdSegment(p, new Vector2(-0.2f, 0.24f), new Vector2(-0.3f, 0.04f), 0.065f));
                float legFront = Mathf.Min(SdSegment(p, new Vector2(-0.05f, 0.0f), new Vector2(0.2f, -0.24f), 0.08f), SdSegment(p, new Vector2(0.2f, -0.24f), new Vector2(0.14f, -0.6f), 0.075f));
                float legBack = Mathf.Min(SdSegment(p, new Vector2(-0.07f, 0.0f), new Vector2(-0.26f, -0.26f), 0.08f), SdSegment(p, new Vector2(-0.26f, -0.26f), new Vector2(-0.56f, -0.3f), 0.075f));
                float lines = Mathf.Min(SdSegment(p, new Vector2(-0.7f, 0.4f), new Vector2(-0.42f, 0.4f), 0.04f), SdSegment(p, new Vector2(-0.78f, 0.16f), new Vector2(-0.48f, 0.16f), 0.04f));
                return Mathf.Min(Mathf.Min(Mathf.Min(head, torso), Mathf.Min(armFront, armBack)), Mathf.Min(Mathf.Min(legFront, legBack), lines));
            },
            ["HudSwap"] = p =>
            {
                // Two bold arrows chasing each other around a circle.
                float arc = SdRing(p, Vector2.zero, 0.56f, 0.09f);
                float angle = Mathf.Repeat(Mathf.Atan2(p.y, p.x) * Mathf.Rad2Deg, 360f);
                if ((angle > 60f && angle < 120f) || (angle > 240f && angle < 300f)) arc = Mathf.Max(arc, 0.2f);
                float headA = SdTriangle(p, new Vector2(0.06f, 0.82f), new Vector2(0.06f, 0.3f), new Vector2(-0.3f, 0.56f));
                float headB = SdTriangle(p, new Vector2(-0.06f, -0.82f), new Vector2(-0.06f, -0.3f), new Vector2(0.3f, -0.56f));
                return Mathf.Min(arc, Mathf.Min(headA, headB));
            },
            ["Alert"] = p => Mathf.Min(SdRing(p, Vector2.zero, 0.7f, 0.08f),
                Mathf.Min(SdSegment(p, new Vector2(0f, 0.38f), new Vector2(0f, -0.06f), 0.1f), SdCircle(p, new Vector2(0f, -0.32f), 0.1f))),
            ["Star"] = SdStar,

            // ---- battlefield icons (screen headers, menu cards)
            ["Helmet"] = p =>
            {
                float dome = Mathf.Max(SdCircle(p, new Vector2(0f, -0.12f), 0.66f), -(p.y + 0.12f));
                float brim = SdBox(p, new Vector2(0f, -0.2f), new Vector2(0.84f, 0.08f)) - 0.02f;
                float band = SdBox(p, new Vector2(0f, 0.08f), new Vector2(0.72f, 0.045f));
                float shell = Mathf.Max(Mathf.Min(dome, brim), -band);
                float strap = Mathf.Max(SdRing(p, new Vector2(0f, -0.3f), 0.4f, 0.05f), p.y + 0.34f);
                return Mathf.Min(shell, strap);
            },
            ["Crosshair"] = p =>
            {
                float ring = SdRing(p, Vector2.zero, 0.56f, 0.07f);
                float vertical = Mathf.Min(SdBox(p, new Vector2(0f, 0.62f), new Vector2(0.06f, 0.26f)), SdBox(p, new Vector2(0f, -0.62f), new Vector2(0.06f, 0.26f)));
                float horizontal = Mathf.Min(SdBox(p, new Vector2(0.62f, 0f), new Vector2(0.26f, 0.06f)), SdBox(p, new Vector2(-0.62f, 0f), new Vector2(0.26f, 0.06f)));
                return Mathf.Min(Mathf.Min(ring, SdCircle(p, Vector2.zero, 0.1f)), Mathf.Min(vertical, horizontal));
            },
            ["Medal"] = p =>
            {
                var c = new Vector2(0f, -0.3f);
                float disc = SdCircle(p, c, 0.52f);
                float rim = SdRing(p, c, 0.4f, 0.035f);
                float star = SdStar((p - c) / 0.5f) * 0.5f;
                float face = Mathf.Max(disc, -Mathf.Min(rim, star));
                float straps = Mathf.Min(SdSegment(p, new Vector2(-0.38f, 0.86f), new Vector2(-0.1f, 0.2f), 0.13f),
                                         SdSegment(p, new Vector2(0.38f, 0.86f), new Vector2(0.1f, 0.2f), 0.13f));
                straps = Mathf.Max(straps, -(disc - 0.07f));
                return Mathf.Min(face, straps);
            },
            ["DogTag"] = p =>
            {
                Vector2 q = Rot(p - new Vector2(0.14f, -0.2f), -14f);
                float front = SdBox(q, Vector2.zero, new Vector2(0.32f, 0.48f)) - 0.14f;
                float cut = Mathf.Min(SdCircle(q, new Vector2(0f, 0.42f), 0.08f),
                    Mathf.Min(SdBox(q, new Vector2(0f, 0.06f), new Vector2(0.24f, 0.035f)), SdBox(q, new Vector2(-0.05f, -0.14f), new Vector2(0.19f, 0.035f))));
                float frontTag = Mathf.Max(front, -cut);
                Vector2 r = Rot(p - new Vector2(-0.2f, -0.06f), 12f);
                float back = Mathf.Abs(SdBox(r, Vector2.zero, new Vector2(0.32f, 0.48f)) - 0.14f) - 0.05f;
                float chain = Mathf.Max(SdRing(p, new Vector2(-0.04f, 0.4f), 0.42f, 0.035f), 0.4f - p.y);
                float behind = Mathf.Max(Mathf.Min(back, chain), -(front - 0.07f));
                return Mathf.Min(frontTag, behind);
            },
            ["Crate"] = p =>
            {
                // Supply crate: solid box with a lid seam, two side planks and a star stencil cut out of the front.
                float body = SdBox(p, new Vector2(0f, -0.1f), new Vector2(0.74f, 0.52f)) - 0.05f;
                float lid = SdBox(p, new Vector2(0f, 0.56f), new Vector2(0.8f, 0.1f)) - 0.04f;
                float seams = Mathf.Min(SdBox(p, new Vector2(-0.46f, -0.1f), new Vector2(0.025f, 0.5f)), SdBox(p, new Vector2(0.46f, -0.1f), new Vector2(0.025f, 0.5f)));
                float star = SdStar((p - new Vector2(0f, -0.1f)) / 0.48f) * 0.48f;
                return Mathf.Min(Mathf.Max(body, -Mathf.Min(seams, star)), lid);
            },
            ["Pin"] = p =>
            {
                var c = new Vector2(0f, 0.24f);
                float head = SdCircle(p, c, 0.5f);
                float tail = SdTriangle(p, new Vector2(-0.43f, 0f), new Vector2(0.43f, 0f), new Vector2(0f, -0.86f));
                return Mathf.Max(Mathf.Min(head, tail), -SdCircle(p, c, 0.2f));
            },
            ["Target"] = p => Mathf.Min(Mathf.Min(SdRing(p, Vector2.zero, 0.72f, 0.07f), SdRing(p, Vector2.zero, 0.44f, 0.07f)), SdCircle(p, Vector2.zero, 0.17f)),
            ["Globe"] = p =>
            {
                float outline = SdRing(p, Vector2.zero, 0.72f, 0.065f);
                float meridian = Mathf.Abs(new Vector2(p.x / 0.36f, p.y / 0.72f).magnitude - 1f) * 0.36f - 0.05f;
                float axis = SdBox(p, Vector2.zero, new Vector2(0.05f, 0.72f));
                float equator = SdBox(p, Vector2.zero, new Vector2(0.72f, 0.05f));
                float latitudes = Mathf.Min(SdBox(p, new Vector2(0f, 0.38f), new Vector2(0.64f, 0.045f)), SdBox(p, new Vector2(0f, -0.38f), new Vector2(0.64f, 0.045f)));
                float lines = Mathf.Max(Mathf.Min(Mathf.Min(meridian, axis), Mathf.Min(equator, latitudes)), SdCircle(p, Vector2.zero, 0.7f));
                return Mathf.Min(outline, lines);
            },
        };

        /// <summary>Five-pointed star, tips every 72° starting straight up; radius blends from the valley (0.3) to the tip (0.68).</summary>
        private static float SdStar(Vector2 p)
        {
            float angle = Mathf.Atan2(p.x, p.y);
            float sector = Mathf.Abs(Mathf.Repeat(angle, Mathf.PI * 2f / 5f) - Mathf.PI / 5f) / (Mathf.PI / 5f);
            return p.magnitude - Mathf.Lerp(0.3f, 0.68f, sector * sector);
        }

        /// <summary>Arrowhead at the end of a circular arc (radius 0.5) at <paramref name="degrees"/>, pointing clockwise.</summary>
        private static float ArrowHead(Vector2 p, float degrees)
        {
            float a = degrees * Mathf.Deg2Rad;
            var radial = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
            var tangent = new Vector2(radial.y, -radial.x);
            Vector2 c = radial * 0.5f;
            return SdTriangle(p, c + tangent * 0.26f, c - radial * 0.24f - tangent * 0.04f, c + radial * 0.24f - tangent * 0.04f);
        }

        private static Vector2 Rot(Vector2 p, float degrees)
        {
            float r = degrees * Mathf.Deg2Rad, cs = Mathf.Cos(r), sn = Mathf.Sin(r);
            return new Vector2(p.x * cs - p.y * sn, p.x * sn + p.y * cs);
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

        /// <summary>Sniper scope mask (tinted black): opaque outside the lens, soft inner edge shadow, thin reticle with thicker outer posts.</summary>
        private static float ScopeMask(int x, int y)
        {
            float dx = x + 0.5f - 256f, dy = y + 0.5f - 256f;
            float r = Mathf.Sqrt(dx * dx + dy * dy);
            float outside = Mathf.Clamp01((r - 238f) / 3f);
            float shade = Mathf.Clamp01((r - 170f) / 68f);
            shade = shade * shade * 0.55f;
            float thick = r > 120f ? 3.5f : 0.9f;
            float lines = Mathf.Abs(dx) < thick || Mathf.Abs(dy) < thick ? 0.92f : 0f;
            if (r < 10f) lines = 0f; // leave the centre open for the red dot
            return Mathf.Max(outside, Mathf.Max(shade, lines));
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
            TMP_FontAsset existing = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(assetPath);
            if (existing != null)
            {
                AddSymbolFallback(existing);
                return;
            }

            TMP_FontAsset fontAsset = TMP_FontAsset.CreateFontAsset(font, 72, 8, GlyphRenderMode.SDFAA, 1024, 1024, AtlasPopulationMode.Dynamic);
            fontAsset.name = "RussoOne SDF";
            AssetDatabase.CreateAsset(fontAsset, assetPath);
            fontAsset.atlasTextures[0].name = "RussoOne SDF Atlas";
            AssetDatabase.AddObjectToAsset(fontAsset.atlasTextures[0], fontAsset);
            fontAsset.material.name = "RussoOne SDF Material";
            AssetDatabase.AddObjectToAsset(fontAsset.material, fontAsset);
            // Pre-bake common characters so the first frame doesn't rasterise glyphs.
            fontAsset.TryAddCharacters(" ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789:/!?.,-–—+()%…'\"#&•<>[]");
            AddSymbolFallback(fontAsset);
            EditorUtility.SetDirty(fontAsset);
            AssetDatabase.SaveAssets();
        }

        /// <summary>Russo One has no symbols such as the infinity sign; fall back to TMP's Liberation Sans for them.</summary>
        private static void AddSymbolFallback(TMP_FontAsset fontAsset)
        {
            var fallback = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/TextMesh Pro/Resources/Fonts & Materials/LiberationSans SDF - Fallback.asset");
            if (fallback == null) return;
            if (fontAsset.fallbackFontAssetTable == null) fontAsset.fallbackFontAssetTable = new System.Collections.Generic.List<TMP_FontAsset>();
            if (fontAsset.fallbackFontAssetTable.Contains(fallback)) return;
            fontAsset.fallbackFontAssetTable.Add(fallback);
            EditorUtility.SetDirty(fontAsset);
        }
    }
}
