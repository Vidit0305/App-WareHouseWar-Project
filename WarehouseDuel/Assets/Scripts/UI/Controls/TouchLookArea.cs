using UnityEngine;
using UnityEngine.EventSystems;

namespace WarehouseDuel.UI.Controls
{
    /// <summary>
    /// Transparent drag zone for camera look. Accumulates finger movement (screen pixels) between reads.
    /// Buttons that sit on top of the zone can forward their drag movement here (see <see cref="TouchButton"/>),
    /// so a player can keep aiming while holding Fire.
    /// </summary>
    [RequireComponent(typeof(RectTransform))]
    public sealed class TouchLookArea : MonoBehaviour, IPointerDownHandler, IDragHandler, IPointerUpHandler
    {
        private int pointerId = int.MinValue;
        private Vector2 accumulated;

        public bool IsActive => pointerId != int.MinValue;

        /// <summary>Returns movement since the last call (pixels) and resets it.</summary>
        public Vector2 ConsumeDelta()
        {
            Vector2 delta = accumulated;
            accumulated = Vector2.zero;
            return delta;
        }

        /// <summary>Lets other controls (e.g. the Fire button) contribute look movement.</summary>
        public void AddExternalDelta(Vector2 pixels) => accumulated += pixels;

        public void OnPointerDown(PointerEventData eventData)
        {
            if (!IsActive) pointerId = eventData.pointerId;
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (eventData.pointerId == pointerId) accumulated += eventData.delta;
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            if (eventData.pointerId == pointerId) pointerId = int.MinValue;
        }

        private void OnDisable()
        {
            pointerId = int.MinValue;
            accumulated = Vector2.zero;
        }
    }
}
