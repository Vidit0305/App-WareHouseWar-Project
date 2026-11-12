using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using WarehouseDuel.EditorTools.Audio;
using WarehouseDuel.Weapons;

namespace WarehouseDuel.EditorTools.Weapons
{
    /// <summary>
    /// Builds the four low-poly weapon models (prefabs), their WeaponDefinition assets with balanced stats,
    /// and the WeaponCatalog. Models are built at real-world scale along +Z with the grip at the origin and a
    /// "Muzzle" child at the barrel tip; the player scales them down for the first-person view.
    /// Re-running keeps asset GUIDs, so existing references stay valid.
    /// </summary>
    internal static class WeaponAssetBuilder
    {
        public const string ModelFolder = "Assets/Prefabs/Weapons";
        public const string DataFolder = "Assets/ScriptableObjects/Weapons";
        public const string CatalogPath = DataFolder + "/WeaponCatalog.asset";
        private const string MaterialFolder = "Assets/Art/Materials/Weapons";

        private static Material metal, polymer, wood, glow, tan, olive, accent;

        public static WeaponCatalog BuildAll(SetupReport report, Texture2D[] icons = null)
        {
            FolderStructure.EnsureFolder(ModelFolder);
            FolderStructure.EnsureFolder(DataFolder);
            FolderStructure.EnsureFolder(MaterialFolder);

            metal = Mat("M_Gun_Metal", new Color32(46, 50, 56, 255), 0.35f);
            polymer = Mat("M_Gun_Polymer", new Color32(64, 70, 79, 255), 0.2f);
            wood = Mat("M_Gun_Wood", new Color32(150, 96, 52, 255), 0.15f);
            tan = Mat("M_Gun_Tan", new Color32(196, 164, 110, 255), 0.15f);
            olive = Mat("M_Gun_Olive", new Color32(110, 126, 72, 255), 0.15f);
            accent = Mat("M_Gun_Accent", new Color32(245, 166, 35, 255), 0.25f);
            glow = Mat("M_Gun_Sight", new Color32(255, 60, 50, 255), 0.4f, emissive: true);

            GameObject rifle = Save("Weapon_Rifle", Rifle());
            GameObject smg = Save("Weapon_SMG", Smg());
            GameObject shotgun = Save("Weapon_Shotgun", Shotgun());
            GameObject pistol = Save("Weapon_Pistol", Pistol());

            WeaponDefinition rifleDef = Definition("Weapon_AssaultRifle", d =>
            {
                d.displayName = "Assault Rifle";
                d.description = "Reliable all-rounder. Full-auto, medium recoil.";
                d.damage = 20f; d.headshotMultiplier = 1.6f; d.pellets = 1;
                d.range = 90f; d.falloffStart = 25f; d.falloffEnd = 60f; d.minDamageMultiplier = 0.7f;
                d.fireMode = FireMode.Automatic; d.roundsPerMinute = 600f; d.hipSpread = 2.2f; d.aimSpread = 0.5f;
                d.magazineSize = 30; d.reserveAmmo = 120; d.reloadSeconds = 2.0f;
                d.recoilPitch = 1.1f; d.recoilYaw = 0.35f; d.moveSpeedMultiplier = 1f; d.aimFieldOfView = 52f; d.equipSeconds = 0.4f;
                d.viewKick = 0.035f; d.muzzleFlashScale = 1f;
                d.viewModelPrefab = d.worldModelPrefab = rifle;
                d.fireSound = AudioGenerator.Load(AudioGenerator.SfxFolder, "Fire_Rifle");
                d.reloadSound = AudioGenerator.Load(AudioGenerator.SfxFolder, "Reload_Rifle");
            });

            WeaponDefinition smgDef = Definition("Weapon_SMG", d =>
            {
                d.displayName = "SMG";
                d.description = "Very fast fire rate, low recoil. Best up close.";
                d.damage = 14f; d.headshotMultiplier = 1.5f; d.pellets = 1;
                d.range = 60f; d.falloffStart = 12f; d.falloffEnd = 30f; d.minDamageMultiplier = 0.5f;
                d.fireMode = FireMode.Automatic; d.roundsPerMinute = 900f; d.hipSpread = 3.0f; d.aimSpread = 1.4f;
                d.magazineSize = 32; d.reserveAmmo = 160; d.reloadSeconds = 1.7f;
                d.recoilPitch = 0.55f; d.recoilYaw = 0.3f; d.moveSpeedMultiplier = 1.06f; d.aimFieldOfView = 58f; d.equipSeconds = 0.3f;
                d.viewKick = 0.025f; d.muzzleFlashScale = 0.8f;
                d.viewModelPrefab = d.worldModelPrefab = smg;
                d.fireSound = AudioGenerator.Load(AudioGenerator.SfxFolder, "Fire_SMG");
                d.reloadSound = AudioGenerator.Load(AudioGenerator.SfxFolder, "Reload_SMG");
            });

            WeaponDefinition shotgunDef = Definition("Weapon_Shotgun", d =>
            {
                d.displayName = "Shotgun";
                d.description = "Eight pellets. Devastating up close, slow to fire, limited ammo.";
                d.damage = 12f; d.headshotMultiplier = 1.25f; d.pellets = 8;
                d.range = 35f; d.falloffStart = 6f; d.falloffEnd = 18f; d.minDamageMultiplier = 0.25f;
                d.fireMode = FireMode.SemiAutomatic; d.roundsPerMinute = 70f; d.hipSpread = 6f; d.aimSpread = 4.5f;
                d.magazineSize = 6; d.reserveAmmo = 24; d.reloadSeconds = 2.4f;
                d.recoilPitch = 6f; d.recoilYaw = 1.2f; d.moveSpeedMultiplier = 0.95f; d.aimFieldOfView = 60f; d.equipSeconds = 0.45f;
                d.viewKick = 0.1f; d.muzzleFlashScale = 1.5f;
                d.viewModelPrefab = d.worldModelPrefab = shotgun;
                d.fireSound = AudioGenerator.Load(AudioGenerator.SfxFolder, "Fire_Shotgun");
                d.reloadSound = AudioGenerator.Load(AudioGenerator.SfxFolder, "Reload_Shotgun");
            });

            WeaponDefinition pistolDef = Definition("Weapon_Pistol", d =>
            {
                d.displayName = "Pistol";
                d.description = "Accurate semi-auto sidearm. Small magazine, quick reload.";
                d.damage = 26f; d.headshotMultiplier = 1.75f; d.pellets = 1;
                d.range = 70f; d.falloffStart = 20f; d.falloffEnd = 45f; d.minDamageMultiplier = 0.7f;
                d.fireMode = FireMode.SemiAutomatic; d.roundsPerMinute = 300f; d.hipSpread = 1.2f; d.aimSpread = 0.3f;
                d.magazineSize = 12; d.reserveAmmo = 60; d.reloadSeconds = 1.4f;
                d.recoilPitch = 2.2f; d.recoilYaw = 0.5f; d.moveSpeedMultiplier = 1.08f; d.aimFieldOfView = 56f; d.equipSeconds = 0.25f;
                d.viewKick = 0.05f; d.muzzleFlashScale = 0.8f;
                d.viewModelPrefab = d.worldModelPrefab = pistol;
                d.fireSound = AudioGenerator.Load(AudioGenerator.SfxFolder, "Fire_Pistol");
                d.reloadSound = AudioGenerator.Load(AudioGenerator.SfxFolder, "Reload_Pistol");
            });

            WeaponDefinition[] definitions = { rifleDef, smgDef, shotgunDef, pistolDef };
            AudioClip dry = AudioGenerator.Load(AudioGenerator.SfxFolder, "DryFire");
            for (int i = 0; i < definitions.Length; i++)
            {
                definitions[i].dryFireSound = dry;
                if (icons != null && i < icons.Length && icons[i] != null)
                {
                    definitions[i].icon = AssetDatabase.LoadAssetAtPath<Sprite>(AssetDatabase.GetAssetPath(icons[i]));
                }
                EditorUtility.SetDirty(definitions[i]);
            }

            var catalog = AssetDatabase.LoadAssetAtPath<WeaponCatalog>(CatalogPath);
            if (catalog == null)
            {
                catalog = ScriptableObject.CreateInstance<WeaponCatalog>();
                AssetDatabase.CreateAsset(catalog, CatalogPath);
            }
            catalog.SetWeapons(definitions);
            EditorUtility.SetDirty(catalog);
            AssetDatabase.SaveAssets();

            report.Pass("Weapons: 4 models, 4 WeaponDefinitions and the WeaponCatalog generated.");
            return catalog;
        }

        // ------------------------------------------------------------------ models (metres, +Z forward, grip at origin)

        private static GameObject Rifle()
        {
            var root = new GameObject("Weapon_Rifle");
            Transform t = root.transform;
            Part(t, new Vector3(0f, 0.045f, 0.06f), new Vector3(0.06f, 0.085f, 0.34f), polymer);              // receiver
            Part(t, new Vector3(0f, 0.04f, 0.33f), new Vector3(0.058f, 0.07f, 0.22f), tan);                   // handguard
            Cyl(t, new Vector3(0f, 0.05f, 0.52f), 0.022f, 0.2f, metal);                                       // barrel
            Part(t, new Vector3(0f, 0.05f, 0.635f), new Vector3(0.034f, 0.034f, 0.05f), metal);               // flash hider
            Part(t, new Vector3(0f, 0.03f, -0.22f), new Vector3(0.05f, 0.085f, 0.22f), tan);                  // stock
            Part(t, new Vector3(0f, 0.005f, -0.33f), new Vector3(0.052f, 0.11f, 0.03f), polymer);             // butt pad
            Part(t, new Vector3(0f, -0.06f, 0.1f), new Vector3(0.035f, 0.13f, 0.07f), metal, Quaternion.Euler(-12f, 0f, 0f)); // magazine
            Part(t, new Vector3(0f, -0.045f, -0.04f), new Vector3(0.032f, 0.09f, 0.04f), polymer, Quaternion.Euler(18f, 0f, 0f)); // grip
            Part(t, new Vector3(0f, 0.105f, 0.08f), new Vector3(0.04f, 0.035f, 0.1f), metal);                 // optic body
            Part(t, new Vector3(0f, 0.115f, 0.08f), new Vector3(0.012f, 0.012f, 0.005f), glow);               // red dot
            Part(t, new Vector3(0.031f, 0.06f, 0.33f), new Vector3(0.004f, 0.02f, 0.16f), accent);            // accent stripe
            Muzzle(t, new Vector3(0f, 0.05f, 0.67f));
            return root;
        }

        private static GameObject Smg()
        {
            var root = new GameObject("Weapon_SMG");
            Transform t = root.transform;
            Part(t, new Vector3(0f, 0.045f, 0.06f), new Vector3(0.055f, 0.08f, 0.26f), olive);
            Cyl(t, new Vector3(0f, 0.05f, 0.27f), 0.028f, 0.16f, metal);
            Part(t, new Vector3(0f, -0.09f, 0.11f), new Vector3(0.028f, 0.18f, 0.04f), metal);                // long straight magazine
            Part(t, new Vector3(0f, -0.04f, -0.03f), new Vector3(0.03f, 0.085f, 0.04f), polymer, Quaternion.Euler(15f, 0f, 0f));
            Part(t, new Vector3(0f, -0.03f, 0.2f), new Vector3(0.026f, 0.07f, 0.03f), polymer);               // fore grip
            Part(t, new Vector3(0f, 0.04f, -0.15f), new Vector3(0.012f, 0.02f, 0.16f), metal);                // wire stock rail
            Part(t, new Vector3(0f, 0.0f, -0.23f), new Vector3(0.04f, 0.08f, 0.015f), metal);                 // stock plate
            Part(t, new Vector3(0f, 0.095f, 0.03f), new Vector3(0.015f, 0.02f, 0.03f), metal);                // rear sight
            Part(t, new Vector3(0f, 0.095f, 0.03f), new Vector3(0.006f, 0.006f, 0.032f), glow);
            Part(t, new Vector3(0.0285f, 0.05f, 0.06f), new Vector3(0.004f, 0.018f, 0.2f), accent);
            Muzzle(t, new Vector3(0f, 0.05f, 0.355f));
            return root;
        }

        private static GameObject Shotgun()
        {
            var root = new GameObject("Weapon_Shotgun");
            Transform t = root.transform;
            Part(t, new Vector3(0f, 0.045f, 0.06f), new Vector3(0.058f, 0.08f, 0.26f), metal);                // receiver
            Cyl(t, new Vector3(0f, 0.06f, 0.43f), 0.03f, 0.5f, metal);                                        // barrel
            Cyl(t, new Vector3(0f, 0.025f, 0.38f), 0.026f, 0.4f, polymer);                                    // magazine tube
            Part(t, new Vector3(0f, 0.02f, 0.33f), new Vector3(0.05f, 0.05f, 0.16f), wood);                   // pump
            Part(t, new Vector3(0f, 0.0f, -0.24f), new Vector3(0.05f, 0.1f, 0.26f), wood, Quaternion.Euler(-6f, 0f, 0f)); // stock
            Part(t, new Vector3(0f, -0.04f, -0.04f), new Vector3(0.034f, 0.08f, 0.05f), wood, Quaternion.Euler(25f, 0f, 0f));
            Part(t, new Vector3(0f, 0.08f, 0.66f), new Vector3(0.008f, 0.012f, 0.008f), glow);                // bead sight
            Part(t, new Vector3(0.0295f, 0.05f, 0.06f), new Vector3(0.004f, 0.025f, 0.18f), accent);
            Muzzle(t, new Vector3(0f, 0.06f, 0.69f));
            return root;
        }

        private static GameObject Pistol()
        {
            var root = new GameObject("Weapon_Pistol");
            Transform t = root.transform;
            Part(t, new Vector3(0f, 0.065f, 0.07f), new Vector3(0.03f, 0.035f, 0.19f), metal);                // slide
            Part(t, new Vector3(0f, 0.035f, 0.065f), new Vector3(0.028f, 0.03f, 0.16f), polymer);             // frame
            Part(t, new Vector3(0f, -0.02f, -0.005f), new Vector3(0.03f, 0.11f, 0.045f), polymer, Quaternion.Euler(14f, 0f, 0f)); // grip
            Part(t, new Vector3(0f, 0.015f, 0.04f), new Vector3(0.008f, 0.025f, 0.035f), metal);              // trigger guard
            Part(t, new Vector3(0f, 0.086f, 0.15f), new Vector3(0.006f, 0.008f, 0.006f), glow);               // front sight dot
            Part(t, new Vector3(0f, 0.086f, -0.01f), new Vector3(0.018f, 0.008f, 0.006f), metal);             // rear sight
            Part(t, new Vector3(0.0155f, 0.065f, 0.07f), new Vector3(0.003f, 0.012f, 0.12f), accent);
            Muzzle(t, new Vector3(0f, 0.065f, 0.17f));
            return root;
        }

        // ------------------------------------------------------------------ helpers

        private static void Part(Transform parent, Vector3 centre, Vector3 size, Material material, Quaternion? rotation = null)
        {
            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            Setup(go, parent, centre, size, material, rotation);
        }

        private static void Cyl(Transform parent, Vector3 centre, float diameter, float length, Material material)
        {
            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            Setup(go, parent, centre, new Vector3(diameter, length / 2f, diameter), material, Quaternion.Euler(90f, 0f, 0f));
        }

        private static void Setup(GameObject go, Transform parent, Vector3 centre, Vector3 scale, Material material, Quaternion? rotation)
        {
            Object.DestroyImmediate(go.GetComponent<Collider>());
            go.name = "Part";
            go.transform.SetParent(parent, false);
            go.transform.localPosition = centre;
            go.transform.localRotation = rotation ?? Quaternion.identity;
            go.transform.localScale = scale;
            var renderer = go.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off; // small parts: shadows cost more than they add
        }

        private static void Muzzle(Transform parent, Vector3 position)
        {
            var muzzle = new GameObject("Muzzle");
            muzzle.transform.SetParent(parent, false);
            muzzle.transform.localPosition = position;
        }

        private static GameObject Save(string name, GameObject root)
        {
            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, $"{ModelFolder}/{name}.prefab");
            Object.DestroyImmediate(root);
            return prefab;
        }

        private static WeaponDefinition Definition(string assetName, System.Action<WeaponDefinition> configure)
        {
            string path = $"{DataFolder}/{assetName}.asset";
            var definition = AssetDatabase.LoadAssetAtPath<WeaponDefinition>(path);
            if (definition == null)
            {
                definition = ScriptableObject.CreateInstance<WeaponDefinition>();
                AssetDatabase.CreateAsset(definition, path);
            }
            configure(definition);
            EditorUtility.SetDirty(definition);
            return definition;
        }

        private static Material Mat(string name, Color color, float smoothness, bool emissive = false)
        {
            string path = $"{MaterialFolder}/{name}.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(Shader.Find("Universal Render Pipeline/Simple Lit"));
                AssetDatabase.CreateAsset(material, path);
            }
            material.SetColor("_BaseColor", color);
            material.SetFloat("_Smoothness", smoothness);
            if (emissive)
            {
                material.EnableKeyword("_EMISSION");
                material.SetColor("_EmissionColor", color * 2f);
                material.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            }
            material.enableInstancing = true;
            EditorUtility.SetDirty(material);
            return material;
        }
    }
}
