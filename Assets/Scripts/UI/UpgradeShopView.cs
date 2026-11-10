using System.Collections.Generic;
using System.Linq;
using Contract.Core;
using Contract.Data;
using Contract.Progression;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Contract.UI
{
    /// <summary>
    /// Upgrades page of the safehouse terminal: categories, one card per upgrade (level pips and
    /// next price), and a detail pane that states the exact effect now and after the next level.
    /// Outfits are unlockables: buy once, then equip / unequip.
    /// </summary>
    public sealed class UpgradeShopView : MonoBehaviour
    {
        [SerializeField] private RectTransform categoryTabs;
        [SerializeField] private RectTransform cardList;
        [SerializeField] private TMP_Text balanceText;
        [SerializeField] private TMP_Text detailTitle;
        [SerializeField] private TMP_Text detailBody;
        [SerializeField] private TMP_Text detailEffect;
        [SerializeField] private TMP_Text statusText;
        [SerializeField] private Button buyButton;
        [SerializeField] private TMP_Text buyLabel;
        [SerializeField] private Button equipButton;
        [SerializeField] private TMP_Text equipLabel;

        private static readonly (UpgradeCategory category, string label)[] Categories =
        {
            (UpgradeCategory.Movement, "이동 · Movement"),
            (UpgradeCategory.Stealth, "은신 · Stealth"),
            (UpgradeCategory.Intelligence, "지능 · Intelligence"),
            (UpgradeCategory.Utility, "사업 · Business"),
            (UpgradeCategory.Cosmetic, "의상 · Outfits")
        };

        private readonly Dictionary<string, Button> cards = new Dictionary<string, Button>();
        private UpgradeCategory category = UpgradeCategory.Movement;
        private string selectedId;

        public string SelectedId => selectedId;
        public UpgradeCategory Category => category;
        public Button BuyButton => buyButton;
        public Button EquipButton => equipButton;
        public IReadOnlyCollection<string> ListedIds => cards.Keys;

        private static UpgradeManager Upgrades => UpgradeManager.Instance;
        private static GameSession Session => GameManager.Instance?.Session;

        private bool wired;

        private void Awake() => Wire();

        /// <summary>Hooks up the buttons; called by the terminal on creation (the page starts hidden).</summary>
        public void Wire()
        {
            if (wired) return;
            wired = true;
            buyButton.onClick.AddListener(Buy);
            equipButton.onClick.AddListener(ToggleEquip);
        }

        private void OnEnable()
        {
            GameEvents.MoneyChanged += OnMoneyChanged;
            Rebuild();
        }

        private void OnDisable() => GameEvents.MoneyChanged -= OnMoneyChanged;

        private void OnMoneyChanged(int money) => Rebuild();

        public void SelectCategory(UpgradeCategory value)
        {
            category = value;
            selectedId = null;
            Rebuild();
        }

        public void Select(string upgradeId)
        {
            UpgradeData data = Upgrades?.GetUpgrade(upgradeId);
            if (data == null) return;
            if (data.category != category) category = data.category;
            selectedId = upgradeId;
            Rebuild();
        }

        private void Buy()
        {
            if (Upgrades == null || string.IsNullOrEmpty(selectedId)) return;
            if (!Upgrades.PurchaseUpgrade(selectedId)) Upgrades.CanPurchase(selectedId, out string reason);
            Rebuild();
        }

        private void ToggleEquip()
        {
            if (Upgrades == null || string.IsNullOrEmpty(selectedId)) return;
            Upgrades.EquipOutfit(Upgrades.EquippedOutfitId == selectedId ? "" : selectedId);
            Rebuild();
        }

        // ---- building ------------------------------------------------------------------------

        private void Rebuild()
        {
            if (Upgrades == null) return;
            balanceText.text = $"잔액 · Balance  <b>${Session?.money ?? 0:N0}</b>";

            BoardRows.Clear(categoryTabs);
            foreach ((UpgradeCategory value, string label) in Categories)
            {
                UpgradeCategory captured = value;
                BoardRows.Row(categoryTabs, label, value == category, 230f).onClick.AddListener(() => SelectCategory(captured));
            }

            BoardRows.Clear(cardList);
            cards.Clear();
            UpgradeData[] items = Upgrades.GetAvailableUpgrades().Where(u => u.category == category).OrderBy(u => u.cost).ToArray();
            if (selectedId == null || items.All(u => u.upgradeId != selectedId)) selectedId = items.FirstOrDefault()?.upgradeId;
            foreach (UpgradeData upgrade in items)
            {
                string id = upgrade.upgradeId;
                Button card = BoardRows.Row(cardList, CardLabel(upgrade), id == selectedId, 560f, 64f);
                card.onClick.AddListener(() => Select(id));
                cards[id] = card;
            }

            ShowDetail(Upgrades.GetUpgrade(selectedId));
        }

        private string CardLabel(UpgradeData upgrade)
        {
            int level = Upgrades.GetUpgradeLevel(upgrade.upgradeId);
            string right;
            if (upgrade.IsOutfit)
            {
                right = level == 0 ? $"${upgrade.cost:N0}" : Upgrades.EquippedOutfitId == upgrade.upgradeId ? "<color=#4CD38A>착용 중 · Worn</color>" : "<color=#9AA3AE>보유 · Owned</color>";
            }
            else
            {
                right = upgrade.IsMaxLevel(level) ? "<color=#4CD38A>최대 · MAX</color>" : $"${upgrade.GetNextLevelCost(level):N0}";
            }

            return $"<b>{upgrade.upgradeName}</b>  <size=85%><color=#C3C9D2>{upgrade.koreanName}</color></size>\n" +
                   $"<size=80%>{Pips(level, upgrade.MaxLevel)}<pos=62%>{right}</size>";
        }

        private static string Pips(int level, int max) =>
            "<color=#E62938>" + new string('■', level) + "</color><color=#3A3F48>" + new string('■', Mathf.Max(0, max - level)) + "</color>";

        private void ShowDetail(UpgradeData upgrade)
        {
            bool has = upgrade != null;
            detailTitle.text = has ? $"{upgrade.upgradeName}  <size=60%><color=#9AA3AE>{upgrade.koreanName}</color></size>" : "";
            detailBody.text = has ? upgrade.description : "";
            int level = has ? Upgrades.GetUpgradeLevel(upgrade.upgradeId) : 0;

            if (!has)
            {
                detailEffect.text = statusText.text = "";
                buyButton.gameObject.SetActive(false);
                equipButton.gameObject.SetActive(false);
                return;
            }

            if (upgrade.IsOutfit)
            {
                detailEffect.text = "외형 전용 · Cosmetic only — changes how your operative looks in the city and in operative selection.";
            }
            else
            {
                string now = Effect(upgrade, level);
                detailEffect.text = upgrade.IsMaxLevel(level)
                    ? $"레벨 · Level {level}/{upgrade.MaxLevel}\n현재 · Now  <color=#4CD38A>{now}</color>"
                    : $"레벨 · Level {level}/{upgrade.MaxLevel}\n현재 · Now  {(level == 0 ? "<color=#7A808A>—</color>" : now)}\n다음 · Next  <color=#4CD38A>{Effect(upgrade, level + 1)}</color>";
            }

            bool canBuy = Upgrades.CanPurchase(upgrade.upgradeId, out string reason);
            bool owned = level > 0;
            buyButton.gameObject.SetActive(!(upgrade.IsOutfit && owned) && !upgrade.IsMaxLevel(level));
            buyButton.interactable = canBuy;
            buyLabel.text = level == 0 ? $"구매 · Buy  ${upgrade.GetNextLevelCost(level):N0}" : $"레벨 업 · Upgrade  ${upgrade.GetNextLevelCost(level):N0}";
            equipButton.gameObject.SetActive(upgrade.IsOutfit && owned);
            equipLabel.text = Upgrades.EquippedOutfitId == upgrade.upgradeId ? "벗기 · Take off" : "착용 · Wear";
            statusText.text = canBuy || upgrade.IsMaxLevel(level) ? "" : reason;
        }

        /// <summary>The effect at a given level, in the words of the system that applies it.</summary>
        public static string Effect(UpgradeData upgrade, int level)
        {
            float pct = upgrade.BonusAt(level) * 100f;
            switch (upgrade.effect)
            {
                case UpgradeEffect.MoveSpeed: return $"+{pct:0}% walk speed";
                case UpgradeEffect.SprintSpeed: return $"+{pct:0}% sprint speed";
                case UpgradeEffect.CrouchSpeed: return $"+{pct:0}% crouch speed";
                case UpgradeEffect.JumpHeight: return $"+{pct:0}% jump height";
                case UpgradeEffect.DetectionResistance: return $"guards notice you {pct:0}% slower";
                case UpgradeEffect.CrouchStealth: return $"crouched: guards notice you {pct:0}% slower";
                case UpgradeEffect.SuspicionCooldown: return $"guards lose suspicion {pct:0}% faster";
                case UpgradeEffect.HackSpeed: return $"+{pct:0}% hold-interaction speed";
                case UpgradeEffect.InteractionRange: return $"+{pct:0}% interaction reach";
                case UpgradeEffect.MissionReward: return $"+{pct:0}% of base contract reward";
                default: return "";
            }
        }
    }
}
