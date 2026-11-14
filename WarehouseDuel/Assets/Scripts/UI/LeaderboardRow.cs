using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace WarehouseWar.UI
{
    /// <summary>One table row (template cloned by <see cref="LeaderboardScreen"/>): text cells, an avatar and a background.</summary>
    public sealed class LeaderboardRow : MonoBehaviour
    {
        [SerializeField] private TMP_Text[] cells = new TMP_Text[0];
        [SerializeField] private Image avatar;
        [SerializeField] private Image background;

        public void Set(int cell, string text)
        {
            if (cell >= 0 && cell < cells.Length && cells[cell] != null) cells[cell].text = text;
        }

        public void SetAvatar(Sprite sprite)
        {
            if (avatar == null) return;
            avatar.sprite = sprite;
            avatar.enabled = sprite != null;
        }

        public void SetHighlight(Color color)
        {
            if (background != null) background.color = color;
        }

        public void SetRankColor(Color color)
        {
            if (cells.Length > 0 && cells[0] != null) cells[0].color = color;
        }
    }
}
