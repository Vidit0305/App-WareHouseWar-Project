using UnityEngine;

namespace WarehouseWar.Core
{
    /// <summary>
    /// Applies process-wide runtime settings once, before the first scene loads.
    /// Runs automatically (no GameObject needed), so every scene — including the one
    /// you press Play on in the Editor — starts with the same configuration.
    /// </summary>
    public static class AppBootstrap
    {
        public static bool IsInitialized { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Initialize()
        {
            // Android/iOS default to 30 FPS unless a target is requested explicitly.
            // vSyncCount must be 0 for targetFrameRate to apply on desktop/Editor; mobile ignores vSync.
            QualitySettings.vSyncCount = 0;
            ApplyTargetFrameRate(PlayerPrefs.GetInt(GameConstants.PrefKeys.TargetFrameRate, GameConstants.Defaults.TargetFrameRate));

            // A match lasts 3 minutes of mostly touch-and-hold input; never let the screen dim mid-fight.
            Screen.sleepTimeout = SleepTimeout.NeverSleep;

            IsInitialized = true;
            Debug.Log($"[AppBootstrap] {GameConstants.GameTitle} v{Application.version} on {Application.platform}, " +
                      $"target {Application.targetFrameRate} FPS, quality '{QualitySettings.names[QualitySettings.GetQualityLevel()]}'.");
        }

        /// <summary>Clamps and applies a frame-rate cap. Used by the Settings screen in Phase 7.</summary>
        public static void ApplyTargetFrameRate(int framesPerSecond)
        {
            Application.targetFrameRate = Mathf.Clamp(framesPerSecond, GameConstants.Defaults.MinFrameRate, GameConstants.Defaults.MaxFrameRate);
        }
    }
}
