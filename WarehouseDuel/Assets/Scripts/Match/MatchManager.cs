using System;
using Fusion;
using UnityEngine;
using WarehouseDuel.Multiplayer;
using WarehouseDuel.Player;

namespace WarehouseDuel.Match
{
    public enum MatchPhase : byte
    {
        WaitingForPlayers = 0,
        Countdown = 1,
        Playing = 2,
        Ended = 3,
    }

    public enum MatchEndReason : byte
    {
        None = 0,
        TimeUp = 1,
        OpponentLeft = 2,
    }

    /// <summary>
    /// Host-authoritative match flow: WaitingForPlayers → Countdown (3 s) → Playing (180 s) → Ended.
    /// The clock is a networked TickTimer, so both devices show the same remaining time.
    /// The result (winner / draw / forfeit) is computed once by the host and replicated, so both players
    /// always see the same outcome.
    /// </summary>
    public sealed class MatchManager : NetworkBehaviour
    {
        [SerializeField] private MatchRules rules;

        [Networked] public MatchPhase Phase { get; private set; }
        [Networked] public TickTimer PhaseTimer { get; private set; }
        [Networked] public int MatchNumber { get; private set; }
        [Networked] public MatchEndReason EndReason { get; private set; }
        [Networked] public PlayerRef Winner { get; private set; }
        [Networked] public NetworkBool IsDraw { get; private set; }
        /// <summary>Seconds actually played (180 unless the match ended early).</summary>
        [Networked] public float PlayedSeconds { get; private set; }
        [Networked] private int PlayingStartTick { get; set; }

        public static MatchManager Instance { get; private set; }

        /// <summary>Fired on every peer when the phase changes (old, new).</summary>
        public static event Action<MatchPhase, MatchPhase> PhaseChanged;

        public static bool GameplayAllowed => Instance != null && Instance.Phase == MatchPhase.Playing;
        public static bool RespawnsAllowed => Instance != null && Instance.Phase == MatchPhase.Playing;

        public MatchRules Rules => rules;

        public float RemainingSeconds => PhaseTimer.IsRunning ? Mathf.Max(0f, PhaseTimer.RemainingTime(Runner) ?? 0f) : 0f;

        private MatchPhase renderedPhase;

        public override void Spawned()
        {
            Instance = this;
            renderedPhase = Phase;
            PhaseChanged?.Invoke(Phase, Phase);
        }

        public override void Despawned(NetworkRunner runner, bool hasState)
        {
            if (Instance == this) Instance = null;
        }

        public override void FixedUpdateNetwork()
        {
            if (!HasStateAuthority) return;

            switch (Phase)
            {
                case MatchPhase.WaitingForPlayers:
                    if (PlayersReadyToStart()) BeginCountdown();
                    break;

                case MatchPhase.Countdown:
                    if (PhaseTimer.Expired(Runner))
                    {
                        Phase = MatchPhase.Playing;
                        PhaseTimer = TickTimer.CreateFromSeconds(Runner, rules.matchDurationSeconds);
                        PlayingStartTick = Runner.Tick.Raw;
                    }
                    break;

                case MatchPhase.Playing:
                    if (PhaseTimer.Expired(Runner)) EndMatch(MatchEndReason.TimeUp, PlayerRef.None);
                    break;

                case MatchPhase.Ended:
                    RoomState room = RoomState.Instance;
                    if (room != null && room.OccupiedCount == RequiredPlayers && room.BothWantRematch) Restart();
                    break;
            }
        }

        public override void Render()
        {
            if (Phase == renderedPhase) return;
            MatchPhase previous = renderedPhase;
            renderedPhase = Phase;
            PhaseChanged?.Invoke(previous, Phase);
        }

        // ------------------------------------------------------------------ host API

        /// <summary>Host: a player left. During a match the remaining player wins by forfeit.</summary>
        public void HandlePlayerLeft(PlayerRef leaver)
        {
            if (!HasStateAuthority) return;
            if (Phase == MatchPhase.Playing || Phase == MatchPhase.Countdown)
            {
                PlayerRef remaining = PlayerRef.None;
                RoomState room = RoomState.Instance;
                if (room != null)
                {
                    for (int i = 0; i < RoomState.Capacity; i++)
                    {
                        if (room.Slots[i].Occupied && room.Slots[i].Player != leaver) remaining = room.Slots[i].Player;
                    }
                }
                EndMatch(MatchEndReason.OpponentLeft, remaining);
            }
            else if (Phase == MatchPhase.WaitingForPlayers)
            {
                PhaseTimer = TickTimer.None;
            }
        }

        /// <summary>Two players online; one in offline practice (Fusion single-player mode).</summary>
        public int RequiredPlayers => Runner != null && Runner.GameMode == GameMode.Single ? 1 : RoomState.Capacity;

        private bool PlayersReadyToStart()
        {
            RoomState room = RoomState.Instance;
            if (room == null || room.OccupiedCount < RequiredPlayers) return false;

            int characters = 0;
            foreach (PlayerRef player in Runner.ActivePlayers)
            {
                if (Runner.TryGetPlayerObject(player, out NetworkObject obj) && obj != null && obj.GetComponent<PlayerCharacter>() != null) characters++;
            }
            return characters >= RequiredPlayers;
        }

        private void BeginCountdown()
        {
            MatchNumber++;
            EndReason = MatchEndReason.None;
            Winner = PlayerRef.None;
            IsDraw = false;
            PlayedSeconds = 0f;
            Phase = MatchPhase.Countdown;
            PhaseTimer = TickTimer.CreateFromSeconds(Runner, rules.countdownSeconds);

            foreach (PlayerRef player in Runner.ActivePlayers)
            {
                if (Runner.TryGetPlayerObject(player, out NetworkObject obj) && obj.TryGetComponent(out PlayerCharacter character))
                {
                    character.Respawn(withProtection: false);
                }
            }
        }

        private void EndMatch(MatchEndReason reason, PlayerRef forcedWinner)
        {
            PlayedSeconds = Phase == MatchPhase.Playing
                ? Mathf.Min(rules.matchDurationSeconds, (Runner.Tick.Raw - PlayingStartTick) * Runner.DeltaTime)
                : 0f;
            Phase = MatchPhase.Ended;
            PhaseTimer = TickTimer.None;
            EndReason = reason;

            if (reason == MatchEndReason.OpponentLeft)
            {
                Winner = forcedWinner;
                IsDraw = false;
                return;
            }

            RoomState room = RoomState.Instance;
            PlayerSlot a = room.Slots[0], b = room.Slots[1];
            if (a.Kills == b.Kills)
            {
                IsDraw = true;
                Winner = PlayerRef.None;
            }
            else
            {
                IsDraw = false;
                Winner = a.Kills > b.Kills ? a.Player : b.Player;
            }
        }

        private void Restart()
        {
            RoomState.Instance.ResetScores();
            BeginCountdown();
        }
    }
}
