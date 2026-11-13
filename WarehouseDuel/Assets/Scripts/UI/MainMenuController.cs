using TMPro;
using UnityEngine;
using UnityEngine.UI;
using WarehouseDuel.Audio;
using WarehouseDuel.Core;
using WarehouseDuel.Multiplayer;
using WarehouseDuel.Player;
using WarehouseDuel.UI.Widgets;

namespace WarehouseDuel.UI
{
    /// <summary>
    /// Main menu hub: the soldier stands centre stage; around him the profile chip, Armory / Profile cards,
    /// game-mode cards (Online Duel, Practice), Leaderboard and Settings, and the big PLAY button.
    /// Play Online opens Create / Join dialogs; room creation and joining go through <see cref="SessionService"/>.
    /// </summary>
    public sealed class MainMenuController : MonoBehaviour
    {
        [Header("Home")]
        [SerializeField] private GameObject homePanel;
        [SerializeField] private SoldierAnimator character;
        [SerializeField] private Button playButton;
        [SerializeField] private Button onlineModeButton;
        [SerializeField] private Button practiceModeButton;
        [SerializeField] private Button profileChipButton;
        [SerializeField] private Button profileCardButton;
        [SerializeField] private Button armoryCardButton;
        [SerializeField] private Button leaderboardButton;
        [SerializeField] private Button settingsButton;
        [SerializeField] private TMP_Text winsText;

        [Header("Screens")]
        [SerializeField] private ProfileScreen profileScreen;
        [SerializeField] private LeaderboardScreen leaderboardScreen;
        [SerializeField] private ArmoryScreen armoryScreen;
        [SerializeField] private GameObject settingsPanel;
        [SerializeField] private Button settingsCloseButton;

        [Header("Online dialogs")]
        [SerializeField] private GameObject onlinePanel;
        [SerializeField] private Button createRoomButton;
        [SerializeField] private Button joinRoomButton;
        [SerializeField] private Button onlineBackButton;
        [SerializeField] private GameObject joinPanel;
        [SerializeField] private TMP_InputField codeInput;
        [SerializeField] private Button joinConfirmButton;
        [SerializeField] private Button joinBackButton;
        [SerializeField] private TMP_Text joinErrorText;

        [Header("Busy / messages")]
        [SerializeField] private GameObject busyPanel;
        [SerializeField] private TMP_Text busyText;
        [SerializeField] private GameObject messagePanel;
        [SerializeField] private TMP_Text messageTitle;
        [SerializeField] private TMP_Text messageText;
        [SerializeField] private Button messageOkButton;

        private SessionService session;

        private void Start()
        {
            session = SessionService.Instance;
            GameSettings.ApplyEngineSettings();
            if (character != null) character.SetWeapon(GameSettings.PreferredWeapon);

            codeInput.characterLimit = 7;
            codeInput.onValueChanged.AddListener(_ => joinErrorText.text = string.Empty);
            codeInput.onSubmit.AddListener(_ => JoinRoom());

            playButton.onClick.AddListener(() => ShowDialog(onlinePanel));
            onlineModeButton.onClick.AddListener(() => ShowDialog(onlinePanel));
            practiceModeButton.onClick.AddListener(StartPractice);
            profileChipButton.onClick.AddListener(() => OpenScreen(profileScreen));
            profileCardButton.onClick.AddListener(() => OpenScreen(profileScreen));
            armoryCardButton.onClick.AddListener(() => OpenScreen(armoryScreen));
            leaderboardButton.onClick.AddListener(() => OpenScreen(leaderboardScreen));
            settingsButton.onClick.AddListener(() =>
            {
                settingsPanel.SetActive(true);
                homePanel.SetActive(false);
            });
            settingsCloseButton.onClick.AddListener(CloseSettings);

            foreach (MenuScreen screen in new MenuScreen[] { profileScreen, leaderboardScreen, armoryScreen })
            {
                screen.Closed += () => homePanel.SetActive(true);
                screen.gameObject.SetActive(false);
            }

            createRoomButton.onClick.AddListener(CreateRoom);
            joinRoomButton.onClick.AddListener(() =>
            {
                joinErrorText.text = string.Empty;
                ShowDialog(joinPanel);
            });
            onlineBackButton.onClick.AddListener(() => ShowDialog(null));
            joinConfirmButton.onClick.AddListener(JoinRoom);
            joinBackButton.onClick.AddListener(() => ShowDialog(onlinePanel));
            messageOkButton.onClick.AddListener(() => PopupFx.Hide(messagePanel));

            settingsPanel.SetActive(false);
            messagePanel.SetActive(false);
            homePanel.SetActive(true);
            onlinePanel.SetActive(false);
            joinPanel.SetActive(false);
            busyPanel.SetActive(false);
            RefreshWins();
            GameSettings.Changed += RefreshWins;

            if (session != null)
            {
                session.StatusChanged += OnSessionStatus;
                // Arrived here after a disconnect or failed join: explain why.
                if (session.Status == SessionStatus.Failed && !string.IsNullOrEmpty(session.LastError)) ShowMessage(session.LastError, "CONNECTION LOST");
            }

            AudioManager.Instance.StopAmbience();
            if (AudioManager.Library != null) AudioManager.Instance.PlayMusic(AudioManager.Library.menuMusic);
        }

        private void OnDestroy()
        {
            if (session != null) session.StatusChanged -= OnSessionStatus;
            GameSettings.Changed -= RefreshWins;
        }

        private void Update()
        {
            if (BackPressed()) HandleBack();
        }

        private static bool BackPressed()
        {
#if ENABLE_INPUT_SYSTEM
            return UnityEngine.InputSystem.Keyboard.current != null && UnityEngine.InputSystem.Keyboard.current.escapeKey.wasPressedThisFrame;
#else
            return Input.GetKeyDown(KeyCode.Escape);
#endif
        }

        /// <summary>Android back: closes the top-most dialog or screen.</summary>
        private void HandleBack()
        {
            if (messagePanel.activeSelf) PopupFx.Hide(messagePanel);
            else if (busyPanel.activeSelf) return;
            else if (joinPanel.activeSelf) ShowDialog(onlinePanel);
            else if (onlinePanel.activeSelf) ShowDialog(null);
            else if (settingsPanel.activeSelf) CloseSettings();
            else if (profileScreen.IsOpen) profileScreen.Close();
            else if (leaderboardScreen.IsOpen) leaderboardScreen.Close();
            else if (armoryScreen.IsOpen) armoryScreen.Close();
        }

        private void OpenScreen(MenuScreen screen)
        {
            homePanel.SetActive(false);
            screen.Open();
        }

        private void CloseSettings()
        {
            PopupFx.Hide(settingsPanel);
            homePanel.SetActive(true);
        }

        private void RefreshWins()
        {
            if (winsText != null) winsText.text = GameSettings.MatchesWon.ToString();
        }

        private async void CreateRoom()
        {
            if (!EnsureSession()) return;
            ShowBusy("Connecting to Photon…");
            bool ok = await session.CreateRoomAsync();
            if (!ok) ShowFailure(onlinePanel);
            // On success Fusion loads the Lobby scene for us.
        }

        private async void JoinRoom()
        {
            if (!EnsureSession()) return;
            if (!RoomCode.TryParse(codeInput.text, out _, out _, out string formatError))
            {
                joinErrorText.text = formatError;
                return;
            }

            ShowBusy("Joining room…");
            bool ok = await session.JoinRoomAsync(codeInput.text);
            if (!ok)
            {
                ShowDialog(joinPanel);
                joinErrorText.text = session.LastError;
            }
        }

        private async void StartPractice()
        {
            if (!EnsureSession()) return;
            ShowBusy("Loading the warehouse…");
            bool ok = await session.StartPracticeAsync();
            if (!ok) ShowFailure(null);
        }

        private bool EnsureSession()
        {
            session = SessionService.Instance;
            if (session != null) return true;
            ShowMessage("Networking is not initialised (SessionService missing).", "SOMETHING WENT WRONG");
            return false;
        }

        private void OnSessionStatus()
        {
            if (session.Status == SessionStatus.Connecting && busyPanel.activeSelf && !string.IsNullOrEmpty(session.LastError))
            {
                busyText.text = session.LastError;
            }
        }

        private void ShowBusy(string text)
        {
            busyText.text = text;
            ShowDialog(busyPanel);
        }

        private void ShowFailure(GameObject returnTo)
        {
            ShowDialog(returnTo);
            ShowMessage(string.IsNullOrEmpty(session.LastError) ? "Something went wrong. Please try again." : session.LastError, "CONNECTION PROBLEM");
        }

        private void ShowMessage(string text, string title = "NOTICE")
        {
            if (messageTitle != null) messageTitle.text = title;
            messageText.text = text;
            messagePanel.SetActive(true);
        }

        /// <summary>Shows one of the online dialogs over the home screen (null = none).</summary>
        private void ShowDialog(GameObject dialog)
        {
            foreach (GameObject panel in new[] { onlinePanel, joinPanel, busyPanel })
            {
                if (panel == dialog) panel.SetActive(true);
                else PopupFx.Hide(panel);
            }
        }
    }
}
