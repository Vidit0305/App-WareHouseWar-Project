using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using WarehouseDuel.Core;
using WarehouseDuel.UI;
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
            SharedUiBuilder.Diorama(catalog, 0);
            BakeBackdrop(scene, GameConstants.Scenes.MainMenu, report);
            catalog = AssetDatabase.LoadAssetAtPath<WeaponCatalog>(WarehouseDuel.EditorTools.Weapons.WeaponAssetBuilder.CatalogPath);
            SharedUiBuilder.SessionBootstrap();
            CreateEventSystem();

            Canvas canvas = CreateCanvas("Menu Canvas", 0, out RectTransform safe);

            // Left-side gradient so text stays readable over the 3D scene.
            Image shade = CreateImage("Shade", canvas.transform, new Color(0.04f, 0.05f, 0.08f, 0.82f));
            shade.rectTransform.SetAsFirstSibling();
            shade.rectTransform.anchorMin = new Vector2(0f, 0f);
            shade.rectTransform.anchorMax = new Vector2(0.42f, 1f);
            shade.rectTransform.offsetMin = shade.rectTransform.offsetMax = Vector2.zero;

            TextMeshProUGUI title = CreateText("Title", safe, "WAREHOUSE\n<color=#FFB020>DUEL</color>", 110f, TextPrimary, TextAlignmentOptions.TopLeft, display: true);
            title.lineSpacing = -18f;
            Place(title.rectTransform, new Vector2(0f, 1f), new Vector2(90f, -70f), new Vector2(760f, 260f), new Vector2(0f, 1f));
            TextMeshProUGUI tagline = CreateText("Tagline", safe, "1V1 ONLINE ARENA SHOOTER", 28f, TextMuted, TextAlignmentOptions.TopLeft);
            tagline.characterSpacing = 12f;
            Place(tagline.rectTransform, new Vector2(0f, 1f), new Vector2(94f, -330f), new Vector2(760f, 40f), new Vector2(0f, 1f));

            // ---- Home panel
            RectTransform home = Column("Home", safe);
            TextMeshProUGUI nameLabel = CreateText("Name Label", home, "CALLSIGN", 24f, TextMuted, TextAlignmentOptions.Left);
            nameLabel.rectTransform.sizeDelta = new Vector2(620f, 34f);
            TMP_InputField nameInput = CreateInputField("Name Input", home, "Enter your name", new Vector2(620f, 90f));
            Spacer(home, 10f);
            Button playOnline = CreateButton("Play Online", home, "PLAY ONLINE", ButtonStyle.Primary, new Vector2(620f, 120f), 46f);
            Button practice = CreateButton("Practice", home, "PRACTICE (OFFLINE)", ButtonStyle.Secondary, new Vector2(620f, 90f));
            RectTransform smallRow = CreateRect("Row", home);
            smallRow.sizeDelta = new Vector2(620f, 90f);
            Button settings = CreateButton("Settings", smallRow, "SETTINGS", ButtonStyle.Secondary, new Vector2(300f, 90f));
            Place((RectTransform)settings.transform, new Vector2(0f, 0.5f), Vector2.zero, new Vector2(300f, 90f), new Vector2(0f, 0.5f));
            Button profile = CreateButton("Profile", smallRow, "PROFILE", ButtonStyle.Secondary, new Vector2(300f, 90f));
            Place((RectTransform)profile.transform, new Vector2(1f, 0.5f), Vector2.zero, new Vector2(300f, 90f), new Vector2(1f, 0.5f));

            // ---- Online panel
            RectTransform online = Column("Online", safe);
            Heading(online, "PLAY ONLINE");
            Button create = CreateButton("Create Room", online, "CREATE ROOM", ButtonStyle.Primary, new Vector2(620f, 120f), 44f);
            Hint(online, "Host a private match and share the room code with a friend.");
            Button join = CreateButton("Join Room", online, "JOIN ROOM", ButtonStyle.Secondary, new Vector2(620f, 120f), 44f);
            Hint(online, "Enter the code your friend shared with you.");
            Button onlineBack = CreateButton("Back", online, "BACK", ButtonStyle.Ghost, new Vector2(620f, 80f));

            // ---- Join panel
            RectTransform joinPanel = Column("Join", safe);
            Heading(joinPanel, "JOIN ROOM");
            Hint(joinPanel, "Room code (e.g. K7QPM-E)");
            TMP_InputField codeInput = CreateInputField("Code Input", joinPanel, "ROOM CODE", new Vector2(620f, 110f), 56f);
            codeInput.characterValidation = TMP_InputField.CharacterValidation.None;
            codeInput.textComponent.characterSpacing = 18f;
            codeInput.textComponent.alignment = TextAlignmentOptions.Center;
            ((TextMeshProUGUI)codeInput.placeholder).alignment = TextAlignmentOptions.Center;
            TextMeshProUGUI joinError = CreateText("Error", joinPanel, "", 26f, Danger, TextAlignmentOptions.Left);
            joinError.rectTransform.sizeDelta = new Vector2(620f, 70f);
            Button joinConfirm = CreateButton("Join", joinPanel, "JOIN", ButtonStyle.Primary, new Vector2(620f, 110f), 44f);
            Button joinBack = CreateButton("Back", joinPanel, "BACK", ButtonStyle.Ghost, new Vector2(620f, 80f));

            // ---- Busy panel
            RectTransform busy = Column("Busy", safe);
            Spacer(busy, 60f);
            Image spinner = CreateImage("Spinner", busy, Accent, UiArt.Ring);
            spinner.rectTransform.sizeDelta = new Vector2(110f, 110f);
            Image spinnerGap = CreateImage("Gap", spinner.transform, new Color(0.06f, 0.07f, 0.1f, 1f));
            Place(spinnerGap.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, 0f), new Vector2(30f, 40f), new Vector2(0.5f, 1f));
            TextMeshProUGUI busyText = CreateText("Text", busy, "Connecting…", 34f, TextPrimary, TextAlignmentOptions.Center);
            busyText.rectTransform.sizeDelta = new Vector2(620f, 120f);

            // ---- Modals
            GameObject settingsPanel = SharedUiBuilder.Settings(safe, out Button settingsClose);
            RectTransform profileCard = SharedUiBuilder.Modal("Profile Panel", safe, new Vector2(900f, 640f), out GameObject profilePanel);
            TextMeshProUGUI profileTitle = CreateText("Title", profileCard, "PROFILE", 56f, Accent, TextAlignmentOptions.Center, display: true);
            Place(profileTitle.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -30f), new Vector2(700f, 80f), new Vector2(0.5f, 1f));
            TextMeshProUGUI profileName = CreateText("Name", profileCard, "Player", 48f, TextPrimary, TextAlignmentOptions.Center, display: true);
            Place(profileName.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -120f), new Vector2(760f, 70f), new Vector2(0.5f, 1f));
            TextMeshProUGUI profileStats = CreateText("Stats", profileCard, "", 34f, TextMuted, TextAlignmentOptions.Center);
            profileStats.lineSpacing = 20f;
            Place(profileStats.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -210f), new Vector2(760f, 280f), new Vector2(0.5f, 1f));
            Button profileClose = CreateButton("Close", profileCard, "DONE", ButtonStyle.Primary, new Vector2(320f, 90f));
            Place((RectTransform)profileClose.transform, new Vector2(0.5f, 0f), new Vector2(0f, 30f), new Vector2(320f, 90f), new Vector2(0.5f, 0f));
            ProfilePanel profileComponent = profilePanel.AddComponent<ProfilePanel>();
            Wire(profileComponent, "nameText", profileName);
            Wire(profileComponent, "statsText", profileStats);

            RectTransform messageCard = SharedUiBuilder.Modal("Message Panel", safe, new Vector2(900f, 420f), out GameObject messagePanel);
            TextMeshProUGUI messageText = CreateText("Text", messageCard, "", 34f, TextPrimary, TextAlignmentOptions.Center);
            Stretch(messageText.rectTransform, 50f, 50f, 50f, 150f);
            Button messageOk = CreateButton("OK", messageCard, "OK", ButtonStyle.Primary, new Vector2(300f, 90f));
            Place((RectTransform)messageOk.transform, new Vector2(0.5f, 0f), new Vector2(0f, 30f), new Vector2(300f, 90f), new Vector2(0.5f, 0f));

            TextMeshProUGUI version = CreateText("Version", safe, $"v{Application.version}", 22f, TextMuted, TextAlignmentOptions.BottomRight);
            Place(version.rectTransform, new Vector2(1f, 0f), new Vector2(-30f, 20f), new Vector2(300f, 40f), new Vector2(1f, 0f));

            var controller = canvas.gameObject.AddComponent<MainMenuController>();
            Wire(controller, "homePanel", home.gameObject);
            Wire(controller, "onlinePanel", online.gameObject);
            Wire(controller, "joinPanel", joinPanel.gameObject);
            Wire(controller, "busyPanel", busy.gameObject);
            Wire(controller, "settingsPanel", settingsPanel);
            Wire(controller, "profilePanel", profilePanel);
            Wire(controller, "messagePanel", messagePanel);
            Wire(controller, "nameInput", nameInput);
            Wire(controller, "playOnlineButton", playOnline);
            Wire(controller, "practiceButton", practice);
            Wire(controller, "settingsButton", settings);
            Wire(controller, "profileButton", profile);
            Wire(controller, "createRoomButton", create);
            Wire(controller, "joinRoomButton", join);
            Wire(controller, "onlineBackButton", onlineBack);
            Wire(controller, "codeInput", codeInput);
            Wire(controller, "joinConfirmButton", joinConfirm);
            Wire(controller, "joinBackButton", joinBack);
            Wire(controller, "joinErrorText", joinError);
            Wire(controller, "busyText", busyText);
            Wire(controller, "busySpinner", spinner.rectTransform);
            Wire(controller, "messageText", messageText);
            Wire(controller, "messageOkButton", messageOk);
            Wire(controller, "settingsCloseButton", settingsClose);
            Wire(controller, "profileCloseButton", profileClose);

            Save(scene, GameConstants.Scenes.MainMenu, report);
        }

        public static void BuildLobby(SetupReport report, WeaponCatalog catalog)
        {
            // Reload: lighting bakes refresh assets, which can leave a previously loaded catalog object stale.
            catalog = AssetDatabase.LoadAssetAtPath<WeaponCatalog>(WarehouseDuel.EditorTools.Weapons.WeaponAssetBuilder.CatalogPath);
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            SharedUiBuilder.Diorama(catalog, 2);
            BakeBackdrop(scene, GameConstants.Scenes.Lobby, report);
            catalog = AssetDatabase.LoadAssetAtPath<WeaponCatalog>(WarehouseDuel.EditorTools.Weapons.WeaponAssetBuilder.CatalogPath);
            SharedUiBuilder.SessionBootstrap();
            CreateEventSystem();

            Canvas canvas = CreateCanvas("Lobby Canvas", 0, out RectTransform safe);
            Image shade = CreateImage("Shade", canvas.transform, new Color(0.04f, 0.05f, 0.08f, 0.78f));
            shade.rectTransform.SetAsFirstSibling();
            Stretch(shade.rectTransform);

            // ---- top bar
            Button leave = CreateButton("Leave", safe, "LEAVE", ButtonStyle.Ghost, new Vector2(220f, 80f));
            Place((RectTransform)leave.transform, new Vector2(0f, 1f), new Vector2(40f, -40f), new Vector2(220f, 80f), new Vector2(0f, 1f));
            TextMeshProUGUI ping = CreateText("Ping", safe, "", 26f, TextMuted, TextAlignmentOptions.Right);
            Place(ping.rectTransform, new Vector2(1f, 1f), new Vector2(-40f, -50f), new Vector2(240f, 50f), new Vector2(1f, 1f));

            TextMeshProUGUI codeLabel = CreateText("Code Label", safe, "ROOM CODE", 24f, TextMuted, TextAlignmentOptions.Center);
            codeLabel.characterSpacing = 10f;
            Place(codeLabel.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -30f), new Vector2(500f, 40f), new Vector2(0.5f, 1f));
            TextMeshProUGUI code = CreateText("Code", safe, "-----", 84f, Accent, TextAlignmentOptions.Center, display: true);
            code.characterSpacing = 14f;
            Place(code.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -70f), new Vector2(620f, 100f), new Vector2(0.5f, 1f));
            Button copy = CreateIconButton("Copy", safe, UiArt.Icon("Copy"), 84f, new Color(1f, 1f, 1f, 0.14f));
            Place((RectTransform)copy.transform, new Vector2(0.5f, 1f), new Vector2(370f, -78f), new Vector2(84f, 84f), new Vector2(0.5f, 1f));
            TextMeshProUGUI copied = CreateText("Copied", safe, "Copied!", 24f, Success, TextAlignmentOptions.Left);
            Place(copied.rectTransform, new Vector2(0.5f, 1f), new Vector2(430f, -100f), new Vector2(200f, 40f), new Vector2(0f, 1f));
            copied.gameObject.SetActive(false);
            TextMeshProUGUI status = CreateText("Status", safe, "Connecting…", 30f, TextPrimary, TextAlignmentOptions.Center);
            Place(status.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -180f), new Vector2(1500f, 50f), new Vector2(0.5f, 1f));

            // ---- player cards
            var cards = new LobbyController.PlayerCard[2];
            for (int i = 0; i < 2; i++)
            {
                Image card = CreatePanel($"Player Card {i + 1}", safe);
                Place(card.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(i == 0 ? -330f : 330f, 150f), new Vector2(580f, 250f), new Vector2(0.5f, 0.5f));
                Image avatar = CreateImage("Avatar", card.transform, Color.white, UiArt.Circle);
                Place(avatar.rectTransform, new Vector2(0f, 0.5f), new Vector2(40f, 20f), new Vector2(120f, 120f), new Vector2(0f, 0.5f));
                Image avatarIcon = CreateImage("Icon", avatar.transform, new Color(0f, 0f, 0f, 0.55f), UiArt.Icon("Profile"));
                Stretch(avatarIcon.rectTransform, 22f, 22f, 22f, 22f);
                TextMeshProUGUI name = CreateText("Name", card.transform, "Waiting…", 40f, TextPrimary, TextAlignmentOptions.Left, display: true);
                Place(name.rectTransform, new Vector2(0f, 1f), new Vector2(190f, -36f), new Vector2(370f, 56f), new Vector2(0f, 1f));
                TextMeshProUGUI state = CreateText("Status", card.transform, "", 24f, TextMuted, TextAlignmentOptions.Left);
                Place(state.rectTransform, new Vector2(0f, 1f), new Vector2(190f, -96f), new Vector2(370f, 36f), new Vector2(0f, 1f));
                TextMeshProUGUI weapon = CreateText("Weapon", card.transform, "", 28f, Accent, TextAlignmentOptions.Left);
                Place(weapon.rectTransform, new Vector2(0f, 1f), new Vector2(190f, -136f), new Vector2(370f, 40f), new Vector2(0f, 1f));
                Image badge = CreateImage("Ready Badge", card.transform, Color.gray, UiArt.Rounded);
                Place(badge.rectTransform, new Vector2(0.5f, 0f), new Vector2(0f, 22f), new Vector2(520f, 50f), new Vector2(0.5f, 0f));
                TextMeshProUGUI readyText = CreateText("Text", badge.transform, "—", 26f, new Color(0.05f, 0.06f, 0.08f), TextAlignmentOptions.Center, display: true);
                Stretch(readyText.rectTransform);
                cards[i] = new LobbyController.PlayerCard { nameText = name, statusText = state, weaponText = weapon, readyBadge = badge, readyText = readyText, avatar = avatar };
            }
            TextMeshProUGUI vs = CreateText("VS", safe, "VS", 64f, Accent, TextAlignmentOptions.Center, display: true);
            Place(vs.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, 150f), new Vector2(120f, 90f), new Vector2(0.5f, 0.5f));

            // ---- weapon selection
            TextMeshProUGUI chooseLabel = CreateText("Choose Label", safe, "CHOOSE YOUR WEAPON", 26f, TextMuted, TextAlignmentOptions.Left);
            chooseLabel.characterSpacing = 8f;
            Place(chooseLabel.rectTransform, new Vector2(0.5f, 0f), new Vector2(-750f, 430f), new Vector2(600f, 40f), new Vector2(0f, 0f));
            var weaponButtons = new Button[4];
            var frames = new Image[4];
            for (int i = 0; i < 4; i++)
            {
                Button button = CreateButton($"Weapon {i + 1}", safe, "", ButtonStyle.Ghost, new Vector2(240f, 170f), 24f);
                Place((RectTransform)button.transform, new Vector2(0.5f, 0f), new Vector2(-620f + i * 260f, 330f), new Vector2(240f, 170f), new Vector2(0.5f, 0.5f));
                TextMeshProUGUI label = button.GetComponentInChildren<TextMeshProUGUI>();
                Place(label.rectTransform, new Vector2(0.5f, 0f), new Vector2(0f, 14f), new Vector2(230f, 40f), new Vector2(0.5f, 0f));
                Image icon = CreateImage("Icon", button.transform, TextPrimary);
                icon.preserveAspect = true;
                Place(icon.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -18f), new Vector2(200f, 100f), new Vector2(0.5f, 1f));
                Image frame = CreateImage("Frame", button.transform, new Color(1f, 1f, 1f, 0.12f), UiArt.RoundedSmall);
                Place(frame.rectTransform, new Vector2(0.5f, 0f), new Vector2(0f, 4f), new Vector2(200f, 6f), new Vector2(0.5f, 0f));
                weaponButtons[i] = button;
                frames[i] = frame;
            }

            Image details = CreatePanel("Weapon Details", safe);
            Place(details.rectTransform, new Vector2(1f, 0f), new Vector2(-40f, 200f), new Vector2(560f, 320f), new Vector2(1f, 0f));
            TextMeshProUGUI weaponName = CreateText("Name", details.transform, "", 40f, Accent, TextAlignmentOptions.Left, display: true);
            Place(weaponName.rectTransform, new Vector2(0f, 1f), new Vector2(30f, -22f), new Vector2(500f, 56f), new Vector2(0f, 1f));
            TextMeshProUGUI weaponDescription = CreateText("Description", details.transform, "", 22f, TextMuted, TextAlignmentOptions.TopLeft);
            Place(weaponDescription.rectTransform, new Vector2(0f, 1f), new Vector2(30f, -80f), new Vector2(500f, 60f), new Vector2(0f, 1f));
            string[] statNames = { "DAMAGE", "FIRE RATE", "RANGE", "MAGAZINE" };
            var bars = new RectTransform[4];
            for (int i = 0; i < 4; i++)
            {
                float y = -150f - i * 40f;
                TextMeshProUGUI statLabel = CreateText(statNames[i], details.transform, statNames[i], 20f, TextMuted, TextAlignmentOptions.Left);
                Place(statLabel.rectTransform, new Vector2(0f, 1f), new Vector2(30f, y), new Vector2(160f, 30f), new Vector2(0f, 1f));
                Image track = CreateImage("Track", details.transform, new Color(1f, 1f, 1f, 0.12f), UiArt.RoundedSmall);
                Place(track.rectTransform, new Vector2(0f, 1f), new Vector2(200f, y - 6f), new Vector2(320f, 16f), new Vector2(0f, 1f));
                Image fill = CreateImage("Fill", track.transform, Accent, UiArt.RoundedSmall);
                fill.rectTransform.anchorMin = Vector2.zero;
                fill.rectTransform.anchorMax = new Vector2(0.5f, 1f);
                fill.rectTransform.offsetMin = fill.rectTransform.offsetMax = Vector2.zero;
                bars[i] = fill.rectTransform;
            }

            // ---- actions
            Button ready = CreateButton("Ready", safe, "READY", ButtonStyle.Secondary, new Vector2(520f, 110f), out TextMeshProUGUI readyLabel, 42f);
            Place((RectTransform)ready.transform, new Vector2(0.5f, 0f), new Vector2(-290f, 40f), new Vector2(520f, 110f), new Vector2(0.5f, 0f));
            Button start = CreateButton("Start", safe, "START MATCH", ButtonStyle.Primary, new Vector2(520f, 110f), out TextMeshProUGUI startLabel, 42f);
            Place((RectTransform)start.transform, new Vector2(0.5f, 0f), new Vector2(290f, 40f), new Vector2(520f, 110f), new Vector2(0.5f, 0f));

            var controller = canvas.gameObject.AddComponent<LobbyController>();
            Wire(controller, "catalog", catalog);
            Wire(controller, "roomCodeText", code);
            Wire(controller, "copyCodeButton", copy);
            Wire(controller, "copyFeedbackText", copied);
            Wire(controller, "roomStatusText", status);
            Wire(controller, "pingText", ping);
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
            WireCards(controller, cards);

            Save(scene, GameConstants.Scenes.Lobby, report);
        }

        // ------------------------------------------------------------------ helpers

        private static RectTransform Column(string name, RectTransform parent)
        {
            RectTransform column = CreateRect(name, parent);
            Place(column, new Vector2(0f, 1f), new Vector2(90f, -400f), new Vector2(620f, 640f), new Vector2(0f, 1f));
            UiFactory.Column(column, 18f, null, TextAnchor.UpperLeft);
            return column;
        }

        private static void Heading(RectTransform parent, string text)
        {
            TextMeshProUGUI heading = CreateText("Heading", parent, text, 44f, Accent, TextAlignmentOptions.Left, display: true);
            heading.rectTransform.sizeDelta = new Vector2(620f, 64f);
        }

        private static void Hint(RectTransform parent, string text)
        {
            TextMeshProUGUI hint = CreateText("Hint", parent, text, 24f, TextMuted, TextAlignmentOptions.Left);
            hint.rectTransform.sizeDelta = new Vector2(620f, 40f);
        }

        private static void Spacer(RectTransform parent, float height) => CreateRect("Spacer", parent).sizeDelta = new Vector2(10f, height);

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
