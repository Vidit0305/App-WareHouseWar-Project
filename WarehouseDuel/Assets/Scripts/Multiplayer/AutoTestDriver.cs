using System;
using System.IO;
using System.Text;
using Fusion;
using UnityEngine;
using UnityEngine.SceneManagement;
using WarehouseDuel.Core;
using WarehouseDuel.Match;
using WarehouseDuel.Player;

namespace WarehouseDuel.Multiplayer
{
    /// <summary>
    /// Automated two-instance multiplayer test (only active when launched with -autotest).
    /// Runs the real game flow over Photon: the host creates a room and writes the code to a shared folder,
    /// the client reads it and joins, both ready up, the host starts the match, and bot input fights it out.
    /// At the end each instance writes what IT saw (winner, kills, deaths) so the two views can be compared.
    ///
    ///   WarehouseDuel.exe -autotest host   -autotest-dir C:\tmp\wd -autotest-seconds 45 -batchmode? (needs rendering off: use -nographics)
    ///   WarehouseDuel.exe -autotest client -autotest-dir C:\tmp\wd
    /// </summary>
    public sealed class AutoTestDriver : MonoBehaviour
    {
        private enum Role { None, Host, Client }

        private Role role;
        private string directory;
        private float matchSeconds = 45f;
        private float startedAt;
        private bool requestedSession, sentReady, startedMatch, wroteResult, shortenedMatch;
        private float nextLog;
        private float strafeSign = 1f, strafeFlipAt;
        private readonly StringBuilder log = new StringBuilder();

        private const float TimeoutSeconds = 360f;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Boot()
        {
            string[] args = System.Environment.GetCommandLineArgs();
            int index = Array.IndexOf(args, "-autotest");
            if (index < 0 || index + 1 >= args.Length) return;

            AudioListener.volume = 0f; // automated tests run silently
            var go = new GameObject("AutoTest Driver");
            DontDestroyOnLoad(go);
            AutoTestDriver driver = go.AddComponent<AutoTestDriver>();
            driver.role = args[index + 1].Equals("host", StringComparison.OrdinalIgnoreCase) ? Role.Host : Role.Client;
            driver.directory = Arg(args, "-autotest-dir") ?? Path.Combine(Application.temporaryCachePath, "autotest");
            if (float.TryParse(Arg(args, "-autotest-seconds"), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float seconds)) driver.matchSeconds = seconds;
            Directory.CreateDirectory(driver.directory);
            GameSettings.NameOverride = driver.role == Role.Host ? "HostBot" : "ClientBot";
            driver.Log($"AutoTest started as {driver.role}, dir={driver.directory}, matchSeconds={driver.matchSeconds}");
        }

        private static string Arg(string[] args, string name)
        {
            int i = Array.IndexOf(args, name);
            return i >= 0 && i + 1 < args.Length ? args[i + 1] : null;
        }

        private void Start() => startedAt = Time.realtimeSinceStartup;

        private void Update()
        {
            if (Time.realtimeSinceStartup - startedAt > TimeoutSeconds)
            {
                Finish("TIMEOUT", success: false);
                return;
            }

            SessionService session = SessionService.Instance;
            string scene = SceneManager.GetActiveScene().name;

            if (scene == GameConstants.Scenes.MainMenu && session != null && !requestedSession) RequestSession(session);
            if (scene == GameConstants.Scenes.Lobby) DriveLobby(session);
            if (scene == GameConstants.Scenes.WarehouseMap) DriveMatch();

            if (session != null && session.Status == SessionStatus.Failed && requestedSession && !wroteResult)
            {
                Finish($"SESSION FAILED: {session.LastError}", success: false);
            }
        }

        private async void RequestSession(SessionService session)
        {
            requestedSession = true;
            string codeFile = Path.Combine(directory, "room.txt");
            if (role == Role.Host)
            {
                if (File.Exists(codeFile)) File.Delete(codeFile);
                bool ok = await session.CreateRoomAsync();
                Log(ok ? $"Room created: {session.RoomCode}" : $"Create failed: {session.LastError}");
                if (ok) File.WriteAllText(codeFile, session.RoomCode);
            }
            else
            {
                float waitUntil = Time.realtimeSinceStartup + 90f;
                while (!File.Exists(codeFile) && Time.realtimeSinceStartup < waitUntil) await System.Threading.Tasks.Task.Delay(500);
                if (!File.Exists(codeFile))
                {
                    Finish("No room code from host", success: false);
                    return;
                }
                string code = File.ReadAllText(codeFile).Trim();
                bool ok = await session.JoinRoomAsync(code);
                Log(ok ? $"Joined room {code}" : $"Join failed: {session.LastError}");
            }
        }

        private void DriveLobby(SessionService session)
        {
            RoomState room = RoomState.Instance;
            if (room == null || room.Object == null || session == null || session.Runner == null) return;
            PlayerRef me = session.Runner.LocalPlayer;
            if (room.SlotOf(me) < 0) return;

            if (!sentReady && Time.realtimeSinceStartup > startedAt + 2f)
            {
                sentReady = true;
                room.RequestProfile(GameSettings.PlayerName, (byte)(role == Role.Host ? 0 : 1)); // rifle vs SMG
                room.RequestReady(true);
                Log("Sent profile + ready");
            }

            if (role == Role.Host && !startedMatch && room.OccupiedCount == RoomState.Capacity && room.AllReady)
            {
                startedMatch = true;
                Log("Both ready: starting match");
                session.StartMatch();
            }
        }

        private void DriveMatch()
        {
            MatchManager match = MatchManager.Instance;
            if (match != null && !shortenedMatch && role == Role.Host && match.Phase != MatchPhase.Playing)
            {
                shortenedMatch = true;
                match.Rules.matchDurationSeconds = matchSeconds;
            }

            PlayerCharacter me = PlayerCharacter.Local;
            LocalInputCollector input = LocalInputCollector.Instance;
            if (me == null || input == null || match == null) return;

            if (match.Phase == MatchPhase.Ended)
            {
                input.StopSynthetic();
                if (!wroteResult) WriteResult(match);
                return;
            }

            // Find the opponent and fight: aim at their chest, close distance, strafe, fire on sight.
            PlayerCharacter opponent = null;
            foreach (PlayerCharacter candidate in FindObjectsByType<PlayerCharacter>(FindObjectsSortMode.None))
            {
                if (candidate != me) opponent = candidate;
            }

            if (Time.time > strafeFlipAt)
            {
                strafeSign = -strafeSign;
                strafeFlipAt = Time.time + UnityEngine.Random.Range(0.6f, 1.6f);
            }

            float yaw = input.Yaw, pitch = 0f;
            bool fire = false;
            Vector2 move = Vector2.zero;
            if (opponent != null && opponent.IsAlive)
            {
                Vector3 target = opponent.transform.position + Vector3.up * 1.15f;
                Vector3 toTarget = target - me.EyePosition;
                yaw = Mathf.Atan2(toTarget.x, toTarget.z) * Mathf.Rad2Deg;
                pitch = -Mathf.Atan2(toTarget.y, new Vector2(toTarget.x, toTarget.z).magnitude) * Mathf.Rad2Deg;
                bool visible = !Physics.Linecast(me.EyePosition, target, LayerMask.GetMask(GameConstants.Layers.Environment));
                fire = visible && toTarget.magnitude < 40f;
                move = new Vector2(strafeSign * 0.7f, visible && toTarget.magnitude < 8f ? -0.3f : 1f);
            }
            input.SetSynthetic(move, yaw, pitch, fire, UnityEngine.Random.value < 0.005f);

            if (Time.time > nextLog)
            {
                nextLog = Time.time + 5f;
                RoomState room = RoomState.Instance;
                string score = room != null ? $"{room.Slots[0].Kills}-{room.Slots[1].Kills}" : "?";
                Log($"t={match.RemainingSeconds:0}s phase={match.Phase} hp={me.Health:0} alive={me.IsAlive} score={score} opponent={(opponent != null ? "seen" : "none")}");
            }
        }

        private void WriteResult(MatchManager match)
        {
            RoomState room = RoomState.Instance;
            var sb = new StringBuilder();
            sb.AppendLine("{");
            sb.AppendLine($"  \"role\": \"{role}\",");
            sb.AppendLine($"  \"matchNumber\": {match.MatchNumber},");
            sb.AppendLine($"  \"endReason\": \"{match.EndReason}\",");
            sb.AppendLine($"  \"isDraw\": {(match.IsDraw ? "true" : "false")},");
            sb.AppendLine($"  \"winner\": \"{(room != null ? room.NameOf(match.Winner) : "?")}\",");
            sb.AppendLine($"  \"playedSeconds\": {match.PlayedSeconds:0.0},");
            for (int i = 0; room != null && i < RoomState.Capacity; i++)
            {
                PlayerSlot s = room.Slots[i];
                sb.AppendLine($"  \"slot{i}\": {{ \"name\": \"{s.Name}\", \"kills\": {s.Kills}, \"deaths\": {s.Deaths}, \"weapon\": {s.Weapon} }}{(i == 0 ? "," : "")}");
            }
            sb.AppendLine("}");
            File.WriteAllText(Path.Combine(directory, $"result_{role.ToString().ToLowerInvariant()}.json"), sb.ToString());
            Finish("Match ended; result written", success: true);
        }

        private void Finish(string reason, bool success)
        {
            if (wroteResult) return;
            wroteResult = true;
            Log($"FINISH ({(success ? "ok" : "failed")}): {reason}");
            File.WriteAllText(Path.Combine(directory, $"log_{role.ToString().ToLowerInvariant()}.txt"), log.ToString());
            Invoke(nameof(Quit), 4f);
        }

        private void Quit()
        {
            if (SessionService.Instance != null) SessionService.Instance.LeaveSession();
            Application.Quit(wroteResult ? 0 : 1);
        }

        private void Log(string message)
        {
            string line = $"[{DateTime.Now:HH:mm:ss}] [AutoTest {role}] {message}";
            log.AppendLine(line);
            Debug.Log(line);
        }
    }
}
