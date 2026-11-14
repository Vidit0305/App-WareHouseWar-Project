using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace WarehouseWar.Profile
{
    /// <summary>
    /// Online matches played on this device (JSON in persistentDataPath, newest last, capped).
    /// Feeds the leaderboard (rival standings), personal records and recent matches.
    /// </summary>
    public static class MatchHistory
    {
        public enum Result { Loss = 0, Win = 1, Draw = 2 }

        [Serializable]
        public sealed class Entry
        {
            public string opponent;
            public int opponentAvatar;
            public int opponentBanner;
            public int opponentLevel;
            public int kills, deaths;
            public int opponentKills, opponentDeaths;
            public int result;
            public int weapon;
            public float seconds;
            public long timestamp;

            public Result Outcome => (Result)result;
        }

        /// <summary>One row of the leaderboard: a player and their record in duels involving you.</summary>
        public sealed class Standing
        {
            public string name;
            public bool isYou;
            public int avatar, banner, level;
            public int played, won, lost, kills, deaths;
            public float WinRate => played == 0 ? 0f : (float)won / played;
            public float KillDeath => deaths == 0 ? kills : (float)kills / deaths;
        }

        [Serializable]
        private sealed class Store { public List<Entry> entries = new List<Entry>(); }

        private const int MaxEntries = 300;
        private static Store store;

        public static event Action Changed;

        private static string FilePath => Path.Combine(Application.persistentDataPath, "match_history.json");

        public static IReadOnlyList<Entry> Entries
        {
            get
            {
                Load();
                return store.entries;
            }
        }

        public static void Record(Entry entry)
        {
            Load();
            entry.timestamp = DateTime.UtcNow.Ticks;
            store.entries.Add(entry);
            if (store.entries.Count > MaxEntries) store.entries.RemoveRange(0, store.entries.Count - MaxEntries);
            try
            {
                File.WriteAllText(FilePath, JsonUtility.ToJson(store));
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[MatchHistory] Could not save: {e.Message}");
            }
            Changed?.Invoke();
        }

        private static void Load()
        {
            if (store != null) return;
            store = new Store();
            try
            {
                if (File.Exists(FilePath)) store = JsonUtility.FromJson<Store>(File.ReadAllText(FilePath)) ?? new Store();
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[MatchHistory] Could not read history, starting fresh: {e.Message}");
                store = new Store();
            }
            if (store.entries == null) store.entries = new List<Entry>();
        }

        // ------------------------------------------------------------------ leaderboard

        /// <summary>You plus everyone you have dueled, ranked by wins, then win rate, then K/D.</summary>
        public static List<Standing> Standings()
        {
            var you = new Standing
            {
                name = PlayerProfile.Name,
                isYou = true,
                avatar = PlayerProfile.AvatarIndex,
                banner = PlayerProfile.BannerIndex,
                level = PlayerProfile.Level,
            };
            var rivals = new Dictionary<string, Standing>(StringComparer.OrdinalIgnoreCase);

            foreach (Entry e in Entries)
            {
                you.played++;
                you.kills += e.kills;
                you.deaths += e.deaths;
                if (e.Outcome == Result.Win) you.won++;
                else if (e.Outcome == Result.Loss) you.lost++;

                string key = string.IsNullOrWhiteSpace(e.opponent) ? "Unknown" : e.opponent;
                if (!rivals.TryGetValue(key, out Standing rival))
                {
                    rival = new Standing { name = key };
                    rivals[key] = rival;
                }
                rival.avatar = e.opponentAvatar;
                rival.banner = e.opponentBanner;
                rival.level = Mathf.Max(1, e.opponentLevel);
                rival.played++;
                rival.kills += e.opponentKills;
                rival.deaths += e.opponentDeaths;
                if (e.Outcome == Result.Loss) rival.won++;
                else if (e.Outcome == Result.Win) rival.lost++;
            }

            var list = new List<Standing>(rivals.Values) { you };
            list.Sort((a, b) =>
            {
                int c = b.won.CompareTo(a.won);
                if (c != 0) return c;
                c = b.WinRate.CompareTo(a.WinRate);
                if (c != 0) return c;
                c = b.KillDeath.CompareTo(a.KillDeath);
                if (c != 0) return c;
                return a.isYou ? -1 : b.isYou ? 1 : string.Compare(a.name, b.name, StringComparison.OrdinalIgnoreCase);
            });
            return list;
        }

        // ------------------------------------------------------------------ records

        public static int BestKills
        {
            get
            {
                int best = 0;
                foreach (Entry e in Entries) best = Mathf.Max(best, e.kills);
                return best;
            }
        }

        public static int BestWinStreak
        {
            get
            {
                int best = 0, run = 0;
                foreach (Entry e in Entries)
                {
                    run = e.Outcome == Result.Win ? run + 1 : 0;
                    best = Mathf.Max(best, run);
                }
                return best;
            }
        }

        public static int CurrentWinStreak
        {
            get
            {
                int run = 0;
                IReadOnlyList<Entry> list = Entries;
                for (int i = list.Count - 1; i >= 0 && list[i].Outcome == Result.Win; i--) run++;
                return run;
            }
        }

        /// <summary>A win where you never died.</summary>
        public static bool HasFlawlessWin
        {
            get
            {
                foreach (Entry e in Entries) if (e.Outcome == Result.Win && e.deaths == 0 && e.kills > 0) return true;
                return false;
            }
        }

        public static float SecondsPlayed
        {
            get
            {
                float total = 0f;
                foreach (Entry e in Entries) total += e.seconds;
                return total;
            }
        }

        /// <summary>Most used weapon slot, or -1 without history.</summary>
        public static int FavouriteWeapon
        {
            get
            {
                var counts = new int[8];
                foreach (Entry e in Entries) if (e.weapon >= 0 && e.weapon < counts.Length) counts[e.weapon]++;
                int best = -1, bestCount = 0;
                for (int i = 0; i < counts.Length; i++)
                {
                    if (counts[i] > bestCount) { best = i; bestCount = counts[i]; }
                }
                return best;
            }
        }
    }
}
