using System.Collections;
using System.Linq;
using Contract.Contracts;
using Contract.Core;
using Contract.NPCs;
using Contract.Performance;
using Contract.UI;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Contract.Tests
{
    /// <summary>
    /// Phase 10: the Android graphics presets at runtime (forced in the editor: real phone
    /// performance still has to be measured on devices), crowd budget, battery frame cap and
    /// static batching.
    /// </summary>
    public sealed class PhonePresetTests
    {
        private RenderPipelineAsset[] pipelines;
        private float[] lodBiases;

        private static GameSession Session => GameManager.Instance.Session;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            SceneManager.LoadScene(GameConstants.Scenes.MainMenu);
            yield return null;
            yield return null;
            int n = QualitySettings.names.Length, current = QualitySettings.GetQualityLevel();
            pipelines = new RenderPipelineAsset[n];
            lodBiases = new float[n];
            for (int i = 0; i < n; i++)
            {
                QualitySettings.SetQualityLevel(i, false);
                pipelines[i] = QualitySettings.renderPipeline;
                lodBiases[i] = QualitySettings.lodBias;
            }

            QualitySettings.SetQualityLevel(current, true);
            Session.Reset();
            Session.hasStarted = true;
            Session.selectedCharacterId = "char_alex_morgan";
        }

        [TearDown]
        public void TearDown()
        {
            // Quality-setting changes made in the editor's play mode persist: put the PC tiers back.
            PerformanceManager.ForceMobileProfile = null;
            PerformanceManager.RestorePcPipelines(pipelines, lodBiases);
            PerformanceBudget.MaxCivilians = 0;
            GameManager.Instance?.Session.Reset();
            GameManager.Instance?.GetService<ISaveService>()?.DeleteSlot(GameConstants.Save.DefaultSlot);
        }

        private static void Tier(int level)
        {
            Session.Settings.graphicsQuality = level;
            SettingsController.Apply(Session.Settings);
        }

        [UnityTest]
        public IEnumerator EachTier_UsesItsPhonePipeline_LodBiasCrowdAndPostProcessing()
        {
            PerformanceManager.ForceMobileProfile = true;
            MobileQualityProfile profile = MobileQualityProfile.Load();
            SceneManager.LoadScene(GameConstants.Scenes.GameWorld);
            for (int i = 0; i < 6; i++) yield return null;
            for (int level = 0; level < 3; level++)
            {
                Tier(level);
                yield return null;
                MobileQualityProfile.Tier tier = profile.ForLevel(level);
                Assert.AreSame(tier.pipeline, QualitySettings.renderPipeline, tier.name);
                Assert.AreEqual(tier.lodBias, QualitySettings.lodBias, 1e-4f, tier.name);
                Assert.AreEqual(tier.maxCivilians, PerformanceBudget.MaxCivilians, tier.name);
                Assert.AreEqual(tier.postProcessing, Camera.main.GetComponent<UniversalAdditionalCameraData>().renderPostProcessing, tier.name);
            }

            // Lowering the preset mid-game shrinks the crowd to the new budget.
            Tier(2);
            NpcManager.Instance.DespawnAllCivilians();
            NpcManager.Instance.SpawnCivilians();
            int full = NpcManager.Instance.CivilianCount;
            Tier(0);
            NpcManager.Instance.TrimToBudget();
            Assert.LessOrEqual(NpcManager.Instance.CivilianCount, profile.ForLevel(0).maxCivilians);
            Assert.Less(NpcManager.Instance.CivilianCount, full);
            NpcManager.Instance.DespawnAllCivilians();
        }

        [UnityTest]
        public IEnumerator PcTiers_AreUnchanged_WithoutThePhoneProfile()
        {
            PerformanceManager.ForceMobileProfile = false;
            for (int level = 0; level < 3; level++)
            {
                Tier(level);
                Assert.AreSame(pipelines[level], QualitySettings.renderPipeline, "PC pipeline kept");
                Assert.AreEqual(0, PerformanceBudget.MaxCivilians, "no crowd limit on PC");
            }

            yield return null;
        }

        [Test]
        public void Phones_CapMenusAt30Fps_ForBattery()
        {
            var s = new GameSettings { targetFramerate = 60 };
            Assert.AreEqual(30, PerformanceManager.FrameRate(s, true, 120, inGameplay: false));
            Assert.AreEqual(60, PerformanceManager.FrameRate(s, true, 120, inGameplay: true));
            Assert.AreEqual(-1, PerformanceManager.FrameRate(s, false, 60, inGameplay: false), "PC menus unchanged");
        }

        [UnityTest]
        public IEnumerator CityGeometry_IsStaticBatched_OnAndroid()
        {
#if UNITY_ANDROID
            SceneManager.LoadScene(GameConstants.Scenes.Myeongdong);
            for (int i = 0; i < 4; i++) yield return null;
            int batched = Object.FindObjectsByType<MeshRenderer>(FindObjectsSortMode.None).Count(r => r.isPartOfStaticBatch);
            Assert.Greater(batched, 500, "static batching combines the district's static meshes");
            NpcManager.Instance?.DespawnAllCivilians();
#else
            Assert.Ignore("Static batching is enabled for the Android target only.");
            yield break;
#endif
        }
    }
}
