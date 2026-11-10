using UnityEngine;

namespace Contract.Data
{
    /// <summary>
    /// A destination in South Korea (Seoul, Busan, Jeju ...) grouping playable districts. Shown on
    /// the World Selection screen; it can only be unlocked and entered once at least one of its
    /// districts is built (scene and content present).
    /// </summary>
    [CreateAssetMenu(menuName = "CONTRACT/Destination Data", fileName = "DestinationData")]
    public sealed class DestinationData : ScriptableObject
    {
        [field: SerializeField] public string destinationId { get; set; }
        [field: SerializeField] public string displayName { get; set; }
        [field: SerializeField] public string koreanName { get; set; }
        [field: SerializeField, TextArea] public string description { get; set; }
        [field: SerializeField] public Texture2D preview { get; set; }
        [field: SerializeField] public int unlockCost { get; set; }
        [field: SerializeField] public bool unlockedByDefault { get; set; }
        [field: SerializeField] public RegionData[] districts { get; set; } = new RegionData[0];
        [Tooltip("Districts planned but without game data yet (shown as \"in development\").")]
        [field: SerializeField] public string[] plannedDistricts { get; set; } = new string[0];
        [field: SerializeField] public string[] loadingTips { get; set; } = new string[0];
        [field: SerializeField] public Color themeColor { get; set; } = new Color(0.9f, 0.16f, 0.22f);
    }
}
