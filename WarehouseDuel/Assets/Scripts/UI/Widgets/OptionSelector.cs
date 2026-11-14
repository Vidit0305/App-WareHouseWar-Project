using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace WarehouseWar.UI.Widgets
{
    /// <summary>
    /// "◀ value ▶" selector — a touch-friendly replacement for dropdowns (quality level, frame-rate cap, layout).
    /// </summary>
    public sealed class OptionSelector : MonoBehaviour
    {
        [SerializeField] private Button previous;
        [SerializeField] private Button next;
        [SerializeField] private TMP_Text label;

        private string[] options = Array.Empty<string>();

        public int Index { get; private set; }
        public event Action<int> Changed;

        private void Awake()
        {
            if (previous != null) previous.onClick.AddListener(() => Step(-1));
            if (next != null) next.onClick.AddListener(() => Step(1));
        }

        public void SetOptions(string[] values, int selected)
        {
            options = values ?? Array.Empty<string>();
            Index = Mathf.Clamp(selected, 0, Mathf.Max(0, options.Length - 1));
            Refresh();
        }

        private void Step(int direction)
        {
            if (options.Length == 0) return;
            Index = (Index + direction + options.Length) % options.Length;
            Refresh();
            Changed?.Invoke(Index);
        }

        private void Refresh()
        {
            if (label != null) label.text = options.Length > 0 ? options[Index] : "-";
        }
    }
}
