using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using WarehouseDuel.Profile;
using WarehouseDuel.Weapons;

namespace WarehouseDuel.UI
{
    /// <summary>
    /// Leaderboard: standings of you and every rival you have dueled (from this device's match history),
    /// plus personal records and recent matches. Rows are cloned from templates at runtime.
    /// </summary>
    public sealed class LeaderboardScreen : MenuScreen
    {
        [SerializeField] private WeaponCatalog catalog;

        [Header("Tabs: standings, records")]
        [SerializeField] private Button[] tabButtons = new Button[2];
        [SerializeField] private Image[] tabBackgrounds = new Image[2];
        [SerializeField] private TMP_Text[] tabLabels = new TMP_Text[2];
        [SerializeField] private GameObject[] pages = new GameObject[2];

        [Header("Standings")]
        [SerializeField] private RectTransform standingsContent;
        [SerializeField] private LeaderboardRow standingTemplate;
        [SerializeField] private GameObject standingsEmpty;

        [Header("Records")]
        [SerializeField] private TMP_Text[] recordValues = new TMP_Text[6]; // best kills, best streak, current streak, win rate, favourite weapon, time played
        [SerializeField] private RectTransform recentContent;
        [SerializeField] private LeaderboardRow recentTemplate;
        [SerializeField] private GameObject recentEmpty;

        private readonly List<GameObject> spawned = new List<GameObject>();

        protected override void Awake()
        {
            base.Awake();
            for (int i = 0; i < tabButtons.Length; i++)
            {
                int index = i;
                tabButtons[i].onClick.AddListener(() => ShowTab(index, tabBackgrounds, tabLabels, pages));
            }
            standingTemplate.gameObject.SetActive(false);
            recentTemplate.gameObject.SetActive(false);
        }

        private void OnEnable()
        {
            ShowTab(0, tabBackgrounds, tabLabels, pages);
            Refresh();
        }

        private void Refresh()
        {
            foreach (GameObject go in spawned) Destroy(go);
            spawned.Clear();

            IReadOnlyList<MatchHistory.Entry> history = MatchHistory.Entries;
            ProfileCatalog profiles = ProfileCatalog.Instance;

            // ---- standings
            List<MatchHistory.Standing> standings = MatchHistory.Standings();
            standingsEmpty.SetActive(history.Count == 0);
            for (int i = 0; i < standings.Count && history.Count > 0; i++)
            {
                MatchHistory.Standing s = standings[i];
                LeaderboardRow row = Spawn(standingTemplate, standingsContent);
                row.Set(0, $"#{i + 1}");
                row.Set(1, s.isYou ? $"{s.name} <size=70%><color=#FFB020>(YOU)</color></size>" : s.name);
                row.Set(2, $"LV {s.level}");
                row.Set(3, s.played.ToString());
                row.Set(4, s.won.ToString());
                row.Set(5, s.lost.ToString());
                row.Set(6, s.KillDeath.ToString("0.00"));
                row.SetAvatar(s.isYou ? PlayerProfile.MyAvatar : profiles != null ? profiles.Avatar(s.avatar) : null);
                row.SetHighlight(s.isYou ? new Color(1f, 0.69f, 0.13f, 0.22f) : new Color(1f, 1f, 1f, i % 2 == 0 ? 0.07f : 0.03f));
                row.SetRankColor(i == 0 ? new Color(1f, 0.78f, 0.2f) : i == 1 ? new Color(0.8f, 0.84f, 0.9f) : i == 2 ? new Color(0.85f, 0.55f, 0.3f) : Color.white);
            }

            // ---- records
            int played = history.Count;
            int wins = 0;
            foreach (MatchHistory.Entry e in history) if (e.Outcome == MatchHistory.Result.Win) wins++;
            int favourite = MatchHistory.FavouriteWeapon;
            WeaponDefinition favouriteDef = favourite >= 0 && catalog != null ? catalog.Get(favourite) : null;
            TimeSpan time = TimeSpan.FromSeconds(MatchHistory.SecondsPlayed);

            SetRecord(0, MatchHistory.BestKills.ToString());
            SetRecord(1, MatchHistory.BestWinStreak.ToString());
            SetRecord(2, MatchHistory.CurrentWinStreak.ToString());
            SetRecord(3, played == 0 ? "—" : $"{100f * wins / played:0}%");
            SetRecord(4, favouriteDef != null ? favouriteDef.displayName.ToUpperInvariant() : "—");
            SetRecord(5, time.TotalHours >= 1 ? $"{(int)time.TotalHours}h {time.Minutes}m" : $"{time.Minutes}m {time.Seconds}s");

            recentEmpty.SetActive(played == 0);
            for (int i = played - 1, shown = 0; i >= 0 && shown < 8; i--, shown++)
            {
                MatchHistory.Entry e = history[i];
                LeaderboardRow row = Spawn(recentTemplate, recentContent);
                string result = e.Outcome == MatchHistory.Result.Win ? "<color=#50D773>WIN</color>"
                    : e.Outcome == MatchHistory.Result.Loss ? "<color=#EB503C>LOSS</color>" : "<color=#96A0B4>DRAW</color>";
                WeaponDefinition weapon = catalog != null ? catalog.Get(e.weapon) : null;
                row.Set(0, result);
                row.Set(1, $"vs {e.opponent}");
                row.Set(2, $"{e.kills} - {e.opponentKills}");
                row.Set(3, weapon != null ? weapon.displayName : string.Empty);
                row.Set(4, Ago(e.timestamp));
                row.SetAvatar(profiles != null ? profiles.Avatar(e.opponentAvatar) : null);
                row.SetHighlight(new Color(1f, 1f, 1f, shown % 2 == 0 ? 0.07f : 0.03f));
            }
        }

        private LeaderboardRow Spawn(LeaderboardRow template, RectTransform parent)
        {
            LeaderboardRow row = Instantiate(template, parent, false);
            row.gameObject.SetActive(true);
            spawned.Add(row.gameObject);
            return row;
        }

        private void SetRecord(int index, string value)
        {
            if (index < recordValues.Length && recordValues[index] != null) recordValues[index].text = value;
        }

        private static string Ago(long ticks)
        {
            if (ticks <= 0) return string.Empty;
            TimeSpan age = DateTime.UtcNow - new DateTime(ticks, DateTimeKind.Utc);
            if (age.TotalMinutes < 1) return "just now";
            if (age.TotalHours < 1) return $"{(int)age.TotalMinutes} min ago";
            if (age.TotalDays < 1) return $"{(int)age.TotalHours} h ago";
            return $"{(int)age.TotalDays} d ago";
        }
    }
}
