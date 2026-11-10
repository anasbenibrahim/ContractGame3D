using UnityEngine;

namespace Contract.Data
{
    /// <summary>
    /// One purchasable upgrade. Every upgrade has exactly one typed <see cref="UpgradeEffect"/>
    /// that a game system reads through PlayerUpgrades (Core), so the shop text, the maths and
    /// the tests all describe the same thing. Outfits are cosmetic unlockables (equip in the shop).
    /// </summary>
    [CreateAssetMenu(menuName = "CONTRACT/Upgrade Data", fileName = "UpgradeData")]
    public sealed class UpgradeData : ScriptableObject
    {
        [Header("Identity")]
        [field: SerializeField] public string upgradeId { get; set; }
        [field: SerializeField] public string upgradeName { get; set; }
        [field: SerializeField] public string koreanName { get; set; }
        [field: SerializeField] public string description { get; set; }

        [Header("Category")]
        [field: SerializeField] public UpgradeCategory category { get; set; }

        [Header("Cost")]
        [Tooltip("Price of level 1; each further level costs 1.5x the previous one.")]
        [field: SerializeField] public int cost { get; set; }
        [SerializeField] private int maxLevel = 1;
        public int MaxLevel { get => maxLevel; set => maxLevel = value; }

        [Header("Effect")]
        [field: SerializeField] public UpgradeEffect effect { get; set; }
        [Tooltip("Percent per level (e.g. 8 = +8% move speed per level). Unused for outfits.")]
        [field: SerializeField] public float valuePerLevel { get; set; }

        [Header("Outfit (effect = Outfit)")]
        [field: SerializeField] public OutfitStyle outfitStyle { get; set; }
        [field: SerializeField] public Color outfitTopColor { get; set; } = Color.gray;
        [field: SerializeField] public Color outfitBottomColor { get; set; } = Color.black;
        [field: SerializeField] public Color outfitAccentColor { get; set; } = Color.red;
        [field: SerializeField] public bool outfitFaceMask { get; set; }

        [Header("Visual")]
        [field: SerializeField] public Sprite icon { get; set; }

        public bool IsOutfit => effect == UpgradeEffect.Outfit;
        public bool IsMaxLevel(int currentLevel) => currentLevel >= MaxLevel;

        /// <summary>Price of the next level (level 0 → 1 costs <see cref="cost"/>), rounded to $500.</summary>
        public int GetNextLevelCost(int currentLevel) => Mathf.RoundToInt(cost * Mathf.Pow(1.5f, currentLevel) / 500f) * 500;

        /// <summary>Total bonus at a level, as a fraction (0.16 = +16%).</summary>
        public float BonusAt(int level) => valuePerLevel * Mathf.Clamp(level, 0, MaxLevel) / 100f;
    }

    public enum UpgradeCategory
    {
        Movement,
        Stealth,
        Intelligence,
        Utility,
        Cosmetic
    }

    /// <summary>What an upgrade changes. Values are percentages per level.</summary>
    public enum UpgradeEffect
    {
        None,
        MoveSpeed,            // walk speed (ThirdPersonController)
        SprintSpeed,          // sprint speed (ThirdPersonController)
        CrouchSpeed,          // crouch speed (ThirdPersonController)
        JumpHeight,           // jump height (ThirdPersonController)
        DetectionResistance,  // guards gain suspicion slower (NpcController)
        CrouchStealth,        // extra resistance while crouched (NpcController)
        SuspicionCooldown,    // guards lose suspicion faster (NpcController)
        HackSpeed,            // hold interactions finish faster (InteractionController)
        InteractionRange,     // reach (InteractionController)
        MissionReward,        // extra contract payout (RewardCalculator)
        Outfit                // cosmetic, equipped from the shop (PlayerCharacterApplier)
    }
}
