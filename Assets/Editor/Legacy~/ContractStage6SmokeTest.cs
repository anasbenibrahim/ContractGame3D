using System.Linq;
using Contract.Contracts;
using Contract.Core;
using Contract.Data;
using Contract.Economy;
using Contract.Items;
using Contract.Progression;
using Contract.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Contract.EditorTools
{
    public static class ContractStage6SmokeTest
    {
        private const double PlayDuration = 2.0;
        private const double StartupTimeout = 20.0;

        private static double startedAt;
        private static double playStartedAt;
        private static bool playStarted;
        private static bool finished;
        private static bool passedSmokeTest;

        [MenuItem("CONTRACT/Validate/Stage 6 Play Mode Smoke Test")]
        public static void Run()
        {
            EditorSceneManager.OpenScene("Assets/Scenes/GameWorld.unity", OpenSceneMode.Single);
            startedAt = EditorApplication.timeSinceStartup;
            EditorApplication.update += Tick;
            EditorApplication.playModeStateChanged += HandlePlayModeChanged;
            EditorApplication.isPlaying = true;
        }

        private static void HandlePlayModeChanged(PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.EnteredPlayMode)
            {
                playStarted = true;
                playStartedAt = EditorApplication.timeSinceStartup;
            }
        }

        private static void Tick()
        {
            if (finished) return;

            double now = EditorApplication.timeSinceStartup;
            if (!playStarted && now - startedAt > StartupTimeout)
            {
                Fail("Play mode did not start within the timeout.");
                return;
            }

            if (!playStarted || now - playStartedAt < PlayDuration) return;
            ValidateRuntime(now);
        }

        private static void ValidateRuntime(double now)
        {
            finished = true;
            EditorApplication.update -= Tick;
            EditorApplication.playModeStateChanged -= HandlePlayModeChanged;

            bool passed = true;
            GameManager gameManager = GameManager.Instance;
            UpgradeManager upgradeManager = UpgradeManager.Instance;
            EconomyManager economyManager = EconomyManager.Instance;
            UpgradeShopController shop = Object.FindAnyObjectByType<UpgradeShopController>();
            ItemLibrary itemLibrary = AssetDatabase.LoadAssetAtPath<ItemLibrary>("Assets/ScriptableObjects/ItemLibrary.asset");

            passed &= Check(gameManager != null, "GameManager was not found.");
            passed &= Check(upgradeManager != null, "UpgradeManager was not found.");
            passed &= Check(economyManager != null, "EconomyManager was not found.");
            passed &= Check(shop != null, "UpgradeShopController was not found.");
            passed &= Check(itemLibrary != null && itemLibrary.items != null && itemLibrary.items.Length >= 15,
                "ItemLibrary is missing or incomplete.");

            if (gameManager != null)
            {
                passed &= Check(gameManager.GetService<IUpgradeService>() != null, "IUpgradeService was not registered.");
                passed &= Check(gameManager.GetService<IEconomyService>() != null, "IEconomyService was not registered.");
            }

            UpgradeData upgrade = upgradeManager?.GetAvailableUpgrades()?.FirstOrDefault();
            if (upgrade == null)
            {
                passed &= Check(false, "No upgrades are available for purchase.");
                Finish(passed, now);
                return;
            }

            GameSession session = gameManager?.Session;
            economyManager.AddMoney(100000);
            int moneyBefore = session.money;
            int levelBefore = upgradeManager.GetUpgradeLevel(upgrade.upgradeId);
            int cost = upgrade.GetNextLevelCost(levelBefore);
            bool purchased = upgradeManager.PurchaseUpgrade(upgrade.upgradeId);

            passed &= Check(purchased, $"Purchase failed for {upgrade.upgradeId}.");
            passed &= Check(upgradeManager.GetUpgradeLevel(upgrade.upgradeId) == levelBefore + 1,
                "Upgrade level did not increase.");
            passed &= Check(session.money == moneyBefore - cost, "Upgrade cost was not deducted.");
            passed &= Check(session.purchasedUpgradeIds.Contains(upgrade.upgradeId),
                "Purchased upgrade was not recorded in the session.");
            passed &= Check(itemLibrary.FindItem("keycard") != null, "ItemLibrary cannot resolve the keycard.");

            shop.OpenShop();
            shop.CloseShop();

            Finish(passed, now);
        }

        private static void Finish(bool passed, double now)
        {
            double elapsed = now - playStartedAt;
            if (passed)
            {
                Debug.Log($"[CONTRACT-STAGE6-SMOKE] PASSED after {elapsed:0.0}s.");
            }
            else
            {
                Debug.LogError("[CONTRACT-STAGE6-SMOKE] FAILED.");
            }

            passedSmokeTest = passed;
            EditorApplication.playModeStateChanged += HandleExitPlayMode;
            EditorApplication.isPlaying = false;
        }

        private static void HandleExitPlayMode(PlayModeStateChange state)
        {
            if (state != PlayModeStateChange.EnteredEditMode) return;

            EditorApplication.playModeStateChanged -= HandleExitPlayMode;
            EditorApplication.Exit(passedSmokeTest ? 0 : 1);
        }

        private static bool Check(bool condition, string failureMessage)
        {
            if (!condition) Debug.LogError($"[CONTRACT-STAGE6-SMOKE] {failureMessage}");
            return condition;
        }

        private static void Fail(string message)
        {
            finished = true;
            EditorApplication.update -= Tick;
            EditorApplication.playModeStateChanged -= HandlePlayModeChanged;
            Debug.LogError($"[CONTRACT-STAGE6-SMOKE] {message}");
            EditorApplication.Exit(1);
        }
    }
}
