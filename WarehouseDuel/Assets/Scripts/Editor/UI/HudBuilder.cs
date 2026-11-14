using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using WarehouseDuel.Core;
using WarehouseDuel.Multiplayer;
using WarehouseDuel.UI;
using WarehouseDuel.UI.Controls;
using WarehouseDuel.UI.Widgets;
using WarehouseDuel.Weapons;
using static WarehouseDuel.EditorTools.UI.UiFactory;

namespace WarehouseDuel.EditorTools.UI
{
    /// <summary>
    /// Adds the in-match UI to every map scene: HUD, touch controls, pause menu and results screen.
    /// Regenerates the "Match UI" root each time; the map itself is untouched.
    /// </summary>
    internal static class HudBuilder
    {
        private const string RootName = "Match UI";

        /// <summary>Adds the match UI to every map scene that exists.</summary>
        public static void Build(SetupReport report, WeaponCatalog catalog)
        {
            foreach (string map in GameConstants.Scenes.Maps)
            {
                string path = GameConstants.Scenes.PathOf(map);
                if (AssetDatabase.LoadAssetAtPath<SceneAsset>(path) == null)
                {
                    report.Error($"Map scene {path} is missing; build the maps first.");
                    continue;
                }
                BuildInto(report, map);
            }
        }

        private static WeaponCatalog LoadCatalog() =>
            AssetDatabase.LoadAssetAtPath<WeaponCatalog>(WarehouseDuel.EditorTools.Weapons.WeaponAssetBuilder.CatalogPath);

        private static void BuildInto(SetupReport report, string mapName)
        {
            string path = GameConstants.Scenes.PathOf(mapName);
            Scene scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
            foreach (GameObject old in scene.GetRootGameObjects().Where(g => g.name == RootName || g.name == "Session Bootstrap" || g.GetComponent<EventSystem>() != null))
            {
                Object.DestroyImmediate(old);
            }

            var root = new GameObject(RootName);
            SharedUiBuilder.SessionBootstrap();
            CreateEventSystem();

            Canvas canvas = CreateCanvas("HUD Canvas", 10, out RectTransform safe);
            canvas.transform.SetParent(root.transform, false);

            // Damage vignette sits behind everything else.
            Image vignette = CreateImage("Damage Vignette", canvas.transform, new Color(0.9f, 0.1f, 0.05f, 1f), UiArt.Gradient);
            Stretch(vignette.rectTransform);
            vignette.rectTransform.SetAsFirstSibling();
            CanvasGroup vignetteGroup = vignette.gameObject.AddComponent<CanvasGroup>();
            vignetteGroup.blocksRaycasts = false;

            // Sniper scope: a square lens sized to the screen height, black wings to either side, shown by HudController while scoped.
            RectTransform scopeRoot = CreateRect("Scope Overlay", canvas.transform);
            Stretch(scopeRoot);
            scopeRoot.SetSiblingIndex(1);
            CanvasGroup scopeGroup = scopeRoot.gameObject.AddComponent<CanvasGroup>();
            scopeGroup.blocksRaycasts = false;
            scopeGroup.interactable = false;
            scopeGroup.alpha = 0f;
            Image lens = CreateImage("Lens", scopeRoot, new Color(0.01f, 0.01f, 0.012f, 1f), UiArt.Scope);
            lens.raycastTarget = false;
            lens.rectTransform.anchorMin = new Vector2(0.5f, 0f);
            lens.rectTransform.anchorMax = new Vector2(0.5f, 1f);
            lens.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            lens.rectTransform.sizeDelta = new Vector2(1080f, 0f);
            var lensFit = lens.gameObject.AddComponent<AspectRatioFitter>();
            lensFit.aspectMode = AspectRatioFitter.AspectMode.HeightControlsWidth;
            lensFit.aspectRatio = 1f;
            foreach (bool left in new[] { true, false })
            {
                Image wing = CreateImage(left ? "Left" : "Right", lens.transform, new Color(0.01f, 0.01f, 0.012f, 1f));
                wing.raycastTarget = false;
                wing.rectTransform.anchorMin = new Vector2(left ? 0f : 1f, 0f);
                wing.rectTransform.anchorMax = new Vector2(left ? 0f : 1f, 1f);
                wing.rectTransform.pivot = new Vector2(left ? 1f : 0f, 0.5f);
                wing.rectTransform.anchoredPosition = new Vector2(left ? 1f : -1f, 0f); // 1 px overlap, no seam
                wing.rectTransform.sizeDelta = new Vector2(4000f, 0f);
            }
            Image scopeDot = CreateImage("Dot", lens.transform, new Color(1f, 0.15f, 0.1f, 1f), UiArt.Circle);
            scopeDot.raycastTarget = false;
            Place(scopeDot.rectTransform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(8f, 8f), new Vector2(0.5f, 0.5f));
            scopeRoot.gameObject.SetActive(false);

            GameObject touch = BuildTouchControls(safe);
            RectTransform gameplay = CreateRect("Gameplay HUD", safe);
            Stretch(gameplay);

            // ---- health (top-left): avatar portrait + bar
            Image healthPanel = CreatePanel("Health", gameplay, new Color(0f, 0f, 0f, 0.42f));
            healthPanel.raycastTarget = false;
            Place(healthPanel.rectTransform, new Vector2(0f, 1f), new Vector2(28f, -22f), new Vector2(500f, 96f), new Vector2(0f, 1f));
            Image avatarRing = CreateImage("Avatar Ring", healthPanel.transform, new Color(0.25f, 0.6f, 1f), UiArt.Circle);
            Place(avatarRing.rectTransform, new Vector2(0f, 0.5f), new Vector2(8f, 0f), new Vector2(84f, 84f), new Vector2(0f, 0.5f));
            var profiles = AssetDatabase.LoadAssetAtPath<WarehouseDuel.Profile.ProfileCatalog>(ProfileArtBuilder.CatalogPath);
            Image avatar = MenuScreensBuilder.Masked(avatarRing.transform, "Avatar", UiArt.Circle, profiles != null ? profiles.Avatar(0) : UiArt.Avatar, out RectTransform avatarMask);
            Stretch(avatarMask, 5f, 5f, 5f, 5f);
            Wire(avatarRing.gameObject.AddComponent<ProfileCard>(), "avatar", avatar); // shows the player's chosen avatar
            TextMeshProUGUI hpLabel = CreateText("Label", healthPanel.transform, "HP", 22f, TextMuted, TextAlignmentOptions.Left, display: true);
            Place(hpLabel.rectTransform, new Vector2(0f, 0.5f), new Vector2(106f, 20f), new Vector2(60f, 30f), new Vector2(0f, 0.5f));
            Image track = CreateImage("Track", healthPanel.transform, new Color(1f, 1f, 1f, 0.14f), UiArt.RoundedSmall);
            Place(track.rectTransform, new Vector2(0f, 0.5f), new Vector2(106f, -14f), new Vector2(270f, 24f), new Vector2(0f, 0.5f));
            Image fill = CreateImage("Fill", track.transform, Success, UiArt.RoundedSmall);
            fill.rectTransform.anchorMin = Vector2.zero;
            fill.rectTransform.anchorMax = Vector2.one;
            fill.rectTransform.offsetMin = fill.rectTransform.offsetMax = Vector2.zero;
            TextMeshProUGUI hpValue = CreateText("Value", healthPanel.transform, "100", 44f, TextPrimary, TextAlignmentOptions.Right, display: true);
            Place(hpValue.rectTransform, new Vector2(1f, 0.5f), new Vector2(-18f, 0f), new Vector2(100f, 56f), new Vector2(1f, 0.5f));

            // ---- team score bar (top-centre): [blue YOU score] [timer] [red opponent score]
            RectTransform scoreBar = CreateRect("Scoreboard", safe);
            Place(scoreBar, new Vector2(0.5f, 1f), new Vector2(0f, -16f), new Vector2(760f, 104f), new Vector2(0.5f, 1f));
            Image blue = CreateImage("Blue", scoreBar, new Color(0.16f, 0.45f, 0.95f, 0.92f), UiArt.Rounded);
            Place(blue.rectTransform, new Vector2(0f, 0.5f), Vector2.zero, new Vector2(270f, 84f), new Vector2(0f, 0.5f));
            Image red = CreateImage("Red", scoreBar, new Color(0.9f, 0.22f, 0.2f, 0.92f), UiArt.Rounded);
            Place(red.rectTransform, new Vector2(1f, 0.5f), Vector2.zero, new Vector2(270f, 84f), new Vector2(1f, 0.5f));
            Image clock = CreateImage("Clock", scoreBar, new Color(0.06f, 0.07f, 0.1f, 0.95f), UiArt.Rounded);
            Place(clock.rectTransform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(240f, 104f), new Vector2(0.5f, 0.5f));
            TextMeshProUGUI timer = CreateText("Timer", clock.transform, "3:00", 60f, TextPrimary, TextAlignmentOptions.Center, display: true);
            Stretch(timer.rectTransform);
            TextMeshProUGUI myName = CreateText("My Name", blue.transform, "YOU", 20f, new Color(1f, 1f, 1f, 0.85f), TextAlignmentOptions.Left, display: true);
            Place(myName.rectTransform, new Vector2(0f, 0.5f), new Vector2(18f, 0f), new Vector2(120f, 30f), new Vector2(0f, 0.5f));
            TextMeshProUGUI myKills = CreateText("My Kills", blue.transform, "0", 58f, Color.white, TextAlignmentOptions.Right, display: true);
            Place(myKills.rectTransform, new Vector2(1f, 0.5f), new Vector2(-24f, 0f), new Vector2(120f, 70f), new Vector2(1f, 0.5f));
            TextMeshProUGUI theirName = CreateText("Opponent Name", red.transform, "—", 20f, new Color(1f, 1f, 1f, 0.85f), TextAlignmentOptions.Right, display: true);
            Place(theirName.rectTransform, new Vector2(1f, 0.5f), new Vector2(-18f, 0f), new Vector2(140f, 30f), new Vector2(1f, 0.5f));
            TextMeshProUGUI theirKills = CreateText("Opponent Kills", red.transform, "0", 58f, Color.white, TextAlignmentOptions.Left, display: true);
            Place(theirKills.rectTransform, new Vector2(0f, 0.5f), new Vector2(24f, 0f), new Vector2(120f, 70f), new Vector2(0f, 0.5f));

            // ---- pause + ping + weapon (top-right)
            Button pause = CreateIconButton("Pause", safe, UiArt.Icon("Pause"), 84f, new Color(0f, 0f, 0f, 0.45f));
            Place((RectTransform)pause.transform, new Vector2(1f, 1f), new Vector2(-28f, -24f), new Vector2(84f, 84f), new Vector2(1f, 1f));
            TextMeshProUGUI ping = CreateText("Ping", safe, "", 22f, TextMuted, TextAlignmentOptions.Right);
            Place(ping.rectTransform, new Vector2(1f, 1f), new Vector2(-128f, -48f), new Vector2(200f, 36f), new Vector2(1f, 1f));

            Image weaponPanel = CreatePanel("Weapon", gameplay, new Color(0f, 0f, 0f, 0.45f));
            weaponPanel.raycastTarget = false;
            Place(weaponPanel.rectTransform, new Vector2(1f, 1f), new Vector2(-28f, -124f), new Vector2(420f, 140f), new Vector2(1f, 1f));
            Image weaponIcon = CreateImage("Icon", weaponPanel.transform, TextPrimary);
            weaponIcon.preserveAspect = true;
            Place(weaponIcon.rectTransform, new Vector2(0f, 1f), new Vector2(18f, -14f), new Vector2(170f, 84f), new Vector2(0f, 1f));
            TextMeshProUGUI weaponName = CreateText("Name", weaponPanel.transform, "", 22f, TextMuted, TextAlignmentOptions.Left, display: true);
            Place(weaponName.rectTransform, new Vector2(0f, 0f), new Vector2(18f, 10f), new Vector2(220f, 34f), new Vector2(0f, 0f));
            TextMeshProUGUI ammo = CreateText("Ammo", weaponPanel.transform, "30", 72f, TextPrimary, TextAlignmentOptions.Right, display: true);
            Place(ammo.rectTransform, new Vector2(1f, 1f), new Vector2(-100f, -10f), new Vector2(140f, 90f), new Vector2(1f, 1f));
            TextMeshProUGUI reserve = CreateText("Reserve", weaponPanel.transform, "/ 120", 28f, TextMuted, TextAlignmentOptions.Left);
            Place(reserve.rectTransform, new Vector2(1f, 1f), new Vector2(-96f, -50f), new Vector2(96f, 40f), new Vector2(0f, 1f));
            RectTransform reloadGroup = CreateRect("Reload", weaponPanel.transform);
            Place(reloadGroup, new Vector2(1f, 0f), new Vector2(-18f, 14f), new Vector2(180f, 26f), new Vector2(1f, 0f));
            TextMeshProUGUI reloadLabel = CreateText("Label", reloadGroup, "RELOADING", 18f, Accent, TextAlignmentOptions.Left, display: true);
            Place(reloadLabel.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 24f), new Vector2(180f, 24f), new Vector2(0f, 1f));
            Image reloadTrack = CreateImage("Track", reloadGroup, new Color(1f, 1f, 1f, 0.15f), UiArt.RoundedSmall);
            Place(reloadTrack.rectTransform, new Vector2(0f, 0f), Vector2.zero, new Vector2(180f, 10f), new Vector2(0f, 0f));
            Image reloadFill = CreateImage("Fill", reloadTrack.transform, Accent, UiArt.RoundedSmall);
            reloadFill.rectTransform.anchorMin = Vector2.zero;
            reloadFill.rectTransform.anchorMax = new Vector2(0f, 1f);
            reloadFill.rectTransform.offsetMin = reloadFill.rectTransform.offsetMax = Vector2.zero;

            // ---- crosshair + hit marker (centre)
            var lines = new RectTransform[4];
            Vector2[] sizes = { new Vector2(4f, 18f), new Vector2(4f, 18f), new Vector2(18f, 4f), new Vector2(18f, 4f) };
            for (int i = 0; i < 4; i++)
            {
                Image line = CreateImage($"Crosshair {i}", gameplay, new Color(1f, 1f, 1f, 0.9f));
                Place(line.rectTransform, new Vector2(0.5f, 0.5f), Vector2.zero, sizes[i], new Vector2(0.5f, 0.5f));
                Outline(line.gameObject);
                lines[i] = line.rectTransform;
            }
            Image dot = CreateImage("Crosshair Dot", gameplay, new Color(1f, 1f, 1f, 0.9f), UiArt.Circle);
            Place(dot.rectTransform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(6f, 6f), new Vector2(0.5f, 0.5f));

            RectTransform hitRoot = CreateRect("Hit Marker", gameplay);
            Place(hitRoot, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(60f, 60f), new Vector2(0.5f, 0.5f));
            CanvasGroup hitGroup = hitRoot.gameObject.AddComponent<CanvasGroup>();
            Image hitA = CreateImage("A", hitRoot, Color.white);
            Place(hitA.rectTransform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(5f, 44f), new Vector2(0.5f, 0.5f));
            hitA.rectTransform.localRotation = Quaternion.Euler(0f, 0f, 45f);
            Image hitB = CreateImage("B", hitRoot, Color.white);
            Place(hitB.rectTransform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(5f, 44f), new Vector2(0.5f, 0.5f));
            hitB.rectTransform.localRotation = Quaternion.Euler(0f, 0f, -45f);
            Image hitCentre = CreateImage("Gap", hitRoot, new Color(0f, 0f, 0f, 0f));
            Place(hitCentre.rectTransform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(16f, 16f), new Vector2(0.5f, 0.5f));
            // Single tint target: both bars share the colour through HudController (hitMarkerImage = A; B mirrors via CanvasGroup).
            hitB.gameObject.AddComponent<ColorMirror>().source = hitA;

            // ---- banners / centre text
            TextMeshProUGUI banner = CreateText("Banner", gameplay, "", 46f, Accent, TextAlignmentOptions.Center, display: true);
            Place(banner.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, 230f), new Vector2(1400f, 70f), new Vector2(0.5f, 0.5f));
            CanvasGroup bannerGroup = banner.gameObject.AddComponent<CanvasGroup>();
            TextMeshProUGUI centre = CreateText("Centre Message", safe, "", 34f, TextPrimary, TextAlignmentOptions.Center, display: true);
            Place(centre.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, -150f), new Vector2(1200f, 60f), new Vector2(0.5f, 0.5f));
            TextMeshProUGUI countdown = CreateText("Countdown", safe, "3", 220f, Accent, TextAlignmentOptions.Center, display: true);
            Place(countdown.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, 60f), new Vector2(600f, 280f), new Vector2(0.5f, 0.5f));

            Image deathPanel = CreatePanel("Death Panel", safe, new Color(0.05f, 0.02f, 0.02f, 0.75f));
            deathPanel.raycastTarget = false;
            Place(deathPanel.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, 40f), new Vector2(1000f, 200f), new Vector2(0.5f, 0.5f));
            TextMeshProUGUI deathText = CreateText("Text", deathPanel.transform, "", 52f, TextPrimary, TextAlignmentOptions.Center, display: true);
            Stretch(deathText.rectTransform, 20f, 20f, 10f, 10f);

            // ---- pause, results
            PauseMenu pauseMenu = BuildPause(safe, pause);
            // Load the catalog only now: opening a baked map scene can refresh assets and leave an earlier-loaded catalog stale.
            WeaponCatalog catalog = LoadCatalog();
            ResultsController results = BuildResults(safe, catalog);

            var hud = canvas.gameObject.AddComponent<HudController>();
            Wire(hud, "healthFill", fill.rectTransform);
            Wire(hud, "healthFillImage", fill);
            Wire(hud, "healthText", hpValue);
            Wire(hud, "timerText", timer);
            Wire(hud, "myNameText", myName);
            Wire(hud, "myKillsText", myKills);
            Wire(hud, "opponentNameText", theirName);
            Wire(hud, "opponentKillsText", theirKills);
            Wire(hud, "pingText", ping);
            Wire(hud, "ammoText", ammo);
            Wire(hud, "reserveText", reserve);
            Wire(hud, "weaponNameText", weaponName);
            Wire(hud, "weaponIcon", weaponIcon);
            Wire(hud, "reloadFill", reloadFill.rectTransform);
            Wire(hud, "reloadGroup", reloadGroup.gameObject);
            WireArray(hud, "crosshairLines", lines);
            Wire(hud, "crosshairDot", dot.gameObject);
            Wire(hud, "scopeOverlay", scopeGroup);
            Wire(hud, "hitMarker", hitGroup);
            Wire(hud, "hitMarkerImage", hitA);
            Wire(hud, "damageVignette", vignetteGroup);
            Wire(hud, "bannerText", banner);
            Wire(hud, "bannerGroup", bannerGroup);
            Wire(hud, "countdownText", countdown);
            Wire(hud, "centerMessageText", centre);
            Wire(hud, "deathPanel", deathPanel.gameObject);
            Wire(hud, "deathText", deathText);
            Wire(hud, "gameplayGroup", gameplay.gameObject);
            Wire(hud, "touchControls", touch);

            // Draw order: touch controls under HUD text, pause/results on top.
            touch.transform.SetSiblingIndex(0);
            pauseMenu.transform.SetAsLastSibling();
            results.transform.SetAsLastSibling();

            var resultsSo = new SerializedObject(results);
            if (resultsSo.FindProperty("catalog").objectReferenceValue == null)
            {
                Wire(results, "catalog", LoadCatalog());
                if (new SerializedObject(results).FindProperty("catalog").objectReferenceValue == null)
                    report.Error($"{mapName}: results screen has no WeaponCatalog.");
            }

            EditorSceneManager.MarkSceneDirty(scene);
            if (EditorSceneManager.SaveScene(scene)) report.Pass($"Match UI (HUD, touch controls, pause, results) added to {mapName}.");
            else report.Error($"Could not save {mapName} after adding the HUD.");
        }

        /// <summary>Button layout, in canvas units from the zone's bottom corner (reference 1920×1080). Shared with the self-test.</summary>
        internal static readonly (string name, string icon, float size, Vector2 position, bool left)[] ButtonLayout =
        {
            ("Fire", "HudFire", 230f, new Vector2(-280f, 320f), false),
            ("Jump", "HudJump", 160f, new Vector2(-95f, 140f), false),
            ("Aim", "HudAim", 150f, new Vector2(-560f, 200f), false),
            ("Reload", "HudReload", 130f, new Vector2(-120f, 560f), false),
            ("Switch Weapon", "HudSwap", 120f, new Vector2(-520f, 440f), false),
            ("Sprint", "HudSprint", 130f, new Vector2(560f, 500f), true),
            ("Fire Left", "HudFire", 160f, new Vector2(190f, 640f), true),
        };

        internal const float StickSize = 300f, StickRest = 300f, HandleSize = 130f;

        internal static GameObject BuildTouchControls(RectTransform safe)
        {
            RectTransform root = CreateRect("Touch Controls", safe);
            Stretch(root);

            // Left zone (joystick). Pivot on the screen centre so left-handed mode can mirror it.
            Image leftZone = CreateImage("Left Zone", root, new Color(0f, 0f, 0f, 0f), raycast: true);
            leftZone.rectTransform.anchorMin = new Vector2(0f, 0f);
            leftZone.rectTransform.anchorMax = new Vector2(0.5f, 0.85f);
            leftZone.rectTransform.pivot = new Vector2(1f, 0.5f);
            leftZone.rectTransform.offsetMin = leftZone.rectTransform.offsetMax = Vector2.zero;
            VirtualJoystick joystick = leftZone.gameObject.AddComponent<VirtualJoystick>();
            Image stickBase = CreateImage("Stick Base", leftZone.transform, new Color(0f, 0f, 0f, 0.32f), UiArt.Circle);
            Place(stickBase.rectTransform, new Vector2(0f, 0f), new Vector2(StickRest, StickRest - 20f), new Vector2(StickSize, StickSize), new Vector2(0.5f, 0.5f));
            Image stickRing = CreateImage("Ring", stickBase.transform, new Color(1f, 1f, 1f, 0.75f), UiArt.Ring);
            Stretch(stickRing.rectTransform);
            Image handle = CreateImage("Handle", stickBase.transform, new Color(1f, 1f, 1f, 0.85f), UiArt.Circle);
            Place(handle.rectTransform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(HandleSize, HandleSize), new Vector2(0.5f, 0.5f));
            Image handleDot = CreateImage("Dot", handle.transform, new Color(0.1f, 0.12f, 0.16f, 0.35f), UiArt.Ring);
            Stretch(handleDot.rectTransform, 22f, 22f, 22f, 22f);
            Wire(joystick, "stickBase", stickBase.rectTransform);
            Wire(joystick, "handle", handle.rectTransform);
            SetFloat(joystick, "radius", StickSize * 0.42f);
            SetFloat(joystick, "idleAlpha", 0.65f);

            // Right zone (look drag + action buttons).
            Image rightZone = CreateImage("Right Zone", root, new Color(0f, 0f, 0f, 0f), raycast: true);
            rightZone.rectTransform.anchorMin = new Vector2(0.5f, 0f);
            rightZone.rectTransform.anchorMax = new Vector2(1f, 0.85f);
            rightZone.rectTransform.pivot = new Vector2(0f, 0.5f);
            rightZone.rectTransform.offsetMin = rightZone.rectTransform.offsetMax = Vector2.zero;
            TouchLookArea look = rightZone.gameObject.AddComponent<TouchLookArea>();

            var buttons = new System.Collections.Generic.Dictionary<string, TouchButton>();
            foreach (var spec in ButtonLayout)
            {
                Transform parent = spec.left ? leftZone.transform : rightZone.transform;
                Vector2 anchor = spec.left ? new Vector2(0f, 0f) : new Vector2(1f, 0f);
                bool isFire = spec.name == "Fire" || spec.name == "Fire Left";
                Color idle = isFire ? new Color(0.55f, 0.08f, 0.05f, 0.5f) : new Color(0.03f, 0.04f, 0.06f, 0.5f);
                Image disc = CreateImage(spec.name, parent, idle, UiArt.Circle, raycast: true);
                Place(disc.rectTransform, anchor, spec.position, new Vector2(spec.size, spec.size), new Vector2(0.5f, 0.5f));
                Image ring = CreateImage("Ring", disc.transform, isFire ? new Color(1f, 0.55f, 0.45f, 0.9f) : new Color(1f, 1f, 1f, 0.8f), UiArt.Ring);
                Stretch(ring.rectTransform);
                Image inner = CreateImage("Inner Ring", disc.transform, new Color(1f, 1f, 1f, 0.12f), UiArt.Ring);
                Stretch(inner.rectTransform, spec.size * 0.1f, spec.size * 0.1f, spec.size * 0.1f, spec.size * 0.1f);
                float pad = spec.size * 0.2f;
                Image glyph = CreateImage("Icon", disc.transform, Color.white, UiArt.Icon(spec.icon));
                Stretch(glyph.rectTransform, pad, pad, pad, pad);
                var shadow = glyph.gameObject.AddComponent<Shadow>();
                shadow.effectColor = new Color(0f, 0f, 0f, 0.55f);
                shadow.effectDistance = new Vector2(2f, -2f);

                TouchButton button = disc.gameObject.AddComponent<TouchButton>();
                SetEnum(button, "mode", spec.name == "Sprint" ? (int)TouchButton.ButtonMode.Toggle : (int)TouchButton.ButtonMode.Hold);
                if (isFire || spec.name == "Aim") Wire(button, "forwardDragTo", look);
                Wire(button, "highlight", disc);
                SetColor(button, "normalColor", idle);
                SetColor(button, "activeColor", isFire ? new Color(0.95f, 0.25f, 0.15f, 0.9f) : new Color(1f, 0.69f, 0.13f, 0.85f));
                buttons[spec.name] = button;
            }

            TouchControlsView view = root.gameObject.AddComponent<TouchControlsView>();
            Wire(view, "joystick", joystick);
            Wire(view, "lookArea", look);
            Wire(view, "fire", buttons["Fire"]);
            Wire(view, "fireLeft", buttons["Fire Left"]);
            Wire(view, "aim", buttons["Aim"]);
            Wire(view, "reload", buttons["Reload"]);
            Wire(view, "jump", buttons["Jump"]);
            Wire(view, "sprint", buttons["Sprint"]);
            Wire(view, "switchWeapon", buttons["Switch Weapon"]);
            Wire(view, "leftZone", leftZone.rectTransform);
            Wire(view, "rightZone", rightZone.rectTransform);
            var scalable = new System.Collections.Generic.List<Object>();
            foreach (TouchButton b in buttons.Values) scalable.Add(b.transform);
            scalable.Add(stickBase.rectTransform);
            WireArray(view, "scalableButtons", scalable.ToArray());
            return root.gameObject;
        }

        private static PauseMenu BuildPause(RectTransform safe, Button openButton)
        {
            RectTransform host = CreateRect("Pause Menu", safe);
            Stretch(host);
            RectTransform card = SharedUiBuilder.Modal("Panel", host, new Vector2(700f, 560f), out GameObject panel);
            TextMeshProUGUI title = CreateText("Title", card, "PAUSED", 56f, Accent, TextAlignmentOptions.Center, display: true);
            Place(title.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -30f), new Vector2(600f, 80f), new Vector2(0.5f, 1f));
            TextMeshProUGUI note = CreateText("Note", card, "The match keeps running while this menu is open.", 22f, TextMuted);
            Place(note.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -110f), new Vector2(620f, 40f), new Vector2(0.5f, 1f));
            Button resume = CreateButton("Resume", card, "RESUME", ButtonStyle.Primary, new Vector2(520f, 100f));
            Place((RectTransform)resume.transform, new Vector2(0.5f, 1f), new Vector2(0f, -180f), new Vector2(520f, 100f), new Vector2(0.5f, 1f));
            Button settings = CreateButton("Settings", card, "SETTINGS", ButtonStyle.Secondary, new Vector2(520f, 90f));
            Place((RectTransform)settings.transform, new Vector2(0.5f, 1f), new Vector2(0f, -300f), new Vector2(520f, 90f), new Vector2(0.5f, 1f));
            Button leave = CreateButton("Leave", card, "LEAVE MATCH", ButtonStyle.Danger, new Vector2(520f, 90f));
            Place((RectTransform)leave.transform, new Vector2(0.5f, 1f), new Vector2(0f, -410f), new Vector2(520f, 90f), new Vector2(0.5f, 1f));

            GameObject settingsPanel = SharedUiBuilder.Settings(host, out Button settingsClose);

            RectTransform confirmCard = SharedUiBuilder.Modal("Confirm Leave", host, new Vector2(800f, 380f), out GameObject confirm);
            TextMeshProUGUI question = CreateText("Question", confirmCard, "Leave the match? Your opponent will win by forfeit.", 34f, TextPrimary);
            Stretch(question.rectTransform, 50f, 50f, 40f, 150f);
            Button confirmLeave = CreateButton("Leave", confirmCard, "LEAVE", ButtonStyle.Danger, new Vector2(300f, 90f));
            Place((RectTransform)confirmLeave.transform, new Vector2(0.5f, 0f), new Vector2(-170f, 30f), new Vector2(300f, 90f), new Vector2(0.5f, 0f));
            Button cancel = CreateButton("Cancel", confirmCard, "STAY", ButtonStyle.Secondary, new Vector2(300f, 90f));
            Place((RectTransform)cancel.transform, new Vector2(0.5f, 0f), new Vector2(170f, 30f), new Vector2(300f, 90f), new Vector2(0.5f, 0f));

            PauseMenu menu = host.gameObject.AddComponent<PauseMenu>();
            Wire(menu, "openButton", openButton);
            Wire(menu, "panel", panel);
            Wire(menu, "resumeButton", resume);
            Wire(menu, "settingsButton", settings);
            Wire(menu, "leaveButton", leave);
            Wire(menu, "settingsPanel", settingsPanel);
            Wire(menu, "settingsCloseButton", settingsClose);
            Wire(menu, "confirmPanel", confirm);
            Wire(menu, "confirmLeaveButton", confirmLeave);
            Wire(menu, "cancelLeaveButton", cancel);
            return menu;
        }

        private static ResultsController BuildResults(RectTransform safe, WeaponCatalog catalog)
        {
            RectTransform host = CreateRect("Results", safe);
            Stretch(host);
            RectTransform card = SharedUiBuilder.Modal("Panel", host, new Vector2(1400f, 960f), out GameObject panel);

            Image band = CreateImage("Title Band", card, UiFactory.Accent, UiArt.Rounded);
            Place(band.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -24f), new Vector2(1340f, 120f), new Vector2(0.5f, 1f));
            TextMeshProUGUI title = CreateText("Title", band.transform, "VICTORY", 84f, new Color(0.06f, 0.07f, 0.1f), TextAlignmentOptions.Center, display: true);
            Stretch(title.rectTransform);
            TextMeshProUGUI subtitle = CreateText("Subtitle", card, "", 30f, TextPrimary);
            Place(subtitle.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -152f), new Vector2(1300f, 46f), new Vector2(0.5f, 1f));

            // Ranking table: header + 8 rows.
            float[] columns = { 40f, 150f, 760f, 940f, 1110f };
            float[] widths = { 100f, 600f, 160f, 160f, 220f };
            string[] headers = { "RANK", "PLAYER", "KILLS", "DEATHS", "WEAPON" };
            for (int i = 0; i < headers.Length; i++)
            {
                TextMeshProUGUI header = CreateText(headers[i], card, headers[i], 20f, TextMuted, i <= 1 ? TextAlignmentOptions.Left : TextAlignmentOptions.Center, display: true);
                Place(header.rectTransform, new Vector2(0f, 1f), new Vector2(columns[i] + 30f, -210f), new Vector2(widths[i], 30f), new Vector2(0f, 1f));
            }
            var rows = new ResultsController.Row[RoomState.Capacity];
            for (int r = 0; r < rows.Length; r++)
            {
                Image background = CreateImage($"Row {r + 1}", card, new Color(1f, 1f, 1f, 0.05f), UiArt.RoundedSmall);
                Place(background.rectTransform, new Vector2(0f, 1f), new Vector2(30f, -246f - r * 62f), new Vector2(1340f, 56f), new Vector2(0f, 1f));
                TextMeshProUGUI Cell(string name, int column, float size, bool display)
                {
                    TextMeshProUGUI cell = CreateText(name, background.transform, "-", size, TextPrimary, column <= 1 ? TextAlignmentOptions.Left : TextAlignmentOptions.Center, display: display);
                    Place(cell.rectTransform, new Vector2(0f, 0.5f), new Vector2(columns[column], 0f), new Vector2(widths[column], 52f), new Vector2(0f, 0.5f));
                    cell.textWrappingMode = TextWrappingModes.NoWrap;
                    cell.overflowMode = TextOverflowModes.Ellipsis;
                    return cell;
                }
                rows[r] = new ResultsController.Row
                {
                    background = background,
                    rank = Cell("Rank", 0, 30f, true),
                    name = Cell("Name", 1, 30f, true),
                    kills = Cell("Kills", 2, 32f, true),
                    deaths = Cell("Deaths", 3, 30f, false),
                    weapon = Cell("Weapon", 4, 24f, false),
                };
            }

            TextMeshProUGUI details = CreateText("Details", card, "", 26f, TextMuted);
            Place(details.rectTransform, new Vector2(0.5f, 0f), new Vector2(0f, 140f), new Vector2(1300f, 40f), new Vector2(0.5f, 0f));

            Button again = CreateButton("Play Again", card, "PLAY AGAIN", ButtonStyle.Primary, new Vector2(400f, 96f), out TextMeshProUGUI againLabel, 34f);
            Place((RectTransform)again.transform, new Vector2(0.5f, 0f), new Vector2(-430f, 28f), new Vector2(400f, 96f), new Vector2(0.5f, 0f));
            Button lobby = CreateButton("Lobby", card, "RETURN TO LOBBY", ButtonStyle.Secondary, new Vector2(400f, 96f), 30f);
            Place((RectTransform)lobby.transform, new Vector2(0.5f, 0f), new Vector2(0f, 28f), new Vector2(400f, 96f), new Vector2(0.5f, 0f));
            Button menu = CreateButton("Menu", card, "MAIN MENU", ButtonStyle.Ghost, new Vector2(400f, 96f), 30f);
            Place((RectTransform)menu.transform, new Vector2(0.5f, 0f), new Vector2(430f, 28f), new Vector2(400f, 96f), new Vector2(0.5f, 0f));

            ResultsController results = host.gameObject.AddComponent<ResultsController>();
            Wire(results, "catalog", catalog);
            Wire(results, "panel", panel);
            Wire(results, "titleText", title);
            Wire(results, "subtitleText", subtitle);
            Wire(results, "detailsText", details);
            Wire(results, "titleBand", band);
            Wire(results, "playAgainButton", again);
            Wire(results, "playAgainText", againLabel);
            Wire(results, "lobbyButton", lobby);
            Wire(results, "menuButton", menu);
            var so = new SerializedObject(results);
            SerializedProperty array = so.FindProperty("rows");
            array.arraySize = rows.Length;
            for (int r = 0; r < rows.Length; r++)
            {
                SerializedProperty element = array.GetArrayElementAtIndex(r);
                element.FindPropertyRelative("background").objectReferenceValue = rows[r].background;
                element.FindPropertyRelative("rank").objectReferenceValue = rows[r].rank;
                element.FindPropertyRelative("name").objectReferenceValue = rows[r].name;
                element.FindPropertyRelative("kills").objectReferenceValue = rows[r].kills;
                element.FindPropertyRelative("deaths").objectReferenceValue = rows[r].deaths;
                element.FindPropertyRelative("weapon").objectReferenceValue = rows[r].weapon;
            }
            so.ApplyModifiedPropertiesWithoutUndo();
            return results;
        }

        private static void Outline(GameObject go)
        {
            var shadow = go.AddComponent<Shadow>();
            shadow.effectColor = new Color(0f, 0f, 0f, 0.6f);
            shadow.effectDistance = new Vector2(1.5f, -1.5f);
        }
    }
}
