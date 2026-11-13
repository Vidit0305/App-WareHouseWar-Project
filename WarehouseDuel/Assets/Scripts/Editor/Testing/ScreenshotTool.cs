using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using WarehouseDuel.Core;
using WarehouseDuel.Weapons;

namespace WarehouseDuel.EditorTools.Testing
{
    /// <summary>
    /// Renders review screenshots of the generated content (warehouse viewpoints, weapon line-up) to PNG files,
    /// so the look can be checked without opening the Editor UI. Needs a graphics device (don't pass -nographics).
    ///   Unity.exe -batchmode -projectPath WarehouseDuel -executeMethod WarehouseDuel.EditorTools.Testing.ScreenshotTool.CaptureBatch -screenshotDir <dir>
    /// </summary>
    public static class ScreenshotTool
    {
        private const int Width = 1280, Height = 720;

        public static void CaptureBatch()
        {
            string[] args = System.Environment.GetCommandLineArgs();
            int i = System.Array.IndexOf(args, "-screenshotDir");
            string dir = i >= 0 && i + 1 < args.Length ? args[i + 1] : Path.GetFullPath("../.tools/screens");
            Directory.CreateDirectory(dir);

            EditorSceneManager.OpenScene(GameConstants.Scenes.PathOf(GameConstants.Scenes.WarehouseMap));
            var views = new (string name, Vector3 position, Vector3 lookAt, float fov)[]
            {
                ("map_spawnA_eye", new Vector3(0f, 1.6f, -25f), new Vector3(0f, 1.4f, 0f), 72f),
                ("map_overview", new Vector3(-15f, 7.5f, -25.5f), new Vector3(0f, 0.5f, 0f), 60f),
                ("map_centre_eye", new Vector3(-4f, 1.6f, -10f), new Vector3(3f, 1.2f, 4f), 72f),
                ("map_catwalk", new Vector3(16.5f, 4.8f, -18f), new Vector3(0f, 1f, 0f), 72f),
                ("map_left_lane", new Vector3(-16.7f, 1.6f, -10f), new Vector3(-16.5f, 1.4f, 5f), 72f),
                ("map_topdown", new Vector3(0f, 60f, 0f), new Vector3(0f, 0f, 0.01f), 55f),
            };

            // Warm-up: the first headless render runs before lighting/shadow data is initialised; discard it.
            string warmup = Path.Combine(dir, "_warmup.png");
            Capture(warmup, views[1].position, views[1].lookAt, views[1].fov);
            File.Delete(warmup);

            foreach (var view in views)
            {
                bool topDown = view.name == "map_topdown";
                GameObject roof = topDown ? GameObject.Find("Roof") : null;
                if (roof != null) roof.SetActive(false);
                Capture(Path.Combine(dir, view.name + ".png"), view.position, view.lookAt, view.fov);
                if (roof != null) roof.SetActive(true);
            }

            CaptureWeapons(Path.Combine(dir, "weapons.png"));
            CaptureFirstPerson(dir);

            // UI screens (canvases are switched to camera space so they appear in the render).
            string[] runtimeHidden = { "Online", "Join", "Busy", "Settings Panel", "Profile Panel", "Message Panel", "Death Panel",
                                       "Countdown", "Panel", "Confirm Leave", "Centre Message", "Banner", "Copied" };
            CaptureUiScene(GameConstants.Scenes.SplashScreen, Path.Combine(dir, "ui_splash.png"), runtimeHidden);
            CaptureUiScene(GameConstants.Scenes.MainMenu, Path.Combine(dir, "ui_mainmenu.png"), runtimeHidden);
            foreach (var (shot, show) in new[] { ("ui_profile", "Profile Screen"), ("ui_leaderboard", "Leaderboard Screen"),
                                                 ("ui_armory", "Armory Screen"), ("ui_characters", "Character Screen"), ("ui_settings", "Settings Panel"), ("ui_online", "Online") })
            {
                bool overHome = show == "Online";
                CaptureUiScene(GameConstants.Scenes.MainMenu, Path.Combine(dir, shot + ".png"), runtimeHidden, show, overHome ? null : "Home");
            }
            CaptureUiScene(GameConstants.Scenes.Lobby, Path.Combine(dir, "ui_lobby.png"), runtimeHidden);
            CaptureUiScene(GameConstants.Scenes.WarehouseMap, Path.Combine(dir, "ui_hud.png"), runtimeHidden);
            Debug.Log($"[Screenshots] Saved to {dir}");
            EditorApplication.Exit(0);
        }

        private static void CaptureUiScene(string sceneName, string path, string[] hideNames, string show = null, string alsoHide = null)
        {
            EditorSceneManager.OpenScene(GameConstants.Scenes.PathOf(sceneName));
            Camera cam = Camera.main != null ? Camera.main : Object.FindFirstObjectByType<Camera>();
            if (cam == null) return;

            foreach (Transform t in Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (System.Array.IndexOf(hideNames, t.name) >= 0 && t.GetComponent<RectTransform>() != null) t.gameObject.SetActive(false);
            }

            foreach (Transform t in Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (t.GetComponent<RectTransform>() == null) continue;
                if (show != null && t.name == show && t.parent != null && t.parent.name == "Safe Area") t.gameObject.SetActive(true);
                if (alsoHide != null && t.name == alsoHide && t.parent != null && t.parent.name == "Safe Area") t.gameObject.SetActive(false);
            }

            // Showcase characters only get their weapon at runtime; equip it for the picture.
            foreach (var animator in Object.FindObjectsByType<WarehouseDuel.Player.SoldierAnimator>(FindObjectsSortMode.None))
            {
                int weapon = new SerializedObject(animator).FindProperty("startWeapon").intValue;
                typeof(WarehouseDuel.Player.SoldierAnimator).GetMethod("ShowWeapon", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                    ?.Invoke(animator, new object[] { weapon });
            }

            // Runtime-only states that would otherwise show in an edit-time picture.
            foreach (CanvasGroup group in Object.FindObjectsByType<CanvasGroup>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (group.name.Contains("Vignette")) group.alpha = 0f;
            }
            cam.cullingMask |= 1 << LayerMask.NameToLayer("UI");

            var rt = new RenderTexture(Width, Height, 24, RenderTextureFormat.ARGB32) { antiAliasing = 4 };
            cam.targetTexture = rt;
            foreach (Canvas canvas in Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None))
            {
                if (!canvas.isRootCanvas) continue;
                canvas.renderMode = RenderMode.ScreenSpaceCamera;
                canvas.worldCamera = cam;
                canvas.planeDistance = Mathf.Max(cam.nearClipPlane + 0.05f, 0.5f);
            }
            Canvas.ForceUpdateCanvases();
            cam.Render(); // warm-up
            Render(cam, path);
        }

        private static void Capture(string path, Vector3 position, Vector3 lookAt, float fov)
        {
            var go = new GameObject("Screenshot Camera");
            Camera cam = go.AddComponent<Camera>();
            go.transform.position = position;
            go.transform.LookAt(lookAt);
            cam.fieldOfView = fov;
            cam.nearClipPlane = 0.05f;
            cam.farClipPlane = 300f;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.1f, 0.12f, 0.16f);
            WarehouseDuel.EditorTools.Map.AtmosphereKit.EnableCameraEffects(cam);
            Render(cam, path);
            Object.DestroyImmediate(go);
        }

        /// <summary>First-person view of every weapon with the hands, framed like the in-game camera rig (hip pose).</summary>
        private static void CaptureFirstPerson(string dir)
        {
            var catalog = AssetDatabase.LoadAssetAtPath<WeaponCatalog>("Assets/ScriptableObjects/Weapons/WeaponCatalog.asset");
            var player = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Players/Player.prefab");
            var rig = player != null ? player.GetComponentInChildren<WarehouseDuel.Player.PlayerCameraRig>(true) : null;
            if (catalog == null || rig == null) return;
            var so = new SerializedObject(rig);
            float scale = so.FindProperty("viewModelScale").floatValue;
            Vector3 hip = so.FindProperty("hipOffset").vector3Value;
            var hands = (GameObject)so.FindProperty("handPrefab").objectReferenceValue;

            var go = new GameObject("FP Camera");
            go.transform.position = new Vector3(0f, 1.6f, -25f);
            go.transform.LookAt(new Vector3(0f, 1.4f, 0f));
            Camera cam = go.AddComponent<Camera>();
            cam.fieldOfView = 72f;
            cam.nearClipPlane = 0.03f;
            cam.farClipPlane = 300f;
            WarehouseDuel.EditorTools.Map.AtmosphereKit.EnableCameraEffects(cam);
            var socket = new GameObject("Socket").transform;
            socket.SetParent(go.transform, false);
            socket.localPosition = hip;

            for (int i = 0; i < catalog.Count; i++)
            {
                WeaponDefinition def = catalog.Get(i);
                if (def == null || def.viewModelPrefab == null) continue;
                var model = (GameObject)Object.Instantiate(def.viewModelPrefab, socket, false);
                model.transform.localScale = Vector3.one * scale;
                model.transform.localPosition = def.viewModelOffset;
                WarehouseDuel.Player.PlayerCameraRig.AttachHands(model.transform, hands);
                Render(cam, Path.Combine(dir, $"fp_{i}_{def.name}.png"));
                Object.DestroyImmediate(model);
            }
            Object.DestroyImmediate(go);
        }

        private static void CaptureWeapons(string path)
        {
            var catalog = AssetDatabase.LoadAssetAtPath<WeaponCatalog>("Assets/ScriptableObjects/Weapons/WeaponCatalog.asset");
            if (catalog == null) return;
            var root = new GameObject("Weapon Lineup");
            root.transform.position = new Vector3(500f, 500f, 500f);
            for (int i = 0; i < catalog.Count; i++)
            {
                WeaponDefinition def = catalog.Get(i);
                if (def == null || def.worldModelPrefab == null) continue;
                var model = (GameObject)PrefabUtility.InstantiatePrefab(def.worldModelPrefab, root.transform);
                model.transform.localPosition = new Vector3(0f, 0.6f - i * 0.4f, 0f);
                model.transform.localRotation = Quaternion.Euler(0f, 90f, 0f);
            }
            var lightObject = new GameObject("Key Light");
            lightObject.transform.SetParent(root.transform, false);
            lightObject.transform.rotation = Quaternion.Euler(30f, -60f, 0f);
            Light light = lightObject.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.4f;

            Capture(path, root.transform.position + new Vector3(0.2f, 0f, -2.1f), root.transform.position + new Vector3(0.2f, 0f, 0f), 40f);
            Object.DestroyImmediate(root);
        }

        private static void Render(Camera cam, string path)
        {
            var rt = new RenderTexture(Width, Height, 24, RenderTextureFormat.ARGB32) { antiAliasing = 4 };
            cam.targetTexture = rt;
            cam.Render();
            RenderTexture.active = rt;
            var texture = new Texture2D(Width, Height, TextureFormat.RGB24, false);
            texture.ReadPixels(new Rect(0, 0, Width, Height), 0, 0);
            texture.Apply();
            File.WriteAllBytes(path, texture.EncodeToPNG());
            RenderTexture.active = null;
            cam.targetTexture = null;
            Object.DestroyImmediate(texture);
            rt.Release();
            Object.DestroyImmediate(rt);
        }
    }
}
