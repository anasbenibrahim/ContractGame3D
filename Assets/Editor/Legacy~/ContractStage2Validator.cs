using System;
using System.Collections.Generic;
using System.Linq;
using Contract.CameraSystem;
using Contract.Core;
using Contract.Player;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace Contract.EditorTools
{
    public static class ContractStage2Validator
    {
        private const string GameWorldPath = "Assets/Scenes/GameWorld.unity";
        private const string PlayerPrefabPath = "Assets/Prefabs/Player/Player.prefab";

        [MenuItem("CONTRACT/Validate/Stage 2")]
        public static void Validate()
        {
            var failures = new List<string>();
            Scene scene = EditorSceneManager.OpenScene(GameWorldPath, OpenSceneMode.Single);
            GameObject cameraObject = GameObject.Find("Main Camera");
            GameObject player = GameObject.Find("Player");
            GameObject spawn = GameObject.Find("PlayerSpawn");
            InputReader inputReader = AssetDatabase.LoadAssetAtPath<InputReader>("Assets/Settings/InputReader.asset");

            ValidateSceneBasics(scene, failures);
            ValidateInputReader(inputReader, failures);
            ValidatePlayer(player, cameraObject, inputReader, failures);
            ValidateCamera(cameraObject, player, inputReader, failures);
            ValidatePrefab(failures);
            ValidateEnvironment(failures);
            ValidateSpawn(player, spawn, failures);
            ValidateOverlay(failures);

            if (failures.Count > 0)
            {
                foreach (string failure in failures)
                {
                    Debug.LogError($"[CONTRACT-STAGE2] {failure}");
                }

                Debug.LogError($"[CONTRACT-STAGE2] FAILED with {failures.Count} issue(s).");
                if (Application.isBatchMode)
                {
                    EditorApplication.Exit(1);
                }

                return;
            }

            Debug.Log("[CONTRACT-STAGE2] PASSED: player prefab, controller, camera, environment layers, spawn and overlay are valid.");
        }

        private static void ValidateSceneBasics(Scene scene, ICollection<string> failures)
        {
            int missingScripts = 0;
            int cameraCount = 0;
            int controllerCount = 0;
            int spawnCount = 0;

            foreach (GameObject root in scene.GetRootGameObjects())
            {
                foreach (Transform transform in root.GetComponentsInChildren<Transform>(true))
                {
                    GameObject gameObject = transform.gameObject;
                    if (gameObject.GetComponents<Component>().Any(component => component == null))
                    {
                        missingScripts++;
                    }

                    if (gameObject.GetComponent<Camera>() != null
                        && gameObject.name != "MinimapCamera"
                        && gameObject.name != "FullMapCamera")
                    {
                        cameraCount++;
                    }

                    if (gameObject.GetComponent<ThirdPersonController>() != null)
                    {
                        controllerCount++;
                    }

                    if (gameObject.name == "PlayerSpawn")
                    {
                        spawnCount++;
                    }
                }
            }

            if (missingScripts > 0)
            {
                failures.Add($"GameWorld contains {missingScripts} missing script reference(s).");
            }

            if (cameraCount != 1)
            {
                failures.Add($"GameWorld must contain exactly one camera, found {cameraCount}.");
            }

            if (controllerCount != 1)
            {
                failures.Add($"GameWorld must contain exactly one player controller, found {controllerCount}.");
            }

            if (spawnCount != 1)
            {
                failures.Add($"GameWorld must contain exactly one PlayerSpawn marker, found {spawnCount}.");
            }
        }

        private static void ValidateInputReader(InputReader inputReader, ICollection<string> failures)
        {
            if (inputReader == null)
            {
                failures.Add("InputReader.asset is missing.");
                return;
            }

            var serializedReader = new SerializedObject(inputReader);
            SerializedProperty actions = serializedReader.FindProperty("actions");
            if (actions == null || actions.objectReferenceValue == null)
            {
                failures.Add("InputReader.asset has no InputActionAsset assigned.");
            }
        }

        private static void ValidatePlayer(GameObject player, GameObject cameraObject, InputReader inputReader, ICollection<string> failures)
        {
            if (player == null)
            {
                failures.Add("GameWorld has no Player object.");
                return;
            }

            int playerLayer = LayerMask.NameToLayer("Player");
            if (player.tag != "Player")
            {
                failures.Add("Player is not tagged 'Player'.");
            }

            if (player.layer != playerLayer)
            {
                failures.Add($"Player layer is {player.layer}, expected {playerLayer}.");
            }

            CharacterController characterController = player.GetComponent<CharacterController>();
            if (characterController == null)
            {
                failures.Add("Player has no CharacterController.");
            }
            else
            {
                if (characterController.height < 1.7f)
                {
                    failures.Add("Player CharacterController height is too small.");
                }

                if (characterController.radius <= 0f)
                {
                    failures.Add("Player CharacterController radius is invalid.");
                }
            }

            ThirdPersonController controller = player.GetComponent<ThirdPersonController>();
            if (controller == null)
            {
                failures.Add("Player has no ThirdPersonController.");
                return;
            }

            if (PrefabUtility.GetPrefabInstanceStatus(player) != PrefabInstanceStatus.Connected)
            {
                failures.Add("Player is not a connected prefab instance.");
            }

            Transform visualRoot = GetObject<Transform>(controller, "visualRoot");
            Transform cameraTarget = GetObject<Transform>(controller, "cameraTarget");
            Transform cameraTransform = GetObject<Transform>(controller, "cameraTransform");
            InputReader assignedReader = GetObject<InputReader>(controller, "inputReader");
            int obstructionMask = GetInt(controller, "obstructionMask");

            if (visualRoot == null || visualRoot.name != "Visual")
            {
                failures.Add("ThirdPersonController visualRoot is not assigned to the Visual child.");
            }
            else if (visualRoot.GetComponentsInChildren<Renderer>(true).Length < 5)
            {
                failures.Add("Player visual root does not contain the expected placeholder body parts.");
            }

            if (cameraTarget == null || cameraTarget.name != "CameraTarget")
            {
                failures.Add("ThirdPersonController cameraTarget is not assigned to the CameraTarget child.");
            }

            if (cameraObject != null && cameraTransform != cameraObject.transform)
            {
                failures.Add("ThirdPersonController cameraTransform does not reference the Main Camera.");
            }

            if (assignedReader != inputReader)
            {
                failures.Add("ThirdPersonController does not reference InputReader.asset.");
            }

            if (playerLayer >= 0 && (obstructionMask & (1 << playerLayer)) != 0)
            {
                failures.Add("Player obstruction mask still includes the Player layer.");
            }
        }

        private static void ValidateCamera(GameObject cameraObject, GameObject player, InputReader inputReader, ICollection<string> failures)
        {
            if (cameraObject == null)
            {
                failures.Add("GameWorld has no Main Camera.");
                return;
            }

            if (cameraObject.tag != "MainCamera")
            {
                failures.Add("Main Camera is not tagged 'MainCamera'.");
            }

            Camera camera = cameraObject.GetComponent<Camera>();
            if (camera == null)
            {
                failures.Add("Main Camera has no Camera component.");
                return;
            }

            if (cameraObject.GetComponent<AudioListener>() == null)
            {
                failures.Add("Main Camera has no AudioListener.");
            }

            if (camera.GetUniversalAdditionalCameraData() == null)
            {
                failures.Add("Main Camera is missing URP additional camera data.");
            }

            if (camera.fieldOfView < 55f || camera.fieldOfView > 75f)
            {
                failures.Add($"Main Camera field of view is {camera.fieldOfView}, expected roughly 60.");
            }

            ThirdPersonCamera thirdPersonCamera = cameraObject.GetComponent<ThirdPersonCamera>();
            if (thirdPersonCamera == null)
            {
                failures.Add("Main Camera has no ThirdPersonCamera component.");
                return;
            }

            Transform target = GetObject<Transform>(thirdPersonCamera, "target");
            InputReader assignedReader = GetObject<InputReader>(thirdPersonCamera, "inputReader");
            int collisionMask = GetInt(thirdPersonCamera, "collisionMask");
            int playerLayer = LayerMask.NameToLayer("Player");

            if (player == null || target == null || target != player.transform.Find("CameraTarget"))
            {
                failures.Add("ThirdPersonCamera target is not the player's CameraTarget.");
            }

            if (assignedReader != inputReader)
            {
                failures.Add("ThirdPersonCamera does not reference InputReader.asset.");
            }

            if (playerLayer >= 0 && (collisionMask & (1 << playerLayer)) != 0)
            {
                failures.Add("ThirdPersonCamera collision mask still includes the Player layer.");
            }
        }

        private static void ValidatePrefab(ICollection<string> failures)
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPrefabPath);
            if (prefab == null)
            {
                failures.Add($"Player prefab is missing at {PlayerPrefabPath}.");
                return;
            }

            if (prefab.GetComponent<ThirdPersonController>() == null)
            {
                failures.Add("Player prefab has no ThirdPersonController.");
            }

            CharacterController characterController = prefab.GetComponent<CharacterController>();
            if (characterController == null)
            {
                failures.Add("Player prefab has no CharacterController.");
            }

            if (prefab.tag != "Player")
            {
                failures.Add("Player prefab is not tagged 'Player'.");
            }

            if (prefab.GetComponentsInChildren<Renderer>(true).Length < 5)
            {
                failures.Add("Player prefab has too few visual renderers.");
            }

            if (prefab.GetComponentsInChildren<Collider>(true).Length != 1)
            {
                failures.Add("Player prefab should contain exactly one collider (the CharacterController).");
            }

            Animator animator = prefab.GetComponent<Animator>();
            if (animator == null || animator.runtimeAnimatorController == null)
            {
                failures.Add("Player prefab has no Animator controller.");
            }
            else if (animator.applyRootMotion)
            {
                failures.Add("Player Animator must not use root motion.");
            }

            if (prefab.GetComponent<PlayerAnimationDriver>() == null)
            {
                failures.Add("Player prefab has no PlayerAnimationDriver.");
            }

            AnimationClip walk = AssetDatabase.LoadAssetAtPath<AnimationClip>("Assets/Art/Animations/Player_Walk.anim");
            if (walk == null || AnimationUtility.GetCurveBindings(walk).Length == 0)
            {
                failures.Add("Player walk animation clip is missing curves.");
            }
        }

        private static void ValidateEnvironment(ICollection<string> failures)
        {
            int environmentLayer = LayerMask.NameToLayer("Environment");
            if (environmentLayer < 0)
            {
                failures.Add("Environment layer is missing.");
                return;
            }

            GameObject environment = GameObject.Find("Environment");
            if (environment == null)
            {
                failures.Add("GameWorld has no Environment root.");
                return;
            }

            int wrongLayerCount = 0;
            foreach (Transform transform in environment.GetComponentsInChildren<Transform>(true))
            {
                if (transform.gameObject.layer != environmentLayer)
                {
                    wrongLayerCount++;
                }
            }

            if (wrongLayerCount > 0)
            {
                failures.Add($"Environment contains {wrongLayerCount} object(s) outside the Environment layer.");
            }

            foreach (string materialPath in new[]
                     {
                         "Assets/Art/Materials/Player_Suit.mat",
                         "Assets/Art/Materials/Player_Visor.mat",
                         "Assets/Art/Materials/Player_Accent.mat"
                     })
            {
                if (AssetDatabase.LoadAssetAtPath<Material>(materialPath) == null)
                {
                    failures.Add($"Player material is missing: {materialPath}");
                }
            }
        }

        private static void ValidateSpawn(GameObject player, GameObject spawn, ICollection<string> failures)
        {
            if (spawn == null)
            {
                failures.Add("GameWorld has no PlayerSpawn marker.");
                return;
            }

            if (player != null)
            {
                float horizontalDistance = Vector2.Distance(
                    new Vector2(player.transform.position.x, player.transform.position.z),
                    new Vector2(spawn.transform.position.x, spawn.transform.position.z));

                if (horizontalDistance > 0.1f)
                {
                    failures.Add("Player is not positioned at PlayerSpawn.");
                }
            }
        }

        private static void ValidateOverlay(ICollection<string> failures)
        {
            TMP_Text stageLabel = Object.FindObjectsByType<TMP_Text>(FindObjectsInactive.Include)
                .FirstOrDefault(text => text.gameObject.name == "Stage Label");

            if (stageLabel == null)
            {
                failures.Add("GameWorld is missing the Stage Label text.");
                return;
            }

            if (!stageLabel.text.Contains("STAGE"))
            {
                failures.Add("GameWorld overlay is missing a stage label.");
            }
        }

        private static T GetObject<T>(Object target, string propertyName) where T : Object
        {
            if (target == null)
            {
                return null;
            }

            var serializedObject = new SerializedObject(target);
            SerializedProperty property = serializedObject.FindProperty(propertyName);
            return property?.objectReferenceValue as T;
        }

        private static int GetInt(Object target, string propertyName)
        {
            if (target == null)
            {
                return 0;
            }

            var serializedObject = new SerializedObject(target);
            SerializedProperty property = serializedObject.FindProperty(propertyName);
            return property?.intValue ?? 0;
        }
    }
}
