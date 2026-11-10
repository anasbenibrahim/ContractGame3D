using System.Linq;
using Contract.Contracts;
using Contract.Core;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Contract.UI
{
    /// <summary>
    /// Main menu: New Game (with overwrite confirmation), Continue (only when a save exists),
    /// Characters, Settings, Credits, Quit. Escape / gamepad B backs out of any sub-panel.
    /// </summary>
    public sealed class MainMenuController : MonoBehaviour
    {
        [Header("Menu Buttons")]
        [SerializeField] private Button playButton;
        [SerializeField] private Button continueButton;
        [SerializeField] private Button characterSelectionButton;
        [SerializeField] private Button settingsButton;
        [SerializeField] private Button creditsButton;
        [SerializeField] private Button quitButton;

        [Header("Panels")]
        [SerializeField] private GameObject mainPanel;
        [SerializeField] private GameObject creditsPanel;
        [SerializeField] private Button creditsBackButton;
        [SerializeField] private SettingsController settings;

        [Header("New game confirmation")]
        [SerializeField] private GameObject confirmPanel;
        [SerializeField] private Button confirmYes;
        [SerializeField] private Button confirmNo;

        [Header("Info")]
        [SerializeField] private TMP_Text continueDetail;

        public bool CreditsOpen => creditsPanel != null && creditsPanel.activeSelf;
        public bool ConfirmOpen => confirmPanel != null && confirmPanel.activeSelf;
        public Button ContinueButton => continueButton;

        private static ISaveService Save => GameManager.Instance?.GetService<ISaveService>();

        private void Awake()
        {
            playButton.onClick.AddListener(OnPlay);
            continueButton.onClick.AddListener(OnContinue);
            characterSelectionButton.onClick.AddListener(() => GameManager.Instance?.SceneLoader.LoadCharacterSelection());
            settingsButton.onClick.AddListener(() => settings.Open(mainPanel));
            creditsButton.onClick.AddListener(() => Show(creditsPanel));
            creditsBackButton.onClick.AddListener(ReturnToMain);
            quitButton.onClick.AddListener(() => GameManager.Instance?.QuitGame());
            confirmYes.onClick.AddListener(StartNewGame);
            confirmNo.onClick.AddListener(ReturnToMain);
            ReturnToMain();
        }

        private void Start()
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            RefreshContinue();
        }

        private void RefreshContinue()
        {
            bool hasSave = Save != null && Save.SlotExists(GameConstants.Save.DefaultSlot);
            continueButton.interactable = hasSave;
            if (continueDetail == null) return;
            continueDetail.text = "저장된 진행 없음 · No saved career";
            if (hasSave && Save.TryGetSummary(GameConstants.Save.DefaultSlot, out SaveSummary summary))
            {
                string who = Contract.Data.GameDatabase.Load()?.Characters.FirstOrDefault(c => c != null && c.characterId == summary.characterId)?.displayName ?? "—";
                System.DateTime saved = System.DateTimeOffset.FromUnixTimeMilliseconds(summary.savedTimestamp).LocalDateTime;
                continueDetail.text = $"{who} · ${summary.money:N0} · {summary.contractsCompleted} contracts · {saved:yyyy-MM-dd HH:mm}";
            }
            else if (hasSave)
            {
                continueDetail.text = "저장 파일 손상 · Save file damaged";
                continueButton.interactable = false;
            }
        }

        private void OnPlay()
        {
            if (Save != null && Save.SlotExists(GameConstants.Save.DefaultSlot)) Show(confirmPanel); // don't silently overwrite
            else StartNewGame();
        }

        private void StartNewGame()
        {
            ReturnToMain();
            GameManager.Instance?.StartNewGame();
        }

        private void OnContinue()
        {
            if (GameManager.Instance != null && !GameManager.Instance.ContinueGame())
            {
                continueDetail.text = "저장 파일을 불러올 수 없습니다 · The save could not be loaded";
                continueButton.interactable = false;
            }
        }

        private void Show(GameObject panel)
        {
            mainPanel.SetActive(false);
            if (creditsPanel != null) creditsPanel.SetActive(panel == creditsPanel);
            if (confirmPanel != null) confirmPanel.SetActive(panel == confirmPanel);
        }

        public void ReturnToMain()
        {
            if (creditsPanel != null) creditsPanel.SetActive(false);
            if (confirmPanel != null) confirmPanel.SetActive(false);
            mainPanel.SetActive(true);
        }

        private void Update()
        {
            bool back = (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
                        || (Gamepad.current != null && Gamepad.current.buttonEast.wasPressedThisFrame);
            if (!back) return;
            if (settings != null && settings.IsOpen) settings.Close();
            else if (CreditsOpen || ConfirmOpen) ReturnToMain();
        }
    }
}
