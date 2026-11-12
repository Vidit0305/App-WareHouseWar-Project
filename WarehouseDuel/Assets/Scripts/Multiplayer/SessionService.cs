using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Fusion;
using Fusion.Photon.Realtime;
using Fusion.Sockets;
using UnityEngine;
using UnityEngine.SceneManagement;
using WarehouseDuel.Core;
using WarehouseDuel.Match;
using WarehouseDuel.Player;

namespace WarehouseDuel.Multiplayer
{
    public enum SessionStatus
    {
        Offline,
        Connecting,
        InRoom,
        Leaving,
        Failed,
    }

    /// <summary>
    /// Owns the Fusion NetworkRunner for one online session and is the single place that knows about
    /// Photon: creating/joining rooms, regions, scene changes, player spawning and disconnect handling.
    /// UI talks to it through methods and events only.
    ///
    /// Host Mode: the room creator is the authoritative host. Everything that decides the match
    /// (damage, kills, timer, result) is simulated with state authority on the host. Gameplay code checks
    /// HasStateAuthority / Runner.IsServer — never "am I the host" — so a dedicated server (GameMode.Server)
    /// can replace StartAsHost later without touching gameplay.
    /// </summary>
    public sealed class SessionService : MonoBehaviour, INetworkRunnerCallbacks
    {
        [SerializeField] private NetworkObject roomStatePrefab;
        [SerializeField] private NetworkObject matchManagerPrefab;
        [SerializeField] private NetworkObject playerPrefab;

        private const int MaxCreateAttempts = 3;

        public static SessionService Instance { get; private set; }

        public SessionStatus Status { get; private set; } = SessionStatus.Offline;
        public string RoomCode { get; private set; } = string.Empty;
        public string LastError { get; private set; } = string.Empty;
        public NetworkRunner Runner { get; private set; }
        public bool IsHost => Runner != null && Runner.IsServer;
        public bool IsPractice { get; private set; }

        /// <summary>Raised whenever Status, RoomCode or LastError changes.</summary>
        public event Action StatusChanged;
        /// <summary>Raised on the remaining player when the other one leaves.</summary>
        public event Action<PlayerRef> PlayerLeft;
        public event Action<PlayerRef> PlayerJoined;

        private readonly Dictionary<PlayerRef, NetworkObject> spawnedCharacters = new Dictionary<PlayerRef, NetworkObject>();
        private bool shutdownRequestedLocally;

        /// <summary>Called by the bootstrap in each scene; keeps one instance alive across scene loads.</summary>
        public static SessionService EnsureInstance(SessionService prefab)
        {
            if (Instance != null) return Instance;
            SessionService service = Instantiate(prefab);
            service.name = "Session Service";
            return service;
        }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        // ------------------------------------------------------------------ public API (UI)

        /// <summary>Creates a private two-player room and becomes its host. Retries if the random code is taken.</summary>
        public async Task<bool> CreateRoomAsync()
        {
            if (Status == SessionStatus.Connecting || Status == SessionStatus.InRoom) return false;
            IsPractice = false;

            for (int attempt = 1; attempt <= MaxCreateAttempts; attempt++)
            {
                string sessionName = Multiplayer.RoomCode.NewSessionName();
                SetStatus(SessionStatus.Connecting, $"Creating room (attempt {attempt})…");

                StartGameResult result = await StartRunner(GameMode.Host, sessionName, region: null);
                if (result.Ok)
                {
                    string region = Runner.SessionInfo.Region;
                    RoomCode = Multiplayer.RoomCode.ToDisplay(sessionName, region);
                    SetStatus(SessionStatus.InRoom, string.Empty);
                    return true;
                }

                if (result.ShutdownReason != ShutdownReason.GameIdAlreadyExists)
                {
                    SetStatus(SessionStatus.Failed, Describe(result.ShutdownReason, result.ErrorMessage));
                    return false;
                }
            }

            SetStatus(SessionStatus.Failed, "Could not create a unique room code. Please try again.");
            return false;
        }

        /// <summary>Joins a friend's room by code. Validates the code before connecting.</summary>
        public async Task<bool> JoinRoomAsync(string code)
        {
            if (Status == SessionStatus.Connecting || Status == SessionStatus.InRoom) return false;
            IsPractice = false;

            if (!Multiplayer.RoomCode.TryParse(code, out string sessionName, out string region, out string error))
            {
                SetStatus(SessionStatus.Failed, error);
                return false;
            }

            SetStatus(SessionStatus.Connecting, "Joining room…");
            StartGameResult result = await StartRunner(GameMode.Client, sessionName, region);
            if (!result.Ok)
            {
                SetStatus(SessionStatus.Failed, Describe(result.ShutdownReason, result.ErrorMessage));
                return false;
            }

            RoomCode = Multiplayer.RoomCode.ToDisplay(sessionName, region ?? Runner.SessionInfo.Region);
            SetStatus(SessionStatus.InRoom, string.Empty);
            return true;
        }

        /// <summary>
        /// Offline practice: Fusion Single-Player mode loads the warehouse with only the local player.
        /// Same gameplay code as online; no network involved.
        /// </summary>
        public async Task<bool> StartPracticeAsync()
        {
            if (Status == SessionStatus.Connecting || Status == SessionStatus.InRoom) return false;
            IsPractice = true;
            SetStatus(SessionStatus.Connecting, "Loading practice…");
            StartGameResult result = await StartRunner(GameMode.Single, "practice", null, GameConstants.Scenes.WarehouseMap);
            if (!result.Ok)
            {
                SetStatus(SessionStatus.Failed, Describe(result.ShutdownReason, result.ErrorMessage));
                return false;
            }
            SetStatus(SessionStatus.InRoom, string.Empty);
            return true;
        }

        /// <summary>Host: moves both players from the lobby into the warehouse.</summary>
        public void StartMatch()
        {
            if (!IsHost || RoomState.Instance == null) return;
            if (RoomState.Instance.OccupiedCount < RoomState.Capacity || !RoomState.Instance.AllReady) return;
            RoomState.Instance.ResetScores();
            LoadNetworkScene(GameConstants.Scenes.WarehouseMap);
        }

        /// <summary>Host: everyone back to the lobby (characters and the match are despawned first).</summary>
        public void ReturnToLobby()
        {
            if (!IsHost) return;
            DespawnGameplayObjects();
            if (RoomState.Instance != null) RoomState.Instance.ClearReadyFlags();
            LoadNetworkScene(GameConstants.Scenes.Lobby);
        }

        /// <summary>Leaves the session (either side) and returns to the main menu.</summary>
        public async void LeaveSession()
        {
            if (Runner == null)
            {
                LoadMainMenu();
                return;
            }
            shutdownRequestedLocally = true;
            SetStatus(SessionStatus.Leaving, string.Empty);
            await Runner.Shutdown();
        }

        public double RoundTripMilliseconds
        {
            get
            {
                if (Runner == null || !Runner.IsRunning || Runner.IsServer) return 0;
                return Runner.GetPlayerRtt(Runner.LocalPlayer) * 1000.0;
            }
        }

        // ------------------------------------------------------------------ runner lifecycle

        private async Task<StartGameResult> StartRunner(GameMode mode, string sessionName, string region, string firstScene = GameConstants.Scenes.Lobby)
        {
            shutdownRequestedLocally = false;
            spawnedCharacters.Clear();

            var runnerObject = new GameObject($"Network Runner ({mode})");
            DontDestroyOnLoad(runnerObject);
            Runner = runnerObject.AddComponent<NetworkRunner>();
            Runner.ProvideInput = true;
            Runner.AddCallbacks(this);
            NetworkSceneManagerDefault sceneManager = runnerObject.AddComponent<NetworkSceneManagerDefault>();

            var sceneInfo = new NetworkSceneInfo();
            if (mode != GameMode.Client)
            {
                sceneInfo.AddSceneRef(SceneRef.FromIndex(BuildIndexOf(firstScene)), LoadSceneMode.Single);
            }

            FusionAppSettings appSettings = PhotonAppSettings.Global.AppSettings.GetCopy();
            if (!string.IsNullOrEmpty(region)) appSettings.FixedRegion = region;

            var args = new StartGameArgs
            {
                GameMode = mode,
                SessionName = sessionName,
                PlayerCount = RoomState.Capacity,
                IsVisible = false,
                IsOpen = true,
                Scene = sceneInfo,
                SceneManager = sceneManager,
                CustomPhotonAppSettings = appSettings,
            };

            StartGameResult result;
            try
            {
                result = await Runner.StartGame(args);
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                CleanupRunner();
                throw;
            }

            if (!result.Ok) CleanupRunner();
            return result;
        }

        private void LoadNetworkScene(string sceneName)
        {
            if (Runner == null || !Runner.IsServer) return;
            Runner.LoadScene(SceneRef.FromIndex(BuildIndexOf(sceneName)), LoadSceneMode.Single);
        }

        private static int BuildIndexOf(string sceneName) =>
            Array.IndexOf(GameConstants.Scenes.BuildOrder, sceneName);

        private void CleanupRunner()
        {
            if (Runner != null) Destroy(Runner.gameObject);
            Runner = null;
            spawnedCharacters.Clear();
        }

        private void SetStatus(SessionStatus status, string error)
        {
            Status = status;
            LastError = error ?? string.Empty;
            if (status == SessionStatus.Offline || status == SessionStatus.Failed) RoomCode = string.Empty;
            StatusChanged?.Invoke();
        }

        private static void LoadMainMenu()
        {
            if (SceneManager.GetActiveScene().name != GameConstants.Scenes.MainMenu)
            {
                SceneManager.LoadScene(GameConstants.Scenes.MainMenu);
            }
        }

        // ------------------------------------------------------------------ host: spawning

        private void SpawnRoomStateIfNeeded(NetworkRunner runner)
        {
            if (!runner.IsServer || RoomState.Instance != null) return;
            runner.Spawn(roomStatePrefab, Vector3.zero, Quaternion.identity);
        }

        private void SpawnGameplayObjects(NetworkRunner runner)
        {
            if (!runner.IsServer) return;
            if (MatchManager.Instance == null) runner.Spawn(matchManagerPrefab, Vector3.zero, Quaternion.identity);
            foreach (PlayerRef player in runner.ActivePlayers) SpawnCharacter(runner, player);
        }

        private void SpawnCharacter(NetworkRunner runner, PlayerRef player)
        {
            if (spawnedCharacters.ContainsKey(player)) return;
            int slot = RoomState.Instance != null ? RoomState.Instance.SlotOf(player) : 0;
            Environment.SpawnPoint spawn = Environment.SpawnPoint.ForSlot(Mathf.Max(0, slot));
            Vector3 position = spawn != null ? spawn.Position : Vector3.up;
            Quaternion rotation = spawn != null ? spawn.Rotation : Quaternion.identity;

            NetworkObject character = runner.Spawn(playerPrefab, position, rotation, player);
            runner.SetPlayerObject(player, character);
            spawnedCharacters[player] = character;
        }

        private void DespawnGameplayObjects()
        {
            if (Runner == null || !Runner.IsServer) return;
            foreach (NetworkObject character in spawnedCharacters.Values)
            {
                if (character != null) Runner.Despawn(character);
            }
            spawnedCharacters.Clear();
            if (MatchManager.Instance != null) Runner.Despawn(MatchManager.Instance.Object);
        }

        private bool InWarehouse => SceneManager.GetActiveScene().name == GameConstants.Scenes.WarehouseMap;

        // ------------------------------------------------------------------ INetworkRunnerCallbacks

        public void OnPlayerJoined(NetworkRunner runner, PlayerRef player)
        {
            if (runner.IsServer)
            {
                SpawnRoomStateIfNeeded(runner);
                // RoomState spawns this tick; register the player once it exists.
                if (RoomState.Instance != null && RoomState.Instance.AddPlayer(player) < 0)
                {
                    runner.Disconnect(player); // defensive: Photon already caps the room at 2
                    return;
                }
                if (InWarehouse) SpawnGameplayObjects(runner);
            }
            PlayerJoined?.Invoke(player);
        }

        public void OnPlayerLeft(NetworkRunner runner, PlayerRef player)
        {
            if (runner.IsServer)
            {
                if (MatchManager.Instance != null) MatchManager.Instance.HandlePlayerLeft(player);
                if (spawnedCharacters.TryGetValue(player, out NetworkObject character))
                {
                    if (character != null) runner.Despawn(character);
                    spawnedCharacters.Remove(player);
                }
                if (RoomState.Instance != null) RoomState.Instance.RemovePlayer(player);
            }
            PlayerLeft?.Invoke(player);
        }

        public void OnSceneLoadDone(NetworkRunner runner)
        {
            if (!runner.IsServer) return;
            SpawnRoomStateIfNeeded(runner);

            // Players that joined before RoomState existed (the host itself) are registered here.
            if (RoomState.Instance != null)
            {
                foreach (PlayerRef player in runner.ActivePlayers) RoomState.Instance.AddPlayer(player);
            }

            if (InWarehouse) SpawnGameplayObjects(runner);
        }

        public void OnSceneLoadStart(NetworkRunner runner) { }

        public void OnInput(NetworkRunner runner, NetworkInput input)
        {
            LocalInputCollector collector = LocalInputCollector.Instance;
            if (collector == null) return;

            InputSnapshot s = collector.ConsumeSnapshot();
            var data = new NetworkInputData
            {
                Move = s.Move,
                Yaw = s.Yaw,
                Pitch = s.Pitch,
                WeaponRequest = (byte)(s.WeaponSlot + 1),
            };
            data.Buttons.Set(InputButton.Fire, s.Fire);
            data.Buttons.Set(InputButton.Aim, s.Aim);
            data.Buttons.Set(InputButton.Sprint, s.Sprint);
            data.Buttons.Set(InputButton.Jump, s.Jump);
            data.Buttons.Set(InputButton.Reload, s.Reload);
            data.Buttons.Set(InputButton.NextWeapon, s.NextWeapon);
            input.Set(data);
        }

        public void OnInputMissing(NetworkRunner runner, PlayerRef player, NetworkInput input) { }

        public void OnShutdown(NetworkRunner runner, ShutdownReason shutdownReason)
        {
            bool expected = shutdownRequestedLocally || shutdownReason == ShutdownReason.Ok;
            Runner = null;
            spawnedCharacters.Clear();
            if (runner != null) Destroy(runner.gameObject);

            if (expected) SetStatus(SessionStatus.Offline, string.Empty);
            else SetStatus(SessionStatus.Failed, Describe(shutdownReason, null));

            LoadMainMenu();
        }

        public void OnDisconnectedFromServer(NetworkRunner runner, NetDisconnectReason reason)
        {
            if (!shutdownRequestedLocally) SetStatus(SessionStatus.Failed, "Connection to the host was lost.");
        }

        public void OnConnectFailed(NetworkRunner runner, NetAddress remoteAddress, NetConnectFailedReason reason)
        {
            SetStatus(SessionStatus.Failed, $"Could not connect to the host ({reason}).");
        }

        public void OnConnectedToServer(NetworkRunner runner) { }
        public void OnConnectRequest(NetworkRunner runner, NetworkRunnerCallbackArgs.ConnectRequest request, byte[] token) { }
        public void OnUserSimulationMessage(NetworkRunner runner, SimulationMessagePtr message) { }
        public void OnSessionListUpdated(NetworkRunner runner, List<SessionInfo> sessionList) { }
        public void OnCustomAuthenticationResponse(NetworkRunner runner, Dictionary<string, object> data) { }
        public void OnHostMigration(NetworkRunner runner, HostMigrationToken hostMigrationToken) { }
        public void OnReliableDataReceived(NetworkRunner runner, PlayerRef player, ReliableKey key, ReadOnlySpan<byte> data)
        {
            if (runner.IsServer && key.Equals(RoomState.RequestKey) && RoomState.Instance != null) RoomState.Instance.HandleRequest(player, data);
        }
        public void OnReliableDataProgress(NetworkRunner runner, PlayerRef player, ReliableKey key, float progress) { }
        public void OnObjectExitAOI(NetworkRunner runner, NetworkObject obj, PlayerRef player) { }
        public void OnObjectEnterAOI(NetworkRunner runner, NetworkObject obj, PlayerRef player) { }

        // ------------------------------------------------------------------ messages

        /// <summary>Turns Fusion shutdown reasons into short messages a player understands.</summary>
        public static string Describe(ShutdownReason reason, string detail)
        {
            switch (reason)
            {
                case ShutdownReason.Ok: return string.Empty;
                case ShutdownReason.GameNotFound: return "Room not found. Check the code — the host may have closed it.";
                case ShutdownReason.GameIsFull: return "That room is already full (2 players).";
                case ShutdownReason.GameClosed: return "That room is closed.";
                case ShutdownReason.GameIdAlreadyExists: return "Room code already in use. Try again.";
                case ShutdownReason.DisconnectedByPluginLogic: return "Disconnected by the server.";
                case ShutdownReason.ConnectionTimeout:
                case ShutdownReason.PhotonCloudTimeout: return "Connection timed out. Check your internet connection.";
                case ShutdownReason.ConnectionRefused: return "Connection refused by the host.";
                case ShutdownReason.InvalidAuthentication:
                case ShutdownReason.CustomAuthenticationFailed: return "Photon rejected the App ID. Check the Photon setup.";
                case ShutdownReason.ServerInRoom:
                case ShutdownReason.HostMigration: return "The host left the match.";
                case ShutdownReason.MaxCcuReached: return "Server is busy (player limit reached). Try again later.";
                case ShutdownReason.InvalidRegion: return "Invalid server region in the room code.";
                case ShutdownReason.UnableToMatchmake: return "Could not reach the matchmaking server. Try again.";
                default: return string.IsNullOrEmpty(detail) ? $"Disconnected ({reason})." : detail;
            }
        }
    }
}
