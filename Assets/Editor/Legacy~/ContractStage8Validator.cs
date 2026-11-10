using System;
using System.Collections.Generic;
using System.IO;
using Contract.Accessibility;
using Contract.Localization;
using Contract.Performance;
using Contract.Player;
using Contract.UI;
using Contract.VFX;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace Contract.EditorTools
{
    public static class ContractStage8Validator
    {
        private const string GameWorldPath = "Assets/Scenes/GameWorld.unity";
        private const string Stage8MarkerPath = "ProjectSettings/ContractStage8.done";

        [MenuItem("CONTRACT/Validate/Stage 8")]
        public static void Validate()
        {
            var failures = new List<string>();
            if (!File.Exists(Stage8MarkerPath))
            {
                failures.Add("Stage 8 setup marker is missing.");
            }

            Scene scene = EditorSceneManager.OpenScene(GameWorldPath, OpenSceneMode.Single);
            ValidateAnimationSystem(scene, failures);
            ValidateUITransitions(failures);
            ValidateAccessibility(failures);
            ValidatePerformance(failures);
            ValidateLocalization(failures);
            ValidateScreenFade(failures);

            if (failures.Count > 0)
            {
                foreach (string failure in failures)
                {
                    Debug.LogError($"[CONTRACT-STAGE8] {failure}");
                }

                Debug.LogError($"[CONTRACT-STAGE8] FAILED with {failures.Count} issue(s).");
                if (Application.isBatchMode)
                {
                    EditorApplication.Exit(1);
                }
                return;
            }

            Debug.Log("[CONTRACT-STAGE8] PASSED: animation, UI transitions, accessibility, performance, localization, and screen fade are valid.");
        }

        private static void ValidateAnimationSystem(Scene scene, ICollection<string> failures)
        {
            PlayerAnimationDriver driver = Object.FindAnyObjectByType<PlayerAnimationDriver>();
            if (driver == null)
            {
                failures.Add("PlayerAnimationDriver not found in scene.");
            }
            else
            {
                AnimationEventReceiver receiver = Object.FindAnyObjectByType<AnimationEventReceiver>();
                if (receiver == null)
                {
                    failures.Add("AnimationEventReceiver not found in scene.");
                }

                SerializedObject so = new SerializedObject(driver);
                if (so.FindProperty("eventReceiver").objectReferenceValue == null)
                {
                    failures.Add("PlayerAnimationDriver missing EventReceiver reference.");
                }
            }
        }

        private static void ValidateUITransitions(ICollection<string> failures)
        {
            UITransitionManager manager = Object.FindAnyObjectByType<UITransitionManager>();
            if (manager == null)
            {
                failures.Add("UITransitionManager not found in scene.");
            }
            else
            {
                Image fadeImage = manager.GetComponentInChildren<Image>();
                if (fadeImage == null)
                {
                    failures.Add("UITransitionManager missing ScreenFade Image.");
                }
                else if (fadeImage.GetComponent<CanvasGroup>() == null)
                {
                    failures.Add("ScreenFade Image missing CanvasGroup component.");
                }
            }
        }

        private static void ValidateAccessibility(ICollection<string> failures)
        {
            AccessibilityManager manager = Object.FindAnyObjectByType<AccessibilityManager>();
            if (manager == null)
            {
                failures.Add("AccessibilityManager not found in scene.");
            }
        }

        private static void ValidatePerformance(ICollection<string> failures)
        {
            PerformanceManager manager = Object.FindAnyObjectByType<PerformanceManager>();
            if (manager == null)
            {
                failures.Add("PerformanceManager not found in scene.");
            }
        }

        private static void ValidateLocalization(ICollection<string> failures)
        {
            LocalizationManager manager = Object.FindAnyObjectByType<LocalizationManager>();
            if (manager == null)
            {
                failures.Add("LocalizationManager not found in scene.");
            }
        }

        private static void ValidateScreenFade(ICollection<string> failures)
        {
            UITransitionManager manager = Object.FindAnyObjectByType<UITransitionManager>();
            if (manager != null)
            {
                Image fadeImage = manager.GetComponentInChildren<Image>();
                if (fadeImage != null)
                {
                    CanvasGroup cg = fadeImage.GetComponent<CanvasGroup>();
                    if (cg == null)
                    {
                        failures.Add("ScreenFade Image missing CanvasGroup for fade transitions.");
                    }
                }
            }
        }
    }
}