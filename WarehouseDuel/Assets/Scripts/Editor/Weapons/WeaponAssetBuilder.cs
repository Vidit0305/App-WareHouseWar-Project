using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using WarehouseWar.EditorTools.Audio;
using WarehouseWar.Weapons;

namespace WarehouseWar.EditorTools.Weapons
{
    /// <summary>
    /// Builds the eight low-poly weapon models (prefabs), their WeaponDefinition assets with balanced stats,
    /// and the WeaponCatalog. Models are built at real-world scale along +Z with the grip at the origin and a
    /// "Muzzle" child at the barrel tip; the player scales them down for the first-person view.
    /// The knife has no "Muzzle" (so nothing ever spawns a muzzle flash on it) and the RPG has a child
    /// group named "Warhead" (the rocket) that the first-person rig hides while the tube is empty.
    /// Catalog order is the network id and must never change: 0 Rifle, 1 SMG, 2 Shotgun, 3 Pistol,
    /// 4 Sniper, 5 Machine Gun, 6 Knife, 7 RPG.
    /// Re-running keeps asset GUIDs, so existing references stay valid.
    /// </summary>
    internal static class WeaponAssetBuilder
    {
        public const string ModelFolder = "Assets/Prefabs/Weapons";
        public const string DataFolder = "Assets/ScriptableObjects/Weapons";
        public const string CatalogPath = DataFolder + "/WeaponCatalog.asset";
        private const string MaterialFolder = "Assets/Art/Materials/Weapons";

        private static Material metal, polymer, wood, glow, tan, olive, accent, blade, lens, dark;

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
            blade = Mat("M_Gun_Blade", new Color32(176, 182, 190, 255), 0.75f);
            lens = Mat("M_Gun_Lens", new Color32(70, 150, 235, 255), 0.9f, emissive: true);
            dark = Mat("M_Gun_Dark", new Color32(28, 30, 33, 255), 0.25f);

            GameObject rifle = Save("Weapon_Rifle", Rifle());
            GameObject smg = Save("Weapon_SMG", Smg());
            GameObject shotgun = Save("Weapon_Shotgun", Shotgun());
            GameObject pistol = Save("Weapon_Pistol", Pistol());
            GameObject sniper = Save("Weapon_Sniper", Sniper());
            GameObject machineGun = Save("Weapon_MachineGun", MachineGun());
            GameObject knife = Save("Weapon_Knife", Knife());
            GameObject rpg = Save("Weapon_RPG", Rpg());

            WeaponDefinition rifleDef = Definition("Weapon_AssaultRifle", d =>
            {
                d.displayName = "Assault Rifle";
                d.description = "Reliable all-rounder. Full-auto, medium recoil.";
                d.damage = 20f; d.headshotMultiplier = 1.6f; d.pellets = 1;
                d.range = 250f; d.falloffStart = 45f; d.falloffEnd = 110f; d.minDamageMultiplier = 0.75f;
                d.fireMode = FireMode.Automatic; d.roundsPerMinute = 600f; d.hipSpread = 1.2f; d.aimSpread = 0.25f;
                d.magazineSize = 30; d.reserveAmmo = 120; d.reloadSeconds = 2.0f;
                d.recoilPitch = 1.1f; d.recoilYaw = 0.35f; d.moveSpeedMultiplier = 1f; d.aimFieldOfView = 52f; d.equipSeconds = 0.4f;
                d.viewKick = 0.035f; d.muzzleFlashScale = 1f; d.muzzleFlashColor = new Color(1f, 0.8f, 0.4f);
                Hitscan(d);
                d.viewModelPrefab = d.worldModelPrefab = rifle;
                d.fireSound = AudioGenerator.Load(AudioGenerator.SfxFolder, "Fire_Rifle");
                d.reloadSound = AudioGenerator.Load(AudioGenerator.SfxFolder, "Reload_Rifle");
            });

            WeaponDefinition smgDef = Definition("Weapon_SMG", d =>
            {
                d.displayName = "SMG";
                d.description = "Very fast fire rate, low recoil. Best up close.";
                d.damage = 14f; d.headshotMultiplier = 1.5f; d.pellets = 1;
                d.range = 160f; d.falloffStart = 25f; d.falloffEnd = 70f; d.minDamageMultiplier = 0.6f;
                d.fireMode = FireMode.Automatic; d.roundsPerMinute = 900f; d.hipSpread = 1.8f; d.aimSpread = 0.7f;
                d.magazineSize = 32; d.reserveAmmo = 160; d.reloadSeconds = 1.7f;
                d.recoilPitch = 0.55f; d.recoilYaw = 0.3f; d.moveSpeedMultiplier = 1.06f; d.aimFieldOfView = 58f; d.equipSeconds = 0.3f;
                d.viewKick = 0.025f; d.muzzleFlashScale = 0.8f; d.muzzleFlashColor = new Color(1f, 0.85f, 0.5f);
                Hitscan(d);
                d.viewModelPrefab = d.worldModelPrefab = smg;
                d.viewModelOffset = new Vector3(-0.01f, 0.025f, 0.03f);
                d.fireSound = AudioGenerator.Load(AudioGenerator.SfxFolder, "Fire_SMG");
                d.reloadSound = AudioGenerator.Load(AudioGenerator.SfxFolder, "Reload_SMG");
            });

            WeaponDefinition shotgunDef = Definition("Weapon_Shotgun", d =>
            {
                d.displayName = "Shotgun";
                d.description = "Eight pellets. Devastating up close, slow to fire, limited ammo.";
                d.damage = 12f; d.headshotMultiplier = 1.25f; d.pellets = 8;
                d.range = 60f; d.falloffStart = 8f; d.falloffEnd = 28f; d.minDamageMultiplier = 0.3f;
                d.fireMode = FireMode.SemiAutomatic; d.roundsPerMinute = 70f; d.hipSpread = 6f; d.aimSpread = 4.5f;
                d.magazineSize = 6; d.reserveAmmo = 24; d.reloadSeconds = 2.4f;
                d.recoilPitch = 6f; d.recoilYaw = 1.2f; d.moveSpeedMultiplier = 0.95f; d.aimFieldOfView = 60f; d.equipSeconds = 0.45f;
                d.viewKick = 0.1f; d.muzzleFlashScale = 1.5f; d.muzzleFlashColor = new Color(1f, 0.65f, 0.3f);
                Hitscan(d);
                d.viewModelPrefab = d.worldModelPrefab = shotgun;
                d.fireSound = AudioGenerator.Load(AudioGenerator.SfxFolder, "Fire_Shotgun");
                d.reloadSound = AudioGenerator.Load(AudioGenerator.SfxFolder, "Reload_Shotgun");
            });

            WeaponDefinition pistolDef = Definition("Weapon_Pistol", d =>
            {
                d.displayName = "Pistol";
                d.description = "Accurate semi-auto sidearm. Small magazine, quick reload.";
                d.damage = 26f; d.headshotMultiplier = 1.75f; d.pellets = 1;
                d.range = 160f; d.falloffStart = 35f; d.falloffEnd = 80f; d.minDamageMultiplier = 0.7f;
                d.fireMode = FireMode.SemiAutomatic; d.roundsPerMinute = 300f; d.hipSpread = 0.8f; d.aimSpread = 0.15f;
                d.magazineSize = 12; d.reserveAmmo = 60; d.reloadSeconds = 1.4f;
                d.recoilPitch = 2.2f; d.recoilYaw = 0.5f; d.moveSpeedMultiplier = 1.08f; d.aimFieldOfView = 56f; d.equipSeconds = 0.25f;
                d.viewKick = 0.05f; d.muzzleFlashScale = 0.8f; d.muzzleFlashColor = new Color(1f, 0.85f, 0.55f);
                Hitscan(d);
                d.viewModelPrefab = d.worldModelPrefab = pistol;
                d.viewModelOffset = new Vector3(-0.04f, 0.07f, 0.09f);
                d.fireSound = AudioGenerator.Load(AudioGenerator.SfxFolder, "Fire_Pistol");
                d.reloadSound = AudioGenerator.Load(AudioGenerator.SfxFolder, "Reload_Pistol");
            });

            WeaponDefinition sniperDef = Definition("Weapon_Sniper", d =>
            {
                d.displayName = "Sniper Rifle";
                d.description = "Bolt-action, 4x scope. One shot to the head kills; wild from the hip, slow to move.";
                d.damage = 95f; d.headshotMultiplier = 2f; d.pellets = 1;
                d.range = 400f; d.falloffStart = 400f; d.falloffEnd = 400f; d.minDamageMultiplier = 1f;
                d.fireMode = FireMode.SemiAutomatic; d.roundsPerMinute = 45f; d.hipSpread = 8f; d.aimSpread = 0f;
                d.magazineSize = 5; d.reserveAmmo = 20; d.reloadSeconds = 3.2f;
                d.recoilPitch = 5f; d.recoilYaw = 0.8f; d.moveSpeedMultiplier = 0.9f; d.aimFieldOfView = 18f; d.equipSeconds = 0.7f;
                d.viewKick = 0.1f; d.muzzleFlashScale = 1.7f; d.muzzleFlashColor = new Color(1f, 0.92f, 0.7f);
                Hitscan(d);
                d.hasScope = true;
                d.viewModelPrefab = d.worldModelPrefab = sniper;
                d.viewModelOffset = new Vector3(0f, 0f, -0.08f);
                d.fireSound = AudioGenerator.Load(AudioGenerator.SfxFolder, "Fire_Sniper");
                d.reloadSound = AudioGenerator.Load(AudioGenerator.SfxFolder, "Reload_Sniper");
            });

            WeaponDefinition machineGunDef = Definition("Weapon_MachineGun", d =>
            {
                d.displayName = "Machine Gun";
                d.description = "Belt-fed LMG with a 100-round box. Suppress everything; heavy, kicks hard, long reload.";
                d.damage = 17f; d.headshotMultiplier = 1.5f; d.pellets = 1;
                d.range = 250f; d.falloffStart = 45f; d.falloffEnd = 110f; d.minDamageMultiplier = 0.7f;
                d.fireMode = FireMode.Automatic; d.roundsPerMinute = 750f; d.hipSpread = 2.0f; d.aimSpread = 0.6f;
                d.magazineSize = 100; d.reserveAmmo = 200; d.reloadSeconds = 4.0f;
                d.recoilPitch = 1.35f; d.recoilYaw = 0.55f; d.moveSpeedMultiplier = 0.85f; d.aimFieldOfView = 50f; d.equipSeconds = 0.6f;
                d.viewKick = 0.04f; d.muzzleFlashScale = 1.25f; d.muzzleFlashColor = new Color(1f, 0.72f, 0.32f);
                Hitscan(d);
                d.viewModelPrefab = d.worldModelPrefab = machineGun;
                d.viewModelOffset = new Vector3(0f, -0.005f, -0.03f);
                d.fireSound = AudioGenerator.Load(AudioGenerator.SfxFolder, "Fire_MachineGun");
                d.reloadSound = AudioGenerator.Load(AudioGenerator.SfxFolder, "Reload_MachineGun");
            });

            WeaponDefinition knifeDef = Definition("Weapon_Knife", d =>
            {
                d.displayName = "Combat Knife";
                d.description = "Silent and always ready. Two slashes kill; one from behind.";
                d.damage = 55f; d.headshotMultiplier = 1.4f; d.pellets = 1;
                // Melee reach. Falloff is unused but must not exceed the range (OnValidate would raise the range).
                d.range = 2.2f; d.falloffStart = 2.2f; d.falloffEnd = 2.2f; d.minDamageMultiplier = 1f;
                d.fireMode = FireMode.Automatic; d.roundsPerMinute = 110f; d.hipSpread = 0f; d.aimSpread = 0f;
                d.magazineSize = 1; d.reserveAmmo = 0; d.reloadSeconds = 0.5f;
                d.recoilPitch = 0.6f; d.recoilYaw = 0.4f; d.moveSpeedMultiplier = 1.1f; d.aimFieldOfView = 66f; d.equipSeconds = 0.25f;
                d.viewKick = 0f; d.muzzleFlashScale = 0.1f; d.muzzleFlashColor = Color.white;
                d.kind = WeaponKind.Melee; d.usesAmmo = false; d.hasScope = false;
                d.backstabMultiplier = 2f;
                d.viewModelPrefab = d.worldModelPrefab = knife;
                d.viewModelOffset = new Vector3(0.01f, 0.02f, 0.04f);
                d.viewModelEuler = new Vector3(-12f, -14f, -22f);
                d.fireSound = AudioGenerator.Load(AudioGenerator.SfxFolder, "Knife_Swing");
                d.reloadSound = null;
                d.impactSound = AudioGenerator.Load(AudioGenerator.SfxFolder, "Knife_HitSurface");
                d.impactPlayerSound = AudioGenerator.Load(AudioGenerator.SfxFolder, "Knife_Hit");
            });

            WeaponDefinition rpgDef = Definition("Weapon_RPG", d =>
            {
                d.displayName = "RPG";
                d.description = "Rocket launcher. Splash damage kills anything near the blast. One rocket per reload.";
                d.damage = 1f; d.headshotMultiplier = 1f; d.pellets = 1; // the splash does the damage
                d.range = 150f; d.falloffStart = 150f; d.falloffEnd = 150f; d.minDamageMultiplier = 1f;
                d.fireMode = FireMode.SemiAutomatic; d.roundsPerMinute = 50f; d.hipSpread = 1.5f; d.aimSpread = 0.4f;
                d.magazineSize = 1; d.reserveAmmo = 4; d.reloadSeconds = 2.8f;
                d.recoilPitch = 4f; d.recoilYaw = 1f; d.moveSpeedMultiplier = 0.88f; d.aimFieldOfView = 50f; d.equipSeconds = 0.6f;
                d.viewKick = 0.12f; d.muzzleFlashScale = 2.2f; d.muzzleFlashColor = new Color(1f, 0.6f, 0.25f);
                d.kind = WeaponKind.Explosive; d.usesAmmo = true; d.hasScope = false; d.viewModelEuler = Vector3.zero;
                d.explosionRadius = 4.5f; d.explosionDamage = 125f; d.explosionFullDamageRadius = 1f;
                d.explosionEdgeMultiplier = 0.15f; d.selfDamageMultiplier = 0.3f;
                d.viewModelPrefab = d.worldModelPrefab = rpg;
                d.viewModelOffset = new Vector3(0.02f, -0.02f, -0.05f);
                d.fireSound = AudioGenerator.Load(AudioGenerator.SfxFolder, "Fire_RPG");
                d.reloadSound = AudioGenerator.Load(AudioGenerator.SfxFolder, "Reload_RPG");
                d.impactSound = d.impactPlayerSound = AudioGenerator.Load(AudioGenerator.SfxFolder, "Explosion");
            });

            // Network ids: never reorder.
            WeaponDefinition[] definitions = { rifleDef, smgDef, shotgunDef, pistolDef, sniperDef, machineGunDef, knifeDef, rpgDef };
            AudioClip dry = AudioGenerator.Load(AudioGenerator.SfxFolder, "DryFire");
            for (int i = 0; i < definitions.Length; i++)
            {
                definitions[i].dryFireSound = definitions[i].usesAmmo ? dry : null;
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

            report.Pass($"Weapons: {definitions.Length} models, {definitions.Length} WeaponDefinitions and the WeaponCatalog generated.");
            return catalog;
        }

        /// <summary>Plain gun defaults for the appended fields (re-running must reset them if an asset was edited).</summary>
        private static void Hitscan(WeaponDefinition d)
        {
            d.kind = WeaponKind.Hitscan;
            d.usesAmmo = true;
            d.hasScope = false;
            d.viewModelEuler = Vector3.zero;
            d.impactSound = null;
            d.impactPlayerSound = null;
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
            Marker(t, "Handguard", new Vector3(0f, 0.0f, 0.33f));
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
            Marker(t, "Handguard", new Vector3(0f, -0.04f, 0.2f));
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
            Marker(t, "Handguard", new Vector3(0f, 0.0f, 0.33f));
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
            Marker(t, "Handguard", new Vector3(-0.02f, -0.04f, 0.0f));
            return root;
        }

        /// <summary>Bolt-action sniper: long barrel with brake, big scope with blue lens, bolt handle, deployed bipod.</summary>
        private static GameObject Sniper()
        {
            var root = new GameObject("Weapon_Sniper");
            Transform t = root.transform;
            Part(t, new Vector3(0f, 0.045f, 0.06f), new Vector3(0.056f, 0.07f, 0.3f), metal);                 // receiver
            Part(t, new Vector3(0f, 0.032f, 0.33f), new Vector3(0.058f, 0.058f, 0.26f), olive);               // forend
            Cyl(t, new Vector3(0f, 0.05f, 0.65f), 0.022f, 0.56f, metal);                                      // long barrel
            Part(t, new Vector3(0f, 0.05f, 0.955f), new Vector3(0.038f, 0.034f, 0.07f), dark);                // muzzle brake
            Part(t, new Vector3(0f, 0.05f, 0.955f), new Vector3(0.042f, 0.01f, 0.04f), metal);                // brake ports
            Part(t, new Vector3(0f, 0.022f, -0.25f), new Vector3(0.05f, 0.1f, 0.3f), olive);                  // stock
            Part(t, new Vector3(0f, 0.08f, -0.2f), new Vector3(0.046f, 0.03f, 0.16f), olive);                 // cheek rest
            Part(t, new Vector3(0f, 0.01f, -0.41f), new Vector3(0.054f, 0.125f, 0.03f), polymer);             // butt pad
            Part(t, new Vector3(0f, -0.045f, -0.04f), new Vector3(0.032f, 0.09f, 0.04f), polymer, Quaternion.Euler(18f, 0f, 0f)); // grip
            Part(t, new Vector3(0f, -0.022f, 0.1f), new Vector3(0.036f, 0.05f, 0.08f), dark);                 // magazine
            Part(t, new Vector3(0.042f, 0.05f, -0.005f), new Vector3(0.04f, 0.012f, 0.012f), metal);          // bolt handle
            Part(t, new Vector3(0.064f, 0.05f, -0.005f), new Vector3(0.022f, 0.022f, 0.022f), dark);          // bolt knob
            Cyl(t, new Vector3(0f, 0.122f, 0.07f), 0.04f, 0.32f, dark);                                       // scope tube
            Cyl(t, new Vector3(0f, 0.122f, 0.26f), 0.058f, 0.08f, dark);                                      // objective bell
            Cyl(t, new Vector3(0f, 0.122f, -0.11f), 0.05f, 0.06f, dark);                                      // eyepiece
            Part(t, new Vector3(0f, 0.122f, 0.302f), new Vector3(0.044f, 0.044f, 0.004f), lens);              // lens glint
            Part(t, new Vector3(0f, 0.152f, 0.07f), new Vector3(0.022f, 0.026f, 0.022f), metal);              // elevation turret
            Part(t, new Vector3(0.027f, 0.122f, 0.07f), new Vector3(0.022f, 0.022f, 0.022f), metal);          // windage turret
            Part(t, new Vector3(0f, 0.09f, -0.01f), new Vector3(0.02f, 0.04f, 0.022f), metal);                // scope rings
            Part(t, new Vector3(0f, 0.09f, 0.15f), new Vector3(0.02f, 0.04f, 0.022f), metal);
            Part(t, new Vector3(0f, 0.005f, 0.44f), new Vector3(0.05f, 0.02f, 0.03f), metal);                 // bipod mount
            Part(t, new Vector3(0.03f, -0.07f, 0.47f), new Vector3(0.012f, 0.16f, 0.012f), metal, Quaternion.Euler(-20f, 0f, 12f)); // bipod legs
            Part(t, new Vector3(-0.03f, -0.07f, 0.47f), new Vector3(0.012f, 0.16f, 0.012f), metal, Quaternion.Euler(-20f, 0f, -12f));
            Part(t, new Vector3(0.029f, 0.04f, -0.25f), new Vector3(0.004f, 0.02f, 0.18f), accent);           // accent stripe
            Muzzle(t, new Vector3(0f, 0.05f, 1.0f));
            Marker(t, "Handguard", new Vector3(0f, 0.0f, 0.3f));
            return root;
        }

        /// <summary>Belt-fed LMG: box magazine with belt, carry handle, perforated handguard, bipod.</summary>
        private static GameObject MachineGun()
        {
            var root = new GameObject("Weapon_MachineGun");
            Transform t = root.transform;
            Part(t, new Vector3(0f, 0.05f, 0.06f), new Vector3(0.07f, 0.1f, 0.36f), polymer);                 // receiver
            Part(t, new Vector3(0f, 0.108f, 0.08f), new Vector3(0.066f, 0.02f, 0.2f), metal);                 // feed cover
            Part(t, new Vector3(0f, 0.045f, 0.34f), new Vector3(0.066f, 0.078f, 0.22f), metal);               // handguard
            for (int i = 0; i < 4; i++)                                                                       // cooling slots
                Part(t, new Vector3(0.0335f, 0.05f, 0.27f + i * 0.045f), new Vector3(0.002f, 0.03f, 0.02f), dark);
            Cyl(t, new Vector3(0f, 0.055f, 0.6f), 0.028f, 0.36f, metal);                                      // barrel
            Part(t, new Vector3(0f, 0.055f, 0.805f), new Vector3(0.042f, 0.042f, 0.06f), dark);               // flash hider
            Part(t, new Vector3(0f, 0.125f, 0.28f), new Vector3(0.014f, 0.05f, 0.014f), metal);               // carry handle posts
            Part(t, new Vector3(0f, 0.125f, 0.44f), new Vector3(0.014f, 0.05f, 0.014f), metal);
            Part(t, new Vector3(0f, 0.152f, 0.36f), new Vector3(0.02f, 0.02f, 0.18f), polymer);               // carry handle
            Part(t, new Vector3(-0.012f, -0.07f, 0.1f), new Vector3(0.095f, 0.115f, 0.13f), olive);           // box magazine
            Part(t, new Vector3(-0.012f, -0.012f, 0.1f), new Vector3(0.097f, 0.012f, 0.132f), dark);          // box lid
            Part(t, new Vector3(0.042f, 0.012f, 0.1f), new Vector3(0.018f, 0.04f, 0.075f), accent);           // brass belt into the feed
            Part(t, new Vector3(0.042f, 0.035f, 0.1f), new Vector3(0.02f, 0.008f, 0.08f), metal);             // belt links
            Part(t, new Vector3(0f, 0.035f, -0.24f), new Vector3(0.055f, 0.095f, 0.26f), polymer);            // stock
            Part(t, new Vector3(0f, 0.02f, -0.38f), new Vector3(0.058f, 0.12f, 0.03f), metal);                // butt plate
            Part(t, new Vector3(0f, -0.045f, -0.04f), new Vector3(0.032f, 0.09f, 0.04f), polymer, Quaternion.Euler(18f, 0f, 0f)); // grip
            Part(t, new Vector3(0f, 0.125f, 0.0f), new Vector3(0.018f, 0.02f, 0.03f), metal);                 // rear sight
            Part(t, new Vector3(0f, 0.09f, 0.76f), new Vector3(0.006f, 0.03f, 0.006f), metal);                // front post
            Part(t, new Vector3(0f, 0.107f, 0.76f), new Vector3(0.008f, 0.008f, 0.008f), glow);
            Part(t, new Vector3(0f, 0.025f, 0.6f), new Vector3(0.05f, 0.02f, 0.03f), metal);                  // bipod mount
            Part(t, new Vector3(0.035f, -0.06f, 0.62f), new Vector3(0.014f, 0.15f, 0.014f), metal, Quaternion.Euler(-15f, 0f, 12f)); // bipod legs
            Part(t, new Vector3(-0.035f, -0.06f, 0.62f), new Vector3(0.014f, 0.15f, 0.014f), metal, Quaternion.Euler(-15f, 0f, -12f));
            Part(t, new Vector3(0.036f, 0.07f, 0.06f), new Vector3(0.004f, 0.02f, 0.22f), accent);            // accent stripe
            Muzzle(t, new Vector3(0f, 0.055f, 0.84f));
            Marker(t, "Handguard", new Vector3(0f, 0.0f, 0.34f));
            return root;
        }

        /// <summary>
        /// Combat knife pointing along +Z. The handle sits where gun grips sit (just below the origin) so the first-person
        /// right hand wraps it; the left-hand marker is placed low and out of the camera view (the left hand rests off screen).
        /// No "Muzzle": the knife never spawns a muzzle flash.
        /// </summary>
        private static GameObject Knife()
        {
            var root = new GameObject("Weapon_Knife");
            Transform t = root.transform;
            Part(t, new Vector3(0f, -0.04f, -0.005f), new Vector3(0.026f, 0.032f, 0.12f), dark);              // handle
            Part(t, new Vector3(0f, -0.04f, -0.02f), new Vector3(0.028f, 0.034f, 0.01f), accent);             // wrap band
            Part(t, new Vector3(0f, -0.04f, 0.025f), new Vector3(0.028f, 0.034f, 0.01f), accent);
            Part(t, new Vector3(0f, -0.04f, -0.072f), new Vector3(0.03f, 0.036f, 0.016f), metal);             // pommel
            Part(t, new Vector3(0f, -0.04f, 0.062f), new Vector3(0.03f, 0.075f, 0.012f), metal);              // cross guard
            Part(t, new Vector3(0f, -0.036f, 0.15f), new Vector3(0.006f, 0.032f, 0.165f), blade);             // blade
            Part(t, new Vector3(0f, -0.024f, 0.245f), new Vector3(0.006f, 0.022f, 0.045f), blade, Quaternion.Euler(-28f, 0f, 0f)); // clipped tip
            Part(t, new Vector3(0f, -0.019f, 0.12f), new Vector3(0.007f, 0.005f, 0.1f), metal);               // spine
            Part(t, new Vector3(0f, -0.052f, 0.15f), new Vector3(0.0065f, 0.004f, 0.15f), blade);             // honed edge
            Marker(t, "BladeTip", new Vector3(0f, -0.03f, 0.27f));
            Marker(t, "Handguard", new Vector3(-0.2f, -0.32f, -0.12f));
            return root;
        }

        /// <summary>RPG launcher: tube with wooden heat shield, venturi, two grips, optic, and a "Warhead" rocket group.</summary>
        private static GameObject Rpg()
        {
            var root = new GameObject("Weapon_RPG");
            Transform t = root.transform;
            Cyl(t, new Vector3(0f, 0.07f, 0.05f), 0.085f, 1.0f, olive);                                       // launch tube
            Part(t, new Vector3(0f, 0.07f, 0.02f), new Vector3(0.096f, 0.096f, 0.26f), wood);                 // heat shield
            Cyl(t, new Vector3(0f, 0.07f, -0.48f), 0.11f, 0.08f, metal);                                      // venturi flare
            Cyl(t, new Vector3(0f, 0.07f, 0.535f), 0.095f, 0.04f, metal);                                     // front ring
            Part(t, new Vector3(0.0435f, 0.07f, -0.2f), new Vector3(0.004f, 0.02f, 0.18f), accent);           // accent stripe
            Part(t, new Vector3(0f, 0.013f, -0.02f), new Vector3(0.03f, 0.03f, 0.09f), metal);                // trigger housing
            Part(t, new Vector3(0f, -0.045f, -0.04f), new Vector3(0.032f, 0.09f, 0.04f), polymer, Quaternion.Euler(18f, 0f, 0f)); // grip
            Part(t, new Vector3(0f, 0.013f, 0.2f), new Vector3(0.026f, 0.03f, 0.04f), metal);                 // fore grip mount
            Part(t, new Vector3(0f, -0.04f, 0.21f), new Vector3(0.03f, 0.09f, 0.04f), polymer, Quaternion.Euler(10f, 0f, 0f)); // fore grip
            Part(t, new Vector3(-0.055f, 0.105f, 0.1f), new Vector3(0.03f, 0.045f, 0.11f), dark);             // optic (left side)
            Part(t, new Vector3(-0.055f, 0.105f, 0.043f), new Vector3(0.022f, 0.022f, 0.004f), glow);         // optic eyepiece glow
            Part(t, new Vector3(0f, 0.128f, 0.38f), new Vector3(0.008f, 0.036f, 0.008f), metal);              // iron front sight
            Part(t, new Vector3(0f, 0.122f, -0.08f), new Vector3(0.02f, 0.024f, 0.008f), metal);              // iron rear sight

            Transform warhead = new GameObject("Warhead").transform;
            warhead.SetParent(t, false);
            Cyl(warhead, new Vector3(0f, 0.07f, 0.61f), 0.07f, 0.12f, olive);                                 // rocket body
            Cyl(warhead, new Vector3(0f, 0.07f, 0.75f), 0.105f, 0.16f, olive);                                // warhead bulb
            Cyl(warhead, new Vector3(0f, 0.07f, 0.68f), 0.108f, 0.012f, accent);                              // band
            Cyl(warhead, new Vector3(0f, 0.07f, 0.865f), 0.06f, 0.07f, olive);                                // ogive
            Part(warhead, new Vector3(0f, 0.07f, 0.915f), new Vector3(0.024f, 0.024f, 0.04f), metal);         // fuse tip

            Muzzle(t, new Vector3(0f, 0.07f, 0.56f));
            Marker(t, "Handguard", new Vector3(0f, -0.04f, 0.21f));
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

        /// <summary>Empty transform used by the first-person rig to place the left hand.</summary>
        private static void Marker(Transform parent, string name, Vector3 position)
        {
            var marker = new GameObject(name);
            marker.transform.SetParent(parent, false);
            marker.transform.localPosition = position;
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
