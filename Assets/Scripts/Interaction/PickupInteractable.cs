using Contract.Core;
using Contract.Data;
using UnityEngine;

namespace Contract.Interaction
{
    public sealed class PickupInteractable : InteractableBase
    {
        [SerializeField] private string itemId = "keycard";
        [SerializeField] private string displayName = "KEYCARD";
        [SerializeField] private bool hideOnCollect = true;
        [SerializeField] private string description;
        [Tooltip("If set, can only be picked up while this contract is active (prevents soft-locking it).")]
        [SerializeField] private string missionId;

        public override string Description => description;

        public void SetMission(string id) => missionId = id;

        private bool collected;

        public string ItemId => itemId;
        public bool IsCollected => collected;

        private void OnEnable() => GameEvents.MissionStarted += OnMissionStarted;

        private void OnDisable() => GameEvents.MissionStarted -= OnMissionStarted;

        /// <summary>Retrying a contract puts its items back.</summary>
        private void OnMissionStarted(string startedMissionId)
        {
            if (string.IsNullOrEmpty(missionId) || startedMissionId != missionId || !collected) return;
            collected = false;
            SetVisible(true);
        }

        private void SetVisible(bool visible)
        {
            foreach (Renderer r in GetComponentsInChildren<Renderer>(true)) r.enabled = visible;
            foreach (Collider c in GetComponentsInChildren<Collider>(true)) c.enabled = visible;
        }

        public override bool CanInteract(GameObject interactor)
        {
            return !collected && (string.IsNullOrEmpty(missionId) || GameManager.Instance?.Session?.activeMissionId == missionId);
        }

        public override string GetPrompt(GameObject interactor)
        {
            return $"[E] TAKE {displayName}";
        }

        public override void Interact(GameObject interactor)
        {
            if (collected)
            {
                return;
            }

            collected = true;
            GameEvents.RaiseItemCollected(itemId);
            GameEvents.RaiseObjectiveSignal(ObjectiveType.CollectItem, itemId);
            GameEvents.RaiseInteractionPerformed($"pickup:{itemId}");

            if (hideOnCollect)
            {
                // Mission items only hide (they come back if the contract is retried); others are removed.
                if (string.IsNullOrEmpty(missionId)) gameObject.SetActive(false);
                else SetVisible(false);
            }
        }
    }
}
