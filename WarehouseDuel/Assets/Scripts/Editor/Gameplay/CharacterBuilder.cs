using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using WarehouseWar.Profile;

namespace WarehouseWar.EditorTools.Gameplay
{
    /// <summary>
    /// The ten playable characters. Each one has its own uniform pattern (camo texture), vest/skin/boots colours,
    /// and gear built from boxes (hats, masks, armour, scarves...). <see cref="BuildAccessories"/> adds every
    /// character's gear to a soldier model (inactive); <see cref="Build"/> writes the materials, renders a portrait
    /// per character and stores everything in Resources/CharacterCatalog.
    /// </summary>
    internal static class CharacterBuilder
    {
        public const string CatalogPath = "Assets/Resources/CharacterCatalog.asset";
        private const string MaterialFolder = "Assets/Art/Materials/Characters/Looks";
        private const string TextureFolder = "Assets/Art/Textures/Characters";
        private const string PortraitFolder = "Assets/Art/UI/Characters";

        private enum Pattern { Base, Blobs, Stripes, Digital, Solid, Leafy }

        private sealed class Def
        {
            public string name, role, description;
            public Pattern pattern;
            public Color32[] palette;
            public Color vest, skin, boots, strap, mask;
            public bool helmet = true, faceMask = true;
            public Color backdrop;
        }

        private static readonly Color SkinLight = new Color32(236, 182, 140, 255);
        private static readonly Color SkinMedium = new Color32(198, 134, 90, 255);
        private static readonly Color SkinDark = new Color32(122, 76, 48, 255);
        private static readonly Color Black = new Color(0.08f, 0.08f, 0.09f);

        private static readonly Def[] Characters =
        {
            new Def { name = "Ranger", role = "Frontline rifleman", description = "The classic infantry soldier: woodland camo, combat helmet and tactical goggles. Ready for anything.",
                pattern = Pattern.Base, backdrop = new Color32(40, 70, 40, 255) },
            new Def { name = "Desert Fox", role = "Dune raider", description = "Fights where the sun is merciless. Boonie hat, shemagh scarf and dark shades.",
                pattern = Pattern.Blobs, palette = new Color32[] { new Color32(196, 164, 112, 255), new Color32(222, 196, 150, 255), new Color32(160, 128, 84, 255), new Color32(132, 104, 70, 255) },
                vest = new Color32(150, 124, 84, 255), skin = SkinMedium, boots = new Color32(96, 72, 48, 255), strap = new Color32(92, 76, 56, 255), mask = Black,
                helmet = false, faceMask = false, backdrop = new Color32(150, 104, 50, 255) },
            new Def { name = "Ghost", role = "Night infiltrator", description = "Never seen, only heard once. Black ops gear, balaclava and night-vision goggles.",
                pattern = Pattern.Digital, palette = new Color32[] { new Color32(40, 42, 46, 255), new Color32(56, 58, 64, 255), new Color32(30, 31, 34, 255), new Color32(70, 72, 78, 255) },
                vest = new Color32(26, 27, 30, 255), skin = SkinLight, boots = new Color32(18, 18, 20, 255), strap = new Color32(20, 20, 22, 255), mask = Black,
                faceMask = false, backdrop = new Color32(20, 50, 34, 255) },
            new Def { name = "Frost", role = "Arctic scout", description = "Snow camo and a white helmet: invisible in the blizzard, deadly in the warehouse.",
                pattern = Pattern.Digital, palette = new Color32[] { new Color32(236, 240, 244, 255), new Color32(206, 216, 226, 255), new Color32(170, 184, 198, 255), new Color32(250, 252, 253, 255) },
                vest = new Color32(205, 212, 220, 255), skin = SkinLight, boots = new Color32(90, 96, 104, 255), strap = new Color32(120, 126, 134, 255), mask = new Color32(226, 230, 236, 255),
                backdrop = new Color32(70, 100, 140, 255) },
            new Def { name = "Commando", role = "Jungle raider", description = "Tiger stripes, red bandana, war paint and a belt of ammo across the chest.",
                pattern = Pattern.Stripes, palette = new Color32[] { new Color32(76, 96, 44, 255), new Color32(22, 26, 18, 255), new Color32(104, 118, 60, 255), new Color32(56, 70, 32, 255) },
                vest = new Color32(54, 60, 38, 255), skin = SkinDark, boots = new Color32(36, 30, 24, 255), strap = new Color32(40, 38, 30, 255), mask = Black,
                helmet = false, faceMask = false, backdrop = new Color32(110, 40, 30, 255) },
            new Def { name = "Urban Ops", role = "City enforcer", description = "Grey urban camo, black beret and shades. Built for tight corners and fast clears.",
                pattern = Pattern.Blobs, palette = new Color32[] { new Color32(124, 128, 132, 255), new Color32(92, 96, 100, 255), new Color32(154, 158, 162, 255), new Color32(70, 72, 76, 255) },
                vest = new Color32(30, 32, 36, 255), skin = SkinMedium, boots = new Color32(22, 22, 24, 255), strap = new Color32(28, 28, 30, 255), mask = Black,
                helmet = false, backdrop = new Color32(60, 66, 78, 255) },
            new Def { name = "Hazmat", role = "Chemical unit", description = "Sealed yellow suit, hood and gas mask. Walks through smoke like it is fresh air.",
                pattern = Pattern.Solid, palette = new Color32[] { new Color32(226, 188, 38, 255), new Color32(212, 174, 32, 255), new Color32(236, 202, 58, 255), new Color32(200, 162, 28, 255) },
                vest = new Color32(58, 60, 56, 255), skin = SkinLight, boots = new Color32(20, 20, 22, 255), strap = new Color32(40, 42, 40, 255), mask = Black,
                helmet = false, faceMask = false, backdrop = new Color32(100, 90, 30, 255) },
            new Def { name = "Officer", role = "Field commander", description = "Navy uniform, peaked cap, gold epaulettes and a chest full of medals. Leads from the front.",
                pattern = Pattern.Solid, palette = new Color32[] { new Color32(38, 50, 84, 255), new Color32(32, 42, 72, 255), new Color32(44, 58, 96, 255), new Color32(34, 46, 78, 255) },
                vest = new Color32(26, 32, 54, 255), skin = SkinMedium, boots = new Color32(14, 14, 16, 255), strap = new Color32(18, 20, 26, 255), mask = Black,
                helmet = false, faceMask = false, backdrop = new Color32(30, 40, 80, 255) },
            new Def { name = "Sniper", role = "Ghillie marksman", description = "Covered in leaves and burlap. If you can see the Sniper, you are already too late.",
                pattern = Pattern.Leafy, palette = new Color32[] { new Color32(82, 96, 48, 255), new Color32(58, 70, 34, 255), new Color32(108, 112, 64, 255), new Color32(74, 62, 40, 255) },
                vest = new Color32(60, 66, 40, 255), skin = SkinMedium, boots = new Color32(44, 36, 26, 255), strap = new Color32(50, 46, 34, 255), mask = Black,
                helmet = false, faceMask = false, backdrop = new Color32(50, 70, 30, 255) },
            new Def { name = "Juggernaut", role = "Heavy assault", description = "Heavy armour plates and a sealed helmet with a glowing visor. Slow to fall, hard to stop.",
                pattern = Pattern.Digital, palette = new Color32[] { new Color32(58, 60, 64, 255), new Color32(42, 44, 48, 255), new Color32(74, 76, 80, 255), new Color32(50, 52, 56, 255) },
                vest = new Color32(70, 72, 76, 255), skin = SkinDark, boots = new Color32(16, 16, 18, 255), strap = new Color32(24, 24, 26, 255), mask = Black,
                helmet = false, backdrop = new Color32(110, 20, 20, 255) },
            new Def { name = "Viper", role = "Recon specialist", description = "Toxic green digital camo with a black helmet. Strikes first and disappears.",
                pattern = Pattern.Digital, palette = new Color32[] { new Color32(70, 150, 60, 255), new Color32(40, 96, 40, 255), new Color32(120, 196, 70, 255), new Color32(24, 52, 26, 255) },
                vest = new Color32(28, 34, 28, 255), skin = SkinMedium, boots = new Color32(20, 22, 20, 255), strap = new Color32(30, 36, 30, 255), mask = Black,
                backdrop = new Color32(30, 90, 40, 255) },
            new Def { name = "Crimson", role = "Shock trooper", description = "Blood-red camo and a black vest. Nobody forgets the squad in red.",
                pattern = Pattern.Blobs, palette = new Color32[] { new Color32(150, 30, 30, 255), new Color32(100, 18, 20, 255), new Color32(190, 50, 40, 255), new Color32(60, 12, 14, 255) },
                vest = new Color32(24, 22, 24, 255), skin = SkinLight, boots = new Color32(18, 16, 16, 255), strap = new Color32(30, 26, 26, 255), mask = Black,
                backdrop = new Color32(120, 20, 24, 255) },
            new Def { name = "Navy Seal", role = "Amphibious raider", description = "Ocean-blue camo, dark helmet and a rebreather mask. Comes in from the water.",
                pattern = Pattern.Blobs, palette = new Color32[] { new Color32(40, 80, 130, 255), new Color32(26, 52, 92, 255), new Color32(70, 120, 170, 255), new Color32(18, 34, 60, 255) },
                vest = new Color32(22, 30, 44, 255), skin = SkinDark, boots = new Color32(14, 18, 24, 255), strap = new Color32(20, 26, 36, 255), mask = Black,
                backdrop = new Color32(20, 60, 120, 255) },
            new Def { name = "Sandstorm", role = "Desert vanguard", description = "Tiger-stripe sand camo and a tan helmet. Built for long fights in the open.",
                pattern = Pattern.Stripes, palette = new Color32[] { new Color32(204, 170, 110, 255), new Color32(120, 90, 54, 255), new Color32(228, 200, 146, 255), new Color32(170, 136, 86, 255) },
                vest = new Color32(140, 112, 72, 255), skin = SkinMedium, boots = new Color32(90, 66, 42, 255), strap = new Color32(100, 80, 56, 255), mask = new Color32(180, 150, 104, 255),
                backdrop = new Color32(170, 120, 50, 255) },
            new Def { name = "Shadow", role = "Special forces", description = "All black from helmet to boots. The last thing you see in a dark corridor.",
                pattern = Pattern.Solid, palette = new Color32[] { new Color32(30, 30, 34, 255), new Color32(24, 24, 28, 255), new Color32(38, 38, 42, 255), new Color32(20, 20, 24, 255) },
                vest = new Color32(16, 16, 18, 255), skin = SkinLight, boots = new Color32(10, 10, 12, 255), strap = new Color32(14, 14, 16, 255), mask = Black,
                backdrop = new Color32(40, 40, 60, 255) },
            new Def { name = "Gold Elite", role = "Legendary veteran", description = "A golden uniform earned through a hundred wars. For players who want everyone to know.",
                pattern = Pattern.Solid, palette = new Color32[] { new Color32(214, 168, 52, 255), new Color32(190, 146, 40, 255), new Color32(236, 196, 80, 255), new Color32(170, 128, 34, 255) },
                vest = new Color32(40, 34, 24, 255), skin = SkinMedium, boots = new Color32(30, 24, 16, 255), strap = new Color32(60, 48, 26, 255), mask = Black,
                backdrop = new Color32(150, 110, 20, 255) },
        };

        // ------------------------------------------------------------------ accessories

        /// <summary>Builds every character's gear onto <paramref name="rig"/>; returns (group, character index).</summary>
        public static IEnumerable<(GameObject, int)> BuildAccessories(SoldierModelBuilder.Rig rig)
        {
            var result = new List<(GameObject, int)>();
            GameObject Group(Transform bone, int character)
            {
                var go = new GameObject($"Look {character} ({Characters[character].name})");
                go.transform.SetParent(bone, false);
                result.Add((go, character));
                return go;
            }
            void P(GameObject group, Vector3 centre, Vector3 size, Material material, float rollZ = 0f) =>
                SoldierModelBuilder.Part(group.transform, centre, size, material, rollZ != 0f ? new Vector3(0f, 0f, rollZ) : (Vector3?)null);

            Material sunglasses = Flat("Acc_Sunglasses", new Color(0.05f, 0.05f, 0.06f), smoothness: 0.8f);
            Material gold = Flat("Acc_Gold", new Color(0.95f, 0.72f, 0.2f), smoothness: 0.6f, emission: 0.25f);
            Material darkGear = Flat("Acc_DarkGear", new Color(0.12f, 0.13f, 0.12f));
            Material rubber = Flat("Acc_Rubber", new Color(0.06f, 0.06f, 0.07f));

            // 1 Desert Fox: boonie hat, shemagh, sunglasses.
            {
                GameObject head = Group(rig.Head, 1);
                Material camo = CamoMaterial(1);
                P(head, new Vector3(0f, 0.41f, 0f), new Vector3(0.41f, 0.12f, 0.41f), camo);
                P(head, new Vector3(0f, 0.355f, 0f), new Vector3(0.64f, 0.025f, 0.64f), camo);
                P(head, new Vector3(0f, 0.22f, 0.197f), new Vector3(0.31f, 0.065f, 0.012f), sunglasses);
                GameObject neck = Group(rig.Spine, 1);
                P(neck, new Vector3(0f, 0.5f, 0.02f), new Vector3(0.46f, 0.12f, 0.4f), Flat("Acc_Shemagh", new Color(0.86f, 0.8f, 0.66f)));
            }
            // 2 Ghost: balaclava + night-vision goggles on the helmet.
            {
                GameObject head = Group(rig.Head, 2);
                P(head, new Vector3(0f, 0.18f, 0f), new Vector3(0.395f, 0.405f, 0.395f), rubber);
                P(head, new Vector3(0f, 0.22f, 0.199f), new Vector3(0.3f, 0.08f, 0.01f), Flat("Acc_SkinLight", SkinLight));
                P(head, new Vector3(-0.08f, 0.22f, 0.205f), new Vector3(0.07f, 0.045f, 0.01f), rubber);
                P(head, new Vector3(0.08f, 0.22f, 0.205f), new Vector3(0.07f, 0.045f, 0.01f), rubber);
                P(head, new Vector3(0f, 0.47f, 0.24f), new Vector3(0.1f, 0.07f, 0.06f), darkGear);
                Material lens = Flat("Acc_NvgLens", new Color(0.3f, 1f, 0.4f), emission: 2.2f);
                foreach (float x in new[] { -0.05f, 0.05f })
                {
                    P(head, new Vector3(x, 0.41f, 0.3f), new Vector3(0.075f, 0.075f, 0.14f), rubber);
                    P(head, new Vector3(x, 0.41f, 0.372f), new Vector3(0.055f, 0.055f, 0.01f), lens);
                }
            }
            // 3 Frost: thick scarf.
            {
                GameObject neck = Group(rig.Spine, 3);
                P(neck, new Vector3(0f, 0.5f, 0.02f), new Vector3(0.46f, 0.11f, 0.4f), Flat("Acc_ScarfGrey", new Color(0.55f, 0.58f, 0.62f)));
            }
            // 4 Commando: red bandana, short hair, war paint, ammo belt.
            {
                GameObject head = Group(rig.Head, 4);
                Material red = Flat("Acc_Red", new Color(0.78f, 0.12f, 0.1f));
                P(head, new Vector3(0f, 0.36f, 0f), new Vector3(0.39f, 0.035f, 0.39f), Flat("Acc_Hair", new Color(0.12f, 0.08f, 0.05f)));
                P(head, new Vector3(0f, 0.31f, 0f), new Vector3(0.405f, 0.07f, 0.405f), red);
                P(head, new Vector3(0f, 0.3f, -0.22f), new Vector3(0.07f, 0.09f, 0.08f), red);
                Material paint = Flat("Acc_FacePaint", new Color(0.06f, 0.08f, 0.05f));
                P(head, new Vector3(-0.09f, 0.185f, 0.1925f), new Vector3(0.08f, 0.022f, 0.005f), paint);
                P(head, new Vector3(0.09f, 0.185f, 0.1925f), new Vector3(0.08f, 0.022f, 0.005f), paint);
                GameObject chest = Group(rig.Spine, 4);
                P(chest, new Vector3(0f, 0.25f, 0.215f), new Vector3(0.07f, 0.72f, 0.03f), Flat("Acc_Brass", new Color(0.78f, 0.6f, 0.26f), smoothness: 0.5f), 35f);
            }
            // 5 Urban Ops: black beret with a gold badge, sunglasses.
            {
                GameObject head = Group(rig.Head, 5);
                P(head, new Vector3(0.03f, 0.385f, 0f), new Vector3(0.43f, 0.07f, 0.44f), Flat("Acc_BeretBlack", new Color(0.1f, 0.1f, 0.11f)), -9f);
                P(head, new Vector3(-0.1f, 0.35f, 0.21f), new Vector3(0.06f, 0.06f, 0.012f), gold);
                P(head, new Vector3(0f, 0.22f, 0.197f), new Vector3(0.31f, 0.065f, 0.012f), sunglasses);
            }
            // 6 Hazmat: hood, gas mask with visor and filter.
            {
                GameObject head = Group(rig.Head, 6);
                P(head, new Vector3(0f, 0.2f, -0.01f), new Vector3(0.43f, 0.45f, 0.43f), CamoMaterial(6));
                P(head, new Vector3(0f, 0.15f, 0.225f), new Vector3(0.34f, 0.28f, 0.07f), rubber);
                P(head, new Vector3(0f, 0.24f, 0.262f), new Vector3(0.3f, 0.1f, 0.012f), Flat("Acc_Visor", new Color(0.4f, 0.85f, 0.8f), smoothness: 0.9f, emission: 0.6f));
                P(head, new Vector3(0f, 0.05f, 0.3f), new Vector3(0.13f, 0.13f, 0.12f), Flat("Acc_Filter", new Color(0.3f, 0.34f, 0.22f)));
            }
            // 7 Officer: peaked cap, gold epaulettes, medals.
            {
                GameObject head = Group(rig.Head, 7);
                Material navy = Flat("Acc_Navy", new Color(0.15f, 0.2f, 0.33f));
                P(head, new Vector3(0f, 0.4f, 0f), new Vector3(0.42f, 0.12f, 0.42f), navy);
                P(head, new Vector3(0f, 0.465f, -0.01f), new Vector3(0.47f, 0.035f, 0.49f), navy);
                P(head, new Vector3(0f, 0.36f, 0f), new Vector3(0.43f, 0.03f, 0.43f), gold);
                P(head, new Vector3(0f, 0.35f, 0.22f), new Vector3(0.36f, 0.025f, 0.13f), sunglasses);
                P(head, new Vector3(0f, 0.415f, 0.212f), new Vector3(0.07f, 0.07f, 0.012f), gold);
                foreach (Transform shoulder in new[] { rig.ShoulderL, rig.ShoulderR })
                {
                    GameObject epaulette = Group(shoulder, 7);
                    P(epaulette, new Vector3(0f, 0.09f, 0f), new Vector3(0.24f, 0.04f, 0.26f), gold);
                }
                GameObject chest = Group(rig.Spine, 7);
                Color[] ribbons = { new Color(0.8f, 0.15f, 0.12f), new Color(0.2f, 0.4f, 0.85f), new Color(0.2f, 0.65f, 0.3f) };
                for (int i = 0; i < ribbons.Length; i++)
                {
                    P(chest, new Vector3(-0.17f + i * 0.045f, 0.33f, 0.206f), new Vector3(0.035f, 0.05f, 0.012f), Flat($"Acc_Ribbon{i}", ribbons[i]));
                }
            }
            // 8 Sniper: ghillie boonie with leafy tufts, tufts on shoulders and back, face paint.
            {
                GameObject head = Group(rig.Head, 8);
                Material camo = CamoMaterial(8);
                P(head, new Vector3(0f, 0.41f, 0f), new Vector3(0.41f, 0.12f, 0.41f), camo);
                P(head, new Vector3(0f, 0.355f, 0f), new Vector3(0.64f, 0.025f, 0.64f), camo);
                Material[] leaves = { Flat("Acc_Leaf0", new Color(0.3f, 0.42f, 0.16f)), Flat("Acc_Leaf1", new Color(0.42f, 0.46f, 0.2f)), Flat("Acc_Leaf2", new Color(0.36f, 0.3f, 0.18f)) };
                var random = new System.Random(8);
                float R(float a, float b) => a + (float)random.NextDouble() * (b - a);
                for (int i = 0; i < 9; i++)
                {
                    P(head, new Vector3(R(-0.28f, 0.28f), R(0.38f, 0.5f), R(-0.28f, 0.28f)), new Vector3(R(0.06f, 0.12f), R(0.04f, 0.09f), R(0.06f, 0.12f)), leaves[i % 3], R(-30f, 30f));
                }
                P(head, new Vector3(-0.09f, 0.185f, 0.1925f), new Vector3(0.08f, 0.022f, 0.005f), leaves[0]);
                P(head, new Vector3(0.09f, 0.185f, 0.1925f), new Vector3(0.08f, 0.022f, 0.005f), leaves[0]);
                foreach (Transform shoulder in new[] { rig.ShoulderL, rig.ShoulderR })
                {
                    GameObject tufts = Group(shoulder, 8);
                    for (int i = 0; i < 4; i++)
                    {
                        P(tufts, new Vector3(R(-0.1f, 0.1f), R(0.02f, 0.12f), R(-0.12f, 0.12f)), new Vector3(R(0.06f, 0.1f), R(0.05f, 0.08f), R(0.06f, 0.1f)), leaves[i % 3], R(-30f, 30f));
                    }
                }
                GameObject back = Group(rig.Spine, 8);
                for (int i = 0; i < 6; i++)
                {
                    P(back, new Vector3(R(-0.22f, 0.22f), R(0.1f, 0.5f), R(-0.3f, -0.24f)), new Vector3(R(0.08f, 0.14f), R(0.06f, 0.12f), R(0.04f, 0.08f)), leaves[i % 3], R(-30f, 30f));
                }
            }
            // 9 Juggernaut: sealed heavy helmet with red visor, shoulder plates, chest plate.
            {
                GameObject head = Group(rig.Head, 9);
                Material steel = Flat("Acc_HeavySteel", new Color(0.24f, 0.25f, 0.27f), smoothness: 0.45f);
                P(head, new Vector3(0f, 0.27f, -0.01f), new Vector3(0.49f, 0.36f, 0.49f), steel);
                P(head, new Vector3(0f, 0.25f, 0.243f), new Vector3(0.38f, 0.07f, 0.02f), Flat("Acc_RedVisor", new Color(1f, 0.15f, 0.1f), emission: 2.4f));
                P(head, new Vector3(0f, 0.07f, 0.21f), new Vector3(0.38f, 0.12f, 0.08f), steel);
                foreach (Transform shoulder in new[] { rig.ShoulderL, rig.ShoulderR })
                {
                    GameObject plate = Group(shoulder, 9);
                    P(plate, new Vector3(0f, 0.06f, 0f), new Vector3(0.3f, 0.12f, 0.32f), steel);
                }
                GameObject chest = Group(rig.Spine, 9);
                P(chest, new Vector3(0f, 0.25f, 0.225f), new Vector3(0.54f, 0.44f, 0.07f), steel);
                P(chest, new Vector3(0f, 0.25f, 0.262f), new Vector3(0.2f, 0.1f, 0.01f), Flat("Acc_RedVisor", new Color(1f, 0.15f, 0.1f), emission: 2.4f));
            }
            return result;
        }

        // ------------------------------------------------------------------ catalog + portraits

        public static void Build(SetupReport report)
        {
            FolderStructure.EnsureFolder(MaterialFolder);
            FolderStructure.EnsureFolder(PortraitFolder);
            FolderStructure.EnsureFolder("Assets/Resources");

            var catalog = AssetDatabase.LoadAssetAtPath<CharacterCatalog>(CatalogPath);
            if (catalog == null)
            {
                catalog = ScriptableObject.CreateInstance<CharacterCatalog>();
                AssetDatabase.CreateAsset(catalog, CatalogPath);
            }

            var entries = new CharacterCatalog.Entry[Characters.Length];
            for (int i = 0; i < Characters.Length; i++)
            {
                Def def = Characters[i];
                var entry = new CharacterCatalog.Entry
                {
                    name = def.name,
                    role = def.role,
                    description = def.description,
                    helmet = def.helmet,
                    faceMask = def.faceMask,
                    materials = new Material[7],
                };
                if (def.pattern != Pattern.Base)
                {
                    entry.materials[(int)CharacterCatalog.Slot.Camo] = CamoMaterial(i);
                    entry.materials[(int)CharacterCatalog.Slot.CamoDark] = CamoMaterial(i, dark: true);
                    entry.materials[(int)CharacterCatalog.Slot.Vest] = Flat($"Look{i}_Vest", def.vest);
                    entry.materials[(int)CharacterCatalog.Slot.Skin] = Flat($"Look{i}_Skin", def.skin);
                    entry.materials[(int)CharacterCatalog.Slot.Boots] = Flat($"Look{i}_Boots", def.boots);
                    entry.materials[(int)CharacterCatalog.Slot.Strap] = Flat($"Look{i}_Strap", def.strap);
                    entry.materials[(int)CharacterCatalog.Slot.Mask] = Flat($"Look{i}_Mask", def.mask);
                }
                entries[i] = entry;
            }
            catalog.characters = entries;
            EditorUtility.SetDirty(catalog);
            AssetDatabase.SaveAssets();

            // Portraits: each character rendered in a three-quarter view in front of a coloured backdrop.
            UnityEditor.SceneManagement.EditorSceneManager.NewScene(UnityEditor.SceneManagement.NewSceneSetup.EmptyScene, UnityEditor.SceneManagement.NewSceneMode.Single);
            var paths = new string[Characters.Length];
            for (int i = 0; i < Characters.Length; i++)
            {
                paths[i] = $"{PortraitFolder}/Character_{i:00}.png";
                RenderPortrait(i, catalog, paths[i]);
            }

            // Importing reloads assets, which leaves earlier references to the catalog stale: import first, then
            // load the catalog again and attach the portraits.
            var sprites = new Sprite[paths.Length];
            for (int i = 0; i < paths.Length; i++) sprites[i] = ImportSprite(paths[i]);
            catalog = AssetDatabase.LoadAssetAtPath<CharacterCatalog>(CatalogPath);
            for (int i = 0; i < catalog.characters.Length && i < sprites.Length; i++) catalog.characters[i].portrait = sprites[i];
            EditorUtility.SetDirty(catalog);
            AssetDatabase.SaveAssets();

            int missing = System.Array.FindAll(catalog.characters, e => e.portrait == null).Length;
            if (missing > 0) report.Error($"Characters: {missing} portrait(s) failed to import.");
            else report.Pass($"Characters: {catalog.characters.Length} playable characters with materials, gear and portraits.");
        }

        private static void RenderPortrait(int index, CharacterCatalog catalog, string path)
        {
            const int size = 256;
            var root = new GameObject("Portrait Model");
            root.transform.position = new Vector3(0f, -600f, 0f);
            root.transform.rotation = Quaternion.Euler(0f, 160f, 0f);
            SoldierModelBuilder.Rig rig = SoldierModelBuilder.Build(root.transform, "Soldier");
            rig.Look.Apply(index, catalog);

            var temporary = new List<Object>();
            var lights = new List<GameObject>();
            void AddLight(Quaternion rotation, float intensity, Color color)
            {
                var go = new GameObject("Portrait Light");
                go.transform.rotation = rotation;
                Light light = go.AddComponent<Light>();
                light.type = LightType.Directional;
                light.intensity = intensity;
                light.color = color;
                light.shadows = LightShadows.None;
                lights.Add(go);
            }
            AddLight(Quaternion.Euler(24f, 22f, 0f), 1.2f, new Color(1f, 0.92f, 0.82f));
            AddLight(Quaternion.Euler(12f, 196f, 0f), 1.4f, new Color(1f, 0.6f, 0.3f));
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.5f, 0.55f, 0.66f);
            RenderSettings.ambientEquatorColor = new Color(0.4f, 0.38f, 0.38f);
            RenderSettings.ambientGroundColor = new Color(0.22f, 0.2f, 0.18f);
            RenderSettings.fog = false;

            Vector3 focus = root.transform.position + new Vector3(0f, 1.25f, 0f);
            var camObject = new GameObject("Portrait Camera");
            camObject.transform.position = focus + new Vector3(0.25f, 0.25f, -3.1f);
            camObject.transform.LookAt(focus);
            Camera cam = camObject.AddComponent<Camera>();
            cam.fieldOfView = 34f;
            cam.nearClipPlane = 0.1f;
            cam.farClipPlane = 20f;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = Characters[index].backdrop;

            var rt = new RenderTexture(size, size, 24, RenderTextureFormat.ARGB32) { antiAliasing = 4 };
            cam.targetTexture = rt;
            cam.Render();
            cam.Render();
            RenderTexture.active = rt;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            texture.ReadPixels(new Rect(0, 0, size, size), 0, 0);
            RenderTexture.active = null;
            cam.targetTexture = null;
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float d = new Vector2(x / (float)size - 0.5f, y / (float)size - 0.55f).magnitude;
                    Color c = texture.GetPixel(x, y) * Mathf.Lerp(1.15f, 0.45f, Mathf.SmoothStep(0.2f, 0.75f, d));
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
            foreach (GameObject go in lights) Object.DestroyImmediate(go);
            Object.DestroyImmediate(root);
            foreach (Object o in temporary) Object.DestroyImmediate(o);
        }

        // ------------------------------------------------------------------ materials

        private static Material CamoMaterial(int index, bool dark = false)
        {
            Texture2D texture = CamoTexture(index);
            return Flat($"Look{index}_{(dark ? "CamoDark" : "Camo")}", dark ? new Color(0.75f, 0.75f, 0.75f) : Color.white, texture);
        }

        private static Material Flat(string name, Color color, Texture2D texture = null, float smoothness = 0.15f, float emission = 0f)
        {
            FolderStructure.EnsureFolder(MaterialFolder);
            string path = $"{MaterialFolder}/M_{name}.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(Shader.Find("Universal Render Pipeline/Simple Lit"));
                AssetDatabase.CreateAsset(material, path);
            }
            material.SetColor("_BaseColor", color);
            material.SetTexture("_BaseMap", texture);
            material.SetFloat("_Smoothness", smoothness);
            if (emission > 0f)
            {
                material.EnableKeyword("_EMISSION");
                material.SetColor("_EmissionColor", color * emission);
                material.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            }
            else
            {
                material.DisableKeyword("_EMISSION");
            }
            material.enableInstancing = true;
            EditorUtility.SetDirty(material);
            return material;
        }

        /// <summary>16×16 point-filtered uniform pattern in the character's palette (blobs, stripes, digital, solid, leafy).</summary>
        private static Texture2D CamoTexture(int index)
        {
            Def def = Characters[index];
            string path = $"{TextureFolder}/T_Camo_{index:00}.png";
            var existing = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            if (existing != null) return existing;

            FolderStructure.EnsureFolder(TextureFolder);
            const int size = 16;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            var random = new System.Random(100 + index);
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    int c;
                    switch (def.pattern)
                    {
                        case Pattern.Stripes:
                        {
                            float wave = Mathf.Sin((x * 0.9f + Mathf.Sin(y * 0.7f) * 2.2f) * 0.9f);
                            c = wave > 0.55f ? 1 : wave < -0.6f ? 2 : (x + y) % 7 == 0 ? 3 : 0;
                            break;
                        }
                        case Pattern.Digital:
                        {
                            float n = Mathf.PerlinNoise((x / 2) * 0.55f + index * 3.1f, (y / 2) * 0.55f + 1.3f);
                            c = n > 0.66f ? 2 : n < 0.33f ? 1 : n > 0.52f ? 3 : 0;
                            break;
                        }
                        case Pattern.Solid:
                            c = random.NextDouble() > 0.85 ? 1 + random.Next(3) : 0;
                            break;
                        case Pattern.Leafy:
                            c = random.Next(4);
                            break;
                        default:
                        {
                            float a = Mathf.PerlinNoise(x * 0.32f + 3.3f + index, y * 0.32f + 1.7f);
                            float b = Mathf.PerlinNoise(x * 0.55f + 9.1f, y * 0.55f + 4.2f + index);
                            c = a > 0.62f ? 2 : a < 0.36f ? 1 : b > 0.66f ? 3 : 0;
                            break;
                        }
                    }
                    texture.SetPixel(x, y, def.palette[c]);
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

        private static Sprite ImportSprite(string path)
        {
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
            if (AssetImporter.GetAtPath(path) is TextureImporter importer)
            {
                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.mipmapEnabled = false;
                importer.wrapMode = TextureWrapMode.Clamp;
                importer.maxTextureSize = 256;
                importer.textureCompression = TextureImporterCompression.Compressed;
                importer.SaveAndReimport();
            }
            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }
    }
}
