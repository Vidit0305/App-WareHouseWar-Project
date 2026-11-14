using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace WarehouseWar.UI.Widgets
{
    /// <summary>
    /// Tactile button feel: squash on press, springy bounce on release, and an optional gentle "call to action"
    /// pulse while the button is interactable (used on READY / START / PLAY).
    /// </summary>
    public sealed class UIButtonFX : MonoBehaviour, IPointerDownHandler, IPointerUpHandler
    {
        [SerializeField] private bool pulse;
        [SerializeField] private Graphic glow;

        private Selectable selectable;
        private Vector3 baseScale = Vector3.one;
        private float pressed;   // 0..1
        private float velocity;
        private float bounce;

        private void Awake()
        {
            selectable = GetComponent<Selectable>();
            baseScale = transform.localScale;
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            if (selectable == null || selectable.IsInteractable()) pressed = 1f;
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            if (pressed > 0f) bounce = 1f;
            pressed = 0f;
        }

        private void OnDisable()
        {
            pressed = 0f;
            bounce = 0f;
            transform.localScale = baseScale;
        }

        private void Update()
        {
            float dt = Time.unscaledDeltaTime;
            bounce = Mathf.MoveTowards(bounce, 0f, dt * 3.5f);
            float target = 1f - pressed * 0.07f + Mathf.Sin(bounce * Mathf.PI * 2.5f) * 0.05f * bounce;

            bool active = selectable == null || selectable.IsInteractable();
            if (pulse && active && pressed <= 0f) target += (Mathf.Sin(Time.unscaledTime * 3.2f) + 1f) * 0.018f;

            float current = transform.localScale.x / Mathf.Max(0.0001f, baseScale.x);
            current = Mathf.SmoothDamp(current, target, ref velocity, 0.05f, Mathf.Infinity, dt);
            transform.localScale = baseScale * current;

            if (glow != null)
            {
                Color c = glow.color;
                c.a = pulse && active ? 0.25f + 0.25f * (Mathf.Sin(Time.unscaledTime * 3.2f) + 1f) * 0.5f : 0f;
                glow.color = c;
            }
        }
    }
}
