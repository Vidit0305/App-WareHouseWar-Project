using UnityEngine;

namespace WarehouseDuel.UI.Widgets
{
    /// <summary>Slides and fades a panel in when it becomes active (ease-out-back, unscaled time).</summary>
    [RequireComponent(typeof(RectTransform))]
    public sealed class UiTransition : MonoBehaviour
    {
        [SerializeField] private Vector2 fromOffset = new Vector2(-120f, 0f);
        [SerializeField, Min(0f)] private float delay;
        [SerializeField, Min(0.05f)] private float duration = 0.45f;

        private RectTransform rect;
        private CanvasGroup group;
        private Vector2 target;
        private float startTime;
        private bool captured;
        private bool finished;

        private void Awake()
        {
            rect = (RectTransform)transform;
            group = GetComponent<CanvasGroup>();
            if (group == null) group = gameObject.AddComponent<CanvasGroup>();
        }

        private void OnEnable()
        {
            if (!captured)
            {
                target = rect.anchoredPosition;
                captured = true;
            }
            startTime = Time.unscaledTime + delay;
            finished = false;
            Apply(0f);
        }

        private void Update()
        {
            if (finished) return;
            float t = Mathf.Clamp01((Time.unscaledTime - startTime) / duration);
            Apply(t);
            finished = t >= 1f;
        }

        private void OnDisable()
        {
            if (captured) rect.anchoredPosition = target; // next activation animates from the true position again
        }

        private void Apply(float t)
        {
            float eased = EaseOutBack(t);
            rect.anchoredPosition = Vector2.LerpUnclamped(target + fromOffset, target, eased);
            group.alpha = Mathf.Clamp01(t * 1.6f);
        }

        private static float EaseOutBack(float t)
        {
            const float c1 = 1.4f, c3 = c1 + 1f;
            float x = t - 1f;
            return 1f + c3 * x * x * x + c1 * x * x;
        }
    }
}
