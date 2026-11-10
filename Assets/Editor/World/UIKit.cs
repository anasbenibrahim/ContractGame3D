using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace Contract.Editor.World
{
    /// <summary>
    /// Shared editor-time UI construction helpers (uGUI + TextMeshPro) for the game's runtime UI
    /// prefabs, so every screen uses the same palette: charcoal panels, red accent, white text.
    /// </summary>
    public static class UIKit
    {
        public static readonly Color Panel = new Color(0.07f, 0.075f, 0.09f, 0.97f);
        public static readonly Color PanelLight = new Color(0.12f, 0.13f, 0.155f, 1f);
        public static readonly Color Accent = new Color(0.9f, 0.16f, 0.22f);
        public static readonly Color TextColor = new Color(0.93f, 0.94f, 0.96f);
        public static readonly Color Muted = new Color(0.62f, 0.66f, 0.72f);

        public static RectTransform Node(Transform parent, string name, Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot, Vector2 position, Vector2 size)
        {
            GameObject go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            RectTransform rt = (RectTransform)go.transform;
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.pivot = pivot;
            rt.anchoredPosition = position;
            rt.sizeDelta = size;
            return rt;
        }

        public static RectTransform Stretch(Transform parent, string name, float left = 0f, float right = 0f, float top = 0f, float bottom = 0f)
        {
            RectTransform rt = Node(parent, name, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            rt.offsetMin = new Vector2(left, bottom);
            rt.offsetMax = new Vector2(-right, -top);
            return rt;
        }

        public static Image Img(RectTransform rt, Color color, bool raycast = false)
        {
            Image image = rt.gameObject.AddComponent<Image>();
            image.color = color;
            image.raycastTarget = raycast;
            return image;
        }

        public static TextMeshProUGUI Text(Transform parent, string name, string text, float size, Color color, TextAlignmentOptions align,
            Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot, Vector2 position, Vector2 box, bool bold = false)
        {
            RectTransform rt = Node(parent, name, anchorMin, anchorMax, pivot, position, box);
            TextMeshProUGUI t = rt.gameObject.AddComponent<TextMeshProUGUI>();
            t.text = text;
            t.fontSize = size;
            t.color = color;
            t.alignment = align;
            t.raycastTarget = false;
            t.textWrappingMode = TextWrappingModes.NoWrap;
            if (bold) t.fontStyle = FontStyles.Bold;
            return t;
        }

        public static Button ButtonAt(Transform parent, string name, string label, Vector2 anchor, Vector2 pivot, Vector2 position, Vector2 size, bool accent = false)
        {
            RectTransform rt = Node(parent, name, anchor, anchor, pivot, position, size);
            Image bg = Img(rt, accent ? Accent : PanelLight, true);
            Button button = rt.gameObject.AddComponent<Button>();
            button.targetGraphic = bg;
            // Hover / focus feedback: slight scale-up plus a red accent edge (white edge on red buttons).
            RectTransform edge = Node(rt, "AccentEdge", new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(0f, 0.5f), Vector2.zero, new Vector2(4f, 0f));
            Image edgeImage = Img(edge, accent ? Color.white : Accent);
            rt.gameObject.AddComponent<Contract.UI.UIButtonFX>().SetAccent(edgeImage);
            ColorBlock colors = button.colors;
            colors.highlightedColor = new Color(1f, 1f, 1f, 1f);
            colors.pressedColor = new Color(0.75f, 0.75f, 0.75f, 1f);
            colors.fadeDuration = 0.08f;
            button.colors = colors;
            TextMeshProUGUI text = Text(rt, "Label", label, 17f, TextColor, TextAlignmentOptions.Center, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero, true);
            if (label.Contains("[")) text.gameObject.AddComponent<Contract.UI.TouchHintText>(); // "[ESC]" hidden on phones
            return button;
        }

        public static Toggle ToggleRow(Transform parent, string name, string label)
        {
            RectTransform row = Node(parent, name, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), Vector2.zero, new Vector2(290f, 30f));
            row.gameObject.AddComponent<LayoutElement>().preferredHeight = 30f;
            RectTransform box = Node(row, "Box", new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), Vector2.zero, new Vector2(22f, 22f));
            Image boxImage = Img(box, PanelLight, true);
            RectTransform check = Node(box, "Check", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(12f, 12f));
            Image checkImage = Img(check, Accent);
            Text(row, "Label", label, 16f, TextColor, TextAlignmentOptions.Left, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(34f, 0f), new Vector2(250f, 26f));
            Toggle toggle = row.gameObject.AddComponent<Toggle>();
            toggle.targetGraphic = boxImage;
            toggle.graphic = checkImage;
            toggle.isOn = true;
            return toggle;
        }

        public static Slider BuildSlider(Transform parent)
        {
            RectTransform rt = Node(parent, "ZoomSlider", new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(50f, 0f), new Vector2(148f, 20f));
            RectTransform track = Stretch(rt, "Track", 0f, 0f, 7f, 7f);
            Img(track, PanelLight);
            RectTransform fillArea = Stretch(rt, "FillArea", 0f, 0f, 7f, 7f);
            RectTransform fill = Stretch(fillArea, "Fill");
            Img(fill, Accent);
            RectTransform handleArea = Stretch(rt, "HandleArea");
            RectTransform handle = Node(handleArea, "Handle", new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(14f, 0f));
            Image handleImage = Img(handle, Color.white, true);
            Slider slider = rt.gameObject.AddComponent<Slider>();
            slider.fillRect = fill;
            slider.handleRect = handle;
            slider.targetGraphic = handleImage;
            slider.direction = Slider.Direction.LeftToRight;
            return slider;
        }

        /// <summary>
        /// Draws a panel above the other HUD canvases (money, minimap, GPS strip ...) with its own
        /// nested canvas, so overlays such as pause and settings are never covered.
        /// </summary>
        public static void DrawAbove(RectTransform panel, int sortingOrder)
        {
            Canvas canvas = panel.gameObject.AddComponent<Canvas>();
            canvas.overrideSorting = true;
            canvas.sortingOrder = sortingOrder;
            panel.gameObject.AddComponent<GraphicRaycaster>();
        }

        /// <summary>Panel opening animation (fade + slight scale), played whenever the panel is shown.</summary>
        public static void Animate(RectTransform panel)
        {
            if (panel.GetComponent<CanvasGroup>() == null) panel.gameObject.AddComponent<CanvasGroup>();
            panel.gameObject.AddComponent<Contract.UI.UIPanelTransition>();
        }

        public static void SetRefs(Object target, params (string field, Object value)[] refs)
        {
            SerializedObject so = new SerializedObject(target);
            foreach ((string field, Object value) in refs)
            {
                SerializedProperty p = so.FindProperty(field);
                if (p == null) throw new System.ArgumentException($"{target.GetType().Name} has no field '{field}'");
                p.objectReferenceValue = value;
            }

            so.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
