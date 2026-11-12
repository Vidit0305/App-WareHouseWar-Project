using TMPro;
using UnityEngine;
using UnityEngine.UI;
using WarehouseDuel.Audio;
using WarehouseDuel.Core;
using WarehouseDuel.Multiplayer;

namespace WarehouseDuel.UI
{
    /// <summary>
    /// Main menu: player name, Play Online (Create / Join room), offline Practice, Settings and Profile.
    /// Room creation and joining go through <see cref="SessionService"/>; on success Fusion loads the lobby.
    /// </summary>
    public sealed class MainMenuController : MonoBehaviour
    {
        [Header("Panels")]
        [SerializeField] private GameObject homePanel;
        [SerializeField] private GameObject onlinePanel;
        [SerializeField] private GameObject joinPanel;
        [SerializeField] private GameObject busyPanel;
        [SerializeField] private GameObject settingsPanel;
        [SerializeField] private GameObject profilePanel;
        [SerializeField] private GameObject messagePanel;

        [Header("Home")]
        [SerializeField] private TMP_InputField nameInput;
        [SerializeField] private Button playOnlineButton;
        [SerializeField] private Button practiceButton;
        [SerializeField] private Button settingsButton;
        [SerializeField] private Button profileButton;

        [Header("Online")]
        [SerializeField] private Button createRoomButton;
        [SerializeField] private Button joinRoomButton;
        [SerializeField] private Button onlineBackButton;

        [Header("Join")]
        [SerializeField] private TMP_InputField codeInput;
        [SerializeField] private Button joinConfirmButton;
        [SerializeField] private Button joinBackButton;
        [SerializeField] private TMP_Text joinErrorText;

        [Header("Busy / messages")]
        [SerializeField] private TMP_Text busyText;
        [SerializeField] private RectTransform busySpinner;
        [SerializeField] private TMP_Text messageText;
        [SerializeField] private Button messageOkButton;

        [Header("Close buttons")]
        [SerializeField] private Button settingsCloseButton;
        [SerializeField] private Button profileCloseButton;

        private SessionService session;

        private void Start()
        {
            session = SessionService.Instance;
            GameSettings.ApplyEngineSettings();

            nameInput.characterLimit = GameSettings.MaxNameLength;
            nameInput.text = GameSettings.PlayerName;
            nameInput.onEndEdit.AddListener(OnNameEdited);

            codeInput.characterLimit = 7;
            codeInput.onValueChanged.AddListener(_ => joinErrorText.text = string.Empty);
            codeInput.onSubmit.AddListener(_ => JoinRoom());

            playOnlineButton.onClick.AddListener(() => Show(onlinePanel));
            practiceButton.onClick.AddListener(StartPractice);
            settingsButton.onClick.AddListener(() => settingsPanel.SetActive(true));
            profileButton.onClick.AddListener(() => profilePanel.SetActive(true));
            settingsCloseButton.onClick.AddListener(() => settingsPanel.SetActive(false));
            profileCloseButton.onClick.AddListener(() => profilePanel.SetActive(false));

            createRoomButton.onClick.AddListener(CreateRoom);
            joinRoomButton.onClick.AddListener(() =>
            {
                joinErrorText.text = string.Empty;
                Show(joinPanel);
            });
            onlineBackButton.onClick.AddListener(() => Show(homePanel));
            joinConfirmButton.onClick.AddListener(JoinRoom);
            joinBackButton.onClick.AddListener(() => Show(onlinePanel));
            messageOkButton.onClick.AddListener(() => messagePanel.SetActive(false));

            settingsPanel.SetActive(false);
            profilePanel.SetActive(false);
            messagePanel.SetActive(false);
            Show(homePanel);

            if (session != null)
            {
                session.StatusChanged += OnSessionStatus;
                // Arrived here after a disconnect or failed join: explain why.
                if (session.Status == SessionStatus.Failed && !string.IsNullOrEmpty(session.LastError)) ShowMessage(session.LastError);
            }

            AudioManager.Instance.StopAmbience();
            if (AudioManager.Library != null) AudioManager.Instance.PlayMusic(AudioManager.Library.menuMusic);
        }

        private void OnDestroy()
        {
            if (session != null) session.StatusChanged -= OnSessionStatus;
        }

        private void Update()
        {
            if (busyPanel.activeSelf && busySpinner != null) busySpinner.Rotate(0f, 0f, -300f * Time.unscaledDeltaTime);
        }

        private void OnNameEdited(string value)
        {
            GameSettings.PlayerName = value;
            nameInput.text = GameSettings.PlayerName; // show the sanitised version
        }

        private async void CreateRoom()
        {
            if (!EnsureSession()) return;
            OnNameEdited(nameInput.text);
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

            OnNameEdited(nameInput.text);
            ShowBusy("Joining room…");
            bool ok = await session.JoinRoomAsync(codeInput.text);
            if (!ok)
            {
                Show(joinPanel);
                joinErrorText.text = session.LastError;
            }
        }

        private async void StartPractice()
        {
            if (!EnsureSession()) return;
            ShowBusy("Loading the warehouse…");
            bool ok = await session.StartPracticeAsync();
            if (!ok) ShowFailure(homePanel);
        }

        private bool EnsureSession()
        {
            session = SessionService.Instance;
            if (session != null) return true;
            ShowMessage("Networking is not initialised (SessionService missing).");
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
            Show(busyPanel);
        }

        private void ShowFailure(GameObject returnTo)
        {
            Show(returnTo);
            ShowMessage(string.IsNullOrEmpty(session.LastError) ? "Something went wrong. Please try again." : session.LastError);
        }

        private void ShowMessage(string text)
        {
            messageText.text = text;
            messagePanel.SetActive(true);
        }

        private void Show(GameObject panel)
        {
            homePanel.SetActive(panel == homePanel);
            onlinePanel.SetActive(panel == onlinePanel);
            joinPanel.SetActive(panel == joinPanel);
            busyPanel.SetActive(panel == busyPanel);
        }
    }
}
