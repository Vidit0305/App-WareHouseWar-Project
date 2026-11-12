using Fusion;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using WarehouseDuel.Audio;
using WarehouseDuel.Core;
using WarehouseDuel.Match;
using WarehouseDuel.Multiplayer;
using WarehouseDuel.Weapons;

namespace WarehouseDuel.UI
{
    /// <summary>
    /// End-of-match screen. Shows the host-decided result (victory / defeat / draw / forfeit), both players'
    /// kills, match duration, selected weapons and total eliminations. Play Again needs both players' votes;
    /// Return to Lobby moves both players back; Main Menu leaves the session.
    /// </summary>
    public sealed class ResultsController : MonoBehaviour
    {
        [SerializeField] private WeaponCatalog catalog;
        [SerializeField] private GameObject panel;
        [SerializeField] private TMP_Text titleText;
        [SerializeField] private TMP_Text subtitleText;
        [SerializeField] private TMP_Text leftNameText;
        [SerializeField] private TMP_Text leftKillsText;
        [SerializeField] private TMP_Text leftWeaponText;
        [SerializeField] private TMP_Text leftTagText;
        [SerializeField] private TMP_Text rightNameText;
        [SerializeField] private TMP_Text rightKillsText;
        [SerializeField] private TMP_Text rightWeaponText;
        [SerializeField] private TMP_Text rightTagText;
        [SerializeField] private TMP_Text detailsText;
        [SerializeField] private Image titleBand;
        [SerializeField] private Button playAgainButton;
        [SerializeField] private TMP_Text playAgainText;
        [SerializeField] private Button lobbyButton;
        [SerializeField] private Button menuButton;

        private static readonly Color VictoryColor = new Color(1f, 0.69f, 0.13f);
        private static readonly Color DefeatColor = new Color(0.85f, 0.25f, 0.2f);
        private static readonly Color DrawColor = new Color(0.45f, 0.55f, 0.7f);

        private int shownForMatch = -1;
        private bool votedRematch;

        private void Awake()
        {
            playAgainButton.onClick.AddListener(VoteRematch);
            lobbyButton.onClick.AddListener(ReturnToLobby);
            menuButton.onClick.AddListener(() => SessionService.Instance?.LeaveSession());
            panel.SetActive(false);
        }

        private void Update()
        {
            MatchManager match = MatchManager.Instance;
            RoomState room = RoomState.Instance;
            bool ended = match != null && match.Object != null && match.Phase == MatchPhase.Ended && room != null;

            if (!ended)
            {
                if (panel.activeSelf) panel.SetActive(false);
                votedRematch = false;
                return;
            }

            if (!panel.activeSelf || shownForMatch != match.MatchNumber)
            {
                panel.SetActive(true);
                Show(match, room);
            }

            RefreshButtons(room);
        }

        private void Show(MatchManager match, RoomState room)
        {
            bool firstTime = shownForMatch != match.MatchNumber;
            shownForMatch = match.MatchNumber;
            SessionService session = SessionService.Instance;
            PlayerRef local = session != null && session.Runner != null ? session.Runner.LocalPlayer : PlayerRef.None;
            bool practice = session != null && session.IsPractice;

            PlayerSlot a = room.Slots[0], b = room.Slots[1];
            PlayerSlot mine = a.Player == local ? a : b;
            PlayerSlot theirs = a.Player == local ? b : a;

            string title, subtitle;
            Color band;
            AudioClip sting = null;
            AudioLibrary lib = AudioManager.Library;
            bool won = false;

            if (practice)
            {
                title = "PRACTICE COMPLETE";
                subtitle = "Ready for the real thing? Create a room and invite a friend.";
                band = DrawColor;
                sting = lib != null ? lib.draw : null;
            }
            else if (match.EndReason == MatchEndReason.OpponentLeft)
            {
                won = match.Winner == local;
                title = won ? "VICTORY" : "MATCH ABANDONED";
                subtitle = won ? "Your opponent left the match." : "The match ended early.";
                band = won ? VictoryColor : DrawColor;
                sting = lib != null ? (won ? lib.victory : lib.draw) : null;
            }
            else if (match.IsDraw)
            {
                title = "DRAW";
                subtitle = $"Both players scored {mine.Kills} elimination{(mine.Kills == 1 ? "" : "s")}.";
                band = DrawColor;
                sting = lib != null ? lib.draw : null;
            }
            else
            {
                won = match.Winner == local;
                string winnerName = room.NameOf(match.Winner);
                title = won ? "VICTORY" : "DEFEAT";
                subtitle = $"{winnerName} wins the match!";
                band = won ? VictoryColor : DefeatColor;
                sting = lib != null ? (won ? lib.victory : lib.defeat) : null;
            }

            titleText.text = title;
            subtitleText.text = subtitle;
            titleBand.color = band;

            // Left column = winner (or you on a draw), right column = the other player.
            bool mineFirst = match.IsDraw || practice || match.Winner == local || match.Winner == PlayerRef.None;
            PlayerSlot left = mineFirst ? mine : theirs;
            PlayerSlot right = mineFirst ? theirs : mine;
            FillColumn(left, local, leftNameText, leftKillsText, leftWeaponText, leftTagText, match);
            FillColumn(right, local, rightNameText, rightKillsText, rightWeaponText, rightTagText, match);

            int seconds = Mathf.RoundToInt(match.PlayedSeconds);
            detailsText.text = $"Match duration  <b>{seconds / 60}:{seconds % 60:00}</b>     Total eliminations  <b>{a.Kills + b.Kills}</b>";

            if (firstTime)
            {
                if (sting != null) AudioManager.Instance.Play2D(sting);
                if (!practice) RecordStats(won, mine);
            }
        }

        private void FillColumn(PlayerSlot slot, PlayerRef local, TMP_Text nameText, TMP_Text killsText, TMP_Text weaponText, TMP_Text tagText, MatchManager match)
        {
            if (!slot.Occupied)
            {
                nameText.text = "—";
                killsText.text = "-";
                weaponText.text = string.Empty;
                tagText.text = match.EndReason == MatchEndReason.OpponentLeft ? "LEFT" : string.Empty;
                return;
            }
            nameText.text = slot.Player == local ? $"{slot.Name} (you)" : slot.Name.ToString();
            killsText.text = slot.Kills.ToString();
            WeaponDefinition weapon = catalog.Get(slot.Weapon);
            weaponText.text = weapon != null ? weapon.displayName : string.Empty;
            tagText.text = match.IsDraw ? "DRAW" : slot.Player == match.Winner ? "WINNER" : "";
        }

        private void RefreshButtons(RoomState room)
        {
            int present = room.OccupiedCount;
            bool practice = SessionService.Instance != null && SessionService.Instance.IsPractice;
            playAgainButton.interactable = !votedRematch && (practice || present == RoomState.Capacity);
            lobbyButton.gameObject.SetActive(!practice);
            lobbyButton.interactable = present > 0;

            if (!practice && present < RoomState.Capacity) playAgainText.text = "OPPONENT LEFT";
            else if (votedRematch) playAgainText.text = "WAITING FOR OPPONENT…";
            else playAgainText.text = "PLAY AGAIN";
        }

        private void VoteRematch()
        {
            RoomState room = RoomState.Instance;
            if (room == null || room.Object == null) return;
            votedRematch = true;
            room.RequestRematch(true);
        }

        private void ReturnToLobby()
        {
            SessionService session = SessionService.Instance;
            if (session == null) return;
            if (session.IsHost) session.ReturnToLobby();
            else if (RoomState.Instance != null) RoomState.Instance.RequestLobby();
        }

        private static void RecordStats(bool won, PlayerSlot mine)
        {
            GameSettings.MatchesPlayed++;
            if (won) GameSettings.MatchesWon++;
            GameSettings.TotalKills += mine.Kills;
            GameSettings.TotalDeaths += mine.Deaths;
        }
    }
}
