using System.Collections.Generic;
using System.Linq;
using Contract.Core;
using Contract.Data;
using Contract.Regions;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Contract.UI
{
    /// <summary>
    /// World Selection: browse South Korea destinations, preview them, read their districts,
    /// contracts, completion and unlock requirements, unlock them (or a district) with in-game
    /// money and enter an unlocked one. Destinations whose districts are not built are shown as
    /// "in development" and cannot be unlocked or entered.
    /// </summary>
    public sealed class WorldSelectionView : MonoBehaviour
    {
        [SerializeField] private RectTransform cardContainer;
        [SerializeField] private RawImage preview;
        [SerializeField] private GameObject previewPlaceholder;
        [SerializeField] private TMP_Text placeholderText;
        [SerializeField] private TMP_Text title;
        [SerializeField] private TMP_Text description;
        [SerializeField] private TMP_Text status;
        [SerializeField] private TMP_Text districts;
        [SerializeField] private TMP_Text progressText;
        [SerializeField] private Image progressFill;
        [SerializeField] private TMP_Text balance;
        [SerializeField] private Button unlockButton;
        [SerializeField] private TMP_Text unlockLabel;
        [SerializeField] private Button districtButton;
        [SerializeField] private TMP_Text districtLabel;
        [SerializeField] private Button enterButton;
        [SerializeField] private TMP_Text enterLabel;
        [SerializeField] private Button backButton;

        private readonly Dictionary<string, Image> cards = new Dictionary<string, Image>();
        private DestinationData selected;
        private RegionData lockedDistrict;

        public string SelectedId => selected?.destinationId;
        public Button UnlockButton => unlockButton;
        public Button EnterButton => enterButton;
        public Button DistrictButton => districtButton;
        public int CardCount => cardContainer.childCount;
        public string StatusText => status.text;

        private static DestinationManager Destinations => DestinationManager.Instance;
        private static IRegionService Regions => GameManager.Instance?.GetService<IRegionService>();
        private static GameSession Session => GameManager.Instance?.Session;

        private void Awake()
        {
            unlockButton.onClick.AddListener(() => { Destinations.Unlock(selected); Rebuild(); });
            districtButton.onClick.AddListener(() => { Destinations.UnlockDistrict(lockedDistrict); Rebuild(); });
            enterButton.onClick.AddListener(Enter);
            backButton.onClick.AddListener(() => GameManager.Instance?.SceneLoader.LoadCharacterSelection());
        }

        private void Start()
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            selected = Destinations.Current ?? Destinations.All.FirstOrDefault();
            Rebuild();
        }

        public void Select(string destinationId)
        {
            selected = Destinations.Get(destinationId);
            Rebuild();
        }

        private void Enter()
        {
            if (!Destinations.Select(selected)) return;
            GameManager.Instance?.SceneLoader.LoadMissionSelection();
        }

        private void Update()
        {
            bool back = (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
                        || (Gamepad.current != null && Gamepad.current.buttonEast.wasPressedThisFrame);
            if (back) GameManager.Instance?.SceneLoader.LoadCharacterSelection();
        }

        // ---- presentation ---------------------------------------------------------------------------

        private void Rebuild()
        {
            BoardRows.Clear(cardContainer);
            cards.Clear();
            foreach (DestinationData d in Destinations.All)
            {
                string id = d.destinationId;
                Button card = BoardRows.Row(cardContainer, $"<b>{d.koreanName}</b>  {d.displayName}\n<size=72%>{Chip(d)}</size>", d == selected, 520f, 84f);
                card.onClick.AddListener(() => Select(id));
                cards[id] = card.targetGraphic as Image;
            }

            balance.text = $"잔액 · Balance  ${Session?.money ?? 0:N0}";
            if (selected == null) return;
            ShowDetail(selected);
        }

        private string Chip(DestinationData d)
        {
            if (!Destinations.IsBuilt(d)) return "<color=#7A808A>개발 중 · In development</color>";
            if (!Destinations.IsUnlocked(d)) return $"<color=#FFB020>잠김 · ${d.unlockCost:N0}</color>";
            return $"<color=#4CD38A>열림 · Open</color>  <color=#9AA3AE>{Destinations.Completion(d):P0} complete</color>";
        }

        private void ShowDetail(DestinationData d)
        {
            bool built = Destinations.IsBuilt(d), unlocked = Destinations.IsUnlocked(d);
            preview.texture = d.preview;
            preview.gameObject.SetActive(d.preview != null);
            previewPlaceholder.SetActive(d.preview == null);
            placeholderText.text = $"{d.koreanName}\n<size=40%>{(built ? d.displayName : "개발 중 · In development")}</size>";
            title.text = $"{d.koreanName}  <size=60%><color=#9AA3AE>{d.displayName}</color></size>";
            description.text = d.description;

            status.text = !built ? "개발 중 · Not playable yet: its districts have not been built."
                : unlocked ? "열림 · Unlocked — choose a contract in one of its districts."
                : $"잠김 · Locked — unlock for ${d.unlockCost:N0}.";

            districts.text = string.Join("\n", Destinations.Districts(d).Select(r =>
            {
                string state = !DestinationManager.IsDistrictBuilt(r) ? "<color=#7A808A>개발 중 · in development</color>"
                    : Regions.IsRegionUnlocked(r.regionId) ? $"<color=#4CD38A>✓ 열림 · open</color>  <color=#9AA3AE>{r.missionIds?.Length ?? 0} contracts</color>"
                    : $"<color=#FFB020>잠김 · ${r.unlockCost:N0}</color>";
                return $"• {r.regionName}   {state}";
            }).Concat((d.plannedDistricts ?? new string[0]).Select(n => $"• {n}   <color=#7A808A>개발 중 · in development</color>")));

            int contracts = Destinations.ContractCount(d);
            float completion = Destinations.Completion(d);
            progressText.text = $"계약 · Contracts {contracts}   ·   완료 · {completion:P0}";
            progressFill.fillAmount = completion;

            unlockButton.gameObject.SetActive(!unlocked);
            unlockButton.interactable = Destinations.CanUnlock(d, out string why);
            unlockLabel.text = built ? (unlockButton.interactable ? $"잠금 해제 · Unlock  ${d.unlockCost:N0}" : why) : "개발 중 · In development";

            lockedDistrict = unlocked ? Destinations.Districts(d).FirstOrDefault(r => DestinationManager.IsDistrictBuilt(r) && !Regions.IsRegionUnlocked(r.regionId)) : null;
            districtButton.gameObject.SetActive(lockedDistrict != null);
            if (lockedDistrict != null)
            {
                districtButton.interactable = Destinations.CanUnlockDistrict(lockedDistrict, out string districtWhy);
                districtLabel.text = districtButton.interactable ? $"{lockedDistrict.regionName} 열기 · Unlock  ${lockedDistrict.unlockCost:N0}" : districtWhy;
            }

            enterButton.gameObject.SetActive(unlocked && built);
            enterLabel.text = $"{d.koreanName} 가기 · Enter {d.displayName}";
        }
    }
}
