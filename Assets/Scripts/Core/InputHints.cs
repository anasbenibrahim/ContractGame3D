using UnityEngine;
using UnityEngine.InputSystem;

namespace Contract.Core
{
    /// <summary>
    /// Which control scheme on-screen hints describe, everywhere (menus too): touch on phones (or
    /// once a touchscreen was used, or when the touch-controls setting is "Always"), keyboard /
    /// mouse / gamepad otherwise. Touch hints drop key labels such as "[E]" and "[ESC]".
    /// </summary>
    public static class InputHints
    {
        private static bool touchSeen;

        /// <summary>Settings value: 0 auto, 1 always, 2 never (TouchControlsMode).</summary>
        public static bool WantedFor(int mode) => mode == 1 || (mode == 0 && (Application.isMobilePlatform || TouchSeen));

        /// <summary>True once a finger has touched a touchscreen in this run.</summary>
        public static bool TouchSeen
        {
            get
            {
                if (!touchSeen && Touchscreen.current != null && Touchscreen.current.primaryTouch.press.isPressed) touchSeen = true;
                return touchSeen;
            }
        }

        public static bool Touch => WantedFor(GameManager.Instance?.Session?.Settings?.touchControls ?? 0);

        /// <summary>Removes a leading "[E] " key label from a prompt while touch hints are shown.</summary>
        public static string ForPrompt(string prompt)
        {
            if (!Touch || string.IsNullOrEmpty(prompt)) return prompt;
            return prompt.StartsWith("[E] ") ? prompt.Substring(4) : prompt;
        }

        /// <summary>Removes "[ESC]"-style key labels from button text while touch hints are shown.</summary>
        public static string ForLabel(string label)
        {
            if (!Touch || string.IsNullOrEmpty(label) || label.IndexOf('[') < 0) return label;
            return System.Text.RegularExpressions.Regex.Replace(label, @"\s*\[(ESC|E|M|Esc)\]", "").TrimEnd();
        }
    }
}
