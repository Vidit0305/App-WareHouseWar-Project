using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace WarehouseWar.UI.Controls
{
    /// <summary>
    /// On-screen action button with multi-touch support.
    /// Hold: <see cref="IsHeld"/> is true while a finger is down (Fire, Aim).
    /// Toggle: each tap flips <see cref="IsHeld"/> (Sprint).
    /// Every press is also latched so a quick tap between two network ticks is never lost (<see cref="ConsumePress"/>).
    /// </summary>
    public sealed class TouchButton : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IDragHandler
    {
        public enum ButtonMode { Hold, Toggle }

        [SerializeField] private ButtonMode mode = ButtonMode.Hold;
        [Tooltip("Optional: finger movement on this button also turns the camera (useful for Fire).")]
        [SerializeField] private TouchLookArea forwardDragTo;
        [SerializeField] private Graphic highlight;
        [SerializeField] private Color normalColor = new Color(1f, 1f, 1f, 0.35f);
        [SerializeField] private Color activeColor = new Color(1f, 0.69f, 0.13f, 0.85f);

        private int pointerId = int.MinValue;
        private bool toggled;
        private bool pressLatched;
        private RectTransform rect;

        public bool IsHeld => mode == ButtonMode.Toggle ? toggled : pointerId != int.MinValue;

        public ButtonMode Mode => mode;

        private void Awake()
        {
            rect = (RectTransform)transform;
            Refresh();
        }

        /// <summary>True once per press since the last call.</summary>
        public bool ConsumePress()
        {
            bool pressed = pressLatched;
            pressLatched = false;
            return pressed;
        }

        /// <summary>Clears a toggle (e.g. stop sprinting when the joystick is released).</summary>
        public void ResetToggle()
        {
            if (!toggled) return;
            toggled = false;
            Refresh();
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            if (pointerId != int.MinValue) return;
            pointerId = eventData.pointerId;
            pressLatched = true;
            if (mode == ButtonMode.Toggle) toggled = !toggled;
            Refresh();
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            if (eventData.pointerId != pointerId) return;
            pointerId = int.MinValue;
            Refresh();
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (eventData.pointerId == pointerId && forwardDragTo != null) forwardDragTo.AddExternalDelta(eventData.delta);
        }

        private void OnDisable()
        {
            pointerId = int.MinValue;
            pressLatched = false;
            Refresh();
        }

        private void Refresh()
        {
            bool active = pointerId != int.MinValue || toggled;
            if (highlight != null) highlight.color = active ? activeColor : normalColor;
            if (rect != null) rect.localScale = Vector3.one * (pointerId != int.MinValue ? 0.92f : 1f);
        }
    }
}
