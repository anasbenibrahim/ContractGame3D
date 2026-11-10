using System.Collections.Generic;
using System.Linq;
using Contract.Contracts;
using Contract.Core;
using Contract.Data;
using UnityEngine;

namespace Contract.Progression
{
    /// <summary>
    /// Upgrade shop logic: prices, purchases (paid through the economy and recorded in the ledger),
    /// levels and outfit equipping. Ownership lives in the session (so it is saved); what upgrades
    /// do is read by each game system through <see cref="PlayerUpgrades"/>.
    /// </summary>
    public sealed class UpgradeManager : MonoBehaviour, IUpgradeService
    {
        public static UpgradeManager Instance { get; private set; }

        [SerializeField] private UpgradeData[] allUpgrades;

        private readonly Dictionary<string, UpgradeData> upgradesById = new Dictionary<string, UpgradeData>();

        private static GameSession Session => GameManager.Instance?.Session;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void CreateInstance()
        {
            if (Instance != null) return;
            GameObject go = new GameObject("[UpgradeManager]");
            go.AddComponent<UpgradeManager>();
        }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                DestroyImmediate(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);
            GameManager.Instance?.RegisterService<IUpgradeService>(this);

            if (allUpgrades == null || allUpgrades.Length == 0) allUpgrades = GameDatabase.Load()?.Upgrades ?? new UpgradeData[0];
            foreach (UpgradeData data in allUpgrades.Where(u => u != null && !string.IsNullOrEmpty(u.upgradeId)))
            {
                upgradesById[data.upgradeId] = data;
            }
        }

        public IEnumerable<UpgradeData> GetAvailableUpgrades() => allUpgrades.Where(u => u != null);

        public UpgradeData GetUpgrade(string upgradeId) =>
            upgradeId != null && upgradesById.TryGetValue(upgradeId, out UpgradeData data) ? data : null;

        public int GetUpgradeLevel(string upgradeId) => Session?.GetUpgradeLevel(upgradeId) ?? 0;

        public bool HasUpgrade(string upgradeId) => GetUpgradeLevel(upgradeId) > 0;

        public int GetNextCost(string upgradeId)
        {
            UpgradeData data = GetUpgrade(upgradeId);
            return data == null ? 0 : data.GetNextLevelCost(GetUpgradeLevel(upgradeId));
        }

        public bool CanPurchase(string upgradeId, out string reason)
        {
            UpgradeData data = GetUpgrade(upgradeId);
            reason = null;
            if (data == null || Session == null)
            {
                reason = "알 수 없는 업그레이드 · Unknown upgrade";
                return false;
            }

            if (data.IsMaxLevel(GetUpgradeLevel(upgradeId)))
            {
                reason = data.IsOutfit ? "보유 중 · Owned" : "최대 레벨 · Max level";
                return false;
            }

            if (Session.money < GetNextCost(upgradeId))
            {
                reason = "자금 부족 · Not enough money";
                return false;
            }

            return true;
        }

        public bool PurchaseUpgrade(string upgradeId)
        {
            if (!CanPurchase(upgradeId, out _)) return false;
            UpgradeData data = GetUpgrade(upgradeId);
            int level = GetUpgradeLevel(upgradeId);
            int price = data.GetNextLevelCost(level);

            IEconomyService economy = GameManager.Instance.GetService<IEconomyService>();
            string label = data.IsOutfit ? $"의상 · {data.upgradeName}" : $"업그레이드 · {data.upgradeName} Lv{level + 1}";
            bool paid = economy != null ? economy.TrySpend(price, label) : Session.TrySpend(price);
            if (!paid) return false;

            Session.PurchaseUpgrade(upgradeId);
            GameEvents.RaiseUpgradePurchased(upgradeId);
            if (data.IsOutfit) EquipOutfit(upgradeId); // a new outfit is worn straight away
            GameManager.Instance.GetService<ISaveService>()?.AutoSave();
            return true;
        }

        public string EquippedOutfitId => Session?.equippedOutfitId ?? string.Empty;

        /// <summary>Wears an owned outfit; null / "" returns to the operative's own clothes.</summary>
        public bool EquipOutfit(string upgradeId)
        {
            if (Session == null) return false;
            if (!string.IsNullOrEmpty(upgradeId))
            {
                UpgradeData data = GetUpgrade(upgradeId);
                if (data == null || !data.IsOutfit || !HasUpgrade(upgradeId)) return false;
            }

            Session.equippedOutfitId = upgradeId ?? string.Empty;
            GameEvents.RaiseOutfitChanged(Session.equippedOutfitId);
            return true;
        }
    }
}
