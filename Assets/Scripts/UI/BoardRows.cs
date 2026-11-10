using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Contract.UI
{
    /// <summary>Row / tab buttons shared by the safehouse terminal pages (contracts, upgrades, finances).</summary>
    public static class BoardRows
    {
        public static readonly Color Selected = new Color(0.9f, 0.16f, 0.22f, 0.9f);
        public static readonly Color Normal = new Color(0.12f, 0.13f, 0.155f, 1f);

        /// <summary>Removes every child now (detached first, so layouts and child counts update this frame).</summary>
        public static void Clear(RectTransform container)
        {
            for (int i = container.childCount - 1; i >= 0; i--)
            {
                Transform child = container.GetChild(i);
                child.SetParent(null, false);
                Object.Destroy(child.gameObject);
            }
        }

        public static Button Row(RectTransform parent, string label, bool selected, float width, float height = 44f)
        {
            GameObject go = new GameObject("Row", typeof(RectTransform), typeof(Image), typeof(Button), typeof(LayoutElement), typeof(UIButtonFX));
            go.transform.SetParent(parent, false);
            go.GetComponent<LayoutElement>().preferredHeight = height;
            ((RectTransform)go.transform).sizeDelta = new Vector2(width, height);
            Image bg = go.GetComponent<Image>();
            bg.color = selected ? Selected : Normal;
            Button button = go.GetComponent<Button>();
            button.targetGraphic = bg;

            TextMeshProUGUI text = Label(go.transform, label, 18f);
            text.rectTransform.offsetMin = new Vector2(14f, 0f);
            text.rectTransform.offsetMax = new Vector2(-10f, 0f);
            return button;
        }

        /// <summary>Stretched, non-raycast label.</summary>
        public static TextMeshProUGUI Label(Transform parent, string label, float size)
        {
            GameObject textObject = new GameObject("Label", typeof(RectTransform));
            textObject.transform.SetParent(parent, false);
            RectTransform rt = (RectTransform)textObject.transform;
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = rt.offsetMax = Vector2.zero;
            TextMeshProUGUI text = textObject.AddComponent<TextMeshProUGUI>();
            text.text = label;
            text.fontSize = size;
            text.alignment = TextAlignmentOptions.MidlineLeft;
            text.color = new Color(0.93f, 0.94f, 0.96f);
            text.raycastTarget = false;
            text.textWrappingMode = TextWrappingModes.NoWrap;
            return text;
        }
    }
}
