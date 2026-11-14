using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using WarehouseDuel.Core;
using WarehouseDuel.Profile;
using WarehouseDuel.UI;
using WarehouseDuel.UI.Widgets;
using WarehouseDuel.Weapons;
using static WarehouseDuel.EditorTools.UI.UiFactory;

namespace WarehouseDuel.EditorTools.UI
{
    /// <summary>Generates the MainMenu and Lobby scenes (3D diorama background + fully wired UI).</summary>
    internal static class MenuScenesBuilder
    {
        public static void BuildMainMenu(SetupReport report, WeaponCatalog catalog)
        {
            // Reload: lighting bakes refresh assets, which can leave a previously loaded catalog object stale.
            catalog = AssetDatabase.LoadAssetAtPath<WeaponCatalog>(WarehouseDuel.EditorTools.Weapons.WeaponAssetBuilder.CatalogPath);
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            SharedUiBuilder.SoldierAnimatorHandle showcase = SharedUiBuilder.Diorama(catalog, 0, SharedUiBuilder.MenuStaging);
            BakeBackdrop(scene, GameConstants.Scenes.MainMenu, report);
            catalog = AssetDatabase.LoadAssetAtPath<WeaponCatalog>(WarehouseDuel.EditorTools.Weapons.WeaponAssetBuilder.CatalogPath);
            ProfileCatalog profiles = AssetDatabase.LoadAssetAtPath<ProfileCatalog>(ProfileArtBuilder.CatalogPath);
            Wire(showcase.Animator, "catalog", catalog);
            SharedUiBuilder.SessionBootstrap();
            CreateEventSystem();

            Canvas canvas = CreateCanvas("Menu Canvas", 0, out RectTransform safe);

            // Soft dark edges so the HUD-style buttons read well over the 3D scene.
            Image vignette = CreateImage("Vignette", canvas.transform, new Color(0f, 0f, 0f, 0.55f), UiArt.Gradient);
            Stretch(vignette.rectTransform, -60f, -60f, -60f, -60f);
            vignette.transform.SetAsFirstSibling();

            RectTransform home = CreateRect("Home", safe);
            Stretch(home);

            // Drag the soldier to spin him (behind everything else).
            Image drag = CreateImage("Character Drag Area", home, new Color(0f, 0f, 0f, 0f), raycast: true);
            Place(drag.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, -20f), new Vector2(620f, 760f), new Vector2(0.5f, 0.5f));
            Wire(drag.gameObject.AddComponent<DragRotate>(), "target", showcase.Turntable);

            // ---- top centre: logo
            TextMeshProUGUI logo = CreateText("Logo", home, "WAREHOUSE <color=#FFB020>DUEL</color>", 60f, TextPrimary, TextAlignmentOptions.Center, display: true);
            Place(logo.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -26f), new Vector2(760f, 78f), new Vector2(0.5f, 1f));
            TextMeshProUGUI tagline = CreateText("Tagline", home, "1V1 ONLINE ARENA SHOOTER", 20f, TextMuted, TextAlignmentOptions.Center, display: true);
            tagline.characterSpacing = 12f;
            Place(tagline.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -100f), new Vector2(760f, 30f), new Vector2(0.5f, 1f));
            AddTransition(logo.rectTransform, new Vector2(0f, 80f), 0f);

            // ---- top left: profile chip
            Button chip = CreateButton("Profile Chip", home, "", ButtonStyle.Ghost, new Vector2(600f, 132f));
            RectTransform chipRect = Place((RectTransform)chip.transform, new Vector2(0f, 1f), new Vector2(30f, -24f), new Vector2(600f, 132f), new Vector2(0f, 1f));
            Image chipBack = chip.GetComponent<Image>();
            chipBack.color = new Color(0.08f, 0.09f, 0.13f, 1f);
            chip.gameObject.AddComponent<Mask>().showMaskGraphic = true;
            Image chipBanner = CreateImage("Banner", chip.transform, Color.white, profiles.Banner(0));
            chipBanner.type = Image.Type.Simple;
            Stretch(chipBanner.rectTransform);
            Image chipShade = CreateImage("Shade", chip.transform, new Color(0f, 0f, 0f, 0.45f));
            Stretch(chipShade.rectTransform);
            Image chipFrame = CreateImage("Avatar Frame", chip.transform, Color.white, UiArt.Circle);
            Place(chipFrame.rectTransform, new Vector2(0f, 0.5f), new Vector2(16f, 0f), new Vector2(106f, 106f), new Vector2(0f, 0.5f));
            Image chipAvatar = MenuScreensBuilder.Masked(chipFrame.transform, "Avatar Mask", UiArt.Circle, profiles.Avatar(0), out RectTransform chipMask);
            Stretch(chipMask, 5f, 5f, 5f, 5f);
            Image chipLevel = CreateImage("Level Badge", chip.transform, Accent, UiArt.Circle);
            Place(chipLevel.rectTransform, new Vector2(0f, 0f), new Vector2(88f, 8f), new Vector2(44f, 44f), new Vector2(0f, 0f));
            TextMeshProUGUI chipLevelText = CreateText("Level", chipLevel.transform, "1", 22f, AccentDark, TextAlignmentOptions.Center, display: true);
            Stretch(chipLevelText.rectTransform);
            TextMeshProUGUI chipName = MenuScreensBuilder.Label(chip.transform, "Name", "Player", 38f, TextPrimary, 140f, -14f, 440f, 50f, display: true);
            TextMeshProUGUI chipTitle = MenuScreensBuilder.Label(chip.transform, "Title", "ROOKIE", 20f, Accent, 142f, -64f, 300f, 28f, display: true);
            chipTitle.characterSpacing = 6f;
            Image chipXpTrack = CreateImage("XP Track", chip.transform, new Color(0f, 0f, 0f, 0.55f), UiArt.RoundedSmall);
            MenuScreensBuilder.At(chipXpTrack.rectTransform, 142f, -100f, 300f, 12f);
            Image chipXp = CreateImage("XP Fill", chipXpTrack.transform, Accent, UiArt.RoundedSmall);
            chipXp.rectTransform.anchorMin = Vector2.zero;
            chipXp.rectTransform.anchorMax = new Vector2(0.3f, 1f);
            chipXp.rectTransform.offsetMin = chipXp.rectTransform.offsetMax = Vector2.zero;
            Image chipEdit = CreateImage("Open", chip.transform, new Color(1f, 1f, 1f, 0.75f), UiArt.Icon("Back"));
            Place(chipEdit.rectTransform, new Vector2(1f, 0.5f), new Vector2(-40f, 0f), new Vector2(40f, 40f), new Vector2(0.5f, 0.5f));
            chipEdit.rectTransform.localRotation = Quaternion.Euler(0f, 0f, 180f);
            ProfileCard chipCard = chip.gameObject.AddComponent<ProfileCard>();
            Wire(chipCard, "avatar", chipAvatar);
            Wire(chipCard, "banner", chipBanner);
            Wire(chipCard, "nameText", chipName);
            Wire(chipCard, "titleText", chipTitle);
            Wire(chipCard, "levelText", chipLevelText);
            Wire(chipCard, "xpFill", chipXp.rectTransform);
            AddTransition(chipRect, new Vector2(-200f, 0f), 0.05f);

            // ---- top right: wins + settings
            RectTransform topRight = CreateRect("Top Right", home);
            Place(topRight, new Vector2(1f, 1f), new Vector2(-30f, -24f), new Vector2(440f, 140f), new Vector2(1f, 1f));
            AddTransition(topRight, new Vector2(200f, 0f), 0.05f);
            Image winsChip = CreateImage("Stars", topRight, new Color(0.04f, 0.05f, 0.08f, 0.85f), UiArt.Rounded);
            Place(winsChip.rectTransform, new Vector2(0f, 1f), new Vector2(0f, -14f), new Vector2(250f, 76f), new Vector2(0f, 1f));
            Image trophy = CreateImage("Star", winsChip.transform, Accent, UiArt.Icon("Star"));
            Place(trophy.rectTransform, new Vector2(0f, 0.5f), new Vector2(14f, 0f), new Vector2(52f, 52f), new Vector2(0f, 0.5f));
            TextMeshProUGUI wins = CreateText("Count", winsChip.transform, "0", 38f, TextPrimary, TextAlignmentOptions.Left, display: true);
            Place(wins.rectTransform, new Vector2(0f, 0.5f), new Vector2(78f, 2f), new Vector2(100f, 60f), new Vector2(0f, 0.5f));
            TextMeshProUGUI winsLabel = CreateText("Label", winsChip.transform, "STARS", 18f, TextMuted, TextAlignmentOptions.Right, display: true);
            Place(winsLabel.rectTransform, new Vector2(1f, 0.5f), new Vector2(-16f, 0f), new Vector2(80f, 40f), new Vector2(1f, 0.5f));
            Button settings = LabelledIcon(topRight, "Settings", "SETTINGS", UiArt.Icon("Settings"), new Vector2(1f, 1f), new Vector2(0f, 0f));

            // ---- left: feature cards
            Button armoryCard = FeatureCard(home, "Armory Card", "ARMORY", "Choose your weapon", catalog.Get(0) != null ? catalog.Get(0).icon : null, true, -200f);
            CharacterCatalog characters = AssetDatabase.LoadAssetAtPath<CharacterCatalog>(WarehouseDuel.EditorTools.Gameplay.CharacterBuilder.CatalogPath);
            Button characterCard = FeatureCard(home, "Wardrobe Card", "WARDROBE", "Change your costume", UiArt.Icon("Profile"), false, -390f,
                new Color(0.5f, 0.24f, 0.82f, 1f));
            Button shopCard = FeatureCard(home, "Shop Card", "SHOP", "Costumes, avatars & banners", UiArt.Icon("Star"), false, -580f,
                new Color(0.16f, 0.58f, 0.34f, 1f));
            Button leaderboard = CreateButton("Leaderboard", home, "LEADERBOARD", ButtonStyle.Secondary, new Vector2(420f, 110f), out TextMeshProUGUI leaderboardLabel, 32f);
            RectTransform leaderboardRect = Place((RectTransform)leaderboard.transform, new Vector2(0f, 0f), new Vector2(30f, 30f), new Vector2(420f, 110f), new Vector2(0f, 0f));
            leaderboard.GetComponent<Image>().color = new Color(0.12f, 0.14f, 0.19f, 0.95f);
            Stretch(leaderboardLabel.rectTransform, 110f, 16f, 4f, 4f);
            leaderboardLabel.alignment = TextAlignmentOptions.Left;
            Image podium = CreateImage("Icon", leaderboard.transform, Accent, UiArt.Icon("Trophy"));
            Place(podium.rectTransform, new Vector2(0f, 0.5f), new Vector2(28f, 0f), new Vector2(64f, 64f), new Vector2(0f, 0.5f));
            AddTransition(leaderboardRect, new Vector2(-220f, 0f), 0.2f);

            // ---- right: game modes
            Button online = ModeCard(home, "Online Mode", "ONLINE DUEL", "Create a room or join a friend", "1V1 ONLINE", profiles.Banner(7), catalog.Get(0) != null ? catalog.Get(0).icon : null, -200f, Accent);
            Button practice = ModeCard(home, "Practice Mode", "PRACTICE", "Offline training in the warehouse", "OFFLINE", profiles.Banner(0), catalog.Get(3) != null ? catalog.Get(3).icon : null, -420f, Success);

            // ---- bottom right: PLAY
            Button play = CreateButton("Play", home, "PLAY", ButtonStyle.Primary, new Vector2(440f, 150f), out TextMeshProUGUI playLabel, 72f);
            RectTransform playRect = Place((RectTransform)play.transform, new Vector2(1f, 0f), new Vector2(-30f, 30f), new Vector2(440f, 150f), new Vector2(1f, 0f));
            Stretch(playLabel.rectTransform, 30f, 110f, 4f, 4f);
            playLabel.enableAutoSizing = true;
            playLabel.fontSizeMin = 22f;
            playLabel.fontSizeMax = 72f;
            playLabel.lineSpacing = -20f;
            Image playIcon = CreateImage("Icon", play.transform, AccentDark, UiArt.Icon("Play"));
            Place(playIcon.rectTransform, new Vector2(1f, 0.5f), new Vector2(-50f, 0f), new Vector2(64f, 64f), new Vector2(1f, 0.5f));
            SetPulse(play);
            AddTransition(playRect, new Vector2(240f, 0f), 0.25f);

            // ---- screens
            ArmoryScreen armory = MenuScreensBuilder.Armory(safe, catalog, showcase.Animator, showcase.Turntable);
            CharacterScreen characterScreen = MenuScreensBuilder.Characters(safe, characters, showcase.Animator, showcase.Turntable);
            ShopScreen shopScreen = MenuScreensBuilder.ShopPage(safe, characters, profiles);
            ProfileScreen profile = MenuScreensBuilder.Profile(safe, profiles);
            LeaderboardScreen leaderboardScreen = MenuScreensBuilder.Leaderboard(safe, catalog, profiles);
            GameObject settingsPanel = SharedUiBuilder.Settings(safe, out Button settingsClose);

            // ---- map picker (quick match + create room): four maps and Random
            RectTransform mapCard = SharedUiBuilder.Modal("Map Picker", safe, new Vector2(1500f, 640f), out GameObject mapPanel);
            mapCard.GetComponent<Image>().color = new Color(0.07f, 0.08f, 0.12f, 0.98f);
            TextMeshProUGUI mapTitle = MenuScreensBuilder.Label(mapCard, "Title", "QUICK MATCH — CHOOSE A MAP", 46f, Accent, 0f, -30f, 1500f, 64f, TextAlignmentOptions.Center, display: true);
            string[] mapNames = { "WAREHOUSE", "DESERT", "FOREST", "SNOW", "RANDOM" };
            string[] mapHints = { "Indoor close quarters", "Ruins under the sun", "Trees, logs and cabins", "Frozen outpost", "Surprise me" };
            int[] mapArt = { 0, 3, 2, 5, 7 };
            var mapButtons = new Button[mapNames.Length];
            for (int i = 0; i < mapNames.Length; i++)
            {
                Button tile = CreateButton($"Map {mapNames[i]}", mapCard, "", ButtonStyle.Ghost, new Vector2(264f, 360f));
                MenuScreensBuilder.At((RectTransform)tile.transform, 40f + i * 288f, -120f, 264f, 360f);
                Image art = MenuScreensBuilder.Masked(tile.transform, "Art", UiArt.Rounded, profiles.Banner(mapArt[i]), out RectTransform artMask);
                Stretch(artMask);
                art.preserveAspect = false;
                Image shade = CreateImage("Shade", tile.transform, new Color(0f, 0f, 0f, 0.45f), UiArt.Rounded);
                Stretch(shade.rectTransform);
                if (i == mapNames.Length - 1)
                {
                    TextMeshProUGUI question = CreateText("Question", tile.transform, "?", 160f, new Color(1f, 1f, 1f, 0.85f), TextAlignmentOptions.Center, display: true);
                    Place(question.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, 30f), new Vector2(240f, 200f), new Vector2(0.5f, 0.5f));
                }
                MenuScreensBuilder.Label(tile.transform, "Name", mapNames[i], 34f, TextPrimary, 0f, -272f, 264f, 44f, TextAlignmentOptions.Center, display: true);
                MenuScreensBuilder.Label(tile.transform, "Hint", mapHints[i], 18f, new Color(1f, 1f, 1f, 0.75f), 0f, -316f, 264f, 30f, TextAlignmentOptions.Center);
                mapButtons[i] = tile;
            }
            Button mapBack = CreateButton("Back", mapCard, "BACK", ButtonStyle.Ghost, new Vector2(300f, 80f));
            Place((RectTransform)mapBack.transform, new Vector2(0.5f, 0f), new Vector2(0f, 26f), new Vector2(300f, 80f), new Vector2(0.5f, 0f));

            // ---- online dialogs
            RectTransform onlineCard = SharedUiBuilder.Modal("Online", safe, new Vector2(820f, 640f), out GameObject onlinePanel);
            Image onlineHeader = CreateImage("Header", onlineCard, new Color(0f, 0f, 0f, 0.3f), UiArt.Rounded);
            MenuScreensBuilder.At(onlineHeader.rectTransform, 0f, 0f, 820f, 110f);
            MenuScreensBuilder.Label(onlineHeader.transform, "Title", "PLAY ONLINE", 50f, Accent, 0f, -18f, 820f, 76f, TextAlignmentOptions.Center, display: true);
            Button create = CreateButton("Create Room", onlineCard, "CREATE ROOM", ButtonStyle.Primary, new Vector2(660f, 120f), 44f);
            Place((RectTransform)create.transform, new Vector2(0.5f, 1f), new Vector2(0f, -150f), new Vector2(660f, 120f), new Vector2(0.5f, 1f));
            DialogHint(onlineCard, "Host a private match and share the room code with a friend.", -280f);
            Button join = CreateButton("Join Room", onlineCard, "JOIN ROOM", ButtonStyle.Secondary, new Vector2(660f, 120f), 44f);
            Place((RectTransform)join.transform, new Vector2(0.5f, 1f), new Vector2(0f, -340f), new Vector2(660f, 120f), new Vector2(0.5f, 1f));
            DialogHint(onlineCard, "Enter the code your friend shared with you.", -470f);
            Button onlineBack = CreateButton("Back", onlineCard, "BACK", ButtonStyle.Ghost, new Vector2(300f, 80f));
            Place((RectTransform)onlineBack.transform, new Vector2(0.5f, 0f), new Vector2(0f, 30f), new Vector2(300f, 80f), new Vector2(0.5f, 0f));

            RectTransform joinCard = SharedUiBuilder.Modal("Join", safe, new Vector2(820f, 600f), out GameObject joinPanel);
            Image joinHeader = CreateImage("Header", joinCard, new Color(0f, 0f, 0f, 0.3f), UiArt.Rounded);
            MenuScreensBuilder.At(joinHeader.rectTransform, 0f, 0f, 820f, 110f);
            MenuScreensBuilder.Label(joinHeader.transform, "Title", "JOIN ROOM", 50f, Accent, 0f, -18f, 820f, 76f, TextAlignmentOptions.Center, display: true);
            DialogHint(joinCard, "Room code (e.g. K7QPM-E)", -136f);
            TMP_InputField codeInput = CreateInputField("Code Input", joinCard, "ROOM CODE", new Vector2(660f, 120f), 60f);
            Place((RectTransform)codeInput.transform, new Vector2(0.5f, 1f), new Vector2(0f, -190f), new Vector2(660f, 120f), new Vector2(0.5f, 1f));
            codeInput.characterValidation = TMP_InputField.CharacterValidation.None;
            codeInput.textComponent.characterSpacing = 18f;
            codeInput.textComponent.alignment = TextAlignmentOptions.Center;
            ((TextMeshProUGUI)codeInput.placeholder).alignment = TextAlignmentOptions.Center;
            TextMeshProUGUI joinError = CreateText("Error", joinCard, "", 26f, Danger, TextAlignmentOptions.Center);
            Place(joinError.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -320f), new Vector2(700f, 60f), new Vector2(0.5f, 1f));
            Button joinConfirm = CreateButton("Join", joinCard, "JOIN", ButtonStyle.Primary, new Vector2(320f, 110f), 44f);
            Place((RectTransform)joinConfirm.transform, new Vector2(0.5f, 0f), new Vector2(170f, 34f), new Vector2(320f, 110f), new Vector2(0.5f, 0f));
            Button joinBack = CreateButton("Back", joinCard, "BACK", ButtonStyle.Ghost, new Vector2(300f, 110f));
            Place((RectTransform)joinBack.transform, new Vector2(0.5f, 0f), new Vector2(-170f, 34f), new Vector2(300f, 110f), new Vector2(0.5f, 0f));

            // Busy: spinner, title, live status line.
            RectTransform busyCard = SharedUiBuilder.Modal("Busy", safe, new Vector2(760f, 440f), out GameObject busyPanel);
            busyCard.GetComponent<Image>().color = new Color(0.07f, 0.08f, 0.12f, 0.98f);
            Image busyAccent = CreateImage("Accent", busyCard, Accent, UiArt.RoundedSmall);
            Place(busyAccent.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -10f), new Vector2(160f, 6f), new Vector2(0.5f, 1f));
            RectTransform spinner = SharedUiBuilder.CreateSpinner(busyCard, 120f);
            Place(spinner, new Vector2(0.5f, 1f), new Vector2(0f, -56f), new Vector2(120f, 120f), new Vector2(0.5f, 1f));
            TextMeshProUGUI busyTitle = CreateText("Title", busyCard, "PLEASE WAIT", 40f, Accent, TextAlignmentOptions.Center, display: true);
            busyTitle.characterSpacing = 6f;
            Place(busyTitle.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -206f), new Vector2(700f, 56f), new Vector2(0.5f, 1f));
            TextMeshProUGUI busyText = CreateText("Text", busyCard, "Connecting…", 30f, TextPrimary, TextAlignmentOptions.Center);
            Place(busyText.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -268f), new Vector2(680f, 90f), new Vector2(0.5f, 1f));
            TextMeshProUGUI busyHint = CreateText("Hint", busyCard, "This usually takes a few seconds.", 22f, TextMuted, TextAlignmentOptions.Center);
            Place(busyHint.rectTransform, new Vector2(0.5f, 0f), new Vector2(0f, 30f), new Vector2(680f, 36f), new Vector2(0.5f, 0f));

            // Message: alert icon, title, text, OK.
            RectTransform messageCard = SharedUiBuilder.Modal("Message Panel", safe, new Vector2(900f, 500f), out GameObject messagePanel);
            messageCard.GetComponent<Image>().color = new Color(0.07f, 0.08f, 0.12f, 0.98f);
            Image messageBand = CreateImage("Band", messageCard, new Color(0.92f, 0.31f, 0.24f, 1f), UiArt.Rounded);
            Place(messageBand.rectTransform, new Vector2(0.5f, 1f), Vector2.zero, new Vector2(900f, 120f), new Vector2(0.5f, 1f));
            Image messageIcon = CreateImage("Icon", messageBand.transform, Color.white, UiArt.Icon("Alert"));
            Place(messageIcon.rectTransform, new Vector2(0f, 0.5f), new Vector2(40f, 0f), new Vector2(72f, 72f), new Vector2(0f, 0.5f));
            TextMeshProUGUI messageTitle = CreateText("Title", messageBand.transform, "NOTICE", 42f, Color.white, TextAlignmentOptions.Left, display: true);
            Place(messageTitle.rectTransform, new Vector2(0f, 0.5f), new Vector2(136f, 0f), new Vector2(720f, 70f), new Vector2(0f, 0.5f));
            TextMeshProUGUI messageText = CreateText("Text", messageCard, "", 31f, TextPrimary, TextAlignmentOptions.Center);
            Stretch(messageText.rectTransform, 60f, 60f, 150f, 150f);
            Button messageOk = CreateButton("OK", messageCard, "OK", ButtonStyle.Primary, new Vector2(320f, 96f), 40f);
            Place((RectTransform)messageOk.transform, new Vector2(0.5f, 0f), new Vector2(0f, 34f), new Vector2(320f, 96f), new Vector2(0.5f, 0f));

            var controller = canvas.gameObject.AddComponent<MainMenuController>();
            Wire(controller, "homePanel", home.gameObject);
            Wire(controller, "character", showcase.Animator);
            Wire(controller, "playButton", play);
            Wire(controller, "onlineModeButton", online);
            Wire(controller, "practiceModeButton", practice);
            Wire(controller, "profileChipButton", chip);
            Wire(controller, "characterCardButton", characterCard);
            Wire(controller, "armoryCardButton", armoryCard);
            Wire(controller, "leaderboardButton", leaderboard);
            Wire(controller, "settingsButton", settings);
            Wire(controller, "starsText", wins);
            Wire(controller, "profileScreen", profile);
            Wire(controller, "leaderboardScreen", leaderboardScreen);
            Wire(controller, "armoryScreen", armory);
            Wire(controller, "characterScreen", characterScreen);
            Wire(controller, "shopScreen", shopScreen);
            Wire(controller, "shopCardButton", shopCard);
            Wire(controller, "playLabel", playLabel);
            Wire(controller, "mapPanel", mapPanel);
            Wire(controller, "mapPanelTitle", mapTitle);
            WireArray(controller, "mapButtons", mapButtons);
            Wire(controller, "mapBackButton", mapBack);
            Wire(controller, "settingsPanel", settingsPanel);
            Wire(controller, "settingsCloseButton", settingsClose);
            Wire(controller, "onlinePanel", onlinePanel);
            Wire(controller, "createRoomButton", create);
            Wire(controller, "joinRoomButton", join);
            Wire(controller, "onlineBackButton", onlineBack);
            Wire(controller, "joinPanel", joinPanel);
            Wire(controller, "codeInput", codeInput);
            Wire(controller, "joinConfirmButton", joinConfirm);
            Wire(controller, "joinBackButton", joinBack);
            Wire(controller, "joinErrorText", joinError);
            Wire(controller, "busyPanel", busyPanel);
            Wire(controller, "busyText", busyText);
            Wire(controller, "messagePanel", messagePanel);
            Wire(controller, "messageTitle", messageTitle);
            Wire(controller, "messageText", messageText);
            Wire(controller, "messageOkButton", messageOk);

            Save(scene, GameConstants.Scenes.MainMenu, report);
        }

        /// <summary>Big yellow (or dark) card on the left: title, subtitle and an icon on the right.</summary>
        private static Button FeatureCard(RectTransform parent, string name, string title, string subtitle, Sprite icon, bool primary, float y, Color? colour = null)
        {
            Button card = CreateButton(name, parent, title, primary ? ButtonStyle.Primary : ButtonStyle.Secondary, new Vector2(420f, 170f), out TextMeshProUGUI label, 40f);
            RectTransform rect = Place((RectTransform)card.transform, new Vector2(0f, 1f), new Vector2(30f, y), new Vector2(420f, 170f), new Vector2(0f, 1f));
            if (!primary) card.GetComponent<Image>().color = colour ?? new Color(0.12f, 0.14f, 0.19f, 0.95f);
            Color ink = primary ? AccentDark : TextPrimary;
            label.alignment = TextAlignmentOptions.TopLeft;
            MenuScreensBuilder.At(label.rectTransform, 28f, -26f, 260f, 56f);
            label.rectTransform.anchorMin = label.rectTransform.anchorMax = new Vector2(0f, 1f);
            MenuScreensBuilder.Label(card.transform, "Subtitle", subtitle, 21f, primary ? new Color(0.15f, 0.12f, 0.05f, 0.8f) : colour.HasValue ? new Color(1f, 1f, 1f, 0.8f) : TextMuted, 30f, -88f, 230f, 60f);
            Image glyph = CreateImage("Icon", card.transform, ink, icon);
            glyph.preserveAspect = true;
            Place(glyph.rectTransform, new Vector2(1f, 0.5f), new Vector2(-20f, -4f), new Vector2(150f, 110f), new Vector2(1f, 0.5f));
            AddTransition(rect, new Vector2(-220f, 0f), y < -300f ? 0.15f : 0.1f);
            return card;
        }

        /// <summary>Game-mode card on the right: banner art, title, subtitle, a tag and a weapon silhouette.</summary>
        private static Button ModeCard(RectTransform parent, string name, string title, string subtitle, string tag, Sprite art, Sprite icon, float y, Color tagColor)
        {
            Button card = CreateButton(name, parent, "", ButtonStyle.Ghost, new Vector2(460f, 196f));
            RectTransform rect = Place((RectTransform)card.transform, new Vector2(1f, 1f), new Vector2(-30f, y), new Vector2(460f, 196f), new Vector2(1f, 1f));
            card.GetComponent<Image>().color = new Color(0.08f, 0.09f, 0.13f, 1f);
            card.gameObject.AddComponent<Mask>().showMaskGraphic = true;
            Image artImage = CreateImage("Art", card.transform, Color.white, art);
            artImage.type = Image.Type.Simple;
            Stretch(artImage.rectTransform);
            Image shade = CreateImage("Shade", card.transform, new Color(0f, 0f, 0f, 0.5f));
            Stretch(shade.rectTransform);
            Image weapon = CreateImage("Weapon", card.transform, new Color(1f, 1f, 1f, 0.9f), icon);
            weapon.preserveAspect = true;
            Place(weapon.rectTransform, new Vector2(1f, 0f), new Vector2(-16f, 14f), new Vector2(220f, 100f), new Vector2(1f, 0f));
            MenuScreensBuilder.Label(card.transform, "Title", title, 42f, TextPrimary, 26f, -22f, 400f, 56f, display: true);
            MenuScreensBuilder.Label(card.transform, "Subtitle", subtitle, 21f, new Color(1f, 1f, 1f, 0.8f), 28f, -80f, 260f, 60f);
            Image tagBack = CreateImage("Tag", card.transform, tagColor, UiArt.RoundedSmall);
            MenuScreensBuilder.At(tagBack.rectTransform, 26f, -146f, 150f, 34f);
            TextMeshProUGUI tagText = CreateText("Text", tagBack.transform, tag, 17f, new Color(0.08f, 0.08f, 0.08f), TextAlignmentOptions.Center, display: true);
            Stretch(tagText.rectTransform);
            AddTransition(rect, new Vector2(240f, 0f), y < -300f ? 0.15f : 0.1f);
            return card;
        }

        /// <summary>Round icon button with a small caption under it (top bar).</summary>
        private static Button LabelledIcon(RectTransform parent, string name, string caption, Sprite icon, Vector2 anchor, Vector2 position)
        {
            Button button = CreateIconButton(name, parent, icon, 100f, new Color(0.04f, 0.05f, 0.08f, 0.85f));
            Place((RectTransform)button.transform, anchor, position, new Vector2(100f, 100f), anchor);
            TextMeshProUGUI text = CreateText("Caption", button.transform, caption, 17f, TextPrimary, TextAlignmentOptions.Center, display: true);
            Place(text.rectTransform, new Vector2(0.5f, 0f), new Vector2(0f, -30f), new Vector2(160f, 26f), new Vector2(0.5f, 0f));
            return button;
        }

        private static void DialogHint(RectTransform card, string text, float y)
        {
            TextMeshProUGUI hint = CreateText("Hint", card, text, 23f, TextMuted, TextAlignmentOptions.Center);
            Place(hint.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, y), new Vector2(700f, 40f), new Vector2(0.5f, 1f));
        }

        public static void BuildLobby(SetupReport report, WeaponCatalog catalog)
        {
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            SharedUiBuilder.SoldierAnimatorHandle showcase = SharedUiBuilder.Diorama(catalog, 0, SharedUiBuilder.LobbyStaging);
            BakeBackdrop(scene, GameConstants.Scenes.Lobby, report);
            // Reload: the lighting bake refreshes assets and can leave earlier asset references stale.
            catalog = AssetDatabase.LoadAssetAtPath<WeaponCatalog>(WarehouseDuel.EditorTools.Weapons.WeaponAssetBuilder.CatalogPath);
            Wire(showcase.Animator, "catalog", catalog);
            ProfileCatalog profiles = AssetDatabase.LoadAssetAtPath<ProfileCatalog>(ProfileArtBuilder.CatalogPath);
            SharedUiBuilder.SessionBootstrap();
            CreateEventSystem();

            Canvas canvas = CreateCanvas("Lobby Canvas", 0, out RectTransform safe);

            // Drag anywhere in the middle to spin the character (behind all other UI).
            Image dragArea = CreateImage("Character Drag Area", safe, new Color(0f, 0f, 0f, 0f), raycast: true);
            Place(dragArea.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, 20f), new Vector2(760f, 760f), new Vector2(0.5f, 0.5f));
            var drag = dragArea.gameObject.AddComponent<DragRotate>();
            Wire(drag, "target", showcase.Turntable);

            // ---- top bar
            RectTransform top = CreateRect("Top Bar", safe);
            Place(top, new Vector2(0.5f, 1f), Vector2.zero, new Vector2(1920f, 150f), new Vector2(0.5f, 1f));
            AddTransition(top, new Vector2(0f, 140f), 0f);
            Button leave = CreateIconButton("Leave", top, UiArt.Icon("Back"), 88f, new Color(0f, 0f, 0f, 0.5f));
            Place((RectTransform)leave.transform, new Vector2(0f, 1f), new Vector2(40f, -30f), new Vector2(88f, 88f), new Vector2(0f, 1f));
            TextMeshProUGUI title = CreateText("Title", top, "LOBBY", 56f, TextPrimary, TextAlignmentOptions.Left, display: true);
            Place(title.rectTransform, new Vector2(0f, 1f), new Vector2(150f, -36f), new Vector2(400f, 76f), new Vector2(0f, 1f));

            Image codeChip = CreatePanel("Room Code", top, new Color(0.04f, 0.05f, 0.08f, 0.85f));
            Place(codeChip.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -22f), new Vector2(600f, 116f), new Vector2(0.5f, 1f));
            TextMeshProUGUI codeLabel = CreateText("Label", codeChip.transform, "ROOM CODE", 20f, TextMuted, TextAlignmentOptions.Left, display: true);
            codeLabel.characterSpacing = 8f;
            Place(codeLabel.rectTransform, new Vector2(0f, 1f), new Vector2(30f, -12f), new Vector2(300f, 30f), new Vector2(0f, 1f));
            TextMeshProUGUI code = CreateText("Code", codeChip.transform, "-----", 64f, Accent, TextAlignmentOptions.Left, display: true);
            code.characterSpacing = 12f;
            Place(code.rectTransform, new Vector2(0f, 0f), new Vector2(28f, 4f), new Vector2(440f, 80f), new Vector2(0f, 0f));
            Button copy = CreateIconButton("Copy", codeChip.transform, UiArt.Icon("Copy"), 80f, new Color(1f, 0.69f, 0.13f, 0.95f));
            Place((RectTransform)copy.transform, new Vector2(1f, 0.5f), new Vector2(-18f, 0f), new Vector2(80f, 80f), new Vector2(1f, 0.5f));
            TextMeshProUGUI copied = CreateText("Copied", top, "Copied!", 24f, Success, TextAlignmentOptions.Center, display: true);
            Place(copied.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -144f), new Vector2(300f, 36f), new Vector2(0.5f, 1f));
            copied.gameObject.SetActive(false);
            TextMeshProUGUI ping = CreateText("Ping", top, "", 24f, TextMuted, TextAlignmentOptions.Right, display: true);
            Place(ping.rectTransform, new Vector2(1f, 1f), new Vector2(-40f, -50f), new Vector2(260f, 40f), new Vector2(1f, 1f));

            // Map + player count chip next to the ping.
            TextMeshProUGUI mapLabel = CreateText("Map", top, "MAP: RANDOM", 24f, Accent, TextAlignmentOptions.Right, display: true);
            Place(mapLabel.rectTransform, new Vector2(1f, 1f), new Vector2(-40f, -92f), new Vector2(360f, 36f), new Vector2(1f, 1f));

            // ---- left: squad (up to 8 compact player rows)
            RectTransform left = CreateRect("Squad", safe);
            Place(left, new Vector2(0f, 1f), new Vector2(40f, -170f), new Vector2(520f, 760f), new Vector2(0f, 1f));
            AddTransition(left, new Vector2(-260f, 0f), 0.1f);
            TextMeshProUGUI squadHeader = CreateText("Header", left, "PLAYERS", 26f, TextMuted, TextAlignmentOptions.Left, display: true);
            squadHeader.characterSpacing = 10f;
            Place(squadHeader.rectTransform, new Vector2(0f, 1f), Vector2.zero, new Vector2(300f, 36f), new Vector2(0f, 1f));
            TextMeshProUGUI playerCount = CreateText("Count", left, "0/8", 26f, Accent, TextAlignmentOptions.Right, display: true);
            Place(playerCount.rectTransform, new Vector2(1f, 1f), Vector2.zero, new Vector2(160f, 36f), new Vector2(1f, 1f));
            var cards = new LobbyController.PlayerCard[WarehouseDuel.Multiplayer.RoomState.Capacity];
            Color[] slotColors =
            {
                new Color(0.2f, 0.55f, 1f), new Color(1f, 0.35f, 0.25f), new Color(0.3f, 0.85f, 0.4f), new Color(1f, 0.75f, 0.15f),
                new Color(0.7f, 0.4f, 1f), new Color(0.2f, 0.9f, 0.95f), new Color(1f, 0.45f, 0.75f), new Color(0.95f, 0.95f, 0.95f),
            };
            const float rowHeight = 80f, rowGap = 8f;
            for (int i = 0; i < cards.Length; i++)
            {
                Image card = CreatePanel($"Player Card {i + 1}", left, new Color(0.04f, 0.05f, 0.08f, 0.9f));
                Place(card.rectTransform, new Vector2(0f, 1f), new Vector2(0f, -46f - i * (rowHeight + rowGap)), new Vector2(520f, rowHeight), new Vector2(0f, 1f));
                card.gameObject.AddComponent<Mask>().showMaskGraphic = true;
                Image cardBanner = CreateImage("Banner", card.transform, Color.white, profiles.Banner(0));
                cardBanner.type = Image.Type.Simple;
                Stretch(cardBanner.rectTransform);
                cardBanner.enabled = false;
                Image cardShade = CreateImage("Shade", card.transform, new Color(0f, 0f, 0f, 0.58f));
                Stretch(cardShade.rectTransform);
                Image stripe = CreateImage("Team Stripe", card.transform, slotColors[i], UiArt.RoundedSmall);
                Place(stripe.rectTransform, new Vector2(0f, 0.5f), new Vector2(6f, 0f), new Vector2(8f, rowHeight - 18f), new Vector2(0f, 0.5f));
                Image avatar = CreateImage("Avatar", card.transform, slotColors[i], UiArt.Circle);
                Place(avatar.rectTransform, new Vector2(0f, 0.5f), new Vector2(22f, 0f), new Vector2(62f, 62f), new Vector2(0f, 0.5f));
                Image portrait = MenuScreensBuilder.Masked(avatar.transform, "Portrait", UiArt.Circle, profiles.Avatar(i % profiles.AvatarCount), out RectTransform portraitMask);
                Stretch(portraitMask, 4f, 4f, 4f, 4f);
                portrait.enabled = false;
                Image levelBadge = CreateImage("Level Badge", card.transform, new Color(0f, 0f, 0f, 0.75f), UiArt.RoundedSmall);
                Place(levelBadge.rectTransform, new Vector2(0f, 0f), new Vector2(26f, 2f), new Vector2(56f, 22f), new Vector2(0f, 0f));
                TextMeshProUGUI levelText = CreateText("Level", levelBadge.transform, "", 14f, Accent, TextAlignmentOptions.Center, display: true);
                Stretch(levelText.rectTransform);
                TextMeshProUGUI name = CreateText("Name", card.transform, "Waiting…", 26f, TextPrimary, TextAlignmentOptions.Left, display: true);
                Place(name.rectTransform, new Vector2(0f, 1f), new Vector2(98f, -8f), new Vector2(260f, 36f), new Vector2(0f, 1f));
                name.textWrappingMode = TextWrappingModes.NoWrap;
                name.overflowMode = TextOverflowModes.Ellipsis;
                TextMeshProUGUI state = CreateText("Status", card.transform, "", 17f, TextMuted, TextAlignmentOptions.Left);
                Place(state.rectTransform, new Vector2(0f, 0f), new Vector2(98f, 8f), new Vector2(200f, 26f), new Vector2(0f, 0f));
                state.textWrappingMode = TextWrappingModes.NoWrap;
                state.overflowMode = TextOverflowModes.Ellipsis;
                TextMeshProUGUI weapon = CreateText("Weapon", card.transform, "", 17f, Accent, TextAlignmentOptions.Right, display: true);
                Place(weapon.rectTransform, new Vector2(1f, 0f), new Vector2(-136f, 8f), new Vector2(140f, 26f), new Vector2(1f, 0f));
                Image badge = CreateImage("Ready Badge", card.transform, Color.gray, UiArt.Rounded);
                Place(badge.rectTransform, new Vector2(1f, 0.5f), new Vector2(-12f, 0f), new Vector2(114f, 40f), new Vector2(1f, 0.5f));
                TextMeshProUGUI readyText = CreateText("Text", badge.transform, "—", 17f, new Color(0.05f, 0.06f, 0.08f), TextAlignmentOptions.Center, display: true);
                Stretch(readyText.rectTransform);
                cards[i] = new LobbyController.PlayerCard { nameText = name, statusText = state, weaponText = weapon, readyBadge = badge, readyText = readyText, avatar = avatar, portrait = portrait, banner = cardBanner, levelText = levelText };
            }

            // ---- right: loadout (weapon details + weapon tiles, 4 per row)
            RectTransform right = CreateRect("Loadout", safe);
            Place(right, new Vector2(1f, 1f), new Vector2(-40f, -190f), new Vector2(540f, 720f), new Vector2(1f, 1f));
            AddTransition(right, new Vector2(260f, 0f), 0.15f);
            TextMeshProUGUI loadoutHeader = CreateText("Header", right, "LOADOUT", 26f, TextMuted, TextAlignmentOptions.Left, display: true);
            loadoutHeader.characterSpacing = 10f;
            Place(loadoutHeader.rectTransform, new Vector2(0f, 1f), Vector2.zero, new Vector2(400f, 36f), new Vector2(0f, 1f));

            Image details = CreatePanel("Weapon Details", right, new Color(0.04f, 0.05f, 0.08f, 0.82f));
            Place(details.rectTransform, new Vector2(0f, 1f), new Vector2(0f, -50f), new Vector2(540f, 300f), new Vector2(0f, 1f));
            TextMeshProUGUI weaponName = CreateText("Name", details.transform, "", 42f, Accent, TextAlignmentOptions.Left, display: true);
            Place(weaponName.rectTransform, new Vector2(0f, 1f), new Vector2(28f, -18f), new Vector2(490f, 56f), new Vector2(0f, 1f));
            TextMeshProUGUI weaponDescription = CreateText("Description", details.transform, "", 21f, TextMuted, TextAlignmentOptions.TopLeft);
            Place(weaponDescription.rectTransform, new Vector2(0f, 1f), new Vector2(28f, -76f), new Vector2(490f, 56f), new Vector2(0f, 1f));
            string[] statNames = { "DAMAGE", "FIRE RATE", "RANGE", "MAGAZINE" };
            var bars = new RectTransform[4];
            for (int i = 0; i < 4; i++)
            {
                float y = -146f - i * 36f;
                TextMeshProUGUI statLabel = CreateText(statNames[i], details.transform, statNames[i], 19f, TextMuted, TextAlignmentOptions.Left, display: true);
                Place(statLabel.rectTransform, new Vector2(0f, 1f), new Vector2(28f, y), new Vector2(160f, 28f), new Vector2(0f, 1f));
                Image track = CreateImage("Track", details.transform, new Color(1f, 1f, 1f, 0.12f), UiArt.RoundedSmall);
                Place(track.rectTransform, new Vector2(0f, 1f), new Vector2(200f, y - 6f), new Vector2(310f, 16f), new Vector2(0f, 1f));
                Image fill = CreateImage("Fill", track.transform, Accent, UiArt.RoundedSmall);
                fill.rectTransform.anchorMin = Vector2.zero;
                fill.rectTransform.anchorMax = new Vector2(0.5f, 1f);
                fill.rectTransform.offsetMin = fill.rectTransform.offsetMax = Vector2.zero;
                bars[i] = fill.rectTransform;
            }

            int weaponCount = catalog != null ? catalog.Count : 4;
            var weaponButtons = new Button[weaponCount];
            var frames = new Image[weaponCount];
            const float tileW = 128f, tileH = 150f, tileGap = 9f;
            for (int i = 0; i < weaponCount; i++)
            {
                int column = i % 4, row = i / 4;
                Button tile = CreateButton($"Weapon {i + 1}", right, "", ButtonStyle.Ghost, new Vector2(tileW, tileH), 15f);
                Place((RectTransform)tile.transform, new Vector2(0f, 1f), new Vector2(column * (tileW + tileGap), -366f - row * (tileH + tileGap)), new Vector2(tileW, tileH), new Vector2(0f, 1f));
                Image frame = CreateImage("Frame", tile.transform, new Color(1f, 1f, 1f, 0.1f), UiArt.Rounded);
                Stretch(frame.rectTransform);
                frame.transform.SetAsFirstSibling();
                Image icon = CreateImage("Icon", tile.transform, TextPrimary);
                icon.preserveAspect = true;
                Place(icon.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -18f), new Vector2(112f, 70f), new Vector2(0.5f, 1f));
                TextMeshProUGUI label = tile.GetComponentInChildren<TextMeshProUGUI>();
                Place(label.rectTransform, new Vector2(0.5f, 0f), new Vector2(0f, 10f), new Vector2(120f, 40f), new Vector2(0.5f, 0f));
                label.enableAutoSizing = true;
                label.fontSizeMin = 11f;
                label.fontSizeMax = 15f;
                weaponButtons[i] = tile;
                frames[i] = frame;
            }

            // ---- centre: name plate under the character
            Image plate = CreatePanel("Name Plate", safe, new Color(0.04f, 0.05f, 0.08f, 0.8f));
            Place(plate.rectTransform, new Vector2(0.5f, 0f), new Vector2(0f, 196f), new Vector2(460f, 74f), new Vector2(0.5f, 0f));
            AddTransition(plate.rectTransform, new Vector2(0f, -60f), 0.25f);
            TextMeshProUGUI plateName = CreateText("Name", plate.transform, "", 34f, TextPrimary, TextAlignmentOptions.Center, display: true);
            Stretch(plateName.rectTransform, 12f, 12f, 4f, 4f);

            // ---- bottom: status + actions
            RectTransform bottom = CreateRect("Actions", safe);
            Place(bottom, new Vector2(0.5f, 0f), Vector2.zero, new Vector2(1920f, 190f), new Vector2(0.5f, 0f));
            AddTransition(bottom, new Vector2(0f, -170f), 0.2f);
            TextMeshProUGUI status = CreateText("Status", bottom, "Connecting…", 26f, TextPrimary, TextAlignmentOptions.Center, display: true);
            Place(status.rectTransform, new Vector2(0.5f, 0f), new Vector2(0f, 158f), new Vector2(1100f, 36f), new Vector2(0.5f, 0f));
            Button ready = CreateButton("Ready", bottom, "READY", ButtonStyle.Primary, new Vector2(480f, 112f), out TextMeshProUGUI readyLabel, 46f);
            Place((RectTransform)ready.transform, new Vector2(0.5f, 0f), new Vector2(0f, 30f), new Vector2(480f, 112f), new Vector2(0.5f, 0f));
            SetPulse(ready);
            Button start = CreateButton("Start", bottom, "START MATCH", ButtonStyle.Danger, new Vector2(400f, 112f), out TextMeshProUGUI startLabel, 38f);
            Place((RectTransform)start.transform, new Vector2(0.5f, 0f), new Vector2(470f, 30f), new Vector2(400f, 112f), new Vector2(0.5f, 0f));
            SetPulse(start);

            var controller = canvas.gameObject.AddComponent<LobbyController>();
            Wire(controller, "catalog", catalog);
            Wire(controller, "roomCodeText", code);
            Wire(controller, "copyCodeButton", copy);
            Wire(controller, "copyFeedbackText", copied);
            Wire(controller, "roomStatusText", status);
            Wire(controller, "pingText", ping);
            Wire(controller, "mapText", mapLabel);
            Wire(controller, "playerCountText", playerCount);
            WireArray(controller, "weaponButtons", weaponButtons);
            WireArray(controller, "weaponButtonFrames", frames);
            Wire(controller, "weaponNameText", weaponName);
            Wire(controller, "weaponDescriptionText", weaponDescription);
            WireArray(controller, "statBars", bars);
            Wire(controller, "readyButton", ready);
            Wire(controller, "readyButtonText", readyLabel);
            Wire(controller, "startButton", start);
            Wire(controller, "startButtonText", startLabel);
            Wire(controller, "leaveButton", leave);
            Wire(controller, "character", showcase.Animator);
            Wire(controller, "characterName", plateName);
            WireCards(controller, cards);

            Save(scene, GameConstants.Scenes.Lobby, report);
        }

        private static void AddTransition(RectTransform rect, Vector2 fromOffset, float delay)
        {
            var transition = rect.gameObject.AddComponent<UiTransition>();
            var so = new SerializedObject(transition);
            so.FindProperty("fromOffset").vector2Value = fromOffset;
            so.FindProperty("delay").floatValue = delay;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void SetPulse(Button button)
        {
            var fx = button.GetComponent<UIButtonFX>();
            if (fx == null) return;
            var so = new SerializedObject(fx);
            so.FindProperty("pulse").boolValue = true;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        // ------------------------------------------------------------------ helpers

        private static void WireCards(LobbyController controller, LobbyController.PlayerCard[] cards)
        {
            var serialized = new SerializedObject(controller);
            SerializedProperty array = serialized.FindProperty("cards");
            array.arraySize = cards.Length;
            for (int i = 0; i < cards.Length; i++)
            {
                SerializedProperty element = array.GetArrayElementAtIndex(i);
                element.FindPropertyRelative("nameText").objectReferenceValue = cards[i].nameText;
                element.FindPropertyRelative("statusText").objectReferenceValue = cards[i].statusText;
                element.FindPropertyRelative("weaponText").objectReferenceValue = cards[i].weaponText;
                element.FindPropertyRelative("readyBadge").objectReferenceValue = cards[i].readyBadge;
                element.FindPropertyRelative("readyText").objectReferenceValue = cards[i].readyText;
                element.FindPropertyRelative("avatar").objectReferenceValue = cards[i].avatar;
                element.FindPropertyRelative("portrait").objectReferenceValue = cards[i].portrait;
                element.FindPropertyRelative("banner").objectReferenceValue = cards[i].banner;
                element.FindPropertyRelative("levelText").objectReferenceValue = cards[i].levelText;
            }
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void Save(Scene scene, string sceneName, SetupReport report)
        {
            string path = GameConstants.Scenes.PathOf(sceneName);
            if (EditorSceneManager.SaveScene(scene, path)) report.Pass($"Built scene {path}");
            else report.Error($"Could not save {path}");
        }

        /// <summary>Saves and bakes the 3D backdrop before UI is added (baking refreshes assets and can drop asset references).</summary>
        private static void BakeBackdrop(Scene scene, string sceneName, SetupReport report)
        {
            EditorSceneManager.SaveScene(scene, GameConstants.Scenes.PathOf(sceneName));
            LightingBaker.BakeEnvironment(scene, report);
        }
    }
}
