using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace WarehouseDuel.EditorTools.Gameplay
{
    /// <summary>
    /// Builds the blocky "voxel" soldier (green camo helmet, blue goggles, black face mask, camo uniform,
    /// skull shoulder patch) used for the in-game player body and the menu/lobby showcase character.
    /// Feet at the origin, facing +Z, ~1.9 m tall. Bones are plain transforms animated procedurally.
    /// </summary>
    internal static class SoldierModelBuilder
    {
        private const string MaterialFolder = "Assets/Art/Materials/Characters";
        private const string TextureFolder = "Assets/Art/Textures/Characters";

        public sealed class Rig
        {
            public Transform Root, Hips, LegL, LegR, Spine, Head, ArmsRig, ShoulderL, ShoulderR, WeaponSocket;
            public readonly List<Renderer> TeamColored = new List<Renderer>();
        }

        private static Material camo, camoDark, skin, mask, vest, boots, goggles, team, patch, eye, strap;

        public static Rig Build(Transform parent, string name = "Soldier")
        {
            LoadMaterials();
            var rig = new Rig();
            rig.Root = Node(parent, name, Vector3.zero);

            rig.Hips = Node(rig.Root, "Hips", new Vector3(0f, 0.92f, 0f));
            Box(rig.Hips, new Vector3(0f, 0.02f, 0f), new Vector3(0.5f, 0.18f, 0.3f), camoDark);           // pelvis
            rig.LegL = Leg(rig.Hips, "LegL", -0.13f);
            rig.LegR = Leg(rig.Hips, "LegR", 0.13f);

            rig.Spine = Node(rig.Root, "Spine", new Vector3(0f, 1.0f, 0f));
            Box(rig.Spine, new Vector3(0f, 0.25f, 0f), new Vector3(0.56f, 0.56f, 0.32f), camo);              // torso
            Box(rig.Spine, new Vector3(0f, 0.2f, 0.17f), new Vector3(0.48f, 0.38f, 0.06f), vest);            // plate carrier
            Box(rig.Spine, new Vector3(-0.13f, 0.08f, 0.21f), new Vector3(0.12f, 0.14f, 0.06f), vest);       // pouches
            Box(rig.Spine, new Vector3(0.13f, 0.08f, 0.21f), new Vector3(0.12f, 0.14f, 0.06f), vest);
            Box(rig.Spine, new Vector3(0f, -0.03f, 0f), new Vector3(0.58f, 0.08f, 0.34f), strap);            // belt
            Box(rig.Spine, new Vector3(0f, 0.27f, -0.2f), new Vector3(0.4f, 0.44f, 0.12f), camoDark);        // backpack

            // Head: skin cube, black lower mask, camo helmet with team band, blue goggles.
            rig.Head = Node(rig.Spine, "Head", new Vector3(0f, 0.5f, 0f));
            Box(rig.Head, new Vector3(0f, 0.17f, 0f), new Vector3(0.38f, 0.38f, 0.38f), skin);
            Box(rig.Head, new Vector3(0f, 0.08f, 0.004f), new Vector3(0.4f, 0.17f, 0.4f), mask);
            Box(rig.Head, new Vector3(-0.08f, 0.22f, 0.192f), new Vector3(0.07f, 0.05f, 0.01f), eye);
            Box(rig.Head, new Vector3(0.08f, 0.22f, 0.192f), new Vector3(0.07f, 0.05f, 0.01f), eye);
            Box(rig.Head, new Vector3(-0.08f, 0.265f, 0.192f), new Vector3(0.09f, 0.02f, 0.01f), mask);      // brows
            Box(rig.Head, new Vector3(0.08f, 0.265f, 0.192f), new Vector3(0.09f, 0.02f, 0.01f), mask);
            Box(rig.Head, new Vector3(0f, 0.38f, -0.01f), new Vector3(0.47f, 0.2f, 0.47f), camo);            // helmet
            Box(rig.Head, new Vector3(0f, 0.29f, 0.02f), new Vector3(0.5f, 0.05f, 0.5f), camoDark);          // brim
            rig.TeamColored.Add(Box(rig.Head, new Vector3(0f, 0.325f, -0.01f), new Vector3(0.48f, 0.035f, 0.48f), team));
            Box(rig.Head, new Vector3(0f, 0.41f, -0.01f), new Vector3(0.485f, 0.05f, 0.485f), strap);        // goggle strap
            Box(rig.Head, new Vector3(0f, 0.42f, 0.235f), new Vector3(0.32f, 0.11f, 0.05f), goggles);        // goggle lens

            // Arms + weapon on one rig so the hands move with the gun (aim pitch, inspection, swaps).
            rig.ArmsRig = Node(rig.Spine, "ArmsRig", new Vector3(0f, 0.42f, 0f));
            rig.ShoulderR = Arm(rig.ArmsRig, "ShoulderR", new Vector3(0.34f, 0.06f, 0f), Quaternion.Euler(-60f, -6f, 0f), rig);
            rig.ShoulderL = Arm(rig.ArmsRig, "ShoulderL", new Vector3(-0.34f, 0.06f, 0f), Quaternion.Euler(-66f, 32f, 0f), rig);
            Transform patchRoot = rig.ShoulderL;
            Box(patchRoot, new Vector3(-0.095f, -0.12f, 0f), new Vector3(0.02f, 0.13f, 0.13f), mask);       // skull patch
            Box(patchRoot, new Vector3(-0.1f, -0.12f, 0f), new Vector3(0.02f, 0.07f, 0.07f), patch);

            rig.WeaponSocket = Node(rig.ArmsRig, "WeaponSocket", new Vector3(0.17f, -0.27f, 0.47f));
            rig.WeaponSocket.localRotation = Quaternion.Euler(0f, -6f, 0f);
            return rig;
        }

        /// <summary>
        /// First-person hand + forearm in weapon space (metres, scaled with the view model). The hand sits at the
        /// origin; the sleeve runs back along -Z towards the camera.
        /// </summary>
        public static GameObject BuildFirstPersonHandPrefab(string path)
        {
            LoadMaterials();
            var root = new GameObject("FP_Hand");
            Box(root.transform, new Vector3(0f, -0.015f, 0f), new Vector3(0.075f, 0.085f, 0.1f), skin);
            Box(root.transform, new Vector3(0f, -0.02f, -0.07f), new Vector3(0.085f, 0.09f, 0.04f), strap);   // cuff
            Box(root.transform, new Vector3(0f, -0.025f, -0.29f), new Vector3(0.09f, 0.095f, 0.42f), camo);   // sleeve
            foreach (Renderer r in root.GetComponentsInChildren<Renderer>()) r.shadowCastingMode = ShadowCastingMode.Off;
            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, path);
            Object.DestroyImmediate(root);
            return prefab;
        }

        private static Transform Leg(Transform hips, string name, float x)
        {
            Transform leg = Node(hips, name, new Vector3(x, 0f, 0f));
            Box(leg, new Vector3(0f, -0.23f, 0f), new Vector3(0.21f, 0.46f, 0.23f), camo);
            Box(leg, new Vector3(0f, -0.4f, 0.115f), new Vector3(0.17f, 0.12f, 0.03f), vest);               // knee pad
            Box(leg, new Vector3(0f, -0.66f, 0f), new Vector3(0.19f, 0.34f, 0.21f), camoDark);
            Box(leg, new Vector3(0f, -0.86f, 0.04f), new Vector3(0.23f, 0.13f, 0.32f), boots);
            return leg;
        }

        private static Transform Arm(Transform parent, string name, Vector3 position, Quaternion rotation, Rig rig)
        {
            Transform shoulder = Node(parent, name, position);
            shoulder.localRotation = rotation;
            rig.TeamColored.Add(Box(shoulder, new Vector3(0f, 0.02f, 0f), new Vector3(0.22f, 0.12f, 0.24f), team)); // shoulder pad
            Box(shoulder, new Vector3(0f, -0.15f, 0f), new Vector3(0.18f, 0.32f, 0.19f), camo);
            Box(shoulder, new Vector3(0f, -0.43f, 0f), new Vector3(0.17f, 0.28f, 0.18f), camo);
            Box(shoulder, new Vector3(0f, -0.6f, 0f), new Vector3(0.185f, 0.06f, 0.195f), strap);          // cuff
            Box(shoulder, new Vector3(0f, -0.69f, 0f), new Vector3(0.16f, 0.13f, 0.16f), skin);            // hand
            return shoulder;
        }

        // ------------------------------------------------------------------ helpers

        private static Transform Node(Transform parent, string name, Vector3 localPosition)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPosition;
            return go.transform;
        }

        private static Renderer Box(Transform parent, Vector3 centre, Vector3 size, Material material)
        {
            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            Object.DestroyImmediate(go.GetComponent<Collider>());
            go.name = material.name.Replace("M_Soldier_", "");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = centre;
            go.transform.localScale = size;
            var renderer = go.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = size.x * size.y * size.z < 0.001f ? ShadowCastingMode.Off : ShadowCastingMode.On;
            return renderer;
        }

        private static void LoadMaterials()
        {
            Texture2D camoTexture = CamoTexture();
            camo = Mat("M_Soldier_Camo", Color.white, camoTexture);
            camoDark = Mat("M_Soldier_CamoDark", new Color(0.72f, 0.75f, 0.7f), camoTexture);
            skin = Mat("M_Soldier_Skin", new Color32(232, 170, 120, 255));
            mask = Mat("M_Soldier_Mask", new Color32(30, 32, 36, 255));
            vest = Mat("M_Soldier_Vest", new Color32(58, 64, 46, 255));
            boots = Mat("M_Soldier_Boots", new Color32(36, 30, 26, 255));
            strap = Mat("M_Soldier_Strap", new Color32(44, 44, 46, 255));
            eye = Mat("M_Soldier_Eye", new Color32(24, 20, 18, 255));
            patch = Mat("M_Soldier_Patch", new Color32(245, 245, 240, 255));
            team = Mat("M_Soldier_Team", Color.white);
            goggles = Mat("M_Soldier_Goggles", new Color32(60, 170, 255, 255), null, emission: 1.4f);
        }

        private static Material Mat(string name, Color color, Texture2D texture = null, float emission = 0f)
        {
            FolderStructure.EnsureFolder(MaterialFolder);
            string path = $"{MaterialFolder}/{name}.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(Shader.Find("Universal Render Pipeline/Simple Lit"));
                AssetDatabase.CreateAsset(material, path);
            }
            material.name = name;
            material.SetColor("_BaseColor", color);
            material.SetTexture("_BaseMap", texture);
            material.SetFloat("_Smoothness", 0.15f);
            if (emission > 0f)
            {
                material.EnableKeyword("_EMISSION");
                material.SetColor("_EmissionColor", color * emission);
            }
            material.enableInstancing = true;
            EditorUtility.SetDirty(material);
            return material;
        }

        /// <summary>16×16 pixel camo (four greens/browns in blobs), point-filtered for the voxel look.</summary>
        private static Texture2D CamoTexture()
        {
            FolderStructure.EnsureFolder(TextureFolder);
            string path = $"{TextureFolder}/T_SoldierCamo.png";
            var existing = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            if (existing != null) return existing;

            Color32[] palette = { new Color32(96, 128, 52, 255), new Color32(124, 156, 66, 255), new Color32(70, 92, 44, 255), new Color32(110, 92, 58, 255) };
            const int size = 16;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float a = Mathf.PerlinNoise(x * 0.32f + 3.3f, y * 0.32f + 1.7f);
                    float b = Mathf.PerlinNoise(x * 0.55f + 9.1f, y * 0.55f + 4.2f);
                    int index = a > 0.62f ? 2 : a < 0.36f ? 1 : b > 0.66f ? 3 : 0;
                    texture.SetPixel(x, y, palette[index]);
                }
            }
            texture.Apply();
            File.WriteAllBytes(path, texture.EncodeToPNG());
            Object.DestroyImmediate(texture);
            AssetDatabase.ImportAsset(path);
            if (AssetImporter.GetAtPath(path) is TextureImporter importer)
            {
                importer.filterMode = FilterMode.Point;
                importer.mipmapEnabled = false;
                importer.wrapMode = TextureWrapMode.Repeat;
                importer.textureCompression = TextureImporterCompression.Uncompressed;
                importer.SaveAndReimport();
            }
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }
    }
}
