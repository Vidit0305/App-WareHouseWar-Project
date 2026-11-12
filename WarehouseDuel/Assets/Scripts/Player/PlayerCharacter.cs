using System;
using Fusion;
using UnityEngine;
using WarehouseDuel.Environment;
using WarehouseDuel.Match;
using WarehouseDuel.Multiplayer;

namespace WarehouseDuel.Player
{
    /// <summary>
    /// Networked first-person character: movement, look, health, death, respawn and spawn protection.
    ///
    /// Authority model (Fusion Host Mode):
    ///  - The owner's device predicts movement from its own input for instant response.
    ///  - The host runs the same code authoritatively and its result overrides the prediction.
    ///  - Health, death, kills and respawns are ONLY changed where HasStateAuthority is true (the host).
    /// </summary>
    [RequireComponent(typeof(NetworkCharacterController))]
    public sealed class PlayerCharacter : NetworkBehaviour
    {
        public const float EyeHeight = 1.6f;

        [SerializeField] private MatchRules rules;

        [Networked] public float Health { get; private set; }
        [Networked] public NetworkBool IsAlive { get; private set; }
        [Networked] public float Pitch { get; private set; }
        [Networked] public NetworkBool IsSprinting { get; private set; }
        [Networked] public NetworkBool IsAiming { get; private set; }
        [Networked] public byte Slot { get; private set; }
        [Networked] public TickTimer RespawnTimer { get; private set; }
        [Networked] public TickTimer ProtectionTimer { get; private set; }
        [Networked] public PlayerRef LastKiller { get; private set; }
        /// <summary>Counters that only go up; Render() compares them to trigger one-off effects on every peer.</summary>
        [Networked] public int DeathCount { get; private set; }
        [Networked] public int SpawnCount { get; private set; }
        [Networked] public int DamageCount { get; private set; }
        [Networked] public NetworkBool LastDeathWasHeadshot { get; private set; }
        [Networked] private NetworkButtons PreviousButtons { get; set; }
        /// <summary>Host sets these on respawn; the owner's device points its camera at <see cref="LookResetYaw"/>.</summary>
        [Networked] private int LookResetCount { get; set; }
        [Networked] private float LookResetYaw { get; set; }

        public static PlayerCharacter Local { get; private set; }

        /// <summary>Fired on every peer when any player dies: (victim, killer).</summary>
        public static event Action<PlayerCharacter, PlayerRef> AnyDied;
        public static event Action<PlayerCharacter> AnySpawned;
        /// <summary>Fired on the victim's peers when this character takes damage.</summary>
        public event Action Damaged;

        public NetworkCharacterController Motor { get; private set; }
        public MatchRules Rules => rules;
        public PlayerRef Owner => Object.InputAuthority;
        public bool IsLocal => Object != null && Object.HasInputAuthority;
        public bool IsProtected => ProtectionTimer.IsRunning && !ProtectionTimer.Expired(Runner);
        public Vector3 EyePosition => transform.position + Vector3.up * EyeHeight;
        public float Yaw => transform.eulerAngles.y;

        /// <summary>True when this player may move and shoot (alive and the match is running).</summary>
        public bool CanAct => IsAlive && MatchManager.GameplayAllowed;

        private int renderedDeaths, renderedSpawns, renderedDamage, renderedLookResets;
        private Weapons.WeaponController weapons;

        private void Awake()
        {
            Motor = GetComponent<NetworkCharacterController>();
            weapons = GetComponent<Weapons.WeaponController>();
        }

        public override void Spawned()
        {
            if (HasStateAuthority)
            {
                Health = rules.maxHealth;
                IsAlive = true;
                RoomState room = RoomState.Instance;
                Slot = (byte)Mathf.Max(0, room != null ? room.SlotOf(Owner) : 0);
                ProtectionTimer = TickTimer.CreateFromSeconds(Runner, rules.spawnProtectionSeconds);
            }

            ApplyMovementTuning(1f);

            if (HasInputAuthority)
            {
                Local = this;
                LocalInputCollector collector = LocalInputCollector.EnsureExists();
                collector.SetLook(transform.eulerAngles.y, 0f);
            }

            renderedDeaths = DeathCount;
            renderedSpawns = SpawnCount;
            renderedDamage = DamageCount;
            renderedLookResets = LookResetCount;
            AnySpawned?.Invoke(this);
        }

        public override void Despawned(NetworkRunner runner, bool hasState)
        {
            if (Local == this) Local = null;
        }

        public override void FixedUpdateNetwork()
        {
            if (HasStateAuthority) UpdateLifecycle();

            if (!GetInput(out NetworkInputData input))
            {
                // No input this tick (proxy or dropped packet): keep gravity running, nothing else.
                if (HasStateAuthority || HasInputAuthority) Motor.Move(Vector3.zero);
                return;
            }

            if (!IsAlive)
            {
                Motor.Move(Vector3.zero);
                PreviousButtons = input.Buttons;
                return;
            }

            // Look is always allowed while alive (also during the countdown).
            transform.rotation = Quaternion.Euler(0f, input.Yaw, 0f);
            Pitch = Mathf.Clamp(input.Pitch, -LocalInputCollector.PitchLimit, LocalInputCollector.PitchLimit);

            Vector2 move = CanAct ? Vector2.ClampMagnitude(input.Move, 1f) : Vector2.zero;
            bool aiming = CanAct && input.Buttons.IsSet(InputButton.Aim);
            bool sprinting = CanAct && input.Buttons.IsSet(InputButton.Sprint) && move.y > 0.3f && !aiming;
            IsAiming = aiming;
            IsSprinting = sprinting;

            float weaponSpeed = weapons != null ? weapons.MoveSpeedMultiplier : 1f;
            float speed = (sprinting ? rules.sprintSpeed : rules.walkSpeed) * (aiming ? rules.aimMoveMultiplier : 1f) * weaponSpeed;
            ApplyMovementTuning(speed * Mathf.Max(move.magnitude, 0.01f) / rules.walkSpeed);

            Vector3 direction = transform.rotation * new Vector3(move.x, 0f, move.y);
            Motor.Move(direction);

            if (CanAct && input.Buttons.WasPressed(PreviousButtons, InputButton.Jump) && Motor.Grounded)
            {
                Motor.Jump();
            }

            PreviousButtons = input.Buttons;
        }

        public override void Render()
        {
            if (LookResetCount != renderedLookResets)
            {
                renderedLookResets = LookResetCount;
                if (HasInputAuthority && LocalInputCollector.Instance != null) LocalInputCollector.Instance.SetLook(LookResetYaw, 0f);
            }

            if (DeathCount != renderedDeaths)
            {
                renderedDeaths = DeathCount;
                AnyDied?.Invoke(this, LastKiller);
            }
            if (SpawnCount != renderedSpawns)
            {
                renderedSpawns = SpawnCount;
                AnySpawned?.Invoke(this);
            }
            if (DamageCount != renderedDamage)
            {
                renderedDamage = DamageCount;
                Damaged?.Invoke();
            }
        }

        // ------------------------------------------------------------------ host-authoritative health

        /// <summary>
        /// Host only. Applies validated damage. Returns true if this hit killed the player.
        /// The alive → dead transition happens exactly once, and only there is a kill credited,
        /// so a kill can never be counted twice (even with shotgun pellets landing in the same tick).
        /// </summary>
        public bool ApplyDamage(float amount, PlayerRef attacker, bool headshot)
        {
            if (!HasStateAuthority || !IsAlive || amount <= 0f) return false;
            if (IsProtected || !MatchManager.GameplayAllowed) return false;

            Health = Mathf.Max(0f, Health - amount);
            DamageCount++;
            if (Health > 0f) return false;

            IsAlive = false;
            LastKiller = attacker;
            LastDeathWasHeadshot = headshot;
            DeathCount++;
            RespawnTimer = TickTimer.CreateFromSeconds(Runner, rules.respawnDelaySeconds);
            if (RoomState.Instance != null) RoomState.Instance.RegisterKill(attacker, Owner);
            return true;
        }

        /// <summary>Host only: ends spawn protection early (the protected player opened fire).</summary>
        public void BreakProtection()
        {
            if (HasStateAuthority) ProtectionTimer = TickTimer.None;
        }

        /// <summary>Host only: puts the player back at their spawn with full health (match start / rematch / respawn).</summary>
        public void Respawn(bool withProtection = true)
        {
            if (!HasStateAuthority) return;

            SpawnPoint spawn = SpawnPoint.ForSlot(Slot);
            Vector3 position = spawn != null ? spawn.Position : Vector3.up;
            Quaternion rotation = spawn != null ? spawn.Rotation : Quaternion.identity;

            Motor.Teleport(position, rotation);
            Motor.Velocity = Vector3.zero;
            Health = rules.maxHealth;
            IsAlive = true;
            Pitch = 0f;
            RespawnTimer = TickTimer.None;
            ProtectionTimer = withProtection ? TickTimer.CreateFromSeconds(Runner, rules.spawnProtectionSeconds) : TickTimer.None;
            SpawnCount++;

            if (weapons != null) weapons.ResetLoadout();
            LookResetYaw = rotation.eulerAngles.y;
            LookResetCount++;
        }

        private void UpdateLifecycle()
        {
            if (!IsAlive && RespawnTimer.Expired(Runner) && MatchManager.RespawnsAllowed)
            {
                Respawn();
                return;
            }

            // Safety net: anyone who leaves the map volume (physics glitch) goes back to their spawn.
            MapBounds bounds = MapBounds.Current;
            if (IsAlive && bounds != null && !bounds.IsInside(transform.position, 2f))
            {
                Respawn(withProtection: false);
            }
        }

        private void ApplyMovementTuning(float speedScale)
        {
            Motor.maxSpeed = rules.walkSpeed * speedScale;
            Motor.acceleration = 60f;
            Motor.braking = 14f;
            Motor.rotationSpeed = 0f; // rotation comes from look input, not from movement direction
            Motor.gravity = -rules.gravity;
            Motor.jumpImpulse = Mathf.Sqrt(2f * rules.gravity * rules.jumpHeight);
        }
    }
}
