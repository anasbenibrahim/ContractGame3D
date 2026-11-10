using System;
using Contract.Core;
using Contract.Data;
using TMPro;
using UnityEngine;

namespace Contract.Interaction
{
    [DisallowMultipleComponent]
    public sealed class InteractionController : MonoBehaviour
    {
        [SerializeField] private InputReader inputReader;
        [SerializeField] private float interactionRange = 2.75f;
        [SerializeField] private LayerMask interactionMask = ~0;
        [SerializeField] private TMP_Text promptText;
        [SerializeField] private GameObject promptRoot;

        private readonly Collider[] hits = new Collider[32];
        private float rangeMultiplier = 1f;

        /// <summary>Effective reach after character stats / upgrades.</summary>
        public float InteractionRange => interactionRange * rangeMultiplier * PlayerUpgrades.Multiplier(UpgradeEffect.InteractionRange);

        public void SetRangeMultiplier(float multiplier) => rangeMultiplier = Mathf.Max(0.1f, multiplier);

        private float holdSpeedMultiplier = 1f;

        /// <summary>How fast hold interactions (terminals, devices) complete; driven by the Intelligence stat.</summary>
        /// <summary>Character intelligence x Quick Hack upgrades.</summary>
        public float HoldSpeedMultiplier => holdSpeedMultiplier * PlayerUpgrades.Multiplier(UpgradeEffect.HackSpeed);

        public void SetHoldSpeedMultiplier(float multiplier) => holdSpeedMultiplier = Mathf.Max(0.1f, multiplier);
        private IInteractable current;
        private bool promptVisible;
        private IHoldInteractable holdTarget;
        private float holdProgress;

        /// <summary>The interactable currently in focus (null if none).</summary>
        public IInteractable Current => current;

        /// <summary>0..1 progress of an in-progress hold interaction, -1 when none.</summary>
        public float HoldProgress => holdTarget != null ? holdProgress : -1f;

        /// <summary>
        /// Raised after any interaction completes (player gestures, audio, VFX). The gesture is
        /// captured before Interact() runs, because interacting can change what the target offers next.
        /// </summary>
        public event Action<IInteractable, InteractionGesture> Interacted;

        private void Awake()
        {
            ResolvePrompt();
            ApplyPromptVisibility(false);
            promptVisible = false;
        }

        private void OnEnable()
        {
            if (inputReader != null)
            {
                inputReader.InteractPressed += HandleInteract;
            }
        }

        private void OnDisable()
        {
            if (inputReader != null)
            {
                inputReader.InteractPressed -= HandleInteract;
            }

            ApplyPromptVisibility(false);
            promptVisible = false;
            current = null;
        }

        private void Update()
        {
            if (GameManager.Instance != null && GameManager.Instance.InputBlocked)
            {
                holdTarget = null;
                current = null;
                SetPromptVisible(false);
                return;
            }

            current = FindBestInteractable();
            UpdateHold();
            if (current == null)
            {
                SetPromptVisible(false);
                return;
            }

            if (promptText != null)
            {
                promptText.text = BuildPrompt(current);
            }

            SetPromptVisible(true);
        }

        private string BuildPrompt(IInteractable target)
        {
            string prompt = InputHints.ForPrompt(target.GetPrompt(gameObject));
            if (holdTarget != null && ReferenceEquals(holdTarget, target))
            {
                int filled = Mathf.RoundToInt(holdProgress * 10f);
                prompt += $"  [{new string('■', filled)}{new string('□', 10 - filled)}] {Mathf.RoundToInt(holdProgress * 100f)}%";
            }
            else if (target is IHoldInteractable hold && hold.HoldDuration > 0f)
            {
                prompt += InputHints.Touch ? "  (길게 누르기 · hold)" : "  (hold)";
            }

            if (target is IDescribedInteractable described && !string.IsNullOrEmpty(described.Description))
            {
                prompt += $"\n<size=70%><color=#B8BEC8>{described.Description}</color></size>";
            }

            return prompt;
        }

        private void UpdateHold()
        {
            if (holdTarget == null)
            {
                return;
            }

            bool stillValid = ReferenceEquals(current, holdTarget) && holdTarget.CanInteract(gameObject)
                              && inputReader != null && inputReader.IsInteractHeld;
            if (!stillValid)
            {
                holdTarget = null; // released early or walked away: cancel
                return;
            }

            holdProgress += Time.deltaTime * HoldSpeedMultiplier / Mathf.Max(0.01f, holdTarget.HoldDuration);
            if (holdProgress >= 1f)
            {
                IHoldInteractable completed = holdTarget;
                holdTarget = null;
                Perform(completed);
            }
        }

        private IInteractable FindBestInteractable()
        {
            int count = Physics.OverlapSphereNonAlloc(
                transform.position + Vector3.up,
                InteractionRange,
                hits,
                interactionMask,
                QueryTriggerInteraction.Collide);

            IInteractable best = null;
            float bestDistance = float.MaxValue;

            for (int i = 0; i < count; i++)
            {
                Collider hit = hits[i];
                if (hit == null)
                {
                    continue;
                }

                IInteractable candidate = hit.GetComponentInParent<IInteractable>();
                if (candidate == null || !candidate.CanInteract(gameObject))
                {
                    continue;
                }

                Transform target = candidate.InteractionTransform;
                Vector3 position = target != null ? target.position : hit.bounds.center;
                float distance = (position - transform.position).sqrMagnitude;
                if (distance < bestDistance)
                {
                    best = candidate;
                    bestDistance = distance;
                }
            }

            return best;
        }

        private void HandleInteract()
        {
            if (current == null || !current.CanInteract(gameObject))
            {
                return;
            }

            if (current is IHoldInteractable hold && hold.HoldDuration > 0f)
            {
                holdTarget = hold;
                holdProgress = 0f;
                return;
            }

            Perform(current);
        }

        private void Perform(IInteractable target)
        {
            InteractionGesture gesture = target is InteractableBase b ? b.Gesture : InteractionGesture.Reach;
            target.Interact(gameObject);
            Interacted?.Invoke(target, gesture);
            current = null;
            SetPromptVisible(false);
        }

        private void ResolvePrompt()
        {
            if (promptText == null)
            {
                GameObject promptObject = GameObject.Find("Interaction Prompt");
                if (promptObject != null)
                {
                    promptText = promptObject.GetComponent<TMP_Text>();
                }
            }

            if (promptRoot == null && promptText != null)
            {
                Transform parent = promptText.transform.parent;
                if (parent != null && parent.name == "Interaction Prompt Panel")
                {
                    promptRoot = parent.gameObject;
                }
            }
        }

        private void SetPromptVisible(bool visible)
        {
            if (promptVisible == visible)
            {
                return;
            }

            promptVisible = visible;
            ApplyPromptVisibility(visible);
        }

        private void ApplyPromptVisibility(bool visible)
        {
            if (promptRoot != null)
            {
                promptRoot.SetActive(visible);
            }
            else if (promptText != null)
            {
                promptText.gameObject.SetActive(visible);
            }

            if (!visible && promptText != null)
            {
                promptText.text = string.Empty;
            }
        }
    }
}
