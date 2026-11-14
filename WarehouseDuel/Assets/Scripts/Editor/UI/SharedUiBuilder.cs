using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UI;
using WarehouseWar.EditorTools.Gameplay;
using WarehouseWar.EditorTools.Map;
using WarehouseWar.Multiplayer;
using WarehouseWar.UI;
using WarehouseWar.UI.Widgets;
using WarehouseWar.Weapons;
using static WarehouseWar.EditorTools.UI.UiFactory;

namespace WarehouseWar.EditorTools.UI
{
    /// <summary>Pieces shared by several scenes: the menu diorama, the settings panel and the session bootstrap.</summary>
    internal static class SharedUiBuilder
    {
        /// <summary>Where the showcase soldier stands and how the camera frames it.</summary>
        public struct Staging
        {
            public Vector3 Character;
            public float CharacterYaw;
            public Vector3 Camera;
            public Vector3 LookAt;
            public float FieldOfView;
        }

        public static readonly Staging LobbyStaging = new Staging
        {
            Character = new Vector3(0f, 0f, 0f), CharacterYaw = 128f, // 3/4 view so the held gun is seen side-on
            Camera = new Vector3(0f, 1.45f, -6.2f), LookAt = new Vector3(0f, 1.05f, 0f), FieldOfView = 34f,
        };

        public static readonly Staging MenuStaging = new Staging
        {
            Character = new Vector3(0f, 0f, 0f), CharacterYaw = 132f, // 3/4 view so the held gun is seen side-on
            Camera = new Vector3(0f, 1.4f, -6.6f), LookAt = new Vector3(0f, 1.0f, 0f), FieldOfView = 33f,
        };

        public struct SoldierAnimatorHandle
        {
            public WarehouseWar.Player.SoldierAnimator Animator;
            public Transform Turntable;
        }

        /// <summary>
        /// Lit warehouse corner with the voxel soldier on a podium (idle-animated, holding a weapon),
        /// a spotlight and floating dust. Returns the animator and the turntable (for drag-to-rotate).
        /// </summary>
        public static SoldierAnimatorHandle Diorama(WeaponCatalog catalog, int weapon, Staging staging)
        {
            GameObject Prefab(string name) => AssetDatabase.LoadAssetAtPath<GameObject>($"{EnvironmentKit.PrefabFolder}/{name}.prefab");
            void Place(string name, Vector3 position, float rotationY)
            {
                GameObject prefab = Prefab(name);
                if (prefab == null) return;
                var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
                instance.transform.SetPositionAndRotation(position, Quaternion.Euler(0f, rotationY, 0f));
            }

            var root = new GameObject("Diorama");
            EnvironmentKit.Box(root.transform, "Floor", new Vector3(0f, -0.25f, 4f), new Vector3(30f, 0.5f, 24f), EnvironmentPalette.Id.ConcreteFloor, false);
            EnvironmentKit.Box(root.transform, "BackWall", new Vector3(0f, 4.5f, 12f), new Vector3(30f, 9f, 0.5f), EnvironmentPalette.Id.MetalWall, false);
            for (float x = -13.5f; x <= 13.6f; x += 3f)
            {
                EnvironmentKit.NoShadow(EnvironmentKit.Box(root.transform, "Rib", new Vector3(x, 4.5f, 11.7f), new Vector3(0.25f, 9f, 0.12f), EnvironmentPalette.Id.MetalWallRib, false));
            }

            Place("ContainerRed", new Vector3(-6.5f, 0f, 8f), 90f);
            Place("ContainerBlue", new Vector3(-6.5f, 2.6f, 8.2f), 92f);
            Place("ContainerGreen", new Vector3(7.5f, 0f, 9f), 90f);
            Place("Crate", new Vector3(3.6f, 0f, 5.5f), 12f);
            Place("Crate", new Vector3(4.9f, 0f, 6.2f), -8f);
            Place("CrateSmall", new Vector3(4.1f, 1.2f, 5.8f), 30f);
            Place("BarrelRed", new Vector3(-3.4f, 0f, 5.5f), 0f);
            Place("BarrelBlue", new Vector3(-2.7f, 0f, 6f), 0f);
            Place("StorageRack", new Vector3(11f, 0f, 6f), 0f);
            Place("Pillar", new Vector3(-10f, 0f, 4f), 0f);
            Place("HangingLamp", new Vector3(0f, 6.5f, 3f), 0f);
            Place("RollUpDoor", new Vector3(1f, 0f, 11.7f), 180f);

            // Podium + character on a turntable.
            var podium = new GameObject("Podium");
            podium.transform.position = staging.Character;
            EnvironmentKit.Cylinder(podium.transform, "Base", new Vector3(0f, 0.06f, 0f), 2.3f, 0.12f, EnvironmentPalette.Id.MachineDark);
            EnvironmentKit.NoShadow(EnvironmentKit.Cylinder(podium.transform, "Ring", new Vector3(0f, 0.125f, 0f), 2.38f, 0.02f, EnvironmentPalette.Id.Hazard));
            EnvironmentKit.NoShadow(EnvironmentKit.Cylinder(podium.transform, "Top", new Vector3(0f, 0.13f, 0f), 2.1f, 0.02f, EnvironmentPalette.Id.Steel));

            var turntable = new GameObject("Turntable");
            turntable.transform.SetPositionAndRotation(staging.Character + Vector3.up * 0.14f, Quaternion.Euler(0f, staging.CharacterYaw, 0f));
            SoldierModelBuilder.Rig soldier = SoldierModelBuilder.Build(turntable.transform, "Soldier");
            Material blue = TeamMaterial();
            foreach (Renderer r in soldier.TeamColored) r.sharedMaterial = blue;
            var animator = soldier.Root.gameObject.AddComponent<WarehouseWar.Player.SoldierAnimator>();
            Wire(animator, "hips", soldier.Hips);
            Wire(animator, "spine", soldier.Spine);
            Wire(animator, "head", soldier.Head);
            Wire(animator, "armsRig", soldier.ArmsRig);
            Wire(animator, "weaponSocket", soldier.WeaponSocket);
            Wire(animator, "catalog", catalog);
            var so = new SerializedObject(animator);
            so.FindProperty("startWeapon").intValue = weapon;
            so.ApplyModifiedPropertiesWithoutUndo();

            // Lighting, cinematic: warm key spotlight, an orange rim light from behind (sunset through the
            // high windows), a low golden sun for long shadows, cool ambient, dark warm haze.
            var spotObject = new GameObject("Key Spotlight");
            spotObject.transform.position = staging.Character + new Vector3(-0.8f, 4.4f, -2.4f);
            spotObject.transform.LookAt(staging.Character + Vector3.up * 1.1f);
            Light spot = spotObject.AddComponent<Light>();
            spot.type = LightType.Spot;
            spot.range = 10f;
            spot.spotAngle = 50f;
            spot.intensity = 15f;
            spot.color = new Color(1f, 0.88f, 0.72f);
            spot.shadows = LightShadows.None;

            var rimObject = new GameObject("Rim Light");
            rimObject.transform.position = staging.Character + new Vector3(0.6f, 3.4f, 2.6f);
            rimObject.transform.LookAt(staging.Character + Vector3.up * 1.3f);
            Light rim = rimObject.AddComponent<Light>();
            rim.type = LightType.Spot;
            rim.range = 8f;
            rim.spotAngle = 60f;
            rim.intensity = 22f;
            rim.color = new Color(1f, 0.5f, 0.2f);
            rim.shadows = LightShadows.None;

            var sunObject = new GameObject("Sun");
            sunObject.transform.rotation = Quaternion.Euler(28f, 200f, 0f);
            Light sun = sunObject.AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.intensity = 1.3f;
            sun.color = new Color(1f, 0.72f, 0.48f);
            sun.shadows = LightShadows.Soft;
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.58f, 0.64f, 0.78f);
            RenderSettings.ambientEquatorColor = new Color(0.52f, 0.47f, 0.44f);
            RenderSettings.ambientGroundColor = new Color(0.34f, 0.3f, 0.27f);
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogColor = new Color(0.32f, 0.23f, 0.18f);
            RenderSettings.fogStartDistance = 10f;
            RenderSettings.fogEndDistance = 46f;

            // Burning-sunset windows high on the back wall, with beams falling towards the camera.
            foreach (float x in new[] { -9.5f, -5.5f, 5.5f, 9.5f })
            {
                EnvironmentKit.NoShadow(EnvironmentKit.Box(root.transform, "Window", new Vector3(x, 5.0f, 11.72f), new Vector3(2.6f, 1.4f, 0.06f), EnvironmentPalette.Id.Skylight, false));
                EnvironmentKit.NoShadow(EnvironmentKit.Box(root.transform, "Window Frame", new Vector3(x, 5.0f, 11.69f), new Vector3(0.12f, 1.4f, 0.08f), EnvironmentPalette.Id.Steel, false));
                AtmosphereKit.LightShaft(root.transform, "Window Beam", new Vector3(x, 5.4f, 11.6f), new Vector3(-x * 0.03f, -0.45f, -1f), Vector3.right,
                    13f, 2.6f, new Color(0.15f, 0.085f, 0.04f));
            }

            Dust(staging.Character);
            AtmosphereKit.Embers(staging.Character + new Vector3(0f, 0.1f, 1.5f), new Vector3(12f, 0.5f, 7f), 7f);
            AtmosphereKit.Smoke(new Vector3(0f, 2.2f, 8.5f), new Vector3(24f, 2f, 4f), new Color(0.32f, 0.24f, 0.2f, 0.16f), 1.4f);
            AtmosphereKit.AddGlobalVolume(AtmosphereKit.Look.Showroom);

            var camObject = new GameObject("Main Camera") { tag = "MainCamera" };
            camObject.transform.position = staging.Camera;
            camObject.transform.LookAt(staging.LookAt);
            Camera cam = camObject.AddComponent<Camera>();
            cam.fieldOfView = staging.FieldOfView;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.12f, 0.1f, 0.1f);
            cam.farClipPlane = 60f;
            camObject.AddComponent<AudioListener>();
            AtmosphereKit.EnableCameraEffects(cam);

            var backdrop = root.AddComponent<MenuBackdrop>();
            Wire(backdrop, "cameraTransform", camObject.transform);
            SetFloat(backdrop, "driftAmplitude", 0.12f);

            return new SoldierAnimatorHandle { Animator = animator, Turntable = turntable.transform };
        }

        private static Material TeamMaterial()
        {
            const string path = "Assets/Art/Materials/Characters/M_Soldier_TeamBlue.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(Shader.Find("Universal Render Pipeline/Simple Lit"));
                AssetDatabase.CreateAsset(material, path);
            }
            material.SetColor("_BaseColor", new Color(0.2f, 0.55f, 1f));
            material.SetFloat("_Smoothness", 0.2f);
            EditorUtility.SetDirty(material);
            return material;
        }

        /// <summary>Slow floating dust motes in the spotlight (cheap: ~30 particles).</summary>
        private static void Dust(Vector3 centre)
        {
            var go = new GameObject("Dust");
            go.transform.position = centre + new Vector3(0f, 1.6f, 0.5f);
            var ps = go.AddComponent<ParticleSystem>();
            var main = ps.main;
            main.loop = true;
            main.duration = 10f;
            main.startLifetime = new ParticleSystem.MinMaxCurve(6f, 10f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.02f, 0.08f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.025f, 0.06f);
            main.startColor = new Color(1f, 0.92f, 0.75f, 0.45f);
            main.maxParticles = 40;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.prewarm = true;
            var emission = ps.emission;
            emission.rateOverTime = 4f;
            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = new Vector3(5f, 3f, 3f);
            var color = ps.colorOverLifetime;
            color.enabled = true;
            var gradient = new Gradient();
            gradient.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.3f), new GradientAlphaKey(1f, 0.7f), new GradientAlphaKey(0f, 1f) });
            color.color = gradient;
            var renderer = go.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = AssetDatabase.LoadAssetAtPath<Material>("Assets/Art/Materials/Effects/M_FX_Glow.mat");
            renderer.shadowCastingMode = ShadowCastingMode.Off;
        }

        public static void SessionBootstrap()
        {
            var go = new GameObject("Session Bootstrap");
            var bootstrap = go.AddComponent<WarehouseWar.Multiplayer.SessionBootstrap>();
            Wire(bootstrap, "sessionServicePrefab", GameplayPrefabBuilder.SessionServicePrefab);
        }

        /// <summary>A darkened full-screen layer with a centred card, for modal panels.</summary>
        public static RectTransform Modal(string name, Transform parent, Vector2 size, out GameObject root)
        {
            Image dim = CreateImage(name, parent, new Color(0f, 0f, 0f, 0.6f), null, raycast: true);
            Stretch(dim.rectTransform, -200f, -200f, -200f, -200f); // extend past the safe area
            root = dim.gameObject;
            Image card = CreatePanel("Card", dim.transform);
            Place(card.rectTransform, new Vector2(0.5f, 0.5f), Vector2.zero, size, new Vector2(0.5f, 0.5f));
            dim.gameObject.AddComponent<CanvasGroup>();
            PopupFx fx = dim.gameObject.AddComponent<PopupFx>();
            Wire(fx, "content", card.rectTransform);
            return card.rectTransform;
        }

        /// <summary>Loading spinner: accent arc + counter-rotating inner arc + pulsing core.</summary>
        public static RectTransform CreateSpinner(Transform parent, float size)
        {
            RectTransform root = CreateRect("Spinner", parent);
            root.sizeDelta = new Vector2(size, size);
            Image glow = CreateImage("Glow", root, new Color(1f, 0.69f, 0.13f, 0.18f), UiArt.Circle);
            Stretch(glow.rectTransform, -size * 0.2f, -size * 0.2f, -size * 0.2f, -size * 0.2f);
            Image outer = CreateImage("Outer", root, Accent, UiArt.Arc);
            Stretch(outer.rectTransform);
            Image inner = CreateImage("Inner", root, new Color(1f, 1f, 1f, 0.55f), UiArt.Arc);
            Stretch(inner.rectTransform, size * 0.2f, size * 0.2f, size * 0.2f, size * 0.2f);
            inner.rectTransform.localScale = new Vector3(-1f, 1f, 1f);
            Image core = CreateImage("Core", root, Accent, UiArt.Circle);
            Stretch(core.rectTransform, size * 0.4f, size * 0.4f, size * 0.4f, size * 0.4f);
            Spinner spinner = root.gameObject.AddComponent<Spinner>();
            Wire(spinner, "outer", outer.rectTransform);
            Wire(spinner, "inner", inner.rectTransform);
            Wire(spinner, "core", core.rectTransform);
            return root;
        }

        /// <summary>
        /// Settings screen (main menu and pause menu): tabs on the left (Controls, Camera, Graphics, Audio, About),
        /// one card per setting with a short description, switches for on/off options, value labels, and a
        /// reset button. Returns the root object and its Done button.
        /// </summary>
        public static GameObject Settings(Transform parent, out Button closeButton)
        {
            // Full-screen page: an opaque backdrop covers everything (menu or match); the content stays centred.
            RectTransform card = Modal("Settings Panel", parent, new Vector2(1640f, 960f), out GameObject root);
            root.GetComponent<Image>().color = new Color(0.06f, 0.07f, 0.1f, 1f);
            card.GetComponent<Image>().color = new Color(1f, 1f, 1f, 0f);
            Image glow = CreateImage("Glow", root.transform, new Color(1f, 0.55f, 0.15f, 0.07f), UiArt.Gradient);
            Stretch(glow.rectTransform);
            glow.transform.SetSiblingIndex(0);

            Image header = CreateImage("Header", card, new Color(0f, 0f, 0f, 0.3f), UiArt.Rounded);
            MenuScreensBuilder.At(header.rectTransform, 0f, 0f, 1640f, 120f);
            Image gear = CreateImage("Icon", header.transform, Accent, UiArt.Icon("Settings"));
            MenuScreensBuilder.At(gear.rectTransform, 44f, -30f, 60f, 60f);
            MenuScreensBuilder.Label(header.transform, "Title", "SETTINGS", 54f, TextPrimary, 124f, -22f, 700f, 76f, display: true);

            string[] tabNames = { "CONTROLS", "CAMERA", "GRAPHICS", "AUDIO", "ABOUT" };
            MenuScreensBuilder.Tabs(card, tabNames, 40f, -150f, 320f, 92f, 14f, true,
                out Button[] tabButtons, out Image[] tabBackgrounds, out TMP_Text[] tabLabels);

            var pages = new GameObject[tabNames.Length];
            RectTransform Page(int index)
            {
                RectTransform page = MenuScreensBuilder.At(CreateRect(tabNames[index] + " Page", card), 400f, -150f, 1200f, 640f);
                pages[index] = page.gameObject;
                page.gameObject.SetActive(index == 0);
                return page;
            }

            float y = 0f;
            RectTransform Row(RectTransform page, string label, string description)
            {
                Image row = MenuScreensBuilder.Panel(page, label, 0f, y, 1200f, 136f, new Color(1f, 1f, 1f, 0.06f));
                MenuScreensBuilder.Label(row.transform, "Label", label, 32f, TextPrimary, 36f, -24f, 600f, 44f, display: true);
                MenuScreensBuilder.Label(row.transform, "Description", description, 21f, TextMuted, 36f, -74f, 600f, 50f);
                RectTransform slot = CreateRect("Control", row.transform);
                Place(slot, new Vector2(1f, 0.5f), new Vector2(-36f, 0f), new Vector2(500f, 80f), new Vector2(1f, 0.5f));
                y -= 152f;
                return slot;
            }

            Slider SliderRow(RectTransform page, string label, string description, out TextMeshProUGUI value)
            {
                RectTransform slot = Row(page, label, description);
                Slider slider = CreateSlider("Slider", slot, new Vector2(370f, 52f));
                Place((RectTransform)slider.transform, new Vector2(0f, 0.5f), Vector2.zero, new Vector2(370f, 52f), new Vector2(0f, 0.5f));
                value = CreateText("Value", slot, "", 30f, Accent, TextAlignmentOptions.Right, display: true);
                Place(value.rectTransform, new Vector2(1f, 0.5f), Vector2.zero, new Vector2(110f, 50f), new Vector2(1f, 0.5f));
                return slider;
            }

            OptionSelector SelectorRow(RectTransform page, string label, string description)
            {
                RectTransform slot = Row(page, label, description);
                OptionSelector selector = CreateSelector("Selector", slot, new Vector2(500f, 76f));
                Place((RectTransform)selector.transform, new Vector2(1f, 0.5f), Vector2.zero, new Vector2(500f, 76f), new Vector2(1f, 0.5f));
                return selector;
            }

            Toggle SwitchRow(RectTransform page, string label, string description)
            {
                RectTransform slot = Row(page, label, description);
                RectTransform switchRoot = CreateRect("Switch", slot);
                Place(switchRoot, new Vector2(1f, 0.5f), Vector2.zero, new Vector2(130f, 64f), new Vector2(1f, 0.5f));
                Toggle toggle = switchRoot.gameObject.AddComponent<Toggle>();
                Image track = CreateImage("Track", switchRoot, new Color(1f, 1f, 1f, 0.18f), UiArt.Rounded, raycast: true);
                Stretch(track.rectTransform);
                Image knob = CreateImage("Knob", track.transform, Color.white, UiArt.Circle);
                knob.rectTransform.sizeDelta = new Vector2(52f, 52f);
                knob.rectTransform.anchorMin = knob.rectTransform.anchorMax = new Vector2(0.27f, 0.5f);
                toggle.targetGraphic = track;
                toggle.transition = Selectable.Transition.None;
                switchRoot.gameObject.AddComponent<UIButtonSound>();
                ToggleSwitch view = switchRoot.gameObject.AddComponent<ToggleSwitch>();
                Wire(view, "track", track);
                Wire(view, "knob", knob.rectTransform);
                return toggle;
            }

            RectTransform controls = Page(0);
            y = 0f;
            OptionSelector layout = SelectorRow(controls, "Button layout", "Where the joystick and fire buttons sit on screen.");
            Slider buttonSize = SliderRow(controls, "Button size", "Make the touch buttons bigger or smaller.", out TextMeshProUGUI buttonSizeValue);
            Toggle invert = SwitchRow(controls, "Invert look", "Swipe up to look down (flight-style controls).");
            Toggle vibration = SwitchRow(controls, "Vibration", "Short buzz on button taps and when you get hit.");

            RectTransform camera = Page(1);
            y = 0f;
            Slider look = SliderRow(camera, "Look sensitivity", "How fast the camera turns when you swipe.", out TextMeshProUGUI lookValue);
            Slider aim = SliderRow(camera, "Aim sensitivity", "Turn speed while aiming down sights.", out TextMeshProUGUI aimValue);

            RectTransform graphics = Page(2);
            y = 0f;
            OptionSelector quality = SelectorRow(graphics, "Graphics quality", "Lower it if your phone gets hot or stutters.");
            OptionSelector fps = SelectorRow(graphics, "Frame rate", "60 FPS is smoother; 30 FPS saves battery.");
            Toggle postEffects = SwitchRow(graphics, "Cinematic effects", "Colour grading, glow and smooth edges. Turn off on slower phones.");

            RectTransform audio = Page(3);
            y = 0f;
            Slider music = SliderRow(audio, "Music", "Menu and match music volume.", out TextMeshProUGUI musicValue);
            Slider sfx = SliderRow(audio, "Sound effects", "Gunshots, footsteps and interface sounds.", out TextMeshProUGUI sfxValue);

            RectTransform about = Page(4);
            Image aboutCard = MenuScreensBuilder.Panel(about, "About Card", 0f, 0f, 1200f, 420f, new Color(1f, 1f, 1f, 0.06f));
            MenuScreensBuilder.Label(aboutCard.transform, "Game", "WAREHOUSE <color=#FFB020>WAR</color>", 64f, TextPrimary, 40f, -36f, 1100f, 84f, display: true);
            TextMeshProUGUI version = MenuScreensBuilder.Label(aboutCard.transform, "Version", "Version", 28f, Accent, 44f, -128f, 1100f, 40f, display: true);
            MenuScreensBuilder.Label(aboutCard.transform, "Text",
                "Online arena shooter for up to 8 players. Create a room, share the 6-digit code with your friends and fight it out on four maps - or practise against bots.\n\nBuilt with Unity and Photon Fusion.",
                26f, TextMuted, 44f, -190f, 1100f, 200f).alignment = TextAlignmentOptions.TopLeft;

            Button reset = CreateButton("Reset", card, "RESET TO DEFAULTS", ButtonStyle.Ghost, new Vector2(400f, 96f), 28f);
            Place((RectTransform)reset.transform, new Vector2(0f, 0f), new Vector2(40f, 36f), new Vector2(400f, 96f), new Vector2(0f, 0f));
            closeButton = CreateButton("Close", card, "DONE", ButtonStyle.Primary, new Vector2(360f, 100f), 40f);
            Place((RectTransform)closeButton.transform, new Vector2(1f, 0f), new Vector2(-40f, 36f), new Vector2(360f, 100f), new Vector2(1f, 0f));

            tabBackgrounds[0].color = Accent;
            tabLabels[0].color = AccentDark;

            SettingsPanel panel = root.AddComponent<SettingsPanel>();
            Wire(panel, "musicVolume", music);
            Wire(panel, "sfxVolume", sfx);
            Wire(panel, "lookSensitivity", look);
            Wire(panel, "aimSensitivity", aim);
            Wire(panel, "buttonSize", buttonSize);
            Wire(panel, "invertY", invert);
            Wire(panel, "vibration", vibration);
            Wire(panel, "postEffects", postEffects);
            Wire(panel, "quality", quality);
            Wire(panel, "frameRate", fps);
            Wire(panel, "layout", layout);
            Wire(panel, "lookValue", lookValue);
            Wire(panel, "aimValue", aimValue);
            Wire(panel, "musicValue", musicValue);
            Wire(panel, "sfxValue", sfxValue);
            Wire(panel, "buttonSizeValue", buttonSizeValue);
            WireArray(panel, "tabButtons", tabButtons);
            WireArray(panel, "tabBackgrounds", tabBackgrounds);
            WireArray(panel, "tabLabels", tabLabels);
            WireArray(panel, "pages", pages);
            Wire(panel, "resetButton", reset);
            Wire(panel, "versionText", version);
            return root;
        }
    }
}
