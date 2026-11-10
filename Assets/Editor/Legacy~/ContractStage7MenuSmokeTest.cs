using System.Reflection;
using Contract.Character;
using Contract.Missions;
using Contract.Regions;
using Contract.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Contract.EditorTools
{
    public static class ContractStage7MenuSmokeTest
    {
        private enum MenuKind
        {
            MainMenu,
            CharacterSelection,
            MissionSelection
        }

        private const double PlayDuration = 1.5;
        private const double StartupTimeout = 20.0;

        private static MenuKind kind;
        private static double startedAt;
        private static double playStartedAt;
        private static bool playStarted;
        private static bool finished;
        private static bool passedSmokeTest;

        [MenuItem("CONTRACT/Validate/Stage 7 Main Menu Smoke Test")]
        public static void RunMainMenu() => Run(MenuKind.MainMenu);

        [MenuItem("CONTRACT/Validate/Stage 7 Character Selection Smoke Test")]
        public static void RunCharacterSelection() => Run(MenuKind.CharacterSelection);

        [MenuItem("CONTRACT/Validate/Stage 7 Mission Selection Smoke Test")]
        public static void RunMissionSelection() => Run(MenuKind.MissionSelection);

        private static void Run(MenuKind menuKind)
        {
            kind = menuKind;
            string path = menuKind switch
            {
                MenuKind.MainMenu => "Assets/Scenes/MainMenu.unity",
                MenuKind.CharacterSelection => "Assets/Scenes/CharacterSelection.unity",
                _ => "Assets/Scenes/MissionSelection.unity"
            };

            EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
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
            switch (kind)
            {
                case MenuKind.MainMenu:
                    MainMenuController menu = Object.FindAnyObjectByType<MainMenuController>();
                    SettingsController settings = Object.FindAnyObjectByType<SettingsController>();
                    CinematicCameraController cinematic = Object.FindAnyObjectByType<CinematicCameraController>();
                    passed &= Check(menu != null, "MainMenuController was not found.");
                    passed &= Check(settings != null, "SettingsController was not found.");
                    passed &= Check(cinematic != null, "CinematicCameraController was not found.");
                    if (menu != null)
                    {
                        GameObject mainPanel = GetField<GameObject>(menu, "mainPanel");
                        GameObject settingsPanel = GetField<GameObject>(menu, "settingsPanel");
                        GameObject creditsPanel = GetField<GameObject>(menu, "creditsPanel");
                        passed &= Check(mainPanel != null && mainPanel.activeSelf, "Main menu panel is not active.");
                        passed &= Check(settingsPanel != null && !settingsPanel.activeSelf, "Settings panel should start hidden.");
                        passed &= Check(creditsPanel != null && !creditsPanel.activeSelf, "Credits panel should start hidden.");
                    }
                    break;

                case MenuKind.CharacterSelection:
                    CharacterSelectionController characters = Object.FindAnyObjectByType<CharacterSelectionController>();
                    CharacterSelectionManager characterManager = CharacterSelectionManager.Instance;
                    passed &= Check(characters != null, "CharacterSelectionController was not found.");
                    passed &= Check(characterManager != null, "CharacterSelectionManager was not found.");
                    if (characters != null)
                    {
                        CharacterSlotUI[] slots = GetField<CharacterSlotUI[]>(characters, "characterSlots");
                        GameObject details = GetField<GameObject>(characters, "detailsPanel");
                        passed &= Check(slots != null && slots.Length >= 3, "Character slots were not configured.");
                        passed &= Check(details != null && !details.activeSelf, "Character details should start hidden.");
                    }
                    break;

                case MenuKind.MissionSelection:
                    MissionSelectionController missions = Object.FindAnyObjectByType<MissionSelectionController>();
                    MissionManager missionManager = MissionManager.Instance;
                    RegionManager regionManager = RegionManager.Instance;
                    passed &= Check(missions != null, "MissionSelectionController was not found.");
                    passed &= Check(missionManager != null, "MissionManager was not found.");
                    passed &= Check(regionManager != null, "RegionManager was not found.");
                    if (missions != null)
                    {
                        Transform tabs = GetField<Transform>(missions, "regionTabContainer");
                        GameObject details = GetField<GameObject>(missions, "detailsPanel");
                        passed &= Check(tabs != null && tabs.childCount > 0, "Region tabs were not generated.");
                        passed &= Check(details != null && !details.activeSelf, "Mission details should start hidden.");
                    }
                    break;
            }

            Finish(passed, now);
        }

        private static T GetField<T>(object target, string fieldName) where T : class
        {
            FieldInfo field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            return field?.GetValue(target) as T;
        }

        private static void Finish(bool passed, double now)
        {
            double elapsed = now - playStartedAt;
            if (passed)
            {
                Debug.Log($"[CONTRACT-STAGE7-MENU-SMOKE] {kind} PASSED after {elapsed:0.0}s.");
            }
            else
            {
                Debug.LogError($"[CONTRACT-STAGE7-MENU-SMOKE] {kind} FAILED.");
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
            if (!condition) Debug.LogError($"[CONTRACT-STAGE7-MENU-SMOKE] {failureMessage}");
            return condition;
        }

        private static void Fail(string message)
        {
            finished = true;
            EditorApplication.update -= Tick;
            EditorApplication.playModeStateChanged -= HandlePlayModeChanged;
            Debug.LogError($"[CONTRACT-STAGE7-MENU-SMOKE] {message}");
            EditorApplication.Exit(1);
        }
    }
}
