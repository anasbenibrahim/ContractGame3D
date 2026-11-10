using UnityEngine;

namespace Contract.UI
{
    /// <summary>
    /// Keeps a canvas's content inside the device safe area (notches, camera cut-outs, rounded
    /// corners, gesture bars). On start it moves the canvas's children into a "SafeArea" rect
    /// (layout unchanged: the rect starts full-screen), then fits that rect to Screen.safeArea and
    /// refits when the safe area, resolution or orientation changes. Every child moves, so the draw
    /// order is unchanged (full-screen dimmers then stop at the safe area: the notch strip stays clear).
    /// </summary>
    [RequireComponent(typeof(Canvas))]
    public sealed class SafeAreaFitter : MonoBehaviour
    {
        /// <summary>Tests / editor preview: a normalised safe rect (0-1) used instead of Screen.safeArea.</summary>
        public static Rect? Simulated { get; set; }

        private RectTransform area;
        private Rect applied = new Rect(-1f, -1f, -1f, -1f);

        public RectTransform Area => area;

        /// <summary>The safe area as a normalised rect (0-1 of the screen).</summary>
        public static Rect Normalised
        {
            get
            {
                if (Simulated.HasValue) return Simulated.Value;
                Rect safe = Screen.safeArea;
                float w = Mathf.Max(1, Screen.width), h = Mathf.Max(1, Screen.height);
                return Rect.MinMaxRect(safe.xMin / w, safe.yMin / h, safe.xMax / w, safe.yMax / h);
            }
        }

        private void Awake()
        {
            var go = new GameObject("SafeArea", typeof(RectTransform));
            area = (RectTransform)go.transform;
            area.SetParent(transform, false);
            area.anchorMin = Vector2.zero;
            area.anchorMax = Vector2.one;
            area.offsetMin = area.offsetMax = Vector2.zero;

            var children = new System.Collections.Generic.List<Transform>();
            foreach (Transform child in transform)
                if (child != area) children.Add(child);
            foreach (Transform child in children) child.SetParent(area, false); // in order: draw order kept
            Fit();
        }

        private void LateUpdate() => Fit();

        private void Fit()
        {
            Rect safe = Normalised;
            if (safe == applied) return;
            applied = safe;
            area.anchorMin = safe.min;
            area.anchorMax = safe.max;
            area.offsetMin = area.offsetMax = Vector2.zero;
        }
    }
}
