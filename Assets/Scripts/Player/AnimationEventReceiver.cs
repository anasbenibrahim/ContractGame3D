using System;
using UnityEngine;

namespace Contract.Player
{
    /// <summary>
    /// Receives animation events from the Animator and forwards them via delegate.
    /// Attach to the same GameObject as the Animator.
    /// </summary>
    public sealed class AnimationEventReceiver : MonoBehaviour
    {
        public event Action<string> OnAnimationEvent;

        public void OnFootstepL() => OnAnimationEvent?.Invoke("FootstepL");
        public void OnFootstepR() => OnAnimationEvent?.Invoke("FootstepR");
        public void OnLand() => OnAnimationEvent?.Invoke("Land");
        public void OnInteractStart() => OnAnimationEvent?.Invoke("InteractStart");
        public void OnInteractEnd() => OnAnimationEvent?.Invoke("InteractEnd");
        public void OnCustomEvent(string eventName) => OnAnimationEvent?.Invoke(eventName);
    }
}