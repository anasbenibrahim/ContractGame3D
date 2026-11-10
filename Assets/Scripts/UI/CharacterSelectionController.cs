using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Contract.Character;
using Contract.Contracts;
using Contract.Core;
using Contract.Data;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Contract.UI
{
    /// <summary>
    /// Character selection: one card per operative, a 3D turntable preview, stats with their real
    /// gameplay effects, biography, unlocking (paid through the economy) and confirmation, which
    /// stores the choice in the session and continues to mission selection.
    /// </summary>
    public sealed class CharacterSelectionController : MonoBehaviour
    {
        [Header("Cards")]
        [SerializeField] private RectTransform cardContainer;

        [Header("Details")]
        [SerializeField] private TMP_Text characterNameText;
        [SerializeField] private TMP_Text specialtyText;
        [SerializeField] private TMP_Text descriptionText;
        [SerializeField] private Image speedBar;
        [SerializeField] private Image stealthBar;
        [SerializeField] private Image intelligenceBar;
        [SerializeField] private TMP_Text effectsText;
        [SerializeField] private TMP_Text balanceText;
        [SerializeField] private TMP_Text lockText;

        [Header("Actions")]
        [SerializeField] private Button selectButton;
        [SerializeField] private TMP_Text selectLabel;
        [SerializeField] private Button unlockButton;
        [SerializeField] private TMP_Text unlockLabel;
        [SerializeField] private Button backButton;

        [Header("Preview")]
        [SerializeField] private CharacterPreviewStage stage;

        private readonly Dictionary<string, Image> cards = new Dictionary<string, Image>();
        private CharacterData selected;
        private bool confirming;

        public string SelectedId => selected?.characterId;
        public Button ConfirmButton => selectButton;
        public Button UnlockButton => unlockButton;
        public CharacterPreviewStage Stage => stage;

        private static CharacterSelectionManager Characters => CharacterSelectionManager.Instance;

        private void Awake()
        {
            selectButton.onClick.AddListener(Confirm);
            unlockButton.onClick.AddListener(Unlock);
            backButton.onClick.AddListener(() => GameManager.Instance?.SceneLoader.LoadMainMenu());
        }

        private void Start()
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            BuildCards();
            string current = GameManager.Instance?.Session?.selectedCharacterId;
            CharacterData first = Characters.GetAvailableCharacters().FirstOrDefault(c => c.characterId == current)
                                  ?? Characters.GetAvailableCharacters().First();
            Select(first.characterId);
        }

        private void BuildCards()
        {
            foreach (CharacterData data in Characters.GetAvailableCharacters())
            {
                string id = data.characterId;
                GameObject card = new GameObject("Card_" + id, typeof(RectTransform), typeof(Image), typeof(Button), typeof(LayoutElement), typeof(UIButtonFX));
                card.transform.SetParent(cardContainer, false);
                card.GetComponent<LayoutElement>().preferredHeight = 92f;
                Image bg = card.GetComponent<Image>();
                bg.color = new Color(0.12f, 0.13f, 0.155f, 1f);
                card.GetComponent<Button>().targetGraphic = bg;
                card.GetComponent<Button>().onClick.AddListener(() => Select(id));

                Image swatch = new GameObject("Theme", typeof(RectTransform), typeof(Image)).GetComponent<Image>();
                swatch.transform.SetParent(card.transform, false);
                swatch.color = data.appearance.topColor;
                RectTransform sr = swatch.rectTransform;
                sr.anchorMin = new Vector2(0f, 0f);
                sr.anchorMax = new Vector2(0f, 1f);
                sr.sizeDelta = new Vector2(8f, 0f);
                sr.anchoredPosition = new Vector2(4f, 0f);

                TextMeshProUGUI label = new GameObject("Label", typeof(RectTransform)).AddComponent<TextMeshProUGUI>();
                label.transform.SetParent(card.transform, false);
                RectTransform lr = label.rectTransform;
                lr.anchorMin = Vector2.zero;
                lr.anchorMax = Vector2.one;
                lr.offsetMin = new Vector2(24f, 0f);
                lr.offsetMax = new Vector2(-12f, 0f);
                bool unlocked = Characters.IsCharacterUnlocked(id);
                label.text = $"<b>{data.displayName}</b>\n<size=75%><color=#9AA3AE>{SpecialtyKo(data.specialty)} · {data.specialty}</color>" +
                             (unlocked ? "</size>" : $"   <color=#FFB020>잠김 · ${data.unlockCost:N0}</color></size>");
                label.fontSize = 22f;
                label.alignment = TextAlignmentOptions.MidlineLeft;
                label.raycastTarget = false;
                cards[id] = bg;
            }
        }

        private static string SpecialtyKo(string specialty) => specialty switch
        {
            "Speed" => "속도",
            "Stealth" => "은신",
            "Intelligence" => "지능",
            _ => specialty
        };

        public void Select(string characterId)
        {
            if (confirming) return;
            selected = Characters.GetCharacter(characterId);
            if (selected == null) return;
            Characters.SelectCharacter(characterId); // persists into the session for the game world

            foreach (var pair in cards) pair.Value.color = pair.Key == characterId ? new Color(0.9f, 0.16f, 0.22f, 0.92f) : new Color(0.12f, 0.13f, 0.155f, 1f);
            characterNameText.text = selected.displayName;
            specialtyText.text = $"{SpecialtyKo(selected.specialty)} · {selected.specialty.ToUpperInvariant()} SPECIALIST";
            descriptionText.text = selected.description;
            speedBar.fillAmount = selected.moveSpeed / 100f;
            stealthBar.fillAmount = selected.stealthRating / 100f;
            intelligenceBar.fillAmount = selected.intelligence / 100f;
            effectsText.text =
                $"이동 속도 · Move speed  {Percent(selected.moveSpeed)}\n" +
                $"질주 속도 · Sprint speed  {Percent(selected.sprintSpeed)}\n" +
                $"은신 · Detection resistance  {Percent(selected.stealthRating)}\n" +
                $"해킹 속도 · Terminal speed  {Percent(selected.intelligence)}\n" +
                $"상호작용 거리 · Reach  {Percent(selected.interactionRange)}";
            stage.Show(selected);
            RefreshActions();
        }

        /// <summary>Same formula the game uses (CharacterData.StatToMultiplier), shown as a percentage.</summary>
        private static string Percent(float stat)
        {
            float delta = (CharacterData.StatToMultiplier(stat) - 1f) * 100f;
            string color = delta > 0.05f ? "#4CD38A" : delta < -0.05f ? "#FF4D5A" : "#9AA3AE";
            return $"<color={color}>{(delta >= 0 ? "+" : "")}{delta:F1}%</color>";
        }

        private void RefreshActions()
        {
            bool unlocked = Characters.IsCharacterUnlocked(selected.characterId);
            int money = GameManager.Instance?.Session?.money ?? 0;
            balanceText.text = $"잔액 · Balance  ${money:N0}";
            selectButton.interactable = unlocked;
            selectLabel.text = unlocked ? "이 요원으로 시작 · Confirm operative" : "잠김 · Locked";
            lockText.text = unlocked ? "" : $"잠금 해제 비용 · Unlock cost  ${selected.unlockCost:N0}";
            unlockButton.gameObject.SetActive(!unlocked);
            unlockButton.interactable = !unlocked && money >= selected.unlockCost;
            unlockLabel.text = money >= selected.unlockCost ? $"잠금 해제 · Unlock  ${selected.unlockCost:N0}" : "자금 부족 · Not enough money";
        }

        private void Unlock()
        {
            if (selected == null || Characters.IsCharacterUnlocked(selected.characterId)) return;
            IEconomyService economy = GameManager.Instance?.GetService<IEconomyService>();
            if (economy == null || !economy.TrySpend(selected.unlockCost, $"요원 해금 · {selected.displayName}")) return;
            Characters.UnlockCharacter(selected.characterId);
            string id = selected.characterId;
            foreach (Transform child in cardContainer) Destroy(child.gameObject);
            cards.Clear();
            BuildCards();
            Select(id);
        }

        private void Confirm()
        {
            if (selected == null || !Characters.IsCharacterUnlocked(selected.characterId) || confirming) return;
            StartCoroutine(ConfirmRoutine());
        }

        private IEnumerator ConfirmRoutine()
        {
            confirming = true;
            GameManager.Instance?.SetSelectedCharacter(selected.characterId);
            stage.PlayConfirm();
            selectButton.interactable = false;
            yield return new WaitForSecondsRealtime(0.9f);
            GameManager.Instance?.SceneLoader.LoadWorldSelection(); // choose a destination next
        }

        private void Update()
        {
            bool back = (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
                        || (Gamepad.current != null && Gamepad.current.buttonEast.wasPressedThisFrame);
            if (back && !confirming) GameManager.Instance?.SceneLoader.LoadMainMenu();
        }
    }
}
