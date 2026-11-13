using System.IO;
using UnityEditor;
using UnityEngine;
using WarehouseDuel.Weapons;

namespace WarehouseDuel.EditorTools.UI
{
    /// <summary>
    /// Procedural profile banners (640×160): camo and colour themes plus battlefield scenes — crossed rifles,
    /// ammo belt, airstrike, sniper scope, tank column, dog tags, barbed wire, skull squad. Weapon shapes come
    /// from the generated weapon silhouettes so they match the game's guns.
    /// </summary>
    internal static class BannerArt
    {
        public const int Width = 640, Height = 160;

        public static readonly string[] Names =
        {
            "Steel", "Hazard", "Woodland", "Desert", "Sunset", "Arctic", "Inferno", "Neon",
            "Crossfire", "Ammo Belt", "Airstrike", "Sniper", "Tank Column", "Dog Tags", "Barbed Wire", "Skull Squad",
        };

        private static Texture2D rifle;

        /// <summary>Loads readable copies of the weapon silhouettes (call before rendering banners).</summary>
        public static void Prepare(WeaponCatalog catalog)
        {
            rifle = Readable(catalog != null ? catalog.Get(0)?.icon : null);
        }

        private static Texture2D Readable(Sprite sprite)
        {
            if (sprite == null) return null;
            string path = AssetDatabase.GetAssetPath(sprite.texture);
            if (string.IsNullOrEmpty(path) || !File.Exists(path)) return null;
            var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            texture.LoadImage(File.ReadAllBytes(path));
            texture.wrapMode = TextureWrapMode.Clamp;
            return texture;
        }

        public static Color Pixel(int style, int x, int y)
        {
            float u = x / (float)(Width - 1), v = y / (float)(Height - 1);
            Vector2 p = new Vector2(x, y);
            switch (style)
            {
                case 0: // Steel
                {
                    Color c = Color.Lerp(Hex(0x1E2633), Hex(0x3A4658), u * 0.7f + v * 0.3f);
                    c *= 0.93f + 0.07f * Hash(0, y);
                    float sheen = Mathf.Exp(-Mathf.Pow((u - v * 0.35f - 0.55f) * 7f, 2f));
                    return Color.Lerp(c, Hex(0x7F93AE), sheen * 0.35f);
                }
                case 1: // Hazard
                {
                    bool yellow = Mathf.Repeat((x + y) / 44f, 1f) < 0.5f;
                    return (yellow ? Hex(0xF5B21E) : Hex(0x17181B)) * (0.85f + 0.15f * Noise(x * 0.05f, y * 0.05f));
                }
                case 2: return Camo(x, y, 7, Hex(0x2F3E22), Hex(0x4C6232), Hex(0x6E7A44), Hex(0x3B2D20));
                case 3: return Camo(x, y, 11, Hex(0xB89A6A), Hex(0xD2B98A), Hex(0x8C7150), Hex(0xE3D2AA));
                case 4: // Sunset
                {
                    Color sky = Sky(v, Hex(0x2B1640), Hex(0xD9406A), Hex(0xFF9A3C));
                    Vector2 q = new Vector2((u - 0.78f) * 4f, v - 0.42f);
                    bool cut = v < 0.42f && Mathf.Repeat(v * 22f, 1f) < 0.35f;
                    if (q.magnitude < 0.36f && !cut) return Color.Lerp(Hex(0xFFE27A), Hex(0xFF7A3C), Mathf.InverseLerp(0.78f, 0.06f, v));
                    float glow = Mathf.Exp(-q.magnitude * 3.2f) * 0.35f;
                    return sky + new Color(glow, glow * 0.6f, glow * 0.2f, 0f);
                }
                case 5: return Camo(x, y, 9, Hex(0xE8EEF3), Hex(0xBFCCD8), Hex(0x8FA3B6), Hex(0xF8FBFD));
                case 6: // Inferno
                {
                    float n = Noise(x * 0.018f, y * 0.045f - u * 2f) * 0.6f + Noise(x * 0.05f, y * 0.11f) * 0.4f;
                    float heat = Mathf.Clamp01((1f - v) * 0.9f + (n - 0.5f) * 0.9f);
                    return heat < 0.5f ? Color.Lerp(Hex(0x120606), Hex(0x8A1A10), heat * 2f) : Color.Lerp(Hex(0x8A1A10), Hex(0xFF8A2A), (heat - 0.5f) * 2f);
                }
                case 7: // Neon
                {
                    Color c = Color.Lerp(Hex(0x0B0618), Hex(0x2A0F45), v);
                    float gx = Mathf.Abs(Mathf.Repeat(x / 40f, 1f) - 0.5f) * 40f, gy = Mathf.Abs(Mathf.Repeat(y / 40f, 1f) - 0.5f) * 40f;
                    float line = Mathf.Max(Mathf.Clamp01(1.5f - (20f - gx)), Mathf.Clamp01(1.5f - (20f - gy)));
                    c = Color.Lerp(c, Hex(0x2EE6FF), line * 0.55f * (1f - v * 0.6f));
                    return Color.Lerp(c, Hex(0xFF3FA4), Mathf.Exp(-Mathf.Pow((u - v * 0.8f - 0.45f) * 5f, 2f)) * 0.45f);
                }
                case 8: return Crossfire(p, u, v);
                case 9: return AmmoBelt(p, u, v);
                case 10: return Airstrike(p, u, v);
                case 11: return Sniper(p, u, v);
                case 12: return TankColumn(p, u, v);
                case 13: return DogTags(p, u, v);
                case 14: return BarbedWire(p, u, v);
                default: return SkullSquad(p, u, v);
            }
        }

        // ------------------------------------------------------------------ battlefield scenes

        private static Color Crossfire(Vector2 p, float u, float v)
        {
            Color c = Camo((int)p.x, (int)p.y, 3, Hex(0x1C2215), Hex(0x2A321E), Hex(0x353C25), Hex(0x15180F));
            c = Color.Lerp(c, Hex(0x3A1A0A), Mathf.Exp(-new Vector2((u - 0.74f) * 4f, v - 0.5f).sqrMagnitude * 3f) * 0.7f);
            Vector2 centre = new Vector2(Width * 0.74f, Height * 0.5f);
            float a = Weapon(rifle, p, centre, 26f, 300f);
            float b = Weapon(rifle, p, centre, -26f, 300f, flip: true);
            float glow = Mathf.Max(Weapon(rifle, p, centre, 26f, 312f), Weapon(rifle, p, centre, -26f, 312f, flip: true));
            c = Color.Lerp(c, Hex(0xFF8A2A), glow * 0.6f);
            c = Color.Lerp(c, Hex(0x0C0C0C), Mathf.Max(a, b));
            return Stencil(c, p, u);
        }

        private static Color AmmoBelt(Vector2 p, float u, float v)
        {
            Color c = Color.Lerp(Hex(0x1A0D08), Hex(0x4A2414), v * 0.6f + 0.2f * Noise(p.x * 0.02f, p.y * 0.05f));
            // Belt runs diagonally; local coords along/across it.
            Vector2 dir = new Vector2(1f, 0.28f).normalized;
            Vector2 n = new Vector2(-dir.y, dir.x);
            Vector2 o = p - new Vector2(0f, Height * 0.32f);
            float along = Vector2.Dot(o, dir), across = Vector2.Dot(o, n);
            float cell = Mathf.Repeat(along, 26f) - 13f;
            if (Mathf.Abs(across) < 36f)
            {
                float t = across / 36f;                                     // -1 (base) .. 1 (tip)
                float half = t > 0.45f ? Mathf.Lerp(8f, 2f, (t - 0.45f) / 0.55f) : 8f;
                if (Mathf.Abs(cell) < half)
                {
                    float shade = 0.75f + 0.25f * Mathf.Cos(cell / half * 1.5f);
                    Color metal = t > 0.45f ? Hex(0xC87A3E) : t < -0.75f ? Hex(0xA9873E) : Hex(0xE2B44E);
                    return metal * shade;
                }
                if (Mathf.Abs(t) < 0.32f) return Hex(0x2B2A27) * (0.8f + 0.2f * Mathf.Abs(Mathf.Sin(cell)));   // links
            }
            return Stencil(c * (0.85f + 0.15f * Hash((int)p.x / 3, (int)p.y / 3)), p, u);
        }

        private static Color Airstrike(Vector2 p, float u, float v)
        {
            Color c = Sky(v, Hex(0x3A1020), Hex(0xC2431F), Hex(0xFFB04A));
            float boom = Mathf.Exp(-new Vector2((u - 0.3f) * 5f, (v - 0.18f) * 2.2f).sqrMagnitude);
            c += new Color(1f, 0.55f, 0.2f) * boom * 0.8f;
            float smoke = Noise(p.x * 0.02f, p.y * 0.04f) * Mathf.Clamp01(0.7f - v) * 1.2f;
            c = Color.Lerp(c, Hex(0x2A1A16), Mathf.Clamp01(smoke - 0.25f));
            float ground = 0.16f + 0.05f * Noise(p.x * 0.03f, 1.5f) + (Hash((int)p.x / 18, 4) > 0.7f ? 0.06f : 0f);
            if (v < ground) return Hex(0x0E0907);
            for (int j = 0; j < 3; j++)
            {
                Vector2 jet = new Vector2(Width * (0.52f + j * 0.15f), Height * (0.5f + j * 0.12f));
                Vector2 q = Rotate(p - jet, -16f);
                float body = SdBox(q, Vector2.zero, new Vector2(20f, 2.4f));
                float wing = SdTriangle(q, new Vector2(4f, 0f), new Vector2(-10f, 13f), new Vector2(-10f, -13f));
                float tail = SdTriangle(q, new Vector2(-14f, 0f), new Vector2(-21f, 6f), new Vector2(-21f, -6f));
                if (Mathf.Min(body, Mathf.Min(wing, tail)) < 0f) return Hex(0x140C0C);
                if (q.x < -20f && Mathf.Abs(q.y) < 1.5f + (-20f - q.x) * 0.03f) c = Color.Lerp(c, Color.white, 0.45f * Mathf.Exp((q.x + 20f) / 160f));
            }
            return c;
        }

        private static Color Sniper(Vector2 p, float u, float v)
        {
            Color c = Color.Lerp(Hex(0x050505), Hex(0x161010), Noise(p.x * 0.03f, p.y * 0.03f));
            Vector2 centre = new Vector2(Width * 0.74f, Height * 0.5f);
            float r = (p - centre).magnitude;
            const float radius = 70f;
            if (r < radius)
            {
                Color view = Color.Lerp(Hex(0x7A1E14), Hex(0xE2683A), Mathf.Clamp01(0.5f + (p.y - centre.y) / 120f));
                view *= 1f - Mathf.Pow(r / radius, 3f) * 0.7f;
                Vector2 d = p - centre;
                bool line = (Mathf.Abs(d.x) < 1.1f || Mathf.Abs(d.y) < 1.1f) && r > 6f;
                bool thick = (Mathf.Abs(d.x) < 3f && Mathf.Abs(d.y) > 44f) || (Mathf.Abs(d.y) < 3f && Mathf.Abs(d.x) > 44f);
                bool dot = (Mathf.Abs(d.x) < 1.6f && Mathf.Abs(Mathf.Repeat(d.y + 5f, 10f) - 5f) < 1.6f) || (Mathf.Abs(d.y) < 1.6f && Mathf.Abs(Mathf.Repeat(d.x + 5f, 10f) - 5f) < 1.6f);
                if (line || thick || (dot && r < 40f)) return Hex(0x080808);
                if (r < 2.2f) return Hex(0xFF3020);
                return view;
            }
            if (r < radius + 6f) return Hex(0x2A2A2A) * (1f - (r - radius) / 12f);
            return Stencil(c, p, u);
        }

        private static Color TankColumn(Vector2 p, float u, float v)
        {
            Color c = Sky(v, Hex(0x5A3A24), Hex(0xC98A4E), Hex(0xF2C98A));
            c = Color.Lerp(c, Hex(0xFFE7B0), Mathf.Exp(-new Vector2((u - 0.2f) * 6f, (v - 0.55f) * 3f).sqrMagnitude) * 0.7f);
            float dune = 0.28f + 0.05f * Mathf.Sin(u * 9f) + 0.03f * Noise(p.x * 0.02f, 0.5f);
            if (v < dune) return Color.Lerp(Hex(0x3A2414), Hex(0x6A4426), v / dune);
            for (int j = 0; j < 3; j++)
            {
                float scale = 1f - j * 0.22f;
                Vector2 o = new Vector2(Width * (0.48f + j * 0.18f), Height * (dune + 0.005f + 0.0f)) ;
                Vector2 q = (p - o) / scale;
                float hull = SdBox(q, new Vector2(0f, 9f), new Vector2(38f, 7f));
                float tracks = SdBox(q, new Vector2(0f, 3f), new Vector2(42f, 4f));
                float turret = SdBox(q, new Vector2(-4f, 21f), new Vector2(16f, 6f));
                float barrel = SdBox(q, new Vector2(-38f, 23f), new Vector2(24f, 1.6f));
                if (Mathf.Min(Mathf.Min(hull, tracks), Mathf.Min(turret, barrel)) < 0f) return Hex(0x24170E) * (0.8f + 0.2f * scale);
            }
            float haze = Noise(p.x * 0.01f, p.y * 0.02f) * 0.25f;
            return c * (1f - haze * 0.4f);
        }

        private static Color DogTags(Vector2 p, float u, float v)
        {
            Color c = Color.Lerp(Hex(0x1B1E22), Hex(0x2E333A), Noise(p.x * 0.004f, p.y * 0.2f) * 0.6f + u * 0.4f);
            c *= 0.94f + 0.06f * Hash(0, (int)p.y);
            float chain = Mathf.Abs(p.y - (Height * 0.86f - Mathf.Pow((p.x - Width * 0.68f) / 120f, 2f) * 40f));
            if (chain < 3f && Mathf.Repeat(p.x, 9f) < 6f) return Hex(0xB8BEC6) * (0.7f + 0.3f * (1f - chain / 3f));
            for (int t = 0; t < 2; t++)
            {
                Vector2 centre = new Vector2(Width * (0.62f + t * 0.13f), Height * (0.48f - t * 0.06f));
                Vector2 q = Rotate(p - centre, t == 0 ? -12f : 9f);
                float tag = SdBox(q, Vector2.zero, new Vector2(42f, 26f)) - 10f;
                float hole = (q - new Vector2(-40f, 0f)).magnitude - 4f;
                if (tag < 0f && hole > 0f)
                {
                    float shade = 0.72f + 0.28f * Mathf.Clamp01((q.y + 30f) / 60f);
                    bool emboss = Mathf.Abs(Mathf.Repeat(q.y + 3f, 11f) - 5.5f) < 1.6f && q.x > -26f && q.x < 30f - Hash((int)(q.y / 11f) + t * 7, 3) * 26f && Mathf.Abs(q.y) < 22f;
                    return Hex(0xCDD3DA) * (emboss ? shade * 0.72f : shade) * (0.96f + 0.04f * Hash((int)q.x, (int)q.y));
                }
                if (tag < 2f && tag >= 0f) return Hex(0x6B7078);
            }
            return Stencil(c, p, u);
        }

        private static Color BarbedWire(Vector2 p, float u, float v)
        {
            Color c = Sky(v, Hex(0xFF7A24), Hex(0x8C1E0E), Hex(0x1E0A08));     // fire glow from below
            c = Color.Lerp(c, Hex(0x2A0E08), Mathf.Clamp01(Noise(p.x * 0.015f, p.y * 0.03f) * 1.4f - 0.4f) * v);
            if (v < 0.12f) return Hex(0x100605);
            for (int s = 0; s < 2; s++)
            {
                float y = Height * 0.42f + Mathf.Sin(p.x * 0.09f + s * Mathf.PI) * 9f + Mathf.Sin(p.x * 0.013f) * 14f;
                if (Mathf.Abs(p.y - y) < 1.8f) return Hex(0x0A0706);
            }
            float baseY = Height * 0.42f + Mathf.Sin(p.x * 0.013f) * 14f;
            float barb = Mathf.Repeat(p.x, 46f) - 23f;
            Vector2 b = new Vector2(barb, p.y - baseY);
            if (b.magnitude < 12f && (Mathf.Abs(b.x - b.y) < 1.6f || Mathf.Abs(b.x + b.y) < 1.6f)) return Hex(0x0A0706);
            float post = Mathf.Repeat(p.x - 80f, 210f);
            if (post < 7f && p.y < Height * 0.7f) return Hex(0x0E0907);
            return c;
        }

        private static Color SkullSquad(Vector2 p, float u, float v)
        {
            Color c = Color.Lerp(Hex(0x0E0F0A), Hex(0x262B1A), Noise(p.x * 0.02f, p.y * 0.02f) * 0.6f + v * 0.3f);
            Vector2 centre = new Vector2(Width * 0.73f, Height * 0.5f);
            float rifles = Mathf.Max(Weapon(rifle, p, centre, 32f, 260f), Weapon(rifle, p, centre, -32f, 260f, flip: true));
            c = Color.Lerp(c, Hex(0x0A0A08), rifles);
            Vector2 q = p - centre;
            float cranium = (q - new Vector2(0f, 10f)).magnitude - 34f;
            float jaw = SdBox(q, new Vector2(0f, -24f), new Vector2(20f, 14f)) - 4f;
            float skull = Mathf.Min(cranium, jaw);
            float eyes = Mathf.Min((q - new Vector2(-13f, 6f)).magnitude - 9f, (q - new Vector2(13f, 6f)).magnitude - 9f);
            float nose = SdTriangle(q, new Vector2(0f, -4f), new Vector2(-5f, -14f), new Vector2(5f, -14f));
            bool teeth = q.y < -22f && q.y > -34f && Mathf.Abs(Mathf.Repeat(q.x + 20f, 8f) - 4f) < 0.9f;
            if (skull < 0f && eyes > 0f && nose > 0f && !teeth) return Hex(0xE8E2CF) * (0.82f + 0.18f * Mathf.Clamp01((q.y + 40f) / 80f));
            if (skull < 2.5f && skull >= 0f) return Hex(0x050505);
            return Stencil(c, p, u);
        }

        // ------------------------------------------------------------------ helpers

        /// <summary>Faint diagonal stencil lines on the left so the name stays readable but the banner isn't flat.</summary>
        private static Color Stencil(Color c, Vector2 p, float u) =>
            u < 0.45f && Mathf.Repeat((p.x - p.y) / 9f, 1f) < 0.12f ? c * 1.08f : c;

        /// <summary>Coverage (0..1) of a weapon silhouette centred at <paramref name="centre"/>, rotated, <paramref name="length"/> px long.</summary>
        private static float Weapon(Texture2D icon, Vector2 p, Vector2 centre, float degrees, float length, bool flip = false)
        {
            if (icon == null) return 0f;
            Vector2 q = Rotate(p - centre, -degrees);
            if (flip) q.x = -q.x;
            float aspect = icon.height / (float)icon.width;
            float tu = q.x / length + 0.5f, tv = q.y / (length * aspect) + 0.5f;
            if (tu < 0f || tu > 1f || tv < 0f || tv > 1f) return 0f;
            return icon.GetPixelBilinear(tu, tv).a;
        }

        private static Color Sky(float v, Color bottom, Color middle, Color top) =>
            v < 0.5f ? Color.Lerp(bottom, middle, v * 2f) : Color.Lerp(middle, top, (v - 0.5f) * 2f);

        private static Color Camo(int x, int y, int seed, Color a, Color b, Color c, Color d)
        {
            const int block = 8;
            int bx = x / block, by = y / block;
            float n = Noise(bx * 0.22f + seed, by * 0.22f) * 0.65f + Noise(bx * 0.6f, by * 0.6f + seed) * 0.35f;
            Color col = n < 0.36f ? a : n < 0.5f ? b : n < 0.64f ? c : d;
            return col * (0.95f + 0.05f * Hash(bx, by));
        }

        private static Vector2 Rotate(Vector2 p, float degrees)
        {
            float r = degrees * Mathf.Deg2Rad, cs = Mathf.Cos(r), sn = Mathf.Sin(r);
            return new Vector2(p.x * cs - p.y * sn, p.x * sn + p.y * cs);
        }

        private static float SdBox(Vector2 p, Vector2 c, Vector2 half)
        {
            Vector2 d = new Vector2(Mathf.Abs(p.x - c.x), Mathf.Abs(p.y - c.y)) - half;
            return new Vector2(Mathf.Max(d.x, 0f), Mathf.Max(d.y, 0f)).magnitude + Mathf.Min(Mathf.Max(d.x, d.y), 0f);
        }

        private static float SdTriangle(Vector2 p, Vector2 a, Vector2 b, Vector2 c)
        {
            float Cross(Vector2 o, Vector2 e, Vector2 f) => (e.x - o.x) * (f.y - o.y) - (e.y - o.y) * (f.x - o.x);
            float c1 = Cross(a, b, p), c2 = Cross(b, c, p), c3 = Cross(c, a, p);
            bool inside = (c1 >= 0f && c2 >= 0f && c3 >= 0f) || (c1 <= 0f && c2 <= 0f && c3 <= 0f);
            return inside ? -1f : 1f;
        }

        public static float Hash(int x, int y)
        {
            unchecked
            {
                uint h = (uint)(x * 374761393 + y * 668265263);
                h = (h ^ (h >> 13)) * 1274126177u;
                return ((h ^ (h >> 16)) & 0xFFFF) / 65535f;
            }
        }

        public static float Noise(float x, float y)
        {
            int ix = Mathf.FloorToInt(x), iy = Mathf.FloorToInt(y);
            float fx = x - ix, fy = y - iy;
            fx = fx * fx * (3f - 2f * fx);
            fy = fy * fy * (3f - 2f * fy);
            float a = Mathf.Lerp(Hash(ix, iy), Hash(ix + 1, iy), fx);
            float b = Mathf.Lerp(Hash(ix, iy + 1), Hash(ix + 1, iy + 1), fx);
            return Mathf.Lerp(a, b, fy);
        }

        public static Color Hex(int rgb) => new Color(((rgb >> 16) & 0xFF) / 255f, ((rgb >> 8) & 0xFF) / 255f, (rgb & 0xFF) / 255f, 1f);
    }
}
