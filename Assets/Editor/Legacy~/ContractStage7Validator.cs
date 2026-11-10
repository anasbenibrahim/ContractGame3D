using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Contract.Character;
using Contract.Data;
using Contract.Missions;
using Contract.Regions;
using Contract.UI;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace Contract.EditorTools
{
    public static class ContractStage7Validator
    {
        private const string MarkerPath = "ProjectSettings/ContractStage7.done";
        private const string MainMenuPath = "Assets/Scenes/MainMenu.unity";
        private const string CharacterSelectionPath = "Assets/Scenes/CharacterSelection.unity";
        private const string MissionSelectionPath = "Assets/Scenes/MissionSelection.unity";
        private const string GameWorldPath = "Assets/Scenes/GameWorld.unity";
        private const string PrefabFolder = "Assets/Prefabs/UI";
        private const string CharacterFolder = "Assets/ScriptableObjects/Characters";
        private const string MissionFolder = "Assets/ScriptableObjects/Missions";
        private const string RegionFolder = "Assets/ScriptableObjects/Regions";

        [MenuItem("CONTRACT/Validate/Stage 7")]
        public static void Validate()
        {
            var failures = new List<string>();
            if (!File.Exists(MarkerPath))
            {
                failures.Add("Stage 7 setup marker is missing.");
            }

            ValidateMainMenu(failures);
            ValidateCharacterSelection(failures);
            ValidateMissionSelection(failures);
            ValidateGameWorld(failures);
            ValidatePrefabs(failures);
            ValidateData(failures);

            if (failures.Count > 0)
            {
                foreach (string failure in failures)
                {
                    Debug.LogError($"[CONTRACT-STAGE7] {failure}");
                }

                Debug.LogError($"[CONTRACT-STAGE7] FAILED with {failures.Count} issue(s).");
                if (Application.isBatchMode)
                {
                    EditorApplication.Exit(1);
                }
                return;
            }

            Debug.Log("[CONTRACT-STAGE7] PASSED: menus, HUD, inventory, map and pause UI are valid.");
        }

        private static void ValidateMainMenu(ICollection<string> failures)
        {
            Scene scene = EditorSceneManager.OpenScene(MainMenuPath, OpenSceneMode.Single);
            MainMenuController menu = FindInScene<MainMenuController>(scene);
            if (menu == null)
            {
                failures.Add("MainMenu scene is missing a MainMenuController.");
            }
            else
            {
                RequireFields(menu, "MainMenuController", failures,
                    "playButton", "continueButton", "characterSelectionButton", "settingsButton",
                    "creditsButton", "quitButton", "mainPanel", "creditsPanel", "settingsPanel", "cinematicCamera");
            }

            SettingsController settings = FindInScene<SettingsController>(scene);
            if (settings == null) failures.Add("MainMenu scene is missing a SettingsController.");
            else RequireFields(settings, "SettingsController", failures,
                "masterVolumeSlider", "musicVolumeSlider", "sfxVolumeSlider", "masterVolumeText",
                "musicVolumeText", "sfxVolumeText", "qualityDropdown", "vSyncToggle", "resolutionDropdown",
                "mouseSensitivitySlider", "gamepadSensitivitySlider", "mouseSensitivityText",
                "gamepadSensitivityText", "backButton", "applyButton", "defaultsButton");

            CinematicCameraController cinematic = FindInScene<CinematicCameraController>(scene);
            if (cinematic == null)
            {
                failures.Add("MainMenu is missing a CinematicCameraController.");
            }
            else
            {
                Transform[] waypoints = GetField<Transform[]>(cinematic, "waypoints");
                if (waypoints == null || waypoints.Length < 2)
                {
                    failures.Add($"CinematicCameraController has {waypoints?.Length ?? 0} waypoints, expected at least 2.");
                }
            }

            if (FindInScene<EventSystem>(scene) == null) failures.Add("MainMenu is missing an EventSystem.");
        }

        private static void ValidateCharacterSelection(ICollection<string> failures)
        {
            Scene scene = EditorSceneManager.OpenScene(CharacterSelectionPath, OpenSceneMode.Single);
            CharacterSelectionController controller = FindInScene<CharacterSelectionController>(scene);
            if (controller == null)
            {
                failures.Add("CharacterSelection scene is missing a CharacterSelectionController.");
            }
            else
            {
                RequireFields(controller, "CharacterSelectionController", failures,
                    "detailsPanel", "characterNameText", "specialtyText", "descriptionText",
                    "speedStatText", "stealthStatText", "intelligenceStatText",
                    "characterPreviewImage", "selectButton", "lockedOverlay", "backButton");

                var slots = GetField<CharacterSlotUI[]>(controller, "characterSlots");
                if (slots == null || slots.Length < 3)
                {
                    failures.Add($"CharacterSelectionController has {slots?.Length ?? 0} slots, expected at least 3.");
                }
                else
                {
                    for (int i = 0; i < slots.Length; i++)
                    {
                        CharacterSlotUI slot = slots[i];
                        if (slot == null || slot.slotObject == null || slot.portraitImage == null ||
                            slot.nameText == null || slot.specialtyText == null ||
                            slot.lockedOverlay == null || slot.selectButton == null)
                        {
                            failures.Add($"Character slot {i} is missing references.");
                        }
                    }
                }
            }

            CharacterSelectionManager manager = FindInScene<CharacterSelectionManager>(scene);
            if (manager == null) failures.Add("CharacterSelection scene is missing a CharacterSelectionManager.");
            else
            {
                CharacterData[] characters = GetField<CharacterData[]>(manager, "allCharacters");
                if (characters == null || characters.Length < 3)
                {
                    failures.Add($"CharacterSelectionManager has {characters?.Length ?? 0} characters, expected at least 3.");
                }
            }

            if (FindInScene<EventSystem>(scene) == null) failures.Add("CharacterSelection is missing an EventSystem.");
        }

        private static void ValidateMissionSelection(ICollection<string> failures)
        {
            Scene scene = EditorSceneManager.OpenScene(MissionSelectionPath, OpenSceneMode.Single);
            MissionSelectionController controller = FindInScene<MissionSelectionController>(scene);
            if (controller == null)
            {
                failures.Add("MissionSelection scene is missing a MissionSelectionController.");
            }
            else
            {
                RequireFields(controller, "MissionSelectionController", failures,
                    "regionTabContainer", "regionTabPrefab", "missionListContainer", "missionItemPrefab",
                    "detailsPanel", "missionNameText", "missionDescriptionText", "rewardText",
                    "difficultyText", "startMissionButton", "backButton");
            }

            MissionManager missionManager = FindInScene<MissionManager>(scene);
            if (missionManager == null) failures.Add("MissionSelection scene is missing a MissionManager.");
            else
            {
                MissionData[] missions = GetField<MissionData[]>(missionManager, "allMissions");
                if (missions == null || missions.Length < 12)
                {
                    failures.Add($"MissionManager has {missions?.Length ?? 0} missions, expected at least 12.");
                }
            }

            RegionManager regionManager = FindInScene<RegionManager>(scene);
            if (regionManager == null) failures.Add("MissionSelection scene is missing a RegionManager.");
            else
            {
                RegionData[] regions = GetField<RegionData[]>(regionManager, "allRegions");
                if (regions == null || regions.Length < 5)
                {
                    failures.Add($"RegionManager has {regions?.Length ?? 0} regions, expected at least 5.");
                }
            }

            if (FindInScene<EventSystem>(scene) == null) failures.Add("MissionSelection is missing an EventSystem.");
        }

        private static void ValidateGameWorld(ICollection<string> failures)
        {
            Scene scene = EditorSceneManager.OpenScene(GameWorldPath, OpenSceneMode.Single);

            HUDController hud = FindInScene<HUDController>(scene);
            if (hud == null) failures.Add("GameWorld is missing a HUDController.");
            else RequireFields(hud, "HUDController", failures,
                "moneyText", "objectiveText", "objectivePanel", "suspicionSlider", "suspicionFill",
                "minimapImage", "inventoryUI", "interactionPromptPanel", "interactionPromptText");

            InventoryUI inventory = FindInScene<InventoryUI>(scene);
            if (inventory == null) failures.Add("GameWorld is missing an InventoryUI.");
            else RequireFields(inventory, "InventoryUI", failures,
                "inventoryContainer", "inventorySlotPrefab", "detailPanel", "detailIcon", "detailName", "detailDescription");

            PauseMenuController pause = FindInScene<PauseMenuController>(scene);
            if (pause == null) failures.Add("GameWorld is missing a PauseMenuController.");
            else RequireFields(pause, "PauseMenuController", failures,
                "pausePanel", "resumeButton", "settingsButton", "mainMenuButton", "quitButton");

            Contract.Map.FullMapView fullMap = FindInScene<Contract.Map.FullMapView>(scene);
            if (fullMap == null) failures.Add("GameWorld is missing a FullMapView.");
            else RequireFields(fullMap, "FullMapView", failures,
                "panel", "mapArea", "mapImage", "markerLayer", "playerMarker", "regionTitle", "coordinates",
                "zoomSlider", "closeButton", "recenterButton");

            UIManager uiManager = FindInScene<UIManager>(scene);
            if (uiManager == null) failures.Add("GameWorld is missing a UIManager.");
            else RequireFields(uiManager, "UIManager", failures,
                "mainCanvas", "hudPanel", "pausePanel", "settingsPanel");

            SettingsController settings = FindInScene<SettingsController>(scene);
            if (settings == null) failures.Add("GameWorld is missing a SettingsController.");

            if (FindInScene<EventSystem>(scene) == null) failures.Add("GameWorld is missing an EventSystem.");
        }

        private static void ValidatePrefabs(ICollection<string> failures)
        {
            ValidatePrefab(failures, PrefabFolder + "/HUDCanvas.prefab");
            ValidatePrefab(failures, PrefabFolder + "/MinimapCanvas.prefab");
            ValidatePrefab(failures, PrefabFolder + "/RegionTab.prefab");
            ValidatePrefab(failures, PrefabFolder + "/MissionItem.prefab");
            ValidatePrefab(failures, PrefabFolder + "/InventorySlot.prefab");

            GameObject missionItem = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabFolder + "/MissionItem.prefab");
            if (missionItem != null)
            {
                if (missionItem.GetComponent<MissionItemUI>() == null) failures.Add("MissionItem prefab missing MissionItemUI.");
                if (missionItem.GetComponent<Button>() == null) failures.Add("MissionItem prefab missing Button.");
                if (missionItem.GetComponentsInChildren<TMP_Text>(true).Length < 2) failures.Add("MissionItem prefab is missing text elements.");
            }

            GameObject inventorySlot = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabFolder + "/InventorySlot.prefab");
            if (inventorySlot != null)
            {
                if (inventorySlot.GetComponent<InventorySlotUI>() == null) failures.Add("InventorySlot prefab missing InventorySlotUI.");
                if (inventorySlot.GetComponent<Button>() == null) failures.Add("InventorySlot prefab missing Button.");
            }

            GameObject regionTab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabFolder + "/RegionTab.prefab");
            if (regionTab != null)
            {
                if (regionTab.GetComponent<Button>() == null) failures.Add("RegionTab prefab missing Button.");
                if (regionTab.GetComponentInChildren<TMP_Text>(true) == null) failures.Add("RegionTab prefab missing text.");
            }
        }

        private static void ValidatePrefab(ICollection<string> failures, string path)
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab == null)
            {
                failures.Add($"Missing UI prefab: {path}.");
                return;
            }

            if (prefab.GetComponentsInChildren<Component>(true).Any(component => component == null))
            {
                failures.Add($"UI prefab has missing scripts: {path}.");
            }
        }

        private static void ValidateData(ICollection<string> failures)
        {
            string[] characters = AssetDatabase.FindAssets("t:CharacterData", new[] { CharacterFolder });
            if (characters.Length < 3) failures.Add($"Character folder contains {characters.Length} assets, expected at least 3.");

            string[] missions = AssetDatabase.FindAssets("t:MissionData", new[] { MissionFolder });
            if (missions.Length < 12) failures.Add($"Mission folder contains {missions.Length} assets, expected at least 12.");

            string[] regions = AssetDatabase.FindAssets("t:RegionData", new[] { RegionFolder });
            if (regions.Length < 5) failures.Add($"Region folder contains {regions.Length} assets, expected at least 5.");
        }

        private static void RequireFields(object target, string typeName, ICollection<string> failures, params string[] fieldNames)
        {
            foreach (string fieldName in fieldNames)
            {
                FieldInfo field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
                if (field == null || field.GetValue(target) == null)
                {
                    failures.Add($"{typeName} missing reference: {fieldName}.");
                }
            }
        }

        private static T GetField<T>(object target, string fieldName) where T : class
        {
            FieldInfo field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            return field?.GetValue(target) as T;
        }

        private static T FindInScene<T>(Scene scene) where T : Component
        {
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                T component = root.GetComponentInChildren<T>(true);
                if (component != null) return component;
            }

            return null;
        }
    }
}
