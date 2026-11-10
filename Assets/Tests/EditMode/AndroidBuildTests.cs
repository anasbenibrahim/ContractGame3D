using System.Linq;
using Contract.Editor;
using Contract.Performance;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.Build;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Contract.Tests.EditMode
{
    /// <summary>Phase 10: Android build configuration, phone graphics presets and LOD culling (build validation, not device testing).</summary>
    public sealed class AndroidBuildTests
    {
        [Test]
        public void PlayerSettings_TargetModernArm64PhonesInLandscape()
        {
            Assert.AreEqual(ScriptingImplementation.IL2CPP, PlayerSettings.GetScriptingBackend(NamedBuildTarget.Android));
            Assert.AreEqual(AndroidArchitecture.ARM64, PlayerSettings.Android.targetArchitectures);
            Assert.AreEqual(AndroidSdkVersions.AndroidApiLevel26, PlayerSettings.Android.minSdkVersion, "Android 8.0+: step counter + runtime permissions");
            Assert.AreEqual(AndroidSdkVersions.AndroidApiLevelAuto, PlayerSettings.Android.targetSdkVersion, "target the newest installed SDK (Play requirement)");
            CollectionAssert.AreEqual(new[] { GraphicsDeviceType.Vulkan, GraphicsDeviceType.OpenGLES3 }, PlayerSettings.GetGraphicsAPIs(BuildTarget.Android), "Vulkan with GLES3 fallback");
            Assert.AreEqual(MobileTextureSubtarget.ASTC, EditorUserBuildSettings.androidBuildSubtarget);
            Assert.IsTrue(PlayerSettings.GetStaticBatchingForPlatform(BuildTarget.Android), "static batching for the box-built city");
            Assert.IsTrue(PlayerSettings.Android.optimizedFramePacing);
            Assert.IsFalse(PlayerSettings.allowedAutorotateToPortrait);
            Assert.IsFalse(PlayerSettings.allowedAutorotateToPortraitUpsideDown);
            Assert.IsTrue(PlayerSettings.allowedAutorotateToLandscapeLeft && PlayerSettings.allowedAutorotateToLandscapeRight);
        }

        [Test]
        public void PhonePresets_GetCheaperFromHighToLow_AndLeaveThePcTiersAlone()
        {
            MobileQualityProfile profile = AssetDatabase.LoadAssetAtPath<MobileQualityProfile>(AndroidOptimizationSetup.ProfilePath);
            Assert.IsNotNull(profile, "Resources/MobileQualityProfile");
            Assert.AreEqual(3, profile.tiers.Length);
            var urp = profile.tiers.Select(t => (UniversalRenderPipelineAsset)t.pipeline).ToArray();
            Assert.IsTrue(urp.All(a => a != null));
            for (int i = 1; i < 3; i++)
            {
                Assert.LessOrEqual(urp[i - 1].renderScale, urp[i].renderScale, "render scale");
                Assert.LessOrEqual(urp[i - 1].shadowDistance, urp[i].shadowDistance, "shadow distance");
                Assert.LessOrEqual(urp[i - 1].mainLightShadowmapResolution, urp[i].mainLightShadowmapResolution, "shadow map");
                Assert.LessOrEqual(profile.tiers[i - 1].lodBias, profile.tiers[i].lodBias, "LOD bias");
                Assert.LessOrEqual(profile.tiers[i - 1].maxCivilians, profile.tiers[i].maxCivilians, "crowd");
            }

            Assert.IsFalse(urp[0].supportsHDR, "Low: no HDR");
            Assert.IsFalse(profile.tiers[0].postProcessing, "Low: no post-processing");
            Assert.IsTrue(urp.All(a => !a.supportsAdditionalLightShadows && !a.supportsCameraOpaqueTexture && !a.supportsCameraDepthTexture));

            // PC quality levels keep their own pipelines.
            var so = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/QualitySettings.asset")[0]);
            SerializedProperty levels = so.FindProperty("m_QualitySettings");
            string[] pc = Enumerable.Range(0, levels.arraySize).Select(i => levels.GetArrayElementAtIndex(i).FindPropertyRelative("customRenderPipeline").objectReferenceValue?.name).ToArray();
            CollectionAssert.AreEqual(new[] { "Mobile_RPAsset", "PC_Medium_RPAsset", "PC_RPAsset" }, pc);
        }

        [Test]
        public void DistrictPrefabs_CullSmallPropsAndSignText()
        {
            foreach (string path in new[] { "Assets/Prefabs/Environment/District_Gangnam.prefab", "Assets/Prefabs/Environment/District_Myeongdong.prefab" })
            {
                GameObject district = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                LODGroup[] groups = district.GetComponentsInChildren<LODGroup>(true);
                Assert.Greater(groups.Length, 100, path);
                foreach (LODGroup g in groups)
                {
                    LOD[] lods = g.GetLODs();
                    Assert.AreEqual(1, lods.Length, g.name);
                    Assert.Greater(lods[0].renderers.Length, 0, g.name);
                    Assert.Greater(lods[0].screenRelativeTransitionHeight, 0f, g.name);
                }

                foreach (Renderer r in district.GetComponentsInChildren<Renderer>(true))
                    Assert.LessOrEqual(groups.Count(g => g.GetLODs()[0].renderers.Contains(r)), 1, $"{r.name} is in more than one LOD group");

                var text = district.GetComponentsInChildren<TMPro.TextMeshPro>(true);
                int culled = text.Count(t => t.GetComponentInParent<LODGroup>(true) != null);
                Assert.AreEqual(text.Length, culled, $"{path}: every sign's text is distance-culled");
            }
        }

        [Test]
        public void PostProcessingResources_AreKeptInBuilds()
        {
            var stripping = GraphicsSettings.GetRenderPipelineSettings<URPShaderStrippingSetting>();
            Assert.IsFalse(stripping.stripUnusedPostProcessingVariants,
                "stripping unused post-processing resources removes the depth-of-field shader and URP then disables all post-processing in the player");
        }

        [Test]
        public void MobileTierRecommendation_FromGpuAndMemory()
        {
            Assert.AreEqual(PerformanceManager.High, PerformanceManager.RecommendMobile("Adreno (TM) 740", 12000));
            Assert.AreEqual(PerformanceManager.Medium, PerformanceManager.RecommendMobile("Adreno (TM) 740", 6000), "flagship GPU, 6 GB");
            Assert.AreEqual(PerformanceManager.High, PerformanceManager.RecommendMobile("Mali-G715 Immortalis MC11", 8000));
            Assert.AreEqual(PerformanceManager.Medium, PerformanceManager.RecommendMobile("Adreno (TM) 619", 6000));
            Assert.AreEqual(PerformanceManager.Medium, PerformanceManager.RecommendMobile("Mali-G68 MC4", 4000));
            Assert.AreEqual(PerformanceManager.Low, PerformanceManager.RecommendMobile("Mali-G52 MC2", 4000));
            Assert.AreEqual(PerformanceManager.Low, PerformanceManager.RecommendMobile("PowerVR Rogue GE8320", 3000));
            Assert.AreEqual(PerformanceManager.Low, PerformanceManager.RecommendMobile("Adreno (TM) 750", 3000), "under 4 GB RAM");
        }
    }
}
