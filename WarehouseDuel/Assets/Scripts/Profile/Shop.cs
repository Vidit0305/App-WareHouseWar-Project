using System;
using UnityEngine;

namespace WarehouseDuel.Profile
{
    /// <summary>
    /// Stars (the in-game currency) and what the player owns. Stars are earned in online matches and spent in the
    /// Shop on costumes (characters), avatars and banners. A few of each are free. Saved locally (PlayerPrefs).
    /// </summary>
    public static class Shop
    {
        public enum Category { Costume, Avatar, Banner }

        public const int StarsPerMatch = 5;
        public const int StarsPerKill = 2;
        public const int StarsPerWin = 15;

        private const string Prefix = "wd.shop.";

        public static event Action Changed;

        public static int Stars => Mathf.Max(0, PlayerPrefs.GetInt(Prefix + "stars", 0));

        public static void AddStars(int amount)
        {
            if (amount <= 0) return;
            PlayerPrefs.SetInt(Prefix + "stars", Stars + amount);
            Save();
        }

        /// <summary>Stars for one finished online match; returns how many were awarded.</summary>
        public static int AwardMatch(bool won, int kills)
        {
            int amount = StarsPerMatch + Mathf.Max(0, kills) * StarsPerKill + (won ? StarsPerWin : 0);
            AddStars(amount);
            return amount;
        }

        // ------------------------------------------------------------------ catalogue rules

        public static bool IsFree(Category category, int index)
        {
            switch (category)
            {
                case Category.Costume: return index < 2;
                case Category.Avatar: return index < 4;
                default: return index < 4;
            }
        }

        private static readonly int[] CostumePrices = { 0, 0, 150, 120, 140, 100, 180, 160, 200, 250 };

        public static int Price(Category category, int index)
        {
            if (IsFree(category, index)) return 0;
            switch (category)
            {
                case Category.Costume: return index < CostumePrices.Length ? CostumePrices[index] : 200;
                case Category.Avatar: return 40 + 5 * Mathf.Max(0, index - 4);
                default: return index < 8 ? 50 : 80 + 5 * (index - 8);
            }
        }

        public static bool IsOwned(Category category, int index) =>
            index >= 0 && (IsFree(category, index) || PlayerPrefs.GetInt(Key(category, index), 0) == 1);

        public static bool CanAfford(Category category, int index) => Stars >= Price(category, index);

        /// <summary>Spends stars and unlocks the item. Returns false if already owned or not affordable.</summary>
        public static bool Buy(Category category, int index)
        {
            if (IsOwned(category, index) || !CanAfford(category, index)) return false;
            PlayerPrefs.SetInt(Prefix + "stars", Stars - Price(category, index));
            PlayerPrefs.SetInt(Key(category, index), 1);
            Save();
            return true;
        }

        private static string Key(Category category, int index) => $"{Prefix}owned.{category}.{index}";

        private static void Save()
        {
            PlayerPrefs.Save();
            Changed?.Invoke();
        }
    }
}
