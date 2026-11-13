using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using WarehouseDuel.Core;
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
            Wire(showcase.Animator, "catalog", catalog);
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
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            SharedUiBuilder.SoldierAnimatorHandle showcase = SharedUiBuilder.Diorama(catalog, 0, SharedUiBuilder.LobbyStaging);
            BakeBackdrop(scene, GameConstants.Scenes.Lobby, report);
            // Reload: the lighting bake refreshes assets and can leave earlier asset references stale.
            catalog = AssetDatabase.LoadAssetAtPath<WeaponCatalog>(WarehouseDuel.EditorTools.Weapons.WeaponAssetBuilder.CatalogPath);
            Wire(showcase.Animator, "catalog", catalog);
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

            // ---- left: squad (both player cards)
            RectTransform left = CreateRect("Squad", safe);
            Place(left, new Vector2(0f, 1f), new Vector2(40f, -190f), new Vector2(520f, 420f), new Vector2(0f, 1f));
            AddTransition(left, new Vector2(-260f, 0f), 0.1f);
            TextMeshProUGUI squadHeader = CreateText("Header", left, "SQUAD", 26f, TextMuted, TextAlignmentOptions.Left, display: true);
            squadHeader.characterSpacing = 10f;
            Place(squadHeader.rectTransform, new Vector2(0f, 1f), Vector2.zero, new Vector2(400f, 36f), new Vector2(0f, 1f));
            var cards = new LobbyController.PlayerCard[2];
            Color[] slotColors = { new Color(0.2f, 0.55f, 1f), new Color(1f, 0.35f, 0.25f) };
            for (int i = 0; i < 2; i++)
            {
                Image card = CreatePanel($"Player Card {i + 1}", left, new Color(0.04f, 0.05f, 0.08f, 0.82f));
                Place(card.rectTransform, new Vector2(0f, 1f), new Vector2(0f, -50f - i * 180f), new Vector2(520f, 160f), new Vector2(0f, 1f));
                Image stripe = CreateImage("Team Stripe", card.transform, slotColors[i], UiArt.RoundedSmall);
                Place(stripe.rectTransform, new Vector2(0f, 0.5f), new Vector2(8f, 0f), new Vector2(10f, 132f), new Vector2(0f, 0.5f));
                Image avatar = CreateImage("Avatar", card.transform, slotColors[i], UiArt.Circle);
                Place(avatar.rectTransform, new Vector2(0f, 0.5f), new Vector2(32f, 0f), new Vector2(108f, 108f), new Vector2(0f, 0.5f));
                Image portrait = CreateImage("Portrait", avatar.transform, Color.white, UiArt.Avatar);
                Stretch(portrait.rectTransform, 6f, 6f, 6f, 6f);
                TextMeshProUGUI name = CreateText("Name", card.transform, "Waiting…", 36f, TextPrimary, TextAlignmentOptions.Left, display: true);
                Place(name.rectTransform, new Vector2(0f, 1f), new Vector2(158f, -20f), new Vector2(340f, 48f), new Vector2(0f, 1f));
                TextMeshProUGUI state = CreateText("Status", card.transform, "", 21f, TextMuted, TextAlignmentOptions.Left);
                Place(state.rectTransform, new Vector2(0f, 1f), new Vector2(158f, -70f), new Vector2(340f, 30f), new Vector2(0f, 1f));
                TextMeshProUGUI weapon = CreateText("Weapon", card.transform, "", 24f, Accent, TextAlignmentOptions.Left, display: true);
                Place(weapon.rectTransform, new Vector2(0f, 0f), new Vector2(158f, 18f), new Vector2(200f, 34f), new Vector2(0f, 0f));
                Image badge = CreateImage("Ready Badge", card.transform, Color.gray, UiArt.Rounded);
                Place(badge.rectTransform, new Vector2(1f, 0f), new Vector2(-16f, 16f), new Vector2(150f, 42f), new Vector2(1f, 0f));
                TextMeshProUGUI readyText = CreateText("Text", badge.transform, "—", 22f, new Color(0.05f, 0.06f, 0.08f), TextAlignmentOptions.Center, display: true);
                Stretch(readyText.rectTransform);
                cards[i] = new LobbyController.PlayerCard { nameText = name, statusText = state, weaponText = weapon, readyBadge = badge, readyText = readyText, avatar = avatar };
            }

            // ---- right: loadout (weapon details + 2×2 weapon tiles)
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

            var weaponButtons = new Button[4];
            var frames = new Image[4];
            for (int i = 0; i < 4; i++)
            {
                int column = i % 2, row = i / 2;
                Button tile = CreateButton($"Weapon {i + 1}", right, "", ButtonStyle.Ghost, new Vector2(262f, 160f), 22f);
                Place((RectTransform)tile.transform, new Vector2(0f, 1f), new Vector2(column * 278f, -372f - row * 176f), new Vector2(262f, 160f), new Vector2(0f, 1f));
                Image frame = CreateImage("Frame", tile.transform, new Color(1f, 1f, 1f, 0.1f), UiArt.Rounded);
                Stretch(frame.rectTransform);
                frame.transform.SetAsFirstSibling();
                Image icon = CreateImage("Icon", tile.transform, TextPrimary);
                icon.preserveAspect = true;
                Place(icon.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -16f), new Vector2(220f, 92f), new Vector2(0.5f, 1f));
                TextMeshProUGUI label = tile.GetComponentInChildren<TextMeshProUGUI>();
                Place(label.rectTransform, new Vector2(0.5f, 0f), new Vector2(0f, 10f), new Vector2(250f, 36f), new Vector2(0.5f, 0f));
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

        private static RectTransform Column(string name, RectTransform parent)
        {
            RectTransform column = CreateRect(name, parent);
            Place(column, new Vector2(0f, 1f), new Vector2(90f, -400f), new Vector2(620f, 640f), new Vector2(0f, 1f));
            UiFactory.Column(column, 18f, null, TextAnchor.UpperLeft);
            AddTransition(column, new Vector2(-160f, 0f), 0f);
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
