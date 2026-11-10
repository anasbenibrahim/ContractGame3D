using System.Reflection;
using Contract.Accessibility;
using Contract.Core;
using Contract.Localization;
using Contract.Performance;
using Contract.Player;
using Contract.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace Contract.EditorTools
{
    public static class ContractStage8SmokeTest
    {
        private const double PlayDuration = 2.0;
        private const double StartupTimeout = 20.0;

        private static double startedAt;
        private static double playStartedAt;
        private static bool playStarted;
        private static bool finished;
        private static bool passedSmokeTest;

        [MenuItem("CONTRACT/Validate/Stage 8 Play Mode Smoke Test")]
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
            UITransitionManager transition = UITransitionManager.Instance;
            AccessibilityManager accessibility = AccessibilityManager.Instance;
            PerformanceManager performance = PerformanceManager.Instance;
            LocalizationManager localization = LocalizationManager.Instance;
            GameObject player = GameObject.FindGameObjectWithTag("Player");
            PlayerAnimationDriver animation = player != null ? player.GetComponent<PlayerAnimationDriver>() : null;
            AnimationEventReceiver receiver = player != null ? player.GetComponent<AnimationEventReceiver>() : null;

            passed &= Check(transition != null, "UITransitionManager was not found.");
            passed &= Check(accessibility != null, "AccessibilityManager was not found.");
            passed &= Check(performance != null, "PerformanceManager was not found.");
            passed &= Check(localization != null, "LocalizationManager was not found.");
            passed &= Check(animation != null, "PlayerAnimationDriver was not found on the player.");
            passed &= Check(receiver != null, "AnimationEventReceiver was not found on the player.");

            if (transition != null)
            {
                Image fade = GetField<Image>(transition, "fadeImage");
                passed &= Check(fade != null, "UITransitionManager is missing the fade image.");
                passed &= Check(fade != null && fade.GetComponent<CanvasGroup>() != null, "Fade image is missing a CanvasGroup.");
                transition.FadeOut(0.1f);
            }

            if (accessibility != null)
            {
                passed &= Check(accessibility.Settings != null, "Accessibility settings were not initialized.");
            }

            if (performance != null)
            {
                passed &= Check(Application.targetFrameRate > 0, "PerformanceManager did not set a target frame rate.");
            }

            if (localization != null)
            {
                passed &= Check(!string.IsNullOrEmpty(localization.CurrentLanguage), "Localization language was not initialized.");
                passed &= Check(localization.HasKey("menu.play"), "Localization defaults are missing key menu.play.");
                passed &= Check(localization.Get("menu.play") == "PLAY", "Localization returned an unexpected menu.play value.");
            }

            if (animation != null && receiver != null)
            {
                AnimationEventReceiver linked = GetField<AnimationEventReceiver>(animation, "eventReceiver");
                passed &= Check(linked == receiver, "PlayerAnimationDriver is not linked to the AnimationEventReceiver.");
            }

            if (GameManager.Instance != null)
            {
                passed &= Check(GameManager.Instance.GetService<IUIService>() != null, "IUIService was not registered.");
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
                Debug.Log($"[CONTRACT-STAGE8-SMOKE] PASSED after {elapsed:0.0}s.");
            }
            else
            {
                Debug.LogError("[CONTRACT-STAGE8-SMOKE] FAILED.");
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
            if (!condition) Debug.LogError($"[CONTRACT-STAGE8-SMOKE] {failureMessage}");
            return condition;
        }

        private static void Fail(string message)
        {
            finished = true;
            EditorApplication.update -= Tick;
            EditorApplication.playModeStateChanged -= HandlePlayModeChanged;
            Debug.LogError($"[CONTRACT-STAGE8-SMOKE] {message}");
            EditorApplication.Exit(1);
        }
    }
}
