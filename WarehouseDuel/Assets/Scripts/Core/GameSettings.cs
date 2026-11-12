using System;
using UnityEngine;

namespace WarehouseDuel.Core
{
    public enum ControlLayout
    {
        /// <summary>Joystick left, fire/aim/actions right.</summary>
        Standard = 0,
        /// <summary>Fire button duplicated on the left side, above the joystick (two-thumb shooting).</summary>
        DualFire = 1,
        /// <summary>Mirrored: joystick right, look + actions left.</summary>
        LeftHanded = 2,
    }

    /// <summary>
    /// Locally saved player preferences (PlayerPrefs). Every setter saves immediately and raises
    /// <see cref="Changed"/> so open screens, audio and the camera react without polling.
    /// </summary>
    public static class GameSettings
    {
        public static event Action Changed;

        private const string Prefix = "wd.settings.";

        public const float MinSensitivity = 0.2f, MaxSensitivity = 3f;
        public const int MaxNameLength = 14;

        public static float MusicVolume
        {
            get => PlayerPrefs.GetFloat(Prefix + "music", 0.55f);
            set => SetFloat("music", Mathf.Clamp01(value));
        }

        public static float SfxVolume
        {
            get => PlayerPrefs.GetFloat(Prefix + "sfx", 0.85f);
            set => SetFloat("sfx", Mathf.Clamp01(value));
        }

        /// <summary>Camera sensitivity multiplier (1 = default).</summary>
        public static float LookSensitivity
        {
            get => PlayerPrefs.GetFloat(Prefix + "look", 1f);
            set => SetFloat("look", Mathf.Clamp(value, MinSensitivity, MaxSensitivity));
        }

        /// <summary>Sensitivity multiplier applied on top of <see cref="LookSensitivity"/> while aiming.</summary>
        public static float AimSensitivity
        {
            get => PlayerPrefs.GetFloat(Prefix + "aim", 0.6f);
            set => SetFloat("aim", Mathf.Clamp(value, MinSensitivity, MaxSensitivity));
        }

        public static bool InvertY
        {
            get => PlayerPrefs.GetInt(Prefix + "invertY", 0) == 1;
            set => SetInt("invertY", value ? 1 : 0);
        }

        public static ControlLayout Layout
        {
            get => (ControlLayout)PlayerPrefs.GetInt(Prefix + "layout", (int)ControlLayout.Standard);
            set => SetInt("layout", (int)value);
        }

        /// <summary>Touch button size multiplier.</summary>
        public static float ButtonScale
        {
            get => PlayerPrefs.GetFloat(Prefix + "buttonScale", 1f);
            set => SetFloat("buttonScale", Mathf.Clamp(value, 0.75f, 1.35f));
        }

        /// <summary>Index into QualitySettings.names. Defaults to the project's current level.</summary>
        public static int QualityLevel
        {
            get => Mathf.Clamp(PlayerPrefs.GetInt(Prefix + "quality", QualitySettings.GetQualityLevel()), 0, QualitySettings.names.Length - 1);
            set
            {
                int level = Mathf.Clamp(value, 0, QualitySettings.names.Length - 1);
                QualitySettings.SetQualityLevel(level, true);
                SetInt("quality", level);
            }
        }

        public static int TargetFrameRate
        {
            get => PlayerPrefs.GetInt(GameConstants.PrefKeys.TargetFrameRate, GameConstants.Defaults.TargetFrameRate);
            set
            {
                int fps = value >= 60 ? 60 : 30;
                PlayerPrefs.SetInt(GameConstants.PrefKeys.TargetFrameRate, fps);
                AppBootstrap.ApplyTargetFrameRate(fps);
                Save();
            }
        }

        /// <summary>Optional per-process name (automated tests run two instances that share PlayerPrefs).</summary>
        public static string NameOverride { get; set; }

        public static string PlayerName
        {
            get
            {
                if (!string.IsNullOrEmpty(NameOverride)) return NameOverride;
                string saved = PlayerPrefs.GetString(GameConstants.PrefKeys.PlayerName, string.Empty);
                if (!string.IsNullOrWhiteSpace(saved)) return saved;
                string generated = $"Player{UnityEngine.Random.Range(1000, 9999)}";
                PlayerPrefs.SetString(GameConstants.PrefKeys.PlayerName, generated);
                return generated;
            }
            set
            {
                string clean = SanitizeName(value);
                if (clean.Length == 0) return;
                PlayerPrefs.SetString(GameConstants.PrefKeys.PlayerName, clean);
                Save();
            }
        }

        /// <summary>Preferred weapon slot (0-3) pre-selected in the lobby.</summary>
        public static int PreferredWeapon
        {
            get => PlayerPrefs.GetInt(Prefix + "weapon", 0);
            set => SetInt("weapon", Mathf.Clamp(value, 0, 3));
        }

        /// <summary>Lifetime stats shown on the profile screen (local only).</summary>
        public static int MatchesPlayed { get => PlayerPrefs.GetInt(Prefix + "stat.matches", 0); set => SetInt("stat.matches", value); }
        public static int MatchesWon { get => PlayerPrefs.GetInt(Prefix + "stat.wins", 0); set => SetInt("stat.wins", value); }
        public static int TotalKills { get => PlayerPrefs.GetInt(Prefix + "stat.kills", 0); set => SetInt("stat.kills", value); }
        public static int TotalDeaths { get => PlayerPrefs.GetInt(Prefix + "stat.deaths", 0); set => SetInt("stat.deaths", value); }

        /// <summary>Keeps letters, digits, space, '_' and '-'; trims; enforces the length limit.</summary>
        public static string SanitizeName(string raw)
        {
            if (string.IsNullOrEmpty(raw)) return string.Empty;
            var chars = new System.Text.StringBuilder(MaxNameLength);
            foreach (char c in raw.Trim())
            {
                if (chars.Length >= MaxNameLength) break;
                if (char.IsLetterOrDigit(c) || c == ' ' || c == '_' || c == '-') chars.Append(c);
            }
            return chars.ToString().Trim();
        }

        /// <summary>Re-applies stored values that live in engine state (quality level, frame rate).</summary>
        public static void ApplyEngineSettings()
        {
            QualitySettings.SetQualityLevel(QualityLevel, true);
            AppBootstrap.ApplyTargetFrameRate(TargetFrameRate);
        }

        private static void SetFloat(string key, float value)
        {
            PlayerPrefs.SetFloat(Prefix + key, value);
            Save();
        }

        private static void SetInt(string key, int value)
        {
            PlayerPrefs.SetInt(Prefix + key, value);
            Save();
        }

        private static void Save()
        {
            PlayerPrefs.Save();
            Changed?.Invoke();
        }
    }
}
