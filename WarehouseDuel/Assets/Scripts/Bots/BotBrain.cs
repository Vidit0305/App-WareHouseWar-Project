using Fusion;
using UnityEngine;
using UnityEngine.AI;
using WarehouseWar.Core;
using WarehouseWar.Match;
using WarehouseWar.Multiplayer;
using WarehouseWar.Player;
using WarehouseWar.Weapons;

namespace WarehouseWar.Bots
{
    /// <summary>
    /// Practice-bot "player": runs on the host only and produces the same <see cref="NetworkInputData"/> a human would,
    /// once per simulation tick, so bots use exactly the player movement, weapon and damage code.
    ///
    /// Behaviour: roam / hunt along a runtime navigation mesh, pick the nearest visible enemy (everyone is an enemy),
    /// react after a human-like delay, aim with an error that shrinks while tracking, fire in bursts, strafe, keep a
    /// weapon-appropriate distance, knife up close, reload when empty or idle, switch weapons now and then.
    /// Expensive work is throttled: senses (line of sight) ~5 Hz, path updates ~2 Hz, staggered per bot.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class BotBrain : MonoBehaviour
    {
        private const float SenseInterval = 0.2f;
        private const float RepathInterval = 0.5f;
        private const float SightRange = 70f;
        private const float AwarenessRadius = 10f;   // enemies this close are noticed even behind the bot
        private const float FieldOfView = 140f;
        private const float MemorySeconds = 4f;

        private PlayerCharacter character;
        private WeaponController weapons;
        private int environmentMask;

        // Personality (per bot).
        private float reactionMin, reactionMax, aimErrorStart, aimErrorFloor, aimSettleRate, turnSpeed, headshotBias;

        // Input cache: computed once per tick, read by every behaviour on the character.
        private int cachedTick = -1;
        private NetworkInputData cachedInput;

        // Look.
        private float yaw, pitch;
        private int seenSpawnCount = -1;

        // Targeting.
        private PlayerCharacter target;
        private bool targetVisible;
        private float nextSenseTime;
        private float reactionReadyTime;
        private Vector2 aimError;
        private Vector3 lastSeenPosition;
        private float lastSeenTime = -100f;

        // Firing.
        private bool fireHeld;
        private float burstEndTime, burstPauseEndTime;
        private bool reloadPulse;

        // Weapons.
        private int desiredWeapon = -1;
        private float nextWeaponThinkTime;
        private bool preferKnife;

        // Movement.
        private readonly Vector3[] corners = new Vector3[32];
        private NavMeshPath path;
        private int cornerCount, cornerIndex;
        private Vector3 destination;
        private bool hasDestination;
        private float nextRepathTime;
        private float strafeSign = 1f;
        private float nextStrafeSwitch;
        private float preferredDistanceJitter;
        private Vector3 stuckCheckPosition;
        private float nextStuckCheck;
        private bool jumpPulse;
        private float nextRoamTime;

        private void Awake()
        {
            character = GetComponent<PlayerCharacter>();
            weapons = GetComponent<WeaponController>();
            environmentMask = LayerMask.GetMask(GameConstants.Layers.Environment);
            if (environmentMask == 0) environmentMask = Physics.DefaultRaycastLayers;
            path = new NavMeshPath();

            // Tuned to be beatable: slower reactions, wider and slower-settling aim error, slower turning.
            reactionMin = Random.Range(0.45f, 0.6f);
            reactionMax = reactionMin + Random.Range(0.25f, 0.45f);
            aimErrorStart = Random.Range(9f, 15f);
            aimErrorFloor = Random.Range(1.4f, 2.6f);
            aimSettleRate = Random.Range(1.0f, 1.6f);
            turnSpeed = Random.Range(160f, 240f);
            headshotBias = Random.Range(0f, 0.15f);
            preferredDistanceJitter = Random.Range(-2f, 2f);
            strafeSign = Random.value < 0.5f ? -1f : 1f;
            nextSenseTime = Random.Range(0f, SenseInterval);
            yaw = transform.eulerAngles.y;
        }

        /// <summary>Host: this tick's input for the bot's character (the same value for every caller in one tick).</summary>
        public bool TryGetInput(out NetworkInputData input)
        {
            NetworkRunner runner = character != null ? character.Runner : null;
            if (runner == null)
            {
                input = default;
                return false;
            }

            int tick = runner.Tick.Raw;
            if (tick != cachedTick)
            {
                cachedTick = tick;
                cachedInput = Think((float)runner.SimulationTime, (float)runner.DeltaTime);
            }
            input = cachedInput;
            return true;
        }

        // ------------------------------------------------------------------ decision making

        private NetworkInputData Think(float now, float dt)
        {
            var input = new NetworkInputData();

            // Respawned (or first tick): face the spawn direction and forget everything.
            if (character.SpawnCount != seenSpawnCount)
            {
                seenSpawnCount = character.SpawnCount;
                yaw = transform.eulerAngles.y;
                pitch = 0f;
                target = null;
                targetVisible = false;
                hasDestination = false;
                cornerCount = 0;
                fireHeld = false;
                lastSeenTime = -100f;
                desiredWeapon = weapons != null ? weapons.CurrentWeapon : -1;
                nextRoamTime = now;
                stuckCheckPosition = transform.position;
                nextStuckCheck = now + 1f;
            }

            if (!character.IsAlive)
            {
                target = null;
                fireHeld = false;
                input.Yaw = yaw;
                input.Pitch = pitch;
                return input;
            }

            bool fighting = MatchManager.GameplayAllowed;
            if (fighting && now >= nextSenseTime)
            {
                nextSenseTime = now + SenseInterval;
                Sense(now);
            }
            else if (!fighting)
            {
                target = null;
                targetVisible = false;
            }

            WeaponDefinition weapon = weapons != null ? weapons.Current : null;
            float distance = target != null ? Vector3.Distance(transform.position, target.transform.position) : float.MaxValue;

            if (fighting) ThinkWeapons(now, weapon, distance);

            // --- look
            Vector3 move = Vector3.zero;
            bool engaged = target != null && targetVisible;
            if (engaged)
            {
                AimAt(target, weapon, dt);
            }

            // --- movement (world-space direction)
            if (fighting)
            {
                move = engaged ? CombatMove(now, weapon, distance) : NavigateMove(now);
                if (!engaged)
                {
                    // Look where we are going (or toward where the enemy was last seen).
                    Vector3 lookDir = move.sqrMagnitude > 0.01f ? move : transform.forward;
                    if (now - lastSeenTime < MemorySeconds) lookDir = lastSeenPosition - transform.position;
                    lookDir.y = 0f;
                    if (lookDir.sqrMagnitude > 0.01f)
                    {
                        float wantedYaw = Mathf.Atan2(lookDir.x, lookDir.z) * Mathf.Rad2Deg;
                        yaw = Mathf.MoveTowardsAngle(yaw, wantedYaw, turnSpeed * 0.75f * dt);
                    }
                    pitch = Mathf.MoveTowards(pitch, 0f, 90f * dt);
                }
                CheckStuck(now, move);
            }

            input.Yaw = Mathf.Repeat(yaw, 360f);
            input.Pitch = Mathf.Clamp(pitch, -LocalInputCollector.PitchLimit, LocalInputCollector.PitchLimit);

            // World move → local stick input relative to the body's facing.
            if (move.sqrMagnitude > 0.0001f)
            {
                Vector3 local = Quaternion.Euler(0f, -input.Yaw, 0f) * move;
                input.Move = Vector2.ClampMagnitude(new Vector2(local.x, local.z), 1f);
            }

            // --- buttons
            bool fire = fighting && engaged && WantsToFire(now, weapon, distance);
            bool aim = fighting && engaged && weapon != null && !weapon.IsMelee && (weapon.hasScope || distance > 18f);
            bool sprint = fighting && !engaged && input.Move.y > 0.6f;
            input.Buttons.Set(InputButton.Fire, fire);
            input.Buttons.Set(InputButton.Aim, aim);
            input.Buttons.Set(InputButton.Sprint, sprint);
            input.Buttons.Set(InputButton.Jump, jumpPulse);
            input.Buttons.Set(InputButton.Reload, reloadPulse && (cachedTick & 1) == 0); // pulse so it registers as a fresh press
            jumpPulse = false;
            reloadPulse = false;

            input.WeaponRequest = desiredWeapon >= 0 ? (byte)(desiredWeapon + 1) : (byte)0;
            return input;
        }

        // ------------------------------------------------------------------ senses

        private void Sense(float now)
        {
            Vector3 eye = character.EyePosition;
            Vector3 forward = Quaternion.Euler(0f, yaw, 0f) * Vector3.forward;
            PlayerCharacter best = null;
            float bestScore = float.MaxValue;
            bool currentStillVisible = false;

            foreach (PlayerCharacter other in PlayerCharacter.All)
            {
                if (other == null || other == character || !other.IsAlive || other.Object == null || !other.Object.IsValid) continue;
                if (other.IsProtected) continue;

                Vector3 toOther = other.transform.position - transform.position;
                float distance = toOther.magnitude;
                if (distance > SightRange) continue;

                // Only notice what is in front of us (or very close / already being fought).
                bool isCurrent = other == target;
                if (!isCurrent && distance > AwarenessRadius)
                {
                    Vector3 flat = new Vector3(toOther.x, 0f, toOther.z);
                    if (flat.sqrMagnitude > 0.01f && Vector3.Angle(forward, flat) > FieldOfView * 0.5f) continue;
                }

                if (!CanSee(eye, other)) continue;

                // Prefer the current target (sticky), then the closest.
                float score = distance - (isCurrent ? 8f : 0f);
                if (isCurrent) currentStillVisible = true;
                if (score < bestScore)
                {
                    bestScore = score;
                    best = other;
                }
            }

            if (best != null)
            {
                if (best != target || !targetVisible)
                {
                    // New target (or re-acquired): human reaction delay and a fresh, larger aim error.
                    bool reacquired = best == target && now - lastSeenTime < 1f;
                    reactionReadyTime = now + (reacquired ? Random.Range(0.08f, 0.2f) : Random.Range(reactionMin, reactionMax));
                    float error = reacquired ? aimErrorStart * 0.4f : aimErrorStart;
                    aimError = Random.insideUnitCircle.normalized * Random.Range(error * 0.6f, error);
                    burstPauseEndTime = reactionReadyTime;
                    preferKnife = Random.value < 0.5f;
                }
                target = best;
                targetVisible = true;
                lastSeenPosition = best.transform.position;
                lastSeenTime = now;
                hasDestination = false; // re-plan movement after the fight
            }
            else
            {
                if (target != null && !currentStillVisible) targetVisible = false;
                if (target != null && (!target.IsAlive || now - lastSeenTime > MemorySeconds)) target = null;
            }
        }

        private bool CanSee(Vector3 eye, PlayerCharacter other)
        {
            Vector3 chest = other.transform.position + Vector3.up * 1.2f;
            if (!Physics.Linecast(eye, chest, environmentMask, QueryTriggerInteraction.Ignore)) return true;
            Vector3 head = other.EyePosition;
            return !Physics.Linecast(eye, head, environmentMask, QueryTriggerInteraction.Ignore);
        }

        // ------------------------------------------------------------------ aim & fire

        private void AimAt(PlayerCharacter enemy, WeaponDefinition weapon, float dt)
        {
            // Aim error shrinks while tracking, never fully to zero.
            float settle = Mathf.Exp(-aimSettleRate * dt);
            aimError *= settle;
            if (aimError.magnitude < aimErrorFloor) aimError = aimError.normalized * aimErrorFloor + Random.insideUnitCircle * 0.05f;

            float height = weapon != null && weapon.IsExplosive ? 0.35f : Mathf.Lerp(1.15f, 1.55f, headshotBias);
            Vector3 point = enemy.transform.position + Vector3.up * height;
            // Lead moving targets a little (imperfectly).
            point += enemy.Motor.Velocity * 0.06f;

            Vector3 toPoint = point - character.EyePosition;
            float flat = new Vector2(toPoint.x, toPoint.z).magnitude;
            float wantedYaw = Mathf.Atan2(toPoint.x, toPoint.z) * Mathf.Rad2Deg + aimError.x;
            float wantedPitch = -Mathf.Atan2(toPoint.y, Mathf.Max(flat, 0.01f)) * Mathf.Rad2Deg + aimError.y * 0.6f;

            float step = turnSpeed * dt;
            // Fast flick when far off, finer tracking when close to the target direction.
            float yawGap = Mathf.Abs(Mathf.DeltaAngle(yaw, wantedYaw));
            if (yawGap > 30f) step *= 1.6f;
            yaw = Mathf.MoveTowardsAngle(yaw, wantedYaw, step);
            pitch = Mathf.MoveTowards(pitch, wantedPitch, step * 0.7f);
        }

        private bool WantsToFire(float now, WeaponDefinition weapon, float distance)
        {
            if (weapon == null || target == null || now < reactionReadyTime) return false;
            if (weapons.IsReloading) return false;

            // Only shoot roughly on target: tolerance depends on the target's apparent size.
            Vector3 toTarget = target.transform.position + Vector3.up * 1.2f - character.EyePosition;
            float wantedYaw = Mathf.Atan2(toTarget.x, toTarget.z) * Mathf.Rad2Deg;
            float tolerance = Mathf.Clamp(Mathf.Atan2(0.9f, Mathf.Max(distance, 0.5f)) * Mathf.Rad2Deg, 2f, 35f) + 2f;
            if (Mathf.Abs(Mathf.DeltaAngle(yaw, wantedYaw)) > tolerance) return false;

            if (weapon.IsMelee) return distance <= weapon.range + 0.4f && Toggle(now, 0.15f, 0.35f);
            if (weapon.IsExplosive && distance < weapon.explosionRadius + 1f) return false;
            float maxRange = weapon.hasScope ? weapon.range : Mathf.Min(weapon.range, Mathf.Max(weapon.falloffEnd * 1.2f, 25f));
            if (distance > maxRange) return false;

            if (weapon.fireMode != FireMode.Automatic)
            {
                // Semi-auto: a fresh trigger pull each shot, with a human cadence.
                float cadence = weapon.SecondsBetweenShots;
                return Toggle(now, cadence * 0.5f, cadence * 0.5f + Random.Range(0.15f, weapon.hasScope ? 0.9f : 0.45f));
            }

            // Automatic: bursts with short pauses (longer bursts up close).
            if (now < burstPauseEndTime) return false;
            if (!fireHeld)
            {
                fireHeld = true;
                float burst = distance < 10f ? Random.Range(0.3f, 0.7f) : Random.Range(0.15f, 0.35f);
                burstEndTime = now + burst;
            }
            if (now >= burstEndTime)
            {
                fireHeld = false;
                burstPauseEndTime = now + Random.Range(0.3f, distance < 10f ? 0.55f : 0.8f);
                return false;
            }
            return true;
        }

        private float toggleUntil;
        private bool toggleState;

        /// <summary>Alternates the trigger on/off (semi-auto weapons need a release between shots).</summary>
        private bool Toggle(float now, float onSeconds, float offSeconds)
        {
            if (now >= toggleUntil)
            {
                toggleState = !toggleState;
                toggleUntil = now + (toggleState ? Mathf.Max(onSeconds, 0.04f) : Mathf.Max(offSeconds, 0.04f));
            }
            return toggleState;
        }

        // ------------------------------------------------------------------ weapons

        private void ThinkWeapons(float now, WeaponDefinition weapon, float distance)
        {
            if (weapons == null || weapon == null) return;
            if (desiredWeapon < 0) desiredWeapon = weapons.CurrentWeapon;

            bool engaged = target != null && targetVisible;
            int current = weapons.CurrentWeapon;
            bool empty = weapon.usesAmmo && weapons.CurrentMagazine <= 0;
            bool dry = empty && weapons.CurrentReserve <= 0;

            // Out of ammo entirely: change weapon now.
            if (dry)
            {
                int other = BestGun(current, distance);
                desiredWeapon = other >= 0 ? other : KnifeIndex();
                return;
            }

            // Empty magazine: reload (the weapon code also auto-reloads on the next trigger pull).
            if (empty && !weapons.IsReloading)
            {
                reloadPulse = true;
            }

            if (now < nextWeaponThinkTime) return;
            nextWeaponThinkTime = now + Random.Range(0.6f, 1.2f);

            int knife = KnifeIndex();
            if (engaged)
            {
                if (knife >= 0 && preferKnife && distance < 2.6f)
                {
                    desiredWeapon = knife;
                }
                else if (weapon.IsMelee && distance > 3.5f)
                {
                    int gun = BestGun(-1, distance);
                    if (gun >= 0) desiredWeapon = gun;
                }
                else if (weapon.IsExplosive && distance < weapon.explosionRadius + 1f)
                {
                    int gun = BestGun(current, distance);
                    if (gun >= 0) desiredWeapon = gun;
                }
                return;
            }

            // Not fighting: top up a partly empty magazine, put the knife away, and now and then try another weapon.
            if (weapon.usesAmmo && weapons.CurrentMagazine < weapon.magazineSize * 0.5f && weapons.CurrentReserve > 0 && !weapons.IsReloading)
            {
                reloadPulse = true;
            }
            if (weapon.IsMelee)
            {
                int gun = BestGun(-1, 15f);
                if (gun >= 0) desiredWeapon = gun;
            }
            else if (Random.value < 0.06f)
            {
                int count = weapons.LoadoutCount;
                int pick = weapons.LoadoutWeapon(Random.Range(0, Mathf.Max(1, count)));
                WeaponDefinition def = weapons.Catalog != null ? weapons.Catalog.GetOrNull(pick) : null;
                if (def != null && !def.IsMelee && HasAmmo(pick)) desiredWeapon = pick;
            }
        }

        private int KnifeIndex()
        {
            for (int slot = 0; slot < WeaponController.LoadoutCapacity; slot++)
            {
                int index = weapons.LoadoutWeapon(slot);
                WeaponDefinition def = weapons.Catalog != null ? weapons.Catalog.GetOrNull(index) : null;
                if (def != null && def.IsMelee) return index;
            }
            return -1;
        }

        private bool HasAmmo(int index)
        {
            if (index < 0) return false;
            WeaponDefinition def = weapons.Catalog != null ? weapons.Catalog.GetOrNull(index) : null;
            if (def == null) return false;
            return !def.usesAmmo || weapons.Magazine[index] > 0 || weapons.Reserve[index] > 0;
        }

        /// <summary>Best carried gun with ammo for this distance (excluding <paramref name="exclude"/>), or -1.</summary>
        private int BestGun(int exclude, float distance)
        {
            int best = -1;
            float bestScore = float.MinValue;
            for (int slot = 0; slot < WeaponController.LoadoutCapacity; slot++)
            {
                int index = weapons.LoadoutWeapon(slot);
                if (index < 0 || index == exclude || !HasAmmo(index)) continue;
                WeaponDefinition def = weapons.Catalog.GetOrNull(index);
                if (def == null || def.IsMelee) continue;
                if (def.IsExplosive && distance < def.explosionRadius + 1f) continue;
                // Prefer the loadout order (primary first) and weapons whose range suits the distance.
                float score = -slot;
                if (distance > def.falloffEnd && !def.hasScope) score -= 2f;
                if (def.hasScope && distance < 8f) score -= 2f;
                if (score > bestScore)
                {
                    bestScore = score;
                    best = index;
                }
            }
            return best;
        }

        // ------------------------------------------------------------------ movement

        /// <summary>Preferred fighting distance for the weapon in hand.</summary>
        private float PreferredDistance(WeaponDefinition weapon)
        {
            if (weapon == null) return 10f;
            if (weapon.IsMelee) return 1.2f;
            if (weapon.hasScope) return 24f + preferredDistanceJitter;
            if (weapon.IsExplosive) return 14f + preferredDistanceJitter;
            if (weapon.pellets > 1) return 5f + preferredDistanceJitter * 0.5f;
            return Mathf.Clamp(weapon.falloffStart * 0.6f, 7f, 16f) + preferredDistanceJitter;
        }

        private Vector3 CombatMove(float now, WeaponDefinition weapon, float distance)
        {
            Vector3 toTarget = target.transform.position - transform.position;
            toTarget.y = 0f;
            Vector3 forward = toTarget.sqrMagnitude > 0.01f ? toTarget.normalized : transform.forward;
            Vector3 right = new Vector3(forward.z, 0f, -forward.x);

            if (now >= nextStrafeSwitch)
            {
                if (Random.value < 0.7f) strafeSign = -strafeSign;
                nextStrafeSwitch = now + Random.Range(0.5f, 1.4f);
                if (Random.value < 0.08f && character.Motor.Grounded) jumpPulse = true;
            }

            float preferred = PreferredDistance(weapon);
            float approach = 0f;
            if (distance > preferred + 2f) approach = 1f;
            else if (distance < preferred - 2f) approach = -0.7f;

            float strafe = weapon != null && weapon.hasScope ? 0.45f : 0.85f;
            Vector3 move = forward * approach + right * (strafeSign * strafe);

            // Closing a long distance: follow the navigation mesh toward the target instead of walking into walls.
            if (approach > 0f && distance > preferred + 6f)
            {
                Vector3 nav = NavigateTo(now, target.transform.position);
                if (nav.sqrMagnitude > 0.01f) move = nav + right * (strafeSign * 0.35f);
            }

            // Don't strafe into walls or off ledges.
            if (Blocked(move))
            {
                strafeSign = -strafeSign;
                move = forward * approach + right * (strafeSign * strafe);
                if (Blocked(move)) move = forward * approach;
            }
            return Vector3.ClampMagnitude(move, 1f);
        }

        private Vector3 NavigateMove(float now)
        {
            // Chase where the enemy was last seen; otherwise hunt (head toward someone) or roam.
            if (target != null && now - lastSeenTime < MemorySeconds)
            {
                return NavigateTo(now, lastSeenPosition);
            }

            if (!hasDestination || now >= nextRoamTime || ReachedDestination())
            {
                PickDestination(now);
            }
            return NavigateTo(now, destination);
        }

        private void PickDestination(float now)
        {
            hasDestination = true;
            nextRoamTime = now + Random.Range(6f, 12f);

            // 60%: hunt a random living enemy (keeps the action going); otherwise roam to a random point.
            if (Random.value < 0.6f)
            {
                int count = PlayerCharacter.All.Count;
                for (int attempt = 0; attempt < 4 && count > 0; attempt++)
                {
                    PlayerCharacter other = PlayerCharacter.All[Random.Range(0, count)];
                    if (other == null || other == character || !other.IsAlive) continue;
                    destination = other.transform.position + new Vector3(Random.Range(-4f, 4f), 0f, Random.Range(-4f, 4f));
                    if (BotNavigation.Snap(destination, 4f, out Vector3 snapped)) destination = snapped;
                    return;
                }
            }

            if (BotNavigation.RandomPoint(out Vector3 point)) destination = point;
            else destination = transform.position + new Vector3(Random.Range(-12f, 12f), 0f, Random.Range(-12f, 12f));
        }

        private bool ReachedDestination()
        {
            Vector3 d = destination - transform.position;
            d.y = 0f;
            return d.sqrMagnitude < 2.5f * 2.5f;
        }

        /// <summary>World-space direction along the navigation path toward <paramref name="goal"/> (re-planned ~2 Hz).</summary>
        private Vector3 NavigateTo(float now, Vector3 goal)
        {
            Vector3 position = transform.position;
            if (BotNavigation.Ready)
            {
                if (now >= nextRepathTime || cornerIndex >= cornerCount)
                {
                    nextRepathTime = now + RepathInterval + Random.Range(0f, 0.15f);
                    cornerCount = 0;
                    cornerIndex = 0;
                    if (BotNavigation.Snap(position, 2f, out Vector3 start) && BotNavigation.Snap(goal, 4f, out Vector3 end)
                        && NavMesh.CalculatePath(start, end, NavMesh.AllAreas, path) && path.status != NavMeshPathStatus.PathInvalid)
                    {
                        cornerCount = path.GetCornersNonAlloc(corners);
                        cornerIndex = cornerCount > 1 ? 1 : 0;
                    }
                }

                while (cornerIndex < cornerCount)
                {
                    Vector3 toCorner = corners[cornerIndex] - position;
                    toCorner.y = 0f;
                    if (toCorner.sqrMagnitude > 0.6f * 0.6f) return toCorner.normalized;
                    cornerIndex++;
                }
                if (cornerCount > 0) return Vector3.zero; // arrived
            }

            // No navigation mesh / no path: steer straight at the goal, sliding along walls.
            Vector3 direct = goal - position;
            direct.y = 0f;
            if (direct.sqrMagnitude < 1f) return Vector3.zero;
            direct.Normalize();
            if (Blocked(direct))
            {
                Vector3 side = new Vector3(direct.z, 0f, -direct.x) * strafeSign;
                if (Blocked(side)) side = -side;
                return side;
            }
            return direct;
        }

        /// <summary>True when moving along <paramref name="direction"/> would soon hit a wall or leave the walkable area.</summary>
        private bool Blocked(Vector3 direction)
        {
            direction.y = 0f;
            if (direction.sqrMagnitude < 0.0001f) return false;
            direction.Normalize();
            Vector3 position = transform.position;
            if (BotNavigation.Ready)
            {
                return NavMesh.Raycast(position, position + direction * 1.4f, out NavMeshHit _, NavMesh.AllAreas);
            }
            return Physics.Raycast(position + Vector3.up * 0.6f, direction, 1.2f, environmentMask, QueryTriggerInteraction.Ignore);
        }

        /// <summary>If the bot has barely moved for a second while trying to, jump and pick a new route.</summary>
        private void CheckStuck(float now, Vector3 move)
        {
            if (now < nextStuckCheck) return;
            nextStuckCheck = now + 1f;
            Vector3 moved = transform.position - stuckCheckPosition;
            moved.y = 0f;
            stuckCheckPosition = transform.position;
            if (move.sqrMagnitude > 0.25f && moved.sqrMagnitude < 0.3f * 0.3f)
            {
                if (character.Motor.Grounded) jumpPulse = true;
                strafeSign = -strafeSign;
                hasDestination = false;
                nextRepathTime = now;
            }
        }
    }
}
