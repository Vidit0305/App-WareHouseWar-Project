using System;
using System.Collections.Generic;
using Fusion;
using UnityEngine;
using WarehouseDuel.Core;
using WarehouseDuel.Multiplayer;
using WarehouseDuel.Player;

namespace WarehouseDuel.Weapons
{
    /// <summary>A bullet impact replicated from the host so the other player sees where shots landed.</summary>
    public struct ImpactData : INetworkStruct
    {
        public Vector3 Point;
        public Vector3 Normal;
        public NetworkBool HitPlayer;
    }

    /// <summary>
    /// Networked weapon handling: selection, ammo, reload, fire-rate, spread, recoil and hit detection.
    ///
    /// Every shot is validated in simulation: alive, match running, not reloading/equipping, cooldown expired,
    /// ammo in the magazine. Damage is computed ONLY on the host, using Fusion lag compensation
    /// (<c>Runner.LagCompensation.Raycast</c>) so hits are judged against where the target was on the
    /// shooter's screen. The shooter's device predicts muzzle flash, recoil and impacts for instant feedback.
    /// </summary>
    [RequireComponent(typeof(PlayerCharacter))]
    public sealed class WeaponController : NetworkBehaviour
    {
        public const int SlotCount = WeaponCatalog.MaxWeapons;
        private const int ImpactBufferSize = 16;

        [SerializeField] private WeaponCatalog catalog;
        [SerializeField] private LayerMask hitMask = ~0;
        [Tooltip("Physics layers used for the shooter's cosmetic (predicted) impact ray.")]
        [SerializeField] private LayerMask cosmeticMask = ~0;

        [Networked] public byte CurrentWeapon { get; private set; }
        [Networked, Capacity(SlotCount)] public NetworkArray<short> Magazine => default;
        [Networked, Capacity(SlotCount)] public NetworkArray<short> Reserve => default;
        [Networked] public TickTimer FireCooldown { get; private set; }
        [Networked] public TickTimer ReloadTimer { get; private set; }
        [Networked] public TickTimer EquipTimer { get; private set; }
        [Networked] private NetworkButtons PreviousButtons { get; set; }

        /// <summary>Counters compared in Render() to replay effects on the other peers.</summary>
        [Networked] public int ShotCount { get; private set; }
        [Networked] public int ReloadCount { get; private set; }
        [Networked] public int SwitchCount { get; private set; }
        [Networked] public int HitConfirmCount { get; private set; }
        [Networked] public NetworkBool LastHitWasHead { get; private set; }
        [Networked] public NetworkBool LastHitKilled { get; private set; }
        [Networked] public int ImpactCount { get; private set; }
        [Networked, Capacity(ImpactBufferSize)] public NetworkArray<ImpactData> Impacts => default;

        /// <summary>Local-only presentation hooks (view model, HUD, audio).</summary>
        public event Action<WeaponDefinition, bool> ShotFired;      // (weapon, predicted locally)
        public event Action<Vector3, Vector3, bool> ImpactShown;     // (point, normal, hit player)
        public event Action<WeaponDefinition> Reloaded;
        public event Action<WeaponDefinition> Equipped;
        public event Action<bool, bool> HitConfirmed;                // (headshot, killed)
        public event Action DryFired;

        public WeaponCatalog Catalog => catalog;
        public WeaponDefinition Current => catalog != null ? catalog.Get(CurrentWeapon) : null;
        public bool IsReloading => ReloadTimer.IsRunning && !ReloadTimer.Expired(Runner);
        public float MoveSpeedMultiplier => Current != null ? Current.moveSpeedMultiplier : 1f;
        public int CurrentMagazine => Magazine[CurrentWeapon];
        public int CurrentReserve => Reserve[CurrentWeapon];

        /// <summary>0..1 progress of the current reload, for the HUD.</summary>
        public float ReloadProgress
        {
            get
            {
                if (!IsReloading || Current == null) return 0f;
                float remaining = ReloadTimer.RemainingTime(Runner) ?? 0f;
                return 1f - remaining / Current.reloadSeconds;
            }
        }

        private PlayerCharacter character;
        private int renderedShots, renderedImpacts, renderedReloads, renderedSwitches, renderedHits;
        private readonly Dictionary<PlayerCharacter, (float damage, bool head)> pendingDamage = new Dictionary<PlayerCharacter, (float, bool)>(2);

        private void Awake() => character = GetComponent<PlayerCharacter>();

        public override void Spawned()
        {
            if (HasStateAuthority)
            {
                int preferred = 0;
                if (RoomState.Instance != null && RoomState.Instance.TryGetSlot(Object.InputAuthority, out PlayerSlot slot)) preferred = slot.Weapon;
                CurrentWeapon = (byte)Mathf.Clamp(preferred, 0, catalog.Count - 1);
                ResetLoadout();
            }

            renderedShots = ShotCount;
            renderedImpacts = ImpactCount;
            renderedReloads = ReloadCount;
            renderedSwitches = SwitchCount;
            renderedHits = HitConfirmCount;
        }

        /// <summary>Host: full magazines and reserves for every weapon (spawn / respawn rule).</summary>
        public void ResetLoadout()
        {
            if (!HasStateAuthority) return;
            for (int i = 0; i < SlotCount; i++)
            {
                WeaponDefinition def = catalog.Get(i);
                Magazine.Set(i, (short)(def != null ? def.magazineSize : 0));
                Reserve.Set(i, (short)(def != null ? def.reserveAmmo : 0));
            }
            ReloadTimer = TickTimer.None;
            FireCooldown = TickTimer.None;
            EquipTimer = TickTimer.None;
        }

        public override void FixedUpdateNetwork()
        {
            if (!GetInput(out NetworkInputData input)) return;

            NetworkButtons pressed = input.Buttons.GetPressed(PreviousButtons);
            PreviousButtons = input.Buttons;

            // Finish a reload whose timer ran out (also if the player died meanwhile: the timer is simply cleared).
            if (ReloadTimer.Expired(Runner))
            {
                ReloadTimer = TickTimer.None;
                if (character.IsAlive) CompleteReload();
            }

            if (!character.CanAct) return;

            // --- weapon switching
            int requested = input.WeaponRequest - 1;
            if (pressed.IsSet(InputButton.NextWeapon)) requested = (CurrentWeapon + 1) % catalog.Count;
            if (requested >= 0 && requested < catalog.Count && requested != CurrentWeapon)
            {
                Equip((byte)requested);
                return;
            }

            WeaponDefinition weapon = Current;
            if (weapon == null) return;
            bool busy = IsReloading || (EquipTimer.IsRunning && !EquipTimer.Expired(Runner));

            // --- reload
            if (pressed.IsSet(InputButton.Reload) && !busy) TryStartReload(weapon);

            // --- fire
            bool trigger = weapon.fireMode == FireMode.Automatic
                ? input.Buttons.IsSet(InputButton.Fire)
                : pressed.IsSet(InputButton.Fire);
            if (!trigger || busy || !FireCooldown.ExpiredOrNotRunning(Runner)) return;

            if (Magazine[CurrentWeapon] <= 0)
            {
                if (!TryStartReload(weapon) && pressed.IsSet(InputButton.Fire) && HasInputAuthority && Runner.IsForward) DryFired?.Invoke();
                return;
            }

            Fire(weapon, input);
        }

        private void Fire(WeaponDefinition weapon, NetworkInputData input)
        {
            Magazine.Set(CurrentWeapon, (short)(Magazine[CurrentWeapon] - 1));
            FireCooldown = TickTimer.CreateFromSeconds(Runner, weapon.SecondsBetweenShots);
            ShotCount++;
            character.BreakProtection();

            Vector3 origin = character.EyePosition;
            Quaternion aim = Quaternion.Euler(input.Pitch, input.Yaw, 0f);
            float spread = character.IsAiming ? weapon.aimSpread : weapon.hipSpread;
            if (!character.Motor.Grounded) spread *= 1.5f;
            bool predictLocally = HasInputAuthority && Runner.IsForward;

            pendingDamage.Clear();
            for (int pellet = 0; pellet < weapon.pellets; pellet++)
            {
                Vector3 direction = aim * SpreadDirection(spread, (uint)Runner.Tick.Raw, (uint)pellet);

                if (HasStateAuthority) ResolveAuthoritativeHit(weapon, origin, direction);
                if (predictLocally) PredictImpact(origin, direction, weapon.range);
            }

            if (HasStateAuthority) ApplyPendingDamage();

            if (predictLocally)
            {
                ShotFired?.Invoke(weapon, true);
                float yawKick = (Hash01((uint)Runner.Tick.Raw, 977u) * 2f - 1f) * weapon.recoilYaw;
                if (LocalInputCollector.Instance != null) LocalInputCollector.Instance.AddRecoil(weapon.recoilPitch, yawKick);
            }
        }

        /// <summary>Host only: lag-compensated ray against hitboxes and level geometry.</summary>
        private void ResolveAuthoritativeHit(WeaponDefinition weapon, Vector3 origin, Vector3 direction)
        {
            bool hasHit = Runner.LagCompensation.Raycast(origin, direction, weapon.range, Object.InputAuthority,
                out LagCompensatedHit hit, hitMask, HitOptions.IncludePhysX | HitOptions.SubtickAccuracy | HitOptions.IgnoreInputAuthority);
            if (!hasHit) return;

            bool hitPlayer = false;
            if (hit.Hitbox != null)
            {
                PlayerCharacter target = hit.Hitbox.Root != null ? hit.Hitbox.Root.GetComponent<PlayerCharacter>() : null;
                if (target != null && target != character && target.IsAlive)
                {
                    hitPlayer = true;
                    HitboxRegion region = hit.Hitbox.GetComponent<HitboxRegion>();
                    bool head = region != null && region.IsHead;
                    float multiplier = region != null ? region.DamageMultiplier : 1f;
                    if (head) multiplier = weapon.headshotMultiplier;
                    float damage = weapon.damage * multiplier * weapon.FalloffMultiplier(hit.Distance);

                    pendingDamage.TryGetValue(target, out (float damage, bool head) sum);
                    pendingDamage[target] = (sum.damage + damage, sum.head || head);
                }
            }

            RecordImpact(hit.Point, hit.Normal, hitPlayer);
        }

        /// <summary>Host only: one damage application per target per shot (shotgun pellets are summed first).</summary>
        private void ApplyPendingDamage()
        {
            foreach (KeyValuePair<PlayerCharacter, (float damage, bool head)> entry in pendingDamage)
            {
                bool killed = entry.Key.ApplyDamage(entry.Value.damage, Object.InputAuthority, entry.Value.head);
                HitConfirmCount++;
                LastHitWasHead = entry.Value.head;
                LastHitKilled = killed;
            }
            pendingDamage.Clear();
        }

        private void RecordImpact(Vector3 point, Vector3 normal, bool hitPlayer)
        {
            Impacts.Set(ImpactCount % ImpactBufferSize, new ImpactData { Point = point, Normal = normal, HitPlayer = hitPlayer });
            ImpactCount++;
        }

        /// <summary>Shooter only, cosmetic: instant impact effect from a plain physics ray (no damage).</summary>
        private void PredictImpact(Vector3 origin, Vector3 direction, float range)
        {
            if (Physics.Raycast(origin, direction, out RaycastHit hit, range, cosmeticMask, QueryTriggerInteraction.Ignore))
            {
                bool isPlayer = hit.collider.GetComponentInParent<PlayerCharacter>() != null;
                ImpactShown?.Invoke(hit.point, hit.normal, isPlayer);
            }
        }

        private bool TryStartReload(WeaponDefinition weapon)
        {
            if (Magazine[CurrentWeapon] >= weapon.magazineSize || Reserve[CurrentWeapon] <= 0 || IsReloading) return false;
            ReloadTimer = TickTimer.CreateFromSeconds(Runner, weapon.reloadSeconds);
            ReloadCount++;
            return true;
        }

        private void CompleteReload()
        {
            WeaponDefinition weapon = Current;
            int needed = weapon.magazineSize - Magazine[CurrentWeapon];
            int taken = Mathf.Min(needed, Reserve[CurrentWeapon]);
            Magazine.Set(CurrentWeapon, (short)(Magazine[CurrentWeapon] + taken));
            Reserve.Set(CurrentWeapon, (short)(Reserve[CurrentWeapon] - taken));
        }

        private void Equip(byte index)
        {
            CurrentWeapon = index;
            ReloadTimer = TickTimer.None; // switching cancels a reload
            WeaponDefinition weapon = Current;
            EquipTimer = TickTimer.CreateFromSeconds(Runner, weapon != null ? weapon.equipSeconds : 0.3f);
            SwitchCount++;
        }

        // ------------------------------------------------------------------ presentation (every peer)

        public override void Render()
        {
            if (SwitchCount != renderedSwitches)
            {
                renderedSwitches = SwitchCount;
                Equipped?.Invoke(Current);
            }

            if (ReloadCount != renderedReloads)
            {
                renderedReloads = ReloadCount;
                Reloaded?.Invoke(Current);
            }

            // The shooter already played its own shots/impacts when predicting; others replay them from state.
            if (ShotCount != renderedShots)
            {
                int newShots = ShotCount - renderedShots;
                renderedShots = ShotCount;
                if (!HasInputAuthority && newShots > 0) ShotFired?.Invoke(Current, false);
            }

            if (ImpactCount != renderedImpacts)
            {
                int first = Mathf.Max(renderedImpacts, ImpactCount - ImpactBufferSize);
                if (!HasInputAuthority)
                {
                    for (int i = first; i < ImpactCount; i++)
                    {
                        ImpactData impact = Impacts[i % ImpactBufferSize];
                        ImpactShown?.Invoke(impact.Point, impact.Normal, impact.HitPlayer);
                    }
                }
                renderedImpacts = ImpactCount;
            }

            if (HitConfirmCount != renderedHits)
            {
                renderedHits = HitConfirmCount;
                if (HasInputAuthority) HitConfirmed?.Invoke(LastHitWasHead, LastHitKilled);
            }
        }

        // ------------------------------------------------------------------ deterministic spread

        /// <summary>Direction inside a cone of <paramref name="degrees"/>, identical on host and shooter for the same tick.</summary>
        private static Vector3 SpreadDirection(float degrees, uint tick, uint pellet)
        {
            if (degrees <= 0f) return Vector3.forward;
            float angle = Hash01(tick, pellet * 2u + 1u) * Mathf.PI * 2f;
            float radius = Mathf.Sqrt(Hash01(tick, pellet * 2u + 2u)) * Mathf.Tan(degrees * Mathf.Deg2Rad);
            return new Vector3(Mathf.Cos(angle) * radius, Mathf.Sin(angle) * radius, 1f).normalized;
        }

        private static float Hash01(uint a, uint b)
        {
            uint h = a * 747796405u + b * 2891336453u;
            h ^= h >> 16;
            h *= 2246822519u;
            h ^= h >> 13;
            h *= 3266489917u;
            h ^= h >> 16;
            return (h & 0xFFFFFF) / 16777216f;
        }
    }
}
