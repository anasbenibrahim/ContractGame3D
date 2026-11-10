using Contract.Core;
using Contract.Data;
using Contract.Missions;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Contract.UI
{
    /// <summary>
    /// Main HUD controller managing all gameplay UI elements.
    /// </summary>
    public sealed class HUDController : MonoBehaviour
    {
        [Header("Money")]
        [SerializeField] private TMP_Text moneyText;

        [Header("Objective")]
        [SerializeField] private TMP_Text objectiveText;
        [SerializeField] private GameObject objectivePanel;

        [Header("Suspicion")]
        [SerializeField] private Slider suspicionSlider;
        [SerializeField] private Image suspicionFill;
        [SerializeField] private Color lowSuspicionColor = Color.green;
        [SerializeField] private Color highSuspicionColor = Color.red;


        [Header("Inventory")]
        [SerializeField] private InventoryUI inventoryUI;

        [Header("Interaction Prompt")]
        [SerializeField] private GameObject interactionPromptPanel;
        [SerializeField] private TMP_Text interactionPromptText;


        private void Awake()
        {
            if (objectivePanel != null) objectivePanel.SetActive(false);
            if (interactionPromptPanel != null) interactionPromptPanel.SetActive(false);
        }

        private void Start()
        {
            GameEvents.MoneyChanged += OnMoneyChanged;
            GameEvents.InteractionPerformed += OnInteractionPerformed;
            GameEvents.NpcAlerted += OnNpcAlerted;
            GameEvents.SuspicionChanged += OnSuspicionChanged;

            GameSession session = GameManager.Instance?.Session;
            if (session != null)
            {
                OnMoneyChanged(session.money);
            }
        }

        private void OnDestroy()
        {
            GameEvents.MoneyChanged -= OnMoneyChanged;
            GameEvents.InteractionPerformed -= OnInteractionPerformed;
            GameEvents.NpcAlerted -= OnNpcAlerted;
            GameEvents.SuspicionChanged -= OnSuspicionChanged;
        }

        private void OnMoneyChanged(int money)
        {
            if (moneyText != null)
            {
                moneyText.text = $"${money:N0}";
            }
        }

        private void ShowObjective(string text)
        {
            if (objectiveText != null)
            {
                objectiveText.text = text;
            }
            if (objectivePanel != null)
            {
                objectivePanel.SetActive(true);
            }
        }

        private void OnInteractionPerformed(string interactionId)
        {
            // Flash interaction feedback
        }

        private void OnNpcAlerted(string npcId)
        {
            // Show alert indicator
        }

        private void OnSuspicionChanged(string npcId, float level)
        {
            if (suspicionSlider != null)
            {
                suspicionSlider.value = level;
            }
            if (suspicionFill != null)
            {
                suspicionFill.color = Color.Lerp(lowSuspicionColor, highSuspicionColor, level);
            }
        }

        public void ShowInteractionPrompt(string prompt)
        {
            if (interactionPromptPanel != null) interactionPromptPanel.SetActive(true);
            if (interactionPromptText != null) interactionPromptText.text = prompt;
        }

        public void HideInteractionPrompt()
        {
            if (interactionPromptPanel != null) interactionPromptPanel.SetActive(false);
        }
    }
}