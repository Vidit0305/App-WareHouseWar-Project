using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace WarehouseDuel.EditorTools.Map
{
    /// <summary>
    /// Material palette for the environment. Each surface gets a base colour plus a procedural detail texture
    /// (rust, grime, seams...) from <see cref="EnvironmentTextures"/>. Materials are saved in
    /// Assets/Art/Materials/Environment and reused (good SRP batching). URP "Simple Lit" keeps it cheap on mobile.
    /// </summary>
    internal static class EnvironmentPalette
    {
        public const string MaterialFolder = "Assets/Art/Materials/Environment";
        public const string TextureFolder = "Assets/Art/Textures";
        private const string ShaderName = "Universal Render Pipeline/Simple Lit";

        public enum Id
        {
            ConcreteFloor, Concrete, ConcreteDark, MetalWall, MetalWallRib, Roof, Steel, Hazard,
            ContainerRed, ContainerBlue, ContainerGreen, ContainerOrange,
            Wood, WoodDark, Cardboard, BarrelRed, BarrelBlue, RackOrange, RackBlue,
            Machine, MachineDark, Grating, FloorPaint, FloorPaintWhite,
            LampGlow, PanelGreen, PanelRed, SpawnBlue, SpawnRed, Skylight,
        }

        private static readonly Dictionary<Id, Material> Cache = new Dictionary<Id, Material>();

        public static Material Get(Id id)
        {
            if (Cache.TryGetValue(id, out Material cached) && cached != null) return cached;

            string path = $"{MaterialFolder}/M_{id}.mat";
            Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                Shader shader = Shader.Find(ShaderName);
                if (shader == null) throw new System.InvalidOperationException($"Shader '{ShaderName}' not found. Is URP installed?");
                material = new Material(shader) { name = $"M_{id}" };
                FolderStructure.EnsureFolder(MaterialFolder);
                AssetDatabase.CreateAsset(material, path);
            }

            Configure(material, id);
            EditorUtility.SetDirty(material);
            Cache[id] = material;
            return material;
        }

        public static void ClearCache()
        {
            Cache.Clear();
            EnvironmentTextures.ClearCache();
        }

        private static void Configure(Material m, Id id)
        {
            m.enableInstancing = true;
            m.SetTexture("_BaseMap", null);
            m.SetTextureScale("_BaseMap", Vector2.one);
            switch (id)
            {
                case Id.ConcreteFloor: Base(m, Hex(0xB2ADA4)); break;
                case Id.Concrete: Base(m, Hex(0xA19E96)); break;
                case Id.ConcreteDark: Base(m, Hex(0x6A6A68)); break;
                case Id.MetalWall: Base(m, Hex(0x5F8AB0)); break;
                case Id.MetalWallRib: Base(m, Hex(0x4C7092)); break;
                case Id.Roof: Base(m, Hex(0x3A3F47)); break;
                case Id.Steel: Base(m, Hex(0x33383F)); break;
                case Id.Hazard: Base(m, Hex(0xF5B71A)); break;
                case Id.ContainerRed: Base(m, Hex(0xB83A2C)); break;
                case Id.ContainerBlue: Base(m, Hex(0x2F62A8)); break;
                case Id.ContainerGreen: Base(m, Hex(0x3E8A4F)); break;
                case Id.ContainerOrange: Base(m, Hex(0xE07B25)); break;
                case Id.Wood: Base(m, Hex(0xC08A4E)); break;
                case Id.WoodDark: Base(m, Hex(0x7E5530)); break;
                case Id.Cardboard: Base(m, Hex(0xC9A270)); break;
                case Id.BarrelRed: Base(m, Hex(0xC42F24)); break;
                case Id.BarrelBlue: Base(m, Hex(0x2B5BB5)); break;
                case Id.RackOrange: Base(m, Hex(0xEE7417)); break;
                case Id.RackBlue: Base(m, Hex(0x2C4C8C)); break;
                case Id.Machine: Base(m, Hex(0x4C9384)); break;
                case Id.MachineDark: Base(m, Hex(0x2C3236)); break;
                case Id.Grating: Base(m, Hex(0x50565E)); break;
                case Id.FloorPaint: Base(m, Hex(0xE8B21E)); break;
                case Id.FloorPaintWhite: Base(m, Hex(0xE6E4DC)); break;
                case Id.LampGlow: Emissive(m, Hex(0xFFF1D0), 2.2f); break;
                case Id.PanelGreen: Emissive(m, Hex(0x48F07A), 1.6f); break;
                case Id.PanelRed: Emissive(m, Hex(0xFF4A3A), 1.6f); break;
                case Id.SpawnBlue: Emissive(m, Hex(0x3C9BFF), 1.2f); break;
                case Id.SpawnRed: Emissive(m, Hex(0xFF5A3C), 1.2f); break;
                case Id.Skylight: Emissive(m, Hex(0xFFE2B8), 1.6f); break;
            }
            ApplySurface(m, id);
        }

        /// <summary>Detail texture + tiling per surface (box UVs are in metres, see <see cref="BoxMeshes"/>).</summary>
        private static void ApplySurface(Material m, Id id)
        {
            EnvironmentTextures.Kind kind;
            float metresPerTile;
            switch (id)
            {
                case Id.ConcreteFloor: kind = EnvironmentTextures.Kind.ConcreteFloor; metresPerTile = 4f; break;
                case Id.Concrete:
                case Id.ConcreteDark: kind = EnvironmentTextures.Kind.Concrete; metresPerTile = 2f; break;
                case Id.MetalWall:
                case Id.MetalWallRib: kind = EnvironmentTextures.Kind.Corrugated; metresPerTile = 2f; break;
                case Id.Roof: kind = EnvironmentTextures.Kind.Corrugated; metresPerTile = 3f; break;
                case Id.Steel: kind = EnvironmentTextures.Kind.Steel; metresPerTile = 1f; break;
                case Id.Hazard: kind = EnvironmentTextures.Kind.Hazard; metresPerTile = 1f; m.SetColor("_BaseColor", Color.white); break;
                case Id.ContainerRed:
                case Id.ContainerBlue:
                case Id.ContainerGreen:
                case Id.ContainerOrange: kind = EnvironmentTextures.Kind.Container; metresPerTile = 2f; break;
                case Id.Wood:
                case Id.WoodDark: kind = EnvironmentTextures.Kind.Wood; metresPerTile = 1f; break;
                case Id.Cardboard: kind = EnvironmentTextures.Kind.Cardboard; metresPerTile = 1f; break;
                case Id.BarrelRed:
                case Id.BarrelBlue: kind = EnvironmentTextures.Kind.Barrel; metresPerTile = 1f; break;
                case Id.RackOrange:
                case Id.RackBlue:
                case Id.Machine:
                case Id.MachineDark: kind = EnvironmentTextures.Kind.PaintedMetal; metresPerTile = 1f; break;
                case Id.Grating: kind = EnvironmentTextures.Kind.Grating; metresPerTile = 1f; break;
                case Id.FloorPaint:
                case Id.FloorPaintWhite: kind = EnvironmentTextures.Kind.WornPaint; metresPerTile = 2f; break;
                default: return; // emissive surfaces stay flat
            }
            m.SetTexture("_BaseMap", EnvironmentTextures.Get(kind));
            m.SetTextureScale("_BaseMap", Vector2.one / metresPerTile);
        }

        private static void Base(Material m, Color color)
        {
            m.SetColor("_BaseColor", color);
            m.SetFloat("_Smoothness", 0.15f);
            m.DisableKeyword("_EMISSION");
            m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.EmissiveIsBlack;
        }

        private static void Emissive(Material m, Color color, float intensity)
        {
            m.SetColor("_BaseColor", color);
            m.SetFloat("_Smoothness", 0.3f);
            m.EnableKeyword("_EMISSION");
            m.SetColor("_EmissionColor", color * intensity);
            m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
        }

        private static Color Hex(int rgb) =>
            new Color(((rgb >> 16) & 0xFF) / 255f, ((rgb >> 8) & 0xFF) / 255f, (rgb & 0xFF) / 255f, 1f);
    }
}
