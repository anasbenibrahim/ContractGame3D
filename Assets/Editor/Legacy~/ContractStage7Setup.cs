using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Contract.Character;
using Contract.Data;
using Contract.Missions;
using Contract.Regions;
using Contract.UI;
using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace Contract.EditorTools
{
    [InitializeOnLoad]
    public static class ContractStage7Setup
    {
        private const string MarkerPath = "ProjectSettings/ContractStage7.done";
        private const string Stage6MarkerPath = "ProjectSettings/ContractStage6.done";
        private const string MainMenuPath = "Assets/Scenes/MainMenu.unity";
        private const string CharacterSelectionPath = "Assets/Scenes/CharacterSelection.unity";
        private const string MissionSelectionPath = "Assets/Scenes/MissionSelection.unity";
        private const string GameWorldPath = "Assets/Scenes/GameWorld.unity";
        private const string PrefabFolder = "Assets/Prefabs/UI";
        private const string CharacterFolder = "Assets/ScriptableObjects/Characters";
        private const string MissionFolder = "Assets/ScriptableObjects/Missions";
        private const string RegionFolder = "Assets/ScriptableObjects/Regions";

        private static bool isRunning;

        static ContractStage7Setup()
        {
            EditorApplication.delayCall += AutoSetup;
        }

        [MenuItem("CONTRACT/Setup/Rebuild Stage 7 UI and Menus")]
        public static void Rebuild()
        {
            if (File.Exists(MarkerPath))
            {
                File.Delete(MarkerPath);
            }

            RunSetup();
        }

        private static void AutoSetup()
        {
            if (Application.isBatchMode || File.Exists(MarkerPath) || EditorApplication.isPlayingOrWillChangePlaymode)
            {
                return;
            }

            if (EditorApplication.isCompiling || EditorApplication.isUpdating)
            {
                EditorApplication.delayCall += AutoSetup;
                return;
            }

            if (!File.Exists(Stage6MarkerPath) || !File.Exists(MainMenuPath))
            {
                EditorApplication.delayCall += AutoSetup;
                return;
            }

            RunSetup();
        }

        public static void RunSetup()
        {
            if (isRunning) return;
            isRunning = true;

            try
            {
                Debug.Log("[CONTRACT] Stage 7: building menus, HUD, inventory, map and pause UI...");
                CreateScriptableObjectAssets.CreateAllAssets();
                EnsureFolder(PrefabFolder);

                BuildMainMenu();
                BuildCharacterSelection();
                BuildMissionSelection();
                BuildGameWorldUI();
                BuildStandalonePrefabs();

                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();

                File.WriteAllText(MarkerPath, DateTime.UtcNow.ToString("o"));
                Debug.Log("[CONTRACT] Stage 7 setup complete.");
            }
            catch (Exception exception)
            {
                Debug.LogError("[CONTRACT] Stage 7 setup failed: " + exception);
            }
            finally
            {
                isRunning = false;
            }
        }

        private static void BuildMainMenu()
        {
            Scene scene = EditorSceneManager.OpenScene(MainMenuPath, OpenSceneMode.Single);
            DestroyRoot(scene, "[MainMenu]");
            DestroyRoot(scene, "[CameraWaypoints]");
            DestroyByName(scene, "Main Menu Panel");
            DestroyByName(scene, "Main Menu Settings Panel");
            DestroyByName(scene, "Main Menu Credits Panel");

            Canvas canvas = FindCanvas();
            if (canvas == null)
            {
                Debug.LogError("[CONTRACT] Stage 7: MainMenu requires a Canvas.");
                return;
            }

            GameObject root = CreateRoot(scene, "[MainMenu]");
            MainMenuController menu = root.AddComponent<MainMenuController>();
            SettingsController settings = root.AddComponent<SettingsController>();

            GameObject mainPanel = CreatePanel(canvas.transform, "Main Menu Panel", new Color(0.015f, 0.02f, 0.03f, 0.82f));
            SetAnchors(mainPanel.GetComponent<RectTransform>(), Vector2.zero, Vector2.zero,
                new Vector2(80f, 120f), new Vector2(440f, 660f));
            VerticalLayoutGroup layout = mainPanel.AddComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(18, 18, 18, 18);
            layout.spacing = 10f;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;

            Button play = CreateLayoutButton(mainPanel.transform, "Play Button", "PLAY", new Color(0.24f, 0.55f, 0.28f, 1f), 58f);
            Button continueButton = CreateLayoutButton(mainPanel.transform, "Continue Button", "CONTINUE", new Color(0.18f, 0.32f, 0.55f, 1f), 58f);
            Button characters = CreateLayoutButton(mainPanel.transform, "Character Selection Button", "OPERATIVES", new Color(0.18f, 0.32f, 0.55f, 1f), 58f);
            Button settingsButton = CreateLayoutButton(mainPanel.transform, "Settings Button", "SETTINGS", new Color(0.18f, 0.32f, 0.55f, 1f), 58f);
            Button creditsButton = CreateLayoutButton(mainPanel.transform, "Credits Button", "CREDITS", new Color(0.18f, 0.32f, 0.55f, 1f), 58f);
            Button quit = CreateLayoutButton(mainPanel.transform, "Quit Button", "QUIT", new Color(0.55f, 0.16f, 0.18f, 1f), 58f);

            GameObject settingsPanel = BuildSettingsPanel(canvas.transform, "Main Menu Settings Panel", settings);
            GameObject creditsPanel = BuildCreditsPanel(canvas.transform, "Main Menu Credits Panel", menu);

            Camera camera = FindMainCamera();
            CinematicCameraController cinematic = null;
            if (camera != null)
            {
                cinematic = camera.GetComponent<CinematicCameraController>();
                if (cinematic == null) cinematic = camera.gameObject.AddComponent<CinematicCameraController>();
            }
            else
            {
                Debug.LogWarning("[CONTRACT] Stage 7: MainMenu has no main camera.");
            }

            GameObject waypointRoot = CreateRoot(scene, "[CameraWaypoints]");
            Transform[] waypoints = new Transform[4];
            waypoints[0] = CreateWaypoint(waypointRoot.transform, "Waypoint 0", new Vector3(0f, 4.8f, -18f));
            waypoints[1] = CreateWaypoint(waypointRoot.transform, "Waypoint 1", new Vector3(4f, 5.4f, -16f));
            waypoints[2] = CreateWaypoint(waypointRoot.transform, "Waypoint 2", new Vector3(0f, 5f, -14f));
            waypoints[3] = CreateWaypoint(waypointRoot.transform, "Waypoint 3", new Vector3(-4f, 4.6f, -17f));
            if (cinematic != null)
            {
                SerializedObject so = new SerializedObject(cinematic);
                SetArray(so, "waypoints", waypoints);
                so.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(cinematic);
            }

            SerializedObject menuSo = new SerializedObject(menu);
            SetObject(menuSo, "playButton", play);
            SetObject(menuSo, "continueButton", continueButton);
            SetObject(menuSo, "characterSelectionButton", characters);
            SetObject(menuSo, "settingsButton", settingsButton);
            SetObject(menuSo, "creditsButton", creditsButton);
            SetObject(menuSo, "quitButton", quit);
            SetObject(menuSo, "mainPanel", mainPanel);
            SetObject(menuSo, "creditsPanel", creditsPanel);
            SetObject(menuSo, "settingsPanel", settingsPanel);
            SetObject(menuSo, "cinematicCamera", cinematic);
            menuSo.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(menu);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }

        private static void BuildCharacterSelection()
        {
            Scene scene = EditorSceneManager.OpenScene(CharacterSelectionPath, OpenSceneMode.Single);
            DestroyRoot(scene, "[CharacterSelection]");
            DestroyRoot(scene, "[CharacterSelectionManager]");
            DestroyByName(scene, "Character Slots Panel");
            DestroyByName(scene, "Character Details Panel");

            Canvas canvas = FindCanvas();
            if (canvas == null)
            {
                Debug.LogError("[CONTRACT] Stage 7: CharacterSelection requires a Canvas.");
                return;
            }

            CharacterSelectionManager manager = CreateManager<CharacterSelectionManager>(scene, "[CharacterSelectionManager]");
            AssignAssets<CharacterData>(manager, "allCharacters", CharacterFolder);

            GameObject root = CreateRoot(scene, "[CharacterSelection]");
            CharacterSelectionController controller = root.AddComponent<CharacterSelectionController>();

            GameObject slotsPanel = CreatePanel(canvas.transform, "Character Slots Panel", new Color(0f, 0f, 0f, 0f));
            SetAnchors(slotsPanel.GetComponent<RectTransform>(), new Vector2(0f, 0f), new Vector2(1f, 0f),
                new Vector2(80f, 120f), new Vector2(-440f, 330f));
            HorizontalLayoutGroup slotLayout = slotsPanel.AddComponent<HorizontalLayoutGroup>();
            slotLayout.spacing = 18f;
            slotLayout.padding = new RectOffset(0, 0, 0, 0);
            slotLayout.childControlWidth = true;
            slotLayout.childControlHeight = true;
            slotLayout.childForceExpandWidth = true;
            slotLayout.childForceExpandHeight = true;

            CharacterSlotUI[] slots = new CharacterSlotUI[3];
            for (int i = 0; i < slots.Length; i++)
            {
                GameObject slot = new GameObject("Character Slot " + (i + 1), typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
                slot.transform.SetParent(slotsPanel.transform, false);
                Image background = slot.GetComponent<Image>();
                background.color = new Color(0.06f, 0.08f, 0.12f, 0.95f);
                Button button = slot.GetComponent<Button>();
                button.targetGraphic = background;
                slot.AddComponent<LayoutElement>().preferredHeight = 190f;

                Image portrait = CreateImage(slot.transform, "Portrait", new Color(0.15f, 0.18f, 0.26f, 1f),
                    new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(16f, -150f), new Vector2(126f, -20f));
                TMP_Text name = CreateText(slot.transform, "Name", "OPERATIVE", 22f, Color.white,
                    TextAlignmentOptions.Left, new Vector2(0f, 1f), new Vector2(1f, 1f),
                    new Vector2(138f, -56f), new Vector2(-12f, -18f));
                TMP_Text specialty = CreateText(slot.transform, "Specialty", "SPECIALTY", 16f, new Color(0.55f, 0.75f, 1f),
                    TextAlignmentOptions.Left, new Vector2(0f, 1f), new Vector2(1f, 1f),
                    new Vector2(138f, -94f), new Vector2(-12f, -58f));
                GameObject locked = CreatePanel(slot.transform, "Locked Overlay", new Color(0.02f, 0.02f, 0.03f, 0.72f));
                SetStretch(locked.GetComponent<RectTransform>(), 0f, 0f, 0f, 0f);
                TMP_Text lockedText = CreateText(locked.transform, "Locked Text", "LOCKED", 20f, new Color(1f, 0.45f, 0.4f),
                    TextAlignmentOptions.Center, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
                lockedText.raycastTarget = false;

                slots[i] = new CharacterSlotUI
                {
                    slotObject = slot,
                    portraitImage = portrait,
                    nameText = name,
                    specialtyText = specialty,
                    lockedOverlay = locked,
                    selectButton = button
                };
            }

            GameObject detailsPanel = CreatePanel(canvas.transform, "Character Details Panel", new Color(0.03f, 0.045f, 0.07f, 0.97f));
            SetAnchors(detailsPanel.GetComponent<RectTransform>(), new Vector2(1f, 0f), new Vector2(1f, 1f),
                new Vector2(-420f, 110f), new Vector2(-60f, -170f));

            TMP_Text detailName = CreateText(detailsPanel.transform, "Character Name", "OPERATIVE", 30f, Color.white,
                TextAlignmentOptions.Left, new Vector2(0f, 1f), new Vector2(1f, 1f),
                new Vector2(24f, -76f), new Vector2(-24f, -24f));
            TMP_Text detailSpecialty = CreateText(detailsPanel.transform, "Specialty", string.Empty, 18f, new Color(0.6f, 0.8f, 1f),
                TextAlignmentOptions.Left, new Vector2(0f, 1f), new Vector2(1f, 1f),
                new Vector2(24f, -112f), new Vector2(-24f, -80f));
            Image preview = CreateImage(detailsPanel.transform, "Preview", new Color(0.14f, 0.17f, 0.25f, 1f),
                new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(24f, -286f), new Vector2(204f, -124f));
            TMP_Text description = CreateText(detailsPanel.transform, "Description", string.Empty, 17f, new Color(0.82f, 0.85f, 0.9f),
                TextAlignmentOptions.TopLeft, new Vector2(0f, 1f), new Vector2(1f, 1f),
                new Vector2(216f, -286f), new Vector2(-24f, -124f));
            TMP_Text speed = CreateText(detailsPanel.transform, "Speed Stat", "SPEED 0", 18f, new Color(0.4f, 1f, 0.6f),
                TextAlignmentOptions.Left, new Vector2(0f, 1f), new Vector2(1f, 1f),
                new Vector2(24f, -348f), new Vector2(-24f, -304f));
            TMP_Text stealth = CreateText(detailsPanel.transform, "Stealth Stat", "STEALTH 0", 18f, new Color(0.4f, 1f, 0.6f),
                TextAlignmentOptions.Left, new Vector2(0f, 1f), new Vector2(1f, 1f),
                new Vector2(24f, -386f), new Vector2(-24f, -342f));
            TMP_Text intelligence = CreateText(detailsPanel.transform, "Intelligence Stat", "INTELLIGENCE 0", 18f, new Color(0.4f, 1f, 0.6f),
                TextAlignmentOptions.Left, new Vector2(0f, 1f), new Vector2(1f, 1f),
                new Vector2(24f, -424f), new Vector2(-24f, -380f));

            GameObject lockedOverlay = CreatePanel(detailsPanel.transform, "Locked Overlay", new Color(0.02f, 0.02f, 0.03f, 0.65f));
            SetStretch(lockedOverlay.GetComponent<RectTransform>(), 0f, 0f, 0f, 0f);
            CreateText(lockedOverlay.transform, "Locked Text", "COMPLETE MISSIONS TO UNLOCK", 20f, new Color(1f, 0.45f, 0.4f),
                TextAlignmentOptions.Center, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);

            Button selectButton = CreateButton(detailsPanel.transform, "Select Button", "SELECT OPERATIVE", new Color(0.18f, 0.5f, 0.26f, 1f),
                new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(24f, 70f), new Vector2(-24f, 126f));
            Button backButton = CreateButton(detailsPanel.transform, "Back Button", "BACK", new Color(0.32f, 0.34f, 0.4f, 1f),
                new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(24f, 14f), new Vector2(-24f, 60f));

            SerializedObject so = new SerializedObject(controller);
            SerializedProperty slotProp = so.FindProperty("characterSlots");
            slotProp.arraySize = slots.Length;
            for (int i = 0; i < slots.Length; i++)
            {
                SerializedProperty element = slotProp.GetArrayElementAtIndex(i);
                element.FindPropertyRelative("slotObject").objectReferenceValue = slots[i].slotObject;
                element.FindPropertyRelative("portraitImage").objectReferenceValue = slots[i].portraitImage;
                element.FindPropertyRelative("nameText").objectReferenceValue = slots[i].nameText;
                element.FindPropertyRelative("specialtyText").objectReferenceValue = slots[i].specialtyText;
                element.FindPropertyRelative("lockedOverlay").objectReferenceValue = slots[i].lockedOverlay;
                element.FindPropertyRelative("selectButton").objectReferenceValue = slots[i].selectButton;
            }

            SetObject(so, "detailsPanel", detailsPanel);
            SetObject(so, "characterNameText", detailName);
            SetObject(so, "specialtyText", detailSpecialty);
            SetObject(so, "descriptionText", description);
            SetObject(so, "speedStatText", speed);
            SetObject(so, "stealthStatText", stealth);
            SetObject(so, "intelligenceStatText", intelligence);
            SetObject(so, "characterPreviewImage", preview);
            SetObject(so, "selectButton", selectButton);
            SetObject(so, "lockedOverlay", lockedOverlay);
            SetObject(so, "backButton", backButton);
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(controller);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }

        private static void BuildMissionSelection()
        {
            Scene scene = EditorSceneManager.OpenScene(MissionSelectionPath, OpenSceneMode.Single);
            DestroyRoot(scene, "[MissionSelection]");
            DestroyRoot(scene, "[MissionManager]");
            DestroyRoot(scene, "[RegionManager]");
            DestroyByName(scene, "Region Tabs");
            DestroyByName(scene, "Mission List Panel");
            DestroyByName(scene, "Mission Details Panel");

            Canvas canvas = FindCanvas();
            if (canvas == null)
            {
                Debug.LogError("[CONTRACT] Stage 7: MissionSelection requires a Canvas.");
                return;
            }

            MissionManager missionManager = CreateManager<MissionManager>(scene, "[MissionManager]");
            AssignAssets<MissionData>(missionManager, "allMissions", MissionFolder);
            RegionManager regionManager = CreateManager<RegionManager>(scene, "[RegionManager]");
            AssignAssets<RegionData>(regionManager, "allRegions", RegionFolder);

            GameObject root = CreateRoot(scene, "[MissionSelection]");
            MissionSelectionController controller = root.AddComponent<MissionSelectionController>();

            GameObject regionTabPrefab = BuildRegionTabPrefab();
            GameObject missionItemPrefab = BuildMissionItemPrefab();

            GameObject tabContainer = CreatePanel(canvas.transform, "Region Tabs", new Color(0f, 0f, 0f, 0f));
            SetAnchors(tabContainer.GetComponent<RectTransform>(), new Vector2(0f, 1f), new Vector2(1f, 1f),
                new Vector2(80f, -210f), new Vector2(-480f, -160f));
            HorizontalLayoutGroup tabLayout = tabContainer.AddComponent<HorizontalLayoutGroup>();
            tabLayout.spacing = 10f;
            tabLayout.childControlWidth = true;
            tabLayout.childControlHeight = true;
            tabLayout.childForceExpandWidth = false;
            tabLayout.childForceExpandHeight = true;

            GameObject listPanel = CreatePanel(canvas.transform, "Mission List Panel", new Color(0.025f, 0.035f, 0.055f, 0.92f));
            SetAnchors(listPanel.GetComponent<RectTransform>(), new Vector2(0f, 0f), new Vector2(0f, 1f),
                new Vector2(80f, 100f), new Vector2(760f, -240f));
            ScrollRect scroll = listPanel.AddComponent<ScrollRect>();
            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 30f;

            GameObject viewport = CreatePanel(listPanel.transform, "Viewport", new Color(0f, 0f, 0f, 0f));
            SetStretch(viewport.GetComponent<RectTransform>(), 12f, 12f, 12f, 12f);
            viewport.AddComponent<RectMask2D>();

            GameObject content = new GameObject("Content", typeof(RectTransform));
            content.transform.SetParent(viewport.transform, false);
            RectTransform contentRect = content.GetComponent<RectTransform>();
            contentRect.anchorMin = new Vector2(0f, 1f);
            contentRect.anchorMax = new Vector2(1f, 1f);
            contentRect.pivot = new Vector2(0.5f, 1f);
            contentRect.anchoredPosition = Vector2.zero;
            contentRect.sizeDelta = Vector2.zero;
            VerticalLayoutGroup listLayout = content.AddComponent<VerticalLayoutGroup>();
            listLayout.spacing = 8f;
            listLayout.childControlWidth = true;
            listLayout.childControlHeight = true;
            listLayout.childForceExpandWidth = true;
            listLayout.childForceExpandHeight = false;
            content.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            scroll.viewport = viewport.GetComponent<RectTransform>();
            scroll.content = contentRect;

            GameObject detailsPanel = CreatePanel(canvas.transform, "Mission Details Panel", new Color(0.03f, 0.045f, 0.07f, 0.97f));
            SetAnchors(detailsPanel.GetComponent<RectTransform>(), new Vector2(1f, 0f), new Vector2(1f, 1f),
                new Vector2(-440f, 100f), new Vector2(-60f, -240f));

            TMP_Text missionName = CreateText(detailsPanel.transform, "Mission Name", "MISSION", 28f, Color.white,
                TextAlignmentOptions.Left, new Vector2(0f, 1f), new Vector2(1f, 1f),
                new Vector2(24f, -74f), new Vector2(-24f, -22f));
            TMP_Text missionDescription = CreateText(detailsPanel.transform, "Mission Description", string.Empty, 17f, new Color(0.82f, 0.85f, 0.9f),
                TextAlignmentOptions.TopLeft, new Vector2(0f, 1f), new Vector2(1f, 1f),
                new Vector2(24f, -270f), new Vector2(-24f, -86f));
            TMP_Text reward = CreateText(detailsPanel.transform, "Reward", string.Empty, 20f, new Color(0.4f, 1f, 0.6f),
                TextAlignmentOptions.Left, new Vector2(0f, 1f), new Vector2(1f, 1f),
                new Vector2(24f, -326f), new Vector2(-24f, -282f));
            TMP_Text difficulty = CreateText(detailsPanel.transform, "Difficulty", string.Empty, 20f, new Color(1f, 0.82f, 0.35f),
                TextAlignmentOptions.Left, new Vector2(0f, 1f), new Vector2(1f, 1f),
                new Vector2(24f, -368f), new Vector2(-24f, -324f));
            Button startButton = CreateButton(detailsPanel.transform, "Start Mission Button", "START MISSION", new Color(0.18f, 0.5f, 0.26f, 1f),
                new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(24f, 70f), new Vector2(-24f, 126f));
            Button backButton = CreateButton(detailsPanel.transform, "Back Button", "BACK", new Color(0.32f, 0.34f, 0.4f, 1f),
                new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(24f, 14f), new Vector2(-24f, 60f));

            SerializedObject so = new SerializedObject(controller);
            SetObject(so, "regionTabContainer", tabContainer.transform);
            SetObject(so, "regionTabPrefab", regionTabPrefab);
            SetObject(so, "missionListContainer", content.transform);
            SetObject(so, "missionItemPrefab", missionItemPrefab);
            SetObject(so, "detailsPanel", detailsPanel);
            SetObject(so, "missionNameText", missionName);
            SetObject(so, "missionDescriptionText", missionDescription);
            SetObject(so, "rewardText", reward);
            SetObject(so, "difficultyText", difficulty);
            SetObject(so, "startMissionButton", startButton);
            SetObject(so, "backButton", backButton);
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(controller);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }

        private static void BuildGameWorldUI()
        {
            Scene scene = EditorSceneManager.OpenScene(GameWorldPath, OpenSceneMode.Single);
            DestroyRoot(scene, "[HUD]");
            DestroyRoot(scene, "[PauseMenu]");
            DestroyRoot(scene, "[GameSettings]");
            DestroyRoot(scene, "[UIManager]");
            DestroyRoot(scene, "[FullMapUI]");
            DestroyByName(scene, "HUD Panel");
            DestroyByName(scene, "Pause Panel");
            DestroyByName(scene, "Game Settings Panel");
            DestroyByName(scene, "Full Map Panel");
            DestroyByName(scene, "Inventory Detail Panel");

            Canvas canvas = FindCanvas();
            if (canvas == null)
            {
                Debug.LogError("[CONTRACT] Stage 7: GameWorld requires a Canvas.");
                return;
            }

            GameObject inventorySlotPrefab = BuildInventorySlotPrefab();
            GameObject hudPanel = BuildHud(canvas.transform, inventorySlotPrefab);
            GameObject pausePanel = BuildPausePanel(canvas.transform, out PauseMenuController pauseMenu);

            GameObject settingsRoot = CreateRoot(scene, "[GameSettings]");
            SettingsController settings = settingsRoot.AddComponent<SettingsController>();
            GameObject settingsPanel = BuildSettingsPanel(canvas.transform, "Game Settings Panel", settings);

            // The full map / minimap UI is built by Contract.Editor.World.GangnamMapBuilder (Stage 5).

            GameObject uiRoot = CreateRoot(scene, "[UIManager]");
            UIManager uiManager = uiRoot.AddComponent<UIManager>();
            SerializedObject uiSo = new SerializedObject(uiManager);
            SetObject(uiSo, "mainCanvas", canvas);
            SetObject(uiSo, "hudPanel", hudPanel);
            SetObject(uiSo, "pausePanel", pausePanel);
            SetObject(uiSo, "settingsPanel", settingsPanel);
            GameObject shopPanel = FindSceneObject(scene, "Upgrade Shop Panel");
            SetObject(uiSo, "upgradeShopPanel", shopPanel);
            uiSo.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(uiManager);

            pauseMenu = Object.FindAnyObjectByType<PauseMenuController>();
            if (pauseMenu != null)
            {
                SerializedObject pauseSo = new SerializedObject(pauseMenu);
                SetObject(pauseSo, "pausePanel", pausePanel);
                SetObject(pauseSo, "resumeButton", pausePanel.transform.Find("Pause Content/Resume Button")?.GetComponent<Button>());
                SetObject(pauseSo, "settingsButton", pausePanel.transform.Find("Pause Content/Settings Button")?.GetComponent<Button>());
                SetObject(pauseSo, "mainMenuButton", pausePanel.transform.Find("Pause Content/Main Menu Button")?.GetComponent<Button>());
                SetObject(pauseSo, "quitButton", pausePanel.transform.Find("Pause Content/Quit Button")?.GetComponent<Button>());
                pauseSo.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(pauseMenu);
            }

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }

        private static GameObject BuildHud(Transform canvas, GameObject inventorySlotPrefab)
        {
            GameObject hudRoot = new GameObject("[HUD]");
            SceneManager.MoveGameObjectToScene(hudRoot, SceneManager.GetActiveScene());
            HUDController hud = hudRoot.AddComponent<HUDController>();
            InventoryUI inventory = hudRoot.AddComponent<InventoryUI>();

            GameObject hudPanel = CreatePanel(canvas, "HUD Panel", new Color(0f, 0f, 0f, 0f));
            SetStretch(hudPanel.GetComponent<RectTransform>(), 0f, 0f, 0f, 0f);
            hudPanel.GetComponent<Image>().raycastTarget = false;

            GameObject objectivePanel = CreatePanel(hudPanel.transform, "Objective Panel", new Color(0.02f, 0.03f, 0.045f, 0.82f));
            SetAnchors(objectivePanel.GetComponent<RectTransform>(), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(-280f, -150f), new Vector2(280f, -92f));
            TMP_Text objectiveText = CreateText(objectivePanel.transform, "Objective Text", "OBJECTIVE", 20f, new Color(0.92f, 0.9f, 0.8f),
                TextAlignmentOptions.Center, Vector2.zero, Vector2.one, new Vector2(16f, 6f), new Vector2(-16f, -6f));

            TMP_Text moneyText = CreateText(hudPanel.transform, "Money Text", "$0", 28f, new Color(0.35f, 1f, 0.55f),
                TextAlignmentOptions.Left, new Vector2(0f, 1f), new Vector2(0f, 1f),
                new Vector2(84f, -212f), new Vector2(360f, -162f));

            GameObject suspicionPanel = CreatePanel(hudPanel.transform, "Suspicion Panel", new Color(0.02f, 0.03f, 0.045f, 0.82f));
            SetAnchors(suspicionPanel.GetComponent<RectTransform>(), new Vector2(0f, 0f), new Vector2(0f, 0f),
                new Vector2(80f, 250f), new Vector2(420f, 320f));
            CreateText(suspicionPanel.transform, "Suspicion Label", "SUSPICION", 16f, new Color(1f, 0.75f, 0.4f),
                TextAlignmentOptions.Left, new Vector2(0f, 1f), new Vector2(1f, 1f),
                new Vector2(14f, -30f), new Vector2(-14f, -6f));
            Slider suspicionSlider = CreateSlider(suspicionPanel.transform, "Suspicion Slider",
                new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(14f, 12f), new Vector2(-14f, 46f));
            suspicionSlider.minValue = 0f;
            suspicionSlider.maxValue = 1f;
            suspicionSlider.value = 0f;
            Image suspicionFill = suspicionSlider.fillRect != null ? suspicionSlider.fillRect.GetComponent<Image>() : null;

            GameObject minimapPanel = CreatePanel(hudPanel.transform, "Minimap Panel", new Color(0.02f, 0.03f, 0.045f, 0.9f));
            SetAnchors(minimapPanel.GetComponent<RectTransform>(), new Vector2(1f, 1f), new Vector2(1f, 1f),
                new Vector2(-300f, -280f), new Vector2(-40f, -20f));
            GameObject minimapObject = new GameObject("Minimap Image", typeof(RectTransform), typeof(CanvasRenderer), typeof(RawImage));
            minimapObject.transform.SetParent(minimapPanel.transform, false);
            RawImage minimapImage = minimapObject.GetComponent<RawImage>();
            minimapImage.color = Color.white;
            minimapImage.raycastTarget = false;
            SetStretch(minimapImage.rectTransform, 10f, 10f, 10f, 10f);

            GameObject inventoryPanel = CreatePanel(hudPanel.transform, "Inventory Panel", new Color(0.02f, 0.03f, 0.045f, 0.82f));
            SetAnchors(inventoryPanel.GetComponent<RectTransform>(), new Vector2(0f, 0f), new Vector2(0f, 0f),
                new Vector2(80f, 80f), new Vector2(640f, 232f));
            CreateText(inventoryPanel.transform, "Inventory Label", "INVENTORY", 16f, new Color(0.7f, 0.85f, 1f),
                TextAlignmentOptions.Left, new Vector2(0f, 1f), new Vector2(1f, 1f),
                new Vector2(14f, -30f), new Vector2(-14f, -6f));
            GameObject inventoryContainer = CreatePanel(inventoryPanel.transform, "Inventory Container", new Color(0f, 0f, 0f, 0f));
            SetStretch(inventoryContainer.GetComponent<RectTransform>(), 12f, 12f, 10f, 36f);
            HorizontalLayoutGroup inventoryLayout = inventoryContainer.AddComponent<HorizontalLayoutGroup>();
            inventoryLayout.spacing = 8f;
            inventoryLayout.childControlWidth = false;
            inventoryLayout.childControlHeight = false;
            inventoryLayout.childForceExpandWidth = false;
            inventoryLayout.childForceExpandHeight = false;

            GameObject detailPanel = CreatePanel(canvas, "Inventory Detail Panel", new Color(0.03f, 0.045f, 0.07f, 0.97f));
            SetAnchors(detailPanel.GetComponent<RectTransform>(), new Vector2(1f, 0f), new Vector2(1f, 0f),
                new Vector2(-400f, 80f), new Vector2(-40f, 300f));
            Image detailIcon = CreateImage(detailPanel.transform, "Detail Icon", new Color(0.15f, 0.18f, 0.26f, 1f),
                new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(16f, -100f), new Vector2(96f, -20f));
            TMP_Text detailName = CreateText(detailPanel.transform, "Detail Name", string.Empty, 22f, Color.white,
                TextAlignmentOptions.Left, new Vector2(0f, 1f), new Vector2(1f, 1f),
                new Vector2(108f, -64f), new Vector2(-16f, -18f));
            TMP_Text detailDescription = CreateText(detailPanel.transform, "Detail Description", string.Empty, 16f, new Color(0.82f, 0.85f, 0.9f),
                TextAlignmentOptions.TopLeft, new Vector2(0f, 0f), new Vector2(1f, 1f),
                new Vector2(16f, 16f), new Vector2(-16f, -108f));

            GameObject promptPanel = FindSceneObject(SceneManager.GetActiveScene(), "Interaction Prompt Panel");
            TMP_Text promptText = FindSceneObject(SceneManager.GetActiveScene(), "Interaction Prompt")?.GetComponent<TMP_Text>();

            SerializedObject so = new SerializedObject(hud);
            SetObject(so, "moneyText", moneyText);
            SetObject(so, "objectiveText", objectiveText);
            SetObject(so, "objectivePanel", objectivePanel);
            SetObject(so, "suspicionSlider", suspicionSlider);
            SetObject(so, "suspicionFill", suspicionFill);
            SetObject(so, "minimapImage", minimapImage);
            SetObject(so, "inventoryUI", inventory);
            SetObject(so, "interactionPromptPanel", promptPanel);
            SetObject(so, "interactionPromptText", promptText);
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(hud);

            SerializedObject invSo = new SerializedObject(inventory);
            SetObject(invSo, "inventoryContainer", inventoryContainer.transform);
            SetObject(invSo, "inventorySlotPrefab", inventorySlotPrefab);
            SetObject(invSo, "detailPanel", detailPanel);
            SetObject(invSo, "detailIcon", detailIcon);
            SetObject(invSo, "detailName", detailName);
            SetObject(invSo, "detailDescription", detailDescription);
            invSo.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(inventory);

            return hudPanel;
        }

        private static GameObject BuildPausePanel(Transform canvas, out PauseMenuController controller)
        {
            GameObject root = new GameObject("[PauseMenu]");
            SceneManager.MoveGameObjectToScene(root, SceneManager.GetActiveScene());
            controller = root.AddComponent<PauseMenuController>();

            GameObject panel = CreatePanel(canvas, "Pause Panel", new Color(0.01f, 0.015f, 0.025f, 0.82f));
            SetStretch(panel.GetComponent<RectTransform>(), 0f, 0f, 0f, 0f);
            panel.GetComponent<Image>().raycastTarget = true;

            GameObject content = CreatePanel(panel.transform, "Pause Content", new Color(0.03f, 0.045f, 0.07f, 0.98f));
            SetAnchors(content.GetComponent<RectTransform>(), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(-260f, -280f), new Vector2(260f, 280f));
            VerticalLayoutGroup layout = content.AddComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(28, 28, 28, 28);
            layout.spacing = 12f;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;

            CreateLayoutText(content.transform, "Pause Title", "PAUSED", 36f, new Color(1f, 0.82f, 0.35f), 70f);
            CreateLayoutButton(content.transform, "Resume Button", "RESUME", new Color(0.18f, 0.5f, 0.26f, 1f), 58f);
            CreateLayoutButton(content.transform, "Settings Button", "SETTINGS", new Color(0.18f, 0.32f, 0.55f, 1f), 58f);
            CreateLayoutButton(content.transform, "Main Menu Button", "MAIN MENU", new Color(0.32f, 0.34f, 0.4f, 1f), 58f);
            CreateLayoutButton(content.transform, "Quit Button", "QUIT", new Color(0.55f, 0.16f, 0.18f, 1f), 58f);

            return panel;
        }

        private static GameObject BuildSettingsPanel(Transform canvas, string name, SettingsController controller)
        {
            GameObject panel = CreatePanel(canvas, name, new Color(0.02f, 0.03f, 0.045f, 0.97f));
            SetAnchors(panel.GetComponent<RectTransform>(), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(-480f, -380f), new Vector2(480f, 380f));

            CreateText(panel.transform, "Settings Title", "SETTINGS", 34f, new Color(1f, 0.82f, 0.35f),
                TextAlignmentOptions.Left, new Vector2(0f, 1f), new Vector2(1f, 1f),
                new Vector2(32f, -74f), new Vector2(-32f, -24f));

            Slider master = CreateLabeledSlider(panel, "Master Volume", 82f, out TMP_Text masterText);
            Slider music = CreateLabeledSlider(panel, "Music Volume", 138f, out TMP_Text musicText);
            Slider sfx = CreateLabeledSlider(panel, "SFX Volume", 194f, out TMP_Text sfxText);

            CreateText(panel.transform, "Quality Label", "GRAPHICS QUALITY", 18f, new Color(0.82f, 0.85f, 0.9f),
                TextAlignmentOptions.Left, new Vector2(0f, 1f), new Vector2(0f, 1f),
                new Vector2(32f, -286f), new Vector2(310f, -252f));
            TMP_Dropdown quality = CreateDropdown(panel.transform, "Quality Dropdown",
                new Vector2(340f, -292f), new Vector2(-32f, -252f));
            quality.ClearOptions();
            quality.AddOptions(new List<string> { "Low", "Medium", "High", "Ultra" });

            CreateText(panel.transform, "VSync Label", "V-SYNC", 18f, new Color(0.82f, 0.85f, 0.9f),
                TextAlignmentOptions.Left, new Vector2(0f, 1f), new Vector2(0f, 1f),
                new Vector2(32f, -346f), new Vector2(310f, -312f));
            Toggle vsync = CreateToggle(panel.transform, "VSync Toggle", new Vector2(340f, -350f), new Vector2(380f, -310f));

            CreateText(panel.transform, "Resolution Label", "RESOLUTION", 18f, new Color(0.82f, 0.85f, 0.9f),
                TextAlignmentOptions.Left, new Vector2(0f, 1f), new Vector2(0f, 1f),
                new Vector2(32f, -406f), new Vector2(310f, -372f));
            TMP_Dropdown resolution = CreateDropdown(panel.transform, "Resolution Dropdown",
                new Vector2(340f, -412f), new Vector2(-32f, -372f));
            resolution.ClearOptions();
            resolution.AddOptions(new List<string> { "1280 x 720", "1600 x 900", "1920 x 1080", "2560 x 1440" });

            Slider mouse = CreateLabeledSlider(panel, "Mouse Sensitivity", 442f, out TMP_Text mouseText);
            mouse.minValue = 0.1f;
            mouse.maxValue = 5f;
            Slider gamepad = CreateLabeledSlider(panel, "Gamepad Sensitivity", 498f, out TMP_Text gamepadText);
            gamepad.minValue = 0.1f;
            gamepad.maxValue = 5f;

            Button defaults = CreateButton(panel.transform, "Defaults Button", "DEFAULTS", new Color(0.32f, 0.34f, 0.4f, 1f),
                new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(32f, 28f), new Vector2(232f, 78f));
            Button apply = CreateButton(panel.transform, "Apply Button", "APPLY", new Color(0.18f, 0.5f, 0.26f, 1f),
                new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(-100f, 28f), new Vector2(100f, 78f));
            Button back = CreateButton(panel.transform, "Back Button", "BACK", new Color(0.55f, 0.16f, 0.18f, 1f),
                new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-232f, 28f), new Vector2(-32f, 78f));

            SerializedObject so = new SerializedObject(controller);
            SetObject(so, "masterVolumeSlider", master);
            SetObject(so, "musicVolumeSlider", music);
            SetObject(so, "sfxVolumeSlider", sfx);
            SetObject(so, "masterVolumeText", masterText);
            SetObject(so, "musicVolumeText", musicText);
            SetObject(so, "sfxVolumeText", sfxText);
            SetObject(so, "qualityDropdown", quality);
            SetObject(so, "vSyncToggle", vsync);
            SetObject(so, "resolutionDropdown", resolution);
            SetObject(so, "mouseSensitivitySlider", mouse);
            SetObject(so, "gamepadSensitivitySlider", gamepad);
            SetObject(so, "mouseSensitivityText", mouseText);
            SetObject(so, "gamepadSensitivityText", gamepadText);
            SetObject(so, "backButton", back);
            SetObject(so, "applyButton", apply);
            SetObject(so, "defaultsButton", defaults);
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(controller);

            panel.SetActive(false);
            return panel;
        }

        private static GameObject BuildCreditsPanel(Transform canvas, string name, MainMenuController menu)
        {
            GameObject panel = CreatePanel(canvas, name, new Color(0.02f, 0.03f, 0.045f, 0.97f));
            SetAnchors(panel.GetComponent<RectTransform>(), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(-420f, -300f), new Vector2(420f, 300f));

            CreateText(panel.transform, "Credits Title", "CREDITS", 36f, new Color(1f, 0.82f, 0.35f),
                TextAlignmentOptions.Center, new Vector2(0f, 1f), new Vector2(1f, 1f),
                new Vector2(32f, -92f), new Vector2(-32f, -28f));
            CreateText(panel.transform, "Credits Text",
                "CONTRACT\n\nA stealth action prototype.\n\nDesign & Engineering\nContract Game Team\n\nBuilt with Unity",
                22f, new Color(0.82f, 0.85f, 0.9f), TextAlignmentOptions.Center,
                new Vector2(0f, 0f), new Vector2(1f, 1f),
                new Vector2(40f, 110f), new Vector2(-40f, -110f));
            Button back = CreateButton(panel.transform, "Back Button", "BACK", new Color(0.55f, 0.16f, 0.18f, 1f),
                new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(-120f, 28f), new Vector2(120f, 78f));
            UnityEventTools.AddPersistentListener(back.onClick, menu.ReturnToMain);

            panel.SetActive(false);
            return panel;
        }

        private static GameObject BuildRegionTabPrefab()
        {
            GameObject root = new GameObject("RegionTab", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
            RectTransform rect = root.GetComponent<RectTransform>();
            rect.sizeDelta = new Vector2(150f, 44f);
            root.AddComponent<LayoutElement>().preferredWidth = 150f;

            Image image = root.GetComponent<Image>();
            image.color = new Color(0.12f, 0.2f, 0.35f, 0.98f);
            Button button = root.GetComponent<Button>();
            button.targetGraphic = image;
            button.transition = Selectable.Transition.ColorTint;

            CreateText(root.transform, "Label", "REGION", 18f, Color.white,
                TextAlignmentOptions.Center, Vector2.zero, Vector2.one, new Vector2(6f, 2f), new Vector2(-6f, -2f));

            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, PrefabFolder + "/RegionTab.prefab");
            Object.DestroyImmediate(root);
            return prefab;
        }

        private static GameObject BuildMissionItemPrefab()
        {
            GameObject root = new GameObject("MissionItem", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
            RectTransform rect = root.GetComponent<RectTransform>();
            rect.sizeDelta = new Vector2(0f, 66f);
            root.AddComponent<LayoutElement>().preferredHeight = 66f;

            Image background = root.GetComponent<Image>();
            background.color = new Color(0.09f, 0.11f, 0.16f, 0.98f);
            Button button = root.GetComponent<Button>();
            button.targetGraphic = background;
            button.transition = Selectable.Transition.ColorTint;

            TMP_Text name = CreateText(root.transform, "Name", "Mission", 21f, Color.white,
                TextAlignmentOptions.Left, new Vector2(0f, 0f), new Vector2(0.7f, 1f),
                new Vector2(20f, 0f), new Vector2(-4f, 0f));
            TMP_Text reward = CreateText(root.transform, "Reward", "$0", 18f, new Color(0.4f, 1f, 0.6f),
                TextAlignmentOptions.Right, new Vector2(0.7f, 0f), new Vector2(1f, 1f),
                new Vector2(4f, 0f), new Vector2(-20f, 0f));
            Image icon = CreateImage(root.transform, "Completion Icon", new Color(0.2f, 0.45f, 0.9f, 1f),
                new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(8f, -12f), new Vector2(32f, 12f));

            MissionItemUI item = root.AddComponent<MissionItemUI>();
            SerializedObject so = new SerializedObject(item);
            SetObject(so, "nameText", name);
            SetObject(so, "rewardText", reward);
            SetObject(so, "completionIcon", icon);
            SetObject(so, "button", button);
            so.ApplyModifiedPropertiesWithoutUndo();

            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, PrefabFolder + "/MissionItem.prefab");
            Object.DestroyImmediate(root);
            return prefab;
        }

        private static GameObject BuildInventorySlotPrefab()
        {
            GameObject root = new GameObject("InventorySlot", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
            RectTransform rect = root.GetComponent<RectTransform>();
            rect.sizeDelta = new Vector2(64f, 64f);
            root.AddComponent<LayoutElement>().preferredWidth = 64f;
            root.AddComponent<LayoutElement>().preferredHeight = 64f;

            Image background = root.GetComponent<Image>();
            background.color = new Color(0.1f, 0.12f, 0.17f, 0.98f);
            Button button = root.GetComponent<Button>();
            button.targetGraphic = background;
            button.transition = Selectable.Transition.ColorTint;

            Image icon = CreateImage(root.transform, "Icon", new Color(0.2f, 0.24f, 0.32f, 1f),
                new Vector2(0f, 0f), new Vector2(1f, 1f), new Vector2(6f, 6f), new Vector2(-6f, -6f));
            TMP_Text count = CreateText(root.transform, "Count", "1", 15f, Color.white,
                TextAlignmentOptions.BottomRight, new Vector2(0f, 0f), new Vector2(1f, 1f),
                new Vector2(2f, 2f), new Vector2(-5f, -5f));
            Image evidence = CreateImage(root.transform, "Evidence Badge", new Color(0.95f, 0.75f, 0.25f, 1f),
                new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(4f, -12f), new Vector2(12f, -4f));

            InventorySlotUI slot = root.AddComponent<InventorySlotUI>();
            SerializedObject so = new SerializedObject(slot);
            SetObject(so, "iconImage", icon);
            SetObject(so, "countText", count);
            SetObject(so, "button", button);
            SetObject(so, "evidenceBadge", evidence);
            so.ApplyModifiedPropertiesWithoutUndo();

            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, PrefabFolder + "/InventorySlot.prefab");
            Object.DestroyImmediate(root);
            return prefab;
        }

        private static void BuildStandalonePrefabs()
        {
            GameObject hudRoot = new GameObject("HUDCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            Canvas hudCanvas = hudRoot.GetComponent<Canvas>();
            hudCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
            CanvasScaler hudScaler = hudRoot.GetComponent<CanvasScaler>();
            hudScaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            hudScaler.referenceResolution = new Vector2(1920f, 1080f);
            GameObject hudPanel = CreatePanel(hudRoot.transform, "HUD Panel", new Color(0f, 0f, 0f, 0f));
            SetStretch(hudPanel.GetComponent<RectTransform>(), 0f, 0f, 0f, 0f);
            CreateText(hudPanel.transform, "Placeholder", "HUD", 24f, Color.white,
                TextAlignmentOptions.TopLeft, new Vector2(0f, 1f), new Vector2(1f, 1f),
                new Vector2(20f, -60f), new Vector2(-20f, -20f));
            PrefabUtility.SaveAsPrefabAsset(hudRoot, PrefabFolder + "/HUDCanvas.prefab");
            Object.DestroyImmediate(hudRoot);

            GameObject minimapRoot = new GameObject("MinimapCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            Canvas minimapCanvas = minimapRoot.GetComponent<Canvas>();
            minimapCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
            CanvasScaler minimapScaler = minimapRoot.GetComponent<CanvasScaler>();
            minimapScaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            minimapScaler.referenceResolution = new Vector2(1920f, 1080f);
            GameObject minimapPanel = CreatePanel(minimapRoot.transform, "Minimap Panel", new Color(0.02f, 0.03f, 0.045f, 0.9f));
            SetAnchors(minimapPanel.GetComponent<RectTransform>(), new Vector2(1f, 1f), new Vector2(1f, 1f),
                new Vector2(-300f, -280f), new Vector2(-40f, -20f));
            GameObject imageObject = new GameObject("Minimap Image", typeof(RectTransform), typeof(CanvasRenderer), typeof(RawImage));
            imageObject.transform.SetParent(minimapPanel.transform, false);
            SetStretch(imageObject.GetComponent<RectTransform>(), 10f, 10f, 10f, 10f);
            PrefabUtility.SaveAsPrefabAsset(minimapRoot, PrefabFolder + "/MinimapCanvas.prefab");
            Object.DestroyImmediate(minimapRoot);
        }

        private static Slider CreateLabeledSlider(GameObject panel, string label, float top, out TMP_Text valueText)
        {
            CreateText(panel.transform, label + " Label", label.ToUpperInvariant(), 18f, new Color(0.82f, 0.85f, 0.9f),
                TextAlignmentOptions.Left, new Vector2(0f, 1f), new Vector2(0f, 1f),
                new Vector2(32f, -top - 34f), new Vector2(310f, -top));
            valueText = CreateText(panel.transform, label + " Value", "100%", 17f, new Color(0.65f, 0.8f, 1f),
                TextAlignmentOptions.Right, new Vector2(0f, 1f), new Vector2(0f, 1f),
                new Vector2(-190f, -top - 34f), new Vector2(-32f, -top));
            return CreateSlider(panel.transform, label + " Slider",
                new Vector2(0f, 1f), new Vector2(1f, 1f),
                new Vector2(340f, -top - 34f), new Vector2(-210f, -top));
        }

        private static Slider CreateSlider(Transform parent, string name, Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax)
        {
            GameObject root = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Slider));
            root.transform.SetParent(parent, false);
            Image background = root.GetComponent<Image>();
            background.color = new Color(0.09f, 0.1f, 0.14f, 1f);
            SetAnchors(root.GetComponent<RectTransform>(), anchorMin, anchorMax, offsetMin, offsetMax);

            GameObject fillArea = new GameObject("Fill Area", typeof(RectTransform));
            fillArea.transform.SetParent(root.transform, false);
            SetAnchors(fillArea.GetComponent<RectTransform>(), new Vector2(0f, 0.25f), new Vector2(1f, 0.75f), new Vector2(6f, 0f), new Vector2(-14f, 0f));
            GameObject fill = new GameObject("Fill", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            fill.transform.SetParent(fillArea.transform, false);
            Image fillImage = fill.GetComponent<Image>();
            fillImage.color = new Color(0.25f, 0.65f, 0.95f, 1f);
            fillImage.raycastTarget = false;
            SetStretch(fill.GetComponent<RectTransform>(), 0f, 0f, 0f, 0f);

            GameObject handleArea = new GameObject("Handle Slide Area", typeof(RectTransform));
            handleArea.transform.SetParent(root.transform, false);
            SetStretch(handleArea.GetComponent<RectTransform>(), 8f, 8f, 0f, 0f);
            GameObject handle = new GameObject("Handle", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            handle.transform.SetParent(handleArea.transform, false);
            Image handleImage = handle.GetComponent<Image>();
            handleImage.color = new Color(0.9f, 0.95f, 1f, 1f);
            RectTransform handleRect = handle.GetComponent<RectTransform>();
            handleRect.sizeDelta = new Vector2(18f, 26f);

            Slider slider = root.GetComponent<Slider>();
            slider.fillRect = fill.GetComponent<RectTransform>();
            slider.handleRect = handleRect;
            slider.targetGraphic = handleImage;
            slider.direction = Slider.Direction.LeftToRight;
            slider.minValue = 0f;
            slider.maxValue = 1f;
            slider.value = 1f;
            return slider;
        }

        private static Toggle CreateToggle(Transform parent, string name, Vector2 offsetMin, Vector2 offsetMax)
        {
            GameObject root = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Toggle));
            root.transform.SetParent(parent, false);
            Image background = root.GetComponent<Image>();
            background.color = new Color(0.09f, 0.1f, 0.14f, 1f);
            SetAnchors(root.GetComponent<RectTransform>(), new Vector2(0f, 1f), new Vector2(0f, 1f), offsetMin, offsetMax);

            GameObject check = new GameObject("Checkmark", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            check.transform.SetParent(root.transform, false);
            Image checkImage = check.GetComponent<Image>();
            checkImage.color = new Color(0.3f, 0.75f, 0.4f, 1f);
            SetStretch(check.GetComponent<RectTransform>(), 5f, 5f, 5f, 5f);

            Toggle toggle = root.GetComponent<Toggle>();
            toggle.targetGraphic = background;
            toggle.graphic = checkImage;
            return toggle;
        }

        private static TMP_Dropdown CreateDropdown(Transform parent, string name, Vector2 offsetMin, Vector2 offsetMax)
        {
            GameObject root = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(TMP_Dropdown));
            root.transform.SetParent(parent, false);
            root.GetComponent<Image>().color = new Color(0.09f, 0.1f, 0.14f, 1f);
            SetAnchors(root.GetComponent<RectTransform>(), new Vector2(0f, 1f), new Vector2(1f, 1f), offsetMin, offsetMax);

            TMP_Text caption = CreateText(root.transform, "Caption", "OPTION", 17f, Color.white,
                TextAlignmentOptions.Left, Vector2.zero, Vector2.one, new Vector2(12f, 2f), new Vector2(-12f, -2f));
            TMP_Dropdown dropdown = root.GetComponent<TMP_Dropdown>();
            dropdown.captionText = caption;
            return dropdown;
        }

        private static Button CreateLayoutButton(Transform parent, string name, string label, Color color, float height)
        {
            Button button = CreateButton(parent, name, label, color,
                new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, -height), new Vector2(0f, 0f));
            button.gameObject.AddComponent<LayoutElement>().preferredHeight = height;
            return button;
        }

        private static TMP_Text CreateLayoutText(Transform parent, string name, string label, float fontSize, Color color, float height)
        {
            TMP_Text text = CreateText(parent, name, label, fontSize, color,
                TextAlignmentOptions.Center, new Vector2(0f, 1f), new Vector2(1f, 1f),
                new Vector2(0f, -height), new Vector2(0f, 0f));
            text.gameObject.AddComponent<LayoutElement>().preferredHeight = height;
            return text;
        }

        private static Button CreateButton(Transform parent, string name, string label, Color color,
            Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax)
        {
            GameObject gameObject = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
            gameObject.transform.SetParent(parent, false);
            Image image = gameObject.GetComponent<Image>();
            image.color = color;
            Button button = gameObject.GetComponent<Button>();
            button.targetGraphic = image;
            button.transition = Selectable.Transition.ColorTint;
            SetAnchors(button.GetComponent<RectTransform>(), anchorMin, anchorMax, offsetMin, offsetMax);

            TMP_Text text = CreateText(gameObject.transform, "Label", label, 20f, Color.white,
                TextAlignmentOptions.Center, Vector2.zero, Vector2.one, new Vector2(6f, 4f), new Vector2(-6f, -4f));
            text.raycastTarget = false;
            return button;
        }

        private static GameObject CreatePanel(Transform parent, string name, Color color)
        {
            GameObject gameObject = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            gameObject.transform.SetParent(parent, false);
            Image image = gameObject.GetComponent<Image>();
            image.color = color;
            return gameObject;
        }

        private static Image CreateImage(Transform parent, string name, Color color, Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax)
        {
            GameObject gameObject = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            gameObject.transform.SetParent(parent, false);
            Image image = gameObject.GetComponent<Image>();
            image.color = color;
            image.raycastTarget = false;
            SetAnchors(image.rectTransform, anchorMin, anchorMax, offsetMin, offsetMax);
            return image;
        }

        private static TMP_Text CreateText(Transform parent, string name, string value, float fontSize, Color color,
            TextAlignmentOptions alignment, Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax)
        {
            GameObject gameObject = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer));
            gameObject.transform.SetParent(parent, false);
            TextMeshProUGUI text = gameObject.AddComponent<TextMeshProUGUI>();
            text.text = value;
            text.fontSize = fontSize;
            text.color = color;
            text.alignment = alignment;
            text.raycastTarget = false;
            if (TMP_Settings.defaultFontAsset != null)
            {
                text.font = TMP_Settings.defaultFontAsset;
            }

            SetAnchors(text.rectTransform, anchorMin, anchorMax, offsetMin, offsetMax);
            return text;
        }

        private static void SetAnchors(RectTransform rect, Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax)
        {
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.offsetMin = offsetMin;
            rect.offsetMax = offsetMax;
        }

        private static void SetStretch(RectTransform rect, float left, float right, float bottom, float top)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(left, bottom);
            rect.offsetMax = new Vector2(-right, -top);
        }

        private static void SetObject(SerializedObject serialized, string propertyName, Object value)
        {
            SerializedProperty property = serialized.FindProperty(propertyName);
            if (property == null)
            {
                Debug.LogWarning("[CONTRACT] Serialized property not found: " + propertyName);
                return;
            }

            property.objectReferenceValue = value;
        }

        private static void SetArray<T>(SerializedObject serialized, string propertyName, T[] values) where T : Object
        {
            SerializedProperty property = serialized.FindProperty(propertyName);
            if (property == null)
            {
                Debug.LogWarning("[CONTRACT] Serialized array not found: " + propertyName);
                return;
            }

            property.arraySize = values.Length;
            for (int i = 0; i < values.Length; i++)
            {
                property.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
            }
        }

        private static void AssignAssets<T>(Component manager, string propertyName, string folder) where T : Object
        {
            string[] guids = AssetDatabase.FindAssets("t:" + typeof(T).Name, new[] { folder });
            T[] assets = guids
                .Select(guid => AssetDatabase.LoadAssetAtPath<T>(AssetDatabase.GUIDToAssetPath(guid)))
                .Where(asset => asset != null)
                .OrderBy(asset => asset.name, StringComparer.Ordinal)
                .ToArray();

            SerializedObject serialized = new SerializedObject(manager);
            SetArray(serialized, propertyName, assets);
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(manager);
        }

        private static T CreateManager<T>(Scene scene, string name) where T : Component
        {
            GameObject gameObject = CreateRoot(scene, name);
            return gameObject.AddComponent<T>();
        }

        private static GameObject CreateRoot(Scene scene, string name)
        {
            GameObject gameObject = new GameObject(name);
            SceneManager.MoveGameObjectToScene(gameObject, scene);
            return gameObject;
        }

        private static Transform CreateWaypoint(Transform parent, string name, Vector3 position)
        {
            GameObject gameObject = new GameObject(name);
            gameObject.transform.SetParent(parent, false);
            gameObject.transform.position = position;
            return gameObject.transform;
        }

        private static Canvas FindCanvas()
        {
            return Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None)
                .FirstOrDefault(canvas => canvas.renderMode == RenderMode.ScreenSpaceOverlay);
        }

        private static Camera FindMainCamera()
        {
            return Object.FindObjectsByType<Camera>(FindObjectsSortMode.None)
                .FirstOrDefault(camera => camera.CompareTag("MainCamera")) ?? Camera.main;
        }

        private static GameObject FindSceneObject(Scene scene, string name)
        {
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                foreach (Transform transform in root.GetComponentsInChildren<Transform>(true))
                {
                    if (transform.name == name)
                    {
                        return transform.gameObject;
                    }
                }
            }

            return null;
        }

        private static void DestroyRoot(Scene scene, string name)
        {
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                if (root.name == name)
                {
                    Object.DestroyImmediate(root);
                    return;
                }
            }
        }

        private static void DestroyByName(Scene scene, string name)
        {
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                foreach (Transform transform in root.GetComponentsInChildren<Transform>(true))
                {
                    if (transform.name == name)
                    {
                        Object.DestroyImmediate(transform.gameObject);
                        return;
                    }
                }
            }
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path))
            {
                return;
            }

            string parent = Path.GetDirectoryName(path)?.Replace('\\', '/');
            string name = Path.GetFileName(path);
            if (!string.IsNullOrEmpty(parent) && !string.IsNullOrEmpty(name))
            {
                if (!AssetDatabase.IsValidFolder(parent))
                {
                    EnsureFolder(parent);
                }

                AssetDatabase.CreateFolder(parent, name);
            }
        }
    }
}
