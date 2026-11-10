using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace Contract.Editor
{
    /// <summary>
    /// Stage 10: three PC quality tiers, measured with the built-player benchmark
    /// (-contract-benchmark -contract-quality N):
    ///   Low    - Mobile_RPAsset: 0.8 render scale, 1 shadow cascade, hard shadows, no SSAO.
    ///   Medium - PC_Medium_RPAsset: full resolution, 2 cascades at 1024, low soft shadows, no SSAO,
    ///            no additional-light shadows.
    ///   High   - PC_RPAsset: 4 cascades at 2048, soft shadows, SSAO.
    /// None of the tiers builds the camera opaque texture (nothing samples it).
    /// Idempotent. Headless: -executeMethod Contract.Editor.QualityTierSetup.Apply
    /// </summary>
    public static class QualityTierSetup
    {
        private const string Low = "Assets/Settings/Mobile_RPAsset.asset";
        private const string Medium = "Assets/Settings/PC_Medium_RPAsset.asset";
        private const string High = "Assets/Settings/PC_RPAsset.asset";
        private const string LeanRenderer = "Assets/Settings/Mobile_Renderer.asset";

        [MenuItem("CONTRACT/Setup/Apply Quality Tiers")]
        public static void Apply()
        {
            if (AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(Medium) == null) AssetDatabase.CopyAsset(High, Medium);
            AssetDatabase.Refresh();

            var low = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(Low);
            var medium = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(Medium);
            var high = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(High);

            Configure(high, opaque: false);
            Configure(low, opaque: false);
            Configure(medium, opaque: false, cascades: 2, shadowmap: 1024, softQuality: 1, additionalShadows: false,
                renderer: AssetDatabase.LoadAssetAtPath<ScriptableRendererData>(LeanRenderer));

            ConfigureLevels(low, medium, high);
            AssetDatabase.SaveAssets();
            Debug.Log("[QualityTierSetup] Low / Medium / High configured.");
        }

        private static void Configure(UniversalRenderPipelineAsset asset, bool opaque, int cascades = -1, int shadowmap = -1, int softQuality = -1,
            bool? additionalShadows = null, ScriptableRendererData renderer = null)
        {
            var so = new SerializedObject(asset);
            so.FindProperty("m_RequireOpaqueTexture").boolValue = opaque;
            if (cascades > 0) so.FindProperty("m_ShadowCascadeCount").intValue = cascades;
            if (shadowmap > 0) so.FindProperty("m_MainLightShadowmapResolution").intValue = shadowmap;
            if (softQuality >= 0) so.FindProperty("m_SoftShadowQuality").intValue = softQuality;
            if (additionalShadows.HasValue) so.FindProperty("m_AdditionalLightShadowsSupported").boolValue = additionalShadows.Value;
            if (renderer != null)
            {
                SerializedProperty list = so.FindProperty("m_RendererDataList");
                list.arraySize = 1;
                list.GetArrayElementAtIndex(0).objectReferenceValue = renderer;
                so.FindProperty("m_DefaultRendererIndex").intValue = 0;
            }

            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(asset);
        }

        private static void ConfigureLevels(Object low, Object medium, Object high)
        {
            Object settings = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/QualitySettings.asset")[0];
            var so = new SerializedObject(settings);
            SerializedProperty levels = so.FindProperty("m_QualitySettings");

            // Index 0 = old "Mobile", index 1 = "PC". Insert Medium as a copy of PC, between them.
            int pcIndex = -1;
            for (int i = 0; i < levels.arraySize; i++)
            {
                string name = levels.GetArrayElementAtIndex(i).FindPropertyRelative("name").stringValue;
                if (name == "PC" || name == "High") pcIndex = i;
            }

            if (levels.arraySize == 2)
            {
                levels.InsertArrayElementAtIndex(pcIndex); // duplicates PC at pcIndex, PC moves to pcIndex+1
            }

            void Level(int index, string name, Object pipeline)
            {
                SerializedProperty level = levels.GetArrayElementAtIndex(index);
                level.FindPropertyRelative("name").stringValue = name;
                level.FindPropertyRelative("customRenderPipeline").objectReferenceValue = pipeline;
                level.FindPropertyRelative("vSyncCount").intValue = 1;
                level.FindPropertyRelative("excludedTargetPlatforms").arraySize = 0; // PC game: every tier is available on PC
            }

            Level(0, "Low", low);
            Level(1, "Medium", medium);
            Level(2, "High", high);

            SerializedProperty defaults = so.FindProperty("m_PerPlatformDefaultQuality");
            for (int i = 0; i < defaults.arraySize; i++)
            {
                SerializedProperty entry = defaults.GetArrayElementAtIndex(i);
                if (entry.FindPropertyRelative("first").stringValue == "Standalone") entry.FindPropertyRelative("second").intValue = 2;
            }

            so.FindProperty("m_CurrentQuality").intValue = 2;
            so.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
