using System.Linq;
using Contract.Core;
using Contract.Interaction;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Contract.EditorTools
{
    public static class ContractStage4SmokeTest
    {
        private const double PlayDuration = 2.0;
        private const double StartupTimeout = 20.0;

        private static double startedAt;
        private static double playStartedAt;
        private static bool playStarted;
        private static bool finished;
        private static bool passedSmokeTest;

        [MenuItem("CONTRACT/Validate/Stage 4 Play Mode Smoke Test")]
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
            if (finished)
            {
                return;
            }

            double now = EditorApplication.timeSinceStartup;
            if (!playStarted && now - startedAt > StartupTimeout)
            {
                Fail("Play mode did not start within the timeout.");
                return;
            }

            if (!playStarted || now - playStartedAt < PlayDuration)
            {
                return;
            }

            ValidateRuntime(now);
        }

        private static void ValidateRuntime(double now)
        {
            finished = true;
            EditorApplication.update -= Tick;
            EditorApplication.playModeStateChanged -= HandlePlayModeChanged;

            InteractionController controller = Object.FindAnyObjectByType<InteractionController>();
            DoorInteractable door = Object.FindObjectsByType<DoorInteractable>(FindObjectsInactive.Include)
                .FirstOrDefault();
            PickupInteractable keycard = Object.FindObjectsByType<PickupInteractable>(FindObjectsInactive.Include)
                .FirstOrDefault(pickup => pickup.ItemId == "keycard");
            SafehouseInteractable safehouse = Object.FindAnyObjectByType<SafehouseInteractable>();
            ExtractionInteractable extraction = Object.FindAnyObjectByType<ExtractionInteractable>();
            TMP_Text prompt = Object.FindObjectsByType<TMP_Text>(FindObjectsInactive.Include)
                .FirstOrDefault(text => text.gameObject.name == "Interaction Prompt");

            bool passed = true;
            passed &= Check(controller != null, "InteractionController was not found.");
            passed &= Check(controller != null && controller.isActiveAndEnabled, "InteractionController is not active.");
            passed &= Check(door != null, "DoorInteractable was not found.");
            passed &= Check(keycard != null, "Keycard pickup was not found.");
            passed &= Check(safehouse != null, "SafehouseInteractable was not found.");
            passed &= Check(extraction != null, "ExtractionInteractable was not found.");
            passed &= Check(prompt != null && prompt.font != null, "Interaction Prompt text or font is missing.");

            if (controller == null)
            {
                Finish(passed, now);
                return;
            }

            GameObject interactor = controller.gameObject;
            bool itemCollected = false;
            void HandleItemCollected(string itemId)
            {
                if (itemId == "keycard")
                {
                    itemCollected = true;
                }
            }

            GameEvents.ItemCollected += HandleItemCollected;

            if (extraction != null)
            {
                extraction.Interact(interactor);
                passed &= Check(!extraction.IsExtracted, "Extraction unlocked without the keycard.");
            }

            if (keycard != null)
            {
                keycard.Interact(interactor);
                passed &= Check(keycard.IsCollected, "Keycard was not collected.");
                passed &= Check(itemCollected, "ItemCollected event was not raised for the keycard.");
            }

            if (extraction != null && keycard != null)
            {
                passed &= Check(extraction.HasRequiredItem, "Extraction did not receive the keycard requirement.");
                extraction.Interact(interactor);
                passed &= Check(extraction.IsExtracted, "Extraction did not activate with the keycard.");
            }

            if (safehouse != null)
            {
                safehouse.Interact(interactor);
                passed &= Check(safehouse.IsSecured, "Safehouse was not secured.");
            }

            if (door != null)
            {
                bool wasOpen = door.IsOpen;
                door.Interact(interactor);
                passed &= Check(door.IsOpen != wasOpen, "Safehouse door did not toggle.");
            }

            GameEvents.ItemCollected -= HandleItemCollected;
            Finish(passed, now);
        }

        private static void Finish(bool passed, double now)
        {
            double elapsed = now - playStartedAt;
            if (passed)
            {
                Debug.Log($"[CONTRACT-STAGE4-SMOKE] PASSED after {elapsed:0.0}s.");
            }
            else
            {
                Debug.LogError("[CONTRACT-STAGE4-SMOKE] FAILED.");
            }

            passedSmokeTest = passed;
            EditorApplication.playModeStateChanged += HandleExitPlayMode;
            EditorApplication.isPlaying = false;
        }

        private static void HandleExitPlayMode(PlayModeStateChange state)
        {
            if (state != PlayModeStateChange.EnteredEditMode)
            {
                return;
            }

            EditorApplication.playModeStateChanged -= HandleExitPlayMode;
            EditorApplication.Exit(passedSmokeTest ? 0 : 1);
        }

        private static bool Check(bool condition, string failureMessage)
        {
            if (!condition)
            {
                Debug.LogError($"[CONTRACT-STAGE4-SMOKE] {failureMessage}");
            }

            return condition;
        }

        private static void Fail(string message)
        {
            finished = true;
            EditorApplication.update -= Tick;
            EditorApplication.playModeStateChanged -= HandlePlayModeChanged;
            Debug.LogError($"[CONTRACT-STAGE4-SMOKE] {message}");
            EditorApplication.Exit(1);
        }
    }
}
