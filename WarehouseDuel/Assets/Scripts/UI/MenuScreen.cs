using System;
using UnityEngine;
using UnityEngine.UI;

namespace WarehouseDuel.UI
{
    /// <summary>A full-screen menu page (profile, leaderboard, armory, settings) with a back button.</summary>
    public abstract class MenuScreen : MonoBehaviour
    {
        [SerializeField] private Button backButton;

        public event Action Closed;

        public bool IsOpen => gameObject.activeSelf;

        protected virtual void Awake()
        {
            if (backButton != null) backButton.onClick.AddListener(Close);
        }

        public virtual void Open() => gameObject.SetActive(true);

        public virtual void Close()
        {
            if (!gameObject.activeSelf) return;
            Widgets.PopupFx.Hide(gameObject);
            Closed?.Invoke();
        }

        /// <summary>Highlights the selected tab and shows its page.</summary>
        public static void ShowTab(int index, Image[] tabBackgrounds, TMPro.TMP_Text[] tabLabels, GameObject[] pages)
        {
            for (int i = 0; i < pages.Length; i++)
            {
                bool on = i == index;
                if (pages[i] != null) pages[i].SetActive(on);
                if (tabBackgrounds != null && i < tabBackgrounds.Length && tabBackgrounds[i] != null)
                    tabBackgrounds[i].color = on ? new Color(1f, 0.69f, 0.13f, 1f) : new Color(1f, 1f, 1f, 0.08f);
                if (tabLabels != null && i < tabLabels.Length && tabLabels[i] != null)
                    tabLabels[i].color = on ? new Color(0.15f, 0.12f, 0.05f, 1f) : new Color(0.94f, 0.95f, 0.96f, 1f);
            }
        }
    }
}
