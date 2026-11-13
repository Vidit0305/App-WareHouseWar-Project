using System;
using System.Text;
using Fusion;
using Fusion.Sockets;
using UnityEngine;
using WarehouseDuel.Core;

namespace WarehouseDuel.Multiplayer
{
    /// <summary>One player's entry in the room. Slot 0 = host (spawn A), slot 1 = guest (spawn B).</summary>
    public struct PlayerSlot : INetworkStruct
    {
        public PlayerRef Player;
        public NetworkString<_16> Name;
        public byte Weapon;
        /// <summary>Preset avatar / banner / title index and level, for the opponent's player card.</summary>
        public byte Avatar;
        public byte Banner;
        public byte Title;
        public byte Level;
        public byte Character;
        public NetworkBool Occupied;
        public NetworkBool Ready;
        public NetworkBool WantsRematch;
        public short Kills;
        public short Deaths;
    }

    /// <summary>
    /// Host-owned record of who is in the room. Survives scene changes (lobby ↔ warehouse).
    /// Clients never write it directly: they ask the host through RPCs, and the host validates every request.
    /// Kills/deaths are only changed by <see cref="RegisterKill"/>, which the host calls exactly once per death.
    /// </summary>
    public sealed class RoomState : NetworkBehaviour
    {
        public const int Capacity = 2;

        [Networked, Capacity(Capacity)] public NetworkArray<PlayerSlot> Slots => default;
        /// <summary>Incremented on every change so UIs can refresh cheaply.</summary>
        [Networked] public int Revision { get; private set; }

        public static RoomState Instance { get; private set; }

        /// <summary>Raised on every peer when the room data changes (names, ready flags, scores...).</summary>
        public static event Action Changed;

        private ChangeDetector changes;

        public override void Spawned()
        {
            Instance = this;
            Runner.MakeDontDestroyOnLoad(gameObject);
            changes = GetChangeDetector(ChangeDetector.Source.SimulationState);
            Changed?.Invoke();
        }

        public override void Despawned(NetworkRunner runner, bool hasState)
        {
            if (Instance == this) Instance = null;
            Changed?.Invoke();
        }

        public override void Render()
        {
            foreach (string _ in changes.DetectChanges(this))
            {
                Changed?.Invoke();
                break;
            }
        }

        // ------------------------------------------------------------------ queries (all peers)

        public int OccupiedCount
        {
            get
            {
                int count = 0;
                for (int i = 0; i < Capacity; i++) if (Slots[i].Occupied) count++;
                return count;
            }
        }

        public int SlotOf(PlayerRef player)
        {
            for (int i = 0; i < Capacity; i++)
            {
                if (Slots[i].Occupied && Slots[i].Player == player) return i;
            }
            return -1;
        }

        public bool TryGetSlot(PlayerRef player, out PlayerSlot slot)
        {
            int index = SlotOf(player);
            slot = index >= 0 ? Slots[index] : default;
            return index >= 0;
        }

        public string NameOf(PlayerRef player) => TryGetSlot(player, out PlayerSlot slot) ? slot.Name.ToString() : "Player";

        public bool AllReady
        {
            get
            {
                for (int i = 0; i < Capacity; i++)
                {
                    if (!Slots[i].Occupied || !Slots[i].Ready) return false;
                }
                return true;
            }
        }

        // ------------------------------------------------------------------ host-only mutation

        /// <summary>Host: adds a player to the first free slot. Returns the slot, or -1 if the room is full.</summary>
        public int AddPlayer(PlayerRef player)
        {
            if (!HasStateAuthority) return -1;
            int existing = SlotOf(player);
            if (existing >= 0) return existing;

            for (int i = 0; i < Capacity; i++)
            {
                if (Slots[i].Occupied) continue;
                Slots.Set(i, new PlayerSlot
                {
                    Player = player,
                    Name = $"Player {i + 1}",
                    Occupied = true,
                });
                Revision++;
                return i;
            }
            return -1;
        }

        public void RemovePlayer(PlayerRef player)
        {
            if (!HasStateAuthority) return;
            int index = SlotOf(player);
            if (index < 0) return;
            Slots.Set(index, default);
            // The remaining player must confirm again before the next match.
            for (int i = 0; i < Capacity; i++) Modify(i, (ref PlayerSlot s) => { s.Ready = false; s.WantsRematch = false; });
            Revision++;
        }

        /// <summary>Host: credits a kill. Called once per death by the victim's authoritative health code.</summary>
        public void RegisterKill(PlayerRef killer, PlayerRef victim)
        {
            if (!HasStateAuthority) return;
            int victimSlot = SlotOf(victim);
            int killerSlot = SlotOf(killer);
            if (victimSlot >= 0) Modify(victimSlot, (ref PlayerSlot s) => s.Deaths++);
            if (killerSlot >= 0 && killer != victim) Modify(killerSlot, (ref PlayerSlot s) => s.Kills++);
            Revision++;
        }

        public void ResetScores()
        {
            if (!HasStateAuthority) return;
            for (int i = 0; i < Capacity; i++)
            {
                Modify(i, (ref PlayerSlot s) => { s.Kills = 0; s.Deaths = 0; s.WantsRematch = false; });
            }
            Revision++;
        }

        public void ClearReadyFlags()
        {
            if (!HasStateAuthority) return;
            for (int i = 0; i < Capacity; i++) Modify(i, (ref PlayerSlot s) => { s.Ready = false; s.WantsRematch = false; });
            Revision++;
        }

        /// <summary>True when every player in the room asked for a rematch.</summary>
        public bool BothWantRematch
        {
            get
            {
                int voters = 0;
                for (int i = 0; i < Capacity; i++)
                {
                    if (!Slots[i].Occupied) continue;
                    if (!Slots[i].WantsRematch) return false;
                    voters++;
                }
                return voters > 0;
            }
        }

        private delegate void SlotEdit(ref PlayerSlot slot);

        private void Modify(int index, SlotEdit edit)
        {
            PlayerSlot slot = Slots[index];
            if (!slot.Occupied) return;
            edit(ref slot);
            Slots.Set(index, slot);
        }

        // ------------------------------------------------------------------ requests from players (validated by host)
        //
        // Requests travel as Fusion reliable data instead of [Rpc] methods: Fusion 2.1's RPC weaving calls an
        // internal runtime method that Mono builds (Editor, Windows) reject with MethodAccessException.
        // Reliable data works identically on Mono and IL2CPP. The host validates every request.

        public enum Request : byte { Profile = 1, Weapon = 2, Ready = 3, Rematch = 4, Lobby = 5 }

        public static readonly ReliableKey RequestKey = ReliableKey.FromInts(0x57_44_52_51, 1, 0, 0);

        /// <summary>Any player: set my display name, lobby weapon and profile look (avatar, banner, title, level).</summary>
        public void RequestProfile(string playerName, byte weapon, byte avatar = 0, byte banner = 0, byte title = 0, byte level = 1, byte character = 0)
        {
            byte[] name = Encoding.UTF8.GetBytes(GameSettings.SanitizeName(playerName ?? string.Empty));
            int length = Mathf.Min(name.Length, 64);
            var payload = new byte[3 + length + 5];
            payload[0] = (byte)Request.Profile;
            payload[1] = weapon;
            payload[2] = (byte)length;
            Array.Copy(name, 0, payload, 3, length);
            payload[3 + length] = avatar;
            payload[4 + length] = banner;
            payload[5 + length] = title;
            payload[6 + length] = level;
            payload[7 + length] = character;
            Send(payload);
        }

        public void RequestWeapon(byte weapon) => Send(new[] { (byte)Request.Weapon, weapon });
        public void RequestReady(bool ready) => Send(new[] { (byte)Request.Ready, (byte)(ready ? 1 : 0) });
        public void RequestRematch(bool wants) => Send(new[] { (byte)Request.Rematch, (byte)(wants ? 1 : 0) });

        /// <summary>Any player, after a match: ask the host to take everyone back to the lobby.</summary>
        public void RequestLobby() => Send(new[] { (byte)Request.Lobby });

        private void Send(byte[] payload)
        {
            if (Runner == null || !Runner.IsRunning) return;
            if (Runner.IsServer) HandleRequest(Runner.LocalPlayer, payload);
            else Runner.SendReliableDataToServer(RequestKey, payload);
        }

        /// <summary>Host only: applies a validated request from <paramref name="source"/>.</summary>
        public void HandleRequest(PlayerRef source, ReadOnlySpan<byte> data)
        {
            if (!HasStateAuthority || data.Length < 1) return;
            int index = SlotOf(source);
            if (index < 0) return;

            switch ((Request)data[0])
            {
                case Request.Profile when data.Length >= 3:
                {
                    int length = Mathf.Min(data[2], data.Length - 3);
                    string clean = GameSettings.SanitizeName(Encoding.UTF8.GetString(data.Slice(3, length).ToArray()));
                    if (clean.Length == 0) clean = $"Player {index + 1}";
                    byte weapon = ClampWeapon(data[1]);
                    bool hasLook = data.Length >= 3 + length + 4;
                    byte avatar = hasLook ? (byte)Mathf.Min(data[3 + length], 63) : (byte)0;
                    byte banner = hasLook ? (byte)Mathf.Min(data[4 + length], 63) : (byte)0;
                    byte title = hasLook ? (byte)Mathf.Min(data[5 + length], 31) : (byte)0;
                    byte level = hasLook ? (byte)Mathf.Clamp(data[6 + length], 1, 99) : (byte)1;
                    byte character = data.Length >= 3 + length + 5 ? (byte)Mathf.Min(data[7 + length], 31) : (byte)0;
                    Modify(index, (ref PlayerSlot s) =>
                    {
                        s.Name = clean;
                        s.Weapon = weapon;
                        s.Avatar = avatar;
                        s.Banner = banner;
                        s.Title = title;
                        s.Level = level;
                        s.Character = character;
                    });
                    break;
                }
                case Request.Weapon when data.Length >= 2:
                {
                    byte weapon = ClampWeapon(data[1]);
                    Modify(index, (ref PlayerSlot s) => s.Weapon = weapon);
                    break;
                }
                case Request.Ready when data.Length >= 2:
                {
                    bool ready = data[1] != 0;
                    Modify(index, (ref PlayerSlot s) => s.Ready = ready);
                    break;
                }
                case Request.Rematch when data.Length >= 2:
                {
                    bool wants = data[1] != 0;
                    Modify(index, (ref PlayerSlot s) => s.WantsRematch = wants);
                    break;
                }
                case Request.Lobby:
                    if (Match.MatchManager.Instance != null && Match.MatchManager.Instance.Phase != Match.MatchPhase.Ended) return;
                    if (SessionService.Instance != null) SessionService.Instance.ReturnToLobby();
                    return;
                default:
                    return;
            }
            Revision++;
        }

        private static byte ClampWeapon(byte weapon) => (byte)Mathf.Clamp(weapon, 0, 3);
    }
}
