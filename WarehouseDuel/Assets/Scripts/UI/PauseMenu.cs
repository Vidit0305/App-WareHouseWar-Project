using UnityEngine;
using UnityEngine.UI;
using WarehouseWar.Multiplayer;

namespace WarehouseWar.UI
{
    /// <summary>
    /// In-match menu. The match keeps running (it's online), so this only blocks local input.
    /// Offers Resume, Settings and Leave Match (with confirmation).
    /// </summary>
    public sealed class PauseMenu : MonoBehaviour
    {
        [SerializeField] private Button openButton;
        [SerializeField] private GameObject panel;
        [SerializeField] private Button resumeButton;
        [SerializeField] private Button settingsButton;
        [SerializeField] private Button leaveButton;
        [SerializeField] private GameObject settingsPanel;
        [SerializeField] private Button settingsCloseButton;
        [SerializeField] private GameObject confirmPanel;
        [SerializeField] private Button confirmLeaveButton;
        [SerializeField] private Button cancelLeaveButton;

        public static bool IsOpen { get; private set; }

        private void Awake()
        {
            openButton.onClick.AddListener(() => SetOpen(true));
            resumeButton.onClick.AddListener(() => SetOpen(false));
            settingsButton.onClick.AddListener(() => settingsPanel.SetActive(true));
            settingsCloseButton.onClick.AddListener(() => settingsPanel.SetActive(false));
            leaveButton.onClick.AddListener(() => confirmPanel.SetActive(true));
            cancelLeaveButton.onClick.AddListener(() => confirmPanel.SetActive(false));
            confirmLeaveButton.onClick.AddListener(() =>
            {
                SetOpen(false);
                if (SessionService.Instance != null) SessionService.Instance.LeaveSession();
            });
            SetOpen(false);
        }

        private void OnDestroy() => IsOpen = false;

        private void Update()
        {
            var keyboard = UnityEngine.InputSystem.Keyboard.current;
            if (keyboard != null && keyboard.escapeKey.wasPressedThisFrame && IsOpen) SetOpen(false);
        }

        public void SetOpen(bool open)
        {
            IsOpen = open;
            panel.SetActive(open);
            settingsPanel.SetActive(false);
            confirmPanel.SetActive(false);
        }
    }
}
