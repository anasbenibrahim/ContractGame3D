using System.Collections;
using System.Linq;
using Contract.Core;
using Contract.Performance;
using Contract.UI;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Contract.Tests
{
    /// <summary>Stage 10: quality tiers, first-launch recommendation and settings application.</summary>
    public sealed class QualityTests
    {
        [TearDown]
        public void TearDown() => GameManager.Instance.Session.Reset();

        [Test]
        public void ThreeTiers_LowMediumHigh_AreAvailableOnPC()
        {
            CollectionAssert.AreEqual(new[] { "Low", "Medium", "High" }, QualitySettings.names);
        }

        [TestCase("Intel(R) UHD Graphics (CML GT2)", 11868, PerformanceManager.Low)]
        [TestCase("Intel(R) Iris(R) Xe Graphics", 8000, PerformanceManager.Medium)]
        [TestCase("Intel(R) Arc(TM) Graphics", 8000, PerformanceManager.Medium)]
        [TestCase("Intel(R) Arc(TM) A770 Graphics", 16384, PerformanceManager.High)]
        [TestCase("AMD Radeon(TM) Graphics", 2048, PerformanceManager.Low)]
        [TestCase("AMD Radeon 780M", 4096, PerformanceManager.Medium)]
        [TestCase("NVIDIA GeForce RTX 3060", 12288, PerformanceManager.High)]
        [TestCase("NVIDIA GeForce GTX 1050", 2048, PerformanceManager.Medium)]
        [TestCase("llvmpipe (LLVM 15.0.7, 256 bits)", 2048, PerformanceManager.Low)]
        public void FirstLaunch_RecommendsATierFromTheGpu(string gpu, int memory, int expected)
        {
            Assert.AreEqual(expected, PerformanceManager.Recommend(gpu, memory));
        }

        [Test]
        public void AutoQuality_IsResolvedOnce_ThenThePlayersChoiceSticks()
        {
            GameSettings settings = GameManager.Instance.Session.Settings;
            settings.graphicsQuality = -1;
            PerformanceManager.Instance.ApplySettings();
            int chosen = settings.graphicsQuality;
            Assert.AreEqual(PerformanceManager.Recommend(SystemInfo.graphicsDeviceName, SystemInfo.graphicsMemorySize), chosen);
            Assert.AreEqual(chosen, QualitySettings.GetQualityLevel());

            settings.graphicsQuality = PerformanceManager.High;
            settings.vSync = false;
            SettingsController.Apply(settings);
            Assert.AreEqual(PerformanceManager.High, QualitySettings.GetQualityLevel());
            Assert.AreEqual(0, QualitySettings.vSyncCount);
            PerformanceManager.Instance.ApplySettings();
            Assert.AreEqual(PerformanceManager.High, settings.graphicsQuality, "the player's choice was overridden");
        }

        [UnityTest]
        public IEnumerator OnlyOneQualityManager_AndItSurvivesSceneLoads()
        {
            SceneManager.LoadScene(GameConstants.Scenes.GameWorld);
            for (int i = 0; i < 4; i++) yield return null;
            Assert.AreEqual(1, Object.FindObjectsByType<PerformanceManager>().Length, "GameWorld's old copy should remove itself");
            SceneManager.LoadScene(GameConstants.Scenes.MainMenu);
            for (int i = 0; i < 3; i++) yield return null;
            Assert.IsNotNull(PerformanceManager.Instance);
        }
    }
}
