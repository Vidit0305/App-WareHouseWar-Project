using Fusion;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using WarehouseDuel.Audio;
using WarehouseDuel.Core;
using WarehouseDuel.Multiplayer;
using WarehouseDuel.Profile;
using WarehouseDuel.Weapons;

namespace WarehouseDuel.UI
{
    /// <summary>
    /// Room lobby, shown to both players: room code (+ copy), both player cards (name, connection,
    /// weapon, ready), weapon selection with stats, Ready toggle, and Start Match for the host.
    /// All data comes from the networked <see cref="RoomState"/>; changes are requested through RPCs.
    /// </summary>
    public sealed class LobbyController : MonoBehaviour
    {
        [System.Serializable]
        public sealed class PlayerCard
        {
            public TMP_Text nameText;
            public TMP_Text statusText;
            public TMP_Text weaponText;
            public Image readyBadge;
            public TMP_Text readyText;
            public Image avatar;
            public Image portrait;
            public Image banner;
            public TMP_Text levelText;
        }

        [SerializeField] private WeaponCatalog catalog;

        [Header("Room")]
        [SerializeField] private TMP_Text roomCodeText;
        [SerializeField] private Button copyCodeButton;
        [SerializeField] private TMP_Text copyFeedbackText;
        [SerializeField] private TMP_Text roomStatusText;
        [SerializeField] private TMP_Text pingText;

        [Header("Players")]
        [SerializeField] private PlayerCard[] cards = new PlayerCard[2];

        [Header("Weapon selection")]
        [SerializeField] private Button[] weaponButtons = new Button[4];
        [SerializeField] private Image[] weaponButtonFrames = new Image[4];
        [SerializeField] private TMP_Text weaponNameText;
        [SerializeField] private TMP_Text weaponDescriptionText;
        [SerializeField] private RectTransform[] statBars = new RectTransform[4]; // damage, fire rate, range, magazine

        [Header("Actions")]
        [SerializeField] private Button readyButton;
        [SerializeField] private TMP_Text readyButtonText;
        [SerializeField] private Button startButton;
        [SerializeField] private TMP_Text startButtonText;
        [SerializeField] private Button leaveButton;

        [Header("Showcase character")]
        [SerializeField] private WarehouseDuel.Player.SoldierAnimator character;
        [SerializeField] private TMP_Text characterName;

        private static readonly Color Selected = new Color(1f, 0.69f, 0.13f, 0.6f);
        private static readonly Color Unselected = new Color(1f, 1f, 1f, 0.12f);
        private static readonly Color ReadyColor = new Color(0.3f, 0.85f, 0.45f);
        private static readonly Color NotReadyColor = new Color(0.55f, 0.58f, 0.65f);
        private static readonly Color[] SlotColors = { new Color(0.2f, 0.55f, 1f), new Color(1f, 0.35f, 0.25f) };

        private SessionService session;
        private int selectedWeapon;
        private bool profileSent;
        private float copyFeedbackUntil;

        private void Start()
        {
            session = SessionService.Instance;
            if (session == null || session.Runner == null)
            {
                // Opened directly in the Editor without a session: go back to the menu.
                UnityEngine.SceneManagement.SceneManager.LoadScene(GameConstants.Scenes.MainMenu);
                return;
            }

            selectedWeapon = GameSettings.PreferredWeapon;
            for (int i = 0; i < weaponButtons.Length; i++)
            {
                int index = i;
                weaponButtons[i].onClick.AddListener(() => SelectWeapon(index));
                WeaponDefinition def = catalog.Get(i);
                TMP_Text label = weaponButtons[i].GetComponentInChildren<TMP_Text>();
                if (label != null && def != null) label.text = def.displayName.ToUpperInvariant();
                Image icon = weaponButtons[i].transform.Find("Icon")?.GetComponent<Image>();
                if (icon != null && def != null && def.icon != null) icon.sprite = def.icon;
            }

            copyCodeButton.onClick.AddListener(CopyCode);
            readyButton.onClick.AddListener(ToggleReady);
            startButton.onClick.AddListener(() => session.StartMatch());
            leaveButton.onClick.AddListener(() => session.LeaveSession());

            RoomState.Changed += Refresh;
            ShowWeapon(selectedWeapon);
            Refresh();

            AudioManager.Instance.StopAmbience();
            if (AudioManager.Library != null) AudioManager.Instance.PlayMusic(AudioManager.Library.menuMusic);
        }

        private void OnDestroy() => RoomState.Changed -= Refresh;

        private void Update()
        {
            // The room object can arrive a few frames after the scene; send our profile once it exists.
            if (!profileSent && RoomState.Instance != null && RoomState.Instance.Object != null && RoomState.Instance.SlotOf(LocalPlayer) >= 0)
            {
                profileSent = true;
                RoomState.Instance.RequestProfile(GameSettings.PlayerName, (byte)selectedWeapon,
                    (byte)PlayerProfile.AvatarIndex, (byte)PlayerProfile.BannerIndex, (byte)PlayerProfile.TitleIndex, (byte)PlayerProfile.Level);
            }

            if (pingText != null && session != null)
            {
                pingText.text = session.IsHost ? "HOST" : $"{session.RoundTripMilliseconds:0} ms";
            }

            if (copyFeedbackText != null && copyFeedbackText.gameObject.activeSelf && Time.unscaledTime > copyFeedbackUntil)
            {
                copyFeedbackText.gameObject.SetActive(false);
            }
        }

        private PlayerRef LocalPlayer => session != null && session.Runner != null ? session.Runner.LocalPlayer : PlayerRef.None;

        private void Refresh()
        {
            if (session == null) return;
            roomCodeText.text = string.IsNullOrEmpty(session.RoomCode) ? "-----" : session.RoomCode;

            RoomState room = RoomState.Instance;
            if (room == null || room.Object == null)
            {
                roomStatusText.text = "Connecting to room…";
                readyButton.interactable = false;
                startButton.gameObject.SetActive(false);
                return;
            }

            for (int i = 0; i < RoomState.Capacity; i++) FillCard(cards[i], room.Slots[i], i);

            int count = room.OccupiedCount;
            room.TryGetSlot(LocalPlayer, out PlayerSlot me);
            bool everyoneReady = room.AllReady;

            if (count < RoomState.Capacity) roomStatusText.text = "Waiting for your opponent to join… Share the room code.";
            else if (!everyoneReady) roomStatusText.text = "Both players are here. Pick a weapon and press READY.";
            else roomStatusText.text = session.IsHost ? "Everyone is ready — start the match!" : "Everyone is ready — waiting for the host to start.";

            readyButton.interactable = me.Occupied;
            if (character != null) character.Ready = me.Ready;
            if (characterName != null)
            {
                string shownName = me.Occupied ? me.Name.ToString() : GameSettings.PlayerName;
                characterName.text = me.Ready ? $"{shownName}  <color=#50D773>READY</color>" : shownName;
            }
            readyButtonText.text = me.Ready ? "NOT READY" : "READY";

            startButton.gameObject.SetActive(session.IsHost);
            startButton.interactable = session.IsHost && count == RoomState.Capacity && everyoneReady;
            startButtonText.text = count < RoomState.Capacity ? "WAITING FOR PLAYER" : everyoneReady ? "START MATCH" : "WAITING FOR READY";
        }

        private void FillCard(PlayerCard card, PlayerSlot slot, int index)
        {
            if (card == null) return;
            if (card.avatar != null) card.avatar.color = slot.Occupied ? SlotColors[index] : new Color(1f, 1f, 1f, 0.1f);
            bool mine = slot.Occupied && slot.Player == LocalPlayer;
            ProfileCatalog profiles = ProfileCatalog.Instance;
            if (card.portrait != null)
            {
                card.portrait.enabled = slot.Occupied;
                card.portrait.sprite = mine ? PlayerProfile.MyAvatar : profiles != null ? profiles.Avatar(slot.Avatar) : null;
            }
            if (card.banner != null)
            {
                card.banner.enabled = slot.Occupied && profiles != null;
                if (profiles != null) card.banner.sprite = profiles.Banner(mine ? PlayerProfile.BannerIndex : slot.Banner);
            }
            if (card.levelText != null) card.levelText.text = slot.Occupied ? $"LV {Mathf.Max(1, (int)slot.Level)}" : string.Empty;

            if (!slot.Occupied)
            {
                card.nameText.text = "Waiting…";
                card.statusText.text = "Not connected";
                card.weaponText.text = string.Empty;
                card.readyBadge.color = new Color(1f, 1f, 1f, 0.08f);
                card.readyText.text = "—";
                return;
            }

            bool isMe = slot.Player == LocalPlayer;
            string role = index == 0 ? "HOST" : "GUEST";
            card.nameText.text = isMe ? $"{slot.Name} <size=70%>(you)</size>" : slot.Name.ToString();
            int title = Mathf.Clamp(slot.Title, 0, PlayerProfile.Titles.Length - 1);
            card.statusText.text = $"<color=#FFB020>{PlayerProfile.Titles[title].name}</color>  •  {role}";
            WeaponDefinition weapon = catalog.Get(slot.Weapon);
            card.weaponText.text = weapon != null ? weapon.displayName : string.Empty;
            card.readyBadge.color = slot.Ready ? ReadyColor : NotReadyColor;
            card.readyText.text = slot.Ready ? "READY" : "NOT READY";
        }

        private void SelectWeapon(int index)
        {
            selectedWeapon = index;
            GameSettings.PreferredWeapon = index;
            ShowWeapon(index);
            if (RoomState.Instance != null && RoomState.Instance.Object != null) RoomState.Instance.RequestWeapon((byte)index);
        }

        private void ShowWeapon(int index)
        {
            for (int i = 0; i < weaponButtonFrames.Length; i++)
            {
                if (weaponButtonFrames[i] != null) weaponButtonFrames[i].color = i == index ? Selected : Unselected;
            }

            if (character != null) character.SetWeapon(index);

            WeaponDefinition def = catalog.Get(index);
            if (def == null) return;
            weaponNameText.text = def.displayName.ToUpperInvariant();
            weaponDescriptionText.text = def.description;

            // Normalised stats for the bars (damage per shot, fire rate, effective range, magazine).
            SetBar(0, def.damage * def.pellets / 100f);
            SetBar(1, def.roundsPerMinute / 900f);
            SetBar(2, def.falloffEnd / 60f);
            SetBar(3, def.magazineSize / 32f);
        }

        private void SetBar(int index, float value)
        {
            if (index >= statBars.Length || statBars[index] == null) return;
            Vector2 max = statBars[index].anchorMax;
            max.x = Mathf.Clamp(value, 0.05f, 1f);
            statBars[index].anchorMax = max;
        }

        private void ToggleReady()
        {
            RoomState room = RoomState.Instance;
            if (room == null || room.Object == null) return;
            room.TryGetSlot(LocalPlayer, out PlayerSlot me);
            room.RequestReady(!me.Ready);
        }

        private void CopyCode()
        {
            if (string.IsNullOrEmpty(session.RoomCode)) return;
            GUIUtility.systemCopyBuffer = session.RoomCode;
            if (copyFeedbackText != null)
            {
                copyFeedbackText.gameObject.SetActive(true);
                copyFeedbackText.text = "Copied!";
                copyFeedbackUntil = Time.unscaledTime + 1.5f;
            }
        }
    }
}
