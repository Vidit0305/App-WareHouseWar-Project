using Fusion;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using WarehouseWar.Audio;
using WarehouseWar.Core;
using WarehouseWar.Multiplayer;
using WarehouseWar.Profile;
using WarehouseWar.Weapons;

namespace WarehouseWar.UI
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
        [SerializeField] private TMP_Text mapText;
        [SerializeField] private TMP_Text playerCountText;

        [Header("Players")]
        [SerializeField] private PlayerCard[] cards = new PlayerCard[2];

        /// <summary>One of the three equipped weapons (set in the Armory), shown on the right.</summary>
        [System.Serializable]
        public struct LoadoutCard
        {
            public Button button;
            public Image frame;
            public Image icon;
            public TMP_Text nameText;
            public TMP_Text infoText;
        }

        [Header("Loadout (equipped in the Armory)")]
        [SerializeField] private LoadoutCard[] loadoutCards = new LoadoutCard[3];

        [Header("Actions")]
        [SerializeField] private Button readyButton;
        [SerializeField] private TMP_Text readyButtonText;
        [SerializeField] private Button startButton;
        [SerializeField] private TMP_Text startButtonText;
        [SerializeField] private Button leaveButton;

        [Header("Showcase character")]
        [SerializeField] private WarehouseWar.Player.SoldierAnimator character;
        [SerializeField] private TMP_Text characterName;

        private static readonly Color Selected = new Color(1f, 0.69f, 0.13f, 0.6f);
        private static readonly Color Unselected = new Color(1f, 1f, 1f, 0.12f);
        private static readonly Color ReadyColor = new Color(0.3f, 0.85f, 0.45f);
        private static readonly Color NotReadyColor = new Color(0.55f, 0.58f, 0.65f);
        private static readonly Color[] SlotColors =
        {
            new Color(0.2f, 0.55f, 1f), new Color(1f, 0.35f, 0.25f), new Color(0.3f, 0.85f, 0.4f), new Color(1f, 0.75f, 0.15f),
            new Color(0.7f, 0.4f, 1f), new Color(0.2f, 0.9f, 0.95f), new Color(1f, 0.45f, 0.75f), new Color(0.95f, 0.95f, 0.95f),
        };

        private SessionService session;
        private int[] loadout;
        private int shownCard;
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

            loadout = GameSettings.Loadout;
            if (character != null && character.TryGetComponent(out WarehouseWar.Player.SoldierLook look)) look.Apply(PlayerProfile.CharacterIndex);
            for (int i = 0; i < loadoutCards.Length && i < loadout.Length; i++)
            {
                int card = i;
                LoadoutCard c = loadoutCards[i];
                WeaponDefinition def = catalog.Get(loadout[i]);
                if (c.button != null) c.button.onClick.AddListener(() => ShowCard(card));
                if (def == null) continue;
                if (c.nameText != null) c.nameText.text = def.displayName.ToUpperInvariant();
                if (c.icon != null)
                {
                    c.icon.sprite = def.icon;
                    c.icon.enabled = def.icon != null;
                }
                if (c.infoText != null) c.infoText.text = Describe(def);
            }

            copyCodeButton.onClick.AddListener(CopyCode);
            readyButton.onClick.AddListener(ToggleReady);
            startButton.onClick.AddListener(() => session.StartMatch());
            leaveButton.onClick.AddListener(() => session.LeaveSession());

            RoomState.Changed += Refresh;
            ShowCard(0);
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
                RoomState.Instance.RequestProfile(GameSettings.PlayerName, (byte)loadout[0],
                    (byte)PlayerProfile.AvatarIndex, (byte)PlayerProfile.BannerIndex, (byte)PlayerProfile.TitleIndex, (byte)PlayerProfile.Level, (byte)PlayerProfile.CharacterIndex,
                    (byte)loadout[1], (byte)loadout[2]);
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
            RoomState room = RoomState.Instance;
            bool quick = room != null && room.Object != null && room.QuickPlay;
            roomCodeText.text = quick ? "QUICK MATCH" : string.IsNullOrEmpty(session.RoomCode) ? "-----" : session.RoomCode;
            if (copyCodeButton != null) copyCodeButton.gameObject.SetActive(!quick);
            if (room == null || room.Object == null)
            {
                roomStatusText.text = "Connecting to room…";
                readyButton.interactable = false;
                startButton.gameObject.SetActive(false);
                return;
            }

            for (int i = 0; i < RoomState.Capacity && i < cards.Length; i++) FillCard(cards[i], room.Slots[i], i);

            int count = room.OccupiedCount;
            room.TryGetSlot(LocalPlayer, out PlayerSlot me);
            bool everyoneReady = room.AllReady;
            if (mapText != null)
            {
                int map = room.MapChoice;
                mapText.text = map < GameConstants.Scenes.MapNames.Length ? $"MAP: {GameConstants.Scenes.MapNames[map].ToUpperInvariant()}" : "MAP: RANDOM";
            }
            if (playerCountText != null) playerCountText.text = $"{count}/{RoomState.Capacity}";

            if (count < RoomState.MinPlayers) roomStatusText.text = "Waiting for players to join… Share the room code (up to 8 players).";
            else if (!everyoneReady) roomStatusText.text = $"{count} players here. Pick a weapon and press READY.";
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
            startButton.interactable = session.IsHost && count >= RoomState.MinPlayers && everyoneReady;
            startButtonText.text = count < RoomState.MinPlayers ? "WAITING FOR PLAYERS" : everyoneReady ? "START MATCH" : "WAITING FOR READY";
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

        /// <summary>Highlights loadout card <paramref name="card"/> and puts that weapon in the soldier's hands.</summary>
        private void ShowCard(int card)
        {
            if (loadout == null || card < 0 || card >= loadout.Length) return;
            shownCard = card;
            for (int i = 0; i < loadoutCards.Length; i++)
            {
                LoadoutCard c = loadoutCards[i];
                bool active = i == shownCard;
                if (c.frame != null) c.frame.color = active ? Selected : Unselected;
            }
            if (character != null) character.SetWeapon(loadout[card]);
        }

        /// <summary>Short stat line for a loadout card, e.g. "DMG 20 · 600 RPM · 30 RDS".</summary>
        private static string Describe(WeaponDefinition def)
        {
            string damage = def.pellets > 1 ? $"{def.damage:0}×{def.pellets}" : $"{def.DisplayDamage:0}";
            string rate = def.IsMelee ? "MELEE" : $"{def.roundsPerMinute:0} RPM";
            string ammo = def.usesAmmo ? $"{def.magazineSize} RDS" : "∞";
            return $"DMG {damage}  ·  {rate}  ·  {ammo}";
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
