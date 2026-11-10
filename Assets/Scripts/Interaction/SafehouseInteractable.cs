using Contract.Core;
using UnityEngine;

namespace Contract.Interaction
{
    public sealed class SafehouseInteractable : InteractableBase
    {
        [SerializeField] private string safehouseId = "safehouse_01";
        [SerializeField] private GameObject securedIndicator;

        private bool secured;

        public string SafehouseId => safehouseId;
        public bool IsSecured => secured;

        public override bool CanInteract(GameObject interactor)
        {
            return !secured;
        }

        public override string GetPrompt(GameObject interactor)
        {
            return "[E] SECURE SAFEHOUSE";
        }

        public override void Interact(GameObject interactor)
        {
            if (secured)
            {
                return;
            }

            secured = true;
            if (securedIndicator != null)
            {
                securedIndicator.SetActive(true);
            }

            GameEvents.RaiseSafehouseSecured(safehouseId);
            GameEvents.RaiseInteractionPerformed($"safehouse:{safehouseId}");
        }
    }
}
