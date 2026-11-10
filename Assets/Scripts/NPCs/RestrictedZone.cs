using System.Collections.Generic;
using UnityEngine;

namespace Contract.NPCs
{
    /// <summary>
    /// Staff-only area (back offices, server rooms). Guards of the same group only grow suspicious
    /// of the player while the player is inside one of their zones.
    /// </summary>
    [RequireComponent(typeof(BoxCollider))]
    public sealed class RestrictedZone : MonoBehaviour
    {
        private static readonly HashSet<string> occupied = new HashSet<string>();

        [SerializeField] private string group = "default";

        public string Group => group;

        public static bool PlayerInside(string zoneGroup) => occupied.Contains(zoneGroup);

        public void Configure(string zoneGroup) => group = zoneGroup;

        private void Awake() => GetComponent<BoxCollider>().isTrigger = true;

        private void OnDisable() => occupied.Remove(group);

        private void OnTriggerEnter(Collider other)
        {
            if (other.CompareTag("Player")) occupied.Add(group);
        }

        private void OnTriggerExit(Collider other)
        {
            if (other.CompareTag("Player")) occupied.Remove(group);
        }
    }
}
