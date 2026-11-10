using System.Collections.Generic;
using UnityEngine;

namespace Contract.NPCs
{
    public enum PoiCategory
    {
        Shop,       // shopfronts, convenience stores
        Food,       // restaurants, 포장마차, cafés
        Transit,    // bus stops, subway entrance
        Bench,      // places to sit / wait
        Viewpoint,  // landmarks tourists stop at
        Crossing,   // crosswalk ends (waiting for the light)
        Patrol,     // security posts
        Lobby       // building lobbies
    }

    /// <summary>
    /// A place NPCs walk to and linger at (facing <see cref="Transform.forward"/>). Registered in a
    /// static list so the crowd can pick destinations without scene searches.
    /// </summary>
    public sealed class NpcPointOfInterest : MonoBehaviour
    {
        private static readonly List<NpcPointOfInterest> registry = new List<NpcPointOfInterest>();
        public static IReadOnlyList<NpcPointOfInterest> All => registry;

        [SerializeField] private PoiCategory category = PoiCategory.Shop;
        [SerializeField] private Vector2 dwellSeconds = new Vector2(4f, 12f);
        [SerializeField] private float radius = 1.5f;
        [Tooltip("Patrol posts are only used by guards of the same group.")]
        [SerializeField] private string group = "";

        public PoiCategory Category => category;
        public float Radius => radius;
        public string Group => group;
        public float RandomDwell() => Random.Range(dwellSeconds.x, dwellSeconds.y);

        public void Configure(PoiCategory poiCategory, Vector2 dwell, float poiRadius, string poiGroup = "")
        {
            category = poiCategory;
            dwellSeconds = dwell;
            radius = poiRadius;
            group = poiGroup;
        }

        private void OnEnable() => registry.Add(this);

        private void OnDisable() => registry.Remove(this);

        private void OnDrawGizmos()
        {
            Gizmos.color = category == PoiCategory.Patrol ? Color.red : Color.cyan;
            Gizmos.DrawWireSphere(transform.position, radius);
            Gizmos.DrawRay(transform.position, transform.forward);
        }
    }
}
