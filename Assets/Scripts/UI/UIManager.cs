using System;
using Contract.Core;
using Contract.Data;
using Contract.Missions;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Contract.UI
{
    /// <summary>
    /// Central UI manager handling HUD, panels, and UI state.
    /// </summary>
    public sealed class UIManager : MonoBehaviour, IUIService
    {
        public static UIManager Instance { get; private set; }

        [Header("Canvas")]
        [SerializeField] private Canvas mainCanvas;

        [Header("Panels")]
        [SerializeField] private GameObject hudPanel;
        [SerializeField] private GameObject pausePanel;
        [SerializeField] private GameObject settingsPanel;
        [SerializeField] private GameObject missionCompletePanel;
        [SerializeField] private GameObject victoryPanel;
        [SerializeField] private GameObject gameOverPanel;
        [SerializeField] private GameObject characterSelectionPanel;
        [SerializeField] private GameObject missionSelectionPanel;
        [SerializeField] private GameObject upgradeShopPanel;

        [Header("HUD Elements")]
        [SerializeField] private TMP_Text moneyText;
        [SerializeField] private TMP_Text objectiveText;
        [SerializeField] private Slider suspicionSlider;
        [SerializeField] private Image suspicionFill;
        [SerializeField] private GameObject minimapContainer;

        [Header("Notifications")]
        [SerializeField] private GameObject notificationPrefab;
        [SerializeField] private Transform notificationContainer;
        [SerializeField] private float notificationDuration = 3f;

        private GameObject activePanel;

        private void Awake()
        {
            // Scene-owned service: every gameplay scene provides its own instance with its own
            // UI/camera references, so it must not persist or be created empty at boot.
            Instance = this;

            if (GameManager.Instance != null)
            {
                GameManager.Instance.RegisterService<IUIService>(this);
            }

            HideAllPanels();
        }

        private void Start()
        {
            ShowHUD();
            SubscribeToEvents();
        }

        private void SubscribeToEvents()
        {
            GameEvents.MoneyChanged += OnMoneyChanged;
            GameEvents.MissionCompleted += OnMissionCompleted;
            GameEvents.VictoryReached += OnVictoryReached;
            GameEvents.GameOver += OnGameOver;
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
                GameManager.Instance?.Services.Unregister<IUIService>();
            }
            GameEvents.MoneyChanged -= OnMoneyChanged;
            GameEvents.MissionCompleted -= OnMissionCompleted;
            GameEvents.VictoryReached -= OnVictoryReached;
            GameEvents.GameOver -= OnGameOver;
        }

        private void OnMoneyChanged(int money)
        {
            if (moneyText != null)
            {
                moneyText.text = $"${money:N0}";
            }
        }

        private void OnMissionCompleted(string missionId)
        {
            if (missionCompletePanel != null)
            {
                missionCompletePanel.SetActive(true);
            }
        }

        private void OnVictoryReached()
        {
            if (victoryPanel != null)
            {
                victoryPanel.SetActive(true);
            }
        }

        private void OnGameOver()
        {
            if (gameOverPanel != null)
            {
                gameOverPanel.SetActive(true);
            }
        }

        public void ShowHUD()
        {
            if (hudPanel != null) hudPanel.SetActive(true);
        }

        public void HideHUD()
        {
            if (hudPanel != null) hudPanel.SetActive(false);
        }

        public void ShowPanel(string panelName)
        {
            GameObject panel = FindPanel(panelName);
            if (panel != null)
            {
                if (activePanel != null) activePanel.SetActive(false);
                panel.SetActive(true);
                activePanel = panel;
            }
        }

        public void HidePanel(string panelName)
        {
            GameObject panel = FindPanel(panelName);
            if (panel != null) panel.SetActive(false);
            if (activePanel == panel) activePanel = null;
        }

        private GameObject FindPanel(string name)
        {
            switch (name.ToLower())
            {
                case "pause": return pausePanel;
                case "settings": return settingsPanel;
                case "characterselection": return characterSelectionPanel;
                case "missionselection": return missionSelectionPanel;
                case "upgradeshop": return upgradeShopPanel;
                default: return null;
            }
        }

        public void ShowObjective(string text)
        {
            if (objectiveText != null)
            {
                objectiveText.text = text;
                objectiveText.gameObject.SetActive(true);
            }
        }

        public void HideObjective()
        {
            if (objectiveText != null) objectiveText.gameObject.SetActive(false);
        }

        public void ShowNotification(string message, float duration = 3f)
        {
            if (notificationPrefab != null && notificationContainer != null)
            {
                GameObject notif = Instantiate(notificationPrefab, notificationContainer);
                TMP_Text text = notif.GetComponentInChildren<TMP_Text>();
                if (text != null) text.text = message;
                Destroy(notif, duration);
            }
        }

        public void ShowMissionCompleted(string missionName, int reward)
        {
            ShowNotification($"Mission Complete: {missionName} +${reward:N0}", 5f);
        }

        public void ShowVictory() { }
        public void ShowGameOver() { }

        private void HideAllPanels()
        {
            if (pausePanel != null) pausePanel.SetActive(false);
            if (settingsPanel != null) settingsPanel.SetActive(false);
            if (missionCompletePanel != null) missionCompletePanel.SetActive(false);
            if (victoryPanel != null) victoryPanel.SetActive(false);
            if (gameOverPanel != null) gameOverPanel.SetActive(false);
            if (characterSelectionPanel != null) characterSelectionPanel.SetActive(false);
            if (missionSelectionPanel != null) missionSelectionPanel.SetActive(false);
            if (upgradeShopPanel != null) upgradeShopPanel.SetActive(false);
        }
    }
}