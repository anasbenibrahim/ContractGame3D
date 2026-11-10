using Contract.Core;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Contract.UI.Touch
{
    /// <summary>
    /// Drag-to-look surface behind the right-hand buttons. One finger (tracked by pointer id)
    /// rotates the camera; a drag of the full screen height turns the camera 180° at sensitivity 1,
    /// independently of resolution. Touches that start on a button never reach this surface (the
    /// buttons sit above it), so pressing buttons does not move the camera.
    /// </summary>
    public sealed class TouchLookArea : MonoBehaviour, IPointerDownHandler, IDragHandler, IPointerUpHandler
    {
        private const int NoPointer = int.MinValue;
        public const float DegreesPerScreenHeight = 180f;

        private InputReader reader;
        private int pointerId = NoPointer;

        public float SensitivityX { get; set; } = 1f;
        public float SensitivityY { get; set; } = 0.8f;
        public bool Held => pointerId != NoPointer;

        public void Bind(InputReader inputReader) => reader = inputReader;

        private void OnDisable() => Release();

        public void Release() => pointerId = NoPointer;

        public void OnPointerDown(PointerEventData e)
        {
            if (!Held) pointerId = e.pointerId;
        }

        public void OnDrag(PointerEventData e)
        {
            if (e.pointerId != pointerId || reader == null) return;
            reader.AddTouchLook(ToDegrees(e.delta, Screen.height, SensitivityX, SensitivityY));
        }

        public void OnPointerUp(PointerEventData e)
        {
            if (e.pointerId == pointerId) Release();
        }

        /// <summary>Screen-pixel drag to camera degrees (x = yaw, y = pitch up).</summary>
        public static Vector2 ToDegrees(Vector2 pixels, float screenHeight, float sensitivityX, float sensitivityY)
        {
            float perPixel = DegreesPerScreenHeight / Mathf.Max(1f, screenHeight);
            return new Vector2(pixels.x * perPixel * sensitivityX, pixels.y * perPixel * sensitivityY);
        }
    }
}
