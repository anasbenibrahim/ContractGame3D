using System.Reflection;
using Contract.Core;
using Contract.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace Contract.EditorTools
{
    public static class ContractStage7SmokeTest
    {
        private const double PlayDuration = 2.0;
        private const double StartupTimeout = 20.0;

        private static double startedAt;
        private static double playStartedAt;
        private static bool playStarted;
        private static bool finished;
        private static bool passedSmokeTest;

        [MenuItem("CONTRACT/Validate/Stage 7 Play Mode Smoke Test")]
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
            UIManager uiManager = UIManager.Instance;
            HUDController hud = Object.FindAnyObjectByType<HUDController>();
            InventoryUI inventory = Object.FindAnyObjectByType<InventoryUI>();
            PauseMenuController pause = Object.FindAnyObjectByType<PauseMenuController>();
            Contract.Map.FullMapView fullMap = Object.FindAnyObjectByType<Contract.Map.FullMapView>();

            passed &= Check(gameManager != null, "GameManager was not found.");
            passed &= Check(uiManager != null, "UIManager was not found.");
            passed &= Check(hud != null, "HUDController was not found.");
            passed &= Check(inventory != null, "InventoryUI was not found.");
            passed &= Check(pause != null, "PauseMenuController was not found.");
            passed &= Check(fullMap != null, "FullMapUI was not found.");

            if (gameManager != null)
            {
                passed &= Check(gameManager.GetService<IUIService>() != null, "IUIService was not registered.");
            }

            if (hud != null)
            {
                GameObject hudPanel = GetField<GameObject>(hud, "moneyText")?.transform?.root?.gameObject;
                passed &= Check(hudPanel == null || hudPanel.activeInHierarchy, "HUD is not active.");
                RawImage minimap = GetField<RawImage>(hud, "minimapImage");
                passed &= Check(minimap != null && minimap.texture != null, "Minimap texture was not assigned.");
            }

            if (pause != null)
            {
                GameObject pausePanel = GetField<GameObject>(pause, "pausePanel");
                pause.OnPause();
                passed &= Check(pausePanel != null && pausePanel.activeSelf, "Pause panel did not open.");
                pause.OnResume();
                passed &= Check(pausePanel != null && !pausePanel.activeSelf, "Pause panel did not close.");
            }

            if (uiManager != null)
            {
                uiManager.ShowPanel("settings");
                GameObject settingsPanel = GetField<GameObject>(uiManager, "settingsPanel");
                passed &= Check(settingsPanel != null && settingsPanel.activeSelf, "Settings panel did not open.");
                uiManager.HidePanel("settings");
                passed &= Check(settingsPanel != null && !settingsPanel.activeSelf, "Settings panel did not close.");
            }

            if (fullMap != null)
            {
                GameObject mapPanel = GetField<GameObject>(fullMap, "fullMapPanel");
                GameEvents.RaiseMapOpened("full");
                passed &= Check(mapPanel != null && mapPanel.activeSelf, "Full map panel did not open.");
                GameEvents.RaiseMapClosed("full");
                passed &= Check(mapPanel != null && !mapPanel.activeSelf, "Full map panel did not close.");
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
                Debug.Log($"[CONTRACT-STAGE7-SMOKE] PASSED after {elapsed:0.0}s.");
            }
            else
            {
                Debug.LogError("[CONTRACT-STAGE7-SMOKE] FAILED.");
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
            if (!condition) Debug.LogError($"[CONTRACT-STAGE7-SMOKE] {failureMessage}");
            return condition;
        }

        private static void Fail(string message)
        {
            finished = true;
            EditorApplication.update -= Tick;
            EditorApplication.playModeStateChanged -= HandlePlayModeChanged;
            Debug.LogError($"[CONTRACT-STAGE7-SMOKE] {message}");
            EditorApplication.Exit(1);
        }
    }
}
