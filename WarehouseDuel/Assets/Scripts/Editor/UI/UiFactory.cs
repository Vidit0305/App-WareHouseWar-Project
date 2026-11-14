using System;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using WarehouseWar.UI.Widgets;
using Object = UnityEngine.Object;

namespace WarehouseWar.EditorTools.UI
{
    /// <summary>
    /// Builds uGUI widgets in the game's visual style (dark glass panels, amber accent, Russo One headings).
    /// Used by the scene builders so every screen is generated, wired and consistent.
    /// </summary>
    internal static class UiFactory
    {
        public static readonly Color Accent = new Color32(255, 176, 32, 255);
        public static readonly Color AccentDark = new Color32(38, 30, 12, 255);
        public static readonly Color Backdrop = new Color32(12, 14, 20, 200);
        public static readonly Color PanelColor = new Color32(22, 26, 36, 235);
        public static readonly Color PanelLight = new Color32(255, 255, 255, 18);
        public static readonly Color TextPrimary = new Color32(240, 242, 246, 255);
        public static readonly Color TextMuted = new Color32(150, 158, 175, 255);
        public static readonly Color Danger = new Color32(235, 80, 60, 255);
        public static readonly Color Success = new Color32(80, 215, 115, 255);

        public enum ButtonStyle { Primary, Secondary, Ghost, Danger }

        private const int UiLayer = 5;

        // ------------------------------------------------------------------ structure

        public static Canvas CreateCanvas(string name, int sortingOrder, out RectTransform safeArea)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster)) { layer = UiLayer };
            var canvas = go.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = sortingOrder;
            var scaler = go.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            // Expand: the whole 1920x1080 layout always fits (wide 20:9 phones get extra width, tablets extra height),
            // so nothing near the bottom or the sides is ever cut off.
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;

            safeArea = CreateRect("Safe Area", go.transform);
            Stretch(safeArea);
            safeArea.gameObject.AddComponent<SafeAreaFitter>();
            return canvas;
        }

        public static void CreateEventSystem()
        {
            if (Object.FindFirstObjectByType<EventSystem>() != null) return;
            var go = new GameObject("EventSystem", typeof(EventSystem));
#if ENABLE_INPUT_SYSTEM
            go.AddComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>();
#else
            go.AddComponent<StandaloneInputModule>();
#endif
        }

        public static RectTransform CreateRect(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform)) { layer = UiLayer };
            var rect = (RectTransform)go.transform;
            rect.SetParent(parent, false);
            return rect;
        }

        public static RectTransform Stretch(RectTransform rect, float left = 0f, float right = 0f, float top = 0f, float bottom = 0f)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.offsetMin = new Vector2(left, bottom);
            rect.offsetMax = new Vector2(-right, -top);
            return rect;
        }

        /// <summary>Anchors at one point (0..1 of the parent), positioned and sized in canvas units.</summary>
        public static RectTransform Place(RectTransform rect, Vector2 anchor, Vector2 position, Vector2 size, Vector2? pivot = null)
        {
            rect.anchorMin = rect.anchorMax = anchor;
            rect.pivot = pivot ?? anchor;
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            return rect;
        }

        // ------------------------------------------------------------------ graphics

        public static Image CreateImage(string name, Transform parent, Color color, Sprite sprite = null, bool raycast = false)
        {
            Image image = CreateRect(name, parent).gameObject.AddComponent<Image>();
            image.color = color;
            image.sprite = sprite;
            image.raycastTarget = raycast;
            if (sprite != null && sprite.border != Vector4.zero) image.type = UnityEngine.UI.Image.Type.Sliced;
            return image;
        }

        public static Image CreatePanel(string name, Transform parent, Color? color = null) =>
            CreateImage(name, parent, color ?? PanelColor, UiArt.Rounded, raycast: true);

        public static TextMeshProUGUI CreateText(string name, Transform parent, string text, float size, Color? color = null,
            TextAlignmentOptions align = TextAlignmentOptions.Center, bool display = false, FontStyles style = FontStyles.Normal)
        {
            TextMeshProUGUI label = CreateRect(name, parent).gameObject.AddComponent<TextMeshProUGUI>();
            if (display && UiArt.DisplayFont != null) label.font = UiArt.DisplayFont;
            label.text = text;
            label.fontSize = size;
            label.color = color ?? TextPrimary;
            label.alignment = align;
            label.fontStyle = style;
            label.raycastTarget = false;
            label.richText = true;
            return label;
        }

        // ------------------------------------------------------------------ controls

        public static Button CreateButton(string name, Transform parent, string label, ButtonStyle style, Vector2 size, out TextMeshProUGUI text, float fontSize = 34f)
        {
            Color background, foreground;
            switch (style)
            {
                case ButtonStyle.Primary: background = Accent; foreground = AccentDark; break;
                case ButtonStyle.Danger: background = Danger; foreground = Color.white; break;
                case ButtonStyle.Ghost: background = new Color(1f, 1f, 1f, 0.06f); foreground = TextPrimary; break;
                default: background = new Color(1f, 1f, 1f, 0.12f); foreground = TextPrimary; break;
            }

            Image image = CreateImage(name, parent, background, UiArt.Rounded, raycast: true);
            image.rectTransform.sizeDelta = size;
            Button button = image.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            ColorBlock colors = button.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(1.08f, 1.08f, 1.08f, 1f);
            colors.pressedColor = new Color(0.8f, 0.8f, 0.8f, 1f);
            colors.selectedColor = Color.white;
            colors.disabledColor = new Color(0.55f, 0.55f, 0.55f, 0.6f);
            colors.fadeDuration = 0.06f;
            button.colors = colors;
            image.gameObject.AddComponent<UIButtonSound>();
            image.gameObject.AddComponent<UIButtonFX>();

            // Chunky mobile-game look: darker bevel under the face + soft gloss on the top half.
            if (style == ButtonStyle.Primary || style == ButtonStyle.Danger || style == ButtonStyle.Secondary)
            {
                var bevel = image.gameObject.AddComponent<Shadow>();
                bevel.effectColor = style == ButtonStyle.Secondary ? new Color(0f, 0f, 0f, 0.35f) : new Color(background.r * 0.55f, background.g * 0.4f, background.b * 0.3f, 1f);
                bevel.effectDistance = new Vector2(0f, -7f);
                Image gloss = CreateImage("Gloss", image.transform, new Color(1f, 1f, 1f, style == ButtonStyle.Secondary ? 0.05f : 0.22f), UiArt.Rounded);
                gloss.rectTransform.anchorMin = new Vector2(0f, 0.5f);
                gloss.rectTransform.anchorMax = new Vector2(1f, 1f);
                gloss.rectTransform.offsetMin = new Vector2(6f, 0f);
                gloss.rectTransform.offsetMax = new Vector2(-6f, -5f);
            }

            text = CreateText("Label", image.transform, label, fontSize, foreground, TextAlignmentOptions.Center, display: true);
            Stretch(text.rectTransform, 16f, 16f, 4f, 4f);
            return button;
        }

        public static Button CreateButton(string name, Transform parent, string label, ButtonStyle style, Vector2 size, float fontSize = 34f) =>
            CreateButton(name, parent, label, style, size, out _, fontSize);

        public static Button CreateIconButton(string name, Transform parent, Sprite icon, float size, Color background)
        {
            Image image = CreateImage(name, parent, background, UiArt.Circle, raycast: true);
            image.rectTransform.sizeDelta = new Vector2(size, size);
            Button button = image.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            image.gameObject.AddComponent<UIButtonSound>();
            Image glyph = CreateImage("Icon", image.transform, TextPrimary, icon);
            Stretch(glyph.rectTransform, size * 0.24f, size * 0.24f, size * 0.24f, size * 0.24f);
            return button;
        }

        public static TMP_InputField CreateInputField(string name, Transform parent, string placeholder, Vector2 size, float fontSize = 38f)
        {
            Image background = CreateImage(name, parent, new Color(0f, 0f, 0f, 0.45f), UiArt.Rounded, raycast: true);
            background.rectTransform.sizeDelta = size;
            TMP_InputField field = background.gameObject.AddComponent<TMP_InputField>();

            RectTransform area = CreateRect("Text Area", background.transform);
            Stretch(area, 28f, 28f, 8f, 8f);
            area.gameObject.AddComponent<RectMask2D>();

            TextMeshProUGUI hint = CreateText("Placeholder", area, placeholder, fontSize, new Color(1f, 1f, 1f, 0.35f), TextAlignmentOptions.Left);
            Stretch(hint.rectTransform);
            hint.fontStyle = FontStyles.Italic;
            TextMeshProUGUI text = CreateText("Text", area, string.Empty, fontSize, TextPrimary, TextAlignmentOptions.Left);
            Stretch(text.rectTransform);

            field.textViewport = area;
            field.textComponent = text;
            field.placeholder = hint;
            field.targetGraphic = background;
            field.caretColor = Accent;
            field.customCaretColor = true;
            field.selectionColor = new Color(1f, 0.69f, 0.13f, 0.35f);
            field.richText = false;
            field.lineType = TMP_InputField.LineType.SingleLine;
            return field;
        }

        public static Slider CreateSlider(string name, Transform parent, Vector2 size)
        {
            RectTransform root = CreateRect(name, parent);
            root.sizeDelta = size;
            Slider slider = root.gameObject.AddComponent<Slider>();

            Image track = CreateImage("Background", root, new Color(1f, 1f, 1f, 0.15f), UiArt.RoundedSmall);
            Stretch(track.rectTransform, 0f, 0f, size.y * 0.35f, size.y * 0.35f);

            RectTransform fillArea = CreateRect("Fill Area", root);
            Stretch(fillArea, 0f, 0f, size.y * 0.35f, size.y * 0.35f);
            Image fill = CreateImage("Fill", fillArea, Accent, UiArt.RoundedSmall);
            Stretch(fill.rectTransform);

            RectTransform handleArea = CreateRect("Handle Slide Area", root);
            Stretch(handleArea, size.y / 2f, size.y / 2f, 0f, 0f);
            Image handle = CreateImage("Handle", handleArea, Color.white, UiArt.Circle, raycast: true);
            handle.rectTransform.sizeDelta = new Vector2(size.y, 0f);
            handle.rectTransform.anchorMin = new Vector2(0f, 0f);
            handle.rectTransform.anchorMax = new Vector2(0f, 1f);

            slider.fillRect = fill.rectTransform;
            slider.handleRect = handle.rectTransform;
            slider.targetGraphic = handle;
            slider.direction = UnityEngine.UI.Slider.Direction.LeftToRight;
            slider.minValue = 0f;
            slider.maxValue = 1f;
            return slider;
        }

        public static Toggle CreateToggle(string name, Transform parent, float size)
        {
            RectTransform root = CreateRect(name, parent);
            root.sizeDelta = new Vector2(size, size);
            Toggle toggle = root.gameObject.AddComponent<Toggle>();
            Image box = CreateImage("Background", root, new Color(1f, 1f, 1f, 0.15f), UiArt.RoundedSmall, raycast: true);
            Stretch(box.rectTransform);
            Image check = CreateImage("Checkmark", box.transform, Accent, UiArt.RoundedSmall);
            Stretch(check.rectTransform, size * 0.2f, size * 0.2f, size * 0.2f, size * 0.2f);
            toggle.targetGraphic = box;
            toggle.graphic = check;
            root.gameObject.AddComponent<UIButtonSound>();
            return toggle;
        }

        public static OptionSelector CreateSelector(string name, Transform parent, Vector2 size)
        {
            Image background = CreateImage(name, parent, new Color(0f, 0f, 0f, 0.35f), UiArt.Rounded);
            background.rectTransform.sizeDelta = size;
            OptionSelector selector = background.gameObject.AddComponent<OptionSelector>();

            Button previous = CreateButton("Previous", background.transform, "<", ButtonStyle.Ghost, new Vector2(size.y, size.y), 30f);
            Place((RectTransform)previous.transform, new Vector2(0f, 0.5f), Vector2.zero, new Vector2(size.y, size.y), new Vector2(0f, 0.5f));
            Button next = CreateButton("Next", background.transform, ">", ButtonStyle.Ghost, new Vector2(size.y, size.y), 30f);
            Place((RectTransform)next.transform, new Vector2(1f, 0.5f), Vector2.zero, new Vector2(size.y, size.y), new Vector2(1f, 0.5f));
            TextMeshProUGUI label = CreateText("Value", background.transform, "-", 28f);
            Stretch(label.rectTransform, size.y, size.y, 0f, 0f);

            Wire(selector, "previous", previous);
            Wire(selector, "next", next);
            Wire(selector, "label", label);
            return selector;
        }

        /// <summary>Adds a vertical layout so children stack with spacing.</summary>
        public static VerticalLayoutGroup Column(RectTransform rect, float spacing, RectOffset padding = null, TextAnchor align = TextAnchor.UpperCenter)
        {
            var layout = rect.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.spacing = spacing;
            layout.padding = padding ?? new RectOffset();
            layout.childAlignment = align;
            layout.childControlWidth = false;
            layout.childControlHeight = false;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;
            return layout;
        }

        public static HorizontalLayoutGroup Row(RectTransform rect, float spacing, TextAnchor align = TextAnchor.MiddleCenter)
        {
            var layout = rect.gameObject.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = spacing;
            layout.childAlignment = align;
            layout.childControlWidth = false;
            layout.childControlHeight = false;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;
            return layout;
        }

        // ------------------------------------------------------------------ wiring

        public static void Wire(Object target, string field, Object value)
        {
            var serialized = new SerializedObject(target);
            SerializedProperty property = serialized.FindProperty(field)
                ?? throw new InvalidOperationException($"{target.GetType().Name} has no serialized field '{field}'.");
            property.objectReferenceValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        public static void WireArray(Object target, string field, params Object[] values)
        {
            var serialized = new SerializedObject(target);
            SerializedProperty property = serialized.FindProperty(field)
                ?? throw new InvalidOperationException($"{target.GetType().Name} has no serialized field '{field}'.");
            property.arraySize = values.Length;
            for (int i = 0; i < values.Length; i++) property.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        public static void SetFloat(Object target, string field, float value)
        {
            var serialized = new SerializedObject(target);
            serialized.FindProperty(field).floatValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        public static void SetColor(Object target, string field, Color value)
        {
            var serialized = new SerializedObject(target);
            serialized.FindProperty(field).colorValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        public static void SetEnum(Object target, string field, int value)
        {
            var serialized = new SerializedObject(target);
            serialized.FindProperty(field).enumValueIndex = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        public static void SetLayerMask(Object target, string field, int mask)
        {
            var serialized = new SerializedObject(target);
            serialized.FindProperty(field).intValue = mask;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
