using System;
using UnityEngine;
using UnityEngine.Rendering;

namespace Contract.Performance
{
    /// <summary>
    /// Android graphics presets (Resources/MobileQualityProfile.asset), one per quality level:
    /// Low for entry-level phones, Medium for mid-range, High for powerful phones. Each preset has
    /// its own URP asset (render scale, HDR, MSAA, shadows, additional lights), a LOD bias, a crowd
    /// size and whether post-processing runs. PCs keep their own tiers. These are starting points
    /// to be confirmed on real phones, not frame-rate promises.
    /// </summary>
    [CreateAssetMenu(menuName = "CONTRACT/Mobile Quality Profile", fileName = "MobileQualityProfile")]
    public sealed class MobileQualityProfile : ScriptableObject
    {
        [Serializable]
        public sealed class Tier
        {
            public string name;
            public RenderPipelineAsset pipeline;
            [Tooltip("Multiplies LOD switch / cull distances (lower = props disappear sooner).")]
            public float lodBias = 1f;
            [Tooltip("Civilians walking the district at once (scene maximum still applies).")]
            public int maxCivilians = 24;
            public bool postProcessing = true;
        }

        public Tier[] tiers = new Tier[0];

        public Tier ForLevel(int level) => tiers.Length == 0 ? null : tiers[Mathf.Clamp(level, 0, tiers.Length - 1)];

        public static MobileQualityProfile Load() => Resources.Load<MobileQualityProfile>("MobileQualityProfile");
    }
}
