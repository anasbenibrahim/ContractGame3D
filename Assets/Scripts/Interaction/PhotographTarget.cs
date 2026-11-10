using Contract.Core;
using Contract.Data;
using UnityEngine;

namespace Contract.Interaction
{
    /// <summary>
    /// Evidence that can be photographed (non-invasive). Requires a clear line of sight from the
    /// player's eye height; triggers a camera flash and raises a PhotographSubject signal.
    /// </summary>
    public sealed class PhotographTarget : InteractableBase
    {
        [SerializeField] private string targetId = "evidence_photo";
        [SerializeField] private string subjectLabel = "증거";
        [SerializeField] private string description;
        [SerializeField] private bool singleUse = true;

        public bool Photographed { get; private set; }
        public string TargetId => targetId;
        public override string Description => description;
        public override InteractionGesture Gesture => InteractionGesture.Photo;

        public void Configure(string id, string label, string desc)
        {
            targetId = id;
            subjectLabel = label;
            description = desc;
        }

        public override bool CanInteract(GameObject interactor) =>
            !(singleUse && Photographed) && HasLineOfSight(interactor.transform, transform);

        public override string GetPrompt(GameObject interactor) => $"[E] 사진 촬영 · Photograph {subjectLabel}";

        public override void Interact(GameObject interactor)
        {
            if (!CanInteract(interactor)) return;
            Photographed = true;
            Snap(targetId);
        }

        /// <summary>Shared camera flash + signal, also used by mission characters.</summary>
        public static void Snap(string id)
        {
            GameEvents.RaiseScreenFlash(Color.white, 0.15f);
            GameEvents.RaiseInteractionPerformed($"photo:{id}");
            GameEvents.RaiseObjectiveSignal(ObjectiveType.PhotographSubject, id);
        }

        /// <summary>True when nothing solid blocks the view between the two transforms (eye height).</summary>
        public static bool HasLineOfSight(Transform viewer, Transform target)
        {
            Vector3 eye = viewer.position + Vector3.up * 1.6f;
            Vector3 aim = target.position + Vector3.up * 0.8f;
            Vector3 dir = aim - eye;
            RaycastHit[] hits = Physics.RaycastAll(eye, dir.normalized, dir.magnitude, ~0, QueryTriggerInteraction.Ignore);
            foreach (RaycastHit hit in hits)
            {
                if (hit.transform.IsChildOf(viewer) || hit.transform.IsChildOf(target)) continue;
                return false;
            }

            return true;
        }
    }
}
