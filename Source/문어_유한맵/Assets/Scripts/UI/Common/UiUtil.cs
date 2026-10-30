using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace OctopusSurvivors
{
    public static class UiUtil
    {
        static Font _font;

        public static Font Font
        {
            get
            {
                if (_font == null)
                    _font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf")
                            ?? Resources.GetBuiltinResource<Font>("Arial.ttf");
                return _font;
            }
        }

        public static Canvas CreateOverlay(string name)
        {
            var root = new GameObject(name);
            Object.DontDestroyOnLoad(root);
            var canvas = root.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 100;
            var scaler = root.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;
            root.AddComponent<GraphicRaycaster>();

            if (Object.FindFirstObjectByType<EventSystem>() == null)
            {
                var es = new GameObject("EventSystem");
                Object.DontDestroyOnLoad(es);
                es.AddComponent<EventSystem>();
                es.AddComponent<InputSystemUIInputModule>();
            }

            return canvas;
        }

        public static Image Panel(Transform parent, Vector2 anchored, Vector2 size, Color color)
        {
            var go = new GameObject("Panel", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            go.transform.SetParent(parent, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = anchored;
            rt.sizeDelta = size;
            var img = go.GetComponent<Image>();
            img.color = color;
            img.raycastTarget = true;
            return img;
        }

        public static Text Label(Transform parent, Vector2 anchored, Vector2 size, int fontSize, Color color, TextAnchor align = TextAnchor.MiddleLeft)
        {
            var go = new GameObject("Label", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
            go.transform.SetParent(parent, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0f, 1f);
            rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot = new Vector2(0f, 1f);
            rt.anchoredPosition = anchored;
            rt.sizeDelta = size;
            var text = go.GetComponent<Text>();
            text.font = Font;
            text.fontSize = fontSize;
            text.color = color;
            text.alignment = align;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            text.raycastTarget = false;
            return text;
        }

        public static Button Button(Transform parent, Vector2 anchored, Vector2 size, string caption)
        {
            var img = Panel(parent, anchored, size, new Color(0.22f, 0.45f, 0.72f, 0.95f));
            img.gameObject.name = "Button_" + caption;
            var btn = img.gameObject.AddComponent<Button>();
            var colors = btn.colors;
            colors.highlightedColor = new Color(0.32f, 0.58f, 0.9f);
            colors.pressedColor = new Color(0.15f, 0.32f, 0.55f);
            btn.colors = colors;

            var label = Label(img.transform, Vector2.zero, size, 28, Color.white, TextAnchor.MiddleCenter);
            var lrt = label.rectTransform;
            lrt.anchorMin = Vector2.zero;
            lrt.anchorMax = Vector2.one;
            lrt.offsetMin = Vector2.zero;
            lrt.offsetMax = Vector2.zero;
            lrt.pivot = new Vector2(0.5f, 0.5f);
            label.text = caption;
            return btn;
        }

        public static InputField Field(Transform parent, Vector2 anchored, Vector2 size, string placeholder)
        {
            var img = Panel(parent, anchored, size, new Color(0.12f, 0.14f, 0.18f, 0.95f));
            var field = img.gameObject.AddComponent<InputField>();

            var text = Label(img.transform, Vector2.zero, size, 26, Color.white, TextAnchor.MiddleLeft);
            var trt = text.rectTransform;
            trt.anchorMin = Vector2.zero;
            trt.anchorMax = Vector2.one;
            trt.offsetMin = new Vector2(16, 0);
            trt.offsetMax = new Vector2(-16, 0);
            trt.pivot = new Vector2(0.5f, 0.5f);

            var ph = Label(img.transform, Vector2.zero, size, 26, new Color(1, 1, 1, 0.35f), TextAnchor.MiddleLeft);
            var prt = ph.rectTransform;
            prt.anchorMin = Vector2.zero;
            prt.anchorMax = Vector2.one;
            prt.offsetMin = new Vector2(16, 0);
            prt.offsetMax = new Vector2(-16, 0);
            ph.text = placeholder;

            field.textComponent = text;
            field.placeholder = ph;
            field.caretColor = Color.white;
            return field;
        }

        public static Image Bar(Transform parent, Vector2 anchored, Vector2 size, Color back, Color fill, out Image fillImage)
        {
            var bg = Panel(parent, anchored, size, back);
            var rt = bg.rectTransform;
            rt.anchorMin = new Vector2(0f, 1f);
            rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot = new Vector2(0f, 1f);

            var fillGo = new GameObject("Fill", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            fillGo.transform.SetParent(bg.transform, false);
            var frt = fillGo.GetComponent<RectTransform>();
            frt.anchorMin = Vector2.zero;
            frt.anchorMax = new Vector2(1f, 1f);
            frt.offsetMin = new Vector2(3, 3);
            frt.offsetMax = new Vector2(-3, -3);
            fillImage = fillGo.GetComponent<Image>();
            fillImage.sprite = SpriteFactory.Square(Color.white);
            fillImage.color = fill;
            fillImage.type = Image.Type.Filled;
            fillImage.fillMethod = Image.FillMethod.Horizontal;
            fillImage.raycastTarget = false;
            return bg;
        }
    }
}
