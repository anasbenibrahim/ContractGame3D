using UnityEngine;

namespace Contract.Activity
{
    /// <summary>
    /// GPS Activity Mode tuning (Resources/ActivityConfig.asset). The accuracy threshold and the
    /// movement ratio can also be changed by the player in Settings; those values win.
    /// </summary>
    [CreateAssetMenu(menuName = "CONTRACT/Activity Config", fileName = "ActivityConfig")]
    public sealed class ActivityConfig : ScriptableObject
    {
        [Tooltip("Distance filter (drift, outliers, vehicles). Defaults tuned on synthetic GPS traces.")]
        public WalkingFilterSettings filter = WalkingFilterSettings.Default;

        [Tooltip("A gap longer than this between fixes breaks the track: distance across it is not counted (s).")]
        public float maxContinuityGap = 90f;

        [Tooltip("Window for the displayed walking speed (s).")]
        public float speedWindow = 20f;

        [Header("Virtual route")]
        [Tooltip("Gap (m) between the character and its target beyond which it jogs to catch up.")]
        public float catchUpDistance = 12f;

        private static ActivityConfig loaded;

        public static ActivityConfig Load()
        {
            if (loaded == null) loaded = Resources.Load<ActivityConfig>("ActivityConfig");
            if (loaded == null) loaded = CreateInstance<ActivityConfig>(); // defaults if the asset is missing
            return loaded;
        }
    }
}
