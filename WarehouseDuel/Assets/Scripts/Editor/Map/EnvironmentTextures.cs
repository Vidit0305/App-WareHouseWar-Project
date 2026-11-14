using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace WarehouseWar.EditorTools.Map
{
    /// <summary>
    /// Procedural, tileable surface textures for the environment (concrete slabs, corrugated metal, container
    /// ribs, wood planks, hazard stripes, painted metal...). Mostly light, near-grey detail maps that the
    /// material's base colour tints, so one texture serves several colours. Saved as PNG and imported with mipmaps
    /// and mobile compression.
    /// </summary>
    internal static class EnvironmentTextures
    {
        public const string Folder = "Assets/Art/Textures/Environment";

        public enum Kind
        {
            ConcreteFloor, Concrete, Corrugated, Steel, Hazard, Container, Wood, Cardboard, Barrel, PaintedMetal, Grating, WornPaint,
            // Outdoor maps (appended).
            Sand, Sandstone, Plaster, Sandbag, Rock, Grass, Dirt, Bark, Leaves, Snow, Ice,
        }

        private static readonly Dictionary<Kind, Texture2D> Cache = new Dictionary<Kind, Texture2D>();

        public static void ClearCache() => Cache.Clear();

        /// <summary>Large ground surfaces get 512² so their detail survives the bigger tiling.</summary>
        private static bool IsLarge(Kind kind) => kind == Kind.ConcreteFloor || kind == Kind.Sand || kind == Kind.Grass || kind == Kind.Snow;

        public static Texture2D Get(Kind kind)
        {
            if (Cache.TryGetValue(kind, out Texture2D cached) && cached != null) return cached;
            int size = IsLarge(kind) ? 512 : 256;
            string path = $"{Folder}/T_{kind}.png";
            FolderStructure.EnsureFolder(Folder);

            var texture = new Texture2D(size, size, TextureFormat.RGB24, false);
            var pixels = new Color[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++) pixels[y * size + x] = Pixel(kind, x, y, size);
            }
            texture.SetPixels(pixels);
            texture.Apply();
            File.WriteAllBytes(path, texture.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(texture);

            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
            if (AssetImporter.GetAtPath(path) is TextureImporter importer)
            {
                importer.textureType = TextureImporterType.Default;
                importer.wrapMode = TextureWrapMode.Repeat;
                importer.filterMode = FilterMode.Trilinear;
                importer.anisoLevel = 2;
                importer.mipmapEnabled = true;
                importer.sRGBTexture = true;
                importer.maxTextureSize = size;
                importer.textureCompression = TextureImporterCompression.Compressed;
                importer.SetPlatformTextureSettings(new TextureImporterPlatformSettings
                {
                    name = "Android",
                    overridden = true,
                    maxTextureSize = size,
                    format = TextureImporterFormat.ASTC_6x6,
                    compressionQuality = 50,
                });
                importer.SaveAndReimport();
            }
            Texture2D result = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            Cache[kind] = result;
            return result;
        }

        // ------------------------------------------------------------------ patterns

        private static Color Pixel(Kind kind, int x, int y, int size)
        {
            float u = x / (float)size, v = y / (float)size;
            switch (kind)
            {
                case Kind.ConcreteFloor: return ConcreteFloor(x, y, size);
                case Kind.Concrete:
                {
                    float g = 0.86f + 0.07f * Fbm(u * 8f, v * 8f, 8, 4) + 0.05f * Fbm(u * 2f, v * 2f, 2, 2) - 0.04f;
                    if (Hash(x * 7, y * 13) > 0.985f) g -= 0.18f;                       // pores
                    float streak = Mathf.Clamp01(Fbm(u * 24f, v * 1.5f, 24, 3) - 0.62f) * 0.5f; // water stains
                    return Warm(g - streak);
                }
                case Kind.Corrugated:
                {
                    float rib = Profile(u * 8f);                                      // 8 ribs per tile (0.25 m)
                    float g = 0.72f + 0.26f * rib + 0.05f * Fbm(u * 6f, v * 6f, 6, 3);
                    float rust = Mathf.Clamp01(Fbm(u * 10f, v * 2f, 10, 3) * 1.6f - 0.95f) * Mathf.Lerp(0.6f, 1f, v);
                    Color c = new Color(g, g, g);
                    return Color.Lerp(c, new Color(0.55f, 0.32f, 0.18f) * g, rust);
                }
                case Kind.Steel:
                {
                    float brushed = 0.82f + 0.08f * Fbm(u * 1.5f, v * 48f, 2, 48) + 0.05f * Fbm(u * 6f, v * 6f, 6, 3);
                    float scratch = Scratches(x, y, size, 7, 26);
                    float rust = Mathf.Clamp01(Fbm(u * 5f, v * 5f, 5, 4) * 1.5f - 1.05f);
                    Color c = new Color(brushed + scratch * 0.12f, brushed + scratch * 0.12f, brushed + scratch * 0.13f);
                    return Color.Lerp(c, new Color(0.5f, 0.3f, 0.18f), rust * 0.7f);
                }
                case Kind.Hazard:
                {
                    bool yellow = Mathf.Repeat((x + y) / (size * 0.5f), 1f) < 0.5f;
                    Color c = yellow ? new Color(0.98f, 0.74f, 0.1f) : new Color(0.09f, 0.09f, 0.1f);
                    float wear = Mathf.Clamp01(Fbm(u * 12f, v * 12f, 12, 4) * 1.8f - 1.1f);
                    c = Color.Lerp(c, new Color(0.55f, 0.53f, 0.5f), wear);
                    return c * (0.9f + 0.1f * Fbm(u * 30f, v * 30f, 30, 2));
                }
                case Kind.Container:
                {
                    float rib = Profile(u * 4f);                                      // 0.5 m corrugation
                    float g = 0.68f + 0.32f * rib;
                    g *= 0.94f + 0.06f * Fbm(u * 8f, v * 8f, 8, 3);
                    float scratch = Scratches(x, y, size, 11, 18);
                    float rust = Mathf.Clamp01(Fbm(u * 6f, v * 6f, 6, 4) * 1.7f - 1.12f);
                    float drip = Mathf.Clamp01(Fbm(u * 18f, v * 1.2f, 18, 2) * 1.6f - 1.05f) * v;
                    Color c = new Color(g + scratch * 0.2f, g + scratch * 0.2f, g + scratch * 0.2f);
                    c = Color.Lerp(c, new Color(0.45f, 0.26f, 0.15f), Mathf.Max(rust, drip * 0.8f));
                    return c;
                }
                case Kind.Wood:
                {
                    const int planks = 4;
                    int plank = y * planks / size;
                    float inPlank = Mathf.Repeat(v * planks, 1f);
                    float tone = 0.82f + 0.12f * Hash(plank, 3);
                    float grain = 0.88f + 0.12f * Fbm(u * 3f + plank * 7.1f, v * 40f, 3, 40);
                    float rings = 0.94f + 0.06f * Mathf.Sin((u * 26f + Fbm(u * 4f, v * 8f, 4, 8) * 6f) * Mathf.PI);
                    float g = tone * grain * rings;
                    if (inPlank < 0.03f || inPlank > 0.97f) g *= 0.45f;               // gaps
                    float nail = NailAt(x, y, size, planks);
                    Color c = new Color(g * 1.04f, g * 0.95f, g * 0.84f);
                    return Color.Lerp(c, new Color(0.25f, 0.24f, 0.24f), nail);
                }
                case Kind.Cardboard:
                {
                    float g = 0.9f + 0.05f * Mathf.Sin(u * size * 0.8f) + 0.05f * Fbm(u * 10f, v * 10f, 10, 3);
                    bool tape = Mathf.Abs(v - 0.5f) < 0.07f;
                    Color c = new Color(g, g * 0.97f, g * 0.9f);
                    return tape ? new Color(0.96f, 0.9f, 0.76f) * (0.95f + 0.05f * Fbm(u * 40f, v * 3f, 40, 3)) : c;
                }
                case Kind.Barrel:
                {
                    float g = 0.85f + 0.06f * Fbm(u * 8f, v * 4f, 8, 4);
                    foreach (float ring in new[] { 0.3f, 0.66f })
                    {
                        float d = Mathf.Abs(v - ring);
                        if (d < 0.025f) g *= 0.7f + 0.5f * (d / 0.025f);
                    }
                    float rim = Mathf.Min(v, 1f - v);
                    float rust = Mathf.Clamp01(Fbm(u * 10f, v * 6f, 10, 6) * 1.4f - 0.8f) * Mathf.Clamp01(1f - rim * 6f);
                    float scratch = Scratches(x, y, size, 5, 14);
                    Color c = new Color(g + scratch * 0.15f, g + scratch * 0.15f, g + scratch * 0.15f);
                    return Color.Lerp(c, new Color(0.42f, 0.25f, 0.15f), rust);
                }
                case Kind.PaintedMetal:
                {
                    float g = 0.86f + 0.05f * Fbm(u * 6f, v * 6f, 6, 3);
                    float seamX = Mathf.Min(Mathf.Repeat(u * 2f, 1f), 1f - Mathf.Repeat(u * 2f, 1f));
                    float seamY = Mathf.Min(Mathf.Repeat(v * 2f, 1f), 1f - Mathf.Repeat(v * 2f, 1f));
                    if (Mathf.Min(seamX, seamY) < 0.012f) g *= 0.55f;                 // panel lines
                    Vector2 cell = new Vector2(Mathf.Repeat(u * 2f, 1f), Mathf.Repeat(v * 2f, 1f));
                    foreach (Vector2 r in new[] { new Vector2(0.06f, 0.06f), new Vector2(0.94f, 0.06f), new Vector2(0.06f, 0.94f), new Vector2(0.94f, 0.94f) })
                    {
                        float d = (cell - r).magnitude;
                        if (d < 0.025f) g = Mathf.Lerp(1.05f, 0.6f, d / 0.025f);       // rivets
                    }
                    float chip = Mathf.Clamp01(Fbm(u * 14f, v * 14f, 14, 4) * 2f - 1.3f);
                    Color c = new Color(g, g, g);
                    return Color.Lerp(c, new Color(0.38f, 0.37f, 0.36f), chip);
                }
                case Kind.Grating:
                {
                    float gx = Mathf.Repeat(u * 16f, 1f), gy = Mathf.Repeat(v * 16f, 1f);
                    bool bar = gx < 0.22f || gy < 0.22f;
                    float g = bar ? 0.85f + 0.08f * Fbm(u * 8f, v * 8f, 8, 3) : 0.12f;
                    return new Color(g, g, g);
                }
                case Kind.Sand:
                {
                    float g = 0.86f + 0.08f * (Fbm(u * 6f, v * 6f, 6, 6) - 0.5f) + 0.06f * (Fbm(u * 32f, v * 32f, 32, 32) - 0.5f);
                    float ripple = Mathf.Sin((v * 9f + Fbm(u * 3f, v * 3f, 3, 3) * 1.5f) * Mathf.PI * 2f); // wind ripples
                    g += ripple * 0.035f;
                    float grain = Hash(x * 3 + 1, y * 7 + 2);
                    if (grain > 0.96f) g += 0.07f;
                    else if (grain < 0.03f) g -= 0.08f;
                    return new Color(g * 1.03f, g, g * 0.94f);
                }
                case Kind.Sandstone:
                {
                    // 0.5 m courses of 1 m blocks (2 m tile), staggered, with eroded faces and mortar joints.
                    const int rows = 4;
                    int row = y * rows / size;
                    float inRow = Mathf.Repeat(v * rows, 1f);
                    float along = Mathf.Repeat(u * 2f + (row % 2) * 0.5f, 2f);
                    float inBlock = Mathf.Repeat(along, 1f);
                    int block = Mathf.FloorToInt(along) + row * 2;
                    float g = 0.8f + 0.12f * Hash(block, 11) + 0.08f * (Fbm(u * 8f, v * 8f, 8, 8) - 0.5f);
                    g -= Mathf.Clamp01(Fbm(u * 5f, v * 5f, 5, 5) * 1.7f - 1.05f) * 0.18f;
                    float edge = Mathf.Min(Mathf.Min(inRow, 1f - inRow) / rows, Mathf.Min(inBlock, 1f - inBlock) / 2f);
                    if (edge < 0.006f) g *= 0.62f;
                    else if (edge < 0.014f) g *= 0.9f;
                    return new Color(g * 1.03f, g, g * 0.92f);
                }
                case Kind.Plaster:
                {
                    float g = 0.88f + 0.06f * (Fbm(u * 6f, v * 6f, 6, 6) - 0.5f) + 0.04f * (Fbm(u * 24f, v * 24f, 24, 24) - 0.5f);
                    float patch = Fbm(u * 4f + 7.3f, v * 4f, 4, 4);
                    if (patch > 0.62f)
                    {
                        // Plaster has fallen off: mud bricks show through.
                        int row = y * 10 / size;
                        float inRow = Mathf.Repeat(v * 10f, 1f);
                        float along = Mathf.Repeat(u * 5f + (row % 2) * 0.5f, 5f);
                        float inBrick = Mathf.Repeat(along, 1f);
                        float brick = 0.66f + 0.1f * Hash(row, Mathf.FloorToInt(along));
                        if (Mathf.Min(inRow, 1f - inRow) < 0.08f || Mathf.Min(inBrick, 1f - inBrick) < 0.04f) brick = 0.82f;
                        g = Mathf.Lerp(g, brick, Mathf.SmoothStep(0f, 1f, (patch - 0.62f) * 12f));
                    }
                    g -= Crack(x, y, size) * 0.2f;
                    return new Color(g * 1.02f, g * 0.98f, g * 0.92f);
                }
                case Kind.Sandbag:
                {
                    // 1.2 m tile: four 0.3 m rows of 0.6 m bags, staggered, each bulging in the middle.
                    const int rows = 4;
                    int row = y * rows / size;
                    float fy = Mathf.Repeat(v * rows, 1f);
                    float along = Mathf.Repeat(u * 2f + (row % 2) * 0.5f, 2f);
                    float fx = Mathf.Repeat(along, 1f);
                    float bulge = Mathf.Sqrt(Mathf.Clamp01(Mathf.Sin(fx * Mathf.PI) * Mathf.Sin(fy * Mathf.PI)));
                    float weave = 0.95f + 0.05f * Mathf.Sin(u * 96f * Mathf.PI * 2f) * Mathf.Sin(v * 96f * Mathf.PI * 2f);
                    int bag = Mathf.FloorToInt(along) + row * 2;
                    float g = (0.5f + 0.45f * bulge) * weave * (0.92f + 0.1f * Hash(bag, 5)) + 0.05f * (Fbm(u * 8f, v * 8f, 8, 8) - 0.5f);
                    return new Color(g * 1.02f, g, g * 0.9f);
                }
                case Kind.Rock:
                {
                    float g = 0.78f + 0.16f * (Fbm(u * 4f, v * 4f, 4, 4) - 0.5f) + 0.08f * (Fbm(u * 16f, v * 16f, 16, 16) - 0.5f);
                    g += Mathf.Sin((v * 5f + Fbm(u * 3f, v * 3f, 3, 3) * 0.8f) * Mathf.PI * 2f) * 0.045f;   // strata
                    float facet = Mathf.Floor(Fbm(u * 3f + 2.1f, v * 3f, 3, 3) * 6f) / 6f;                 // flat facets
                    g += (facet - 0.5f) * 0.12f;
                    g -= Crack(x, y, size) * 0.28f;
                    if (Hash(x * 5, y * 3) > 0.985f) g += 0.08f;
                    return new Color(g, g, g * 0.98f);
                }
                case Kind.Grass:
                {
                    float clumps = Fbm(u * 6f, v * 6f, 6, 6);
                    float blades = Fbm(u * 64f, v * 16f, 64, 16);
                    float g = 0.74f + 0.31f * (blades - 0.5f) + 0.14f * (clumps - 0.5f);
                    float dry = Mathf.Clamp01(Fbm(u * 3f + 5f, v * 3f, 3, 3) * 2f - 1.1f);
                    var c = new Color(g * 0.96f, g, g * 0.86f);
                    c = Color.Lerp(c, new Color(g * 1.25f, g * 1.12f, g * 0.7f), dry * 0.7f);          // yellowed patches
                    float speck = Hash(x * 7 + 3, y * 11);
                    if (speck > 0.992f) c = new Color(1f, 0.95f, 0.75f);                                // tiny flowers
                    else if (speck < 0.012f) c *= 0.7f;
                    return c;
                }
                case Kind.Dirt:
                {
                    float g = 0.8f + 0.14f * (Fbm(u * 5f, v * 5f, 5, 5) - 0.5f) + 0.08f * (Fbm(u * 24f, v * 24f, 24, 24) - 0.5f);
                    float pebble = Fbm(u * 40f, v * 40f, 40, 40);
                    if (pebble > 0.72f) g += 0.12f;
                    else if (pebble < 0.25f) g -= 0.06f;
                    g -= Mathf.Clamp01(Fbm(u * 2f, v * 8f, 2, 8) * 2f - 1.2f) * 0.12f;                // ruts
                    return new Color(g * 1.03f, g, g * 0.93f);
                }
                case Kind.Bark:
                {
                    // Vertical fissures (box side faces have V pointing up) with a little moss.
                    float warp = Fbm(u * 4f, v * 2f, 4, 2) * 0.9f;
                    float ridge = Mathf.Abs(Mathf.Sin((u * 7f + warp) * Mathf.PI));
                    float g = 0.55f + 0.38f * Mathf.Pow(ridge, 0.6f) + 0.06f * (Fbm(u * 16f, v * 32f, 16, 32) - 0.5f);
                    float moss = Mathf.Clamp01(Fbm(u * 3f + 9f, v * 3f, 3, 3) * 2f - 1.2f);
                    var c = new Color(g * 1.02f, g, g * 0.95f);
                    return Color.Lerp(c, new Color(0.55f, 0.75f, 0.4f) * g * 1.3f, moss * 0.5f);
                }
                case Kind.Leaves:
                {
                    float big = Fbm(u * 5f, v * 5f, 5, 5);
                    float small = Fbm(u * 28f, v * 28f, 28, 28);
                    float g = 0.62f + 0.3f * small + 0.16f * (big - 0.5f);
                    if (small < 0.35f) g *= 0.72f;                                                      // gaps between clusters
                    if (Hash(x * 13, y * 17) > 0.985f) g += 0.12f;
                    return new Color(g * 0.95f, g, g * 0.85f);
                }
                case Kind.Snow:
                {
                    float g = 0.93f + 0.05f * (Fbm(u * 5f, v * 5f, 5, 5) - 0.5f) + 0.025f * (Fbm(u * 32f, v * 32f, 32, 32) - 0.5f);
                    g += Mathf.Sin((v * 6f + u * 2f + Fbm(u * 2f, v * 2f, 2, 2) * 1.2f) * Mathf.PI * 2f) * 0.02f; // wind drifts
                    var c = new Color(g * 0.97f, g * 0.99f, g);
                    float shade = Mathf.Clamp01(0.5f - Fbm(u * 4f + 3f, v * 4f, 4, 4)) * 0.12f;         // blue hollows
                    c -= new Color(shade, shade * 0.6f, 0f, 0f);
                    if (Hash(x * 3 + 7, y * 5 + 1) > 0.993f) c = Color.white;                            // sparkle
                    return c;
                }
                case Kind.Ice:
                {
                    float g = 0.82f + 0.1f * (Fbm(u * 3f, v * 3f, 3, 3) - 0.5f);
                    g += (Fbm(u * 2f, v * 18f, 2, 18) - 0.5f) * 0.08f;
                    g += Crack(x, y, size) * 0.15f;                                                     // white fractures
                    if (Hash(x * 9, y * 4) > 0.99f) g += 0.08f;                                         // bubbles
                    return new Color(g * 0.95f, g * 0.99f, g);
                }
                default: // WornPaint
                {
                    float scuff = Mathf.Clamp01(Fbm(u * 6f, v * 6f, 6, 4) * 1.6f - 0.85f);
                    float speck = Hash(x * 3, y * 5) > 0.97f ? 0.25f : 0f;
                    float g = 0.97f - scuff * 0.35f - speck;
                    return new Color(g, g, g);
                }
            }
        }

        /// <summary>4 m × 4 m of floor: four 2 m slabs with seams, oil stains, cracks and grain.</summary>
        private static Color ConcreteFloor(int x, int y, int size)
        {
            float u = x / (float)size, v = y / (float)size;
            int slab = (x * 2 / size) + 2 * (y * 2 / size);
            float g = 0.8f + 0.04f * (Hash(slab, 9) - 0.5f);
            g += 0.07f * (Fbm(u * 16f, v * 16f, 16, 4) - 0.5f);
            g += 0.06f * (Fbm(u * 3f, v * 3f, 3, 3) - 0.5f);
            float oil = Mathf.Clamp01(Fbm(u * 4f + 3.3f, v * 4f, 4, 3) * 2.2f - 1.4f);
            g -= oil * 0.22f;
            float crack = Crack(x, y, size);
            g -= crack * 0.3f;

            int sx = x % (size / 2), sy = y % (size / 2);
            int seam = Mathf.Min(Mathf.Min(sx, size / 2 - sx), Mathf.Min(sy, size / 2 - sy));
            if (seam < 2) g *= 0.6f;
            else if (seam < 4) g *= 1.04f;
            return Warm(g);
        }

        // ------------------------------------------------------------------ helpers

        /// <summary>Trapezoid corrugation profile, 0..1 (flat top, slopes, flat valley).</summary>
        private static float Profile(float t)
        {
            float p = Mathf.Repeat(t, 1f);
            float tri = 1f - Mathf.Abs(p * 2f - 1f);
            return Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((tri - 0.25f) * 2f));
        }

        private static float Scratches(int x, int y, int size, int seed, int count)
        {
            float best = 0f;
            for (int i = 0; i < count; i++)
            {
                float ax = Hash(seed, i) * size, ay = Hash(i, seed) * size;
                float angle = Hash(seed + i, 17) * Mathf.PI;
                float length = 10f + Hash(i, seed + 3) * 40f;
                Vector2 dir = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
                Vector2 p = new Vector2(Wrap(x - ax, size), Wrap(y - ay, size));
                float along = Vector2.Dot(p, dir);
                if (along < 0f || along > length) continue;
                float across = Mathf.Abs(p.x * dir.y - p.y * dir.x);
                if (across < 0.8f) best = Mathf.Max(best, 1f - across / 0.8f);
            }
            return best;
        }

        private static float Crack(int x, int y, int size)
        {
            // A few jagged polylines (deterministic random walks).
            float best = 0f;
            for (int c = 0; c < 5; c++)
            {
                float px = Hash(c, 41) * size, py = Hash(41, c) * size;
                float angle = Hash(c, 7) * Mathf.PI * 2f;
                for (int s = 0; s < 14; s++)
                {
                    angle += (Hash(c * 31 + s, 5) - 0.5f) * 1.2f;
                    float nx = px + Mathf.Cos(angle) * 9f, ny = py + Mathf.Sin(angle) * 9f;
                    float d = SegmentDistance(new Vector2(Wrap(x - px, size), Wrap(y - py, size)), Vector2.zero, new Vector2(nx - px, ny - py));
                    if (d < 1f) best = Mathf.Max(best, 1f - d);
                    px = nx;
                    py = ny;
                }
            }
            return best;
        }

        private static float NailAt(int x, int y, int size, int planks)
        {
            int plankHeight = size / planks;
            int cy = (y / plankHeight) * plankHeight + plankHeight / 2;
            float best = 0f;
            foreach (int cx in new[] { 12, size - 12 })
            {
                float d = new Vector2(x - cx, y - cy).magnitude;
                if (d < 2.5f) best = Mathf.Max(best, 1f - d / 2.5f);
            }
            return best;
        }

        private static float SegmentDistance(Vector2 p, Vector2 a, Vector2 b)
        {
            Vector2 ab = b - a;
            float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / Mathf.Max(0.0001f, ab.sqrMagnitude));
            return (p - (a + ab * t)).magnitude;
        }

        private static float Wrap(float d, int size)
        {
            d = Mathf.Repeat(d + size * 0.5f, size) - size * 0.5f;
            return d;
        }

        private static Color Warm(float g) => new Color(g * 1.02f, g, g * 0.95f);

        private static float Hash(int x, int y)
        {
            unchecked
            {
                uint h = (uint)(x * 374761393 + y * 668265263);
                h = (h ^ (h >> 13)) * 1274126177u;
                return ((h ^ (h >> 16)) & 0xFFFF) / 65535f;
            }
        }

        /// <summary>Tileable fractal noise: x/y in lattice units over [0, periodX/periodY).</summary>
        private static float Fbm(float x, float y, int periodX, int periodY)
        {
            float sum = 0f, amp = 0.5f, norm = 0f;
            int px = Mathf.Max(1, periodX), py = Mathf.Max(1, periodY);
            for (int o = 0; o < 4; o++)
            {
                sum += amp * NoiseXY(x, y, px, py);
                norm += amp;
                x *= 2f;
                y *= 2f;
                px *= 2;
                py *= 2;
                amp *= 0.5f;
            }
            return sum / norm;
        }

        private static float NoiseXY(float x, float y, int periodX, int periodY)
        {
            int ix = Mathf.FloorToInt(x), iy = Mathf.FloorToInt(y);
            float fx = x - ix, fy = y - iy;
            fx = fx * fx * (3f - 2f * fx);
            fy = fy * fy * (3f - 2f * fy);
            int x0 = Mod(ix, periodX), x1 = Mod(ix + 1, periodX), y0 = Mod(iy, periodY), y1 = Mod(iy + 1, periodY);
            float a = Mathf.Lerp(Hash(x0, y0), Hash(x1, y0), fx);
            float b = Mathf.Lerp(Hash(x0, y1), Hash(x1, y1), fx);
            return Mathf.Lerp(a, b, fy);
        }

        private static int Mod(int a, int m) => ((a % m) + m) % m;
    }

    /// <summary>
    /// Box meshes whose UVs are measured in metres, so a texture tiles at the same real-world size on every
    /// box no matter how it is scaled (Unity's cube stretches 0..1 UVs over each face). One mesh per size,
    /// stored as sub-assets of a single file ("bank").
    /// The warehouse kit, its scene and the menu backdrops use the default bank; each outdoor map uses its own bank
    /// (<see cref="UseBank"/>), so rebuilding one map never deletes meshes that another scene still references.
    /// </summary>
    internal static class BoxMeshes
    {
        public const string AssetPath = "Assets/Art/Meshes/EnvironmentBoxes.asset";
        private static readonly Dictionary<string, Mesh> Cache = new Dictionary<string, Mesh>();
        private static string bankPath = AssetPath;

        /// <summary>The bank new box meshes are written to.</summary>
        public static string CurrentBank => bankPath;

        /// <summary>Directs <see cref="Get"/> to another bank (null = the default bank).</summary>
        public static void UseBank(string path)
        {
            path = string.IsNullOrEmpty(path) ? AssetPath : path;
            if (path == bankPath) return;
            bankPath = path;
            Cache.Clear();
        }

        /// <summary>Deletes the default mesh bank (call once at the start of a full warehouse rebuild so stale sizes go away).</summary>
        public static void Reset() => ResetBank(AssetPath);

        /// <summary>Switches to <paramref name="path"/> and deletes its contents.</summary>
        public static void ResetBank(string path)
        {
            UseBank(path);
            Cache.Clear();
            if (AssetDatabase.LoadMainAssetAtPath(bankPath) != null) AssetDatabase.DeleteAsset(bankPath);
        }

        public static Mesh Get(Vector3 size)
        {
            Vector3 s = new Vector3(Round(size.x), Round(size.y), Round(size.z));
            string key = $"Box_{s.x:0.00}x{s.y:0.00}x{s.z:0.00}";
            if (Cache.TryGetValue(key, out Mesh cached) && cached != null) return cached;

            foreach (UnityEngine.Object asset in AssetDatabase.LoadAllAssetsAtPath(bankPath))
            {
                if (asset is Mesh existing && existing.name == key)
                {
                    Cache[key] = existing;
                    return existing;
                }
            }

            Mesh mesh = Build(s);
            mesh.name = key;
            FolderStructure.EnsureFolder("Assets/Art/Meshes");
            if (AssetDatabase.LoadMainAssetAtPath(bankPath) == null) AssetDatabase.CreateAsset(mesh, bankPath);
            else AssetDatabase.AddObjectToAsset(mesh, bankPath);
            Cache[key] = mesh;
            return mesh;
        }

        private static float Round(float v) => Mathf.Max(0.01f, Mathf.Round(Mathf.Abs(v) * 100f) / 100f);

        /// <summary>Unit cube (−0.5..0.5) with per-face UVs = local position × size along the face axes.</summary>
        private static Mesh Build(Vector3 size)
        {
            var vertices = new List<Vector3>(24);
            var normals = new List<Vector3>(24);
            var uvs = new List<Vector2>(24);
            var triangles = new List<int>(36);

            void Face(Vector3 normal, Vector3 axisU, Vector3 axisV)
            {
                int start = vertices.Count;
                float su = Mathf.Abs(Vector3.Dot(axisU, size)), sv = Mathf.Abs(Vector3.Dot(axisV, size));
                for (int i = 0; i < 4; i++)
                {
                    float a = i == 0 || i == 3 ? -0.5f : 0.5f;
                    float b = i < 2 ? -0.5f : 0.5f;
                    vertices.Add(normal * 0.5f + axisU * a + axisV * b);
                    normals.Add(normal);
                    uvs.Add(new Vector2((a + 0.5f) * su, (b + 0.5f) * sv));
                }
                triangles.AddRange(new[] { start, start + 2, start + 1, start, start + 3, start + 2 });
            }

            Face(Vector3.forward, Vector3.left, Vector3.up);
            Face(Vector3.back, Vector3.right, Vector3.up);
            Face(Vector3.right, Vector3.forward, Vector3.up);
            Face(Vector3.left, Vector3.back, Vector3.up);
            Face(Vector3.up, Vector3.right, Vector3.forward);
            Face(Vector3.down, Vector3.right, Vector3.back);

            var mesh = new Mesh();
            mesh.SetVertices(vertices);
            mesh.SetNormals(normals);
            mesh.SetUVs(0, uvs);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateBounds();
            mesh.RecalculateTangents();
            return mesh;
        }
    }
}
