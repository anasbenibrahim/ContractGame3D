using UnityEngine;

namespace Contract.Activity
{
    /// <summary>
    /// A closed walking loop in a district (sidewalks and crossings, computed on the navmesh by
    /// the route builder). GPS mode moves the character along it by the distance walked in reality.
    /// </summary>
    public sealed class GpsRoute : MonoBehaviour
    {
        [SerializeField] private Vector3[] points = new Vector3[0];
        [SerializeField] private float length;
        private float[] cumulative;

        public static GpsRoute Current { get; private set; }
        public float Length => length;
        public int PointCount => points.Length;

        public void SetPoints(Vector3[] newPoints)
        {
            points = newPoints;
            cumulative = null;
            Prepare();
        }

        private void OnEnable() => Current = this;

        private void OnDisable()
        {
            if (Current == this) Current = null;
        }

        private void Prepare()
        {
            if (cumulative != null && cumulative.Length == points.Length + 1) return;
            cumulative = new float[points.Length + 1];
            for (int i = 0; i < points.Length; i++) cumulative[i + 1] = cumulative[i] + Vector3.Distance(points[i], points[(i + 1) % points.Length]);
            length = cumulative[points.Length];
        }

        /// <summary>Distance along the loop of the route point nearest to <paramref name="position"/>.</summary>
        public double Project(Vector3 position)
        {
            Prepare();
            if (points.Length < 2) return 0;
            double best = 0;
            float bestSq = float.MaxValue;
            for (int i = 0; i < points.Length; i++)
            {
                Vector3 a = points[i], b = points[(i + 1) % points.Length];
                Vector3 ab = b - a;
                float t = ab.sqrMagnitude > 1e-6f ? Mathf.Clamp01(Vector3.Dot(position - a, ab) / ab.sqrMagnitude) : 0f;
                Vector3 p = a + ab * t;
                float sq = (new Vector2(p.x, p.z) - new Vector2(position.x, position.z)).sqrMagnitude;
                if (sq < bestSq)
                {
                    bestSq = sq;
                    best = cumulative[i] + t * (cumulative[i + 1] - cumulative[i]);
                }
            }

            return best;
        }

        /// <summary>Position after travelling <paramref name="meters"/> along the loop (wraps).</summary>
        public Vector3 PointAt(double meters)
        {
            Prepare();
            if (points.Length == 0 || length <= 0f) return transform.position;
            float d = (float)(meters % length);
            if (d < 0) d += length;
            int i = 0;
            while (i < points.Length - 1 && cumulative[i + 1] < d) i++;
            float segment = cumulative[i + 1] - cumulative[i];
            float k = segment > 0f ? (d - cumulative[i]) / segment : 0f;
            return Vector3.Lerp(points[i], points[(i + 1) % points.Length], k);
        }
    }
}
