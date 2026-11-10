using System;
using System.IO;
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
    [InitializeOnLoad]
    public static class ContractStage2Setup
    {
        private const string MarkerPath = "ProjectSettings/ContractStage2.done";
        private const string GameWorldPath = "Assets/Scenes/GameWorld.unity";
        private const string PlayerPrefabPath = "Assets/Prefabs/Player/Player.prefab";

        private static bool isRunning;

        static ContractStage2Setup()
        {
            EditorApplication.delayCall += AutoSetup;
        }

        [MenuItem("CONTRACT/Setup/Rebuild Stage 2 Player")]
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
            if (Application.isBatchMode || EditorApplication.isPlayingOrWillChangePlaymode)
            {
                return;
            }

            if (File.Exists(MarkerPath) || PlayerPrefabAlreadyConfigured())
            {
                if (!File.Exists(MarkerPath) && PlayerPrefabAlreadyConfigured())
                {
                    File.WriteAllText(MarkerPath, $"Stage 2 already present {DateTime.UtcNow:O}\n");
                    Debug.Log("[CONTRACT] Stage 2 player prefab is already configured. Automatic rebuild skipped.");
                }

                return;
            }

            if (EditorApplication.isCompiling || EditorApplication.isUpdating)
            {
                EditorApplication.delayCall += AutoSetup;
                return;
            }

            if (!File.Exists("ProjectSettings/ContractStage1.done") || !File.Exists(GameWorldPath))
            {
                EditorApplication.delayCall += AutoSetup;
                return;
            }

            RunSetup();
        }

        private static void RunSetup()
        {
            if (isRunning)
            {
                return;
            }

            isRunning = true;
            try
            {
                Debug.Log("[CONTRACT] Stage 2: building player controller and third-person camera...");
                EnsureFolder("Assets/Prefabs/Player");

                Material suitMaterial = CreateMaterial(
                    "Assets/Art/Materials/Player_Suit.mat",
                    new Color(0.045f, 0.055f, 0.075f),
                    0.35f,
                    0.55f);

                Material visorMaterial = CreateMaterial(
                    "Assets/Art/Materials/Player_Visor.mat",
                    new Color(0.35f, 0.02f, 0.035f),
                    0.1f,
                    0.8f,
                    new Color(2.8f, 0.05f, 0.1f));

                Material accentMaterial = CreateMaterial(
                    "Assets/Art/Materials/Player_Accent.mat",
                    new Color(0.5f, 0.025f, 0.04f),
                    0.25f,
                    0.65f,
                    new Color(1.2f, 0.02f, 0.04f));

                InputReader inputReader = AssetDatabase.LoadAssetAtPath<InputReader>("Assets/Settings/InputReader.asset");
                if (inputReader == null)
                {
                    Debug.LogError("[CONTRACT] Stage 2 requires InputReader.asset. Run Stage 1 setup first.");
                    return;
                }

                int playerLayer = LayerMask.NameToLayer("Player");
                if (playerLayer < 0)
                {
                    Debug.LogError("[CONTRACT] Stage 2 requires the Player layer. Run Stage 1 setup first.");
                    return;
                }

                Scene scene = EditorSceneManager.OpenScene(GameWorldPath, OpenSceneMode.Single);
                ConfigureEnvironmentLayer(scene);
                GameObject spawn = EnsureSpawn(scene);

                RemoveExistingPlayer();
                GameObject cameraObject = EnsureCamera(scene);
                Camera camera = cameraObject.GetComponent<Camera>();
                ThirdPersonCamera thirdPersonCamera = cameraObject.GetComponent<ThirdPersonCamera>();
                if (thirdPersonCamera == null)
                {
                    thirdPersonCamera = cameraObject.AddComponent<ThirdPersonCamera>();
                }

                GameObject player = BuildPlayer(suitMaterial, visorMaterial, accentMaterial, playerLayer);
                player.transform.SetPositionAndRotation(spawn.transform.position, Quaternion.identity);
                LayerMask playerExcludedMask = ~(1 << playerLayer);

                ThirdPersonController prefabController = player.GetComponent<ThirdPersonController>();
                ConfigureController(
                    prefabController,
                    inputReader,
                    null,
                    player.transform.Find("CameraTarget"),
                    player.transform.Find("Visual"),
                    playerExcludedMask);

                GameObject prefab = SavePlayerPrefab(player);
                Object.DestroyImmediate(player);

                if (prefab == null)
                {
                    Debug.LogError("[CONTRACT] Stage 2 player prefab could not be saved.");
                    return;
                }

                GameObject playerInstance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
                if (playerInstance == null)
                {
                    Debug.LogError("[CONTRACT] Stage 2 player prefab could not be instantiated.");
                    return;
                }

                playerInstance.transform.SetPositionAndRotation(spawn.transform.position, Quaternion.identity);

                Transform cameraTarget = playerInstance.transform.Find("CameraTarget");
                Transform visualRoot = playerInstance.transform.Find("Visual");
                ThirdPersonController controller = playerInstance.GetComponent<ThirdPersonController>();

                ConfigureController(
                    controller,
                    inputReader,
                    cameraObject.transform,
                    cameraTarget,
                    visualRoot,
                    playerExcludedMask);

                ConfigureCamera(thirdPersonCamera, inputReader, cameraTarget, playerExcludedMask);

                camera.fieldOfView = 60f;
                camera.nearClipPlane = 0.1f;
                camera.farClipPlane = 450f;
                camera.allowHDR = true;
                camera.allowMSAA = true;

                UniversalAdditionalCameraData cameraData = camera.GetUniversalAdditionalCameraData();
                cameraData.renderPostProcessing = true;
                cameraData.antialiasing = AntialiasingMode.FastApproximateAntialiasing;

                cameraObject.transform.SetPositionAndRotation(
                    spawn.transform.position + new Vector3(0.45f, 2.59f, -4.46f),
                    Quaternion.Euler(14f, 0f, 0f));

                UpdateGameWorldOverlay();
                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();

                File.WriteAllText(MarkerPath, $"Stage 2 completed {DateTime.UtcNow:O}\n");
                Debug.Log("[CONTRACT] Stage 2 player and third-person camera generated successfully.");
            }
            catch (Exception exception)
            {
                Debug.LogError($"[CONTRACT] Stage 2 setup failed: {exception}");
            }
            finally
            {
                isRunning = false;
            }
        }

        private static void ConfigureEnvironmentLayer(Scene scene)
        {
            int environmentLayer = LayerMask.NameToLayer("Environment");
            if (environmentLayer < 0)
            {
                Debug.LogWarning("[CONTRACT] Environment layer was not found; camera collision will still include default-layer geometry.");
                return;
            }

            GameObject environment = FindRoot(scene, "Environment");
            if (environment != null)
            {
                SetLayerRecursively(environment, environmentLayer);
            }
        }

        private static GameObject EnsureSpawn(Scene scene)
        {
            GameObject spawn = FindDeep(scene, "PlayerSpawn");
            if (spawn == null)
            {
                spawn = new GameObject("PlayerSpawn");
                SceneManager.MoveGameObjectToScene(spawn, scene);
            }

            foreach (GameObject root in scene.GetRootGameObjects())
            {
                foreach (Transform transform in root.GetComponentsInChildren<Transform>(true))
                {
                    if (transform.name == "PlayerSpawn" && transform.gameObject != spawn)
                    {
                        Object.DestroyImmediate(transform.gameObject);
                    }
                }
            }

            spawn.transform.position = new Vector3(0f, 0.1f, -2f);
            return spawn;
        }

        private static void RemoveExistingPlayer()
        {
            ThirdPersonController[] controllers = Object.FindObjectsByType<ThirdPersonController>(FindObjectsInactive.Include);
            foreach (ThirdPersonController controller in controllers)
            {
                if (controller != null)
                {
                    Object.DestroyImmediate(controller.gameObject);
                }
            }

            GameObject existingPlayer = GameObject.Find("Player");
            if (existingPlayer != null)
            {
                Object.DestroyImmediate(existingPlayer);
            }
        }

        private static GameObject EnsureCamera(Scene scene)
        {
            GameObject cameraObject = FindRoot(scene, "Main Camera");
            if (cameraObject == null)
            {
                cameraObject = new GameObject("Main Camera");
                cameraObject.tag = "MainCamera";
                SceneManager.MoveGameObjectToScene(cameraObject, scene);
            }

            Camera camera = cameraObject.GetComponent<Camera>();
            if (camera == null)
            {
                camera = cameraObject.AddComponent<Camera>();
            }

            cameraObject.tag = "MainCamera";
            if (cameraObject.GetComponent<AudioListener>() == null)
            {
                cameraObject.AddComponent<AudioListener>();
            }

            return cameraObject;
        }

        private static GameObject BuildPlayer(Material suitMaterial, Material visorMaterial, Material accentMaterial, int playerLayer)
        {
            var root = new GameObject("Player");
            root.tag = "Player";
            root.layer = playerLayer;

            CharacterController characterController = root.AddComponent<CharacterController>();
            characterController.height = 1.8f;
            characterController.radius = 0.35f;
            characterController.center = new Vector3(0f, 0.9f, 0f);
            characterController.slopeLimit = 45f;
            characterController.stepOffset = 0.35f;
            characterController.skinWidth = 0.02f;
            characterController.minMoveDistance = 0f;

            root.AddComponent<ThirdPersonController>();
            Animator animator = root.AddComponent<Animator>();
            animator.applyRootMotion = false;
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            animator.runtimeAnimatorController = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(
                "Assets/Art/Animations/PlayerLocomotion.controller");
            root.AddComponent<PlayerAnimationDriver>();

            var visualRoot = new GameObject("Visual");
            visualRoot.transform.SetParent(root.transform, false);

            CreateVisualPrimitive(PrimitiveType.Capsule, "Body", visualRoot.transform,
                new Vector3(0f, 0.82f, 0f), new Vector3(0.72f, 0.82f, 0.72f), suitMaterial);

            CreateVisualPrimitive(PrimitiveType.Sphere, "Head", visualRoot.transform,
                new Vector3(0f, 1.62f, 0f), new Vector3(0.42f, 0.44f, 0.42f), suitMaterial);

            CreateVisualPrimitive(PrimitiveType.Cube, "Visor", visualRoot.transform,
                new Vector3(0f, 1.64f, 0.2f), new Vector3(0.3f, 0.075f, 0.04f), visorMaterial);

            CreateVisualPrimitive(PrimitiveType.Cube, "Chest Accent", visualRoot.transform,
                new Vector3(0f, 1.27f, 0.3f), new Vector3(0.22f, 0.14f, 0.04f), accentMaterial);

            GameObject shoulderLeft = CreateVisualPrimitive(PrimitiveType.Cube, "Shoulder L", visualRoot.transform,
                new Vector3(-0.33f, 1.47f, 0f), new Vector3(0.12f, 0.1f, 0.22f), accentMaterial);
            CreateVisualPrimitive(PrimitiveType.Cube, "Arm", shoulderLeft.transform,
                new Vector3(0f, -2.2f, 0f), new Vector3(0.7f, 4.2f, 0.45f), suitMaterial);

            GameObject shoulderRight = CreateVisualPrimitive(PrimitiveType.Cube, "Shoulder R", visualRoot.transform,
                new Vector3(0.33f, 1.47f, 0f), new Vector3(0.12f, 0.1f, 0.22f), accentMaterial);
            CreateVisualPrimitive(PrimitiveType.Cube, "Arm", shoulderRight.transform,
                new Vector3(0f, -2.2f, 0f), new Vector3(0.7f, 4.2f, 0.45f), suitMaterial);

            CreateVisualPrimitive(PrimitiveType.Capsule, "Leg L", visualRoot.transform,
                new Vector3(-0.42f, 0.36f, 0.05f), new Vector3(0.16f, 0.34f, 0.16f), suitMaterial);
            CreateVisualPrimitive(PrimitiveType.Capsule, "Leg R", visualRoot.transform,
                new Vector3(0.42f, 0.36f, 0.05f), new Vector3(0.16f, 0.34f, 0.16f), suitMaterial);

            CreateVisualPrimitive(PrimitiveType.Cube, "Backpack", visualRoot.transform,
                new Vector3(0f, 1.12f, -0.28f), new Vector3(0.34f, 0.5f, 0.18f), suitMaterial);

            var cameraTarget = new GameObject("CameraTarget");
            cameraTarget.transform.SetParent(root.transform, false);
            cameraTarget.transform.localPosition = new Vector3(0f, 1.58f, 0f);

            SetLayerRecursively(root, playerLayer);
            return root;
        }

        private static GameObject CreateVisualPrimitive(
            PrimitiveType primitiveType,
            string name,
            Transform parent,
            Vector3 localPosition,
            Vector3 localScale,
            Material material)
        {
            GameObject primitive = GameObject.CreatePrimitive(primitiveType);
            primitive.name = name;
            primitive.transform.SetParent(parent, false);
            primitive.transform.localPosition = localPosition;
            primitive.transform.localScale = localScale;

            Collider collider = primitive.GetComponent<Collider>();
            if (collider != null)
            {
                Object.DestroyImmediate(collider);
            }

            Renderer renderer = primitive.GetComponent<Renderer>();
            if (renderer != null && material != null)
            {
                renderer.sharedMaterial = material;
            }

            return primitive;
        }

        private static GameObject SavePlayerPrefab(GameObject player)
        {
            if (AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPrefabPath) != null)
            {
                AssetDatabase.DeleteAsset(PlayerPrefabPath);
            }

            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(player, PlayerPrefabPath);
            AssetDatabase.SaveAssets();
            return prefab;
        }

        private static void UpdateGameWorldOverlay()
        {
            TMP_Text[] texts = Object.FindObjectsByType<TMP_Text>(FindObjectsInactive.Include);
            foreach (TMP_Text text in texts)
            {
                switch (text.gameObject.name)
                {
                    case "Title":
                        text.text = "DOWNTOWN CITY";
                        break;
                    case "Subtitle":
                        text.text = "TRAINING SECTOR // PLAYER ONLINE";
                        break;
                    case "Stage Label":
                        text.text = "WASD MOVE | SHIFT SPRINT | CTRL CROUCH | SPACE JUMP | ESC PAUSE // STAGE 2";
                        break;
                }

                EditorUtility.SetDirty(text);
            }
        }

        private static void EnsureFolder(string folder)
        {
            if (AssetDatabase.IsValidFolder(folder))
            {
                return;
            }

            string parent = Path.GetDirectoryName(folder)?.Replace('\\', '/');
            string name = Path.GetFileName(folder);
            if (!string.IsNullOrEmpty(parent) && !AssetDatabase.IsValidFolder(parent))
            {
                EnsureFolder(parent);
            }

            AssetDatabase.CreateFolder(parent, name);
        }

        private static Material CreateMaterial(
            string path,
            Color baseColor,
            float metallic,
            float smoothness,
            Color? emission = null)
        {
            Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                Shader shader = Shader.Find("Universal Render Pipeline/Lit");
                if (shader == null)
                {
                    Debug.LogError("[CONTRACT] URP Lit shader was not found; player materials could not be created.");
                    return null;
                }

                material = new Material(shader);
                AssetDatabase.CreateAsset(material, path);
            }

            material.SetColor("_BaseColor", baseColor);
            material.SetFloat("_Metallic", metallic);
            material.SetFloat("_Smoothness", smoothness);
            material.enableInstancing = true;

            if (emission.HasValue)
            {
                material.EnableKeyword("_EMISSION");
                material.SetColor("_EmissionColor", emission.Value);
                material.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            }

            EditorUtility.SetDirty(material);
            AssetDatabase.SaveAssets();
            return AssetDatabase.LoadAssetAtPath<Material>(path);
        }

        private static bool PlayerPrefabAlreadyConfigured()
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPrefabPath);
            if (prefab == null)
            {
                return false;
            }

            Animator animator = prefab.GetComponent<Animator>();
            return prefab.GetComponent<ThirdPersonController>() != null
                && prefab.GetComponent<CharacterController>() != null
                && prefab.GetComponent<PlayerAnimationDriver>() != null
                && animator != null
                && animator.runtimeAnimatorController != null;
        }

        private static GameObject FindRoot(Scene scene, string name)
        {
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                if (root.name == name)
                {
                    return root;
                }
            }

            return null;
        }

        private static GameObject FindDeep(Scene scene, string name)
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

        private static void SetLayerRecursively(GameObject gameObject, int layer)
        {
            gameObject.layer = layer;
            foreach (Transform child in gameObject.GetComponentsInChildren<Transform>(true))
            {
                child.gameObject.layer = layer;
            }
        }

        private static void ConfigureController(
            ThirdPersonController controller,
            InputReader inputReader,
            Transform cameraTransform,
            Transform cameraTarget,
            Transform visualRoot,
            LayerMask obstructionMask)
        {
            var serializedController = new SerializedObject(controller);
            serializedController.FindProperty("inputReader").objectReferenceValue = inputReader;
            serializedController.FindProperty("cameraTransform").objectReferenceValue = cameraTransform;
            serializedController.FindProperty("cameraTarget").objectReferenceValue = cameraTarget;
            serializedController.FindProperty("visualRoot").objectReferenceValue = visualRoot;
            serializedController.FindProperty("obstructionMask").intValue = obstructionMask.value;
            serializedController.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(controller);
        }

        private static void ConfigureCamera(
            ThirdPersonCamera thirdPersonCamera,
            InputReader inputReader,
            Transform target,
            LayerMask collisionMask)
        {
            var serializedCamera = new SerializedObject(thirdPersonCamera);
            serializedCamera.FindProperty("inputReader").objectReferenceValue = inputReader;
            serializedCamera.FindProperty("target").objectReferenceValue = target;
            serializedCamera.FindProperty("collisionMask").intValue = collisionMask.value;
            serializedCamera.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(thirdPersonCamera);
        }
    }
}
