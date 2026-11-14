using System;
using UnityEngine;

namespace WarehouseWar.Core
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

        /// <summary>Post-processing (colour grading, bloom, vignette) + anti-aliasing. Off = faster on weak phones.</summary>
        public static bool PostEffects
        {
            get => PlayerPrefs.GetInt(Prefix + "postfx", 1) == 1;
            set => SetInt("postfx", value ? 1 : 0);
        }

        /// <summary>Short vibration on button presses and when hit.</summary>
        public static bool Vibration
        {
            get => PlayerPrefs.GetInt(Prefix + "vibration", 1) == 1;
            set => SetInt("vibration", value ? 1 : 0);
        }

        public static bool InvertY
        {
            get => PlayerPrefs.GetInt(Prefix + "invertY", 0) == 1;
            set => SetInt("invertY", value ? 1 : 0);
        }

        public static ControlLayout Layout
        {
            get => (ControlLayout)PlayerPrefs.GetInt(Prefix + "layout", (int)ControlLayout.DualFire);
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

        /// <summary>Weapons the player takes into a match (equipped in the Armory).</summary>
        public const int LoadoutSize = 3;
        private const int WeaponCount = 8;
        private static readonly int[] DefaultLoadout = { 0, 3, 6 }; // rifle, pistol, knife

        /// <summary>The three equipped weapons (catalog indices), always valid and distinct.</summary>
        public static int[] Loadout
        {
            get
            {
                var result = new int[LoadoutSize];
                for (int slot = 0; slot < LoadoutSize; slot++)
                {
                    int fallback = slot == 0 ? PlayerPrefs.GetInt(Prefix + "weapon", DefaultLoadout[0]) : DefaultLoadout[slot];
                    int weapon = PlayerPrefs.GetInt(Prefix + "loadout" + slot, fallback);
                    if (weapon < 0 || weapon >= WeaponCount || System.Array.IndexOf(result, weapon, 0, slot) >= 0) weapon = -1;
                    result[slot] = weapon;
                }
                // Fill any invalid or duplicate slot with the first unused default (then any unused weapon).
                for (int slot = 0; slot < LoadoutSize; slot++)
                {
                    if (result[slot] >= 0) continue;
                    for (int w = 0; w < WeaponCount + LoadoutSize && result[slot] < 0; w++)
                    {
                        int candidate = w < LoadoutSize ? DefaultLoadout[w] : w - LoadoutSize;
                        if (System.Array.IndexOf(result, candidate) < 0) result[slot] = candidate;
                    }
                }
                return result;
            }
        }

        /// <summary>Puts <paramref name="weapon"/> in loadout <paramref name="slot"/>; if it was in another slot, the two swap.</summary>
        public static void SetLoadoutWeapon(int slot, int weapon)
        {
            if (slot < 0 || slot >= LoadoutSize || weapon < 0 || weapon >= WeaponCount) return;
            int[] current = Loadout;
            int other = System.Array.IndexOf(current, weapon);
            if (other >= 0 && other != slot) current[other] = current[slot];
            current[slot] = weapon;
            for (int i = 0; i < LoadoutSize; i++) PlayerPrefs.SetInt(Prefix + "loadout" + i, current[i]);
            PlayerPrefs.SetInt(Prefix + "weapon", current[0]);
            PlayerPrefs.Save();
        }

        /// <summary>First equipped weapon (shown on the menu soldier).</summary>
        public static int PreferredWeapon
        {
            get => Loadout[0];
            set => SetLoadoutWeapon(0, value);
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

        /// <summary>Restores audio, camera and control settings to their defaults (graphics and profile are kept).</summary>
        public static void ResetToDefaults()
        {
            foreach (string key in new[] { "music", "sfx", "look", "aim", "invertY", "layout", "buttonScale", "vibration" })
            {
                PlayerPrefs.DeleteKey(Prefix + key);
            }
            Save();
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
