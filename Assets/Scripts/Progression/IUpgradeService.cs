using System.Collections.Generic;
using Contract.Data;

namespace Contract.Progression
{
    public interface IUpgradeService
    {
        bool PurchaseUpgrade(string upgradeId);
        bool CanPurchase(string upgradeId, out string reason);
        bool HasUpgrade(string upgradeId);
        int GetUpgradeLevel(string upgradeId);
        int GetNextCost(string upgradeId);
        IEnumerable<UpgradeData> GetAvailableUpgrades();
        UpgradeData GetUpgrade(string upgradeId);
        bool EquipOutfit(string upgradeId);
        string EquippedOutfitId { get; }
    }
}
