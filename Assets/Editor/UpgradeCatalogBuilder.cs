using Contract.Data;
using UnityEditor;
using UnityEngine;

namespace Contract.Editor
{
    /// <summary>
    /// Stage 8: the upgrade catalogue. Every entry has one typed effect that a game system
    /// applies (see UpgradeEffect), and the description says exactly that. Existing asset ids are
    /// kept so older saves still own what they bought; upgrades whose effect was never
    /// implemented are removed. Rebuilds the GameDatabase afterwards.
    /// Headless: -executeMethod Contract.Editor.UpgradeCatalogBuilder.Build
    /// </summary>
    public static class UpgradeCatalogBuilder
    {
        private const string Folder = "Assets/ScriptableObjects/Upgrades";

        private static readonly string[] Obsolete =
        {
            "upgrade_auto_save_1", "upgrade_intel_gathering_1", "upgrade_inventory_slots_1", "upgrade_scan_range_1",
            "upgrade_sprint_speed_2", "upgrade_stamina_1", "upgrade_starting_money_1", "upgrade_stealth_rating_1",
            "upgrade_suspicion_build_1", "upgrade_suspicion_decay_1"
        };

        [MenuItem("CONTRACT/Setup/Build Upgrade Catalogue")]
        public static void Build()
        {
            // Movement
            Stat("upgrade_move_speed_1", "Light Footwork", "가벼운 발놀림", UpgradeCategory.Movement, UpgradeEffect.MoveSpeed, 6f, 3, 8000,
                "Lighter shoes and better posture: you walk faster everywhere.");
            Stat("upgrade_sprint_speed_1", "Enhanced Sprint", "질주 강화", UpgradeCategory.Movement, UpgradeEffect.SprintSpeed, 8f, 3, 6000,
                "Interval training on the Han river paths: a faster sprint when a contract goes loud.");
            Stat("upgrade_crouch_speed_1", "Silent Movement", "조용한 이동", UpgradeCategory.Movement, UpgradeEffect.CrouchSpeed, 25f, 2, 10000,
                "Practised low movement: crouch-walk faster without standing up.");
            Stat("upgrade_jump_height_1", "Vertical Leap", "도약", UpgradeCategory.Movement, UpgradeEffect.JumpHeight, 15f, 2, 7000,
                "Parkour drills: jump higher onto ledges, planters and low walls.");

            // Stealth
            Stat("upgrade_silent_takedown_1", "Quiet Steps", "조용한 발걸음", UpgradeCategory.Stealth, UpgradeEffect.DetectionResistance, 10f, 3, 20000,
                "Soft soles and a calmer stride: guards who can see you in restricted areas become suspicious more slowly.");
            Stat("upgrade_shadow_cloak_1", "Shadow Cloak", "그림자 망토", UpgradeCategory.Stealth, UpgradeEffect.CrouchStealth, 35f, 1, 30000,
                "A dark, low-profile layer: while crouched, guards build suspicion much more slowly.");
            Stat("upgrade_cool_head_1", "Cool Head", "침착함", UpgradeCategory.Stealth, UpgradeEffect.SuspicionCooldown, 30f, 2, 15000,
                "Act like you belong: once you are out of sight, guards lose interest faster.");

            // Intelligence
            Stat("upgrade_hack_speed_1", "Quick Hack", "빠른 해킹", UpgradeCategory.Intelligence, UpgradeEffect.HackSpeed, 15f, 3, 15000,
                "Better tools and scripts: terminals, device planting and other hold actions finish faster.");
            Stat("upgrade_interaction_range_1", "Extended Reach", "손 뻗기", UpgradeCategory.Intelligence, UpgradeEffect.InteractionRange, 15f, 3, 8000,
                "A telescopic grabber and a longer cable: interact with things from further away.");

            // Business
            Stat("upgrade_mission_reward_1", "Negotiator", "협상가", UpgradeCategory.Utility, UpgradeEffect.MissionReward, 5f, 3, 25000,
                "Better rates from your clients: every completed contract pays extra, on top of the usual bonuses.");

            // Outfits (unlockables)
            Outfit("upgrade_outfit_casual_1", "Street Hoodie", "스트리트 후디", 8000, OutfitStyle.Hoodie,
                new Color(0.86f, 0.86f, 0.84f), new Color(0.16f, 0.18f, 0.24f), new Color(0.2f, 0.55f, 0.95f), false,
                "Hongdae streetwear: a pale oversized hoodie and indigo trousers.");
            Outfit("upgrade_outfit_tactical_1", "Night Bomber", "나이트 봄버", 12000, OutfitStyle.BomberJacket,
                new Color(0.07f, 0.07f, 0.08f), new Color(0.05f, 0.05f, 0.06f), new Color(0.9f, 0.12f, 0.18f), true,
                "All-black bomber with a red trim and a face mask, for night work.");
            Outfit("upgrade_outfit_formal_1", "Gala Suit", "정장", 20000, OutfitStyle.BusinessSuit,
                new Color(0.12f, 0.14f, 0.22f), new Color(0.1f, 0.11f, 0.16f), new Color(0.85f, 0.7f, 0.3f), false,
                "A tailored navy suit for hotel lobbies and corporate floors.");

            foreach (string id in Obsolete)
            {
                string path = $"{Folder}/{id}.asset";
                if (AssetDatabase.LoadAssetAtPath<UpgradeData>(path) != null) AssetDatabase.DeleteAsset(path);
            }

            AssetDatabase.SaveAssets();
            GameDatabaseBuilder.Build();
            Debug.Log("[UpgradeCatalogBuilder] Done.");
        }

        private static UpgradeData Asset(string id)
        {
            string path = $"{Folder}/{id}.asset";
            UpgradeData data = AssetDatabase.LoadAssetAtPath<UpgradeData>(path);
            if (data == null)
            {
                data = ScriptableObject.CreateInstance<UpgradeData>();
                AssetDatabase.CreateAsset(data, path);
            }

            data.upgradeId = id;
            return data;
        }

        private static void Stat(string id, string name, string korean, UpgradeCategory category, UpgradeEffect effect, float perLevel, int levels, int cost, string description)
        {
            UpgradeData data = Asset(id);
            data.upgradeName = name;
            data.koreanName = korean;
            data.category = category;
            data.effect = effect;
            data.valuePerLevel = perLevel;
            data.MaxLevel = levels;
            data.cost = cost;
            data.description = description;
            EditorUtility.SetDirty(data);
        }

        private static void Outfit(string id, string name, string korean, int cost, OutfitStyle style, Color top, Color bottom, Color accent, bool mask, string description)
        {
            UpgradeData data = Asset(id);
            data.upgradeName = name;
            data.koreanName = korean;
            data.category = UpgradeCategory.Cosmetic;
            data.effect = UpgradeEffect.Outfit;
            data.valuePerLevel = 0f;
            data.MaxLevel = 1;
            data.cost = cost;
            data.outfitStyle = style;
            data.outfitTopColor = top;
            data.outfitBottomColor = bottom;
            data.outfitAccentColor = accent;
            data.outfitFaceMask = mask;
            data.description = description;
            EditorUtility.SetDirty(data);
        }
    }
}
