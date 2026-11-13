using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UI;
using WarehouseDuel.EditorTools.Gameplay;
using WarehouseDuel.EditorTools.Map;
using WarehouseDuel.Multiplayer;
using WarehouseDuel.UI;
using WarehouseDuel.UI.Widgets;
using WarehouseDuel.Weapons;
using static WarehouseDuel.EditorTools.UI.UiFactory;

namespace WarehouseDuel.EditorTools.UI
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
            Character = new Vector3(0f, 0f, 0f), CharacterYaw = 148f,
            Camera = new Vector3(0f, 1.45f, -6.2f), LookAt = new Vector3(0f, 1.05f, 0f), FieldOfView = 34f,
        };

        public static readonly Staging MenuStaging = new Staging
        {
            Character = new Vector3(1.55f, 0f, 0.4f), CharacterYaw = 218f,
            Camera = new Vector3(0f, 1.35f, -4.6f), LookAt = new Vector3(0.5f, 0.95f, 0f), FieldOfView = 40f,
        };

        public struct SoldierAnimatorHandle
        {
            public WarehouseDuel.Player.SoldierAnimator Animator;
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
            var animator = soldier.Root.gameObject.AddComponent<WarehouseDuel.Player.SoldierAnimator>();
            Wire(animator, "hips", soldier.Hips);
            Wire(animator, "spine", soldier.Spine);
            Wire(animator, "head", soldier.Head);
            Wire(animator, "armsRig", soldier.ArmsRig);
            Wire(animator, "weaponSocket", soldier.WeaponSocket);
            Wire(animator, "catalog", catalog);
            var so = new SerializedObject(animator);
            so.FindProperty("startWeapon").intValue = weapon;
            so.ApplyModifiedPropertiesWithoutUndo();

            // Lighting: warm key spotlight on the character, sun for shadows, bright trilight ambient.
            var spotObject = new GameObject("Key Spotlight");
            spotObject.transform.position = staging.Character + new Vector3(-0.6f, 4.4f, -2.2f);
            spotObject.transform.LookAt(staging.Character + Vector3.up * 1.1f);
            Light spot = spotObject.AddComponent<Light>();
            spot.type = LightType.Spot;
            spot.range = 10f;
            spot.spotAngle = 48f;
            spot.intensity = 9f;
            spot.color = new Color(1f, 0.92f, 0.8f);
            spot.shadows = LightShadows.None;

            var sunObject = new GameObject("Sun");
            sunObject.transform.rotation = Quaternion.Euler(42f, -40f, 0f);
            Light sun = sunObject.AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.intensity = 1.1f;
            sun.color = new Color(1f, 0.93f, 0.82f);
            sun.shadows = LightShadows.Soft;
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.62f, 0.68f, 0.8f);
            RenderSettings.ambientEquatorColor = new Color(0.5f, 0.48f, 0.47f);
            RenderSettings.ambientGroundColor = new Color(0.32f, 0.3f, 0.28f);
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogColor = new Color(0.12f, 0.14f, 0.18f);
            RenderSettings.fogStartDistance = 9f;
            RenderSettings.fogEndDistance = 42f;

            Dust(staging.Character);

            var camObject = new GameObject("Main Camera") { tag = "MainCamera" };
            camObject.transform.position = staging.Camera;
            camObject.transform.LookAt(staging.LookAt);
            Camera cam = camObject.AddComponent<Camera>();
            cam.fieldOfView = staging.FieldOfView;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.12f, 0.14f, 0.18f);
            cam.farClipPlane = 60f;
            camObject.AddComponent<AudioListener>();

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
            var bootstrap = go.AddComponent<WarehouseDuel.Multiplayer.SessionBootstrap>();
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
            return card.rectTransform;
        }

        /// <summary>Settings modal. Returns the root object and its Close button.</summary>
        public static GameObject Settings(Transform parent, out Button closeButton)
        {
            RectTransform card = Modal("Settings Panel", parent, new Vector2(1200f, 900f), out GameObject root);
            TextMeshProUGUI title = CreateText("Title", card, "SETTINGS", 56f, Accent, TextAlignmentOptions.Center, display: true);
            Place(title.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -30f), new Vector2(800f, 80f), new Vector2(0.5f, 1f));

            float y = -130f;
            const float rowHeight = 80f;
            RectTransform Row(string label)
            {
                TextMeshProUGUI text = CreateText(label, card, label, 32f, TextPrimary, TextAlignmentOptions.Left);
                Place(text.rectTransform, new Vector2(0f, 1f), new Vector2(80f, y), new Vector2(460f, 60f), new Vector2(0f, 1f));
                RectTransform slot = CreateRect(label + " Control", card);
                Place(slot, new Vector2(0f, 1f), new Vector2(560f, y), new Vector2(560f, 60f), new Vector2(0f, 1f));
                y -= rowHeight;
                return slot;
            }

            Slider SliderRow(string label, out TextMeshProUGUI value)
            {
                RectTransform slot = Row(label);
                Slider slider = CreateSlider("Slider", slot, new Vector2(440f, 44f));
                Place((RectTransform)slider.transform, new Vector2(0f, 0.5f), Vector2.zero, new Vector2(440f, 44f), new Vector2(0f, 0.5f));
                value = CreateText("Value", slot, "", 28f, TextMuted, TextAlignmentOptions.Right);
                Place(value.rectTransform, new Vector2(1f, 0.5f), Vector2.zero, new Vector2(100f, 50f), new Vector2(1f, 0.5f));
                return slider;
            }

            OptionSelector SelectorRow(string label)
            {
                RectTransform slot = Row(label);
                OptionSelector selector = CreateSelector("Selector", slot, new Vector2(560f, 60f));
                Place((RectTransform)selector.transform, new Vector2(0f, 0.5f), Vector2.zero, new Vector2(560f, 60f), new Vector2(0f, 0.5f));
                return selector;
            }

            Slider music = SliderRow("Music volume", out _);
            Slider sfx = SliderRow("Sound effects volume", out _);
            Slider look = SliderRow("Camera sensitivity", out TextMeshProUGUI lookValue);
            Slider aim = SliderRow("Aim sensitivity", out TextMeshProUGUI aimValue);
            RectTransform invertSlot = Row("Invert vertical look");
            Toggle invert = CreateToggle("Toggle", invertSlot, 52f);
            Place((RectTransform)invert.transform, new Vector2(0f, 0.5f), Vector2.zero, new Vector2(52f, 52f), new Vector2(0f, 0.5f));
            OptionSelector quality = SelectorRow("Graphics quality");
            OptionSelector fps = SelectorRow("Frame rate limit");
            OptionSelector layout = SelectorRow("Control layout");
            Slider buttonSize = SliderRow("Touch button size", out _);

            closeButton = CreateButton("Close", card, "DONE", ButtonStyle.Primary, new Vector2(360f, 90f));
            Place((RectTransform)closeButton.transform, new Vector2(0.5f, 0f), new Vector2(0f, 30f), new Vector2(360f, 90f), new Vector2(0.5f, 0f));

            SettingsPanel panel = root.AddComponent<SettingsPanel>();
            Wire(panel, "musicVolume", music);
            Wire(panel, "sfxVolume", sfx);
            Wire(panel, "lookSensitivity", look);
            Wire(panel, "aimSensitivity", aim);
            Wire(panel, "buttonSize", buttonSize);
            Wire(panel, "invertY", invert);
            Wire(panel, "quality", quality);
            Wire(panel, "frameRate", fps);
            Wire(panel, "layout", layout);
            Wire(panel, "lookValue", lookValue);
            Wire(panel, "aimValue", aimValue);
            return root;
        }
    }
}
