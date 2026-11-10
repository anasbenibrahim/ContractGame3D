using Contract.Core;
using Contract.Data;
using Contract.Interaction;
using UnityEngine;

namespace Contract.Interaction
{
    /// <summary>
    /// Extraction point that requires a keycard to activate.
    /// </summary>
    public sealed class ExtractionInteractable : InteractableBase
    {
        [Header("Configuration")]
        [SerializeField] private string extractionId = "extraction_01";
        [SerializeField] private string requiredItemId = "keycard";

        [Header("Prompts")]
        [SerializeField] private string lockedPrompt = "EXTRACTION LOCKED // KEYCARD REQUIRED";
        [SerializeField] private string readyPrompt = "[E] EXTRACT";

        [Header("Visual")]
        [SerializeField] private GameObject lockedVisual;
        [SerializeField] private GameObject readyVisual;
        [SerializeField] private Light extractionLight;

        private bool extracted;
        private bool hasRequiredItem;

        public string ExtractionId => extractionId;
        public bool IsExtracted => extracted;
        public bool HasRequiredItem => hasRequiredItem;

        private void OnEnable()
        {
            GameEvents.ItemCollected += HandleItemCollected;
            GameEvents.MissionStarted += Rearm;
            if (string.IsNullOrEmpty(requiredItemId))
            {
                hasRequiredItem = true;
            }
            UpdateVisuals();
        }

        private void OnDisable()
        {
            GameEvents.ItemCollected -= HandleItemCollected;
            GameEvents.MissionStarted -= Rearm;
        }

        private void HandleItemCollected(string itemId)
        {
            if (itemId == requiredItemId)
            {
                hasRequiredItem = true;
                UpdateVisuals();
            }
        }

        /// <summary>Every new contract needs its own extraction.</summary>
        private void Rearm(string missionId)
        {
            extracted = false;
            if (extractionLight != null) extractionLight.color = Color.white;
        }

        public override bool CanInteract(GameObject interactor)
        {
            return !extracted;
        }

        public override string GetPrompt(GameObject interactor)
        {
            return hasRequiredItem ? readyPrompt : lockedPrompt;
        }

        public override void Interact(GameObject interactor)
        {
            if (extracted || !hasRequiredItem) return;

            extracted = true;
            GameEvents.RaiseExtractionActivated(extractionId);
            GameEvents.RaiseObjectiveSignal(ObjectiveType.Extract, extractionId);
            GameEvents.RaiseInteractionPerformed($"extraction:{extractionId}");

            if (extractionLight != null)
            {
                extractionLight.color = Color.green;
            }
        }

        private void UpdateVisuals()
        {
            if (lockedVisual != null) lockedVisual.SetActive(!hasRequiredItem);
            if (readyVisual != null) readyVisual.SetActive(hasRequiredItem);
        }
    }
}