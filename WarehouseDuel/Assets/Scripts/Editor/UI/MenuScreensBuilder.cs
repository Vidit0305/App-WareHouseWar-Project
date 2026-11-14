using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using WarehouseWar.Player;
using WarehouseWar.Profile;
using WarehouseWar.UI;
using WarehouseWar.UI.Widgets;
using WarehouseWar.Weapons;
using static WarehouseWar.EditorTools.UI.UiFactory;

namespace WarehouseWar.EditorTools.UI
{
    /// <summary>
    /// Builds the main-menu screens: Profile (avatar / banner / title editor + career stats), Leaderboard
    /// (standings + records + recent matches) and Armory (weapon browser), plus shared building blocks.
    /// Layout is in 1920×1080 reference units; content is anchored top-left inside each screen.
    /// </summary>
    internal static class MenuScreensBuilder
    {
        public static readonly Color CardDark = new Color(0.04f, 0.05f, 0.08f, 0.82f);
        public static readonly Color Tile = new Color(1f, 1f, 1f, 0.07f);

        // ------------------------------------------------------------------ building blocks

        /// <summary>Top-left anchored placement with y measured downwards (negative).</summary>
        public static RectTransform At(RectTransform rect, float x, float y, float w, float h)
        {
            Place(rect, new Vector2(0f, 1f), new Vector2(x, y), new Vector2(w, h), new Vector2(0f, 1f));
            return rect;
        }

        public static TextMeshProUGUI Label(Transform parent, string name, string text, float size, Color color,
            float x, float y, float w, float h, TextAlignmentOptions align = TextAlignmentOptions.Left, bool display = false)
        {
            TextMeshProUGUI label = CreateText(name, parent, text, size, color, align, display);
            At(label.rectTransform, x, y, w, h);
            label.textWrappingMode = TextWrappingModes.Normal;
            label.overflowMode = TextOverflowModes.Ellipsis;
            return label;
        }

        /// <summary>An image clipped to <paramref name="maskSprite"/> (rounded square or circle).</summary>
        public static Image Masked(Transform parent, string name, Sprite maskSprite, Sprite content, out RectTransform container)
        {
            Image mask = CreateImage(name, parent, Color.white, maskSprite);
            mask.gameObject.AddComponent<Mask>().showMaskGraphic = false;
            container = mask.rectTransform;
            Image inner = CreateImage("Image", mask.transform, Color.white, content);
            inner.type = Image.Type.Simple;
            Stretch(inner.rectTransform);
            return inner;
        }

        /// <summary>
        /// Full-screen page: background (opaque, or transparent in the middle to show the 3D scene), a top bar with
        /// a back button and the title. Returns the content area below the bar.
        /// </summary>
        public static RectTransform Frame(string name, RectTransform safe, string title, bool showScene, out GameObject root, out Button back)
        {
            Image background = CreateImage(name, safe, showScene ? new Color(0f, 0f, 0f, 0f) : new Color(0.05f, 0.06f, 0.09f, 0.96f), null, raycast: !showScene);
            Stretch(background.rectTransform, -200f, -200f, -200f, -200f);
            root = background.gameObject;
            RectTransform page = CreateRect("Page", background.transform);
            Stretch(page, 200f, 200f, 200f, 200f);

            Image bar = CreateImage("Top Bar", background.transform, new Color(0f, 0f, 0f, showScene ? 0.55f : 0.35f));
            bar.rectTransform.anchorMin = new Vector2(0f, 1f);
            bar.rectTransform.anchorMax = new Vector2(1f, 1f);
            bar.rectTransform.pivot = new Vector2(0.5f, 1f);
            bar.rectTransform.sizeDelta = new Vector2(0f, 330f);
            bar.rectTransform.anchoredPosition = Vector2.zero;
            Image accentLine = CreateImage("Accent", bar.transform, Accent);
            accentLine.rectTransform.anchorMin = Vector2.zero;
            accentLine.rectTransform.anchorMax = new Vector2(1f, 0f);
            accentLine.rectTransform.sizeDelta = new Vector2(0f, 4f);
            accentLine.rectTransform.pivot = new Vector2(0.5f, 0f);

            back = CreateIconButton("Back", page, UiArt.Icon("Back"), 84f, new Color(1f, 1f, 1f, 0.14f));
            At((RectTransform)back.transform, 36f, -20f, 84f, 84f);
            Label(page, "Title", title, 54f, TextPrimary, 146f, -22f, 900f, 80f, TextAlignmentOptions.Left, display: true);

            RectTransform content = CreateRect("Content", page);
            Stretch(content, 40f, 40f, 150f, 26f);

            // Fade + slide in when opened, fade out when closed (PopupFx.Hide).
            root.AddComponent<CanvasGroup>();
            PopupFx fx = root.AddComponent<PopupFx>();
            Wire(fx, "content", content);
            SetEnum(fx, "style", (int)PopupFx.Style.Slide);
            return content;
        }

        /// <summary>A row of tab buttons; the screen script colours the selected one.</summary>
        public static void Tabs(RectTransform parent, string[] labels, float x, float y, float w, float h, float spacing, bool vertical,
            out Button[] buttons, out Image[] backgrounds, out TMP_Text[] texts)
        {
            buttons = new Button[labels.Length];
            backgrounds = new Image[labels.Length];
            texts = new TMP_Text[labels.Length];
            for (int i = 0; i < labels.Length; i++)
            {
                Button tab = CreateButton($"Tab {labels[i]}", parent, labels[i], ButtonStyle.Ghost, new Vector2(w, h), out TextMeshProUGUI text, 30f);
                float px = vertical ? x : x + i * (w + spacing);
                float py = vertical ? y - i * (h + spacing) : y;
                At((RectTransform)tab.transform, px, py, w, h);
                buttons[i] = tab;
                backgrounds[i] = tab.GetComponent<Image>();
                texts[i] = text;
            }
        }

        /// <summary>Dark rounded panel at a top-left position.</summary>
        public static Image Panel(Transform parent, string name, float x, float y, float w, float h, Color? color = null)
        {
            Image panel = CreateImage(name, parent, color ?? CardDark, UiArt.Rounded);
            At(panel.rectTransform, x, y, w, h);
            return panel;
        }

        /// <summary>Scrollable vertical list (mask + content with layout). Returns the content to clone rows into.</summary>
        public static RectTransform ScrollList(Transform parent, string name, float x, float y, float w, float h, float spacing)
        {
            RectTransform viewport = CreateRect(name, parent);
            At(viewport, x, y, w, h);
            viewport.gameObject.AddComponent<RectMask2D>();
            Image hit = viewport.gameObject.AddComponent<Image>();
            hit.color = new Color(0f, 0f, 0f, 0f);
            ScrollRect scroll = viewport.gameObject.AddComponent<ScrollRect>();

            RectTransform content = CreateRect("Content", viewport);
            content.anchorMin = new Vector2(0f, 1f);
            content.anchorMax = new Vector2(1f, 1f);
            content.pivot = new Vector2(0.5f, 1f);
            content.sizeDelta = new Vector2(0f, 0f);
            content.anchoredPosition = Vector2.zero;
            VerticalLayoutGroup layout = Column(content, spacing, null, TextAnchor.UpperLeft);
            layout.childControlWidth = true;
            layout.childForceExpandWidth = true;
            ContentSizeFitter fitter = content.gameObject.AddComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            scroll.content = content;
            scroll.viewport = viewport;
            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 30f;
            return content;
        }

        /// <summary>Scrolling grid filling <paramref name="parent"/> (fixed column count). Returns the content to add cells to.</summary>
        public static RectTransform ScrollGrid(RectTransform parent, string name, Vector2 cell, Vector2 spacing, int columns)
        {
            RectTransform viewport = CreateRect(name, parent);
            Stretch(viewport);
            viewport.gameObject.AddComponent<RectMask2D>().padding = new Vector4(-8f, -8f, -8f, -8f);
            Image hit = viewport.gameObject.AddComponent<Image>();
            hit.color = new Color(0f, 0f, 0f, 0f);
            ScrollRect scroll = viewport.gameObject.AddComponent<ScrollRect>();

            RectTransform content = CreateRect("Content", viewport);
            content.anchorMin = new Vector2(0f, 1f);
            content.anchorMax = new Vector2(1f, 1f);
            content.pivot = new Vector2(0.5f, 1f);
            content.sizeDelta = Vector2.zero;
            content.anchoredPosition = Vector2.zero;
            GridLayoutGroup grid = content.gameObject.AddComponent<GridLayoutGroup>();
            grid.cellSize = cell;
            grid.spacing = spacing;
            grid.padding = new RectOffset(8, 8, 8, 8);
            grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            grid.constraintCount = columns;
            content.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            scroll.content = content;
            scroll.viewport = viewport;
            scroll.horizontal = false;
            scroll.movementType = ScrollRect.MovementType.Elastic;
            scroll.scrollSensitivity = 30f;
            return content;
        }

        // ------------------------------------------------------------------ profile

        public static ProfileScreen Profile(RectTransform safe, ProfileCatalog catalog)
        {
            RectTransform content = Frame("Profile Screen", safe, "PROFILE", false, out GameObject root, out Button back);

            // ---- left: preview card
            Image preview = Panel(content, "Preview", 0f, 0f, 640f, 230f, new Color(0.1f, 0.12f, 0.16f, 1f));
            preview.gameObject.AddComponent<Mask>().showMaskGraphic = true;
            Image banner = CreateImage("Banner", preview.transform, Color.white, catalog.Banner(0));
            banner.type = Image.Type.Simple;
            Stretch(banner.rectTransform);
            Image shade = CreateImage("Shade", preview.transform, new Color(0f, 0f, 0f, 0.42f));
            Stretch(shade.rectTransform);
            Image avatarFrame = CreateImage("Avatar Frame", preview.transform, Color.white, UiArt.Circle);
            At(avatarFrame.rectTransform, 28f, -30f, 170f, 170f);
            Image avatar = Masked(avatarFrame.transform, "Avatar Mask", UiArt.Circle, catalog.Avatar(0), out RectTransform avatarMask);
            Stretch(avatarMask, 6f, 6f, 6f, 6f);
            Image levelBadge = CreateImage("Level Badge", preview.transform, Accent, UiArt.Circle);
            At(levelBadge.rectTransform, 150f, -150f, 58f, 58f);
            TextMeshProUGUI level = CreateText("Level", levelBadge.transform, "1", 28f, AccentDark, TextAlignmentOptions.Center, display: true);
            Stretch(level.rectTransform);
            TextMeshProUGUI name = Label(preview.transform, "Name", "Player", 46f, TextPrimary, 222f, -36f, 400f, 60f, display: true);
            TextMeshProUGUI title = Label(preview.transform, "Title", "ROOKIE", 24f, Accent, 224f, -98f, 400f, 34f, display: true);
            title.characterSpacing = 6f;
            Image xpTrack = CreateImage("XP Track", preview.transform, new Color(0f, 0f, 0f, 0.5f), UiArt.RoundedSmall);
            At(xpTrack.rectTransform, 224f, -146f, 380f, 18f);
            Image xpFill = CreateImage("XP Fill", xpTrack.transform, Accent, UiArt.RoundedSmall);
            xpFill.rectTransform.anchorMin = Vector2.zero;
            xpFill.rectTransform.anchorMax = new Vector2(0.3f, 1f);
            xpFill.rectTransform.offsetMin = xpFill.rectTransform.offsetMax = Vector2.zero;
            TextMeshProUGUI xp = Label(preview.transform, "XP", "0 / 300 XP", 20f, new Color(1f, 1f, 1f, 0.75f), 224f, -170f, 380f, 30f);
            ProfileCard card = preview.gameObject.AddComponent<ProfileCard>();
            Wire(card, "avatar", avatar);
            Wire(card, "banner", banner);
            Wire(card, "nameText", name);
            Wire(card, "titleText", title);
            Wire(card, "levelText", level);
            Wire(card, "xpText", xp);
            Wire(card, "xpFill", xpFill.rectTransform);

            // ---- left: callsign
            Label(content, "Callsign Label", "CALLSIGN", 22f, TextMuted, 0f, -258f, 400f, 32f, display: true).characterSpacing = 8f;
            TMP_InputField nameInput = CreateInputField("Name Input", content, "Enter your name", new Vector2(640f, 90f));
            At((RectTransform)nameInput.transform, 0f, -294f, 640f, 90f);

            // ---- left: career stats
            Label(content, "Career Label", "CAREER", 22f, TextMuted, 0f, -408f, 400f, 32f, display: true).characterSpacing = 8f;
            string[] captions = { "MATCHES", "WINS", "WIN RATE", "ELIMINATIONS", "DEATHS", "K/D RATIO" };
            var statValues = new TMP_Text[captions.Length];
            for (int i = 0; i < captions.Length; i++)
            {
                float x = (i % 3) * 220f, y = -446f - (i / 3) * 140f;
                Image tile = Panel(content, $"Stat {captions[i]}", x, y, 200f, 124f, Tile);
                statValues[i] = Label(tile.transform, "Value", "0", 46f, i == 1 ? Accent : TextPrimary, 0f, -14f, 200f, 60f, TextAlignmentOptions.Center, display: true);
                Label(tile.transform, "Caption", captions[i], 18f, TextMuted, 0f, -78f, 200f, 30f, TextAlignmentOptions.Center, display: true);
            }

            // ---- right: tabs
            Tabs(content, new[] { "AVATAR", "BANNER", "TITLE" }, 700f, 0f, 250f, 80f, 16f, false,
                out Button[] tabButtons, out Image[] tabBackgrounds, out TMP_Text[] tabLabels);

            var pages = new GameObject[3];

            // AVATAR page: upload tile, photo tile, presets.
            RectTransform avatarPage = At(CreateRect("Avatar Page", content), 700f, -112f, 1140f, 780f);
            pages[0] = avatarPage.gameObject;
            const float tileSize = 160f, gap = 36f;
            // Grid layout so the photo tile (hidden until a photo exists) never leaves a hole.
            RectTransform grid = At(CreateRect("Avatar Grid", avatarPage), 0f, 0f, 1140f, 600f);
            GridLayoutGroup gridLayout = grid.gameObject.AddComponent<GridLayoutGroup>();
            gridLayout.cellSize = new Vector2(tileSize, tileSize);
            gridLayout.spacing = new Vector2(gap, gap);
            gridLayout.padding = new RectOffset(6, 0, 6, 0);
            gridLayout.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            gridLayout.constraintCount = 6;

            Image RingChild(Button tile)
            {
                Image ring = CreateImage("Selected Ring", tile.transform, new Color(1f, 0.69f, 0.13f, 0f), UiArt.Rounded);
                Stretch(ring.rectTransform, -7f, -7f, -7f, -7f);
                ring.transform.SetAsFirstSibling();
                return ring;
            }

            Button upload = CreateButton("Upload Photo", grid, "", ButtonStyle.Secondary, new Vector2(tileSize, tileSize));
            Image uploadIcon = CreateImage("Icon", upload.transform, Accent, UiArt.Icon("Upload"));
            At(uploadIcon.rectTransform, 45f, -26f, 70f, 70f);
            Label(upload.transform, "Text", "UPLOAD\nPHOTO", 20f, TextPrimary, 0f, -96f, tileSize, 56f, TextAlignmentOptions.Center, display: true);

            Button customTile = CreateButton("Photo", grid, "", ButtonStyle.Ghost, new Vector2(tileSize, tileSize));
            Image customRing = RingChild(customTile);
            Image customImage = Masked(customTile.transform, "Mask", UiArt.Rounded, null, out RectTransform customMask);
            Stretch(customMask);
            Image photoTag = CreateImage("Tag", customTile.transform, new Color(0f, 0f, 0f, 0.6f), UiArt.RoundedSmall);
            At(photoTag.rectTransform, 10f, -122f, 90f, 30f);
            TextMeshProUGUI photoText = CreateText("Text", photoTag.transform, "PHOTO", 16f, TextPrimary, TextAlignmentOptions.Center, display: true);
            Stretch(photoText.rectTransform);
            customTile.gameObject.SetActive(false); // shown at runtime once a photo exists

            int presetCount = catalog.AvatarCount;
            var avatarTiles = new Button[presetCount];
            var avatarImages = new Image[presetCount];
            var avatarRings = new Image[presetCount];
            var avatarLocks = new GameObject[presetCount];
            for (int i = 0; i < presetCount; i++)
            {
                Button tile = CreateButton($"Avatar {i + 1}", grid, "", ButtonStyle.Ghost, new Vector2(tileSize, tileSize));
                avatarRings[i] = RingChild(tile);
                avatarImages[i] = Masked(tile.transform, "Mask", UiArt.Rounded, catalog.Avatar(i), out RectTransform mask);
                Stretch(mask);
                avatarLocks[i] = LockOverlay(tile.transform, out _, withPrice: false);
                avatarTiles[i] = tile;
            }
            avatarRings[0].color = Accent;
            TextMeshProUGUI avatarStatus = Label(avatarPage, "Status", "", 22f, TextMuted, 6f, -612f, 1130f, 64f);

            // BANNER page: two columns in a scrolling grid.
            RectTransform bannerPage = At(CreateRect("Banner Page", content), 700f, -112f, 1140f, 780f);
            pages[1] = bannerPage.gameObject;
            RectTransform bannerGrid = ScrollGrid(bannerPage, "Banner List", new Vector2(556f, 140f), new Vector2(18f, 22f), 2);
            int bannerCount = catalog.BannerCount;
            var bannerTiles = new Button[bannerCount];
            var bannerImages = new Image[bannerCount];
            var bannerRings = new Image[bannerCount];
            var bannerLocks = new GameObject[bannerCount];
            for (int i = 0; i < bannerCount; i++)
            {
                Button tile = CreateButton($"Banner {i + 1}", bannerGrid, "", ButtonStyle.Ghost, new Vector2(556f, 140f));
                bannerRings[i] = CreateImage("Selected Ring", tile.transform, new Color(1f, 0.69f, 0.13f, 0f), UiArt.Rounded);
                Stretch(bannerRings[i].rectTransform, -7f, -7f, -7f, -7f);
                bannerImages[i] = Masked(tile.transform, "Mask", UiArt.Rounded, catalog.Banner(i), out RectTransform mask);
                Stretch(mask);
                Image nameBack = CreateImage("Name Back", tile.transform, new Color(0f, 0f, 0f, 0.55f), UiArt.RoundedSmall);
                At(nameBack.rectTransform, 14f, -88f, 200f, 40f);
                TextMeshProUGUI bannerName = CreateText("Name", nameBack.transform, catalog.BannerName(i).ToUpperInvariant(), 20f, TextPrimary, TextAlignmentOptions.Center, display: true);
                Stretch(bannerName.rectTransform);
                bannerLocks[i] = LockOverlay(tile.transform, out _, withPrice: false);
                bannerTiles[i] = tile;
            }
            if (bannerCount > 0) bannerRings[0].color = Accent;

            // TITLE page: two columns of titles.
            RectTransform titlePage = At(CreateRect("Title Page", content), 700f, -112f, 1140f, 780f);
            pages[2] = titlePage.gameObject;
            int titleCount = PlayerProfile.Titles.Length;
            var titleButtons = new Button[titleCount];
            var titleBackgrounds = new Image[titleCount];
            var titleNames = new TMP_Text[titleCount];
            var titleRequirements = new TMP_Text[titleCount];
            var titleLocks = new GameObject[titleCount];
            for (int i = 0; i < titleCount; i++)
            {
                float x = (i % 2) * 574f + 6f, y = -(i / 2) * 136f - 6f;
                Button tile = CreateButton($"Title {i + 1}", titlePage, "", ButtonStyle.Ghost, new Vector2(556f, 116f));
                At((RectTransform)tile.transform, x, y, 556f, 116f);
                titleBackgrounds[i] = tile.GetComponent<Image>();
                titleNames[i] = Label(tile.transform, "Name", PlayerProfile.Titles[i].name.ToUpperInvariant(), 32f, TextPrimary, 30f, -18f, 430f, 44f, display: true);
                titleRequirements[i] = Label(tile.transform, "Requirement", PlayerProfile.Titles[i].requirement, 21f, TextMuted, 30f, -64f, 430f, 34f);
                Image lockIcon = CreateImage("Lock", tile.transform, new Color(1f, 1f, 1f, 0.5f), UiArt.Icon("Lock"));
                At(lockIcon.rectTransform, 480f, -34f, 48f, 48f);
                titleLocks[i] = lockIcon.gameObject;
                titleButtons[i] = tile;
            }

            avatarPage.gameObject.SetActive(true);
            bannerPage.gameObject.SetActive(false);
            titlePage.gameObject.SetActive(false);
            tabBackgrounds[0].color = Accent;
            tabLabels[0].color = AccentDark;

            ProfileScreen screen = root.AddComponent<ProfileScreen>();
            Wire(screen, "backButton", back);
            Wire(screen, "nameInput", nameInput);
            WireArray(screen, "statValues", statValues);
            WireArray(screen, "tabButtons", tabButtons);
            WireArray(screen, "tabBackgrounds", tabBackgrounds);
            WireArray(screen, "tabLabels", tabLabels);
            WireArray(screen, "pages", pages);
            Wire(screen, "uploadButton", upload);
            Wire(screen, "customTile", customTile);
            Wire(screen, "customImage", customImage);
            Wire(screen, "customRing", customRing);
            WireArray(screen, "avatarTiles", avatarTiles);
            WireArray(screen, "avatarImages", avatarImages);
            WireArray(screen, "avatarRings", avatarRings);
            WireArray(screen, "avatarLocks", avatarLocks);
            Wire(screen, "avatarStatus", avatarStatus);
            WireArray(screen, "bannerTiles", bannerTiles);
            WireArray(screen, "bannerImages", bannerImages);
            WireArray(screen, "bannerRings", bannerRings);
            WireArray(screen, "bannerLocks", bannerLocks);
            WireArray(screen, "titleButtons", titleButtons);
            WireArray(screen, "titleBackgrounds", titleBackgrounds);
            WireArray(screen, "titleNames", titleNames);
            WireArray(screen, "titleRequirements", titleRequirements);
            WireArray(screen, "titleLocks", titleLocks);
            root.SetActive(false);
            return screen;
        }

        // ------------------------------------------------------------------ leaderboard

        public static LeaderboardScreen Leaderboard(RectTransform safe, WeaponCatalog catalog, ProfileCatalog profiles)
        {
            RectTransform content = Frame("Leaderboard Screen", safe, "LEADERBOARD", false, out GameObject root, out Button back);
            Tabs(content, new[] { "STANDINGS", "RECORDS" }, 0f, 0f, 300f, 80f, 16f, false,
                out Button[] tabButtons, out Image[] tabBackgrounds, out TMP_Text[] tabLabels);
            Label(content, "Hint", "Your matches on this device", 22f, TextMuted, 660f, -24f, 900f, 36f);

            // ---- standings
            RectTransform standings = At(CreateRect("Standings Page", content), 0f, -110f, 1840f, 790f);
            string[] headers = { "RANK", "PLAYER", "LEVEL", "PLAYED", "WON", "LOST", "K/D" };
            float[] columns = { 24f, 230f, 900f, 1080f, 1260f, 1440f, 1620f };
            float[] widths = { 120f, 640f, 160f, 160f, 160f, 160f, 180f };
            for (int i = 0; i < headers.Length; i++)
            {
                Label(standings, headers[i], headers[i], 21f, TextMuted, columns[i], 0f, widths[i], 40f,
                    i == 1 ? TextAlignmentOptions.Left : i == 0 ? TextAlignmentOptions.Left : TextAlignmentOptions.Center, display: true).characterSpacing = 4f;
            }
            RectTransform standingsList = ScrollList(standings, "Standings List", 0f, -52f, 1840f, 730f, 10f);
            LeaderboardRow standingRow = Row(standingsList, "Standing Row", 100f, profiles,
                new[] { ("Rank", 24f, 120f, 40f, TextAlignmentOptions.Left, true), ("Name", 228f, 640f, 34f, TextAlignmentOptions.Left, true),
                        ("Level", 900f, 160f, 28f, TextAlignmentOptions.Center, false), ("Played", 1080f, 160f, 32f, TextAlignmentOptions.Center, false),
                        ("Won", 1260f, 160f, 32f, TextAlignmentOptions.Center, true), ("Lost", 1440f, 160f, 32f, TextAlignmentOptions.Center, false),
                        ("KD", 1620f, 180f, 32f, TextAlignmentOptions.Center, false) }, avatarX: 130f, avatarSize: 76f);
            RectTransform standingsEmpty = At(CreateRect("Standings Empty", standings), 0f, -160f, 1840f, 400f);
            Image trophy = CreateImage("Trophy", standingsEmpty, new Color(1f, 1f, 1f, 0.18f), UiArt.Icon("Trophy"));
            Place(trophy.rectTransform, new Vector2(0.5f, 1f), Vector2.zero, new Vector2(170f, 170f), new Vector2(0.5f, 1f));
            TextMeshProUGUI emptyTitle = CreateText("Title", standingsEmpty, "NO MATCHES YET", 44f, TextPrimary, TextAlignmentOptions.Center, display: true);
            Place(emptyTitle.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -190f), new Vector2(1200f, 60f), new Vector2(0.5f, 1f));
            TextMeshProUGUI emptyText = CreateText("Text", standingsEmpty, "Play an online match — you and every opponent you face are ranked here by wins.", 26f, TextMuted, TextAlignmentOptions.Center);
            Place(emptyText.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -258f), new Vector2(1200f, 80f), new Vector2(0.5f, 1f));

            // ---- records
            RectTransform records = At(CreateRect("Records Page", content), 0f, -110f, 1840f, 790f);
            string[] recordCaptions = { "MOST ELIMINATIONS IN A MATCH", "BEST WIN STREAK", "CURRENT WIN STREAK", "WIN RATE", "FAVOURITE WEAPON", "TIME PLAYED" };
            string[] recordIcons = { "Fire", "Star", "Trophy", "Podium", "Bullets", "Reload" };
            var recordValues = new TMP_Text[recordCaptions.Length];
            for (int i = 0; i < recordCaptions.Length; i++)
            {
                float x = (i % 3) * 625f, y = -(i / 3) * 176f;
                Image tile = Panel(records, $"Record {i + 1}", x, y, 590f, 156f, Tile);
                Image icon = CreateImage("Icon", tile.transform, Accent, UiArt.Icon(recordIcons[i]));
                At(icon.rectTransform, 30f, -40f, 76f, 76f);
                recordValues[i] = Label(tile.transform, "Value", "0", 52f, TextPrimary, 136f, -22f, 430f, 70f, display: true);
                Label(tile.transform, "Caption", recordCaptions[i], 19f, TextMuted, 138f, -96f, 430f, 40f, display: true);
            }
            Label(records, "Recent Label", "RECENT MATCHES", 22f, TextMuted, 0f, -370f, 600f, 34f, display: true).characterSpacing = 8f;
            RectTransform recentList = ScrollList(records, "Recent List", 0f, -414f, 1840f, 370f, 8f);
            LeaderboardRow recentRow = Row(recentList, "Recent Row", 76f, profiles,
                new[] { ("Result", 24f, 140f, 30f, TextAlignmentOptions.Left, true), ("Opponent", 250f, 640f, 30f, TextAlignmentOptions.Left, false),
                        ("Score", 920f, 220f, 32f, TextAlignmentOptions.Center, true), ("Weapon", 1180f, 300f, 26f, TextAlignmentOptions.Center, false),
                        ("When", 1500f, 300f, 24f, TextAlignmentOptions.Right, false) }, avatarX: 170f, avatarSize: 58f);
            TextMeshProUGUI recentEmpty = Label(records, "Recent Empty", "Your online matches will show up here.", 26f, TextMuted, 0f, -440f, 1840f, 60f, TextAlignmentOptions.Center);

            records.gameObject.SetActive(false);
            tabBackgrounds[0].color = Accent;
            tabLabels[0].color = AccentDark;

            LeaderboardScreen screen = root.AddComponent<LeaderboardScreen>();
            Wire(screen, "backButton", back);
            Wire(screen, "catalog", catalog);
            WireArray(screen, "tabButtons", tabButtons);
            WireArray(screen, "tabBackgrounds", tabBackgrounds);
            WireArray(screen, "tabLabels", tabLabels);
            WireArray(screen, "pages", standings.gameObject, records.gameObject);
            Wire(screen, "standingsContent", standingsList);
            Wire(screen, "standingTemplate", standingRow);
            Wire(screen, "standingsEmpty", standingsEmpty.gameObject);
            WireArray(screen, "recordValues", recordValues);
            Wire(screen, "recentContent", recentList);
            Wire(screen, "recentTemplate", recentRow);
            Wire(screen, "recentEmpty", recentEmpty.gameObject);
            root.SetActive(false);
            return screen;
        }

        private static LeaderboardRow Row(RectTransform list, string name, float height, ProfileCatalog profiles,
            (string name, float x, float w, float size, TextAlignmentOptions align, bool display)[] cells, float avatarX, float avatarSize)
        {
            Image background = CreateImage(name, list, new Color(1f, 1f, 1f, 0.06f), UiArt.Rounded);
            background.rectTransform.sizeDelta = new Vector2(1840f, height);
            var element = background.gameObject.AddComponent<LayoutElement>();
            element.preferredHeight = height;
            element.minHeight = height;

            Image avatar = Masked(background.transform, "Avatar", UiArt.Circle, profiles.Avatar(0), out RectTransform avatarMask);
            Place(avatarMask, new Vector2(0f, 0.5f), new Vector2(avatarX, 0f), new Vector2(avatarSize, avatarSize), new Vector2(0f, 0.5f));

            var texts = new TMP_Text[cells.Length];
            for (int i = 0; i < cells.Length; i++)
            {
                TextMeshProUGUI text = CreateText(cells[i].name, background.transform, "-", cells[i].size, TextPrimary, cells[i].align, cells[i].display);
                Place(text.rectTransform, new Vector2(0f, 0.5f), new Vector2(cells[i].x, 0f), new Vector2(cells[i].w, height - 10f), new Vector2(0f, 0.5f));
                text.overflowMode = TextOverflowModes.Ellipsis;
                text.textWrappingMode = TextWrappingModes.NoWrap;
                texts[i] = text;
            }

            LeaderboardRow row = background.gameObject.AddComponent<LeaderboardRow>();
            WireArray(row, "cells", texts);
            Wire(row, "avatar", avatar);
            Wire(row, "background", background);
            background.gameObject.SetActive(false);
            return row;
        }

        // ------------------------------------------------------------------ characters

        public static CharacterScreen Characters(RectTransform safe, CharacterCatalog catalog, SoldierAnimator character, Transform turntable)
        {
            RectTransform content = Frame("Character Screen", safe, "WARDROBE", true, out GameObject root, out Button back);

            // Drag the soldier in the middle.
            Image drag = CreateImage("Character Drag Area", content, new Color(0f, 0f, 0f, 0f), raycast: true);
            Place(drag.rectTransform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(620f, 760f), new Vector2(0.5f, 0.5f));
            Wire(drag.gameObject.AddComponent<DragRotate>(), "target", turntable);

            // ---- left: two columns of character cards
            int count = catalog != null ? catalog.Count : 0;
            var cards = new Button[count];
            var frames = new Image[count];
            var portraits = new Image[count];
            var names = new TMP_Text[count];
            var tags = new GameObject[count];
            var locks = new GameObject[count];
            var prices = new TMP_Text[count];
            const float cardW = 236f, cardH = 158f, gap = 14f;
            for (int i = 0; i < count; i++)
            {
                CharacterCatalog.Entry entry = catalog.Get(i);
                float x = (i % 2) * (cardW + gap), y = -(i / 2) * (cardH + gap);
                Button card = CreateButton($"Character {i + 1}", content, "", ButtonStyle.Secondary, new Vector2(cardW, cardH));
                At((RectTransform)card.transform, x, y, cardW, cardH);
                frames[i] = card.GetComponent<Image>();
                frames[i].color = i == 0 ? Accent : new Color(1f, 1f, 1f, 0.08f);
                portraits[i] = Masked(card.transform, "Portrait", UiArt.Rounded, entry != null ? entry.portrait : null, out RectTransform mask);
                At(mask, 10f, -10f, 104f, 104f);
                names[i] = Label(card.transform, "Name", entry != null ? entry.name.ToUpperInvariant() : "", 22f, i == 0 ? AccentDark : TextPrimary, 10f, -118f, cardW - 20f, 32f, display: true);
                ((TextMeshProUGUI)names[i]).enableAutoSizing = true;
                ((TextMeshProUGUI)names[i]).fontSizeMin = 14f;
                ((TextMeshProUGUI)names[i]).fontSizeMax = 22f;
                Image tag = CreateImage("Selected", card.transform, Success, UiArt.RoundedSmall);
                At(tag.rectTransform, 122f, -14f, 104f, 30f);
                TextMeshProUGUI tagText = CreateText("Text", tag.transform, "IN USE", 16f, new Color(0.05f, 0.1f, 0.06f), TextAlignmentOptions.Center, display: true);
                Stretch(tagText.rectTransform);
                tag.gameObject.SetActive(i == 0);
                tags[i] = tag.gameObject;
                locks[i] = LockOverlay(mask, out prices[i], withPrice: true);
                cards[i] = card;
            }

            // ---- right: details
            Image details = CreateImage("Details", content, CardDark, UiArt.Rounded);
            Place(details.rectTransform, new Vector2(1f, 1f), Vector2.zero, new Vector2(560f, 880f), new Vector2(1f, 1f));
            CharacterCatalog.Entry first = catalog != null ? catalog.Get(0) : null;
            TextMeshProUGUI name = Label(details.transform, "Name", first != null ? first.name.ToUpperInvariant() : "", 56f, Accent, 36f, -32f, 490f, 74f, display: true);
            TextMeshProUGUI role = Label(details.transform, "Role", first != null ? first.role.ToUpperInvariant() : "", 24f, TextMuted, 38f, -110f, 490f, 36f, display: true);
            role.characterSpacing = 6f;
            Image line = CreateImage("Line", details.transform, new Color(1f, 1f, 1f, 0.12f));
            At(line.rectTransform, 36f, -162f, 488f, 3f);
            TextMeshProUGUI description = Label(details.transform, "Description", first != null ? first.description : "", 26f, TextPrimary, 36f, -186f, 490f, 260f);
            description.alignment = TextAlignmentOptions.TopLeft;
            Label(details.transform, "Hint", "Your character is shown in the menu, the lobby and to your opponent in the match.", 21f, TextMuted, 36f, -620f, 490f, 80f);
            Button select = CreateButton("Select", details.transform, "SELECTED", ButtonStyle.Primary, new Vector2(488f, 120f), out TextMeshProUGUI selectLabel, 44f);
            Place((RectTransform)select.transform, new Vector2(0.5f, 0f), new Vector2(0f, 36f), new Vector2(488f, 120f), new Vector2(0.5f, 0f));

            CharacterScreen screen = root.AddComponent<CharacterScreen>();
            Wire(screen, "backButton", back);
            Wire(screen, "preview", character != null ? character.GetComponent<SoldierLook>() : null);
            Wire(screen, "previewAnimator", character);
            WireArray(screen, "cards", cards);
            WireArray(screen, "cardFrames", frames);
            WireArray(screen, "portraits", portraits);
            WireArray(screen, "cardNames", names);
            WireArray(screen, "selectedTags", tags);
            WireArray(screen, "lockTags", locks);
            WireArray(screen, "priceTexts", prices);
            Wire(screen, "characterName", name);
            Wire(screen, "characterRole", role);
            Wire(screen, "characterDescription", description);
            Wire(screen, "selectButton", select);
            Wire(screen, "selectLabel", selectLabel);
            root.SetActive(false);
            return screen;
        }

        // ------------------------------------------------------------------ shop

        /// <summary>Lock overlay for a tile: dark veil, lock icon and (optionally) a star price.</summary>
        public static GameObject LockOverlay(Transform tile, out TMP_Text price, bool withPrice)
        {
            Image veil = CreateImage("Locked", tile, new Color(0f, 0f, 0f, 0.55f), UiArt.Rounded);
            Stretch(veil.rectTransform);
            Image icon = CreateImage("Lock", veil.transform, new Color(1f, 1f, 1f, 0.85f), UiArt.Icon("Lock"));
            Place(icon.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, withPrice ? 14f : 0f), new Vector2(44f, 44f), new Vector2(0.5f, 0.5f));
            price = null;
            if (withPrice)
            {
                Image star = CreateImage("Star", veil.transform, Accent, UiArt.Icon("Star"));
                Place(star.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(-26f, -26f), new Vector2(26f, 26f), new Vector2(0.5f, 0.5f));
                TextMeshProUGUI text = CreateText("Price", veil.transform, "0", 22f, Accent, TextAlignmentOptions.Left, display: true);
                Place(text.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(-10f, -26f), new Vector2(90f, 30f), new Vector2(0f, 0.5f));
                price = text;
            }
            veil.gameObject.SetActive(false);
            return veil.gameObject;
        }

        public static ShopScreen ShopPage(RectTransform safe, CharacterCatalog characters, ProfileCatalog profiles)
        {
            RectTransform content = Frame("Shop Screen", safe, "SHOP", false, out GameObject root, out Button back);

            // Balance (top right of the page).
            Image balance = CreateImage("Balance", content, new Color(0.04f, 0.05f, 0.08f, 0.9f), UiArt.Rounded);
            Place(balance.rectTransform, new Vector2(1f, 1f), new Vector2(0f, 4f), new Vector2(260f, 76f), new Vector2(1f, 1f));
            Image star = CreateImage("Star", balance.transform, Accent, UiArt.Icon("Star"));
            Place(star.rectTransform, new Vector2(0f, 0.5f), new Vector2(16f, 0f), new Vector2(52f, 52f), new Vector2(0f, 0.5f));
            TextMeshProUGUI stars = CreateText("Stars", balance.transform, "0", 40f, TextPrimary, TextAlignmentOptions.Left, display: true);
            Place(stars.rectTransform, new Vector2(0f, 0.5f), new Vector2(80f, 0f), new Vector2(170f, 60f), new Vector2(0f, 0.5f));

            Tabs(content, new[] { "COSTUMES", "AVATARS", "BANNERS" }, 0f, 0f, 280f, 80f, 16f, false,
                out Button[] tabButtons, out Image[] tabBackgrounds, out TMP_Text[] tabLabels);
            TextMeshProUGUI status = Label(content, "Status", "", 22f, TextMuted, 0f, -96f, 1500f, 34f);

            var pages = new GameObject[3];
            // Card: picture at its true aspect ratio (never stretched), name, then a buy button / OWNED tag.
            ShopScreen.Item MakeItem(RectTransform grid, string name, Sprite art, string title, Vector2 cell, Vector2 artSize)
            {
                Image card = CreateImage(name, grid, new Color(0.05f, 0.06f, 0.09f, 0.92f), UiArt.Rounded);
                Image rim = CreateImage("Rim", card.transform, new Color(1f, 1f, 1f, 0.08f), UiArt.Rounded);
                Stretch(rim.rectTransform);
                Image picture = Masked(card.transform, "Art", UiArt.Rounded, art, out RectTransform mask);
                At(mask, (cell.x - artSize.x) * 0.5f, -14f, artSize.x, artSize.y);
                picture.preserveAspect = true;
                TextMeshProUGUI titleText = Label(card.transform, "Name", title, 24f, TextPrimary, 12f, -26f - artSize.y, cell.x - 24f, 34f, TextAlignmentOptions.Center, display: true);
                titleText.enableAutoSizing = true;
                titleText.fontSizeMin = 16f;
                titleText.fontSizeMax = 24f;
                titleText.textWrappingMode = TextWrappingModes.NoWrap;

                Image owned = CreateImage("Owned", card.transform, Success, UiArt.RoundedSmall);
                Place(owned.rectTransform, new Vector2(0.5f, 0f), new Vector2(0f, 16f), new Vector2(cell.x - 44f, 52f), new Vector2(0.5f, 0f));
                TextMeshProUGUI ownedText = CreateText("Text", owned.transform, "OWNED", 22f, new Color(0.05f, 0.1f, 0.06f), TextAlignmentOptions.Center, display: true);
                Stretch(ownedText.rectTransform);

                Button buy = CreateButton("Buy", card.transform, "0", ButtonStyle.Primary, new Vector2(cell.x - 44f, 60f), out TextMeshProUGUI price, 28f);
                Place((RectTransform)buy.transform, new Vector2(0.5f, 0f), new Vector2(0f, 14f), new Vector2(cell.x - 44f, 60f), new Vector2(0.5f, 0f));
                Stretch(price.rectTransform, 56f, 12f, 4f, 4f);
                Image coin = CreateImage("Star", buy.transform, AccentDark, UiArt.Icon("Star"));
                Place(coin.rectTransform, new Vector2(0f, 0.5f), new Vector2(18f, 0f), new Vector2(32f, 32f), new Vector2(0f, 0.5f));
                return new ShopScreen.Item { buy = buy, label = price, owned = owned.gameObject, priceIcon = coin.gameObject };
            }

            // Costumes.
            RectTransform costumePage = At(CreateRect("Costumes Page", content), 0f, -140f, 1840f, 770f);
            pages[0] = costumePage.gameObject;
            var costumeCell = new Vector2(330f, 440f);
            RectTransform costumeGrid = ScrollGrid(costumePage, "Costume List", costumeCell, new Vector2(30f, 30f), 5);
            int characterCount = characters != null ? characters.Count : 0;
            var costumes = new ShopScreen.Item[characterCount];
            for (int i = 0; i < characterCount; i++)
            {
                CharacterCatalog.Entry entry = characters.Get(i);
                costumes[i] = MakeItem(costumeGrid, $"Costume {i + 1}", entry.portrait, entry.name.ToUpperInvariant(), costumeCell, new Vector2(290f, 290f));
            }

            // Avatars.
            RectTransform avatarPage = At(CreateRect("Avatars Page", content), 0f, -140f, 1840f, 770f);
            pages[1] = avatarPage.gameObject;
            var avatarCell = new Vector2(270f, 380f);
            RectTransform avatarGrid = ScrollGrid(avatarPage, "Avatar List", avatarCell, new Vector2(28f, 28f), 6);
            var avatars = new ShopScreen.Item[profiles.AvatarCount];
            for (int i = 0; i < avatars.Length; i++) avatars[i] = MakeItem(avatarGrid, $"Avatar {i + 1}", profiles.Avatar(i), $"AVATAR {i + 1}", avatarCell, new Vector2(230f, 230f));

            // Banners.
            RectTransform bannerPage = At(CreateRect("Banners Page", content), 0f, -140f, 1840f, 770f);
            pages[2] = bannerPage.gameObject;
            var bannerCell = new Vector2(580f, 290f);
            RectTransform bannerGrid = ScrollGrid(bannerPage, "Banner List", bannerCell, new Vector2(30f, 30f), 3);
            var banners = new ShopScreen.Item[profiles.BannerCount];
            for (int i = 0; i < banners.Length; i++) banners[i] = MakeItem(bannerGrid, $"Banner {i + 1}", profiles.Banner(i), profiles.BannerName(i).ToUpperInvariant(), bannerCell, new Vector2(540f, 135f));

            avatarPage.gameObject.SetActive(false);
            bannerPage.gameObject.SetActive(false);
            tabBackgrounds[0].color = Accent;
            tabLabels[0].color = AccentDark;

            ShopScreen screen = root.AddComponent<ShopScreen>();
            Wire(screen, "backButton", back);
            Wire(screen, "starsText", stars);
            Wire(screen, "statusText", status);
            WireArray(screen, "tabButtons", tabButtons);
            WireArray(screen, "tabBackgrounds", tabBackgrounds);
            WireArray(screen, "tabLabels", tabLabels);
            WireArray(screen, "pages", pages);
            var so = new SerializedObject(screen);
            void SetItems(string field, ShopScreen.Item[] items)
            {
                SerializedProperty array = so.FindProperty(field);
                array.arraySize = items.Length;
                for (int i = 0; i < items.Length; i++)
                {
                    SerializedProperty element = array.GetArrayElementAtIndex(i);
                    element.FindPropertyRelative("buy").objectReferenceValue = items[i].buy;
                    element.FindPropertyRelative("label").objectReferenceValue = items[i].label;
                    element.FindPropertyRelative("owned").objectReferenceValue = items[i].owned;
                    element.FindPropertyRelative("priceIcon").objectReferenceValue = items[i].priceIcon;
                }
            }
            SetItems("costumes", costumes);
            SetItems("avatars", avatars);
            SetItems("banners", banners);
            so.ApplyModifiedPropertiesWithoutUndo();
            root.SetActive(false);
            return screen;
        }

        // ------------------------------------------------------------------ armory

        public static ArmoryScreen Armory(RectTransform safe, WeaponCatalog catalog, SoldierAnimator character, Transform turntable)
        {
            RectTransform content = Frame("Armory Screen", safe, "ARMORY", true, out GameObject root, out Button back);

            // Drag the soldier in the middle.
            Image drag = CreateImage("Character Drag Area", content, new Color(0f, 0f, 0f, 0f), raycast: true);
            Place(drag.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, 0f), new Vector2(640f, 760f), new Vector2(0.5f, 0.5f));
            Wire(drag.gameObject.AddComponent<DragRotate>(), "target", turntable);

            // ---- left: weapon list (two columns)
            int weaponCount = catalog != null ? catalog.Count : 4;
            var tiles = new Button[weaponCount];
            var frames = new Image[weaponCount];
            var icons = new Image[weaponCount];
            var names = new TMP_Text[weaponCount];
            var equipped = new GameObject[weaponCount];
            var equippedTexts = new TMP_Text[weaponCount];
            for (int i = 0; i < weaponCount; i++)
            {
                WeaponDefinition def = catalog != null ? catalog.Get(i) : null;
                Button tile = CreateButton($"Weapon {i + 1}", content, "", ButtonStyle.Secondary, new Vector2(470f, 160f));
                At((RectTransform)tile.transform, (i % 2) * 300f, -(i / 2) * 200f, 284f, 186f);
                frames[i] = tile.GetComponent<Image>();
                frames[i].color = i == 0 ? Accent : new Color(1f, 1f, 1f, 0.08f);
                icons[i] = CreateImage("Icon", tile.transform, i == 0 ? AccentDark : Color.white, def != null ? def.icon : null);
                icons[i].preserveAspect = true;
                At(icons[i].rectTransform, 22f, -40f, 240f, 90f);
                names[i] = Label(tile.transform, "Name", def != null ? def.displayName.ToUpperInvariant() : $"WEAPON {i + 1}", 24f, i == 0 ? AccentDark : TextPrimary, 14f, -136f, 256f, 40f, TextAlignmentOptions.Center, display: true);
                ((TextMeshProUGUI)names[i]).enableAutoSizing = true;
                ((TextMeshProUGUI)names[i]).fontSizeMin = 18f;
                ((TextMeshProUGUI)names[i]).fontSizeMax = 30f;
                ((TextMeshProUGUI)names[i]).textWrappingMode = TextWrappingModes.NoWrap;
                Image tag = CreateImage("Equipped", tile.transform, Success, UiArt.RoundedSmall);
                At(tag.rectTransform, 132f, -10f, 140f, 30f);
                TextMeshProUGUI tagText = CreateText("Text", tag.transform, "SLOT 1", 18f, new Color(0.05f, 0.1f, 0.06f), TextAlignmentOptions.Center, display: true);
                Stretch(tagText.rectTransform);
                tag.gameObject.SetActive(i == 0);
                equipped[i] = tag.gameObject;
                equippedTexts[i] = tagText;
                tiles[i] = tile;
            }

            // ---- right: details
            Image details = CreateImage("Details", content, CardDark, UiArt.Rounded);
            Place(details.rectTransform, new Vector2(1f, 1f), Vector2.zero, new Vector2(580f, 900f), new Vector2(1f, 1f));
            WeaponDefinition first = catalog != null ? catalog.Get(0) : null;

            // Loadout: the three weapons carried into every match. Tap a slot, then a weapon, then EQUIP.
            Label(details.transform, "Loadout Header", "YOUR LOADOUT", 22f, TextMuted, 36f, -22f, 510f, 32f, display: true).characterSpacing = 6f;
            var slotButtons = new Button[3];
            var slotFrames = new Image[3];
            var slotIcons = new Image[3];
            var slotNames = new TMP_Text[3];
            for (int s = 0; s < 3; s++)
            {
                WeaponDefinition slotDef = catalog != null ? catalog.Get(new[] { 0, 3, 6 }[s]) : null;
                Button slot = CreateButton($"Slot {s + 1}", details.transform, "", ButtonStyle.Secondary, new Vector2(160f, 150f));
                At((RectTransform)slot.transform, 36f + s * 172f, -60f, 160f, 150f);
                slotFrames[s] = slot.GetComponent<Image>();
                slotFrames[s].color = s == 0 ? Accent : new Color(1f, 1f, 1f, 0.1f);
                TextMeshProUGUI number = Label(slot.transform, "Number", (s + 1).ToString(), 22f, TextMuted, 10f, -6f, 40f, 30f, display: true);
                number.alpha = 0.8f;
                slotIcons[s] = CreateImage("Icon", slot.transform, s == 0 ? AccentDark : Color.white, slotDef != null ? slotDef.icon : null);
                slotIcons[s].preserveAspect = true;
                At(slotIcons[s].rectTransform, 12f, -34f, 136f, 66f);
                TextMeshProUGUI slotName = Label(slot.transform, "Name", slotDef != null ? slotDef.displayName.ToUpperInvariant() : "", 18f, s == 0 ? AccentDark : TextPrimary, 6f, -106f, 148f, 34f, TextAlignmentOptions.Center, display: true);
                slotName.enableAutoSizing = true;
                slotName.fontSizeMin = 12f;
                slotName.fontSizeMax = 18f;
                slotName.textWrappingMode = TextWrappingModes.NoWrap;
                slotNames[s] = slotName;
                slotButtons[s] = slot;
            }

            TextMeshProUGUI weaponName = Label(details.transform, "Name", first != null ? first.displayName.ToUpperInvariant() : "", 46f, Accent, 36f, -228f, 510f, 60f, display: true);
            TextMeshProUGUI description = Label(details.transform, "Description", first != null ? first.description : "", 21f, TextMuted, 36f, -290f, 510f, 84f);
            description.alignment = TextAlignmentOptions.TopLeft;
            string[] stats = { "DAMAGE", "FIRE RATE", "RANGE", "MAGAZINE" };
            var bars = new RectTransform[4];
            var values = new TMP_Text[4];
            for (int i = 0; i < 4; i++)
            {
                float y = -390f - i * 70f;
                Label(details.transform, stats[i], stats[i], 22f, TextPrimary, 36f, y, 300f, 34f, display: true);
                values[i] = Label(details.transform, stats[i] + " Value", "", 24f, Accent, 300f, y, 244f, 34f, TextAlignmentOptions.Right, display: true);
                Image track = CreateImage("Track", details.transform, new Color(1f, 1f, 1f, 0.12f), UiArt.RoundedSmall);
                At(track.rectTransform, 36f, y - 40f, 508f, 18f);
                Image fill = CreateImage("Fill", track.transform, Accent, UiArt.RoundedSmall);
                fill.rectTransform.anchorMin = Vector2.zero;
                fill.rectTransform.anchorMax = new Vector2(0.5f, 1f);
                fill.rectTransform.offsetMin = fill.rectTransform.offsetMax = Vector2.zero;
                bars[i] = fill.rectTransform;
            }
            Button equip = CreateButton("Equip", details.transform, "EQUIPPED", ButtonStyle.Primary, new Vector2(508f, 120f), out TextMeshProUGUI equipLabel, 44f);
            Place((RectTransform)equip.transform, new Vector2(0.5f, 0f), new Vector2(0f, 36f), new Vector2(508f, 120f), new Vector2(0.5f, 0f));
            Label(details.transform, "Hint", "Tap a slot, pick a weapon, then EQUIP. You carry all three in every match.", 19f, TextMuted, 36f, -680f, 510f, 56f);

            ArmoryScreen screen = root.AddComponent<ArmoryScreen>();
            Wire(screen, "backButton", back);
            Wire(screen, "catalog", catalog);
            Wire(screen, "character", character);
            WireArray(screen, "weaponTiles", tiles);
            WireArray(screen, "tileFrames", frames);
            WireArray(screen, "tileIcons", icons);
            WireArray(screen, "tileNames", names);
            WireArray(screen, "equippedTags", equipped);
            WireArray(screen, "equippedTagTexts", equippedTexts);
            WireArray(screen, "slotButtons", slotButtons);
            WireArray(screen, "slotFrames", slotFrames);
            WireArray(screen, "slotIcons", slotIcons);
            WireArray(screen, "slotNames", slotNames);
            Wire(screen, "weaponName", weaponName);
            Wire(screen, "weaponDescription", description);
            WireArray(screen, "statBars", bars);
            WireArray(screen, "statValues", values);
            Wire(screen, "equipButton", equip);
            Wire(screen, "equipLabel", equipLabel);
            root.SetActive(false);
            return screen;
        }
    }
}
