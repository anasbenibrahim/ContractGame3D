using Contract.Interaction;
using UnityEngine;

namespace Contract.NPCs
{
    /// <summary>"Talk" interaction for ordinary NPCs (civilians and guards).</summary>
    [RequireComponent(typeof(NpcController))]
    public sealed class CivilianInteractable : InteractableBase
    {
        private NpcController npc;

        private void Awake() => npc = GetComponent<NpcController>();

        public override InteractionGesture Gesture => InteractionGesture.Talk;

        public override bool CanInteract(GameObject interactor) => npc.CurrentState != NpcState.Talking;

        public override string GetPrompt(GameObject interactor) => "[E] 말 걸기 · Talk";

        public override void Interact(GameObject interactor) => npc.Talk();
    }
}
