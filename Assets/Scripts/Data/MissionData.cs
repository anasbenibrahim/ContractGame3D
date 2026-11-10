using System;
using System.Collections.Generic;
using UnityEngine;

namespace Contract.Data
{
    public enum MissionState
    {
        NotStarted,
        InProgress,
        Completed,
        Failed
    }

    public enum ObjectiveType
    {
        ReachLocation,
        CollectItem,
        Interact,
        IdentifySubject,
        PhotographSubject,
        EscortSubject,
        SecureLocation,
        Extract,
        AvoidDetection,
        // Real-world activity (GPS Activity Mode). targetValue holds metres / steps / areas.
        WalkDistance,
        TakeSteps,
        ReachCheckpoint,     // targetId = checkpoint id, or empty for any checkpoint
        ExploreAreas,
        FindCollectible      // targetId = collectible item id, or empty for any
    }

    /// <summary>Contracts are played in a district; activity missions progress from real walking.</summary>
    public enum MissionCategory
    {
        Contract,
        Activity
    }

    [System.Serializable]
    public sealed class MissionObjective
    {
        [field: SerializeField] public string objectiveId { get; set; }
        [field: SerializeField] public string description { get; set; }
        [field: SerializeField] public ObjectiveType type { get; set; }
        [field: SerializeField] public string targetId { get; set; }
        [field: SerializeField] public string targetLocationId { get; set; }
        [field: SerializeField] public bool isOptional { get; set; }
        [Tooltip("Activity objectives: metres, steps or areas to reach (counted from when the mission becomes available).")]
        [field: SerializeField] public float targetValue { get; set; }
        [Tooltip("Legacy field - progress is tracked per playthrough in the GameSession, never in this asset.")]
        [field: SerializeField] public bool isCompleted { get; private set; }
    }

    [CreateAssetMenu(menuName = "CONTRACT/Mission Data", fileName = "MissionData")]
    public sealed class MissionData : ScriptableObject
    {
        [Header("Identity")]
        [field: SerializeField] public string missionId { get; set; }
        [field: SerializeField] public string missionName { get; set; }
        [field: SerializeField] public string description { get; set; }
        [field: SerializeField] public string regionId { get; set; }
        [field: SerializeField] public string subjectId { get; set; }
        [field: SerializeField] public string subjectName { get; set; }
        [field: SerializeField] public MissionCategory category { get; set; } = MissionCategory.Contract;
        [field: SerializeField] public string koreanName { get; set; }

        [Header("Requirements")]
        [field: SerializeField] public string[] requiredItemIds { get; set; }
        [field: SerializeField] public int minLevel { get; set; }
        [field: SerializeField] public bool requiresRegionUnlocked { get; set; } = true;

        [Header("Objectives")]
        [field: SerializeField] public MissionObjective[] objectives { get; set; }

        [Header("Rewards")]
        [field: SerializeField] public int rewardMoney { get; set; }
        [field: SerializeField] public string[] rewardItemIds { get; set; }
        [field: SerializeField] public string[] rewardUpgradeIds { get; set; }
        [field: SerializeField] public int rewardXP { get; set; }
        [field: SerializeField] public string[] unlocksMissionIds { get; set; }
        [field: SerializeField] public string unlockRegionId { get; set; }

        [Header("Activity missions")]
        [Tooltip("Available from the start; otherwise another mission or milestone unlocks it.")]
        [field: SerializeField] public bool unlockedByDefault { get; set; }

        [Header("Difficulty")]
        [field: SerializeField, Range(1, 5)] public int difficulty { get; set; } = 1;
        [field: SerializeField] public int timeLimitMinutes { get; set; }

        [Header("Metadata")]
        [field: SerializeField] public MissionState initialState { get; set; } = MissionState.NotStarted;
        [field: SerializeField] public bool repeatable { get; set; }

        public int GetObjectiveCount() => objectives != null ? objectives.Length : 0;



        public MissionObjective FindObjective(string objectiveId)
        {
            if (objectives == null || string.IsNullOrEmpty(objectiveId)) return null;
            foreach (MissionObjective obj in objectives)
            {
                if (obj.objectiveId == objectiveId) return obj;
            }
            return null;
        }
    }
}