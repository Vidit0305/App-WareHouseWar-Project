using System;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using WarehouseDuel.Core;
using WarehouseDuel.UI;
using Object = UnityEngine.Object;

namespace WarehouseDuel.EditorTools
{
    /// <summary>
    /// Generates the four Phase 1 scenes from code, so every component reference is created by Unity
    /// itself (no hand-written scene YAML, no broken script GUIDs).
    /// Later phases add content to these scenes through their own builders.
    /// </summary>
    internal static class SceneBuilder
    {
        // Phase 1 palette. Phase 7 moves these into a UI theme asset.
        private static readonly Color32 Background = new Color32(18, 21, 28, 255);
        private static readonly Color32 Track = new Color32(42, 47, 61, 255);
        private static readonly Color32 Accent = new Color32(255, 176, 32, 255);
        private static readonly Color32 TextPrimary = new Color32(240, 242, 246, 255);
        private static readonly Color32 TextMuted = new Color32(150, 158, 175, 255);

        private static readonly Vector2 ReferenceResolution = new Vector2(1920f, 1080f);
        private const int UiLayer = 5; // built-in "UI" layer

        public static bool TmpEssentialsImported() => Resources.Load<TMP_Settings>("TMP Settings") != null;

        public static void BuildScenes(SetupReport report, bool overwriteExisting)
        {
            if (!TmpEssentialsImported())
            {
                report.Error("TextMesh Pro Essential Resources are not imported. Use Window > TextMeshPro > Import TMP Essential Resources, then run the setup again. No scenes were created.");
                return;
            }

            FolderStructure.EnsureFolder(GameConstants.Scenes.Folder);

            Build(GameConstants.Scenes.SplashScreen, PopulateSplash, overwriteExisting, report);
            Build(GameConstants.Scenes.MainMenu,
                () => PopulateScaffoldUiScene("MAIN MENU",
                    "Scaffold scene. Create Room / Join Room are built in Phase 5; Settings and the full menu in Phase 7."),
                overwriteExisting, report);
            Build(GameConstants.Scenes.Lobby,
                () => PopulateScaffoldUiScene("LOBBY",
                    "Scaffold scene. The host loads it over the network when a room has two players (Phase 5)."),
                overwriteExisting, report);
            Build(GameConstants.Scenes.WarehouseMap, PopulateWarehouseMap, overwriteExisting, report);
        }

        public static void OpenStartScene()
        {
            string path = GameConstants.Scenes.PathOf(GameConstants.Scenes.SplashScreen);
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(path) != null)
            {
                EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
            }
        }

        private static void Build(string sceneName, Action populate, bool overwrite, SetupReport report)
        {
            string path = GameConstants.Scenes.PathOf(sceneName);
            bool exists = AssetDatabase.LoadAssetAtPath<SceneAsset>(path) != null;
            if (exists && !overwrite)
            {
                report.Info($"Scene already exists, left unchanged: {path}");
                return;
            }

            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            try
            {
                populate();
            }
            catch (Exception e)
            {
                report.Error($"Failed to build {sceneName}: {e.Message}");
                Debug.LogException(e);
                return;
            }

            if (EditorSceneManager.SaveScene(scene, path)) report.Pass(exists ? $"Rebuilt scene {path}" : $"Created scene {path}");
            else report.Error($"Could not save scene {path}");
        }

        // ------------------------------------------------------------------ scenes

        private static void PopulateSplash()
        {
            CreateUiCamera();
            Canvas canvas = CreateCanvas("Splash Canvas");
            CanvasGroup group = canvas.gameObject.AddComponent<CanvasGroup>();

            Stretch(CreateImage("Background", canvas.transform, Background).rectTransform);

            TextMeshProUGUI title = CreateText("Title", canvas.transform, GameConstants.GameTitle.ToUpperInvariant(), 120f, TextPrimary, FontStyles.Bold);
            title.characterSpacing = 8f;
            Place(title.rectTransform, new Vector2(0f, 110f), new Vector2(1700f, 170f));

            Place(CreateImage("Accent Line", canvas.transform, Accent).rectTransform, new Vector2(0f, 20f), new Vector2(240f, 6f));

            TextMeshProUGUI tagline = CreateText("Tagline", canvas.transform, "1V1 ONLINE ARENA", 34f, TextMuted);
            tagline.characterSpacing = 20f;
            Place(tagline.rectTransform, new Vector2(0f, -40f), new Vector2(1200f, 60f));

            TextMeshProUGUI status = CreateText("Status", canvas.transform, "LOADING", 28f, TextMuted);
            status.characterSpacing = 6f;
            Place(status.rectTransform, new Vector2(0f, -330f), new Vector2(900f, 44f));

            Image track = CreateImage("Progress Track", canvas.transform, Track);
            Place(track.rectTransform, new Vector2(0f, -375f), new Vector2(900f, 10f));

            Image fill = CreateImage("Progress Fill", track.transform, Accent);
            RectTransform fillRect = fill.rectTransform;
            fillRect.anchorMin = new Vector2(0f, 0f);
            fillRect.anchorMax = new Vector2(0f, 1f); // x driven 0..1 at runtime
            fillRect.pivot = new Vector2(0f, 0.5f);
            fillRect.offsetMin = Vector2.zero;
            fillRect.offsetMax = Vector2.zero;

            TextMeshProUGUI version = CreateText("Version", canvas.transform, "v0.0.0", 22f, TextMuted, FontStyles.Normal, TextAlignmentOptions.BottomRight);
            RectTransform versionRect = version.rectTransform;
            versionRect.anchorMin = versionRect.anchorMax = versionRect.pivot = new Vector2(1f, 0f);
            versionRect.anchoredPosition = new Vector2(-40f, 28f);
            versionRect.sizeDelta = new Vector2(400f, 40f);

            var controller = new GameObject("Splash Controller").AddComponent<SplashScreenController>();
            Wire(controller, "canvasGroup", group);
            Wire(controller, "progressFill", fillRect);
            Wire(controller, "statusLabel", status);
            Wire(controller, "versionLabel", version);
        }

        private static void PopulateScaffoldUiScene(string heading, string body)
        {
            CreateUiCamera();
            CreateEventSystem();
            Canvas canvas = CreateCanvas("Canvas");

            Stretch(CreateImage("Background", canvas.transform, Background).rectTransform);

            TextMeshProUGUI title = CreateText("Heading", canvas.transform, heading, 72f, TextPrimary, FontStyles.Bold);
            title.characterSpacing = 6f;
            Place(title.rectTransform, new Vector2(0f, 70f), new Vector2(1400f, 110f));

            Place(CreateImage("Accent Line", canvas.transform, Accent).rectTransform, new Vector2(0f, 5f), new Vector2(160f, 5f));

            TextMeshProUGUI text = CreateText("Body", canvas.transform, body, 30f, TextMuted);
            Place(text.rectTransform, new Vector2(0f, -70f), new Vector2(1300f, 140f));
        }

        private static void PopulateWarehouseMap()
        {
            // Organizer roots. Phase 2's map builder fills "Environment" and "Spawn Points".
            new GameObject("Environment");
            new GameObject("Spawn Points");
            new GameObject("Gameplay");
            var lighting = new GameObject("Lighting");

            var sunObject = new GameObject("Sun");
            sunObject.transform.SetParent(lighting.transform, false);
            sunObject.transform.rotation = Quaternion.Euler(50f, -35f, 0f);
            Light sun = sunObject.AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.color = new Color(1f, 0.96f, 0.88f);
            sun.intensity = 1.1f;
            sun.shadows = LightShadows.Soft;

            // Overview camera: used before the local player spawns and behind the results screen.
            var camObject = new GameObject("Overview Camera") { tag = "MainCamera" };
            camObject.transform.SetPositionAndRotation(new Vector3(0f, 14f, -20f), Quaternion.Euler(35f, 0f, 0f));
            Camera cam = camObject.AddComponent<Camera>();
            cam.nearClipPlane = 0.1f;
            cam.farClipPlane = 200f;
            camObject.AddComponent<AudioListener>();
        }

        // ------------------------------------------------------------------ helpers

        private static void CreateUiCamera()
        {
            var go = new GameObject("Main Camera") { tag = "MainCamera" };
            Camera cam = go.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = Background;
            cam.orthographic = true;
            cam.cullingMask = 0; // UI is a Screen Space - Overlay canvas; the camera only clears the screen.
            go.AddComponent<AudioListener>();
        }

        private static void CreateEventSystem()
        {
            var go = new GameObject("EventSystem", typeof(EventSystem));
#if ENABLE_INPUT_SYSTEM
            go.AddComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>();
#else
            go.AddComponent<StandaloneInputModule>();
#endif
        }

        private static Canvas CreateCanvas(string name)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            go.layer = UiLayer;

            var canvas = go.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;

            var scaler = go.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = ReferenceResolution;
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f; // balanced for 16:9 phones, 20:9 phones and 4:3 tablets
            return canvas;
        }

        private static RectTransform CreateRect(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform)) { layer = UiLayer };
            var rect = (RectTransform)go.transform;
            rect.SetParent(parent, false);
            return rect;
        }

        private static Image CreateImage(string name, Transform parent, Color color)
        {
            Image image = CreateRect(name, parent).gameObject.AddComponent<Image>();
            image.color = color;
            image.raycastTarget = false;
            return image;
        }

        private static TextMeshProUGUI CreateText(string name, Transform parent, string text, float size, Color color,
            FontStyles style = FontStyles.Normal, TextAlignmentOptions alignment = TextAlignmentOptions.Center)
        {
            TextMeshProUGUI label = CreateRect(name, parent).gameObject.AddComponent<TextMeshProUGUI>();
            label.text = text;
            label.fontSize = size;
            label.color = color;
            label.fontStyle = style;
            label.alignment = alignment;
            label.raycastTarget = false;
            return label;
        }

        private static void Place(RectTransform rect, Vector2 anchoredPosition, Vector2 size)
        {
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = anchoredPosition;
            rect.sizeDelta = size;
        }

        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        /// <summary>Assigns a private [SerializeField] reference the same way the Inspector does.</summary>
        private static void Wire(Object target, string fieldName, Object value)
        {
            var serialized = new SerializedObject(target);
            SerializedProperty property = serialized.FindProperty(fieldName);
            if (property == null)
            {
                throw new InvalidOperationException($"{target.GetType().Name} has no serialized field '{fieldName}'.");
            }
            property.objectReferenceValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
