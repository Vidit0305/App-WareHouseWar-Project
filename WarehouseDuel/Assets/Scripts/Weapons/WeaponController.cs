using System;
using System.Collections.Generic;
using Fusion;
using UnityEngine;
using WarehouseDuel.Core;
using WarehouseDuel.Multiplayer;
using WarehouseDuel.Player;

namespace WarehouseDuel.Weapons
{
    /// <summary>What a replicated <see cref="ImpactData"/> represents.</summary>
    public enum ImpactKind : byte
    {
        Bullet = 0,
        Melee = 1,
        Explosion = 2,
    }

    /// <summary>An impact replicated from the host so the other player sees where shots landed.</summary>
    public struct ImpactData : INetworkStruct
    {
        public Vector3 Point;
        /// <summary>Surface normal; zero for a rocket that detonated in mid-air at max range.</summary>
        public Vector3 Normal;
        /// <summary>Bullet/knife: a player was hit. Explosion: the rocket hit a player directly.</summary>
        public NetworkBool HitPlayer;
        /// <summary><see cref="ImpactKind"/> stored as a byte.</summary>
        public byte Kind;
        /// <summary>Catalog index of the weapon that caused the impact.</summary>
        public byte Weapon;
        /// <summary>Approximate muzzle position of the shot (start of the rocket's smoke trail).</summary>
        public Vector3 Origin;
    }

    /// <summary>
    /// Networked weapon handling: loadout, selection, ammo, reload, fire-rate, spread, recoil and hit detection.
    ///
    /// Loadout: every player carries three weapons - the primary chosen in the lobby (RoomState slot weapon),
    /// the pistol and the knife (just two when the primary IS the pistol or the knife). NextWeapon cycles
    /// primary → pistol → knife; a direct request for a weapon outside the loadout is ignored.
    ///
    /// Every shot is validated in simulation: alive, match running, not reloading/equipping, cooldown expired,
    /// ammo in the magazine (knife: no ammo). Damage is computed ONLY on the host, using Fusion lag compensation
    /// (<c>Runner.LagCompensation</c>) so hits are judged against where the target was on the shooter's screen,
    /// and always goes through <see cref="PlayerCharacter.ApplyDamage"/> (the single kill-crediting path).
    ///  - Hitscan: one ray per pellet with distance falloff.
    ///  - Melee: a short fan of rays in front of the player; head and back-stab bonuses.
    ///  - Explosive: instant ray to the impact point, then lag-compensated splash damage with linear falloff
    ///    to every player within the radius that the blast can see; reduced, never-lethal self-damage.
    /// The shooter's device predicts muzzle flash, recoil and impacts for instant feedback.
    /// </summary>
    [RequireComponent(typeof(PlayerCharacter))]
    public sealed class WeaponController : NetworkBehaviour
    {
        public const int SlotCount = WeaponCatalog.MaxWeapons;
        /// <summary>Maximum weapons carried at once (primary, pistol, knife).</summary>
        public const int LoadoutCapacity = 3;
        private const int ImpactBufferSize = 16;
        private const HitOptions RayOptions = HitOptions.IncludePhysX | HitOptions.SubtickAccuracy | HitOptions.IgnoreInputAuthority;

        /// <summary>Melee ray fan around the aim direction: (yaw, pitch) offsets in degrees. Centre first.</summary>
        private static readonly Vector2[] MeleeFan =
        {
            new Vector2(0f, 0f), new Vector2(-9f, 0f), new Vector2(9f, 0f), new Vector2(0f, 7f), new Vector2(0f, -7f),
        };

        /// <summary>Third-person muzzle offset from the eye in aim space (start of the rocket trail on other peers).</summary>
        private static readonly Vector3 MuzzleOffset = new Vector3(0.2f, -0.18f, 0.6f);

        [SerializeField] private WeaponCatalog catalog;
        [SerializeField] private LayerMask hitMask = ~0;
        [Tooltip("Physics layers used for the shooter's cosmetic (predicted) impact ray.")]
        [SerializeField] private LayerMask cosmeticMask = ~0;

        [Networked] public byte CurrentWeapon { get; private set; }
        /// <summary>Catalog index of the primary weapon picked in the lobby (loadout slot 0).</summary>
        [Networked] public byte PrimaryWeapon { get; private set; }
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
        public event Action<Vector3, Vector3, bool> ImpactShown;     // bullets only: (point, normal, hit player)
        public event Action<WeaponDefinition, Vector3, Vector3, bool> MeleeImpactShown;       // (weapon, point, normal, hit player)
        public event Action<WeaponDefinition, Vector3, Vector3, Vector3> ExplosionShown;      // (weapon, origin, point, normal)
        public event Action<WeaponDefinition> Reloaded;
        public event Action<WeaponDefinition> Equipped;
        public event Action<bool, bool> HitConfirmed;                // (headshot, killed)
        public event Action DryFired;

        /// <summary>Any player's explosion shown on this device: (point, radius). Used for camera shake / haptics.</summary>
        public static event Action<Vector3, float> AnyExplosion;

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
        private PlayerCameraRig cameraRig;
        private int environmentMask;
        private int renderedShots, renderedImpacts, renderedReloads, renderedSwitches, renderedHits;
        private readonly Dictionary<PlayerCharacter, (float damage, bool head)> pendingDamage = new Dictionary<PlayerCharacter, (float, bool)>(2);
        private readonly Dictionary<PlayerCharacter, float> splashDistances = new Dictionary<PlayerCharacter, float>(2);
        private readonly List<LagCompensatedHit> overlapHits = new List<LagCompensatedHit>(16);

        private void Awake()
        {
            character = GetComponent<PlayerCharacter>();
            cameraRig = GetComponentInChildren<PlayerCameraRig>(true);
            environmentMask = LayerMask.GetMask(GameConstants.Layers.Environment);
        }

        public override void Spawned()
        {
            if (HasStateAuthority) ResetLoadout();

            renderedShots = ShotCount;
            renderedImpacts = ImpactCount;
            renderedReloads = ReloadCount;
            renderedSwitches = SwitchCount;
            renderedHits = HitConfirmCount;
        }

        // ------------------------------------------------------------------ loadout

        /// <summary>Number of weapons carried: 3, or 2 when the primary is the pistol or the knife.</summary>
        public int LoadoutCount
        {
            get
            {
                int count = 0;
                for (int slot = 0; slot < LoadoutCapacity; slot++)
                {
                    if (LoadoutWeapon(slot) >= 0) count++;
                }
                return count;
            }
        }

        /// <summary>Catalog index of the weapon in loadout slot <paramref name="slot"/> (0 primary, 1 pistol, 2 knife), or -1.</summary>
        public int LoadoutWeapon(int slot)
        {
            if (catalog == null || slot < 0 || Object == null || !Object.IsValid) return -1;
            int found = 0;
            for (int k = 0; k < LoadoutCapacity; k++)
            {
                int index = LoadoutCandidate(k);
                if (!catalog.IsValidIndex(index)) continue;
                bool duplicate = false;
                for (int j = 0; j < k; j++)
                {
                    if (LoadoutCandidate(j) == index) duplicate = true;
                }
                if (duplicate) continue;
                if (found == slot) return index;
                found++;
            }
            return -1;
        }

        /// <summary>Loadout slot holding catalog weapon <paramref name="index"/>, or -1 if it is not carried.</summary>
        public int LoadoutSlotOf(int index)
        {
            for (int slot = 0; slot < LoadoutCapacity; slot++)
            {
                if (LoadoutWeapon(slot) == index) return slot;
            }
            return -1;
        }

        public bool InLoadout(int index) => index >= 0 && LoadoutSlotOf(index) >= 0;

        private int LoadoutCandidate(int k) =>
            k == 0 ? PrimaryWeapon : k == 1 ? WeaponCatalog.SidearmIndex : WeaponCatalog.MeleeIndex;

        private int NextInLoadout()
        {
            int count = LoadoutCount;
            if (count == 0) return -1;
            int slot = LoadoutSlotOf(CurrentWeapon);
            return LoadoutWeapon(slot < 0 ? 0 : (slot + 1) % count);
        }

        /// <summary>Host: the weapon this player picked in the lobby (falls back to the rifle).</summary>
        private int ChosenPrimary()
        {
            int preferred = WeaponCatalog.Rifle;
            if (RoomState.Instance != null && RoomState.Instance.TryGetSlot(Object.InputAuthority, out PlayerSlot slot)) preferred = slot.Weapon;
            return catalog.IsValidIndex(preferred) ? preferred : WeaponCatalog.Rifle;
        }

        /// <summary>
        /// Host: spawn / respawn rule. Re-reads the lobby pick, fills magazines and reserves of the carried weapons
        /// (others are emptied) and puts the primary in hand.
        /// </summary>
        public void ResetLoadout()
        {
            if (!HasStateAuthority) return;
            PrimaryWeapon = (byte)ChosenPrimary();
            for (int i = 0; i < SlotCount; i++)
            {
                WeaponDefinition def = catalog.GetOrNull(i);
                bool carried = def != null && InLoadout(i);
                Magazine.Set(i, (short)(carried ? def.magazineSize : 0));
                Reserve.Set(i, (short)(carried ? def.reserveAmmo : 0));
            }
            ReloadTimer = TickTimer.None;
            FireCooldown = TickTimer.None;
            EquipTimer = TickTimer.None;

            int primary = LoadoutWeapon(0);
            if (primary >= 0 && primary != CurrentWeapon)
            {
                CurrentWeapon = (byte)primary;
                SwitchCount++;
            }
        }

        // ------------------------------------------------------------------ simulation

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

            // --- weapon switching (only within the loadout)
            int requested = input.WeaponRequest - 1;
            if (pressed.IsSet(InputButton.NextWeapon)) requested = NextInLoadout();
            if (requested >= 0 && requested != CurrentWeapon && InLoadout(requested))
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

            if (weapon.usesAmmo && Magazine[CurrentWeapon] <= 0)
            {
                if (!TryStartReload(weapon) && pressed.IsSet(InputButton.Fire) && HasInputAuthority && Runner.IsForward) DryFired?.Invoke();
                return;
            }

            Fire(weapon, input);
        }

        private void Fire(WeaponDefinition weapon, NetworkInputData input)
        {
            if (weapon.usesAmmo) Magazine.Set(CurrentWeapon, (short)(Magazine[CurrentWeapon] - 1));
            FireCooldown = TickTimer.CreateFromSeconds(Runner, weapon.SecondsBetweenShots);
            ShotCount++;
            character.BreakProtection();

            Vector3 origin = character.EyePosition;
            Quaternion aim = Quaternion.Euler(input.Pitch, input.Yaw, 0f);
            float spread = character.IsAiming ? weapon.aimSpread : weapon.hipSpread;
            if (!character.Motor.Grounded) spread *= 1.5f;
            bool predictLocally = HasInputAuthority && Runner.IsForward;
            uint tick = (uint)Runner.Tick.Raw;

            switch (weapon.kind)
            {
                case WeaponKind.Melee:
                    if (HasStateAuthority) ResolveMelee(weapon, origin, aim);
                    if (predictLocally) PredictMelee(weapon, origin, aim);
                    break;

                case WeaponKind.Explosive:
                {
                    Vector3 direction = aim * SpreadDirection(spread, tick, 0u);
                    if (HasStateAuthority) ResolveExplosion(weapon, origin, direction, origin + aim * MuzzleOffset);
                    if (predictLocally) PredictExplosion(weapon, origin, direction, aim);
                    // Single-shot launchers start reloading straight away instead of waiting for another trigger pull.
                    if (weapon.usesAmmo && Magazine[CurrentWeapon] <= 0) TryStartReload(weapon);
                    break;
                }

                default:
                    pendingDamage.Clear();
                    for (int pellet = 0; pellet < weapon.pellets; pellet++)
                    {
                        Vector3 direction = aim * SpreadDirection(spread, tick, (uint)pellet);

                        if (HasStateAuthority) ResolveAuthoritativeHit(weapon, origin, direction);
                        if (predictLocally) PredictImpact(origin, direction, weapon.range);
                    }
                    if (HasStateAuthority) ApplyPendingDamage();
                    break;
            }

            if (predictLocally)
            {
                ShotFired?.Invoke(weapon, true);
                float yawKick = (Hash01(tick, 977u) * 2f - 1f) * weapon.recoilYaw;
                if (LocalInputCollector.Instance != null) LocalInputCollector.Instance.AddRecoil(weapon.recoilPitch, yawKick);
            }
        }

        // ------------------------------------------------------------------ host-authoritative hit resolution

        private PlayerCharacter TargetOf(in LagCompensatedHit hit)
        {
            if (hit.Hitbox == null || hit.Hitbox.Root == null) return null;
            PlayerCharacter target = hit.Hitbox.Root.GetComponent<PlayerCharacter>();
            return target != null && target != character && target.IsAlive ? target : null;
        }

        /// <summary>Host only: lag-compensated ray against hitboxes and level geometry.</summary>
        private void ResolveAuthoritativeHit(WeaponDefinition weapon, Vector3 origin, Vector3 direction)
        {
            bool hasHit = Runner.LagCompensation.Raycast(origin, direction, weapon.range, Object.InputAuthority,
                out LagCompensatedHit hit, hitMask, RayOptions);
            if (!hasHit) return;

            bool hitPlayer = false;
            PlayerCharacter target = TargetOf(hit);
            if (target != null)
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

            RecordImpact(hit.Point, hit.Normal, hitPlayer, ImpactKind.Bullet, origin);
        }

        /// <summary>
        /// Host only: knife. Casts a small fan of lag-compensated rays (weapon.range long) against the same hitboxes
        /// the guns use; the first ray that reaches a player wins. Head = headshotMultiplier, from behind = backstabMultiplier.
        /// </summary>
        private void ResolveMelee(WeaponDefinition weapon, Vector3 origin, Quaternion aim)
        {
            bool haveSurface = false;
            Vector3 surfacePoint = default, surfaceNormal = default;
            for (int i = 0; i < MeleeFan.Length; i++)
            {
                if (!Runner.LagCompensation.Raycast(origin, MeleeDirection(aim, i), weapon.range, Object.InputAuthority,
                        out LagCompensatedHit hit, hitMask, RayOptions)) continue;

                PlayerCharacter target = TargetOf(hit);
                if (target == null)
                {
                    if (i == 0)
                    {
                        haveSurface = true;
                        surfacePoint = hit.Point;
                        surfaceNormal = hit.Normal;
                    }
                    continue;
                }

                HitboxRegion region = hit.Hitbox.GetComponent<HitboxRegion>();
                bool head = region != null && region.IsHead;
                float damage = weapon.damage * (head ? weapon.headshotMultiplier : 1f);
                if (IsBehind(target)) damage *= weapon.backstabMultiplier;

                pendingDamage.Clear();
                pendingDamage[target] = (damage, head);
                ApplyPendingDamage();
                RecordImpact(hit.Point, hit.Normal, true, ImpactKind.Melee, origin);
                return;
            }

            if (haveSurface) RecordImpact(surfacePoint, surfaceNormal, false, ImpactKind.Melee, origin);
        }

        /// <summary>True when the attacker stands behind the target (within ±60° of the target's back).</summary>
        private bool IsBehind(PlayerCharacter target)
        {
            Vector3 toTarget = target.transform.position - transform.position;
            toTarget.y = 0f;
            Vector3 facing = target.transform.forward;
            facing.y = 0f;
            if (toTarget.sqrMagnitude < 0.0001f || facing.sqrMagnitude < 0.0001f) return false;
            return Vector3.Dot(facing.normalized, toTarget.normalized) > 0.5f;
        }

        /// <summary>
        /// Host only: rocket. Instant lag-compensated ray to the impact point (max weapon.range: detonates in mid-air
        /// there), then splash damage around it.
        /// </summary>
        private void ResolveExplosion(WeaponDefinition weapon, Vector3 origin, Vector3 direction, Vector3 muzzle)
        {
            Vector3 point, normal;
            bool direct = false;
            if (Runner.LagCompensation.Raycast(origin, direction, weapon.range, Object.InputAuthority, out LagCompensatedHit hit, hitMask, RayOptions))
            {
                point = hit.Point;
                normal = hit.Normal;
                direct = TargetOf(hit) != null;
            }
            else
            {
                point = origin + direction * weapon.range;
                normal = Vector3.zero;
            }

            RecordImpact(point, direct ? Vector3.zero : normal, direct, ImpactKind.Explosion, muzzle);

            // Line-of-sight checks start slightly in front of the surface so the wall/floor itself never blocks them.
            Vector3 blastOrigin = point + (normal.sqrMagnitude > 0.01f ? normal * 0.2f : -direction * 0.2f);
            ApplySplash(weapon, point, blastOrigin);
        }

        /// <summary>
        /// Host only: splash damage to every other player whose hitboxes overlap the radius (lag-compensated) and are
        /// visible from the blast (environment blocks it). Damage uses the closest visible hitbox centre:
        /// full within explosionFullDamageRadius, then linear down to explosionEdgeMultiplier at the edge.
        /// The shooter takes selfDamageMultiplier of it, never lethal.
        /// </summary>
        private void ApplySplash(WeaponDefinition weapon, Vector3 point, Vector3 blastOrigin)
        {
            if (weapon.explosionRadius <= 0f || weapon.explosionDamage <= 0f) return;

            splashDistances.Clear();
            Runner.LagCompensation.OverlapSphere(point, weapon.explosionRadius, Object.InputAuthority, overlapHits, hitMask,
                HitOptions.SubtickAccuracy | HitOptions.IgnoreInputAuthority);
            for (int i = 0; i < overlapHits.Count; i++)
            {
                LagCompensatedHit hit = overlapHits[i];
                PlayerCharacter target = TargetOf(hit);
                if (target == null) continue;

                Vector3 part = hit.HitboxColliderPosition;
                if (part == Vector3.zero) part = hit.Hitbox.transform.position;
                if (Physics.Linecast(blastOrigin, part, environmentMask, QueryTriggerInteraction.Ignore)) continue;

                float distance = Vector3.Distance(point, part);
                if (!splashDistances.TryGetValue(target, out float best) || distance < best) splashDistances[target] = distance;
            }
            overlapHits.Clear();

            pendingDamage.Clear();
            foreach (KeyValuePair<PlayerCharacter, float> entry in splashDistances)
            {
                float damage = weapon.SplashDamage(entry.Value);
                if (damage > 0f) pendingDamage[entry.Key] = (damage, false);
            }
            splashDistances.Clear();
            ApplyPendingDamage();

            // Self-damage: reduced and never lethal (rocket-jumping into your own death is no fun on a phone).
            if (weapon.selfDamageMultiplier <= 0f || !character.IsAlive) return;
            Vector3 body = character.transform.position + Vector3.up * 1f;
            float selfDistance = Vector3.Distance(point, body);
            if (selfDistance >= weapon.explosionRadius) return;
            if (Physics.Linecast(blastOrigin, body, environmentMask, QueryTriggerInteraction.Ignore)) return;
            float selfDamage = Mathf.Min(weapon.SplashDamage(selfDistance) * weapon.selfDamageMultiplier, character.Health - 1f);
            if (selfDamage >= 1f) character.ApplyDamage(selfDamage, Object.InputAuthority, false);
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

        private void RecordImpact(Vector3 point, Vector3 normal, bool hitPlayer, ImpactKind kind, Vector3 origin)
        {
            Impacts.Set(ImpactCount % ImpactBufferSize, new ImpactData
            {
                Point = point,
                Normal = normal,
                HitPlayer = hitPlayer,
                Kind = (byte)kind,
                Weapon = CurrentWeapon,
                Origin = origin,
            });
            ImpactCount++;
        }

        // ------------------------------------------------------------------ shooter-side prediction (cosmetic only)

        /// <summary>Shooter only, cosmetic: instant impact effect from a plain physics ray (no damage).</summary>
        private void PredictImpact(Vector3 origin, Vector3 direction, float range)
        {
            if (Physics.Raycast(origin, direction, out RaycastHit hit, range, cosmeticMask, QueryTriggerInteraction.Ignore))
            {
                bool isPlayer = hit.collider.GetComponentInParent<PlayerCharacter>() != null;
                ImpactShown?.Invoke(hit.point, hit.normal, isPlayer);
            }
        }

        private void PredictMelee(WeaponDefinition weapon, Vector3 origin, Quaternion aim)
        {
            bool haveSurface = false;
            RaycastHit surface = default;
            for (int i = 0; i < MeleeFan.Length; i++)
            {
                if (!Physics.Raycast(origin, MeleeDirection(aim, i), out RaycastHit hit, weapon.range, cosmeticMask, QueryTriggerInteraction.Ignore)) continue;
                if (hit.collider.GetComponentInParent<PlayerCharacter>() != null)
                {
                    ShowMeleeImpact(weapon, hit.point, hit.normal, true);
                    return;
                }
                if (i == 0)
                {
                    haveSurface = true;
                    surface = hit;
                }
            }
            if (haveSurface) ShowMeleeImpact(weapon, surface.point, surface.normal, false);
        }

        private void PredictExplosion(WeaponDefinition weapon, Vector3 origin, Vector3 direction, Quaternion aim)
        {
            Vector3 point, normal;
            bool direct = false;
            if (Physics.Raycast(origin, direction, out RaycastHit hit, weapon.range, cosmeticMask, QueryTriggerInteraction.Ignore))
            {
                point = hit.point;
                direct = hit.collider.GetComponentInParent<PlayerCharacter>() != null;
                normal = direct ? Vector3.zero : hit.normal;
            }
            else
            {
                point = origin + direction * weapon.range;
                normal = Vector3.zero;
            }

            Transform viewMuzzle = cameraRig != null ? cameraRig.ViewMuzzle : null;
            Vector3 muzzle = viewMuzzle != null ? viewMuzzle.position : origin + aim * MuzzleOffset;
            ShowExplosion(weapon, muzzle, point, normal, direct);
        }

        private static Vector3 MeleeDirection(Quaternion aim, int ray)
        {
            Vector2 offset = MeleeFan[ray];
            return aim * (Quaternion.Euler(offset.y, offset.x, 0f) * Vector3.forward);
        }

        // ------------------------------------------------------------------ reload / equip

        private bool TryStartReload(WeaponDefinition weapon)
        {
            if (!weapon.usesAmmo) return false;
            if (Magazine[CurrentWeapon] >= weapon.magazineSize || Reserve[CurrentWeapon] <= 0 || IsReloading) return false;
            ReloadTimer = TickTimer.CreateFromSeconds(Runner, weapon.reloadSeconds);
            ReloadCount++;
            return true;
        }

        private void CompleteReload()
        {
            WeaponDefinition weapon = Current;
            if (weapon == null || !weapon.usesAmmo) return;
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
                        switch ((ImpactKind)impact.Kind)
                        {
                            case ImpactKind.Melee:
                                ShowMeleeImpact(catalog != null ? catalog.GetOrNull(impact.Weapon) : null, impact.Point, impact.Normal, impact.HitPlayer);
                                break;
                            case ImpactKind.Explosion:
                                ShowExplosion(catalog != null ? catalog.GetOrNull(impact.Weapon) : null, impact.Origin, impact.Point, impact.Normal, impact.HitPlayer);
                                break;
                            default:
                                ImpactShown?.Invoke(impact.Point, impact.Normal, impact.HitPlayer);
                                break;
                        }
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

        private void ShowMeleeImpact(WeaponDefinition weapon, Vector3 point, Vector3 normal, bool hitPlayer)
        {
            WeaponEffects.MeleeImpact(point, normal, hitPlayer);
            MeleeImpactShown?.Invoke(weapon, point, normal, hitPlayer);
        }

        private void ShowExplosion(WeaponDefinition weapon, Vector3 origin, Vector3 point, Vector3 normal, bool hitPlayer)
        {
            WeaponEffects.Explosion(origin, point, normal, hitPlayer);
            ExplosionShown?.Invoke(weapon, origin, point, normal);
            AnyExplosion?.Invoke(point, weapon != null ? weapon.explosionRadius : 4.5f);
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
