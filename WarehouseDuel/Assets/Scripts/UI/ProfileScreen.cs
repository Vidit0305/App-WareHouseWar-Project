using TMPro;
using UnityEngine;
using UnityEngine.UI;
using WarehouseWar.Core;
using WarehouseWar.Profile;

namespace WarehouseWar.UI
{
    /// <summary>
    /// Profile editor: callsign, avatar (presets or a photo from the device), banner, title, and lifetime stats.
    /// Everything saves immediately; the preview card (a <see cref="Widgets.ProfileCard"/>) updates live.
    /// </summary>
    public sealed class ProfileScreen : MenuScreen
    {
        [Header("Identity")]
        [SerializeField] private TMP_InputField nameInput;
        [SerializeField] private TMP_Text[] statValues = new TMP_Text[6]; // matches, wins, win rate, kills, deaths, K/D

        [Header("Tabs: avatar, banner, title")]
        [SerializeField] private Button[] tabButtons = new Button[3];
        [SerializeField] private Image[] tabBackgrounds = new Image[3];
        [SerializeField] private TMP_Text[] tabLabels = new TMP_Text[3];
        [SerializeField] private GameObject[] pages = new GameObject[3];

        [Header("Avatar")]
        [SerializeField] private Button uploadButton;
        [SerializeField] private Button customTile;
        [SerializeField] private Image customImage;
        [SerializeField] private Image customRing;
        [SerializeField] private Button[] avatarTiles = new Button[0];
        [SerializeField] private Image[] avatarImages = new Image[0];
        [SerializeField] private Image[] avatarRings = new Image[0];
        [SerializeField] private TMP_Text avatarStatus;
        [SerializeField] private GameObject[] avatarLocks = new GameObject[0];

        [Header("Banner")]
        [SerializeField] private Button[] bannerTiles = new Button[0];
        [SerializeField] private Image[] bannerImages = new Image[0];
        [SerializeField] private Image[] bannerRings = new Image[0];
        [SerializeField] private GameObject[] bannerLocks = new GameObject[0];

        [Header("Title")]
        [SerializeField] private Button[] titleButtons = new Button[0];
        [SerializeField] private Image[] titleBackgrounds = new Image[0];
        [SerializeField] private TMP_Text[] titleNames = new TMP_Text[0];
        [SerializeField] private TMP_Text[] titleRequirements = new TMP_Text[0];
        [SerializeField] private GameObject[] titleLocks = new GameObject[0];

        private static readonly Color RingOn = new Color(1f, 0.69f, 0.13f, 1f);
        private static readonly Color RingOff = new Color(1f, 1f, 1f, 0f);
        private int tab;
        private bool picking;

        protected override void Awake()
        {
            base.Awake();
            nameInput.characterLimit = GameSettings.MaxNameLength;
            nameInput.onEndEdit.AddListener(value =>
            {
                PlayerProfile.Name = value;
                nameInput.text = PlayerProfile.Name; // show the sanitised version
            });

            for (int i = 0; i < tabButtons.Length; i++)
            {
                int index = i;
                tabButtons[i].onClick.AddListener(() => SelectTab(index));
            }

            ProfileCatalog catalog = ProfileCatalog.Instance;
            for (int i = 0; i < avatarTiles.Length; i++)
            {
                int index = i;
                avatarTiles[i].onClick.AddListener(() =>
                {
                    if (Shop.IsOwned(Shop.Category.Avatar, index)) PlayerProfile.AvatarIndex = index;
                    else if (avatarStatus != null) avatarStatus.text = $"<color=#FFB020>Locked.</color> Unlock it in the Shop for {Shop.Price(Shop.Category.Avatar, index)} stars.";
                });
                if (catalog != null && i < avatarImages.Length) avatarImages[i].sprite = catalog.Avatar(i);
            }
            for (int i = 0; i < bannerTiles.Length; i++)
            {
                int index = i;
                bannerTiles[i].onClick.AddListener(() =>
                {
                    if (Shop.IsOwned(Shop.Category.Banner, index)) PlayerProfile.BannerIndex = index;
                    else if (avatarStatus != null) avatarStatus.text = $"<color=#FFB020>Locked.</color> Unlock it in the Shop for {Shop.Price(Shop.Category.Banner, index)} stars.";
                });
                if (catalog != null && i < bannerImages.Length) bannerImages[i].sprite = catalog.Banner(i);
            }
            for (int i = 0; i < titleButtons.Length; i++)
            {
                int index = i;
                titleButtons[i].onClick.AddListener(() => PlayerProfile.TitleIndex = index);
            }

            uploadButton.onClick.AddListener(PickPhoto);
            customTile.onClick.AddListener(() => PlayerProfile.UsingCustomAvatar = true);
        }

        private void OnEnable()
        {
            PlayerProfile.Changed += Refresh;
            nameInput.text = PlayerProfile.Name;
            if (avatarStatus != null) avatarStatus.text = DefaultHint;
            SelectTab(tab);
            Refresh();
        }

        private void OnDisable() => PlayerProfile.Changed -= Refresh;

        private const string DefaultHint = "";

        private void SelectTab(int index)
        {
            tab = index;
            ShowTab(index, tabBackgrounds, tabLabels, pages);
        }

        private void Refresh()
        {
            int played = GameSettings.MatchesPlayed, won = GameSettings.MatchesWon;
            int kills = GameSettings.TotalKills, deaths = GameSettings.TotalDeaths;
            SetStat(0, played.ToString());
            SetStat(1, won.ToString());
            SetStat(2, played == 0 ? "0%" : $"{100f * won / played:0}%");
            SetStat(3, kills.ToString());
            SetStat(4, deaths.ToString());
            SetStat(5, (deaths == 0 ? kills : kills / (float)deaths).ToString("0.00"));

            bool custom = PlayerProfile.UsingCustomAvatar;
            int avatar = PlayerProfile.AvatarIndex;
            for (int i = 0; i < avatarRings.Length; i++) avatarRings[i].color = !custom && i == avatar ? RingOn : RingOff;

            bool hasPhoto = PlayerProfile.HasCustomAvatar;
            customTile.gameObject.SetActive(hasPhoto);
            if (hasPhoto) customImage.sprite = PlayerProfile.CustomAvatar;
            customRing.color = custom ? RingOn : RingOff;

            for (int i = 0; i < avatarLocks.Length; i++) if (avatarLocks[i] != null) avatarLocks[i].SetActive(!Shop.IsOwned(Shop.Category.Avatar, i));
            for (int i = 0; i < bannerLocks.Length; i++) if (bannerLocks[i] != null) bannerLocks[i].SetActive(!Shop.IsOwned(Shop.Category.Banner, i));

            int banner = PlayerProfile.BannerIndex;
            for (int i = 0; i < bannerRings.Length; i++) bannerRings[i].color = i == banner ? RingOn : RingOff;

            int title = PlayerProfile.TitleIndex;
            for (int i = 0; i < titleButtons.Length && i < PlayerProfile.Titles.Length; i++)
            {
                PlayerProfile.TitleInfo info = PlayerProfile.Titles[i];
                bool unlocked = info.unlocked();
                titleButtons[i].interactable = unlocked;
                titleNames[i].text = info.name.ToUpperInvariant();
                titleNames[i].color = unlocked ? (i == title ? new Color(0.15f, 0.12f, 0.05f) : Color.white) : new Color(1f, 1f, 1f, 0.45f);
                titleRequirements[i].text = i == title ? "EQUIPPED" : unlocked ? "Tap to equip" : info.requirement;
                titleRequirements[i].color = i == title ? new Color(0.15f, 0.12f, 0.05f, 0.8f) : new Color(0.59f, 0.62f, 0.69f);
                titleBackgrounds[i].color = i == title ? RingOn : new Color(1f, 1f, 1f, unlocked ? 0.1f : 0.04f);
                if (i < titleLocks.Length && titleLocks[i] != null) titleLocks[i].SetActive(!unlocked);
            }
        }

        private void SetStat(int index, string value)
        {
            if (index < statValues.Length && statValues[index] != null) statValues[index].text = value;
        }

        private void PickPhoto()
        {
            if (picking) return;
            picking = true;
            if (avatarStatus != null) avatarStatus.text = "Opening your photos…";
            AvatarPicker.Pick((texture, error) =>
            {
                picking = false;
                if (texture != null)
                {
                    PlayerProfile.SetCustomAvatar(texture);
                    if (avatarStatus != null) avatarStatus.text = "Photo set as your avatar.";
                }
                else if (avatarStatus != null)
                {
                    avatarStatus.text = string.IsNullOrEmpty(error) ? DefaultHint : $"<color=#EB503C>{error}</color>";
                }
            });
        }
    }
}
