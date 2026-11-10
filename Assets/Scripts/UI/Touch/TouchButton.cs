using System;
using Contract.Core;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Contract.UI.Touch
{
    /// <summary>
    /// A large on-screen action button. Tap fires on finger down (no wait for release), Hold
    /// reports held while the finger stays down, Toggle flips on each press. Shows a pressed state
    /// (darker, slightly smaller) and an "on" state for toggles, ticks the haptics, and follows
    /// one finger by pointer id so several buttons can be held at once.
    /// </summary>
    public sealed class TouchButton : MonoBehaviour, IPointerDownHandler, IPointerUpHandler
    {
        public enum Mode { Tap, Hold, Toggle }

        private const int NoPointer = int.MinValue;

        [SerializeField] private Mode mode = Mode.Tap;
        [SerializeField] private Image background;
        [SerializeField] private Image icon;
        [SerializeField] private Color normalColor = new Color(0.07f, 0.075f, 0.09f, 0.62f);
        [SerializeField] private Color pressedColor = new Color(0.9f, 0.16f, 0.22f, 0.9f);
        [SerializeField] private Color onColor = new Color(0.9f, 0.16f, 0.22f, 0.7f);

        private int pointerId = NoPointer;
        private bool on;
        private float dim = 1f;

        public event Action Pressed;
        public event Action Released;
        public event Action<bool> Toggled;

        public Mode ButtonMode => mode;
        public bool Held => pointerId != NoPointer;

        public bool On
        {
            get => on;
            set
            {
                if (on == value) return;
                on = value;
                Refresh();
            }
        }

        /// <summary>Fades the icon (e.g. Interact with nothing in reach); the button still works.</summary>
        public float Dim
        {
            get => dim;
            set
            {
                dim = Mathf.Clamp01(value);
                Refresh();
            }
        }

        private void OnEnable() => Refresh();

        private void OnDisable()
        {
            if (Held) Release();
        }

        public void OnPointerDown(PointerEventData e)
        {
            if (Held) return;
            pointerId = e.pointerId;
            Haptics.Tick();
            if (mode == Mode.Toggle)
            {
                on = !on;
                Toggled?.Invoke(on);
            }

            Pressed?.Invoke();
            Refresh();
        }

        public void OnPointerUp(PointerEventData e)
        {
            if (e.pointerId == pointerId) Release();
        }

        /// <summary>Finger lifted (or controls hidden): ends a hold.</summary>
        public void Release()
        {
            if (!Held) return;
            pointerId = NoPointer;
            Released?.Invoke();
            Refresh();
        }

        private void Refresh()
        {
            if (background != null) background.color = Held ? pressedColor : on ? onColor : normalColor;
            transform.localScale = Vector3.one * (Held ? 0.92f : 1f);
            if (icon != null)
            {
                Color c = icon.color;
                c.a = Held || on ? 1f : 0.9f * dim + 0.1f;
                icon.color = c;
            }
        }
    }
}
