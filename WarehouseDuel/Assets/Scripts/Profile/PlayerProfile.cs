using System;
using System.IO;
using UnityEngine;
using WarehouseWar.Core;

namespace WarehouseWar.Profile
{
    /// <summary>
    /// The local player's identity: name, avatar (preset or a photo stored on this device), banner, title,
    /// and level/XP derived from lifetime stats. Saved locally; the preset indices and level are shared with
    /// the opponent through the room (photos never leave the device).
    /// </summary>
    public static class PlayerProfile
    {
        public static event Action Changed;

        private const string Prefix = "wd.profile.";
        public const int XpPerLevel = 300;
        public const int MaxLevel = 99;

        public sealed class TitleInfo
        {
            public string name;
            public string requirement;
            public Func<bool> unlocked;
        }

        /// <summary>Titles shown under the name. Unlocked by lifetime stats and records.</summary>
        public static readonly TitleInfo[] Titles =
        {
            new TitleInfo { name = "Rookie", requirement = "Unlocked from the start", unlocked = () => true },
            new TitleInfo { name = "Gunslinger", requirement = "Get 10 eliminations", unlocked = () => GameSettings.TotalKills >= 10 },
            new TitleInfo { name = "Warrior", requirement = "Win 5 online matches", unlocked = () => GameSettings.MatchesWon >= 5 },
            new TitleInfo { name = "Veteran", requirement = "Play 25 online matches", unlocked = () => GameSettings.MatchesPlayed >= 25 },
            new TitleInfo { name = "Sharpshooter", requirement = "Get 50 eliminations", unlocked = () => GameSettings.TotalKills >= 50 },
            new TitleInfo { name = "Untouchable", requirement = "Win a match without dying", unlocked = () => MatchHistory.HasFlawlessWin },
            new TitleInfo { name = "On Fire", requirement = "Win 3 matches in a row", unlocked = () => MatchHistory.BestWinStreak >= 3 },
            new TitleInfo { name = "Warehouse Boss", requirement = "Win 25 online matches", unlocked = () => GameSettings.MatchesWon >= 25 },
        };

        // ------------------------------------------------------------------ identity

        public static string Name
        {
            get => GameSettings.PlayerName;
            set
            {
                GameSettings.PlayerName = value;
                Changed?.Invoke();
            }
        }

        public static int AvatarIndex
        {
            get => Owned(Shop.Category.Avatar, PlayerPrefs.GetInt(Prefix + "avatar", 0));
            set
            {
                PlayerPrefs.SetInt(Prefix + "avatar", Mathf.Max(0, value));
                PlayerPrefs.SetInt(Prefix + "custom", 0);
                Save();
            }
        }

        /// <summary>Chosen playable character (see <see cref="CharacterCatalog"/>).</summary>
        public static int CharacterIndex
        {
            get => Owned(Shop.Category.Costume, PlayerPrefs.GetInt(Prefix + "character", 0));
            set
            {
                PlayerPrefs.SetInt(Prefix + "character", Mathf.Max(0, value));
                Save();
            }
        }

        public static int BannerIndex
        {
            get => Owned(Shop.Category.Banner, PlayerPrefs.GetInt(Prefix + "banner", 0));
            set
            {
                PlayerPrefs.SetInt(Prefix + "banner", Mathf.Max(0, value));
                Save();
            }
        }

        public static int TitleIndex
        {
            get
            {
                int index = Mathf.Clamp(PlayerPrefs.GetInt(Prefix + "title", 0), 0, Titles.Length - 1);
                return Titles[index].unlocked() ? index : 0;
            }
            set
            {
                if (value < 0 || value >= Titles.Length || !Titles[value].unlocked()) return;
                PlayerPrefs.SetInt(Prefix + "title", value);
                Save();
            }
        }

        public static string Title => Titles[TitleIndex].name;

        /// <summary>The saved choice if the player owns it, otherwise the first (free) item.</summary>
        private static int Owned(Shop.Category category, int index) => Shop.IsOwned(category, Mathf.Max(0, index)) ? Mathf.Max(0, index) : 0;

        // ------------------------------------------------------------------ avatar

        private static Sprite customSprite;
        private static bool customLoaded;

        private static string CustomAvatarPath => Path.Combine(Application.persistentDataPath, "avatar_custom.png");

        public static bool HasCustomAvatar => CustomAvatar != null;

        /// <summary>True when the photo (not a preset) is the active avatar.</summary>
        public static bool UsingCustomAvatar
        {
            get => PlayerPrefs.GetInt(Prefix + "custom", 0) == 1 && HasCustomAvatar;
            set
            {
                PlayerPrefs.SetInt(Prefix + "custom", value && HasCustomAvatar ? 1 : 0);
                Save();
            }
        }

        public static Sprite CustomAvatar
        {
            get
            {
                if (customLoaded) return customSprite;
                customLoaded = true;
                try
                {
                    if (File.Exists(CustomAvatarPath))
                    {
                        var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                        if (texture.LoadImage(File.ReadAllBytes(CustomAvatarPath))) customSprite = ToSprite(texture);
                    }
                }
                catch (Exception e)
                {
                    Debug.LogWarning($"[Profile] Could not load the avatar photo: {e.Message}");
                }
                return customSprite;
            }
        }

        /// <summary>Stores a square photo as the avatar and makes it active.</summary>
        public static void SetCustomAvatar(Texture2D square)
        {
            if (square == null) return;
            try
            {
                File.WriteAllBytes(CustomAvatarPath, square.EncodeToPNG());
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[Profile] Could not save the avatar photo: {e.Message}");
            }
            customSprite = ToSprite(square);
            customLoaded = true;
            PlayerPrefs.SetInt(Prefix + "custom", 1);
            Save();
        }

        /// <summary>The avatar to show for the local player (photo or preset).</summary>
        public static Sprite MyAvatar
        {
            get
            {
                if (UsingCustomAvatar) return CustomAvatar;
                return ProfileCatalog.Instance != null ? ProfileCatalog.Instance.Avatar(AvatarIndex) : null;
            }
        }

        public static Sprite MyBanner => ProfileCatalog.Instance != null ? ProfileCatalog.Instance.Banner(BannerIndex) : null;

        // ------------------------------------------------------------------ level

        public static int Xp => GameSettings.TotalKills * 10 + GameSettings.MatchesWon * 50 + GameSettings.MatchesPlayed * 20;
        public static int Level => Mathf.Min(MaxLevel, 1 + Xp / XpPerLevel);
        public static int XpIntoLevel => Level >= MaxLevel ? XpPerLevel : Xp % XpPerLevel;
        public static float LevelProgress => XpIntoLevel / (float)XpPerLevel;

        /// <summary>Raise <see cref="Changed"/> after stats change (e.g. a match was recorded).</summary>
        public static void NotifyChanged() => Changed?.Invoke();

        private static Sprite ToSprite(Texture2D texture)
        {
            texture.wrapMode = TextureWrapMode.Clamp;
            texture.filterMode = FilterMode.Bilinear;
            return Sprite.Create(texture, new Rect(0f, 0f, texture.width, texture.height), new Vector2(0.5f, 0.5f), 100f);
        }

        private static void Save()
        {
            PlayerPrefs.Save();
            Changed?.Invoke();
        }
    }
}
