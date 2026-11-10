using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using Contract.CameraSystem;
using Contract.Contracts;
using Contract.Core;
using Contract.Player;
using Contract.UI;
using Contract.Audio;
using Contract.SaveSystem;
using Contract.Missions;
using Contract.Regions;
using Contract.Character;
using Contract.Economy;
using Contract.Progression;
using Contract.Map;
using Contract.NPCs;
using Contract.VFX;
using Contract.Accessibility;
using Contract.Performance;
using Contract.Localization;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace Contract.EditorTools
{
    /// <summary>
    /// Comprehensive automated test suite for CONTRACT: SEOUL SHADOWS.
    /// Run: CONTRACT/Validate/Run All Tests
    /// </summary>
    public static class AutomatedTestSuite
    {
        private static int testsPassed = 0;
        private static int testsFailed = 0;
        private static List<string> failures = new List<string>();

        [MenuItem("CONTRACT/Validate/Run All Tests")]
        public static void RunAllTests()
        {
            testsPassed = 0;
            testsFailed = 0;
            failures.Clear();

            EditorSceneManager.OpenScene("Assets/Scenes/MainMenu.unity", OpenSceneMode.Single);
            TestMainMenuScene();

            EditorSceneManager.OpenScene("Assets/Scenes/CharacterSelection.unity", OpenSceneMode.Single);
            TestCharacterSelectionScene();

            EditorSceneManager.OpenScene("Assets/Scenes/MissionSelection.unity", OpenSceneMode.Single);
            TestMissionSelectionScene();

            EditorSceneManager.OpenScene("Assets/Scenes/GameWorld.unity", OpenSceneMode.Single);
            TestGameWorldScene();

            TestCoreSystems();
            TestDataIntegrity();
            TestServiceRegistration();

            PrintResults();
        }

        private static void TestMainMenuScene()
        {
            LogTest("MainMenu Scene - Root Objects", GameObject.Find("MainMenuController") != null);
            LogTest("MainMenu Scene - Play Button", FindButton("PlayButton") != null);
            LogTest("MainMenu Scene - Continue Button", FindButton("ContinueButton") != null);
            LogTest("MainMenu Scene - Character Button", FindButton("CharacterSelectionButton") != null);
            LogTest("MainMenu Scene - Settings Button", FindButton("SettingsButton") != null);
            LogTest("MainMenu Scene - Credits Button", FindButton("CreditsButton") != null);
            LogTest("MainMenu Scene - Quit Button", FindButton("QuitButton") != null);
            LogTest("MainMenu Scene - Cinematic Camera", Object.FindAnyObjectByType<CinematicCameraController>() != null);
        }

        private static void TestCharacterSelectionScene()
        {
            LogTest("CharacterSelection Scene - Controller", Object.FindAnyObjectByType<CharacterSelectionController>() != null);
            LogTest("CharacterSelection Scene - Slots", Object.FindObjectsByType<UnityEngine.UI.Button>(FindObjectsInactive.Include, FindObjectsSortMode.None).Length >= 3);
            LogTest("CharacterSelection Scene - Details Panel", GameObject.Find("DetailsPanel") != null);
            LogTest("CharacterSelection Scene - Select Button", FindButton("SelectButton") != null);
            LogTest("CharacterSelection Scene - Back Button", FindButton("BackButton") != null);
        }

        private static void TestMissionSelectionScene()
        {
            LogTest("MissionSelection Scene - Contract board", Object.FindAnyObjectByType<ContractBoardView>() != null);
            LogTest("MissionSelection Scene - Region Tabs", GameObject.Find("RegionTabContainer") != null);
            LogTest("MissionSelection Scene - Mission List", GameObject.Find("MissionListContainer") != null);
            LogTest("MissionSelection Scene - Details Panel", GameObject.Find("DetailsPanel") != null);
            LogTest("MissionSelection Scene - Start Button", FindButton("StartMissionButton") != null);
            LogTest("MissionSelection Scene - Back Button", FindButton("BackButton") != null);
        }

        private static void TestGameWorldScene()
        {
            // Core
            LogTest("GameWorld - GameManager", GameManager.Instance != null);
            LogTest("GameWorld - SceneLoader", GameManager.Instance?.SceneLoader != null);
            LogTest("GameWorld - Player", GameObject.FindGameObjectWithTag("Player") != null);
            LogTest("GameWorld - Player Controller", GameObject.FindGameObjectWithTag("Player")?.GetComponent<ThirdPersonController>() != null);
            LogTest("GameWorld - Player Animation", GameObject.FindGameObjectWithTag("Player")?.GetComponent<PlayerAnimationDriver>() != null);
            LogTest("GameWorld - Player CameraTarget", GameObject.FindGameObjectWithTag("Player")?.transform.Find("CameraTarget") != null);
            LogTest("GameWorld - Main Camera", Camera.main != null);
            LogTest("GameWorld - Third Person Camera", Camera.main?.GetComponent<ThirdPersonCamera>() != null);

            // Services
            LogTest("GameWorld - AudioService", AudioService.Instance != null);
            LogTest("GameWorld - SaveManager", SaveManager.Instance != null);
            LogTest("GameWorld - MissionManager", MissionManager.Instance != null);
            LogTest("GameWorld - RegionManager", RegionManager.Instance != null);
            LogTest("GameWorld - CharacterManager", CharacterSelectionManager.Instance != null);
            LogTest("GameWorld - EconomyManager", EconomyManager.Instance != null);
            LogTest("GameWorld - UpgradeManager", UpgradeManager.Instance != null);
            LogTest("GameWorld - MapManager", MapManager.Instance != null);
            LogTest("GameWorld - NpcManager", NpcManager.Instance != null);
            LogTest("GameWorld - UIManager", UIManager.Instance != null);
            LogTest("GameWorld - VFXManager", VFXManager.Instance != null);
            LogTest("GameWorld - PostProcessingController", PostProcessingController.Instance != null);
            LogTest("GameWorld - AccessibilityManager", AccessibilityManager.Instance != null);
            LogTest("GameWorld - PerformanceManager", PerformanceManager.Instance != null);
            LogTest("GameWorld - LocalizationManager", LocalizationManager.Instance != null);
            LogTest("GameWorld - UITransitionManager", UITransitionManager.Instance != null);
            LogTest("GameWorld - HUDController", Object.FindAnyObjectByType<HUDController>() != null);
            LogTest("GameWorld - PauseMenuController", Object.FindAnyObjectByType<PauseMenuController>() != null);
            LogTest("GameWorld - InventoryUI", Object.FindAnyObjectByType<InventoryUI>() != null);
            LogTest("GameWorld - FullMapView", Object.FindAnyObjectByType<FullMapView>() != null);

            // Environment
            LogTest("GameWorld - Environment Root", GameObject.Find("Environment") != null);
            LogTest("GameWorld - NavMesh", UnityEngine.AI.NavMesh.CalculateTriangulation().vertices.Length > 0);
            LogTest("GameWorld - Interactables Container", GameObject.Find("Interactables") != null);
            LogTest("GameWorld - WorldMarkers Container", GameObject.Find("WorldMarkers") != null);
            LogTest("GameWorld - PlayerSpawn", GameObject.Find("PlayerSpawn") != null);

            // Minimap
            LogTest("GameWorld - Minimap View", Object.FindAnyObjectByType<MinimapView>() != null);
            LogTest("GameWorld - Map Texture", MapManager.Instance?.MapTexture != null);
            LogTest("GameWorld - Minimap Layer", LayerMask.NameToLayer("Minimap") >= 0);

            // UI
            LogTest("GameWorld - UI Canvas", Object.FindAnyObjectByType<Canvas>() != null);
            LogTest("GameWorld - EventSystem", Object.FindAnyObjectByType<UnityEngine.EventSystems.EventSystem>() != null);
            LogTest("GameWorld - InputSystemUIInputModule", Object.FindAnyObjectByType<UnityEngine.InputSystem.UI.InputSystemUIInputModule>() != null);
        }

        private static void TestCoreSystems()
        {
            LogTest("Core - GameEvents", typeof(GameEvents).GetEvents().Length > 20);
            LogTest("Core - ServiceLocator", new ServiceLocator().Get<object>() == null);
            LogTest("Core - GameSession", new GameSession().completedMissionIds != null);
            LogTest("Core - GameSettings", new GameSettings().masterVolume == 1f);
            LogTest("Core - GameConstants", GameConstants.VictoryMoney == 1000000);
            LogTest("Core - InputReader", typeof(InputReader).GetProperties().Length > 0);
        }

        private static void TestDataIntegrity()
        {
            // Check ScriptableObjects exist
            string[] soPaths = {
                "Assets/ScriptableObjects/Characters/char_alex_morgan.asset",
                "Assets/ScriptableObjects/Characters/char_daniel_cross.asset",
                "Assets/ScriptableObjects/Characters/char_ryan_blake.asset",
                "Assets/ScriptableObjects/Regions/region_downtown.asset",
                "Assets/ScriptableObjects/Regions/region_industrial.asset",
                "Assets/ScriptableObjects/Regions/region_residential.asset",
                "Assets/ScriptableObjects/Regions/region_luxury.asset",
                "Assets/ScriptableObjects/Regions/region_harbor.asset",
                "Assets/ScriptableObjects/Missions/mission_downtown_01.asset",
                "Assets/ScriptableObjects/Upgrades/upgrade_sprint_speed_1.asset",
            };

            foreach (string path in soPaths)
            {
                LogTest($"Data - {Path.GetFileName(path)}", File.Exists(path));
            }
        }

        private static void TestServiceRegistration()
        {
            GameManager gm = GameManager.Instance;
            if (gm != null)
            {
                LogTest("Services - IAudioService", gm.GetService<IAudioService>() != null);
                LogTest("Services - ISaveService", gm.GetService<ISaveService>() != null);
                LogTest("Services - IMissionService", gm.GetService<IMissionService>() != null);
                LogTest("Services - IRegionService", gm.GetService<IRegionService>() != null);
                LogTest("Services - ICharacterService", gm.GetService<ICharacterService>() != null);
                LogTest("Services - IEconomyService", gm.GetService<IEconomyService>() != null);
                LogTest("Services - IUpgradeService", gm.GetService<IUpgradeService>() != null);
                LogTest("Services - IMapService", gm.GetService<IMapService>() != null);
                LogTest("Services - INpcService", gm.GetService<INpcService>() != null);
                LogTest("Services - IUIService", gm.GetService<IUIService>() != null);
            }
            else
            {
                LogTest("Services - GameManager exists", false);
            }
        }

        private static Button FindButton(string name)
        {
            Button[] buttons = Object.FindObjectsByType<Button>(FindObjectsInactive.Include);
            foreach (Button b in buttons)
            {
                if (b.name == name) return b;
            }
            return null;
        }

        private static void LogTest(string name, bool passed)
        {
            if (passed)
            {
                testsPassed++;
                Debug.Log($"[TEST PASS] {name}");
            }
            else
            {
                testsFailed++;
                failures.Add(name);
                Debug.LogError($"[TEST FAIL] {name}");
            }
        }

        private static void PrintResults()
        {
            Debug.Log("========================================");
            Debug.Log($"TEST RESULTS: {testsPassed} passed, {testsFailed} failed");
            Debug.Log("========================================");

            if (testsFailed > 0)
            {
                Debug.LogError("FAILED TESTS:");
                foreach (string f in failures)
                {
                    Debug.LogError($"  - {f}");
                }
            }
            else
            {
                Debug.Log("ALL TESTS PASSED!");
            }
        }
    }
}