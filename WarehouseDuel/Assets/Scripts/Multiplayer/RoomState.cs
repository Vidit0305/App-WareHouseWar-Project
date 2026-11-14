using System;
using System.Text;
using Fusion;
using Fusion.Sockets;
using UnityEngine;
using WarehouseWar.Core;

namespace WarehouseWar.Multiplayer
{
    /// <summary>One player's entry in the room (up to 8). Slot 0 = host; each slot has its own spawn point.</summary>
    public struct PlayerSlot : INetworkStruct
    {
        public PlayerRef Player;
        public NetworkString<_16> Name;
        /// <summary>Equipped weapons (catalog indices): Weapon is the first, Weapon2/Weapon3 the others.</summary>
        public byte Weapon;
        public byte Weapon2;
        public byte Weapon3;
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
    /// Host-owned record of who is in the room (up to 8 players, free-for-all), the chosen map and, for quick
    /// matches, the auto-start countdown. Survives scene changes (menu/lobby ↔ map).
    /// Clients never write it directly: they ask the host through RPCs, and the host validates every request.
    /// Kills/deaths are only changed by <see cref="RegisterKill"/>, which the host calls exactly once per death.
    /// </summary>
    public sealed class RoomState : NetworkBehaviour
    {
        public const int Capacity = 8;
        /// <summary>Players needed to start an online match.</summary>
        public const int MinPlayers = 2;
        /// <summary>Quick match: seconds to wait for more players once the minimum is reached.</summary>
        public const float QuickStartDelay = 4f;

        [Networked, Capacity(Capacity)] public NetworkArray<PlayerSlot> Slots => default;
        /// <summary>Incremented on every change so UIs can refresh cheaply.</summary>
        [Networked] public int Revision { get; private set; }
        /// <summary>Map index into GameConstants.Scenes.Maps, or GameConstants.Scenes.RandomMap.</summary>
        [Networked] public byte MapChoice { get; private set; }
        /// <summary>True for matchmaking rooms (no code, starts by itself once enough players are in).</summary>
        [Networked] public NetworkBool QuickPlay { get; private set; }
        [Networked] public TickTimer QuickStartTimer { get; private set; }
        [Networked] public NetworkBool QuickStarted { get; private set; }

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

        public override void FixedUpdateNetwork()
        {
            if (!HasStateAuthority || !QuickPlay || QuickStarted) return;
            if (OccupiedCount < MinPlayers)
            {
                QuickStartTimer = TickTimer.None;
                return;
            }
            if (!QuickStartTimer.IsRunning) QuickStartTimer = TickTimer.CreateFromSeconds(Runner, QuickStartDelay);
            else if (QuickStartTimer.Expired(Runner))
            {
                QuickStarted = true;
                Revision++;
                if (SessionService.Instance != null) SessionService.Instance.StartQuickMatch();
            }
        }

        /// <summary>Seconds until a quick match starts (0 when not counting down).</summary>
        public float QuickStartRemaining => QuickStartTimer.IsRunning ? Mathf.Max(0f, QuickStartTimer.RemainingTime(Runner) ?? 0f) : 0f;

        /// <summary>Host: the map and mode chosen when the room was created.</summary>
        public void Configure(int mapChoice, bool quickPlay)
        {
            if (!HasStateAuthority) return;
            MapChoice = (byte)mapChoice;
            QuickPlay = quickPlay;
            QuickStarted = false;
            QuickStartTimer = TickTimer.None;
            Revision++;
        }

        /// <summary>Host: the map to load now (resolves "random").</summary>
        public int ResolveMap()
        {
            int count = GameConstants.Scenes.Maps.Length;
            return MapChoice < count ? MapChoice : UnityEngine.Random.Range(0, count);
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

        // ------------------------------------------------------------------ practice bots (host only)

        /// <summary>Bots use fake PlayerRefs (encoded value BotRefBase + n) that no real Fusion player ever gets.</summary>
        public const int BotRefBase = 100;

        public static PlayerRef BotRef(int botIndex) => PlayerRef.FromEncoded(BotRefBase + botIndex);

        public static bool IsBot(PlayerRef player) => player.RawEncoded >= BotRefBase;

        /// <summary>
        /// Host: adds (or finds) bot number <paramref name="botIndex"/> in the first free slot with the given look and
        /// loadout. Bots are always ready. Returns the slot, or -1 if the room is full.
        /// </summary>
        public int AddBot(int botIndex, string botName, byte weapon, byte weapon2, byte weapon3, byte character, byte avatar, byte banner, byte title, byte level)
        {
            if (!HasStateAuthority) return -1;
            PlayerRef bot = BotRef(botIndex);
            int existing = SlotOf(bot);
            if (existing >= 0) return existing;

            for (int i = 0; i < Capacity; i++)
            {
                if (Slots[i].Occupied) continue;
                Slots.Set(i, new PlayerSlot
                {
                    Player = bot,
                    Name = botName,
                    Weapon = ClampWeapon(weapon),
                    Weapon2 = ClampWeapon(weapon2),
                    Weapon3 = ClampWeapon(weapon3),
                    Character = character,
                    Avatar = avatar,
                    Banner = banner,
                    Title = title,
                    Level = level,
                    Occupied = true,
                    Ready = true,
                });
                Revision++;
                return i;
            }
            return -1;
        }

        /// <summary>Number of bots currently in the room.</summary>
        public int BotCount
        {
            get
            {
                int count = 0;
                for (int i = 0; i < Capacity; i++) if (Slots[i].Occupied && IsBot(Slots[i].Player)) count++;
                return count;
            }
        }

        public void RemovePlayer(PlayerRef player)
        {
            if (!HasStateAuthority) return;
            int index = SlotOf(player);
            if (index < 0) return;
            Slots.Set(index, default);
            // Rematch votes are cast again by whoever is still here.
            for (int i = 0; i < Capacity; i++) Modify(i, (ref PlayerSlot s) => s.WantsRematch = false);
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

        /// <summary>True when every player in the room (at least two) asked for a rematch.</summary>
        public bool BothWantRematch
        {
            get
            {
                int voters = 0;
                for (int i = 0; i < Capacity; i++)
                {
                    if (!Slots[i].Occupied) continue;
                    // Practice bots always want another round.
                    if (!Slots[i].WantsRematch && !IsBot(Slots[i].Player)) return false;
                    voters++;
                }
                return voters >= MinPlayers;
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
        public void RequestProfile(string playerName, byte weapon, byte avatar = 0, byte banner = 0, byte title = 0, byte level = 1, byte character = 0,
                                   byte weapon2 = 3, byte weapon3 = 6)
        {
            byte[] name = Encoding.UTF8.GetBytes(GameSettings.SanitizeName(playerName ?? string.Empty));
            int length = Mathf.Min(name.Length, 64);
            var payload = new byte[3 + length + 7];
            payload[0] = (byte)Request.Profile;
            payload[1] = weapon;
            payload[2] = (byte)length;
            Array.Copy(name, 0, payload, 3, length);
            payload[3 + length] = avatar;
            payload[4 + length] = banner;
            payload[5 + length] = title;
            payload[6 + length] = level;
            payload[7 + length] = character;
            payload[8 + length] = weapon2;
            payload[9 + length] = weapon3;
            Send(payload);
        }

        /// <summary>Any player: set my three equipped weapons.</summary>
        public void RequestLoadout(byte weapon, byte weapon2, byte weapon3) => Send(new[] { (byte)Request.Weapon, weapon, weapon2, weapon3 });
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
                    bool hasLoadout = data.Length >= 3 + length + 7;
                    byte weapon2 = hasLoadout ? ClampWeapon(data[8 + length]) : (byte)3;
                    byte weapon3 = hasLoadout ? ClampWeapon(data[9 + length]) : (byte)6;
                    Modify(index, (ref PlayerSlot s) =>
                    {
                        s.Name = clean;
                        s.Weapon = weapon;
                        s.Weapon2 = weapon2;
                        s.Weapon3 = weapon3;
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
                    bool all = data.Length >= 4;
                    byte weapon2 = all ? ClampWeapon(data[2]) : (byte)0;
                    byte weapon3 = all ? ClampWeapon(data[3]) : (byte)0;
                    Modify(index, (ref PlayerSlot s) =>
                    {
                        s.Weapon = weapon;
                        if (all)
                        {
                            s.Weapon2 = weapon2;
                            s.Weapon3 = weapon3;
                        }
                    });
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

        private static byte ClampWeapon(byte weapon) => (byte)Mathf.Clamp(weapon, 0, WarehouseWar.Weapons.WeaponCatalog.MaxWeapons - 1);
    }
}
