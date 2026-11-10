using System;
using Contract.Core;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Contract.UI
{
    /// <summary>
    /// Handles pause menu functionality.
    /// </summary>
    public sealed class PauseMenuController : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private GameObject pausePanel;
        [SerializeField] private Button resumeButton;
        [SerializeField] private Button settingsButton;
        [SerializeField] private Button mainMenuButton;
        [SerializeField] private Button quitButton;
        [SerializeField] private Button saveButton;
        [SerializeField] private SettingsController settings;

        public bool IsOpen => pausePanel != null && pausePanel.activeSelf;

        private void Awake()
        {
            if (resumeButton != null) resumeButton.onClick.AddListener(OnResume);
            if (settingsButton != null) settingsButton.onClick.AddListener(OnSettings);
            if (mainMenuButton != null) mainMenuButton.onClick.AddListener(OnMainMenu);
            if (quitButton != null) quitButton.onClick.AddListener(OnQuit);
            if (saveButton != null) saveButton.onClick.AddListener(OnSave);

            if (pausePanel != null) pausePanel.SetActive(false);
        }

        private void OnEnable()
        {
            GameEvents.PauseChanged += HandlePauseChanged;
        }

        private void OnDisable()
        {
            GameEvents.PauseChanged -= HandlePauseChanged;
        }

        private void HandlePauseChanged(bool paused)
        {
            if (pausePanel != null)
            {
                pausePanel.SetActive(paused);
            }
        }

        public void TogglePause()
        {
            bool isPaused = GameManager.Instance != null && GameManager.Instance.IsPaused;

            if (isPaused)
            {
                OnResume();
            }
            else
            {
                OnPause();
            }
        }

        public void OnPause()
        {
            if (GameManager.Instance != null)
            {
                GameManager.Instance.SetPaused(true);
            }
            if (pausePanel != null) pausePanel.SetActive(true);
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        public void OnResume()
        {
            if (GameManager.Instance != null)
            {
                GameManager.Instance.SetPaused(false);
            }
            if (pausePanel != null) pausePanel.SetActive(false);
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }

        private void OnSettings()
        {
            if (settings != null) settings.Open(pausePanel);
            else UIManager.Instance?.ShowPanel("settings");
        }

        private void OnMainMenu()
        {
            OnResume();
            GameManager.Instance?.ReturnToMainMenu();
        }

        /// <summary>Manual save; the HUD shows "Saved" when the file is written.</summary>
        private void OnSave()
        {
            GameManager.Instance?.GetService<Contract.Contracts.ISaveService>()?.SaveSlot(GameConstants.Save.DefaultSlot);
        }

        private void OnQuit()
        {
            GameManager.Instance?.QuitGame();
        }
    }
}