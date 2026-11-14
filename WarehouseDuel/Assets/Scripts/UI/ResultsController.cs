using System.Collections.Generic;
using Fusion;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using WarehouseDuel.Audio;
using WarehouseDuel.Core;
using WarehouseDuel.Match;
using WarehouseDuel.Multiplayer;
using WarehouseDuel.Profile;
using WarehouseDuel.Weapons;

namespace WarehouseDuel.UI
{
    /// <summary>
    /// End-of-match screen for free-for-all: the host-decided result (victory / defeat / draw / forfeit), a ranking of
    /// every player (eliminations, deaths, weapon), match duration and the stars you earned. Play Again needs every
    /// remaining player's vote; Return to Lobby moves everyone back; Main Menu leaves the session.
    /// </summary>
    public sealed class ResultsController : MonoBehaviour
    {
        [System.Serializable]
        public sealed class Row
        {
            public Image background;
            public TMP_Text rank;
            public TMP_Text name;
            public TMP_Text kills;
            public TMP_Text deaths;
            public TMP_Text weapon;
        }

        [SerializeField] private WeaponCatalog catalog;
        [SerializeField] private GameObject panel;
        [SerializeField] private TMP_Text titleText;
        [SerializeField] private TMP_Text subtitleText;
        [SerializeField] private Row[] rows = new Row[0];
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
        private int starsEarned;

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

            // Ranking: most eliminations first, then fewest deaths.
            var ranking = new List<PlayerSlot>();
            for (int i = 0; i < RoomState.Capacity; i++) if (room.Slots[i].Occupied) ranking.Add(room.Slots[i]);
            ranking.Sort((x, y) => x.Kills != y.Kills ? y.Kills.CompareTo(x.Kills) : x.Deaths.CompareTo(y.Deaths));
            room.TryGetSlot(local, out PlayerSlot mine);
            int myRank = ranking.FindIndex(s => s.Player == local) + 1;

            string title, subtitle;
            Color band;
            AudioClip sting = null;
            AudioLibrary lib = AudioManager.Library;
            bool won = false;

            if (practice)
            {
                title = "PRACTICE COMPLETE";
                subtitle = "Ready for the real thing? Press PLAY to find opponents online.";
                band = DrawColor;
                sting = lib != null ? lib.draw : null;
            }
            else if (match.EndReason == MatchEndReason.OpponentLeft)
            {
                won = match.Winner == local;
                title = won ? "VICTORY" : "MATCH ABANDONED";
                subtitle = won ? "Everyone else left the match." : "The match ended early.";
                band = won ? VictoryColor : DrawColor;
                sting = lib != null ? (won ? lib.victory : lib.draw) : null;
            }
            else if (match.IsDraw)
            {
                title = "DRAW";
                subtitle = $"Tied for first place with {ranking[0].Kills} elimination{(ranking.Count > 0 && ranking[0].Kills == 1 ? "" : "s")}.";
                band = DrawColor;
                sting = lib != null ? lib.draw : null;
            }
            else
            {
                won = match.Winner == local;
                title = won ? "VICTORY" : "DEFEAT";
                subtitle = won ? "You topped the leaderboard!" : $"{room.NameOf(match.Winner)} wins the match.  You finished #{myRank}.";
                band = won ? VictoryColor : DefeatColor;
                sting = lib != null ? (won ? lib.victory : lib.defeat) : null;
            }

            titleText.text = title;
            subtitleText.text = subtitle;
            titleBand.color = band;

            for (int i = 0; i < rows.Length; i++)
            {
                Row row = rows[i];
                bool used = i < ranking.Count;
                row.background.gameObject.SetActive(used);
                if (!used) continue;
                PlayerSlot slot = ranking[i];
                bool isMe = slot.Player == local;
                row.rank.text = $"#{i + 1}";
                row.rank.color = i == 0 ? VictoryColor : Color.white;
                row.name.text = isMe ? $"{slot.Name} <size=70%><color=#FFB020>(YOU)</color></size>" : slot.Name.ToString();
                row.kills.text = slot.Kills.ToString();
                row.deaths.text = slot.Deaths.ToString();
                WeaponDefinition weapon = catalog.Get(slot.Weapon);
                row.weapon.text = weapon != null ? weapon.displayName : string.Empty;
                row.background.color = isMe ? new Color(1f, 0.69f, 0.13f, 0.22f) : new Color(1f, 1f, 1f, i % 2 == 0 ? 0.07f : 0.03f);
            }

            if (firstTime)
            {
                if (sting != null) AudioManager.Instance.Play2D(sting);
                starsEarned = practice ? 0 : RecordStats(won, mine, ranking, local, match);
            }

            int seconds = Mathf.RoundToInt(match.PlayedSeconds);
            detailsText.text = $"Match duration  <b>{seconds / 60}:{seconds % 60:00}</b>     Players  <b>{ranking.Count}</b>" +
                               (starsEarned > 0 ? $"     <color=#FFB020>+{starsEarned} STARS</color>" : string.Empty);
        }

        private void RefreshButtons(RoomState room)
        {
            int present = room.OccupiedCount;
            bool practice = SessionService.Instance != null && SessionService.Instance.IsPractice;
            bool enough = practice || present >= RoomState.MinPlayers;
            playAgainButton.interactable = !votedRematch && enough;
            lobbyButton.gameObject.SetActive(!practice);
            lobbyButton.interactable = present > 0;

            if (!enough) playAgainText.text = "PLAYERS LEFT";
            else if (votedRematch) playAgainText.text = "WAITING FOR OTHERS…";
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

        /// <summary>Lifetime stats, match history (best other player as the "opponent") and stars. Returns stars earned.</summary>
        private static int RecordStats(bool won, PlayerSlot mine, List<PlayerSlot> ranking, PlayerRef local, MatchManager match)
        {
            GameSettings.MatchesPlayed++;
            if (won) GameSettings.MatchesWon++;
            GameSettings.TotalKills += mine.Kills;
            GameSettings.TotalDeaths += mine.Deaths;

            PlayerSlot rival = default;
            foreach (PlayerSlot slot in ranking)
            {
                if (slot.Player == local) continue;
                rival = slot;
                break;
            }

            MatchHistory.Record(new MatchHistory.Entry
            {
                opponent = rival.Occupied ? rival.Name.ToString() : "Unknown",
                opponentAvatar = rival.Avatar,
                opponentBanner = rival.Banner,
                opponentLevel = Mathf.Max(1, (int)rival.Level),
                kills = mine.Kills,
                deaths = mine.Deaths,
                opponentKills = rival.Kills,
                opponentDeaths = rival.Deaths,
                result = (int)(match.IsDraw ? MatchHistory.Result.Draw : won ? MatchHistory.Result.Win : MatchHistory.Result.Loss),
                weapon = mine.Weapon,
                seconds = match.PlayedSeconds,
            });
            int stars = Shop.AwardMatch(won, mine.Kills);
            PlayerProfile.NotifyChanged();
            return stars;
        }
    }
}
