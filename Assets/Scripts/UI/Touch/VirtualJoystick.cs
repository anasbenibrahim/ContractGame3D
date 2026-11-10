using Contract.Core;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Contract.UI.Touch
{
    /// <summary>
    /// Floating movement joystick for the left side of the screen. A finger touching anywhere in
    /// the zone places the joystick there; dragging moves the knob (clamped to the radius). Output
    /// passes a dead zone, is rescaled to 0-1 and multiplied by the sensitivity, then goes to the
    /// InputReader's touch layer. It follows one finger (by pointer id) so other fingers can use
    /// the camera and buttons at the same time, and resets when that finger lifts or the controls hide.
    /// </summary>
    public sealed class VirtualJoystick : MonoBehaviour, IPointerDownHandler, IDragHandler, IPointerUpHandler
    {
        private const int NoPointer = int.MinValue;

        [SerializeField] private RectTransform zone;
        [SerializeField] private RectTransform baseRect;
        [SerializeField] private RectTransform knob;
        [SerializeField] private Image baseImage;
        [SerializeField] private Image knobImage;
        [SerializeField, Tooltip("Knob travel in canvas units.")] private float radius = 110f;
        [SerializeField, Range(0f, 1f)] private float idleAlpha = 0.35f;

        private InputReader reader;
        private int pointerId = NoPointer;
        private Vector2 restPosition;
        private Vector2 origin;

        /// <summary>Current output (after dead zone and sensitivity), magnitude 0-1.</summary>
        public Vector2 Value { get; private set; }
        public bool Held => pointerId != NoPointer;
        public int PointerId => pointerId;
        public RectTransform Zone => zone;
        public Vector2 BaseLocalPosition => baseRect.localPosition;
        public float Radius => radius;

        public float DeadZone { get; set; } = 0.12f;

        /// <summary>Visual and travel scale of the joystick (settings: joystick size).</summary>
        public float Size
        {
            get => size;
            set
            {
                size = Mathf.Clamp(value, 0.5f, 2f);
                if (baseRect != null) baseRect.localScale = Vector3.one * size;
            }
        }

        private float size = 1f;
        public float Sensitivity { get; set; } = 1f;

        public void Bind(InputReader inputReader) => reader = inputReader;

        private void Awake()
        {
            restPosition = baseRect.localPosition;
            SetAlpha(idleAlpha);
        }

        private void OnDisable() => Release();

        public void OnPointerDown(PointerEventData e)
        {
            if (Held) return; // a second finger in the zone does not steal the joystick
            pointerId = e.pointerId;
            if (!ToLocal(e, out Vector2 local)) return;
            // Keep the whole base inside the zone.
            Rect r = zone.rect;
            float margin = radius * size;
            origin = new Vector2(Mathf.Clamp(local.x, r.xMin + margin, r.xMax - margin), Mathf.Clamp(local.y, r.yMin + margin, r.yMax - margin));
            baseRect.localPosition = origin;
            SetAlpha(1f);
            Track(local);
        }

        public void OnDrag(PointerEventData e)
        {
            if (e.pointerId != pointerId) return;
            if (ToLocal(e, out Vector2 local)) Track(local);
        }

        public void OnPointerUp(PointerEventData e)
        {
            if (e.pointerId == pointerId) Release();
        }

        /// <summary>Lets go: zero output, knob centred, base back at its rest position.</summary>
        public void Release()
        {
            pointerId = NoPointer;
            Value = Vector2.zero;
            if (knob != null) knob.localPosition = Vector3.zero;
            if (baseRect != null && zone != null) baseRect.localPosition = restPosition;
            SetAlpha(idleAlpha);
            reader?.SetTouchMove(Vector2.zero);
        }

        private void Track(Vector2 local)
        {
            float travel = radius * size;
            Vector2 offset = Vector2.ClampMagnitude(local - origin, travel);
            knob.localPosition = offset / size; // knob is a child of the scaled base
            Value = Shape(offset / travel, DeadZone, Sensitivity);
            reader?.SetTouchMove(Value);
        }

        /// <summary>Dead zone (share of the radius), rescaled so output starts at 0 at its edge, times sensitivity.</summary>
        public static Vector2 Shape(Vector2 raw, float deadZone, float sensitivity)
        {
            float m = Mathf.Min(1f, raw.magnitude);
            deadZone = Mathf.Clamp(deadZone, 0f, 0.9f);
            if (m <= deadZone) return Vector2.zero;
            float scaled = (m - deadZone) / (1f - deadZone) * Mathf.Max(0f, sensitivity);
            return raw.normalized * Mathf.Min(1f, scaled);
        }

        private bool ToLocal(PointerEventData e, out Vector2 local) =>
            RectTransformUtility.ScreenPointToLocalPointInRectangle(zone, e.position, e.pressEventCamera, out local);

        private void SetAlpha(float a)
        {
            if (baseImage != null) baseImage.canvasRenderer.SetAlpha(a);
            if (knobImage != null) knobImage.canvasRenderer.SetAlpha(Mathf.Min(1f, a + 0.25f));
        }
    }
}
