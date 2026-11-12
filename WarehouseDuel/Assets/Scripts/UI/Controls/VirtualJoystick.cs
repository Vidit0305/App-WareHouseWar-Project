using UnityEngine;
using UnityEngine.EventSystems;

namespace WarehouseDuel.UI.Controls
{
    /// <summary>
    /// Floating movement joystick. Put this on a large transparent touch zone (raycast target);
    /// the stick appears wherever the thumb lands inside the zone. Tracks one finger by pointer id,
    /// so it keeps working while other fingers look, shoot or jump.
    /// </summary>
    [RequireComponent(typeof(RectTransform))]
    public sealed class VirtualJoystick : MonoBehaviour, IPointerDownHandler, IDragHandler, IPointerUpHandler
    {
        [SerializeField] private RectTransform stickBase;
        [SerializeField] private RectTransform handle;
        [Tooltip("Handle travel radius in canvas units.")]
        [SerializeField, Min(10f)] private float radius = 110f;
        [Tooltip("Inputs below this magnitude are ignored.")]
        [SerializeField, Range(0f, 0.5f)] private float deadZone = 0.12f;
        [SerializeField, Range(0f, 1f)] private float idleAlpha = 0.35f;

        private RectTransform zone;
        private CanvasGroup baseGroup;
        private Vector2 restPosition;
        private int pointerId = int.MinValue;

        /// <summary>Stick direction, magnitude 0..1.</summary>
        public Vector2 Value { get; private set; }
        public bool IsActive => pointerId != int.MinValue;

        private void Awake()
        {
            zone = (RectTransform)transform;
            if (stickBase != null)
            {
                restPosition = stickBase.anchoredPosition;
                baseGroup = stickBase.GetComponent<CanvasGroup>();
                if (baseGroup == null) baseGroup = stickBase.gameObject.AddComponent<CanvasGroup>();
                baseGroup.blocksRaycasts = false;
                baseGroup.alpha = idleAlpha;
            }
        }

        private void OnDisable() => Release();

        public void OnPointerDown(PointerEventData eventData)
        {
            if (IsActive) return;
            pointerId = eventData.pointerId;

            if (stickBase != null && RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    (RectTransform)stickBase.parent, eventData.position, eventData.pressEventCamera, out Vector2 local))
            {
                stickBase.anchoredPosition = local;
            }
            if (baseGroup != null) baseGroup.alpha = 1f;
            UpdateHandle(eventData);
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (eventData.pointerId == pointerId) UpdateHandle(eventData);
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            if (eventData.pointerId == pointerId) Release();
        }

        private void UpdateHandle(PointerEventData eventData)
        {
            if (stickBase == null || !RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    stickBase, eventData.position, eventData.pressEventCamera, out Vector2 offset))
            {
                return;
            }

            Vector2 clamped = Vector2.ClampMagnitude(offset, radius);
            if (handle != null) handle.anchoredPosition = clamped;

            Vector2 normalized = clamped / radius;
            float magnitude = normalized.magnitude;
            Value = magnitude < deadZone ? Vector2.zero : normalized * Mathf.InverseLerp(deadZone, 1f, magnitude) / magnitude;
        }

        private void Release()
        {
            pointerId = int.MinValue;
            Value = Vector2.zero;
            if (handle != null) handle.anchoredPosition = Vector2.zero;
            if (stickBase != null) stickBase.anchoredPosition = restPosition;
            if (baseGroup != null) baseGroup.alpha = idleAlpha;
        }
    }
}
