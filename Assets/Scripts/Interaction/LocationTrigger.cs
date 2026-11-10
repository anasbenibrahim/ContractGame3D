using Contract.Core;
using Contract.Data;
using UnityEngine;

namespace Contract.Interaction
{
    /// <summary>
    /// Trigger volume for "reach / discover a location" objectives. Raises a ReachLocation signal
    /// the first time the player enters (and again after <see cref="Rearm"/>).
    /// </summary>
    [RequireComponent(typeof(BoxCollider))]
    public sealed class LocationTrigger : MonoBehaviour
    {
        [SerializeField] private string locationId = "location";
        [SerializeField] private string displayName = "Location";
        [Tooltip("Secret locations announce themselves as discoveries.")]
        [SerializeField] private bool secret;

        private bool reached;

        public string LocationId => locationId;
        public string DisplayName => displayName;
        public bool Reached => reached;

        public void Configure(string id, string name, bool isSecret)
        {
            locationId = id;
            displayName = name;
            secret = isSecret;
        }

        private void Reset() => GetComponent<BoxCollider>().isTrigger = true;

        private void Awake() => GetComponent<BoxCollider>().isTrigger = true;

        private void OnEnable() => GameEvents.MissionStarted += OnMissionStarted;

        private void OnDisable() => GameEvents.MissionStarted -= OnMissionStarted;

        /// <summary>A new contract re-arms the trigger (and counts the player if already standing inside).</summary>
        private void OnMissionStarted(string missionId) => reached = false;

        public void Rearm() => reached = false;

        private void OnTriggerEnter(Collider other) => Reach(other);

        private void OnTriggerStay(Collider other) => Reach(other);

        private void Reach(Collider other)
        {
            if (reached || !other.CompareTag("Player")) return;
            reached = true;
            GameEvents.RaiseInteractionPerformed(secret ? $"discover:{locationId}" : $"location:{locationId}");
            GameEvents.RaiseObjectiveSignal(ObjectiveType.ReachLocation, locationId);
        }
    }
}
