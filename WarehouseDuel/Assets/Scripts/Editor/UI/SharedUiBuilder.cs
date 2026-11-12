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
        /// <summary>A small lit warehouse corner with a spinning weapon on a pedestal, behind the menu UI.</summary>
        public static void Diorama(WeaponCatalog catalog, int showcaseWeapon)
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
            Place("Crate", new Vector3(3.2f, 0f, 4.5f), 12f);
            Place("Crate", new Vector3(4.5f, 0f, 5.2f), -8f);
            Place("CrateSmall", new Vector3(3.7f, 1.2f, 4.8f), 30f);
            Place("BarrelRed", new Vector3(-2.8f, 0f, 5.5f), 0f);
            Place("BarrelBlue", new Vector3(-2.1f, 0f, 6f), 0f);
            Place("StorageRack", new Vector3(11f, 0f, 6f), 0f);
            Place("Pillar", new Vector3(-10f, 0f, 4f), 0f);
            Place("HangingLamp", new Vector3(1.5f, 6.5f, 4f), 0f);
            Place("RollUpDoor", new Vector3(1f, 0f, 11.7f), 180f);

            // Pedestal + showcase weapon.
            EnvironmentKit.Box(root.transform, "Pedestal", new Vector3(1.6f, 0.45f, 3f), new Vector3(1.4f, 0.9f, 1.4f), EnvironmentPalette.Id.MachineDark, false);
            EnvironmentKit.NoShadow(EnvironmentKit.Box(root.transform, "PedestalTrim", new Vector3(1.6f, 0.91f, 3f), new Vector3(1.42f, 0.04f, 1.42f), EnvironmentPalette.Id.Hazard, false));
            var showcase = new GameObject("Showcase");
            showcase.transform.position = new Vector3(1.6f, 1.45f, 3f);
            WeaponDefinition weapon = catalog != null ? catalog.Get(showcaseWeapon) : null;
            if (weapon != null && weapon.worldModelPrefab != null)
            {
                var model = (GameObject)PrefabUtility.InstantiatePrefab(weapon.worldModelPrefab, showcase.transform);
                model.transform.localScale = Vector3.one * 1.6f;
                model.transform.localPosition = new Vector3(0f, 0f, -0.25f);
            }

            var sunObject = new GameObject("Sun");
            sunObject.transform.rotation = Quaternion.Euler(42f, -40f, 0f);
            Light sun = sunObject.AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.intensity = 1.1f;
            sun.color = new Color(1f, 0.93f, 0.82f);
            sun.shadows = LightShadows.Soft;
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.5f, 0.56f, 0.68f);
            RenderSettings.ambientEquatorColor = new Color(0.42f, 0.4f, 0.4f);
            RenderSettings.ambientGroundColor = new Color(0.25f, 0.23f, 0.21f);
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogColor = new Color(0.12f, 0.14f, 0.18f);
            RenderSettings.fogStartDistance = 8f;
            RenderSettings.fogEndDistance = 40f;

            var camObject = new GameObject("Main Camera") { tag = "MainCamera" };
            camObject.transform.SetPositionAndRotation(new Vector3(-0.6f, 1.9f, -3.2f), Quaternion.Euler(8f, 12f, 0f));
            Camera cam = camObject.AddComponent<Camera>();
            cam.fieldOfView = 50f;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.12f, 0.14f, 0.18f);
            cam.farClipPlane = 60f;
            camObject.AddComponent<AudioListener>();

            var backdrop = root.AddComponent<MenuBackdrop>();
            Wire(backdrop, "showcase", showcase.transform);
            Wire(backdrop, "cameraTransform", camObject.transform);
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
