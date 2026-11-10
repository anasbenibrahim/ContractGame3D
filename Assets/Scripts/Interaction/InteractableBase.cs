using UnityEngine;

namespace Contract.Interaction
{
    public abstract class InteractableBase : MonoBehaviour, IHoldInteractable, IDescribedInteractable
    {
        public virtual Transform InteractionTransform => transform;

        /// <summary>0 = instant interaction (default).</summary>
        public virtual float HoldDuration => 0f;

        /// <summary>Gesture the player performs.</summary>
        public virtual InteractionGesture Gesture => InteractionGesture.Reach;

        /// <summary>Optional description shown under the prompt.</summary>
        public virtual string Description => null;

        public abstract bool CanInteract(GameObject interactor);

        public abstract string GetPrompt(GameObject interactor);

        public abstract void Interact(GameObject interactor);
    }
}
