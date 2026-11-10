using System;
using Contract.Accessibility;
using Contract.Audio;
using Contract.Core;
using Contract.Character;
using Contract.Economy;
using Contract.Interaction;
using Contract.Localization;
using Contract.Map;
using Contract.Missions;
using Contract.NPCs;
using Contract.Performance;
using Contract.Player;
using Contract.Progression;
using Contract.Regions;
using Contract.SaveSystem;
using Contract.UI;
using Contract.VFX;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Audio;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;
using UnityEditor;
using Object = UnityEngine.Object;

namespace Contract.EditorTools
{
    /// <summary>
    /// Editor script to set up GameWorld scene with all required components.
    /// Run from Unity Editor: CONTRACT/Setup/Setup GameWorld Scene
    /// </summary>
    public static class GameWorldSceneSetup
    {
        [UnityEditor.MenuItem("CONTRACT/Setup/Setup GameWorld Scene")]
        public static void SetupGameWorld()
        {
            UnityEditor.SceneManagement.EditorSceneManager.OpenScene("Assets/Scenes/GameWorld.unity", UnityEditor.SceneManagement.OpenSceneMode.Single);

            SetupMinimapLayer();
            SetupCinemachineCamera();
            SetupAudioService();
            SetupAudioMixer();
            SetupVFXSystem();
            SetupPostProcessingController();
            SetupGameManager();
            SetupSaveManager();
            SetupMissionManager();
            SetupRegionManager();
            SetupCharacterManager();
            SetupEconomyManager();
            SetupUpgradeManager();
            SetupMapManager();
            SetupNpcManager();
            SetupUIManager();
            SetupPlayer();
            SetupEnvironment();
            SetupPolishSystems();

            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene());
            UnityEditor.SceneManagement.EditorSceneManager.SaveScene(UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene());
            UnityEditor.AssetDatabase.SaveAssets();

            UnityEngine.Debug.Log("[CONTRACT] GameWorld scene setup complete.");
        }

        private static void SetupPolishSystems()
        {
            ContractStage8Setup.SetupInScene(UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene(), false);
        }

        private static void SetupAnimationSystem()
        {
            GameObject player = GameObject.Find("Player");
            if (player == null) return;

            PlayerAnimationDriver driver = player.GetComponent<PlayerAnimationDriver>();
            if (driver == null) driver = player.AddComponent<PlayerAnimationDriver>();

            AnimationEventReceiver receiver = player.GetComponent<AnimationEventReceiver>();
            if (receiver == null) receiver = player.AddComponent<AnimationEventReceiver>();

            SerializedObject so = new SerializedObject(driver);
            so.FindProperty("eventReceiver").objectReferenceValue = receiver;
            so.ApplyModifiedProperties();

            EditorUtility.SetDirty(driver);
            EditorUtility.SetDirty(receiver);
        }

        private static void SetupUITransitionManager()
        {
            GameObject go = new GameObject("[UITransitionManager]");
            UITransitionManager manager = go.AddComponent<UITransitionManager>();

            GameObject fadeObj = new GameObject("ScreenFade");
            fadeObj.transform.SetParent(go.transform, false);
            RectTransform rt = fadeObj.AddComponent<RectTransform>();
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.sizeDelta = Vector2.zero;
            Image fadeImage = fadeObj.AddComponent<Image>();
            fadeImage.color = Color.black;
            fadeImage.raycastTarget = false;

            SerializedObject so = new SerializedObject(manager);
            so.FindProperty("fadeImage").objectReferenceValue = fadeImage;
            so.ApplyModifiedProperties();

            EditorUtility.SetDirty(manager);
        }

        private static void SetupAccessibilityManager()
        {
            GameObject go = new GameObject("[AccessibilityManager]");
            AccessibilityManager manager = go.AddComponent<AccessibilityManager>();

            PostProcessingController pp = Object.FindAnyObjectByType<PostProcessingController>();
            if (pp != null)
            {
                SerializedObject so = new SerializedObject(manager);
                so.FindProperty("postProcessing").objectReferenceValue = pp;
                so.ApplyModifiedProperties();
            }

            EditorUtility.SetDirty(manager);
        }

        private static void SetupPerformanceManager()
        {
            GameObject go = new GameObject("[PerformanceManager]");
            PerformanceManager manager = go.AddComponent<PerformanceManager>();
            EditorUtility.SetDirty(manager);
        }

        private static void SetupLocalizationManager()
        {
            GameObject go = new GameObject("[LocalizationManager]");
            LocalizationManager manager = go.AddComponent<LocalizationManager>();
            EditorUtility.SetDirty(manager);
        }

        private static void SetupScreenFade()
        {
            // Handled by UITransitionManager setup
        }

        private static void SetupMinimapLayer()
        {
            // Layer creation is handled by MinimapLayerSetup; this is a no-op placeholder
            // so the full GameWorld setup can be run end-to-end in any order.
        }

        private static void SetupCinemachineCamera()
        {
            Camera mainCam = Camera.main;
            if (mainCam == null)
            {
                GameObject camObj = new GameObject("Main Camera");
                camObj.tag = "MainCamera";
                mainCam = camObj.AddComponent<Camera>();
                camObj.AddComponent<AudioListener>();
            }

            GameObject player = GameObject.Find("Player");
            Transform cameraTarget = null;
            if (player != null)
            {
                cameraTarget = player.transform.Find("CameraTarget");
                if (cameraTarget == null)
                {
                    GameObject targetObj = new GameObject("CameraTarget");
                    targetObj.transform.SetParent(player.transform, false);
                    targetObj.transform.localPosition = new Vector3(0f, 1.5f, 0f);
                    cameraTarget = targetObj.transform;
                }
            }

            Contract.CameraSystem.ThirdPersonCamera thirdPersonCamera =
                mainCam.GetComponent<Contract.CameraSystem.ThirdPersonCamera>();
            if (thirdPersonCamera == null)
            {
                thirdPersonCamera = mainCam.gameObject.AddComponent<Contract.CameraSystem.ThirdPersonCamera>();
            }

            InputReader inputReader = AssetDatabase.LoadAssetAtPath<InputReader>("Assets/Settings/InputReader.asset");
            SerializedObject serializedCamera = new SerializedObject(thirdPersonCamera);
            SerializedProperty inputReaderProperty = serializedCamera.FindProperty("inputReader");
            SerializedProperty targetProperty = serializedCamera.FindProperty("target");
            if (inputReaderProperty != null) inputReaderProperty.objectReferenceValue = inputReader;
            if (targetProperty != null) targetProperty.objectReferenceValue = cameraTarget;
            serializedCamera.ApplyModifiedPropertiesWithoutUndo();

            EditorUtility.SetDirty(thirdPersonCamera);
            EditorUtility.SetDirty(mainCam);
        }

        private static void SetupAudioMixer()
        {
            string mixerPath = "Assets/Audio/ContractMixer.mixer";

            // Create Audio folder if needed
            if (!AssetDatabase.IsValidFolder("Assets/Audio"))
            {
                AssetDatabase.CreateFolder("Assets", "Audio");
            }

            // Audio Mixer is created manually via CONTRACT/Setup/Create Audio Mixer.
            AudioMixer mixer = AssetDatabase.LoadAssetAtPath<AudioMixer>(mixerPath);
            if (mixer == null)
            {
                Debug.LogWarning("[CONTRACT] Audio mixer not found at " + mixerPath +
                                 ". Create it via Assets > Create > Audio Mixer.");
                return;
            }

            // Assign to AudioService
            AudioService audioService = Object.FindAnyObjectByType<AudioService>();
            if (audioService != null)
            {
                SerializedObject so = new SerializedObject(audioService);
                so.FindProperty("audioMixer").objectReferenceValue = mixer;
                so.ApplyModifiedProperties();
                EditorUtility.SetDirty(audioService);
            }

            AssetDatabase.SaveAssets();
        }

        private static void SetupVFXSystem()
        {
            GameObject go = new GameObject("[VFXManager]");
            VFXManager vfxManager = go.AddComponent<VFXManager>();

            // Create particle prefabs
            string prefabPath = "Assets/Prefabs/VFX/";
            if (!AssetDatabase.IsValidFolder(prefabPath))
            {
                string guid = AssetDatabase.CreateFolder("Assets/Prefabs", "VFX");
                prefabPath = AssetDatabase.GUIDToAssetPath(guid) + "/";
            }

            vfxManager.InteractionParticles = GetOrCreateParticlePrefab(prefabPath + "InteractionParticles.prefab", "Interaction", ParticlePresets.CreateInteractionParticles);
            vfxManager.SuspicionParticles = GetOrCreateParticlePrefab(prefabPath + "SuspicionParticles.prefab", "Suspicion", ParticlePresets.CreateSuspicionParticles);
            vfxManager.AlertParticles = GetOrCreateParticlePrefab(prefabPath + "AlertParticles.prefab", "Alert", ParticlePresets.CreateAlertParticles);
            vfxManager.ExtractionParticles = GetOrCreateParticlePrefab(prefabPath + "ExtractionParticles.prefab", "Extraction", ParticlePresets.CreateExtractionParticles);
            vfxManager.FootstepParticles = GetOrCreateParticlePrefab(prefabPath + "FootstepParticles.prefab", "Footstep", ParticlePresets.CreateFootstepParticles);
            vfxManager.ImpactParticles = GetOrCreateParticlePrefab(prefabPath + "ImpactParticles.prefab", "Impact", ParticlePresets.CreateImpactParticles);
            vfxManager.HackParticles = GetOrCreateParticlePrefab(prefabPath + "HackParticles.prefab", "Hack", ParticlePresets.CreateHackParticles);
            vfxManager.PickupParticles = GetOrCreateParticlePrefab(prefabPath + "PickupParticles.prefab", "Pickup", ParticlePresets.CreatePickupParticles);

            EditorUtility.SetDirty(vfxManager);
        }

        private static void SetupPostProcessingController()
        {
            GameObject go = new GameObject("[PostProcessingController]");
            PostProcessingController controller = go.AddComponent<PostProcessingController>();

            Volume volume = go.AddComponent<Volume>();
            volume.isGlobal = true;
            volume.priority = 100;

            VolumeProfile profile = ScriptableObject.CreateInstance<VolumeProfile>();
            volume.profile = profile;

            Vignette vignette = profile.Add<Vignette>(true);
            vignette.intensity.value = 0.32f;
            vignette.smoothness.value = 0.45f;
            vignette.color.value = new Color(0.01f, 0.01f, 0.015f);
            vignette.active = true;

            ChromaticAberration ca = profile.Add<ChromaticAberration>(true);
            ca.intensity.value = 0f;
            ca.active = true;

            ColorAdjustments ca2 = profile.Add<ColorAdjustments>(true);
            ca2.postExposure.value = 0.15f;
            ca2.contrast.value = 12f;
            ca2.saturation.value = -12f;
            ca2.active = true;

            controller.GlobalVolume = volume;
            controller.Profile = profile;

            EditorUtility.SetDirty(controller);
            EditorUtility.SetDirty(volume);
            EditorUtility.SetDirty(profile);
        }

        private static void SetupGameManager()
        {
            GameObject gm = GameObject.Find("[GameManager]");
            if (gm == null)
            {
                gm = new GameObject("[GameManager]");
            }

            if (gm.GetComponent<GameManager>() == null)
            {
                gm.AddComponent<GameManager>();
            }

            if (gm.GetComponent<SceneLoader>() == null)
            {
                gm.AddComponent<SceneLoader>();
            }

            gm.tag = "GameController";
            UnityEditor.EditorUtility.SetDirty(gm);
        }

        private static void SetupAudioService()
        {
            GameObject go = new GameObject("[AudioService]");
            AudioService audio = go.AddComponent<AudioService>();
            // AudioLibrary would be assigned in Inspector
            UnityEngine.Object.DontDestroyOnLoad(go);
        }

        private static void SetupSaveManager()
        {
            GameObject go = new GameObject("[SaveManager]");
            go.AddComponent<SaveManager>();
            UnityEngine.Object.DontDestroyOnLoad(go);
        }

        private static void SetupMissionManager()
        {
            GameObject go = new GameObject("[MissionManager]");
            go.AddComponent<MissionManager>();
            UnityEngine.Object.DontDestroyOnLoad(go);
        }

        private static void SetupRegionManager()
        {
            GameObject go = new GameObject("[RegionManager]");
            go.AddComponent<RegionManager>();
            UnityEngine.Object.DontDestroyOnLoad(go);
        }

        private static void SetupCharacterManager()
        {
            GameObject go = new GameObject("[CharacterSelectionManager]");
            go.AddComponent<CharacterSelectionManager>();
            UnityEngine.Object.DontDestroyOnLoad(go);
        }

        private static void SetupEconomyManager()
        {
            GameObject go = new GameObject("[EconomyManager]");
            go.AddComponent<EconomyManager>();
            UnityEngine.Object.DontDestroyOnLoad(go);
        }

        private static void SetupUpgradeManager()
        {
            GameObject go = new GameObject("[UpgradeManager]");
            go.AddComponent<UpgradeManager>();
            UnityEngine.Object.DontDestroyOnLoad(go);
        }

        private static void SetupMapManager()
        {
            GameObject go = new GameObject("[MapManager]");
            MapManager map = go.AddComponent<MapManager>();

            // MapManager creates cameras in Awake/Start
            // Just ensure it has the reference
            UnityEngine.Object.DontDestroyOnLoad(go);
        }

        private static void SetupNpcManager()
        {
            GameObject go = new GameObject("[NpcManager]");
            go.AddComponent<NpcManager>();
            UnityEngine.Object.DontDestroyOnLoad(go);
        }

        private static void SetupUIManager()
        {
            GameObject go = new GameObject("[UIManager]");
            UIManager ui = go.AddComponent<UIManager>();
            ui.gameObject.AddComponent<UpgradeShopController>();

            // Create main canvas if missing
            Canvas canvas = UnityEngine.Object.FindAnyObjectByType<Canvas>();
            if (canvas == null)
            {
                GameObject canvasObj = new GameObject("ContractUI");
                canvas = canvasObj.AddComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                canvasObj.AddComponent<UnityEngine.UI.CanvasScaler>().uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                canvasObj.AddComponent<UnityEngine.UI.GraphicRaycaster>();
            }

            // Create EventSystem
            if (UnityEngine.Object.FindAnyObjectByType<UnityEngine.EventSystems.EventSystem>() == null)
            {
                GameObject es = new GameObject("EventSystem");
                es.AddComponent<UnityEngine.EventSystems.EventSystem>();
                es.AddComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>();
            }

            UnityEngine.Object.DontDestroyOnLoad(go);
        }

        private static void SetupPlayer()
        {
            GameObject player = GameObject.Find("Player");
            if (player == null)
            {
                // Player prefab should be instantiated from Prefabs/Player/Player.prefab
                UnityEngine.Debug.LogWarning("[CONTRACT] Player not found in scene. Instantiate from Prefabs/Player/Player.prefab");
            }
            else
            {
                // Ensure components
                if (player.GetComponent<ThirdPersonController>() == null)
                    player.AddComponent<ThirdPersonController>();
                if (player.GetComponent<PlayerAnimationDriver>() == null)
                    player.AddComponent<PlayerAnimationDriver>();
                if (player.GetComponent<InteractionController>() == null)
                    player.AddComponent<InteractionController>();
                if (player.GetComponent<HUDController>() == null)
                    player.AddComponent<HUDController>();
                if (player.GetComponent<InventoryUI>() == null)
                    player.AddComponent<InventoryUI>();
                if (player.GetComponent<PauseMenuController>() == null)
                    player.AddComponent<PauseMenuController>();

                // Add MapIcon for minimap
                MapIcon icon = player.GetComponent<MapIcon>();
                if (icon == null)
                {
                    icon = player.AddComponent<MapIcon>();
                    icon.Configure("Player", MapIconType.Player, "Player");
                }

                // Set Minimap layer on player's MapIcon
                int minimapLayer = LayerMask.NameToLayer("Minimap");
                if (minimapLayer >= 0)
                {
                    icon.gameObject.layer = minimapLayer;
                }
            }
        }

        private static void SetupEnvironment()
        {
            GameObject env = GameObject.Find("Environment");
            if (env == null)
            {
                env = new GameObject("Environment");
            }

            // Bake built-in NavMesh data for the environment hierarchy
            UnityEditor.AI.NavMeshBuilder.BuildNavMesh();

            // Add Interactables container
            GameObject interactables = GameObject.Find("Interactables");
            if (interactables == null)
            {
                interactables = new GameObject("Interactables");
                interactables.transform.SetParent(env.transform, false);
            }

            // Add WorldMarkers container
            GameObject markers = GameObject.Find("WorldMarkers");
            if (markers == null)
            {
                markers = new GameObject("WorldMarkers");
                markers.transform.SetParent(env.transform, false);
            }
        }

        private static GameObject GetOrCreateParticlePrefab(string path, string name, System.Func<ParticleSystem, ParticleSystem> createFn)
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab != null) return prefab;

            GameObject go = new GameObject(name);
            ParticleSystem ps = go.AddComponent<ParticleSystem>();
            ps = createFn(ps);

            string folder = System.IO.Path.GetDirectoryName(path);
            if (!AssetDatabase.IsValidFolder(folder))
            {
                string[] parts = folder.Split('/');
                string current = parts[0];
                for (int i = 1; i < parts.Length; i++)
                {
                    string next = current + "/" + parts[i];
                    if (!AssetDatabase.IsValidFolder(next))
                    {
                        AssetDatabase.CreateFolder(current, parts[i]);
                    }
                    current = next;
                }
            }

            PrefabUtility.SaveAsPrefabAsset(go, path);
            Object.DestroyImmediate(go);

            return AssetDatabase.LoadAssetAtPath<GameObject>(path);
        }

        private static class ParticlePresets
        {
            public static ParticleSystem CreateInteractionParticles(ParticleSystem ps)
            {
                var main = ps.main;
                main.duration = 0.5f;
                main.loop = false;
                main.startLifetime = new ParticleSystem.MinMaxCurve(0.3f, 0.8f);
                main.startSpeed = new ParticleSystem.MinMaxCurve(1f, 3f);
                main.startSize = new ParticleSystem.MinMaxCurve(0.1f, 0.3f);
                main.startColor = new ParticleSystem.MinMaxGradient(new Color(0.2f, 0.8f, 1f, 1f), new Color(0f, 0.5f, 1f, 0f));
                main.gravityModifier = 0f;

                var emission = ps.emission;
                emission.rateOverTime = 0f;
                emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 10, 20) });

                var shape = ps.shape;
                shape.shapeType = ParticleSystemShapeType.Sphere;
                shape.radius = 0.2f;

                var velocity = ps.velocityOverLifetime;
                velocity.enabled = true;
                velocity.space = ParticleSystemSimulationSpace.World;
                velocity.y = new ParticleSystem.MinMaxCurve(-1f, 1f);

                var colorOverLifetime = ps.colorOverLifetime;
                colorOverLifetime.enabled = true;
                colorOverLifetime.color = new ParticleSystem.MinMaxGradient(Color.white, new Color(1f, 1f, 1f, 0f));

                return ps;
            }

            public static ParticleSystem CreateSuspicionParticles(ParticleSystem ps)
            {
                var main = ps.main;
                main.duration = 1f;
                main.loop = true;
                main.startLifetime = new ParticleSystem.MinMaxCurve(0.5f, 1.5f);
                main.startSpeed = new ParticleSystem.MinMaxCurve(0.5f, 1.5f);
                main.startSize = new ParticleSystem.MinMaxCurve(0.05f, 0.15f);
                main.startColor = new ParticleSystem.MinMaxGradient(new Color(1f, 0.8f, 0f, 0.6f), new Color(1f, 0.3f, 0f, 0f));
                main.gravityModifier = -0.1f;

                var emission = ps.emission;
                emission.rateOverTime = 5f;

                var shape = ps.shape;
                shape.shapeType = ParticleSystemShapeType.Cone;
                shape.angle = 30f;
                shape.radius = 0.1f;

                var velocity = ps.velocityOverLifetime;
                velocity.enabled = true;
                velocity.space = ParticleSystemSimulationSpace.Local;
                velocity.y = new ParticleSystem.MinMaxCurve(0.5f, 1f);

                return ps;
            }

            public static ParticleSystem CreateAlertParticles(ParticleSystem ps)
            {
                var main = ps.main;
                main.duration = 0.3f;
                main.loop = false;
                main.startLifetime = new ParticleSystem.MinMaxCurve(0.2f, 0.5f);
                main.startSpeed = new ParticleSystem.MinMaxCurve(5f, 10f);
                main.startSize = new ParticleSystem.MinMaxCurve(0.2f, 0.5f);
                main.startColor = new ParticleSystem.MinMaxGradient(new Color(1f, 0.1f, 0.1f, 1f), new Color(1f, 0f, 0f, 0f));
                main.gravityModifier = 0f;

                var emission = ps.emission;
                emission.rateOverTime = 0f;
                emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 30, 50) });

                var shape = ps.shape;
                shape.shapeType = ParticleSystemShapeType.Sphere;
                shape.radius = 0.5f;

                var velocity = ps.velocityOverLifetime;
                velocity.enabled = true;
                velocity.space = ParticleSystemSimulationSpace.World;
                velocity.y = new ParticleSystem.MinMaxCurve(-2f, 2f);

                return ps;
            }

            public static ParticleSystem CreateExtractionParticles(ParticleSystem ps)
            {
                var main = ps.main;
                main.duration = 2f;
                main.loop = true;
                main.startLifetime = new ParticleSystem.MinMaxCurve(1f, 2f);
                main.startSpeed = new ParticleSystem.MinMaxCurve(1f, 3f);
                main.startSize = new ParticleSystem.MinMaxCurve(0.2f, 0.5f);
                main.startColor = new ParticleSystem.MinMaxGradient(new Color(0f, 1f, 0.3f, 0.8f), new Color(0f, 0.5f, 0.2f, 0f));
                main.gravityModifier = -0.2f;

                var emission = ps.emission;
                emission.rateOverTime = 15f;

                var shape = ps.shape;
                shape.shapeType = ParticleSystemShapeType.Circle;
                shape.radius = 1.5f;
                shape.position = new Vector3(0f, 0.1f, 0f);
                shape.rotation = new Vector3(90f, 0f, 0f);

                var velocity = ps.velocityOverLifetime;
                velocity.enabled = true;
                velocity.space = ParticleSystemSimulationSpace.Local;
                velocity.y = new ParticleSystem.MinMaxCurve(0.5f, 1.5f);

                var rotation = ps.rotationOverLifetime;
                rotation.enabled = true;
                rotation.z = new ParticleSystem.MinMaxCurve(-45f, 45f);

                return ps;
            }

            public static ParticleSystem CreateFootstepParticles(ParticleSystem ps)
            {
                var main = ps.main;
                main.duration = 0.2f;
                main.loop = false;
                main.startLifetime = new ParticleSystem.MinMaxCurve(0.1f, 0.3f);
                main.startSpeed = new ParticleSystem.MinMaxCurve(0.5f, 1.5f);
                main.startSize = new ParticleSystem.MinMaxCurve(0.05f, 0.15f);
                main.startColor = new ParticleSystem.MinMaxGradient(new Color(0.4f, 0.3f, 0.2f, 0.5f), new Color(0.3f, 0.2f, 0.1f, 0f));
                main.gravityModifier = 1f;

                var emission = ps.emission;
                emission.rateOverTime = 0f;
                emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 3, 8) });

                var shape = ps.shape;
                shape.shapeType = ParticleSystemShapeType.Hemisphere;
                shape.radius = 0.1f;

                var velocity = ps.velocityOverLifetime;
                velocity.enabled = true;
                velocity.space = ParticleSystemSimulationSpace.World;
                velocity.y = new ParticleSystem.MinMaxCurve(0.5f, 1.5f);

                return ps;
            }

            public static ParticleSystem CreateImpactParticles(ParticleSystem ps)
            {
                var main = ps.main;
                main.duration = 0.3f;
                main.loop = false;
                main.startLifetime = new ParticleSystem.MinMaxCurve(0.2f, 0.5f);
                main.startSpeed = new ParticleSystem.MinMaxCurve(2f, 5f);
                main.startSize = new ParticleSystem.MinMaxCurve(0.1f, 0.3f);
                main.startColor = new ParticleSystem.MinMaxGradient(new Color(0.6f, 0.5f, 0.4f, 1f), new Color(0.4f, 0.3f, 0.2f, 0f));
                main.gravityModifier = 1f;

                var emission = ps.emission;
                emission.rateOverTime = 0f;
                emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 15, 30) });

                var shape = ps.shape;
                shape.shapeType = ParticleSystemShapeType.Hemisphere;
                shape.radius = 0.1f;

                var velocity = ps.velocityOverLifetime;
                velocity.enabled = true;
                velocity.space = ParticleSystemSimulationSpace.World;
                velocity.y = new ParticleSystem.MinMaxCurve(1f, 3f);

                return ps;
            }

            public static ParticleSystem CreateHackParticles(ParticleSystem ps)
            {
                var main = ps.main;
                main.duration = 1f;
                main.loop = true;
                main.startLifetime = new ParticleSystem.MinMaxCurve(0.3f, 0.8f);
                main.startSpeed = new ParticleSystem.MinMaxCurve(0.5f, 1.5f);
                main.startSize = new ParticleSystem.MinMaxCurve(0.03f, 0.08f);
                main.startColor = new ParticleSystem.MinMaxGradient(new Color(0f, 1f, 1f, 0.7f), new Color(0f, 0.5f, 1f, 0f));
                main.gravityModifier = -0.1f;

                var emission = ps.emission;
                emission.rateOverTime = 20f;

                var shape = ps.shape;
                shape.shapeType = ParticleSystemShapeType.Box;
                shape.scale = new Vector3(0.5f, 0.5f, 0.1f);

                var velocity = ps.velocityOverLifetime;
                velocity.enabled = true;
                velocity.space = ParticleSystemSimulationSpace.Local;
                velocity.z = new ParticleSystem.MinMaxCurve(-0.5f, 0.5f);

                return ps;
            }

            public static ParticleSystem CreatePickupParticles(ParticleSystem ps)
            {
                var main = ps.main;
                main.duration = 0.5f;
                main.loop = false;
                main.startLifetime = new ParticleSystem.MinMaxCurve(0.5f, 1f);
                main.startSpeed = new ParticleSystem.MinMaxCurve(1f, 2f);
                main.startSize = new ParticleSystem.MinMaxCurve(0.15f, 0.3f);
                main.startColor = new ParticleSystem.MinMaxGradient(new Color(1f, 0.9f, 0.2f, 1f), new Color(1f, 0.5f, 0f, 0f));
                main.gravityModifier = -0.3f;

                var emission = ps.emission;
                emission.rateOverTime = 0f;
                emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 20, 40) });

                var shape = ps.shape;
                shape.shapeType = ParticleSystemShapeType.Sphere;
                shape.radius = 0.3f;

                var velocity = ps.velocityOverLifetime;
                velocity.enabled = true;
                velocity.space = ParticleSystemSimulationSpace.World;
                velocity.y = new ParticleSystem.MinMaxCurve(1f, 3f);

                var rotation = ps.rotationOverLifetime;
                rotation.enabled = true;
                rotation.z = new ParticleSystem.MinMaxCurve(-180f, 180f);

                return ps;
            }
        }
    }
}