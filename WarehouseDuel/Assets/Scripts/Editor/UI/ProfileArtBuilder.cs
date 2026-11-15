using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using WarehouseWar.EditorTools.Gameplay;
using WarehouseWar.Profile;
using WarehouseWar.Weapons;

namespace WarehouseWar.EditorTools.UI
{
    /// <summary>
    /// Generates the profile art and stores it in Resources/ProfileCatalog:
    /// avatars = portraits of the voxel soldier in different war gear (helmet, beret, night vision, gas mask,
    /// balaclava, headset, boonie hat, bandana, face paint), camo and skin tones, holding a weapon, in front of a
    /// painted battlefield backdrop; banners = <see cref="BannerArt"/>.
    /// </summary>
    internal static class ProfileArtBuilder
    {
        public const string AvatarFolder = "Assets/Art/UI/Avatars";
        public const string BannerFolder = "Assets/Art/UI/Banners";
        public const string CatalogPath = "Assets/Resources/ProfileCatalog.asset";

        private const int AvatarSize = 256;

        private enum Gear { Helmet, Beret, Boonie, Balaclava, Headset, NightVision, GasMask, Bandana }
        private enum Backdrop { SunsetFire, NightOps, DesertStorm, Inferno, Arctic, SteelStorm }

        private struct AvatarStyle
        {
            public Gear gear;
            public Color team, camo, skin, accent;
            public bool mask, facePaint;
            public Backdrop backdrop;
            public int weapon;    // catalog index, -1 = none
            public float yaw;
        }

        private static readonly Color Woodland = Color.white;
        private static readonly Color Desert = new Color(1f, 0.82f, 0.58f);
        private static readonly Color Urban = new Color(0.72f, 0.76f, 0.8f);
        private static readonly Color Navy = new Color(0.6f, 0.7f, 0.95f);
        private static readonly Color Night = new Color(0.45f, 0.47f, 0.5f);
        private static readonly Color Snow = new Color(1.25f, 1.3f, 1.35f);
        private static readonly Color SkinLight = new Color32(236, 182, 140, 255);
        private static readonly Color SkinMedium = new Color32(198, 134, 90, 255);
        private static readonly Color SkinDark = new Color32(122, 76, 48, 255);
        private static readonly Color Blue = new Color(0.2f, 0.55f, 1f), Red = new Color(1f, 0.3f, 0.25f), Green = new Color(0.35f, 0.8f, 0.35f);
        private static readonly Color Orange = new Color(1f, 0.55f, 0.15f), Black = new Color(0.14f, 0.14f, 0.16f), Olive = new Color(0.36f, 0.4f, 0.24f);
        private static readonly Color Maroon = new Color(0.45f, 0.08f, 0.1f), Tan = new Color(0.7f, 0.58f, 0.4f);

        private static readonly AvatarStyle[] Avatars =
        {
            new AvatarStyle { gear = Gear.Helmet, team = Blue, camo = Woodland, skin = SkinLight, accent = new Color(0.25f, 0.7f, 1f), mask = true, backdrop = Backdrop.SunsetFire, weapon = 0, yaw = -14f },
            new AvatarStyle { gear = Gear.Beret, team = Maroon, camo = Desert, skin = SkinMedium, accent = Maroon, backdrop = Backdrop.DesertStorm, weapon = 3, yaw = 12f },
            new AvatarStyle { gear = Gear.NightVision, team = Green, camo = Night, skin = SkinDark, accent = Green, mask = true, backdrop = Backdrop.NightOps, weapon = 1, yaw = -10f },
            new AvatarStyle { gear = Gear.Balaclava, team = Black, camo = Urban, skin = SkinLight, accent = Black, backdrop = Backdrop.Inferno, weapon = 0, yaw = 14f },
            new AvatarStyle { gear = Gear.Headset, team = Olive, camo = Woodland, skin = SkinMedium, accent = new Color(1f, 0.6f, 0.2f), mask = true, backdrop = Backdrop.SteelStorm, weapon = 0, yaw = -16f },
            new AvatarStyle { gear = Gear.GasMask, team = Olive, camo = Night, skin = SkinLight, accent = new Color(0.4f, 0.9f, 0.8f), backdrop = Backdrop.Inferno, weapon = 1, yaw = 10f },
            new AvatarStyle { gear = Gear.Boonie, team = Olive, camo = Woodland, skin = SkinDark, accent = Olive, facePaint = true, backdrop = Backdrop.SunsetFire, weapon = 2, yaw = -12f },
            new AvatarStyle { gear = Gear.Bandana, team = Red, camo = Desert, skin = SkinMedium, accent = Red, facePaint = true, backdrop = Backdrop.DesertStorm, weapon = 0, yaw = 15f },
            new AvatarStyle { gear = Gear.Helmet, team = Red, camo = Urban, skin = SkinDark, accent = new Color(1f, 0.4f, 0.3f), mask = true, backdrop = Backdrop.Arctic, weapon = 1, yaw = 9f },
            new AvatarStyle { gear = Gear.Beret, team = Black, camo = Navy, skin = SkinLight, accent = Black, backdrop = Backdrop.NightOps, weapon = 3, yaw = -15f },
            new AvatarStyle { gear = Gear.Helmet, team = Orange, camo = Desert, skin = SkinLight, accent = new Color(1f, 0.7f, 0.2f), facePaint = true, backdrop = Backdrop.SunsetFire, weapon = 2, yaw = 12f },
            new AvatarStyle { gear = Gear.NightVision, team = Olive, camo = Woodland, skin = SkinMedium, accent = Green, backdrop = Backdrop.NightOps, weapon = 0, yaw = 14f },
            new AvatarStyle { gear = Gear.Balaclava, team = new Color(0.9f, 0.92f, 0.95f), camo = Snow, skin = SkinLight, accent = new Color(0.9f, 0.92f, 0.95f), backdrop = Backdrop.Arctic, weapon = 0, yaw = -9f },
            new AvatarStyle { gear = Gear.Boonie, team = Tan, camo = Desert, skin = SkinMedium, accent = Tan, mask = true, backdrop = Backdrop.DesertStorm, weapon = 0, yaw = 10f },
            new AvatarStyle { gear = Gear.GasMask, team = Black, camo = Woodland, skin = SkinDark, accent = new Color(1f, 0.55f, 0.2f), backdrop = Backdrop.SteelStorm, weapon = 1, yaw = -13f },
            new AvatarStyle { gear = Gear.Helmet, team = Black, camo = Night, skin = SkinDark, accent = new Color(1f, 0.3f, 0.2f), facePaint = true, backdrop = Backdrop.Inferno, weapon = 0, yaw = 11f },
            new AvatarStyle { gear = Gear.Boonie, team = Olive, camo = Woodland, skin = SkinLight, accent = Green, facePaint = true, backdrop = Backdrop.SteelStorm, weapon = 4, yaw = -12f },
            new AvatarStyle { gear = Gear.Helmet, team = Tan, camo = Desert, skin = SkinDark, accent = new Color(1f, 0.6f, 0.2f), mask = true, backdrop = Backdrop.DesertStorm, weapon = 5, yaw = 13f },
            new AvatarStyle { gear = Gear.Balaclava, team = Black, camo = Night, skin = SkinMedium, accent = new Color(0.6f, 0.3f, 1f), backdrop = Backdrop.NightOps, weapon = 6, yaw = -14f },
            new AvatarStyle { gear = Gear.Headset, team = Red, camo = Urban, skin = SkinLight, accent = Red, backdrop = Backdrop.Inferno, weapon = 7, yaw = 12f },
            new AvatarStyle { gear = Gear.Beret, team = Green, camo = Woodland, skin = SkinDark, accent = Green, backdrop = Backdrop.SunsetFire, weapon = 4, yaw = 10f },
            new AvatarStyle { gear = Gear.NightVision, team = Blue, camo = Snow, skin = SkinLight, accent = Blue, mask = true, backdrop = Backdrop.Arctic, weapon = 5, yaw = -11f },
            new AvatarStyle { gear = Gear.GasMask, team = Orange, camo = Desert, skin = SkinMedium, accent = Orange, backdrop = Backdrop.SteelStorm, weapon = 7, yaw = 14f },
            new AvatarStyle { gear = Gear.Bandana, team = Maroon, camo = Urban, skin = SkinDark, accent = new Color(1f, 0.75f, 0.2f), facePaint = true, backdrop = Backdrop.NightOps, weapon = 2, yaw = -13f },
            new AvatarStyle { gear = Gear.Helmet, team = Green, camo = Snow, skin = SkinDark, accent = new Color(0.3f, 0.9f, 1f), mask = true, backdrop = Backdrop.Arctic, weapon = 4, yaw = -12f },
            new AvatarStyle { gear = Gear.Beret, team = Blue, camo = Woodland, skin = SkinMedium, accent = new Color(0.3f, 0.55f, 0.95f), backdrop = Backdrop.SteelStorm, weapon = 0, yaw = 13f },
            new AvatarStyle { gear = Gear.NightVision, team = Black, camo = Night, skin = SkinLight, accent = Red, facePaint = true, backdrop = Backdrop.Inferno, weapon = 2, yaw = -10f },
            new AvatarStyle { gear = Gear.GasMask, team = Green, camo = Woodland, skin = SkinDark, accent = Green, backdrop = Backdrop.NightOps, weapon = 3, yaw = 11f },
            new AvatarStyle { gear = Gear.Boonie, team = Orange, camo = Desert, skin = SkinLight, accent = Orange, facePaint = true, backdrop = Backdrop.SunsetFire, weapon = 5, yaw = -14f },
            new AvatarStyle { gear = Gear.Headset, team = Blue, camo = Navy, skin = SkinDark, accent = new Color(0.3f, 0.85f, 1f), mask = true, backdrop = Backdrop.NightOps, weapon = 6, yaw = 12f },
            new AvatarStyle { gear = Gear.Bandana, team = Olive, camo = Woodland, skin = SkinMedium, accent = Green, facePaint = true, backdrop = Backdrop.SteelStorm, weapon = 7, yaw = -11f },
            new AvatarStyle { gear = Gear.Balaclava, team = Red, camo = Desert, skin = SkinMedium, accent = Tan, backdrop = Backdrop.DesertStorm, weapon = 1, yaw = 14f },
        };

        public static void Build(SetupReport report)
        {
            FolderStructure.EnsureFolder(AvatarFolder);
            FolderStructure.EnsureFolder(BannerFolder);
            FolderStructure.EnsureFolder("Assets/Resources");
            ClearFolder(AvatarFolder);
            ClearFolder(BannerFolder);

            WeaponCatalog weapons = AssetDatabase.LoadAssetAtPath<WeaponCatalog>(WarehouseWar.EditorTools.Weapons.WeaponAssetBuilder.CatalogPath);

            var avatars = new List<Sprite>();
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            for (int i = 0; i < Avatars.Length; i++)
            {
                string path = $"{AvatarFolder}/Avatar_{i:00}.png";
                RenderAvatar(Avatars[i], weapons, path);
                avatars.Add(ImportSprite(path, 256));
            }

            BannerArt.Prepare(weapons);
            var banners = new List<Sprite>();
            for (int i = 0; i < BannerArt.Names.Length; i++)
            {
                int style = i;
                string path = $"{BannerFolder}/Banner_{i:00}_{BannerArt.Names[i].Replace(" ", "")}.png";
                SaveTexture(path, BannerArt.Width, BannerArt.Height, (x, y) => BannerArt.Pixel(style, x, y));
                banners.Add(ImportSprite(path, 512));
            }

            var catalog = AssetDatabase.LoadAssetAtPath<ProfileCatalog>(CatalogPath);
            if (catalog == null)
            {
                catalog = ScriptableObject.CreateInstance<ProfileCatalog>();
                AssetDatabase.CreateAsset(catalog, CatalogPath);
            }
            catalog.avatars = avatars.ToArray();
            catalog.banners = banners.ToArray();
            catalog.bannerNames = (string[])BannerArt.Names.Clone();
            EditorUtility.SetDirty(catalog);
            AssetDatabase.SaveAssets();

            int missing = avatars.FindAll(s => s == null).Count + banners.FindAll(s => s == null).Count;
            if (missing > 0) report.Error($"Profile art: {missing} sprite(s) failed to import.");
            else report.Pass($"Profile art: {avatars.Count} avatars and {banners.Count} banners generated.");
        }

        // ------------------------------------------------------------------ avatars

        private static void RenderAvatar(AvatarStyle style, WeaponCatalog weapons, string path)
        {
            var root = new GameObject("Avatar Model");
            root.transform.position = new Vector3(0f, -500f, 0f);
            root.transform.rotation = Quaternion.Euler(0f, 180f + style.yaw, 0f);
            SoldierModelBuilder.Rig rig = SoldierModelBuilder.Build(root.transform, "Soldier");
            var temporary = new List<Object>();

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
                temporary.Add(copy);
                return copy;
            }

            Material skinMaterial = null, camoMaterial = null;
            foreach (Renderer r in root.GetComponentsInChildren<Renderer>())
            {
                Material m = r.sharedMaterial;
                switch (m.name)
                {
                    case "M_Soldier_Camo": r.sharedMaterial = camoMaterial = Copy(m, style.camo); break;
                    case "M_Soldier_CamoDark": r.sharedMaterial = Copy(m, style.camo * 0.75f); break;
                    case "M_Soldier_Skin": r.sharedMaterial = skinMaterial = Copy(m, style.skin); break;
                    case "M_Soldier_Team": r.sharedMaterial = Copy(m, style.team); break;
                    case "M_Soldier_Goggles": r.sharedMaterial = Copy(m, style.accent); break;
                }
            }

            Material Flat(Color color, float emission = 0f)
            {
                var material = new Material(Shader.Find("Universal Render Pipeline/Simple Lit"));
                material.SetColor("_BaseColor", color);
                material.SetFloat("_Smoothness", 0.2f);
                if (emission > 0f)
                {
                    material.EnableKeyword("_EMISSION");
                    material.SetColor("_EmissionColor", color * emission);
                }
                temporary.Add(material);
                return material;
            }

            void Part(Vector3 position, Vector3 size, Material material, float rollZ = 0f)
            {
                GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cube);
                Object.DestroyImmediate(go.GetComponent<Collider>());
                go.transform.SetParent(rig.Head, false);
                go.transform.localPosition = position;
                go.transform.localRotation = Quaternion.Euler(0f, 0f, rollZ);
                go.transform.localScale = size;
                go.GetComponent<MeshRenderer>().sharedMaterial = material;
            }

            // Head pieces: helmet group sits above y = 0.28; the lower face mask is the big mask box.
            GameObject lowerMask = rig.FaceMask != null ? rig.FaceMask.gameObject : null;
            void HideHelmet() { if (rig.Helmet != null) rig.Helmet.gameObject.SetActive(false); }
            if (lowerMask != null) lowerMask.SetActive(style.mask);

            Material black = Flat(new Color(0.07f, 0.07f, 0.08f));
            Material darkGear = Flat(new Color(0.16f, 0.17f, 0.15f));
            switch (style.gear)
            {
                case Gear.Beret:
                    HideHelmet();
                    Part(new Vector3(0.03f, 0.385f, 0f), new Vector3(0.43f, 0.07f, 0.44f), Flat(style.accent), -9f);
                    Part(new Vector3(-0.1f, 0.35f, 0.21f), new Vector3(0.06f, 0.06f, 0.012f), Flat(new Color(1f, 0.78f, 0.25f), 0.6f));
                    break;
                case Gear.Boonie:
                    HideHelmet();
                    Part(new Vector3(0f, 0.41f, 0f), new Vector3(0.41f, 0.12f, 0.41f), camoMaterial);
                    Part(new Vector3(0f, 0.355f, 0f), new Vector3(0.64f, 0.025f, 0.64f), camoMaterial);
                    break;
                case Gear.Balaclava:
                    HideHelmet();
                    if (lowerMask != null) lowerMask.SetActive(false);
                    Material wrap = Flat(style.accent * 0.9f + new Color(0.03f, 0.03f, 0.03f));
                    Part(new Vector3(0f, 0.18f, 0f), new Vector3(0.395f, 0.41f, 0.395f), wrap);
                    Part(new Vector3(0f, 0.22f, 0.199f), new Vector3(0.3f, 0.08f, 0.01f), skinMaterial);
                    Part(new Vector3(-0.08f, 0.22f, 0.205f), new Vector3(0.07f, 0.045f, 0.01f), black);
                    Part(new Vector3(0.08f, 0.22f, 0.205f), new Vector3(0.07f, 0.045f, 0.01f), black);
                    break;
                case Gear.Headset:
                    Part(new Vector3(-0.215f, 0.2f, 0f), new Vector3(0.07f, 0.15f, 0.13f), darkGear);
                    Part(new Vector3(0.215f, 0.2f, 0f), new Vector3(0.07f, 0.15f, 0.13f), darkGear);
                    Part(new Vector3(0f, 0.49f, 0f), new Vector3(0.5f, 0.035f, 0.06f), darkGear);
                    Part(new Vector3(0.16f, 0.09f, 0.15f), new Vector3(0.02f, 0.02f, 0.16f), black, 0f);
                    Part(new Vector3(0.24f, 0.2f, 0.02f), new Vector3(0.02f, 0.03f, 0.03f), Flat(style.accent, 1.5f));
                    break;
                case Gear.NightVision:
                    Part(new Vector3(0f, 0.47f, 0.24f), new Vector3(0.1f, 0.07f, 0.06f), darkGear);
                    Part(new Vector3(-0.05f, 0.41f, 0.3f), new Vector3(0.075f, 0.075f, 0.14f), black);
                    Part(new Vector3(0.05f, 0.41f, 0.3f), new Vector3(0.075f, 0.075f, 0.14f), black);
                    Material lens = Flat(new Color(0.3f, 1f, 0.4f), 2.2f);
                    Part(new Vector3(-0.05f, 0.41f, 0.372f), new Vector3(0.055f, 0.055f, 0.01f), lens);
                    Part(new Vector3(0.05f, 0.41f, 0.372f), new Vector3(0.055f, 0.055f, 0.01f), lens);
                    break;
                case Gear.GasMask:
                    if (lowerMask != null) lowerMask.SetActive(false);
                    Part(new Vector3(0f, 0.15f, 0.215f), new Vector3(0.34f, 0.27f, 0.07f), black);
                    Material glass = Flat(style.accent * 0.6f, 0.8f);
                    Part(new Vector3(-0.085f, 0.22f, 0.252f), new Vector3(0.1f, 0.08f, 0.012f), glass);
                    Part(new Vector3(0.085f, 0.22f, 0.252f), new Vector3(0.1f, 0.08f, 0.012f), glass);
                    Part(new Vector3(0f, 0.06f, 0.29f), new Vector3(0.13f, 0.13f, 0.12f), Flat(new Color(0.3f, 0.34f, 0.22f)));
                    break;
                case Gear.Bandana:
                    HideHelmet();
                    Part(new Vector3(0f, 0.36f, 0f), new Vector3(0.39f, 0.04f, 0.39f), Flat(new Color(0.16f, 0.1f, 0.07f)));
                    Part(new Vector3(0f, 0.31f, 0f), new Vector3(0.405f, 0.07f, 0.405f), Flat(style.accent));
                    Part(new Vector3(0f, 0.3f, -0.22f), new Vector3(0.07f, 0.09f, 0.08f), Flat(style.accent * 0.85f));
                    break;
            }
            if (style.facePaint)
            {
                Material paint = Flat(new Color(0.08f, 0.1f, 0.06f));
                Part(new Vector3(-0.09f, 0.185f, 0.1925f), new Vector3(0.08f, 0.022f, 0.005f), paint);
                Part(new Vector3(0.09f, 0.185f, 0.1925f), new Vector3(0.08f, 0.022f, 0.005f), paint);
                Part(new Vector3(0f, 0.3f, 0.1925f), new Vector3(0.22f, 0.02f, 0.005f), paint);
            }

            if (weapons != null && style.weapon >= 0)
            {
                WeaponDefinition def = weapons.Get(style.weapon);
                if (def != null && def.worldModelPrefab != null) Object.Instantiate(def.worldModelPrefab, rig.WeaponSocket, false);
            }

            // Lighting: warm key, strong coloured rim from behind, cool fill.
            var lights = new List<GameObject>();
            Light AddLight(string name, Quaternion rotation, float intensity, Color color)
            {
                var go = new GameObject(name);
                go.transform.rotation = rotation;
                Light light = go.AddComponent<Light>();
                light.type = LightType.Directional;
                light.intensity = intensity;
                light.color = color;
                light.shadows = LightShadows.None;
                lights.Add(go);
                return light;
            }
            Color rimColour = BackdropRim(style.backdrop);
            // The soldier faces -Z (towards the camera): the key shines along +Z onto his face, the rim along -Z from behind.
            AddLight("Key", Quaternion.Euler(24f, 22f, 0f), 1.2f, new Color(1f, 0.9f, 0.78f));
            AddLight("Rim", Quaternion.Euler(12f, 196f, 0f), 1.7f, rimColour);
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.45f, 0.52f, 0.66f);
            RenderSettings.ambientEquatorColor = new Color(0.38f, 0.36f, 0.36f);
            RenderSettings.ambientGroundColor = new Color(0.22f, 0.2f, 0.18f);
            RenderSettings.fog = false;

            Vector3 focus = root.transform.position + new Vector3(0f, 1.58f, 0f);
            var camObject = new GameObject("Avatar Camera");
            camObject.transform.position = focus + new Vector3(0.14f, 0.1f, -1.8f);
            camObject.transform.LookAt(focus);
            Camera cam = camObject.AddComponent<Camera>();
            cam.fieldOfView = 31f;
            cam.nearClipPlane = 0.1f;
            cam.farClipPlane = 10f;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = Color.black;

            // Painted battlefield backdrop on an unlit quad behind the soldier.
            Texture2D backdropTexture = BackdropTexture(style.backdrop);
            temporary.Add(backdropTexture);
            var backdropMaterial = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
            backdropMaterial.SetTexture("_BaseMap", backdropTexture);
            temporary.Add(backdropMaterial);
            GameObject quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
            quad.transform.position = camObject.transform.position + camObject.transform.forward * 3.4f;
            quad.transform.rotation = camObject.transform.rotation;
            quad.transform.localScale = new Vector3(2.2f, 2.2f, 1f);
            quad.GetComponent<MeshRenderer>().sharedMaterial = backdropMaterial;

            var rt = new RenderTexture(AvatarSize, AvatarSize, 24, RenderTextureFormat.ARGB32) { antiAliasing = 4 };
            cam.targetTexture = rt;
            cam.Render();
            cam.Render();
            RenderTexture.active = rt;
            var texture = new Texture2D(AvatarSize, AvatarSize, TextureFormat.RGBA32, false);
            texture.ReadPixels(new Rect(0, 0, AvatarSize, AvatarSize), 0, 0);
            RenderTexture.active = null;
            cam.targetTexture = null;

            // Grade: a touch of contrast and a dark vignette.
            for (int y = 0; y < AvatarSize; y++)
            {
                for (int x = 0; x < AvatarSize; x++)
                {
                    float d = new Vector2(x / (float)AvatarSize - 0.5f, y / (float)AvatarSize - 0.55f).magnitude;
                    Color c = texture.GetPixel(x, y);
                    c = new Color(Contrast(c.r), Contrast(c.g), Contrast(c.b)) * Mathf.Lerp(1.05f, 0.55f, Mathf.SmoothStep(0.25f, 0.75f, d));
                    c.a = 1f;
                    texture.SetPixel(x, y, c);
                }
            }
            texture.Apply();
            File.WriteAllBytes(path, texture.EncodeToPNG());

            Object.DestroyImmediate(texture);
            rt.Release();
            Object.DestroyImmediate(rt);
            Object.DestroyImmediate(quad);
            Object.DestroyImmediate(camObject);
            foreach (GameObject go in lights) Object.DestroyImmediate(go);
            Object.DestroyImmediate(root);
            foreach (Object o in temporary) Object.DestroyImmediate(o);
        }

        private static float Contrast(float v) => Mathf.Clamp01((v - 0.5f) * 1.12f + 0.5f);

        private static Color BackdropRim(Backdrop backdrop)
        {
            switch (backdrop)
            {
                case Backdrop.NightOps: return new Color(0.4f, 1f, 0.5f);
                case Backdrop.Arctic: return new Color(0.6f, 0.8f, 1f);
                case Backdrop.SteelStorm: return new Color(0.55f, 0.7f, 1f);
                case Backdrop.DesertStorm: return new Color(1f, 0.78f, 0.45f);
                default: return new Color(1f, 0.45f, 0.15f);
            }
        }

        /// <summary>256² painted battlefield: sky gradient, glow, ruined skyline, smoke, embers or snow.</summary>
        private static Texture2D BackdropTexture(Backdrop backdrop)
        {
            const int size = 256;
            var texture = new Texture2D(size, size, TextureFormat.RGB24, false);
            Color top, middle, bottom, glow, skyline, particle;
            switch (backdrop)
            {
                case Backdrop.NightOps: top = BannerArt.Hex(0x02080A); middle = BannerArt.Hex(0x0B2A1E); bottom = BannerArt.Hex(0x154A2C); glow = new Color(0.3f, 0.9f, 0.4f); skyline = BannerArt.Hex(0x020604); particle = new Color(0.7f, 1f, 0.7f); break;
                case Backdrop.DesertStorm: top = BannerArt.Hex(0x8C6A44); middle = BannerArt.Hex(0xC9965A); bottom = BannerArt.Hex(0xE8C08A); glow = new Color(1f, 0.92f, 0.7f); skyline = BannerArt.Hex(0x4A3220); particle = new Color(0.95f, 0.85f, 0.65f); break;
                case Backdrop.Inferno: top = BannerArt.Hex(0x120404); middle = BannerArt.Hex(0x6A1208); bottom = BannerArt.Hex(0xE04A12); glow = new Color(1f, 0.5f, 0.15f); skyline = BannerArt.Hex(0x0A0303); particle = new Color(1f, 0.6f, 0.2f); break;
                case Backdrop.Arctic: top = BannerArt.Hex(0x4C6688); middle = BannerArt.Hex(0x9DB6CF); bottom = BannerArt.Hex(0xE2ECF4); glow = new Color(0.9f, 0.95f, 1f); skyline = BannerArt.Hex(0x2A3A4E); particle = Color.white; break;
                case Backdrop.SteelStorm: top = BannerArt.Hex(0x10161E); middle = BannerArt.Hex(0x34465A); bottom = BannerArt.Hex(0x6A7E92); glow = new Color(0.7f, 0.8f, 1f); skyline = BannerArt.Hex(0x0A0E14); particle = new Color(0.8f, 0.85f, 0.9f); break;
                default: top = BannerArt.Hex(0x2A0E1C); middle = BannerArt.Hex(0xB2401E); bottom = BannerArt.Hex(0xFFAE4A); glow = new Color(1f, 0.7f, 0.3f); skyline = BannerArt.Hex(0x120806); particle = new Color(1f, 0.65f, 0.25f); break;
            }
            bool snow = backdrop == Backdrop.Arctic;
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float u = x / (size - 1f), v = y / (size - 1f);
                    Color c = v > 0.45f ? Color.Lerp(middle, top, (v - 0.45f) / 0.55f) : Color.Lerp(bottom, middle, v / 0.45f);
                    c += glow * Mathf.Exp(-new Vector2((u - 0.35f) * 2.2f, (v - 0.32f) * 3f).sqrMagnitude) * 0.45f;
                    float smoke = BannerArt.Noise(x * 0.025f, y * 0.04f) * 0.6f + BannerArt.Noise(x * 0.07f, y * 0.09f) * 0.4f;
                    c = Color.Lerp(c, skyline * 1.6f + new Color(0.05f, 0.05f, 0.05f), Mathf.Clamp01(smoke * 1.3f - 0.55f) * Mathf.Clamp01(1.2f - v));
                    int column = x / 14;
                    float building = 0.18f + 0.22f * BannerArt.Hash(column, 11) * (BannerArt.Hash(column, 3) > 0.35f ? 1f : 0.3f);
                    building -= (BannerArt.Hash(x / 4, 19) > 0.8f ? 0.03f : 0f);                       // broken tops
                    if (v < building)
                    {
                        c = skyline;
                        if (BannerArt.Hash(x / 3, y / 4) > 0.97f && v < building - 0.04f) c = glow * 0.9f; // burning windows
                    }
                    float speck = BannerArt.Hash(x, y * 3 + 7);
                    if (speck > (snow ? 0.985f : 0.993f)) c = Color.Lerp(c, particle, snow ? 0.9f : 0.8f);
                    texture.SetPixel(x, y, c);
                }
            }
            texture.Apply();
            texture.wrapMode = TextureWrapMode.Clamp;
            return texture;
        }

        // ------------------------------------------------------------------ helpers

        private static void ClearFolder(string folder)
        {
            if (!AssetDatabase.IsValidFolder(folder)) return;
            foreach (string guid in AssetDatabase.FindAssets("t:Texture2D", new[] { folder }))
            {
                AssetDatabase.DeleteAsset(AssetDatabase.GUIDToAssetPath(guid));
            }
        }

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

        /// <summary>Imports as a sprite with mobile compression (ASTC on Android) to keep the APK small.</summary>
        private static Sprite ImportSprite(string path, int maxSize)
        {
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
            if (AssetImporter.GetAtPath(path) is TextureImporter importer)
            {
                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.mipmapEnabled = false;
                importer.wrapMode = TextureWrapMode.Clamp;
                importer.filterMode = FilterMode.Bilinear;
                importer.maxTextureSize = maxSize;
                importer.textureCompression = TextureImporterCompression.Compressed;
                importer.SetPlatformTextureSettings(new TextureImporterPlatformSettings
                {
                    name = "Android",
                    overridden = true,
                    maxTextureSize = maxSize,
                    format = TextureImporterFormat.ASTC_5x5,
                    compressionQuality = 50,
                });
                importer.SaveAndReimport();
            }
            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }
    }
}
