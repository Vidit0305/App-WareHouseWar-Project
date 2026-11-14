using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using WarehouseWar.Core;
using WarehouseWar.Profile;
using WarehouseWar.UI;
using WarehouseWar.UI.Widgets;
using WarehouseWar.Weapons;
using static WarehouseWar.EditorTools.UI.UiFactory;

namespace WarehouseWar.EditorTools.UI
{
    /// <summary>Generates the MainMenu and Lobby scenes (3D diorama background + fully wired UI).</summary>
    internal static class MenuScenesBuilder
    {
        public static void BuildMainMenu(SetupReport report, WeaponCatalog catalog)
        {
            // Reload: lighting bakes refresh assets, which can leave a previously loaded catalog object stale.
            catalog = AssetDatabase.LoadAssetAtPath<WeaponCatalog>(WarehouseWar.EditorTools.Weapons.WeaponAssetBuilder.CatalogPath);
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            SharedUiBuilder.SoldierAnimatorHandle showcase = SharedUiBuilder.Diorama(catalog, 0, SharedUiBuilder.MenuStaging);
            BakeBackdrop(scene, GameConstants.Scenes.MainMenu, report);
            catalog = AssetDatabase.LoadAssetAtPath<WeaponCatalog>(WarehouseWar.EditorTools.Weapons.WeaponAssetBuilder.CatalogPath);
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
            TextMeshProUGUI logo = CreateText("Logo", home, "WAREHOUSE <color=#FFB020>WAR</color>", 60f, TextPrimary, TextAlignmentOptions.Center, display: true);
            Place(logo.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -26f), new Vector2(760f, 78f), new Vector2(0.5f, 1f));
            TextMeshProUGUI tagline = CreateText("Tagline", home, "UP TO 8 PLAYERS · ONLINE ARENA SHOOTER", 20f, TextMuted, TextAlignmentOptions.Center, display: true);
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
            CharacterCatalog characters = AssetDatabase.LoadAssetAtPath<CharacterCatalog>(WarehouseWar.EditorTools.Gameplay.CharacterBuilder.CatalogPath);
            Sprite costumeArt = characters != null && characters.Count > 3 ? characters.Get(3).portrait : UiArt.Icon("Profile");
            Button armoryCard = SideCard(home, "Armory Card", "ARMORY", "Equip three weapons", "8 WEAPONS", profiles.Banner(5),
                catalog.Get(4) != null ? catalog.Get(4).icon : null, false, -200f, new Color(0.95f, 0.62f, 0.08f, 1f));
            Button characterCard = SideCard(home, "Wardrobe Card", "WARDROBE", "Change your costume", "COSTUMES", profiles.Banner(2),
                costumeArt, true, -416f, new Color(0.62f, 0.3f, 1f, 1f));
            Button shopCard = SideCard(home, "Shop Card", "SHOP", "Spend your stars", "NEW ITEMS", profiles.Banner(3),
                UiArt.Icon("Star"), false, -632f, new Color(0.25f, 0.85f, 0.45f, 1f));
            Button leaderboard = CreateButton("Leaderboard", home, "LEADERBOARD", ButtonStyle.Secondary, new Vector2(420f, 110f), out TextMeshProUGUI leaderboardLabel, 32f);
            RectTransform leaderboardRect = Place((RectTransform)leaderboard.transform, new Vector2(0f, 0f), new Vector2(30f, 30f), new Vector2(420f, 110f), new Vector2(0f, 0f));
            leaderboard.GetComponent<Image>().color = new Color(0.12f, 0.14f, 0.19f, 0.95f);
            Stretch(leaderboardLabel.rectTransform, 110f, 16f, 4f, 4f);
            leaderboardLabel.alignment = TextAlignmentOptions.Left;
            Image podium = CreateImage("Icon", leaderboard.transform, Accent, UiArt.Icon("Trophy"));
            Place(podium.rectTransform, new Vector2(0f, 0.5f), new Vector2(28f, 0f), new Vector2(64f, 64f), new Vector2(0f, 0.5f));
            AddTransition(leaderboardRect, new Vector2(-220f, 0f), 0.2f);

            // ---- right: game modes
            Button online = ModeCard(home, "Online Mode", "ONLINE WAR", "Create a room or join friends", "8 PLAYERS", profiles.Banner(7), catalog.Get(0) != null ? catalog.Get(0).icon : null, -200f, Accent);
            Button practice = ModeCard(home, "Practice Mode", "PRACTICE", "Fight 5 bots on any map", "VS BOTS", profiles.Banner(0), catalog.Get(3) != null ? catalog.Get(3).icon : null, -420f, Success);

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
            DialogHint(joinCard, "Enter the 6-digit room code (e.g. 482915)", -136f);
            TMP_InputField codeInput = CreateInputField("Code Input", joinCard, "ROOM CODE", new Vector2(660f, 120f), 60f);
            Place((RectTransform)codeInput.transform, new Vector2(0.5f, 1f), new Vector2(0f, -190f), new Vector2(660f, 120f), new Vector2(0.5f, 1f));
            codeInput.contentType = TMP_InputField.ContentType.IntegerNumber;
            codeInput.characterLimit = 6;
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

        /// <summary>
        /// Left-hand feature card in the same style as the game-mode cards: banner art washed with the card's colour,
        /// a coloured edge, title, subtitle, a tag chip and a large icon (weapon silhouette, costume portrait or star).
        /// </summary>
        private static Button SideCard(RectTransform parent, string name, string title, string subtitle, string tag, Sprite art, Sprite icon,
                                       bool fullColourIcon, float y, Color colour)
        {
            Button card = CreateButton(name, parent, "", ButtonStyle.Ghost, new Vector2(460f, 196f));
            RectTransform rect = Place((RectTransform)card.transform, new Vector2(0f, 1f), new Vector2(30f, y), new Vector2(460f, 196f), new Vector2(0f, 1f));
            card.GetComponent<Image>().color = new Color(0.08f, 0.09f, 0.13f, 1f);
            card.gameObject.AddComponent<Mask>().showMaskGraphic = true;
            Image artImage = CreateImage("Art", card.transform, Color.white, art);
            artImage.type = Image.Type.Simple;
            Stretch(artImage.rectTransform);
            Image wash = CreateImage("Wash", card.transform, new Color(colour.r * 0.45f, colour.g * 0.45f, colour.b * 0.45f, 0.72f));
            Stretch(wash.rectTransform);
            Image shade = CreateImage("Shade", card.transform, new Color(0f, 0f, 0f, 0.55f), UiArt.BottomFade);
            Stretch(shade.rectTransform);
            Image edge = CreateImage("Edge", card.transform, colour);
            Place(edge.rectTransform, new Vector2(0f, 0f), Vector2.zero, new Vector2(10f, 0f), new Vector2(0f, 0f));
            edge.rectTransform.anchorMax = new Vector2(0f, 1f);
            edge.rectTransform.sizeDelta = new Vector2(10f, 0f);
            Image glyph = CreateImage("Icon", card.transform, fullColourIcon ? Color.white : new Color(1f, 1f, 1f, 0.92f), icon);
            glyph.preserveAspect = true;
            if (fullColourIcon) Place(glyph.rectTransform, new Vector2(1f, 0f), new Vector2(-6f, 0f), new Vector2(190f, 190f), new Vector2(1f, 0f));
            else Place(glyph.rectTransform, new Vector2(1f, 0.5f), new Vector2(-18f, -10f), new Vector2(200f, 120f), new Vector2(1f, 0.5f));
            MenuScreensBuilder.Label(card.transform, "Title", title, 42f, TextPrimary, 30f, -22f, 400f, 56f, display: true);
            MenuScreensBuilder.Label(card.transform, "Subtitle", subtitle, 21f, new Color(1f, 1f, 1f, 0.85f), 32f, -80f, 240f, 60f);
            Image tagBack = CreateImage("Tag", card.transform, colour, UiArt.RoundedSmall);
            MenuScreensBuilder.At(tagBack.rectTransform, 30f, -146f, 150f, 34f);
            TextMeshProUGUI tagText = CreateText("Text", tagBack.transform, tag, 17f, new Color(0.06f, 0.06f, 0.06f), TextAlignmentOptions.Center, display: true);
            Stretch(tagText.rectTransform);
            AddTransition(rect, new Vector2(-240f, 0f), y < -300f ? 0.15f : 0.1f);
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
            catalog = AssetDatabase.LoadAssetAtPath<WeaponCatalog>(WarehouseWar.EditorTools.Weapons.WeaponAssetBuilder.CatalogPath);
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
            var cards = new LobbyController.PlayerCard[WarehouseWar.Multiplayer.RoomState.Capacity];
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

            // ---- right: loadout (the three weapons equipped in the Armory)
            RectTransform right = CreateRect("Loadout", safe);
            Place(right, new Vector2(1f, 1f), new Vector2(-40f, -190f), new Vector2(540f, 720f), new Vector2(1f, 1f));
            AddTransition(right, new Vector2(260f, 0f), 0.15f);
            TextMeshProUGUI loadoutHeader = CreateText("Header", right, "LOADOUT", 26f, TextMuted, TextAlignmentOptions.Left, display: true);
            loadoutHeader.characterSpacing = 10f;
            Place(loadoutHeader.rectTransform, new Vector2(0f, 1f), Vector2.zero, new Vector2(400f, 36f), new Vector2(0f, 1f));

            var loadoutCards = new LobbyController.LoadoutCard[3];
            const float cardH = 156f, cardGap = 14f;
            for (int i = 0; i < loadoutCards.Length; i++)
            {
                WeaponDefinition def = catalog != null ? catalog.Get(new[] { 0, 3, 6 }[i]) : null;
                Button card = CreateButton($"Loadout {i + 1}", right, "", ButtonStyle.Ghost, new Vector2(540f, cardH), 15f);
                Place((RectTransform)card.transform, new Vector2(0f, 1f), new Vector2(0f, -50f - i * (cardH + cardGap)), new Vector2(540f, cardH), new Vector2(0f, 1f));
                TextMeshProUGUI unusedLabel = card.GetComponentInChildren<TextMeshProUGUI>();
                if (unusedLabel != null) unusedLabel.gameObject.SetActive(false);
                Image back = CreateImage("Back", card.transform, new Color(0.04f, 0.05f, 0.08f, 0.88f), UiArt.Rounded);
                Stretch(back.rectTransform);
                back.transform.SetAsFirstSibling();
                Image frame = CreateImage("Frame", card.transform, new Color(1f, 1f, 1f, 0.12f), UiArt.Rounded);
                Stretch(frame.rectTransform);
                frame.transform.SetSiblingIndex(1);
                Image inner = CreateImage("Inner", card.transform, new Color(0.06f, 0.07f, 0.1f, 0.96f), UiArt.Rounded);
                Stretch(inner.rectTransform, 4f, 4f, 4f, 4f);
                inner.transform.SetSiblingIndex(2);
                Image badge = CreateImage("Slot Badge", card.transform, Accent, UiArt.RoundedSmall);
                Place(badge.rectTransform, new Vector2(0f, 1f), new Vector2(14f, -14f), new Vector2(40f, 40f), new Vector2(0f, 1f));
                TextMeshProUGUI badgeText = CreateText("Number", badge.transform, (i + 1).ToString(), 24f, new Color(0.08f, 0.07f, 0.04f), TextAlignmentOptions.Center, display: true);
                Stretch(badgeText.rectTransform);
                Image icon = CreateImage("Icon", card.transform, TextPrimary, def != null ? def.icon : null);
                icon.preserveAspect = true;
                Place(icon.rectTransform, new Vector2(0f, 0.5f), new Vector2(62f, 0f), new Vector2(220f, 120f), new Vector2(0f, 0.5f));
                TextMeshProUGUI name = CreateText("Name", card.transform, def != null ? def.displayName.ToUpperInvariant() : "", 34f, Accent, TextAlignmentOptions.Left, display: true);
                Place(name.rectTransform, new Vector2(0f, 1f), new Vector2(296f, -30f), new Vector2(232f, 46f), new Vector2(0f, 1f));
                name.enableAutoSizing = true;
                name.fontSizeMin = 20f;
                name.fontSizeMax = 34f;
                name.textWrappingMode = TextWrappingModes.NoWrap;
                TextMeshProUGUI info = CreateText("Info", card.transform, "", 18f, TextMuted, TextAlignmentOptions.TopLeft, display: true);
                Place(info.rectTransform, new Vector2(0f, 1f), new Vector2(296f, -82f), new Vector2(232f, 60f), new Vector2(0f, 1f));
                loadoutCards[i] = new LobbyController.LoadoutCard { button = card, frame = frame, icon = icon, nameText = name, infoText = info };
            }
            TextMeshProUGUI loadoutHint = CreateText("Hint", right, "Change your three weapons in the Armory.", 19f, TextMuted, TextAlignmentOptions.Left);
            Place(loadoutHint.rectTransform, new Vector2(0f, 1f), new Vector2(4f, -50f - 3f * (cardH + cardGap)), new Vector2(536f, 32f), new Vector2(0f, 1f));

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
            {
                var so = new SerializedObject(controller);
                SerializedProperty array = so.FindProperty("loadoutCards");
                array.arraySize = loadoutCards.Length;
                for (int i = 0; i < loadoutCards.Length; i++)
                {
                    SerializedProperty element = array.GetArrayElementAtIndex(i);
                    element.FindPropertyRelative("button").objectReferenceValue = loadoutCards[i].button;
                    element.FindPropertyRelative("frame").objectReferenceValue = loadoutCards[i].frame;
                    element.FindPropertyRelative("icon").objectReferenceValue = loadoutCards[i].icon;
                    element.FindPropertyRelative("nameText").objectReferenceValue = loadoutCards[i].nameText;
                    element.FindPropertyRelative("infoText").objectReferenceValue = loadoutCards[i].infoText;
                }
                so.ApplyModifiedPropertiesWithoutUndo();
            }
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
