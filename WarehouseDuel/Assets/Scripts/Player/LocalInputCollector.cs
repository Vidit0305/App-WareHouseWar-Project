using UnityEngine;
using UnityEngine.InputSystem;
using WarehouseDuel.Core;
using WarehouseDuel.UI.Controls;

namespace WarehouseDuel.Player
{
    /// <summary>Everything the local player asked for since the previous network tick.</summary>
    public struct InputSnapshot
    {
        public Vector2 Move;
        public float Yaw;
        public float Pitch;
        public bool Fire;
        public bool Aim;
        public bool Sprint;
        public bool Jump;
        public bool Reload;
        /// <summary>Requested weapon slot 0-3, or -1 for "no change".</summary>
        public int WeaponSlot;
        public bool NextWeapon;
    }

    /// <summary>
    /// Merges keyboard/mouse (Editor/PC testing) and the touch controls into one <see cref="InputSnapshot"/>.
    /// Look angles are integrated every rendered frame so the local camera turns smoothly at full frame rate;
    /// the network layer samples a snapshot once per tick (<see cref="ConsumeSnapshot"/>).
    /// One-shot actions (jump, reload, weapon switch) are latched until consumed, so short taps are never missed.
    /// </summary>
    public sealed class LocalInputCollector : MonoBehaviour
    {
        public const float PitchLimit = 85f;

        // Base rates before the player's sensitivity setting.
        private const float MouseDegreesPerPixel = 0.09f;
        private const float TouchDegreesPerScreenWidth = 220f;

        public static LocalInputCollector Instance { get; private set; }

        /// <summary>When false (menus, death, match end) all inputs read as neutral.</summary>
        public bool InputEnabled { get; set; } = true;

        public float Yaw { get; private set; }
        public float Pitch { get; private set; }
        public bool IsAiming { get; private set; }

        private Vector2 move;
        private bool fireHeld, sprintHeld, keyboardSprintToggle;

        // Synthetic input (automated multiplayer test only; see AutoTestDriver).
        private bool synthetic;
        private Vector2 syntheticMove;
        private bool syntheticFire, syntheticJump;
        private bool jumpLatch, reloadLatch, nextWeaponLatch;
        private int slotLatch = -1;

        public static LocalInputCollector EnsureExists()
        {
            if (Instance != null) return Instance;
            return new GameObject("Local Input").AddComponent<LocalInputCollector>();
        }

        public void SetLook(float yaw, float pitch)
        {
            Yaw = yaw;
            Pitch = Mathf.Clamp(pitch, -PitchLimit, PitchLimit);
        }

        /// <summary>Adds a camera kick (recoil) in degrees. Positive pitch kick looks up.</summary>
        public void AddRecoil(float pitchUp, float yaw)
        {
            Pitch = Mathf.Clamp(Pitch - pitchUp, -PitchLimit, PitchLimit);
            Yaw += yaw;
        }

        /// <summary>Automated tests: replaces device input with the given values until <see cref="StopSynthetic"/>.</summary>
        public void SetSynthetic(Vector2 moveInput, float yaw, float pitch, bool fire, bool jump)
        {
            synthetic = true;
            syntheticMove = Vector2.ClampMagnitude(moveInput, 1f);
            Yaw = Mathf.Repeat(yaw, 360f);
            Pitch = Mathf.Clamp(pitch, -PitchLimit, PitchLimit);
            syntheticFire = fire;
            syntheticJump |= jump;
        }

        public void StopSynthetic() => synthetic = false;

        public InputSnapshot ConsumeSnapshot()
        {
            var snapshot = new InputSnapshot
            {
                Move = InputEnabled ? move : Vector2.zero,
                Yaw = Yaw,
                Pitch = Pitch,
                Fire = InputEnabled && fireHeld,
                Aim = InputEnabled && IsAiming,
                Sprint = InputEnabled && sprintHeld,
                Jump = InputEnabled && jumpLatch,
                Reload = InputEnabled && reloadLatch,
                WeaponSlot = InputEnabled ? slotLatch : -1,
                NextWeapon = InputEnabled && nextWeaponLatch,
            };
            jumpLatch = reloadLatch = nextWeaponLatch = false;
            slotLatch = -1;
            return snapshot;
        }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
        }

        private void OnDestroy()
        {
            if (Instance != this) return;
            Instance = null;
            UnlockCursor();
        }

        private void Update()
        {
            TouchControlsView touch = TouchControlsView.Instance != null && TouchControlsView.Instance.isActiveAndEnabled
                ? TouchControlsView.Instance
                : null;

            if (synthetic)
            {
                move = InputEnabled ? syntheticMove : Vector2.zero;
                fireHeld = InputEnabled && syntheticFire;
                jumpLatch |= InputEnabled && syntheticJump;
                syntheticJump = false;
                sprintHeld = IsAiming = false;
                return;
            }

            if (!InputEnabled)
            {
                move = Vector2.zero;
                fireHeld = sprintHeld = IsAiming = false;
                if (touch != null) touch.LookArea.ConsumeDelta();
                UnlockCursor();
                return;
            }

            Keyboard keyboard = Keyboard.current;
            Mouse mouse = Mouse.current;
            bool cursorLocked = Cursor.lockState == CursorLockMode.Locked;

            // --- movement
            Vector2 keyMove = Vector2.zero;
            if (keyboard != null)
            {
                if (keyboard.wKey.isPressed) keyMove.y += 1f;
                if (keyboard.sKey.isPressed) keyMove.y -= 1f;
                if (keyboard.dKey.isPressed) keyMove.x += 1f;
                if (keyboard.aKey.isPressed) keyMove.x -= 1f;
            }
            Vector2 stick = touch != null ? touch.Joystick.Value : Vector2.zero;
            move = Vector2.ClampMagnitude(stick.sqrMagnitude > keyMove.sqrMagnitude ? stick : keyMove, 1f);

            // --- held buttons
            bool mouseFire = cursorLocked && mouse != null && mouse.leftButton.isPressed;
            bool mouseAim = cursorLocked && mouse != null && mouse.rightButton.isPressed;
            fireHeld = mouseFire || (touch != null && (touch.Fire.IsHeld || (touch.FireLeft != null && touch.FireLeft.IsHeld)));
            IsAiming = mouseAim || (touch != null && touch.Aim.IsHeld);

            if (keyboard != null && keyboard.leftShiftKey.wasPressedThisFrame) keyboardSprintToggle = true;
            if (keyboard != null && keyboard.leftShiftKey.wasReleasedThisFrame) keyboardSprintToggle = false;
            if (touch != null && stick.sqrMagnitude < 0.01f) touch.Sprint.ResetToggle(); // stop sprinting when the thumb lifts
            sprintHeld = (keyboardSprintToggle || (touch != null && touch.Sprint.IsHeld)) && move.y > 0.3f;

            // --- latched one-shot actions
            if (keyboard != null)
            {
                jumpLatch |= keyboard.spaceKey.wasPressedThisFrame;
                reloadLatch |= keyboard.rKey.wasPressedThisFrame;
                nextWeaponLatch |= keyboard.qKey.wasPressedThisFrame;
                if (keyboard.digit1Key.wasPressedThisFrame) slotLatch = 0;
                if (keyboard.digit2Key.wasPressedThisFrame) slotLatch = 1;
                if (keyboard.digit3Key.wasPressedThisFrame) slotLatch = 2;
                if (keyboard.digit4Key.wasPressedThisFrame) slotLatch = 3;
            }
            if (mouse != null && cursorLocked && Mathf.Abs(mouse.scroll.ReadValue().y) > 0.1f) nextWeaponLatch = true;
            if (touch != null)
            {
                jumpLatch |= touch.Jump.ConsumePress();
                reloadLatch |= touch.Reload.ConsumePress();
                nextWeaponLatch |= touch.SwitchWeapon.ConsumePress();
            }

            // --- look
            float sensitivity = GameSettings.LookSensitivity * (IsAiming ? GameSettings.AimSensitivity : 1f);
            Vector2 lookDegrees = Vector2.zero;
            if (mouse != null && cursorLocked) lookDegrees += mouse.delta.ReadValue() * MouseDegreesPerPixel;
            if (touch != null) lookDegrees += touch.LookArea.ConsumeDelta() * (TouchDegreesPerScreenWidth / Mathf.Max(1, Screen.width));
            lookDegrees *= sensitivity;
            if (GameSettings.InvertY) lookDegrees.y = -lookDegrees.y;

            Yaw = Mathf.Repeat(Yaw + lookDegrees.x, 360f);
            Pitch = Mathf.Clamp(Pitch - lookDegrees.y, -PitchLimit, PitchLimit);

            UpdateCursorLock(keyboard, mouse, touch != null);
        }

        private static void UpdateCursorLock(Keyboard keyboard, Mouse mouse, bool usingTouch)
        {
            if (usingTouch) return;
            if (keyboard != null && keyboard.escapeKey.wasPressedThisFrame) UnlockCursor();
            else if (mouse != null && mouse.leftButton.wasPressedThisFrame && Cursor.lockState != CursorLockMode.Locked
                     && (UnityEngine.EventSystems.EventSystem.current == null || !UnityEngine.EventSystems.EventSystem.current.IsPointerOverGameObject()))
            {
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;
            }
        }

        private static void UnlockCursor()
        {
            if (Cursor.lockState == CursorLockMode.None) return;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }
    }
}
