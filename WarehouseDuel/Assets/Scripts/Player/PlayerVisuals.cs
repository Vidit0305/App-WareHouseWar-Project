using UnityEngine;
using UnityEngine.Rendering;
using WarehouseWar.Core;
using WarehouseWar.Weapons;

namespace WarehouseWar.Player
{
    /// <summary>
    /// Third-person look of a player, seen by the opponent: team colour, held weapon, procedural walk cycle,
    /// aim pitch, death fall, spawn-protection bubble, muzzle flashes and impact effects.
    /// For the local player the body only casts a shadow (the first-person rig draws the gun instead).
    /// Everything here is presentation: it reads networked state and never changes it.
    /// </summary>
    public sealed class PlayerVisuals : MonoBehaviour
    {
        [SerializeField] private Transform bodyRoot;
        [SerializeField] private Transform legLeft;
        [SerializeField] private Transform legRight;
        [SerializeField] private Transform armPivot;
        [SerializeField] private Transform weaponSocket;
        [SerializeField] private Renderer[] teamColored;
        [SerializeField] private GameObject protectionBubble;
        [SerializeField] private Color[] slotColors =
        {
            new Color(0.2f, 0.55f, 1f), new Color(1f, 0.35f, 0.25f), new Color(0.3f, 0.85f, 0.4f), new Color(1f, 0.75f, 0.15f),
            new Color(0.7f, 0.4f, 1f), new Color(0.2f, 0.9f, 0.95f), new Color(1f, 0.45f, 0.75f), new Color(0.95f, 0.95f, 0.95f),
        };

        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

        private PlayerCharacter character;
        private WeaponController weapons;
        private PlayerCameraRig cameraRig;
        private GameObject worldWeapon;
        private Transform worldMuzzle;
        private WeaponDefinition shownWeapon;
        private float walkPhase;
        private float deathBlend;
        private Vector3 lastPosition;
        private bool isLocal;
        private int appliedSlot = -1;
        private SoldierLook look;

        private void Awake()
        {
            character = GetComponent<PlayerCharacter>();
            weapons = GetComponent<WeaponController>();
            cameraRig = GetComponentInChildren<PlayerCameraRig>(true);
            look = bodyRoot != null ? bodyRoot.GetComponentInChildren<SoldierLook>(true) : null;
        }

        private void OnEnable()
        {
            if (weapons != null)
            {
                weapons.ShotFired += OnShotFired;
                weapons.ImpactShown += OnImpact;
            }
            PlayerCharacter.AnyDied += OnAnyDied;
            PlayerCharacter.AnySpawned += OnAnySpawned;
        }

        private void OnDisable()
        {
            if (weapons != null)
            {
                weapons.ShotFired -= OnShotFired;
                weapons.ImpactShown -= OnImpact;
            }
            PlayerCharacter.AnyDied -= OnAnyDied;
            PlayerCharacter.AnySpawned -= OnAnySpawned;
        }

        private void OnAnySpawned(PlayerCharacter spawned)
        {
            if (spawned != character) return;

            if (character.IsLocal && !isLocal)
            {
                isLocal = true;
                foreach (Renderer r in bodyRoot.GetComponentsInChildren<Renderer>(true)) r.shadowCastingMode = ShadowCastingMode.ShadowsOnly;
                if (cameraRig != null) cameraRig.Activate();
                if (EffectsLibrary.Instance != null) EffectsLibrary.Instance.WarmPools();
            }

            if (EffectsLibrary.Instance != null) EffectPool.Spawn(EffectsLibrary.Instance.spawn, transform.position, Quaternion.identity);
            deathBlend = 0f;
            lastPosition = transform.position;
        }

        private void OnAnyDied(PlayerCharacter victim, Fusion.PlayerRef killer)
        {
            if (victim != character || EffectsLibrary.Instance == null) return;
            EffectPool.Spawn(EffectsLibrary.Instance.elimination, transform.position + Vector3.up, Quaternion.identity);
        }

        private void OnShotFired(WeaponDefinition weapon, bool predicted)
        {
            // The local player's flash is drawn on the view model by PlayerCameraRig.
            if (isLocal || worldMuzzle == null || EffectsLibrary.Instance == null || weapon == null) return;
            PooledEffect flash = EffectPool.Spawn(EffectsLibrary.Instance.muzzleFlash, worldMuzzle.position, worldMuzzle.rotation);
            if (flash != null) flash.transform.localScale = Vector3.one * weapon.muzzleFlashScale;
        }

        private void OnImpact(Vector3 point, Vector3 normal, bool hitPlayer)
        {
            EffectsLibrary fx = EffectsLibrary.Instance;
            if (fx == null) return;
            Quaternion facing = Quaternion.LookRotation(normal.sqrMagnitude > 0.001f ? normal : Vector3.up);
            EffectPool.Spawn(hitPlayer ? fx.impactPlayer : fx.impactEnvironment, point, facing);
            if (!hitPlayer) EffectPool.Spawn(fx.bulletHole, point, facing);
        }

        private void LateUpdate()
        {
            if (character == null || character.Object == null || !character.Object.IsValid) return;
            float dt = Time.deltaTime;

            if (appliedSlot != character.Slot) ApplyTeamColor(character.Slot);
            if (look != null)
            {
                int wanted = WantedCharacter();
                if (look.Applied != wanted)
                {
                    look.Apply(wanted);
                    // The local player only sees their own shadow; newly shown accessories must follow suit.
                    if (isLocal) foreach (Renderer r in bodyRoot.GetComponentsInChildren<Renderer>(true)) r.shadowCastingMode = ShadowCastingMode.ShadowsOnly;
                }
            }
            if (weapons != null && weapons.Current != shownWeapon) SwapWorldWeapon(weapons.Current);

            // Walk cycle from actual movement (works the same for predicted and interpolated players).
            Vector3 position = transform.position;
            Vector3 moved = position - lastPosition;
            lastPosition = position;
            float speed = dt > 0f ? new Vector2(moved.x, moved.z).magnitude / dt : 0f;
            if (speed > 20f) speed = 0f; // teleport
            bool grounded = character.Motor.Grounded;
            walkPhase += dt * speed * 1.6f;
            float swing = grounded ? Mathf.Sin(walkPhase) * Mathf.Clamp01(speed / 5f) * 35f : 15f;
            if (legLeft != null) legLeft.localRotation = Quaternion.Euler(swing, 0f, 0f);
            if (legRight != null) legRight.localRotation = Quaternion.Euler(-swing, 0f, 0f);

            if (armPivot != null) armPivot.localRotation = Quaternion.Euler(character.Pitch, 0f, 0f);

            // Death: tip over backwards and sink a little; respawn snaps back.
            deathBlend = Mathf.MoveTowards(deathBlend, character.IsAlive ? 0f : 1f, dt * (character.IsAlive ? 10f : 3f));
            if (bodyRoot != null)
            {
                bodyRoot.localRotation = Quaternion.Euler(-85f * EaseOut(deathBlend), 0f, 0f);
                bodyRoot.localPosition = new Vector3(0f, -0.15f * deathBlend, 0f);
            }

            if (protectionBubble != null)
            {
                bool shielded = character.IsAlive && character.IsProtected && !isLocal;
                if (protectionBubble.activeSelf != shielded) protectionBubble.SetActive(shielded);
            }
        }

        /// <summary>The owner's character from the room (falls back to my own choice offline).</summary>
        private int WantedCharacter()
        {
            if (WarehouseWar.Multiplayer.RoomState.Instance != null
                && WarehouseWar.Multiplayer.RoomState.Instance.TryGetSlot(character.Owner, out WarehouseWar.Multiplayer.PlayerSlot slot)
                && slot.Occupied)
            {
                return slot.Character;
            }
            return character.IsLocal ? WarehouseWar.Profile.PlayerProfile.CharacterIndex : 0;
        }

        private void ApplyTeamColor(int slot)
        {
            appliedSlot = slot;
            Color color = slotColors[Mathf.Clamp(slot, 0, slotColors.Length - 1)];
            var block = new MaterialPropertyBlock();
            block.SetColor(BaseColorId, color);
            foreach (Renderer r in teamColored)
            {
                if (r != null) r.SetPropertyBlock(block);
            }
        }

        private void SwapWorldWeapon(WeaponDefinition weapon)
        {
            if (worldWeapon != null) Destroy(worldWeapon);
            worldWeapon = null;
            worldMuzzle = null;
            shownWeapon = weapon;
            if (weapon == null || weapon.worldModelPrefab == null || weaponSocket == null) return;

            worldWeapon = Instantiate(weapon.worldModelPrefab, weaponSocket, false);
            worldWeapon.transform.localScale = Vector3.one * WeaponDefinition.HeldScale;
            foreach (Transform t in worldWeapon.GetComponentsInChildren<Transform>(true))
            {
                t.gameObject.layer = gameObject.layer;
                if (t.name == "Muzzle") worldMuzzle = t;
            }
            foreach (Renderer r in worldWeapon.GetComponentsInChildren<Renderer>(true))
            {
                r.shadowCastingMode = isLocal ? ShadowCastingMode.ShadowsOnly : ShadowCastingMode.On;
            }
        }

        private static float EaseOut(float t) => 1f - (1f - t) * (1f - t);
    }
}
