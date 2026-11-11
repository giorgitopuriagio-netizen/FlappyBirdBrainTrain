using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace FlappyBird.Presentation
{
    public sealed class UIFactory
    {
        public static readonly Color Ink = new Color32(28, 52, 43, 255);
        public static readonly Color Muted = new Color32(78, 101, 89, 255);
        public static readonly Color Green = new Color32(43, 112, 64, 255);
        public static readonly Color Cream = new Color32(235, 242, 230, 255);
        public static readonly Color Border = new Color32(210, 223, 209, 255);
        public static readonly Color Background = new Color32(241, 245, 235, 255);
        private readonly TMP_FontAsset font;
        public RectTransform Root { get; }

        public UIFactory()
        {
            font = Resources.Load<TMP_FontAsset>("Fonts & Materials/LiberationSans SDF");
            if (font == null) throw new InvalidOperationException("The interface font is missing from Resources.");
            var canvasObject = new GameObject("Interface", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            var canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.pixelPerfect = true;
            var scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1280, 960);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;
            var backdrop = Panel(canvasObject.transform, 0, 0, 0, 0, Background).rectTransform;
            backdrop.anchorMin = Vector2.zero;
            backdrop.anchorMax = Vector2.one;
            backdrop.offsetMin = backdrop.offsetMax = Vector2.zero;
            Root = Rect("Layout", canvasObject.transform, 0, 0, 1280, 960);
            Root.anchorMin = Root.anchorMax = new Vector2(0.5f, 0.5f);
            Root.pivot = new Vector2(0.5f, 0.5f);
            Root.anchoredPosition = Vector2.zero;
            if (UnityEngine.Object.FindFirstObjectByType<EventSystem>() == null)
                new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
        }

        public static RectTransform Rect(string name, Transform parent, float x, float y, float width, float height)
        {
            var result = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            result.SetParent(parent, false);
            result.anchorMin = result.anchorMax = new Vector2(0, 1);
            result.pivot = new Vector2(0, 1);
            result.anchoredPosition = new Vector2(x, -y);
            result.sizeDelta = new Vector2(width, height);
            return result;
        }

        public Image Panel(Transform parent, float x, float y, float width, float height, Color color)
        {
            var rect = Rect("Panel", parent, x, y, width, height);
            var image = rect.gameObject.AddComponent<Image>();
            image.color = color;
            image.raycastTarget = false;
            return image;
        }

        public void Card(float x, float y, float width, float height)
        {
            Panel(Root, x, y + 4, width, height, Border);
            Panel(Root, x, y, width, height, Color.white);
        }

        public TMP_Text Label(Transform parent, string text, float x, float y, float width, float height, int size = 22,
            Color? color = null, TextAlignmentOptions alignment = TextAlignmentOptions.TopLeft, bool bold = false)
        {
            var rect = Rect("Text", parent, x, y, width, height);
            var label = rect.gameObject.AddComponent<TextMeshProUGUI>();
            label.font = font;
            label.text = text;
            label.fontSize = size;
            label.fontStyle = bold ? FontStyles.Bold : FontStyles.Normal;
            label.color = color ?? Ink;
            label.alignment = alignment;
            label.textWrappingMode = TextWrappingModes.Normal;
            label.overflowMode = TextOverflowModes.Truncate;
            label.richText = false;
            label.raycastTarget = false;
            return label;
        }

        public TMP_Text Metric(string caption, float x, float y, float width)
        {
            Panel(Root, x, y, width, 92, Cream);
            Label(Root, caption, x + 20, y + 14, width - 40, 24, 15, Muted, bold: true);
            return Label(Root, "0", x + 20, y + 40, width - 40, 46, 34, bold: true);
        }

        public Button Button(Transform parent, string text, float x, float y, float width, float height, Action clicked,
            bool primary = false, int fontSize = 22)
        {
            var rect = Rect("Button " + text, parent, x, y, width, height);
            var image = rect.gameObject.AddComponent<Image>();
            var button = rect.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            button.onClick.AddListener(() => clicked());
            Label(rect, text, 16, 0, width - 32, height, fontSize, alignment: TextAlignmentOptions.Midline, bold: true);
            Emphasize(button, primary);
            return button;
        }

        public static void Emphasize(Button button, bool selected)
        {
            var colors = button.colors;
            colors.normalColor = selected ? Green : Cream;
            colors.highlightedColor = selected ? new Color32(32, 91, 49, 255) : new Color32(217, 232, 207, 255);
            colors.pressedColor = selected ? new Color32(23, 72, 38, 255) : new Color32(194, 215, 183, 255);
            colors.selectedColor = colors.normalColor;
            colors.disabledColor = selected ? new Color32(103, 133, 109, 255) : new Color32(226, 232, 222, 255);
            colors.fadeDuration = 0.08f;
            button.colors = colors;
            button.GetComponentInChildren<TMP_Text>().color = selected ? Color.white : Ink;
        }

        public TMP_Dropdown Dropdown(Transform parent, float x, float y, float width, Action<int> selected)
        {
            var root = Rect("Selection", parent, x, y, width, 56);
            root.gameObject.AddComponent<Image>().color = Cream;
            var dropdown = root.gameObject.AddComponent<TMP_Dropdown>();
            dropdown.targetGraphic = root.GetComponent<Image>();
            var caption = Label(root, "", 16, 0, width - 64, 56, 19, Ink, TextAlignmentOptions.MidlineLeft);
            Label(root, "v", width - 44, 0, 30, 56, 20, Ink, TextAlignmentOptions.Midline);

            var template = Rect("Template", root, 0, 60, width, 240);
            template.gameObject.AddComponent<Image>().color = Color.white;
            var scroll = template.gameObject.AddComponent<ScrollRect>();
            scroll.horizontal = false;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            var viewport = Rect("Viewport", template, 0, 0, width, 240);
            viewport.anchorMin = Vector2.zero;
            viewport.anchorMax = Vector2.one;
            viewport.offsetMin = viewport.offsetMax = Vector2.zero;
            viewport.gameObject.AddComponent<RectMask2D>();
            var content = Rect("Content", viewport, 0, 0, width, 52);
            content.anchorMax = new Vector2(1, 1);
            content.sizeDelta = new Vector2(0, 52);
            var item = Rect("Item", content, 0, 0, width, 52);
            var toggleImage = item.gameObject.AddComponent<Image>();
            toggleImage.color = Cream;
            var toggle = item.gameObject.AddComponent<Toggle>();
            toggle.targetGraphic = toggleImage;
            toggle.graphic = Panel(item, 12, 20, 12, 12, Green);
            var itemLabel = Label(item, "", 36, 0, width - 48, 52, 18, Ink, TextAlignmentOptions.MidlineLeft);
            scroll.viewport = viewport;
            scroll.content = content;
            dropdown.template = template;
            dropdown.captionText = caption;
            dropdown.itemText = itemLabel;
            dropdown.onValueChanged.AddListener(value => selected(value));
            template.gameObject.SetActive(false);
            return dropdown;
        }

        public void Header(string section)
        {
            Label(Root, "FLAPPY BIRD", 56, 24, 430, 44, 32, bold: true);
            Label(Root, "B R A I N   T R A I N", 58, 70, 430, 24, 14, Muted);
            Label(Root, section, 560, 40, 640, 44, 28, bold: true);
            Panel(Root, 56, 106, 1168, 2, Border);
            Card(528, 128, 696, 768);
        }
    }
}
