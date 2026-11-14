using UnityEngine;

namespace WarehouseWar.UI.Widgets
{
    /// <summary>Loading spinner: an outer arc and a counter-rotating inner arc around a pulsing core.</summary>
    public sealed class Spinner : MonoBehaviour
    {
        [SerializeField] private RectTransform outer;
        [SerializeField] private RectTransform inner;
        [SerializeField] private RectTransform core;
        [SerializeField] private float speed = 300f;

        private void Update()
        {
            float dt = Time.unscaledDeltaTime;
            float t = Time.unscaledTime;
            // Ease the speed a little so the motion feels alive rather than mechanical.
            float pace = 1f + 0.35f * Mathf.Sin(t * 3f);
            if (outer != null) outer.Rotate(0f, 0f, -speed * pace * dt);
            if (inner != null) inner.Rotate(0f, 0f, speed * 0.7f * dt);
            if (core != null) core.localScale = Vector3.one * (0.85f + 0.15f * Mathf.Sin(t * 6f));
        }
    }
}
