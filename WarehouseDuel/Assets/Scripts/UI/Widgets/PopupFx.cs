using System.Collections;
using UnityEngine;

namespace WarehouseDuel.UI.Widgets
{
    /// <summary>
    /// Open/close animation for dialogs and full screens. Opening: the backdrop fades in and the card pops
    /// (scale with a slight overshoot) or the page slides up. Closing through <see cref="Hide"/> plays the
    /// reverse before deactivating. Uses unscaled time so it also works while the game is paused.
    /// </summary>
    [RequireComponent(typeof(CanvasGroup))]
    public sealed class PopupFx : MonoBehaviour
    {
        public enum Style { Pop, Slide }

        [SerializeField] private Style style = Style.Pop;
        [SerializeField] private RectTransform content;
        [SerializeField] private float openSeconds = 0.24f;
        [SerializeField] private float closeSeconds = 0.14f;

        private CanvasGroup group;
        private Vector2 restPosition;
        private bool restCaptured;
        private Coroutine running;

        private void Awake() => group = GetComponent<CanvasGroup>();

        private void OnEnable()
        {
            if (group == null) group = GetComponent<CanvasGroup>();
            if (content != null && !restCaptured)
            {
                restPosition = content.anchoredPosition;
                restCaptured = true;
            }
            if (running != null) StopCoroutine(running);
            running = StartCoroutine(Animate(true));
        }

        /// <summary>Closes <paramref name="target"/> with its animation if it has one, otherwise just deactivates it.</summary>
        public static void Hide(GameObject target)
        {
            if (target == null || !target.activeSelf) return;
            if (target.TryGetComponent(out PopupFx fx) && fx.isActiveAndEnabled) fx.Close();
            else target.SetActive(false);
        }

        public void Close()
        {
            if (running != null) StopCoroutine(running);
            running = StartCoroutine(Animate(false));
        }

        private IEnumerator Animate(bool opening)
        {
            float duration = opening ? openSeconds : closeSeconds;
            group.blocksRaycasts = opening;
            for (float t = 0f; t < duration; t += Time.unscaledDeltaTime)
            {
                Apply(opening ? t / duration : 1f - t / duration, opening);
                yield return null;
            }
            Apply(opening ? 1f : 0f, opening);
            running = null;
            if (!opening) gameObject.SetActive(false);
            else group.blocksRaycasts = true;
        }

        private void Apply(float k, bool opening)
        {
            float eased = 1f - Mathf.Pow(1f - Mathf.Clamp01(k), 3f);
            group.alpha = eased;
            if (content == null) return;
            if (style == Style.Pop)
            {
                // Overshoot only when opening ("back" easing).
                float s = opening ? BackOut(Mathf.Clamp01(k)) : eased;
                content.localScale = Vector3.one * Mathf.LerpUnclamped(0.86f, 1f, s);
            }
            else
            {
                content.anchoredPosition = restPosition + new Vector2(0f, Mathf.Lerp(-40f, 0f, eased));
            }
        }

        private static float BackOut(float t)
        {
            const float c1 = 1.6f, c3 = c1 + 1f;
            float x = t - 1f;
            return 1f + c3 * x * x * x + c1 * x * x;
        }
    }
}
