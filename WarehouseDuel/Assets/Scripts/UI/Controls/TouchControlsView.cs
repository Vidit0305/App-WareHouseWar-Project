using UnityEngine;
using WarehouseDuel.Core;

namespace WarehouseDuel.UI.Controls
{
    /// <summary>
    /// Root of the on-screen controls. Exposes the individual controls to the input collector and
    /// applies the player's layout preference (standard / dual fire / left-handed) and button size.
    /// Hidden automatically on devices without a touchscreen (keyboard + mouse is used there).
    /// </summary>
    public sealed class TouchControlsView : MonoBehaviour
    {
        [SerializeField] private VirtualJoystick joystick;
        [SerializeField] private TouchLookArea lookArea;
        [SerializeField] private TouchButton fire;
        [SerializeField] private TouchButton fireLeft;
        [SerializeField] private TouchButton aim;
        [SerializeField] private TouchButton reload;
        [SerializeField] private TouchButton jump;
        [SerializeField] private TouchButton sprint;
        [SerializeField] private TouchButton switchWeapon;

        [Header("Layout")]
        [Tooltip("Left-side zone (joystick) and right-side zone (look + buttons). Swapped for left-handed play.")]
        [SerializeField] private RectTransform leftZone;
        [SerializeField] private RectTransform rightZone;
        [SerializeField] private RectTransform[] scalableButtons;

        public static TouchControlsView Instance { get; private set; }

        public VirtualJoystick Joystick => joystick;
        public TouchLookArea LookArea => lookArea;
        public TouchButton Fire => fire;
        public TouchButton FireLeft => fireLeft;
        public TouchButton Aim => aim;
        public TouchButton Reload => reload;
        public TouchButton Jump => jump;
        public TouchButton Sprint => sprint;
        public TouchButton SwitchWeapon => switchWeapon;

        /// <summary>True when touch controls are in use on this device.</summary>
        public static bool ShouldShow =>
            Application.isMobilePlatform || UnityEngine.InputSystem.Touchscreen.current != null;

        private void Awake()
        {
            Instance = this;
            gameObject.SetActive(ShouldShow);
        }

        private void OnEnable()
        {
            GameSettings.Changed += ApplySettings;
            ApplySettings();
        }

        private void OnDisable() => GameSettings.Changed -= ApplySettings;

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        private void ApplySettings()
        {
            ControlLayout layout = GameSettings.Layout;
            if (fireLeft != null) fireLeft.gameObject.SetActive(layout == ControlLayout.DualFire);

            // Left-handed: mirror both zones horizontally (their children are laid out relative to the zone edges).
            bool mirrored = layout == ControlLayout.LeftHanded;
            Mirror(leftZone, mirrored);
            Mirror(rightZone, mirrored);

            float scale = GameSettings.ButtonScale;
            if (scalableButtons != null)
            {
                foreach (RectTransform button in scalableButtons)
                {
                    if (button != null) button.localScale = Vector3.one * scale;
                }
            }
        }

        private static void Mirror(RectTransform zone, bool mirrored)
        {
            if (zone == null) return;
            // The zone's own scale.x flips child placement; children counter-flip so icons/text read correctly.
            zone.localScale = new Vector3(mirrored ? -1f : 1f, 1f, 1f);
            foreach (RectTransform child in zone)
            {
                Vector3 s = child.localScale;
                child.localScale = new Vector3(Mathf.Abs(s.x) * (mirrored ? -1f : 1f), s.y, s.z);
            }
        }
    }
}
