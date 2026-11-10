using Contract.Interaction;
using UnityEngine;

namespace Contract.Player
{
    /// <summary>Plays the matching upper-body gesture on the player's rig when an interaction completes.</summary>
    [RequireComponent(typeof(InteractionController))]
    public sealed class PlayerGestureDriver : MonoBehaviour
    {
        [SerializeField] private float gestureSeconds = 0.9f;

        private InteractionController interaction;

        private void Awake() => interaction = GetComponent<InteractionController>();

        private void OnEnable() => interaction.Interacted += OnInteracted;

        private void OnDisable() => interaction.Interacted -= OnInteracted;

        private void OnInteracted(IInteractable target, InteractionGesture gesture)
        {
            PlaceholderRig rig = GetComponentInChildren<PlaceholderRig>();
            if (rig == null) return;

            rig.PlayGesture(gesture switch
            {
                InteractionGesture.Photo => RigGesture.Photo,
                InteractionGesture.Talk => RigGesture.Talk,
                _ => RigGesture.Reach
            }, gesture == InteractionGesture.Talk ? 2.5f : gestureSeconds);
        }
    }
}
