using System.IO;
using System.Linq;
using Contract.Activity;
using Contract.Data;
using UnityEditor;
using UnityEngine;

namespace Contract.Editor
{
    /// <summary>
    /// GPS Activity Mode content: the activity config, the reward table, the six GPS missions of
    /// the design brief (activity missions in the normal mission system) and the collectibles
    /// that walking reveals. Re-running updates the assets in place (ids are kept, so saves stay valid).
    /// </summary>
    public static class ActivityAssetsBuilder
    {
        private const string ConfigPath = "Assets/Resources/ActivityConfig.asset";
        private const string RewardsPath = "Assets/Resources/ActivityRewards.asset";
        private const string MissionFolder = "Assets/ScriptableObjects/Missions/Activity";
        private const string CollectibleFolder = "Assets/ScriptableObjects/Items/Collectibles";

        [MenuItem("CONTRACT/Setup/Build Activity Config")]
        public static void Build()
        {
            if (AssetDatabase.LoadAssetAtPath<ActivityConfig>(ConfigPath) == null)
            {
                AssetDatabase.CreateAsset(ScriptableObject.CreateInstance<ActivityConfig>(), ConfigPath);
            }

            ActivityRewardsConfig rewards = AssetDatabase.LoadAssetAtPath<ActivityRewardsConfig>(RewardsPath);
            if (rewards == null)
            {
                rewards = ScriptableObject.CreateInstance<ActivityRewardsConfig>();
                AssetDatabase.CreateAsset(rewards, RewardsPath);
            }

            ActivityMilestone first = rewards.milestones.FirstOrDefault(m => m.id == "walk_100m");
            if (first != null) first.unlockMissionId = "gps_02_explorer";

            rewards.collectibles = new[]
            {
                Collectible("col_yeopjeon", "엽전 · Yeopjeon coin", "A brass coin with a square hole, the everyday money of the Joseon era."),
                Collectible("col_norigae", "노리개 · Norigae charm", "A silk-tasselled charm once worn on a hanbok jacket ribbon."),
                Collectible("col_dancheong", "단청 · Dancheong tile", "A painted roof-beam fragment in the five traditional colours."),
                Collectible("col_hahoe", "하회탈 · Hahoe mask", "A carved wooden mask from the Hahoe village mask dance."),
                Collectible("col_taegeukseon", "태극선 · Taegeuk fan", "A round paper fan painted with the red-and-blue taegeuk swirl."),
                Collectible("col_celadon", "청자 · Celadon shard", "A sea-green fragment of Goryeo-era celadon pottery."),
                Collectible("col_najeon", "나전칠기 · Mother-of-pearl box", "A small lacquer box inlaid with shimmering shell."),
                Collectible("col_jangseung", "장승 · Jangseung figure", "A tiny carved village guardian, grinning at travellers.")
            }.ToList();
            EditorUtility.SetDirty(rewards);

            Mission("gps_01_first_steps", "First Steps", "첫 걸음", "Take your first walk with GPS Activity Mode: 100 metres anywhere safe.",
                ObjectiveType.WalkDistance, 100, xp: 100, unlocks: new[] { "gps_02_explorer" }, byDefault: true);
            Mission("gps_02_explorer", "Explorer", "탐험가", "Walk 500 metres. Something interesting turns up along the way.",
                ObjectiveType.WalkDistance, 500, xp: 150, discoversCollectible: true, unlocks: new[] { "gps_03_long_journey" });
            Mission("gps_03_long_journey", "Long Journey", "긴 여정", "Walk a full kilometre. It opens up a new part of Seoul.",
                ObjectiveType.WalkDistance, 1000, xp: 250, unlockRegion: "region_industrial");
            Mission("gps_04_step_challenge", "Step Challenge", "걸음 도전", "Take 2,000 steps (needs the phone's step counter).",
                ObjectiveType.TakeSteps, 2000, xp: 200, money: 3000, byDefault: true);
            Mission("gps_05_checkpoint", "Checkpoint Discovery", "체크포인트 발견",
                "Reach a GPS checkpoint. Mark a safe, public spot in the activity screen, walk on, then come back to it.",
                ObjectiveType.ReachCheckpoint, 1, xp: 150, byDefault: true);
            Mission("gps_06_new_discovery", "New Discovery", "새로운 발견", "Explore 3 areas you have never walked in before (about 100 m each).",
                ObjectiveType.ExploreAreas, 3, xp: 150, discoversCollectible: true, byDefault: true);

            AssetDatabase.SaveAssets();
            GameDatabaseBuilder.Build(); // activity missions join the mission database
            Debug.Log("[ActivityAssetsBuilder] Config, rewards, 6 GPS missions and 8 collectibles ready.");
        }

        private static ItemData Collectible(string id, string name, string description)
        {
            Directory.CreateDirectory(CollectibleFolder);
            string path = $"{CollectibleFolder}/{id}.asset";
            ItemData item = AssetDatabase.LoadAssetAtPath<ItemData>(path);
            if (item == null)
            {
                item = ScriptableObject.CreateInstance<ItemData>();
                AssetDatabase.CreateAsset(item, path);
            }

            item.itemId = id;
            item.displayName = name;
            item.description = description;
            item.type = ItemType.Collectible;
            item.MaxStack = 1;
            EditorUtility.SetDirty(item);
            return item;
        }

        private static void Mission(string id, string name, string korean, string description, ObjectiveType type, float target,
            int xp = 0, int money = 0, string[] unlocks = null, string unlockRegion = null, bool byDefault = false, bool discoversCollectible = false)
        {
            Directory.CreateDirectory(MissionFolder);
            string path = $"{MissionFolder}/{id}.asset";
            MissionData mission = AssetDatabase.LoadAssetAtPath<MissionData>(path);
            if (mission == null)
            {
                mission = ScriptableObject.CreateInstance<MissionData>();
                AssetDatabase.CreateAsset(mission, path);
            }

            mission.missionId = id;
            mission.missionName = name;
            mission.koreanName = korean;
            mission.description = description;
            mission.category = MissionCategory.Activity;
            mission.regionId = string.Empty;
            mission.requiresRegionUnlocked = false;
            mission.unlockedByDefault = byDefault;
            mission.rewardXP = xp;
            mission.rewardMoney = money;
            mission.unlocksMissionIds = unlocks ?? new string[0];
            mission.unlockRegionId = unlockRegion ?? string.Empty;
            mission.rewardItemIds = discoversCollectible ? new[] { "collectible" } : new string[0];
            mission.difficulty = 1;
            mission.objectives = new[]
            {
                new MissionObjective { objectiveId = "main", description = description, type = type, targetValue = target }
            };
            EditorUtility.SetDirty(mission);
        }
    }
}
