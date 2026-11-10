using Contract.CameraSystem;
using Contract.Core;
using Contract.Player;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Contract.EditorTools
{
    public static class ContractStage2SmokeTest
    {
        private const double PlayDuration = 2.0;
        private const double StartupTimeout = 20.0;

        private static double startedAt;
        private static double playStartedAt;
        private static bool playStarted;
        private static bool finished;
        private static bool passedSmokeTest;

        [MenuItem("CONTRACT/Validate/Stage 2 Play Mode Smoke Test")]
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

            GameManager manager = GameManager.Instance;
            ThirdPersonController controller = Object.FindAnyObjectByType<ThirdPersonController>();
            ThirdPersonCamera cameraController = Object.FindAnyObjectByType<ThirdPersonCamera>();
            Camera camera = Camera.main;

            bool passed = true;
            passed &= Check(manager != null, "GameManager was not created.");
            passed &= Check(controller != null, "ThirdPersonController was not found.");
            passed &= Check(cameraController != null, "ThirdPersonCamera was not found.");
            passed &= Check(camera != null, "Main Camera was not found.");

            if (controller != null)
            {
                passed &= Check(controller.isActiveAndEnabled, "ThirdPersonController is not active.");
                passed &= Check(controller.transform.position.y > -0.5f, "Player fell through the ground.");
                passed &= Check(controller.GetComponent<CharacterController>() != null, "Player CharacterController is missing.");
                Animator animator = controller.GetComponent<Animator>();
                passed &= Check(animator != null && animator.runtimeAnimatorController != null, "Player Animator controller is missing.");
                passed &= Check(controller.GetComponent<PlayerAnimationDriver>() != null, "PlayerAnimationDriver is missing.");
            }

            if (camera != null && controller != null)
            {
                float cameraDistance = Vector3.Distance(camera.transform.position, controller.transform.position);
                passed &= Check(cameraDistance > 0.5f && cameraDistance < 8f, $"Camera distance is invalid: {cameraDistance:0.00}.");
            }

            if (manager != null)
            {
                passed &= Check(!manager.IsPaused, "GameManager started paused.");
                passed &= Check(Mathf.Approximately(Time.timeScale, 1f), "Time scale is not 1.");
            }

            double elapsed = now - playStartedAt;
            if (passed)
            {
                Debug.Log($"[CONTRACT-SMOKE] PASSED after {elapsed:0.0}s.");
            }
            else
            {
                Debug.LogError("[CONTRACT-SMOKE] FAILED.");
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
                Debug.LogError($"[CONTRACT-SMOKE] {failureMessage}");
            }

            return condition;
        }

        private static void Fail(string message)
        {
            finished = true;
            EditorApplication.update -= Tick;
            EditorApplication.playModeStateChanged -= HandlePlayModeChanged;
            Debug.LogError($"[CONTRACT-SMOKE] {message}");
            EditorApplication.Exit(1);
        }
    }
}
