using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace WarehouseDuel.Core
{
    /// <summary>
    /// Applies the "Cinematic effects" setting to this camera: post-processing (colour grading, bloom, vignette)
    /// and FXAA on or off. Lets low-end phones trade the look for frame rate.
    /// </summary>
    [RequireComponent(typeof(Camera))]
    public sealed class CameraEffects : MonoBehaviour
    {
        private UniversalAdditionalCameraData data;

        private void OnEnable()
        {
            data = GetComponent<UniversalAdditionalCameraData>();
            GameSettings.Changed += Apply;
            Apply();
        }

        private void OnDisable() => GameSettings.Changed -= Apply;

        private void Apply()
        {
            if (data == null) return;
            bool on = GameSettings.PostEffects;
            data.renderPostProcessing = on;
            data.antialiasing = on ? AntialiasingMode.FastApproximateAntialiasing : AntialiasingMode.None;
        }
    }
}
