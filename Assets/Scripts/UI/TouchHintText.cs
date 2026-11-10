using Contract.Core;
using TMPro;
using UnityEngine;

namespace Contract.UI
{
    /// <summary>
    /// Shows touch wording on phones: drops key labels such as "[ESC]" from a label, or replaces
    /// the whole text with <see cref="touchText"/> (e.g. "Right-drag to rotate" -> "Drag to rotate").
    /// The PC text comes back when touch hints are off. Text set by code later is handled too.
    /// </summary>
    [RequireComponent(typeof(TMP_Text))]
    public sealed class TouchHintText : MonoBehaviour
    {
        [SerializeField, TextArea] private string touchText;

        private TMP_Text label;
        private string pcText, shown;
        private bool applied;

        public void SetTouchText(string text) => touchText = text;

        private void Awake() => label = GetComponent<TMP_Text>();

        private void LateUpdate()
        {
            if (InputHints.Touch)
            {
                string current = label.text;
                if (applied && current == shown) return;
                pcText = current;
                shown = string.IsNullOrEmpty(touchText) ? InputHints.ForLabel(current) : touchText;
                applied = true;
                if (shown != current) label.text = shown;
            }
            else if (applied)
            {
                applied = false;
                if (label.text == shown) label.text = pcText;
            }
        }
    }
}
