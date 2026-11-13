using UnityEngine;
using WarehouseDuel.Core;
using WarehouseDuel.Weapons;

namespace WarehouseDuel.Player
{
    /// <summary>
    /// Local player's first-person camera and weapon view model (presentation only).
    /// Look angles come straight from the input collector every frame (smooth at any frame rate);
    /// position follows the character's interpolated transform. Adds head bob, aim zoom, recoil kick,
    /// weapon sway, reload/equip motion and a death camera. Disabled on remote players.
    ///
    /// The view model is kept within ~0.35 m of the camera, inside the character capsule (radius 0.4 m),
    /// so it can never poke through walls — no extra overlay camera needed (cheaper on mobile).
    /// </summary>
    public sealed class PlayerCameraRig : MonoBehaviour
    {
        [SerializeField] private Camera playerCamera;
        [SerializeField] private Transform viewModelSocket;
        [SerializeField] private float baseFieldOfView = 72f;
        [SerializeField] private float viewModelScale = 0.58f;
        [SerializeField] private Vector3 hipOffset = new Vector3(0.15f, -0.17f, 0.21f);
        [SerializeField] private Vector3 aimOffset = new Vector3(0f, -0.11f, 0.16f);
        [Tooltip("First-person hand + sleeve, placed on the grip and on the weapon's 'Handguard' marker.")]
        [SerializeField] private GameObject handPrefab;

        private PlayerCharacter character;
        private WeaponController weapons;
        private GameObject viewModel;
        private Vector3 weaponOffset;
        private Transform viewMuzzle;
        private WeaponDefinition shownWeapon;

        private float bobPhase;
        private float kick;            // view model kick back (m)
        private float cameraKick;      // visual camera pitch kick (deg), recovers
        private float reloadDip;       // 0..1
        private float equipDip;        // 0..1
        private float aimBlend;        // 0..1
        private Vector2 sway;
        private float lastYaw, lastPitch;
        private float deathBlend;
        private float tuck;
        private int environmentMask;
        private Vector3 smoothedPosition;
        private int firstPersonLayer;

        public Transform ViewMuzzle => viewMuzzle;
        public Camera Camera => playerCamera;

        private void Awake()
        {
            character = GetComponentInParent<PlayerCharacter>();
            weapons = GetComponentInParent<WeaponController>();
            firstPersonLayer = LayerMask.NameToLayer(GameConstants.Layers.FirstPersonView);
            environmentMask = LayerMask.GetMask(GameConstants.Layers.Environment);
            if (playerCamera != null) playerCamera.gameObject.SetActive(false);
            enabled = false; // only the local player's rig runs (see Activate)
        }

        /// <summary>Called by PlayerCharacter presentation when this is the local player.</summary>
        public void Activate()
        {
            if (playerCamera == null) return;
            playerCamera.gameObject.SetActive(true);
            playerCamera.fieldOfView = baseFieldOfView;
            playerCamera.transform.SetParent(null, true); // free from the body: no interpolation jitter from the hierarchy
            smoothedPosition = character.transform.position;

            // Turn off the map's overview camera while we own the view.
            foreach (Camera cam in Camera.allCameras)
            {
                if (cam != playerCamera && cam.CompareTag("MainCamera")) cam.gameObject.SetActive(false);
            }
            playerCamera.tag = "MainCamera";

            weapons.ShotFired += OnShotFired;
            weapons.Reloaded += _ => reloadDip = 1f;
            weapons.Equipped += _ => equipDip = 1f;
            enabled = true;
        }

        private void OnDestroy()
        {
            if (weapons != null) weapons.ShotFired -= OnShotFired;
            if (playerCamera != null && playerCamera.transform.parent == null) Destroy(playerCamera.gameObject);
        }

        private void OnShotFired(WeaponDefinition weapon, bool predicted)
        {
            if (!predicted || weapon == null) return;
            kick = Mathf.Min(kick + weapon.viewKick, 0.12f);
            cameraKick = Mathf.Min(cameraKick + weapon.recoilPitch * 0.35f, 6f);
            if (viewMuzzle != null && EffectsLibrary.Instance != null)
            {
                PooledEffect flash = EffectPool.Spawn(EffectsLibrary.Instance.muzzleFlash, viewMuzzle.position, viewMuzzle.rotation, viewMuzzle);
                if (flash != null)
                {
                    flash.gameObject.layer = firstPersonLayer;
                    flash.transform.localScale = Vector3.one * (weapon.muzzleFlashScale * 0.6f);
                }
            }
        }

        private void LateUpdate()
        {
            if (playerCamera == null || character == null || character.Object == null || !character.Object.IsValid) return;

            LocalInputCollector input = LocalInputCollector.Instance;
            float dt = Time.deltaTime;
            float yaw = input != null ? input.Yaw : character.Yaw;
            float pitch = input != null ? input.Pitch : character.Pitch;

            // --- position (slight smoothing removes tick-to-frame stepping on low-FPS phones)
            Vector3 target = character.transform.position;
            smoothedPosition = Vector3.Lerp(smoothedPosition, target, 1f - Mathf.Exp(-30f * dt));
            if ((smoothedPosition - target).sqrMagnitude > 4f) smoothedPosition = target; // teleport

            Vector3 velocity = character.Motor.Velocity;
            float horizontalSpeed = new Vector2(velocity.x, velocity.z).magnitude;
            bool grounded = character.Motor.Grounded;
            if (grounded && horizontalSpeed > 0.5f) bobPhase += dt * horizontalSpeed * 2.1f;
            float bobAmount = grounded ? Mathf.Clamp01(horizontalSpeed / 7f) * (1f - aimBlend * 0.8f) : 0f;
            Vector3 bob = new Vector3(Mathf.Cos(bobPhase) * 0.025f, Mathf.Abs(Mathf.Sin(bobPhase)) * 0.045f, 0f) * bobAmount;

            deathBlend = Mathf.MoveTowards(deathBlend, character.IsAlive ? 0f : 1f, dt * (character.IsAlive ? 6f : 2.5f));
            Vector3 eye = smoothedPosition + Vector3.up * Mathf.Lerp(PlayerCharacter.EyeHeight, 0.35f, deathBlend);

            cameraKick = Mathf.MoveTowards(cameraKick, 0f, dt * 18f);
            Quaternion look = Quaternion.Euler(pitch - cameraKick, yaw, Mathf.Lerp(0f, 35f, deathBlend));
            playerCamera.transform.SetPositionAndRotation(eye + look * bob, look);

            // --- zoom
            WeaponDefinition weapon = weapons != null ? weapons.Current : null;
            bool aiming = character.IsAiming && character.IsAlive;
            aimBlend = Mathf.MoveTowards(aimBlend, aiming ? 1f : 0f, dt * 7f);
            float aimFov = weapon != null ? weapon.aimFieldOfView : 55f;
            float sprintFov = character.IsSprinting ? 4f : 0f;
            playerCamera.fieldOfView = Mathf.Lerp(baseFieldOfView + sprintFov, aimFov, aimBlend);

            UpdateViewModel(weapon, yaw, pitch, dt, bob);
        }

        private void UpdateViewModel(WeaponDefinition weapon, float yaw, float pitch, float dt, Vector3 bob)
        {
            if (viewModelSocket == null) return;
            if (viewModelSocket.parent != playerCamera.transform) viewModelSocket.SetParent(playerCamera.transform, false);

            if (weapon != shownWeapon) SwapViewModel(weapon);
            if (viewModel == null) return;

            float yawDelta = Mathf.DeltaAngle(lastYaw, yaw);
            float pitchDelta = pitch - lastPitch;
            lastYaw = yaw;
            lastPitch = pitch;
            sway = Vector2.Lerp(sway, new Vector2(-yawDelta, pitchDelta) * 0.0025f, 1f - Mathf.Exp(-12f * dt));
            sway = Vector2.ClampMagnitude(sway, 0.03f);

            kick = Mathf.MoveTowards(kick, 0f, dt * 0.6f);
            reloadDip = Mathf.MoveTowards(reloadDip, weapons.IsReloading ? 1f : 0f, dt * 4f);
            equipDip = Mathf.MoveTowards(equipDip, 0f, dt * 3.5f);

            bool visible = character.IsAlive;
            if (viewModel.activeSelf != visible) viewModel.SetActive(visible);
            viewModel.transform.localPosition = weaponOffset * (1f - aimBlend);

            // Weapon tuck: when a wall is right in front of the camera, pull the gun back and down
            // instead of letting the (larger) view model poke through it.
            Transform camT = playerCamera.transform;
            float wallDistance = Physics.SphereCast(camT.position, 0.06f, camT.forward, out RaycastHit wall, 0.8f, environmentMask, QueryTriggerInteraction.Ignore)
                ? wall.distance : 0.8f;
            tuck = Mathf.MoveTowards(tuck, Mathf.Clamp01(1f - wallDistance / 0.8f), dt * 5f);

            Vector3 offset = Vector3.Lerp(hipOffset, aimOffset, aimBlend);
            offset.z -= tuck * 0.22f;
            offset.y -= tuck * 0.05f;
            offset += new Vector3(sway.x, sway.y, -kick) + bob * 0.6f;
            offset.y -= (reloadDip * 0.08f) + (equipDip * 0.15f);
            viewModelSocket.localPosition = offset;
            viewModelSocket.localRotation = Quaternion.Euler(-kick * 120f + reloadDip * 25f + equipDip * 30f + tuck * 40f, tuck * -15f, reloadDip * -20f);
        }

        /// <summary>Places the right hand on the grip and the left hand on the weapon's "Handguard" marker.</summary>
        public static void AttachHands(Transform model, GameObject handPrefab)
        {
            if (handPrefab == null) return;
            GameObject right = Instantiate(handPrefab, model, false);
            right.transform.localPosition = new Vector3(0.005f, -0.035f, -0.01f);
            right.transform.localRotation = Quaternion.Euler(-24f, -14f, 0f);

            Transform guard = model.Find("Handguard");
            GameObject left = Instantiate(handPrefab, model, false);
            left.transform.localPosition = guard != null ? guard.localPosition + new Vector3(-0.01f, -0.035f, 0f) : new Vector3(-0.03f, -0.04f, 0.05f);
            left.transform.localRotation = Quaternion.Euler(-14f, 32f, -10f);
        }

        private void SwapViewModel(WeaponDefinition weapon)
        {
            if (viewModel != null) Destroy(viewModel);
            viewModel = null;
            viewMuzzle = null;
            shownWeapon = weapon;
            if (weapon == null || weapon.viewModelPrefab == null) return;

            viewModel = Instantiate(weapon.viewModelPrefab, viewModelSocket, false);
            viewModel.transform.localScale = Vector3.one * viewModelScale;
            weaponOffset = weapon.viewModelOffset;
            viewModel.transform.localPosition = weaponOffset;
            AttachHands(viewModel.transform, handPrefab);
            foreach (Transform t in viewModel.GetComponentsInChildren<Transform>(true))
            {
                t.gameObject.layer = firstPersonLayer;
                if (t.name == "Muzzle") viewMuzzle = t;
            }
            foreach (Renderer r in viewModel.GetComponentsInChildren<Renderer>(true))
            {
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                r.receiveShadows = false;
            }
        }
    }
}
