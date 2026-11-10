using System;
using UnityEngine;

namespace Contract.Data
{
    [CreateAssetMenu(menuName = "CONTRACT/Region Data", fileName = "RegionData")]
    public sealed class RegionData : ScriptableObject
    {
        [Header("Identity")]
        [field: SerializeField] public string regionId { get; set; }
        [field: SerializeField] public string regionName { get; set; }
        [field: SerializeField] public string description { get; set; }
        [field: SerializeField] public string sceneName { get; set; } = "GameWorld";

        [Header("Visual")]
        [field: SerializeField] public Color atmosphereColor { get; set; } = new Color(0.05f, 0.07f, 0.1f);
        [field: SerializeField] public Sprite regionIcon { get; set; }

        [Header("Progression")]
        [field: SerializeField] public bool unlockedByDefault { get; set; }
        [field: SerializeField] public string[] requiredRegionIds { get; set; }
        [field: SerializeField] public int unlockCost { get; set; }
        [Tooltip("Paid once when every mission in the region is completed.")]
        [field: SerializeField] public int completionBonus { get; set; } = 100000;
        [Tooltip("True once the region's playable district exists in the game world. Missions of regions " +
                 "without a built district cannot be started.")]
        [field: SerializeField] public bool districtBuilt { get; set; }

        [Header("Missions")]
        [field: SerializeField] public string[] missionIds { get; set; }
        [field: SerializeField] public string safehouseId { get; set; }
        [field: SerializeField] public string extractionId { get; set; }

        [Header("NPCs")]
        [field: SerializeField] public int civilianNPCCount { get; set; } = 10;
        [field: SerializeField] public int missionNPCCount { get; set; } = 2;

        [Header("Bounds")]
        [field: SerializeField] public Vector3 centerPosition { get; set; } = Vector3.zero;
        [field: SerializeField] public Vector3 regionSize { get; set; } = new Vector3(120f, 20f, 120f);

        public int GetMissionCount() => missionIds != null ? missionIds.Length : 0;

        public bool ContainsMission(string missionId)
        {
            if (missionIds == null || string.IsNullOrEmpty(missionId)) return false;
            foreach (string id in missionIds)
            {
                if (id == missionId) return true;
            }
            return false;
        }
    }
}