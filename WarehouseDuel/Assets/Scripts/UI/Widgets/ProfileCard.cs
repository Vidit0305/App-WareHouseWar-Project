using TMPro;
using UnityEngine;
using UnityEngine.UI;
using WarehouseWar.Core;
using WarehouseWar.Profile;

namespace WarehouseWar.UI.Widgets
{
    /// <summary>
    /// Shows the local player's identity (avatar, banner, name, title, level, XP bar) and keeps it up to date.
    /// Every field is optional, so the same component drives the menu chip, the profile preview and the HUD avatar.
    /// </summary>
    public sealed class ProfileCard : MonoBehaviour
    {
        [SerializeField] private Image avatar;
        [SerializeField] private Image banner;
        [SerializeField] private TMP_Text nameText;
        [SerializeField] private TMP_Text titleText;
        [SerializeField] private TMP_Text levelText;
        [SerializeField] private TMP_Text xpText;
        [SerializeField] private RectTransform xpFill;

        private void OnEnable()
        {
            PlayerProfile.Changed += Refresh;
            GameSettings.Changed += Refresh;
            Refresh();
        }

        private void OnDisable()
        {
            PlayerProfile.Changed -= Refresh;
            GameSettings.Changed -= Refresh;
        }

        public void Refresh()
        {
            if (avatar != null)
            {
                Sprite sprite = PlayerProfile.MyAvatar;
                if (sprite != null) avatar.sprite = sprite;
            }
            if (banner != null)
            {
                Sprite sprite = PlayerProfile.MyBanner;
                if (sprite != null) banner.sprite = sprite;
            }
            if (nameText != null) nameText.text = PlayerProfile.Name;
            if (titleText != null) titleText.text = PlayerProfile.Title.ToUpperInvariant();
            if (levelText != null) levelText.text = PlayerProfile.Level.ToString();
            if (xpText != null)
            {
                xpText.text = PlayerProfile.Level >= PlayerProfile.MaxLevel
                    ? "MAX LEVEL"
                    : $"{PlayerProfile.XpIntoLevel} / {PlayerProfile.XpPerLevel} XP";
            }
            if (xpFill != null)
            {
                Vector2 max = xpFill.anchorMax;
                max.x = Mathf.Clamp(PlayerProfile.LevelProgress, 0.02f, 1f);
                xpFill.anchorMax = max;
            }
        }
    }
}
