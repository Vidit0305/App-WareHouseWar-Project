using TMPro;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using WarehouseDuel.Core;
using WarehouseDuel.UI;
using static WarehouseDuel.EditorTools.UI.UiFactory;

namespace WarehouseDuel.EditorTools.UI
{
    /// <summary>
    /// Splash scene: the key art fills the screen (cropped, never stretched, on any aspect ratio), a dark
    /// fade at the bottom, and a live loading bar (real scene-load progress + percentage + shine sweep).
    /// </summary>
    internal static class SplashBuilder
    {
        private const float ArtAspect = 1672f / 941f;

        public static void Build(SetupReport report)
        {
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var camObject = new GameObject("Main Camera") { tag = "MainCamera" };
            Camera cam = camObject.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = Color.black;
            cam.cullingMask = 0;
            camObject.AddComponent<AudioListener>();

            Canvas canvas = CreateCanvas("Splash Canvas", 0, out RectTransform safe);
            CanvasGroup group = canvas.gameObject.AddComponent<CanvasGroup>();

            // Key art, "envelope" fitted: fills the whole screen and crops the overflow.
            RectTransform artFrame = CreateRect("Art Frame", canvas.transform);
            Stretch(artFrame);
            artFrame.SetAsFirstSibling();
            artFrame.gameObject.AddComponent<RectMask2D>();
            Image art = CreateImage("Background Art", artFrame, Color.white, UiArt.SplashBackground);
            art.preserveAspect = false;
            var fitter = art.gameObject.AddComponent<AspectRatioFitter>();
            fitter.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
            fitter.aspectRatio = ArtAspect;

            Image fade = CreateImage("Bottom Fade", canvas.transform, new Color(0f, 0f, 0f, 0.88f), UiArt.BottomFade);
            fade.rectTransform.anchorMin = new Vector2(0f, 0f);
            fade.rectTransform.anchorMax = new Vector2(1f, 0f);
            fade.rectTransform.pivot = new Vector2(0.5f, 0f);
            fade.rectTransform.sizeDelta = new Vector2(0f, 330f);
            fade.rectTransform.anchoredPosition = Vector2.zero;
            fade.transform.SetSiblingIndex(1);

            // Loading bar (bottom centre).
            const float barWidth = 880f, barHeight = 30f, barY = 82f;
            TextMeshProUGUI status = CreateText("Status", safe, "LOADING", 32f, Color.white, TextAlignmentOptions.BottomLeft, display: true);
            status.characterSpacing = 10f;
            Place(status.rectTransform, new Vector2(0.5f, 0f), new Vector2(-barWidth / 2f, barY + barHeight / 2f + 10f), new Vector2(500f, 46f), new Vector2(0f, 0f));
            TextMeshProUGUI percent = CreateText("Percent", safe, "0%", 32f, Accent, TextAlignmentOptions.BottomRight, display: true);
            Place(percent.rectTransform, new Vector2(0.5f, 0f), new Vector2(barWidth / 2f, barY + barHeight / 2f + 10f), new Vector2(200f, 46f), new Vector2(1f, 0f));

            Image outline = CreateImage("Bar Outline", safe, new Color(1f, 1f, 1f, 0.55f), UiArt.RoundedSmall);
            Place(outline.rectTransform, new Vector2(0.5f, 0f), new Vector2(0f, barY), new Vector2(barWidth + 8f, barHeight + 8f), new Vector2(0.5f, 0.5f));
            Image track = CreateImage("Bar Track", outline.transform, new Color(0.08f, 0.09f, 0.12f, 0.95f), UiArt.RoundedSmall);
            Stretch(track.rectTransform, 4f, 4f, 4f, 4f);
            Image fill = CreateImage("Bar Fill", track.transform, new Color(1f, 0.74f, 0.1f), UiArt.RoundedSmall);
            RectTransform fillRect = fill.rectTransform;
            fillRect.anchorMin = new Vector2(0f, 0f);
            fillRect.anchorMax = new Vector2(0f, 1f);
            fillRect.pivot = new Vector2(0f, 0.5f);
            fillRect.offsetMin = fillRect.offsetMax = Vector2.zero;
            Image highlight = CreateImage("Top Highlight", fill.transform, new Color(1f, 0.95f, 0.6f, 0.55f), UiArt.RoundedSmall);
            highlight.rectTransform.anchorMin = new Vector2(0f, 0.55f);
            highlight.rectTransform.anchorMax = new Vector2(1f, 0.95f);
            highlight.rectTransform.offsetMin = new Vector2(4f, 0f);
            highlight.rectTransform.offsetMax = new Vector2(-4f, 0f);
            Image shine = CreateImage("Shine", fill.transform, new Color(1f, 1f, 1f, 0.45f), UiArt.RoundedSmall);
            Stretch(shine.rectTransform);


            var controller = new GameObject("Splash Controller").AddComponent<SplashScreenController>();
            Wire(controller, "canvasGroup", group);
            Wire(controller, "progressFill", fillRect);
            Wire(controller, "statusLabel", status);
            Wire(controller, "percentLabel", percent);
            Wire(controller, "shine", shine.rectTransform);

            string path = GameConstants.Scenes.PathOf(GameConstants.Scenes.SplashScreen);
            if (EditorSceneManager.SaveScene(scene, path)) report.Pass("Splash screen built with the key art and a live loading bar.");
            else report.Error($"Could not save {path}");
        }
    }
}
