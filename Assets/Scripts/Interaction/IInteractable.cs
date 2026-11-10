using UnityEngine;

namespace Contract.Interaction
{
    /// <summary>Body language the player uses for an interaction.</summary>
    public enum InteractionGesture
    {
        Reach,
        Photo,
        Talk
    }

    public interface IInteractable
    {
        Transform InteractionTransform { get; }
        bool CanInteract(GameObject interactor);
        string GetPrompt(GameObject interactor);
        void Interact(GameObject interactor);
    }

    /// <summary>Interactions that require holding the interact button (terminals, planting devices...).</summary>
    public interface IHoldInteractable : IInteractable
    {
        /// <summary>Seconds the button must be held. 0 = instant.</summary>
        float HoldDuration { get; }
    }

    /// <summary>Optional second prompt line describing what the interaction does.</summary>
    public interface IDescribedInteractable : IInteractable
    {
        string Description { get; }
    }
}
