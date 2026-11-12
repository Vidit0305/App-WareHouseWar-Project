using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using WarehouseDuel.Core;
using WarehouseDuel.UI;
using WarehouseDuel.UI.Controls;
using WarehouseDuel.UI.Widgets;
using WarehouseDuel.Weapons;
using static WarehouseDuel.EditorTools.UI.UiFactory;

namespace WarehouseDuel.EditorTools.UI
{
    /// <summary>
    /// Adds the in-match UI to the WarehouseMap scene: HUD, touch controls, pause menu and results screen.
    /// Regenerates the "Match UI" root each time; the map itself is untouched.
    /// </summary>
    internal static class HudBuilder
    {
        private const string RootName = "Match UI";

        public static void Build(SetupReport report, WeaponCatalog catalog)
        {
            // Reload: lighting bakes refresh assets, which can leave a previously loaded catalog object stale.
            catalog = AssetDatabase.LoadAssetAtPath<WeaponCatalog>(WarehouseDuel.EditorTools.Weapons.WeaponAssetBuilder.CatalogPath);
            string path = GameConstants.Scenes.PathOf(GameConstants.Scenes.WarehouseMap);
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

            GameObject touch = BuildTouchControls(safe);
            RectTransform gameplay = CreateRect("Gameplay HUD", safe);
            Stretch(gameplay);

            // ---- health (top-left)
            Image healthPanel = CreatePanel("Health", gameplay, new Color(0f, 0f, 0f, 0.45f));
            healthPanel.raycastTarget = false;
            Place(healthPanel.rectTransform, new Vector2(0f, 1f), new Vector2(30f, -24f), new Vector2(520f, 86f), new Vector2(0f, 1f));
            TextMeshProUGUI hpLabel = CreateText("Label", healthPanel.transform, "HP", 26f, TextMuted, TextAlignmentOptions.Left, display: true);
            Place(hpLabel.rectTransform, new Vector2(0f, 0.5f), new Vector2(22f, 0f), new Vector2(60f, 40f), new Vector2(0f, 0.5f));
            Image track = CreateImage("Track", healthPanel.transform, new Color(1f, 1f, 1f, 0.12f), UiArt.RoundedSmall);
            Place(track.rectTransform, new Vector2(0f, 0.5f), new Vector2(80f, 0f), new Vector2(320f, 26f), new Vector2(0f, 0.5f));
            Image fill = CreateImage("Fill", track.transform, Success, UiArt.RoundedSmall);
            fill.rectTransform.anchorMin = Vector2.zero;
            fill.rectTransform.anchorMax = Vector2.one;
            fill.rectTransform.offsetMin = fill.rectTransform.offsetMax = Vector2.zero;
            TextMeshProUGUI hpValue = CreateText("Value", healthPanel.transform, "100", 40f, TextPrimary, TextAlignmentOptions.Right, display: true);
            Place(hpValue.rectTransform, new Vector2(1f, 0.5f), new Vector2(-20f, 0f), new Vector2(100f, 50f), new Vector2(1f, 0.5f));

            // ---- timer + scores (top-centre)
            Image scorePanel = CreatePanel("Scoreboard", safe, new Color(0f, 0f, 0f, 0.45f));
            scorePanel.raycastTarget = false;
            Place(scorePanel.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -18f), new Vector2(720f, 120f), new Vector2(0.5f, 1f));
            TextMeshProUGUI timer = CreateText("Timer", scorePanel.transform, "3:00", 64f, TextPrimary, TextAlignmentOptions.Center, display: true);
            Place(timer.rectTransform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(220f, 90f), new Vector2(0.5f, 0.5f));
            TextMeshProUGUI myName = CreateText("My Name", scorePanel.transform, "YOU", 22f, new Color(0.45f, 0.7f, 1f), TextAlignmentOptions.Center);
            Place(myName.rectTransform, new Vector2(0f, 0.5f), new Vector2(130f, 30f), new Vector2(220f, 30f), new Vector2(0.5f, 0.5f));
            TextMeshProUGUI myKills = CreateText("My Kills", scorePanel.transform, "0", 56f, TextPrimary, TextAlignmentOptions.Center, display: true);
            Place(myKills.rectTransform, new Vector2(0f, 0.5f), new Vector2(130f, -16f), new Vector2(220f, 64f), new Vector2(0.5f, 0.5f));
            TextMeshProUGUI theirName = CreateText("Opponent Name", scorePanel.transform, "—", 22f, new Color(1f, 0.5f, 0.4f), TextAlignmentOptions.Center);
            Place(theirName.rectTransform, new Vector2(1f, 0.5f), new Vector2(-130f, 30f), new Vector2(220f, 30f), new Vector2(0.5f, 0.5f));
            TextMeshProUGUI theirKills = CreateText("Opponent Kills", scorePanel.transform, "0", 56f, TextPrimary, TextAlignmentOptions.Center, display: true);
            Place(theirKills.rectTransform, new Vector2(1f, 0.5f), new Vector2(-130f, -16f), new Vector2(220f, 64f), new Vector2(0.5f, 0.5f));

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

            EditorSceneManager.MarkSceneDirty(scene);
            if (EditorSceneManager.SaveScene(scene)) report.Pass("Match UI (HUD, touch controls, pause, results) added to WarehouseMap.");
            else report.Error("Could not save WarehouseMap after adding the HUD.");
        }

        private static GameObject BuildTouchControls(RectTransform safe)
        {
            RectTransform root = CreateRect("Touch Controls", safe);
            Stretch(root);

            // Left zone (joystick). Pivot on the screen centre so left-handed mode can mirror it.
            Image leftZone = CreateImage("Left Zone", root, new Color(0f, 0f, 0f, 0f), raycast: true);
            leftZone.rectTransform.anchorMin = new Vector2(0f, 0f);
            leftZone.rectTransform.anchorMax = new Vector2(0.5f, 0.78f);
            leftZone.rectTransform.pivot = new Vector2(1f, 0.5f);
            leftZone.rectTransform.offsetMin = leftZone.rectTransform.offsetMax = Vector2.zero;
            VirtualJoystick joystick = leftZone.gameObject.AddComponent<VirtualJoystick>();
            Image stickBase = CreateImage("Stick Base", leftZone.transform, new Color(1f, 1f, 1f, 0.35f), UiArt.Ring);
            Place(stickBase.rectTransform, new Vector2(0f, 0f), new Vector2(280f, 280f), new Vector2(280f, 280f), new Vector2(0.5f, 0.5f));
            Image handle = CreateImage("Handle", stickBase.transform, new Color(1f, 1f, 1f, 0.6f), UiArt.Circle);
            Place(handle.rectTransform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(130f, 130f), new Vector2(0.5f, 0.5f));
            Wire(joystick, "stickBase", stickBase.rectTransform);
            Wire(joystick, "handle", handle.rectTransform);

            // Right zone (look drag + action buttons).
            Image rightZone = CreateImage("Right Zone", root, new Color(0f, 0f, 0f, 0f), raycast: true);
            rightZone.rectTransform.anchorMin = new Vector2(0.5f, 0f);
            rightZone.rectTransform.anchorMax = new Vector2(1f, 0.82f);
            rightZone.rectTransform.pivot = new Vector2(0f, 0.5f);
            rightZone.rectTransform.offsetMin = rightZone.rectTransform.offsetMax = Vector2.zero;
            TouchLookArea look = rightZone.gameObject.AddComponent<TouchLookArea>();

            TouchButton MakeButton(string name, Transform parent, string icon, float size, Vector2 anchor, Vector2 position, TouchButton.ButtonMode mode, bool forwardLook)
            {
                Image background = CreateImage(name, parent, new Color(1f, 1f, 1f, 0.35f), UiArt.Circle, raycast: true);
                Place(background.rectTransform, anchor, position, new Vector2(size, size), new Vector2(0.5f, 0.5f));
                Image glyph = CreateImage("Icon", background.transform, new Color(1f, 1f, 1f, 0.95f), UiArt.Icon(icon));
                Stretch(glyph.rectTransform, size * 0.24f, size * 0.24f, size * 0.24f, size * 0.24f);
                TouchButton button = background.gameObject.AddComponent<TouchButton>();
                SetEnum(button, "mode", (int)mode);
                if (forwardLook) Wire(button, "forwardDragTo", look);
                Wire(button, "highlight", background);
                return button;
            }

            Vector2 bottomRight = new Vector2(1f, 0f);
            Vector2 bottomLeft = new Vector2(0f, 0f);
            TouchButton fire = MakeButton("Fire", rightZone.transform, "Fire", 210f, bottomRight, new Vector2(-210f, 250f), TouchButton.ButtonMode.Hold, true);
            TouchButton aim = MakeButton("Aim", rightZone.transform, "Aim", 130f, bottomRight, new Vector2(-445f, 140f), TouchButton.ButtonMode.Hold, true);
            TouchButton reload = MakeButton("Reload", rightZone.transform, "Reload", 120f, bottomRight, new Vector2(-460f, 330f), TouchButton.ButtonMode.Hold, false);
            TouchButton jump = MakeButton("Jump", rightZone.transform, "Jump", 140f, bottomRight, new Vector2(-170f, 500f), TouchButton.ButtonMode.Hold, false);
            TouchButton swap = MakeButton("Switch Weapon", rightZone.transform, "Swap", 110f, bottomRight, new Vector2(-400f, 510f), TouchButton.ButtonMode.Hold, false);
            TouchButton sprint = MakeButton("Sprint", leftZone.transform, "Sprint", 120f, bottomLeft, new Vector2(530f, 470f), TouchButton.ButtonMode.Toggle, false);
            TouchButton fireLeft = MakeButton("Fire Left", leftZone.transform, "Fire", 150f, bottomLeft, new Vector2(170f, 620f), TouchButton.ButtonMode.Hold, false);

            TouchControlsView view = root.gameObject.AddComponent<TouchControlsView>();
            Wire(view, "joystick", joystick);
            Wire(view, "lookArea", look);
            Wire(view, "fire", fire);
            Wire(view, "fireLeft", fireLeft);
            Wire(view, "aim", aim);
            Wire(view, "reload", reload);
            Wire(view, "jump", jump);
            Wire(view, "sprint", sprint);
            Wire(view, "switchWeapon", swap);
            Wire(view, "leftZone", leftZone.rectTransform);
            Wire(view, "rightZone", rightZone.rectTransform);
            WireArray(view, "scalableButtons",
                (RectTransform)fire.transform, (RectTransform)aim.transform, (RectTransform)reload.transform,
                (RectTransform)jump.transform, (RectTransform)swap.transform, (RectTransform)sprint.transform,
                (RectTransform)fireLeft.transform, stickBase.rectTransform);
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
            RectTransform card = SharedUiBuilder.Modal("Panel", host, new Vector2(1320f, 820f), out GameObject panel);

            Image band = CreateImage("Title Band", card, UiFactory.Accent, UiArt.Rounded);
            Place(band.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -30f), new Vector2(1240f, 140f), new Vector2(0.5f, 1f));
            TextMeshProUGUI title = CreateText("Title", band.transform, "VICTORY", 96f, new Color(0.06f, 0.07f, 0.1f), TextAlignmentOptions.Center, display: true);
            Stretch(title.rectTransform);
            TextMeshProUGUI subtitle = CreateText("Subtitle", card, "", 32f, TextPrimary);
            Place(subtitle.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -190f), new Vector2(1200f, 50f), new Vector2(0.5f, 1f));

            TextMeshProUGUI MakeColumn(string name, float x, out TextMeshProUGUI kills, out TextMeshProUGUI weapon, out TextMeshProUGUI tag)
            {
                Image column = CreatePanel(name, card, new Color(1f, 1f, 1f, 0.06f));
                Place(column.rectTransform, new Vector2(0.5f, 1f), new Vector2(x, -270f), new Vector2(520f, 300f), new Vector2(0.5f, 1f));
                TextMeshProUGUI playerName = CreateText("Name", column.transform, "", 38f, TextPrimary, TextAlignmentOptions.Center, display: true);
                Place(playerName.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -20f), new Vector2(480f, 56f), new Vector2(0.5f, 1f));
                tag = CreateText("Tag", column.transform, "", 22f, Accent, TextAlignmentOptions.Center, display: true);
                Place(tag.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -76f), new Vector2(480f, 30f), new Vector2(0.5f, 1f));
                kills = CreateText("Kills", column.transform, "0", 110f, TextPrimary, TextAlignmentOptions.Center, display: true);
                Place(kills.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -100f), new Vector2(480f, 120f), new Vector2(0.5f, 1f));
                TextMeshProUGUI killsLabel = CreateText("Kills Label", column.transform, "ELIMINATIONS", 20f, TextMuted, TextAlignmentOptions.Center);
                Place(killsLabel.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -218f), new Vector2(480f, 30f), new Vector2(0.5f, 1f));
                weapon = CreateText("Weapon", column.transform, "", 26f, TextMuted, TextAlignmentOptions.Center);
                Place(weapon.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -254f), new Vector2(480f, 36f), new Vector2(0.5f, 1f));
                return playerName;
            }

            TextMeshProUGUI leftName = MakeColumn("Left", -290f, out TextMeshProUGUI leftKills, out TextMeshProUGUI leftWeapon, out TextMeshProUGUI leftTag);
            TextMeshProUGUI rightName = MakeColumn("Right", 290f, out TextMeshProUGUI rightKills, out TextMeshProUGUI rightWeapon, out TextMeshProUGUI rightTag);
            TextMeshProUGUI vs = CreateText("VS", card, "VS", 48f, TextMuted, TextAlignmentOptions.Center, display: true);
            Place(vs.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -390f), new Vector2(100f, 60f), new Vector2(0.5f, 1f));

            TextMeshProUGUI details = CreateText("Details", card, "", 28f, TextMuted);
            Place(details.rectTransform, new Vector2(0.5f, 0f), new Vector2(0f, 150f), new Vector2(1200f, 44f), new Vector2(0.5f, 0f));

            Button again = CreateButton("Play Again", card, "PLAY AGAIN", ButtonStyle.Primary, new Vector2(400f, 100f), out TextMeshProUGUI againLabel, 36f);
            Place((RectTransform)again.transform, new Vector2(0.5f, 0f), new Vector2(-420f, 30f), new Vector2(400f, 100f), new Vector2(0.5f, 0f));
            Button lobby = CreateButton("Lobby", card, "RETURN TO LOBBY", ButtonStyle.Secondary, new Vector2(400f, 100f), 32f);
            Place((RectTransform)lobby.transform, new Vector2(0.5f, 0f), new Vector2(0f, 30f), new Vector2(400f, 100f), new Vector2(0.5f, 0f));
            Button menu = CreateButton("Menu", card, "MAIN MENU", ButtonStyle.Ghost, new Vector2(400f, 100f), 32f);
            Place((RectTransform)menu.transform, new Vector2(0.5f, 0f), new Vector2(420f, 30f), new Vector2(400f, 100f), new Vector2(0.5f, 0f));

            ResultsController results = host.gameObject.AddComponent<ResultsController>();
            Wire(results, "catalog", catalog);
            Wire(results, "panel", panel);
            Wire(results, "titleText", title);
            Wire(results, "subtitleText", subtitle);
            Wire(results, "leftNameText", leftName);
            Wire(results, "leftKillsText", leftKills);
            Wire(results, "leftWeaponText", leftWeapon);
            Wire(results, "leftTagText", leftTag);
            Wire(results, "rightNameText", rightName);
            Wire(results, "rightKillsText", rightKills);
            Wire(results, "rightWeaponText", rightWeapon);
            Wire(results, "rightTagText", rightTag);
            Wire(results, "detailsText", details);
            Wire(results, "titleBand", band);
            Wire(results, "playAgainButton", again);
            Wire(results, "playAgainText", againLabel);
            Wire(results, "lobbyButton", lobby);
            Wire(results, "menuButton", menu);
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
