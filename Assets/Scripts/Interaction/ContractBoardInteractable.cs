using Contract.Core;
using UnityEngine;

namespace Contract.Interaction
{
    /// <summary>Safehouse terminal: opens the contract board (choose and start a contract in the world).</summary>
    public sealed class ContractBoardInteractable : InteractableBase
    {
        public override string Description => "의뢰 · 업그레이드 · 재정  —  Contracts · Upgrades · Finances";

        public override bool CanInteract(GameObject interactor) => true;

        public override string GetPrompt(GameObject interactor) => "[E] 안전가옥 단말기 · Safehouse terminal";

        public override void Interact(GameObject interactor)
        {
            GameEvents.RaiseInteractionPerformed("contract_board");
            GameEvents.RaiseContractBoardRequested();
        }
    }
}
