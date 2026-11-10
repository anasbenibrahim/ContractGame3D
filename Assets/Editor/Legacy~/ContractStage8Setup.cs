using System;
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
    [InitializeOnLoad]
    public static class ContractStage8Setup
    {
        private const string MarkerPath = "ProjectSettings/ContractStage8.done";
        private const string Stage7MarkerPath = "ProjectSettings/ContractStage7.done";
        private const string GameWorldPath = "Assets/Scenes/GameWorld.unity";

        private static bool isRunning;

        static ContractStage8Setup()
        {
            EditorApplication.delayCall += AutoSetup;
        }

        [MenuItem("CONTRACT/Setup/Rebuild Stage 8 Polish Systems")]
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

            if (!File.Exists(Stage7MarkerPath) || !File.Exists(GameWorldPath))
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
                Debug.Log("[CONTRACT] Stage 8: wiring animation, transitions, accessibility, performance, and localization...");
                Scene scene = EditorSceneManager.OpenScene(GameWorldPath, OpenSceneMode.Single);
                SetupInScene(scene, true);
                Debug.Log("[CONTRACT] Stage 8 setup complete.");
            }
            catch (Exception exception)
            {
                Debug.LogError("[CONTRACT] Stage 8 setup failed: " + exception);
            }
            finally
            {
                isRunning = false;
            }
        }


        public static void SetupInScene(Scene scene, bool saveScene)
        {
            DestroyRoot(scene, "[UITransitionManager]");
            DestroyRoot(scene, "[AccessibilityManager]");
            DestroyRoot(scene, "[PerformanceManager]");
            DestroyRoot(scene, "[LocalizationManager]");
            DestroyRoot(scene, "ScreenFade");

            SetupAnimationSystem();
            SetupUITransitionManager(scene);
            SetupAccessibilityManager(scene);
            SetupPerformanceManager(scene);
            SetupLocalizationManager(scene);

            EditorSceneManager.MarkSceneDirty(scene);
            if (saveScene)
            {
                EditorSceneManager.SaveScene(scene);
            }

            AssetDatabase.SaveAssets();
            File.WriteAllText(MarkerPath, DateTime.UtcNow.ToString("o"));
        }

        private static void SetupAnimationSystem()
        {
            GameObject player = GameObject.FindGameObjectWithTag("Player") ?? GameObject.Find("Player");
            if (player == null)
            {
                Debug.LogWarning("[CONTRACT] Stage 8: Player not found; animation setup skipped.");
                return;
            }

            Animator animator = player.GetComponent<Animator>();
            PlayerAnimationDriver driver = player.GetComponent<PlayerAnimationDriver>();
            if (driver == null) driver = player.AddComponent<PlayerAnimationDriver>();

            AnimationEventReceiver receiver = player.GetComponent<AnimationEventReceiver>();
            if (receiver == null) receiver = player.AddComponent<AnimationEventReceiver>();

            SerializedObject so = new SerializedObject(driver);
            SerializedProperty animatorProperty = so.FindProperty("animator");
            if (animatorProperty != null && animator != null)
            {
                animatorProperty.objectReferenceValue = animator;
            }
            SerializedProperty receiverProperty = so.FindProperty("eventReceiver");
            if (receiverProperty != null)
            {
                receiverProperty.objectReferenceValue = receiver;
            }
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(driver);
            EditorUtility.SetDirty(receiver);
        }

        private static void SetupUITransitionManager(Scene scene)
        {
            GameObject root = CreateRoot(scene, "[UITransitionManager]");
            UITransitionManager manager = root.AddComponent<UITransitionManager>();

            GameObject canvasObject = new GameObject("ScreenFade Canvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasObject.transform.SetParent(root.transform, false);
            Canvas canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 1000;
            CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);

            GameObject fadeObject = new GameObject("ScreenFade", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(CanvasGroup));
            fadeObject.transform.SetParent(canvasObject.transform, false);
            RectTransform fadeRect = fadeObject.GetComponent<RectTransform>();
            fadeRect.anchorMin = Vector2.zero;
            fadeRect.anchorMax = Vector2.one;
            fadeRect.offsetMin = Vector2.zero;
            fadeRect.offsetMax = Vector2.zero;
            Image fadeImage = fadeObject.GetComponent<Image>();
            fadeImage.color = Color.black;
            fadeImage.raycastTarget = false;
            CanvasGroup fadeGroup = fadeObject.GetComponent<CanvasGroup>();
            fadeGroup.alpha = 0f;
            fadeGroup.interactable = false;
            fadeGroup.blocksRaycasts = false;

            SerializedObject so = new SerializedObject(manager);
            SetObject(so, "fadeImage", fadeImage);
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(manager);
        }

        private static void SetupAccessibilityManager(Scene scene)
        {
            GameObject root = CreateRoot(scene, "[AccessibilityManager]");
            AccessibilityManager manager = root.AddComponent<AccessibilityManager>();

            PostProcessingController postProcessing = Object.FindAnyObjectByType<PostProcessingController>();
            if (postProcessing != null)
            {
                SerializedObject so = new SerializedObject(manager);
                SetObject(so, "postProcessing", postProcessing);
                so.ApplyModifiedPropertiesWithoutUndo();
            }

            EditorUtility.SetDirty(manager);
        }

        private static void SetupPerformanceManager(Scene scene)
        {
            GameObject root = CreateRoot(scene, "[PerformanceManager]");
            PerformanceManager manager = root.AddComponent<PerformanceManager>();
            EditorUtility.SetDirty(manager);
        }

        private static void SetupLocalizationManager(Scene scene)
        {
            GameObject root = CreateRoot(scene, "[LocalizationManager]");
            LocalizationManager manager = root.AddComponent<LocalizationManager>();
            EditorUtility.SetDirty(manager);
        }

        private static GameObject CreateRoot(Scene scene, string name)
        {
            GameObject gameObject = new GameObject(name);
            SceneManager.MoveGameObjectToScene(gameObject, scene);
            return gameObject;
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
    }
}
