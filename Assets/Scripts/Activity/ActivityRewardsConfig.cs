using System;
using System.Collections.Generic;
using UnityEngine;

namespace Contract.Activity
{
    public enum MilestoneMeasure
    {
        RealDistance,   // career metres walked (validated)
        Steps           // career steps (hardware counter)
    }

    /// <summary>One real-world activity goal and what it gives. Thresholds use career totals.</summary>
    [Serializable]
    public sealed class ActivityMilestone
    {
        public string id;
        public string titleKorean;
        public string title;
        public MilestoneMeasure measure;
        [Tooltip("Metres or steps.")] public double threshold = 100;
        [Tooltip("Awarded again every 'threshold' (e.g. every 2,000 steps) instead of once.")] public bool repeat;
        [Header("Rewards")]
        public int money;
        public int xp;
        public string unlockRegionId;
        public string unlockMissionId;
        public string itemId;
    }

    /// <summary>
    /// Activity reward table (Resources/ActivityRewards.asset): what walking and steps unlock.
    /// Defaults follow the design brief; every value can be edited in the Inspector.
    /// </summary>
    [CreateAssetMenu(menuName = "CONTRACT/Activity Rewards", fileName = "ActivityRewards")]
    public sealed class ActivityRewardsConfig : ScriptableObject
    {
        public List<ActivityMilestone> milestones = Defaults();

        public static List<ActivityMilestone> Defaults() => new List<ActivityMilestone>
        {
            new ActivityMilestone { id = "walk_100m", titleKorean = "첫 걸음", title = "First 100 m", measure = MilestoneMeasure.RealDistance, threshold = 100, xp = 50, unlockMissionId = "gps_02_explorer" },
            new ActivityMilestone { id = "walk_500m", titleKorean = "탐험가", title = "500 m walked", measure = MilestoneMeasure.RealDistance, threshold = 500, money = 5000, xp = 100 },
            new ActivityMilestone { id = "walk_1km", titleKorean = "긴 여정", title = "1 km walked", measure = MilestoneMeasure.RealDistance, threshold = 1000, xp = 200, unlockRegionId = "region_industrial" },
            new ActivityMilestone { id = "steps_2000", titleKorean = "걸음 도전", title = "Every 2,000 steps", measure = MilestoneMeasure.Steps, threshold = 2000, repeat = true, xp = 100 }
        };

        [Header("Exploration")]
        [Tooltip("Size of an exploration area (m). Areas are stored only as salted hashes.")]
        public float areaSize = 100f;
        [Tooltip("New areas visited per collectible discovery.")]
        public int areasPerDiscovery = 5;
        [Tooltip("Ignore fixes less accurate than this for exploration and checkpoints (m).")]
        public float placeAccuracy = 30f;

        [Header("Collectibles (discovered in this order)")]
        public List<Contract.Data.ItemData> collectibles = new List<Contract.Data.ItemData>();
        public int collectibleXp = 40;
        public int collectibleMoney = 1000;

        [Header("Checkpoints")]
        [Tooltip("Designer checkpoints at real, public, safe places (none by default: players mark their own).")]
        public List<GpsCheckpointDefinition> checkpoints = new List<GpsCheckpointDefinition>();
        public float playerCheckpointRadius = 25f;
        [Tooltip("Metres to walk after marking a spot before returning to it counts.")]
        public float playerCheckpointWalk = 150f;
        [Tooltip("How far away the player must get before coming back counts (m).")]
        public float playerCheckpointAway = 80f;
        public int playerCheckpointMoney = 5000;
        public int playerCheckpointXp = 100;
        public int maxPlayerCheckpoints = 5;

        private static ActivityRewardsConfig loaded;

        public static ActivityRewardsConfig Load()
        {
            if (loaded == null) loaded = Resources.Load<ActivityRewardsConfig>("ActivityRewards");
            if (loaded == null) loaded = CreateInstance<ActivityRewardsConfig>();
            return loaded;
        }
    }

    /// <summary>A designer-placed checkpoint. Use only public, safe, accessible places.</summary>
    [Serializable]
    public sealed class GpsCheckpointDefinition
    {
        public string id;
        public string nameKorean;
        public string name;
        [TextArea] public string description;
        public double latitude;
        public double longitude;
        [Tooltip("Activation radius (m). GPS is not centimetre-accurate: 20 m or more.")] public float radius = 25f;
        [Tooltip("Metres walked in the current session before this checkpoint can trigger.")] public float requiredSessionMeters = 100f;
        public int money = 5000;
        public int xp = 100;
        public string itemId;
    }
}
