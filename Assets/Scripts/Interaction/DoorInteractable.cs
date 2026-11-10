using Contract.Core;
using UnityEngine;

namespace Contract.Interaction
{
    public sealed class DoorInteractable : InteractableBase
    {
        [SerializeField] private float openAngle = 95f;
        [SerializeField] private float rotationSpeed = 220f;
        [SerializeField] private bool startsOpen;
        [SerializeField] private string openPrompt = "[E] CLOSE SAFEHOUSE DOOR";
        [SerializeField] private string closePrompt = "[E] OPEN SAFEHOUSE DOOR";
        [SerializeField] private string interactionId = "safehouse_door";

        private Quaternion closedRotation;
        private Quaternion openRotation;
        private bool isOpen;

        public bool IsOpen => isOpen;

        private void Awake()
        {
            closedRotation = transform.localRotation;
            openRotation = closedRotation * Quaternion.Euler(0f, openAngle, 0f);

            if (startsOpen)
            {
                isOpen = true;
                transform.localRotation = openRotation;
            }
        }

        private void Update()
        {
            Quaternion target = isOpen ? openRotation : closedRotation;
            if (Quaternion.Angle(transform.localRotation, target) > 0.05f)
            {
                transform.localRotation = Quaternion.RotateTowards(
                    transform.localRotation,
                    target,
                    rotationSpeed * Time.deltaTime);
            }
        }

        public override bool CanInteract(GameObject interactor)
        {
            return true;
        }

        public override string GetPrompt(GameObject interactor)
        {
            return isOpen ? openPrompt : closePrompt;
        }

        public override void Interact(GameObject interactor)
        {
            isOpen = !isOpen;
            GameEvents.RaiseInteractionPerformed(interactionId);
        }
    }
}
