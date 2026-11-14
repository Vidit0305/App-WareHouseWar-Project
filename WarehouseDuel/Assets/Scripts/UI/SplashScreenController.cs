using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using WarehouseWar.Core;

namespace WarehouseWar.UI
{
    /// <summary>
    /// Splash screen flow: fade in → load the main menu in the background while a progress bar
    /// animates → fade out → activate the menu. The splash stays up for a minimum time so the
    /// logo is readable even on fast devices.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class SplashScreenController : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private CanvasGroup canvasGroup;
        [Tooltip("Child of the progress track, anchored left. Its anchorMax.x is driven 0..1.")]
        [SerializeField] private RectTransform progressFill;
        [SerializeField] private TMP_Text statusLabel;
        [SerializeField] private TMP_Text versionLabel;
        [Tooltip("Optional: shows the real loading percentage.")]
        [SerializeField] private TMP_Text percentLabel;
        [Tooltip("Optional: a narrow highlight that sweeps across the filled part of the bar.")]
        [SerializeField] private RectTransform shine;

        [Header("Timing")]
        [Tooltip("The splash stays visible at least this long, even if loading finishes sooner.")]
        [SerializeField, Min(0f)] private float minimumDisplaySeconds = 2.5f;
        [SerializeField, Min(0.05f)] private float fadeSeconds = 0.35f;
        [Tooltip("Maximum speed of the visible bar, in fractions of the bar per second.")]
        [SerializeField, Min(0.1f)] private float progressBarSpeed = 1.5f;

        [Header("Flow")]
        [SerializeField] private string nextSceneName = GameConstants.Scenes.MainMenu;

        // AsyncOperation.progress stops at 0.9 while allowSceneActivation is false.
        private const float LoadedProgressThreshold = 0.9f;
        private const float DotsPerSecond = 3f;
        private static readonly string[] LoadingFrames = { "LOADING", "LOADING.", "LOADING..", "LOADING..." };

        private float shownProgress;
        private int lastFrameIndex = -1;
        private int lastPercent = -1;
        private bool failed;

        private void Awake()
        {
            if (versionLabel != null) versionLabel.text = $"v{Application.version}";
            if (canvasGroup != null) canvasGroup.alpha = 0f;
            SetProgress(0f);
        }

        private IEnumerator Start()
        {
            yield return Fade(0f, 1f);

            AsyncOperation load = SceneManager.LoadSceneAsync(nextSceneName, LoadSceneMode.Single);
            if (load == null)
            {
                // Unity returns null (and logs) when the scene is not in Build Settings.
                failed = true;
                if (statusLabel != null) statusLabel.text = $"SCENE '{nextSceneName}' MISSING FROM BUILD SETTINGS";
                Debug.LogError($"[Splash] Cannot load '{nextSceneName}'. Run Tools > Warehouse War > Phase 1 - Run Project Setup.");
                yield break;
            }

            load.allowSceneActivation = false;
            float elapsed = 0f;

            while (shownProgress < 1f)
            {
                elapsed += Time.unscaledDeltaTime;
                float loadFraction = Mathf.Clamp01(load.progress / LoadedProgressThreshold);
                float timeFraction = minimumDisplaySeconds > 0f ? Mathf.Clamp01(elapsed / minimumDisplaySeconds) : 1f;

                // The bar never runs ahead of real loading, and never finishes before the minimum time.
                float target = Mathf.Min(loadFraction, timeFraction);
                shownProgress = Mathf.MoveTowards(shownProgress, target, progressBarSpeed * Time.unscaledDeltaTime);
                SetProgress(shownProgress);
                yield return null;
            }

            yield return Fade(1f, 0f);
            load.allowSceneActivation = true;
        }

        private void Update()
        {
            if (shine != null)
            {
                // Sweep a highlight across the filled part of the bar every ~1.2 s.
                float x = Mathf.Repeat(Time.unscaledTime * 0.85f, 1.3f) - 0.15f;
                shine.anchorMin = new Vector2(Mathf.Clamp01(x - 0.08f), 0f);
                shine.anchorMax = new Vector2(Mathf.Clamp01(x + 0.08f), 1f);
                shine.offsetMin = shine.offsetMax = Vector2.zero;
            }

            if (failed || statusLabel == null) return;

            int frameIndex = (int)(Time.unscaledTime * DotsPerSecond) % LoadingFrames.Length;
            if (frameIndex == lastFrameIndex) return; // avoid re-meshing text every frame
            lastFrameIndex = frameIndex;
            statusLabel.text = LoadingFrames[frameIndex];
        }

        private void SetProgress(float fraction)
        {
            if (progressFill == null) return;
            Vector2 max = progressFill.anchorMax;
            max.x = Mathf.Clamp01(fraction);
            progressFill.anchorMax = max;

            int percent = Mathf.RoundToInt(Mathf.Clamp01(fraction) * 100f);
            if (percentLabel != null && percent != lastPercent)
            {
                lastPercent = percent;
                percentLabel.text = percent + "%";
            }
        }

        private IEnumerator Fade(float from, float to)
        {
            if (canvasGroup == null) yield break;

            for (float t = 0f; t < fadeSeconds; t += Time.unscaledDeltaTime)
            {
                canvasGroup.alpha = Mathf.Lerp(from, to, t / fadeSeconds);
                yield return null;
            }
            canvasGroup.alpha = to;
        }
    }
}
