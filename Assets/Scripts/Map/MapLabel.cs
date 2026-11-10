using System.Collections.Generic;
using UnityEngine;

namespace Contract.Map
{
    /// <summary>Street / landmark / region name drawn on the full map (crisp UI text, not baked into the map texture).</summary>
    public sealed class MapLabel : MonoBehaviour
    {
        private static readonly List<MapLabel> registry = new List<MapLabel>();
        public static IReadOnlyList<MapLabel> All => registry;

        [SerializeField] private string korean;
        [SerializeField] private string english;
        [Tooltip("0 = always shown, 1 = from medium zoom, 2 = close zoom only")]
        [SerializeField, Range(0, 2)] private int detailLevel;
        [Tooltip("Angle on the map in degrees (90 = runs north-south)")]
        [SerializeField] private float angle;
        [SerializeField] private float size = 1f;

        public string Korean => korean;
        public string English => english;
        public int DetailLevel => detailLevel;
        public float Angle => angle;
        public float Size => size;

        public void Configure(string ko, string en, int level, float mapAngle, float relativeSize)
        {
            korean = ko;
            english = en;
            detailLevel = level;
            angle = mapAngle;
            size = relativeSize;
        }

        private void OnEnable() => registry.Add(this);

        private void OnDisable() => registry.Remove(this);
    }
}
