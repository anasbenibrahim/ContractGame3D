using System;
using System.IO;
using System.Linq;
using Contract.Contracts;
using Contract.Core;
using Contract.Audio;
using Contract.SaveSystem;
using Contract.Missions;
using Contract.Regions;
using Contract.Character;
using Contract.Economy;
using Contract.Progression;
using UnityEditor.Build;
using Contract.Map;
using Contract.NPCs;
using Contract.UI;
using Contract.VFX;
using Contract.Accessibility;
using Contract.Performance;
using Contract.Localization;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace Contract.EditorTools
{
    /// <summary>
    /// Final validation and project summary for CONTRACT: SEOUL SHADOWS.
    /// Run: CONTRACT/Validate/Final Validation
    /// </summary>
    public static class FinalValidation
    {
        private static int checksPassed = 0;
        private static int checksFailed = 0;
        private static System.Collections.Generic.List<string> failures = new System.Collections.Generic.List<string>();

        [MenuItem("CONTRACT/Validate/Final Validation")]
        public static void RunFinalValidation()
        {
            checksPassed = 0;
            checksFailed = 0;
            failures.Clear();

            Debug.Log("========================================");
            Debug.Log("CONTRACT: SEOUL SHADOWS - FINAL VALIDATION");
            Debug.Log("========================================");

            // 1. Scene validation
            ValidateAllScenes();

            // 2. ScriptableObject validation
            ValidateScriptableObjects();

            // 3. Prefab validation
            ValidatePrefabs();

            // 4. Settings validation
            ValidateSettings();

            // 5. Service architecture validation
            ValidateServiceArchitecture();

            // 6. Input system validation
            ValidateInputSystem();

            // 7. Build readiness
            ValidateBuildReadiness();

            // Print summary
            PrintSummary();
        }

        private static void ValidateAllScenes()
        {
            LogSection("SCENE VALIDATION");

            string[] scenes = {
                "Assets/Scenes/MainMenu.unity",
                "Assets/Scenes/CharacterSelection.unity",
                "Assets/Scenes/MissionSelection.unity",
                "Assets/Scenes/GameWorld.unity",
            };

            foreach (string scenePath in scenes)
            {
                LogCheck($"Scene exists: {Path.GetFileName(scenePath)}", File.Exists(scenePath));

                if (File.Exists(scenePath))
                {
                    // Open and check
                    Scene scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
                    int rootCount = scene.rootCount;
                    LogCheck($"  Root objects: {rootCount}", rootCount > 0);
                }
            }
        }

        private static void ValidateScriptableObjects()
        {
            LogSection("SCRIPTABLEOBJECT VALIDATION");

            string[] requiredSOs = {
                // Characters
                "Assets/ScriptableObjects/Characters/char_alex_morgan.asset",
                "Assets/ScriptableObjects/Characters/char_daniel_cross.asset",
                "Assets/ScriptableObjects/Characters/char_ryan_blake.asset",
                // Regions
                "Assets/ScriptableObjects/Regions/region_downtown.asset",
                "Assets/ScriptableObjects/Regions/region_industrial.asset",
                "Assets/ScriptableObjects/Regions/region_residential.asset",
                "Assets/ScriptableObjects/Regions/region_luxury.asset",
                "Assets/ScriptableObjects/Regions/region_harbor.asset",
                // Missions
                "Assets/ScriptableObjects/Missions/mission_downtown_01.asset",
                "Assets/ScriptableObjects/Missions/mission_downtown_02.asset",
                "Assets/ScriptableObjects/Missions/mission_downtown_03.asset",
                "Assets/ScriptableObjects/Missions/mission_industrial_01.asset",
                "Assets/ScriptableObjects/Missions/mission_industrial_02.asset",
                "Assets/ScriptableObjects/Missions/mission_residential_01.asset",
                "Assets/ScriptableObjects/Missions/mission_residential_02.asset",
                "Assets/ScriptableObjects/Missions/mission_residential_03.asset",
                "Assets/ScriptableObjects/Missions/mission_luxury_01.asset",
                "Assets/ScriptableObjects/Missions/mission_luxury_02.asset",
                "Assets/ScriptableObjects/Missions/mission_harbor_01.asset",
                "Assets/ScriptableObjects/Missions/mission_harbor_02.asset",
                "Assets/ScriptableObjects/Missions/mission_harbor_03.asset",
                // Upgrades
                "Assets/ScriptableObjects/Upgrades/upgrade_sprint_speed_1.asset",
                "Assets/ScriptableObjects/Upgrades/upgrade_stealth_rating_1.asset",
                "Assets/ScriptableObjects/Upgrades/upgrade_interaction_range_1.asset",
                "Assets/ScriptableObjects/Upgrades/upgrade_inventory_slots_1.asset",
                // Items
                "Assets/ScriptableObjects/Upgrades/keycard.asset",
                "Assets/ScriptableObjects/Upgrades/stamina_shot.asset",
                "Assets/ScriptableObjects/Upgrades/lockpick_set.asset",
                // Audio
                "Assets/ScriptableObjects/Upgrades/AudioLibrary.asset",
                "Assets/ScriptableObjects/Upgrades/ItemLibrary.asset",
            };

            foreach (string path in requiredSOs)
            {
                LogCheck($"SO exists: {Path.GetFileName(path)}", File.Exists(path));
            }
        }

        private static void ValidatePrefabs()
        {
            LogSection("PREFAB VALIDATION");

            string[] requiredPrefabs = {
                "Assets/Prefabs/Player/Player.prefab",
                "Assets/Prefabs/NPCs/CivilianNPC.prefab",
                "Assets/Prefabs/NPCs/MissionNPC.prefab",
                "Assets/Prefabs/UI/HUDCanvas.prefab",
                "Assets/Prefabs/UI/MinimapCanvas.prefab",
            };

            foreach (string path in requiredPrefabs)
            {
                bool exists = File.Exists(path);
                LogCheck($"Prefab exists: {Path.GetFileName(path)}", exists);

                if (exists)
                {
                    GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                    if (prefab != null)
                    {
                        // Check for missing scripts
                        Component[] components = prefab.GetComponentsInChildren<Component>(true);
                        int missingScripts = 0;
                        foreach (Component c in components)
                        {
                            if (c == null) missingScripts++;
                        }
                        LogCheck($"  No missing scripts: {Path.GetFileName(path)}", missingScripts == 0);
                    }
                }
            }
        }

        private static void ValidateSettings()
        {
            LogSection("SETTINGS VALIDATION");

            // Input
            LogCheck("Input Actions exist", File.Exists("Assets/Settings/ContractInputActions.inputactions"));
            LogCheck("InputReader asset exists", File.Exists("Assets/Settings/InputReader.asset"));

            // Rendering
            LogCheck("URP Asset exists", File.Exists("Assets/Settings/PC_RPAsset.asset"));
            LogCheck("PostProcess Volume exists", File.Exists("Assets/Settings/ContractPostProcess.asset"));

            // Tags & Layers
            LogCheck("Tag 'Player' exists", HasTag("Player"));
            LogCheck("Tag 'NPC' exists", HasTag("NPC"));
            LogCheck("Tag 'MissionSubject' exists", HasTag("MissionSubject"));
            LogCheck("Tag 'Interactable' exists", HasTag("Interactable"));
            LogCheck("Tag 'ExtractionPoint' exists", HasTag("ExtractionPoint"));
            LogCheck("Tag 'Safehouse' exists", HasTag("Safehouse"));
            LogCheck("Layer 'Minimap' exists", LayerMask.NameToLayer("Minimap") >= 0);
            LogCheck("Layer 'Player' exists", LayerMask.NameToLayer("Player") >= 0);
            LogCheck("Layer 'NPC' exists", LayerMask.NameToLayer("NPC") >= 0);
            LogCheck("Layer 'Interactable' exists", LayerMask.NameToLayer("Interactable") >= 0);
            LogCheck("Layer 'MissionSubject' exists", LayerMask.NameToLayer("MissionSubject") >= 0);
            LogCheck("Layer 'Environment' exists", LayerMask.NameToLayer("Environment") >= 0);
        }

        private static void ValidateServiceArchitecture()
        {
            LogSection("SERVICE ARCHITECTURE VALIDATION");

            // Check interfaces exist
            Type[] interfaces = {
                typeof(IAudioService),
                typeof(ISaveService),
                typeof(IMissionService),
                typeof(IRegionService),
                typeof(ICharacterService),
                typeof(IEconomyService),
                typeof(IUpgradeService),
                typeof(IMapService),
                typeof(INpcService),
                typeof(IUIService),
            };

            foreach (Type t in interfaces)
            {
                LogCheck($"Interface exists: {t.Name}", t != null);
            }

            // Check services exist
            Type[] services = {
                typeof(AudioService),
                typeof(SaveManager),
                typeof(MissionManager),
                typeof(RegionManager),
                typeof(CharacterSelectionManager),
                typeof(EconomyManager),
                typeof(UpgradeManager),
                typeof(MapManager),
                typeof(NpcManager),
                typeof(UIManager),
                typeof(VFXManager),
                typeof(PostProcessingController),
                typeof(AccessibilityManager),
                typeof(PerformanceManager),
                typeof(LocalizationManager),
                typeof(UITransitionManager),
            };

            foreach (Type t in services)
            {
                LogCheck($"Service exists: {t.Name}", t != null);
            }
        }

        private static void ValidateInputSystem()
        {
            LogSection("INPUT SYSTEM VALIDATION");

            LogCheck("Input Actions asset", File.Exists("Assets/Settings/ContractInputActions.inputactions"));
            LogCheck("InputReader asset", File.Exists("Assets/Settings/InputReader.asset"));

            // Check action maps
            if (File.Exists("Assets/Settings/ContractInputActions.inputactions"))
            {
                string content = File.ReadAllText("Assets/Settings/ContractInputActions.inputactions");
                LogCheck("  Player map", content.Contains("Player"));
                LogCheck("  UI map", content.Contains("UI"));
                LogCheck("  Move action", content.Contains("Move"));
                LogCheck("  Look action", content.Contains("Look"));
                LogCheck("  Sprint action", content.Contains("Sprint"));
                LogCheck("  Crouch action", content.Contains("Crouch"));
                LogCheck("  Interact action", content.Contains("Interact"));
                LogCheck("  Jump action", content.Contains("Jump"));
                LogCheck("  OpenMap action", content.Contains("OpenMap"));
                LogCheck("  Pause action", content.Contains("Pause"));
            }
        }

        private static void ValidateBuildReadiness()
        {
            LogSection("BUILD READINESS VALIDATION");

            // Player settings
            LogCheck("Product name set", PlayerSettings.productName == "CONTRACT: SEOUL SHADOWS");
            LogCheck("Company name set", PlayerSettings.companyName == "Contract Games");
            LogCheck("Color space: Linear", PlayerSettings.colorSpace == ColorSpace.Linear);
            LogCheck("Scripting backend: Mono", PlayerSettings.GetScriptingBackend(NamedBuildTarget.Standalone) == ScriptingImplementation.Mono2x);
            LogCheck("API compatibility: .NET Standard", PlayerSettings.GetApiCompatibilityLevel(NamedBuildTarget.Standalone) == ApiCompatibilityLevel.NET_Standard);
            LogCheck("Default screen 1920x1080", PlayerSettings.defaultScreenWidth == 1920 && PlayerSettings.defaultScreenHeight == 1080);
            LogCheck("Fullscreen windowed", PlayerSettings.fullScreenMode == FullScreenMode.FullScreenWindow);
            LogCheck("Resizable window", PlayerSettings.resizableWindow == true);
            LogCheck("Run in background", PlayerSettings.runInBackground == true);

            // Build scenes
            string[] buildScenes = {
                "Assets/Scenes/MainMenu.unity",
                "Assets/Scenes/CharacterSelection.unity",
                "Assets/Scenes/MissionSelection.unity",
                "Assets/Scenes/GameWorld.unity",
            };

            foreach (string scene in buildScenes)
            {
                LogCheck($"Build scene: {Path.GetFileName(scene)}", File.Exists(scene));
            }
        }

        private static bool HasTag(string tagName)
        {
            SerializedObject tagManager = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);
            SerializedProperty tags = tagManager.FindProperty("tags");
            for (int i = 0; i < tags.arraySize; i++)
            {
                if (tags.GetArrayElementAtIndex(i).stringValue == tagName) return true;
            }
            return false;
        }

        private static void LogSection(string title)
        {
            Debug.Log($"\n--- {title} ---");
        }

        private static void LogCheck(string name, bool passed)
        {
            if (passed)
            {
                checksPassed++;
                Debug.Log($"  ✓ {name}");
            }
            else
            {
                checksFailed++;
                failures.Add(name);
                Debug.LogError($"  ✗ {name}");
            }
        }

        private static void PrintSummary()
        {
            Debug.Log("\n========================================");
            Debug.Log("FINAL VALIDATION SUMMARY");
            Debug.Log("========================================");
            Debug.Log($"Total Checks: {checksPassed + checksFailed}");
            Debug.Log($"Passed: {checksPassed}");
            Debug.Log($"Failed: {checksFailed}");
            Debug.Log($"Success Rate: {(float)checksPassed / (checksPassed + checksFailed) * 100:F1}%");

            if (checksFailed > 0)
            {
                Debug.LogError("\nFAILED CHECKS:");
                foreach (string f in failures)
                {
                    Debug.LogError($"  - {f}");
                }
                Debug.LogError("\n❌ VALIDATION FAILED - Fix issues before build");
            }
            else
            {
                Debug.Log("\n✅ ALL VALIDATIONS PASSED - Ready for build!");
            }
            Debug.Log("========================================");
        }
    }
}