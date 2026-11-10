using System;
using System.IO;
using TMPro;
using UnityEditor;
using UnityEditor.PackageManager;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Contract.Core;
using Object = UnityEngine.Object;

namespace Contract.EditorTools
{
    [InitializeOnLoad]
    public static class ContractStage1Setup
    {
        private const string MarkerPath = "ProjectSettings/ContractStage1.done";
        private const string TmpAttemptKey = "Contract.Stage1.TmpAttempts";
        private const int MaxTmpAttempts = 3;

        private static bool isRunning;

        static ContractStage1Setup()
        {
            EditorApplication.delayCall += AutoSetup;
        }

        [MenuItem("CONTRACT/Setup/Rebuild Stage 1 Foundation")]
        public static void Rebuild()
        {
            if (File.Exists(MarkerPath))
            {
                File.Delete(MarkerPath);
            }

            SessionState.SetInt(TmpAttemptKey, 0);
            RunSetup(overwriteExistingScenes: false);
        }

        [MenuItem("CONTRACT/Open Main Menu Scene")]
        public static void OpenMainMenu()
        {
            if (File.Exists("Assets/Scenes/MainMenu.unity"))
            {
                EditorSceneManager.OpenScene("Assets/Scenes/MainMenu.unity");
            }
        }

        private static void AutoSetup()
        {
            if (File.Exists(MarkerPath) || EditorApplication.isPlayingOrWillChangePlaymode)
            {
                return;
            }

            if (EditorApplication.isCompiling || EditorApplication.isUpdating)
            {
                EditorApplication.delayCall += AutoSetup;
                return;
            }

            RunSetup(overwriteExistingScenes: false);
        }

        private static void RunSetup(bool overwriteExistingScenes)
        {
            if (isRunning)
            {
                return;
            }

            isRunning = true;
            try
            {
                if (!TryEnsureTmpEssentials())
                {
                    return;
                }

                Debug.Log("[CONTRACT] Stage 1: configuring folders and project settings...");
                EnsureFolders();
                ConfigurePlayerSettings();
                ConfigureTagsAndLayers();
                ConfigureRenderPipeline();
                ConfigureQuality();

                Material groundMaterial = CreateOrUpdateMaterial(
                    "Assets/Art/Materials/Ground_Dark.mat",
                    new Color(0.045f, 0.05f, 0.062f),
                    0.1f,
                    0.35f);

                Material buildingMaterial = CreateOrUpdateMaterial(
                    "Assets/Art/Materials/Building_Charcoal.mat",
                    new Color(0.085f, 0.09f, 0.105f),
                    0.3f,
                    0.45f);

                Material accentMaterial = CreateOrUpdateMaterial(
                    "Assets/Art/Materials/Accent_Red.mat",
                    new Color(0.55f, 0.035f, 0.05f),
                    0.1f,
                    0.55f,
                    new Color(1.6f, 0.08f, 0.12f));

                Debug.Log("[CONTRACT] Stage 1: configuring input and post processing...");
                InputActionAsset inputActions = ConfigureInput();
                InputReader inputReader = CreateInputReader(inputActions);
                VolumeProfile volumeProfile = CreateOrUpdateVolumeProfile();

                CreateSceneIfMissing(
                    "Assets/Scenes/MainMenu.unity",
                    overwriteExistingScenes,
                    () => CreateMainMenuScene(groundMaterial, buildingMaterial, accentMaterial, volumeProfile));
                CreateSceneIfMissing(
                    "Assets/Scenes/CharacterSelection.unity",
                    overwriteExistingScenes,
                    () => CreateCharacterSelectionScene(groundMaterial, buildingMaterial, accentMaterial, volumeProfile));
                CreateSceneIfMissing(
                    "Assets/Scenes/MissionSelection.unity",
                    overwriteExistingScenes,
                    () => CreateMissionSelectionScene(groundMaterial, buildingMaterial, accentMaterial, volumeProfile));
                CreateSceneIfMissing(
                    "Assets/Scenes/GameWorld.unity",
                    overwriteExistingScenes,
                    () => CreateGameWorldScene(groundMaterial, buildingMaterial, accentMaterial, volumeProfile));

                Debug.Log("[CONTRACT] Stage 1: writing build settings...");
                ConfigureBuildSettings();
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();

                File.WriteAllText(MarkerPath, $"Stage 1 completed {DateTime.UtcNow:O}\n");
                Debug.Log("[CONTRACT] Stage 1 foundation generated: scenes, input, URP, materials, tags and build settings.");

                inputReader = AssetDatabase.LoadAssetAtPath<InputReader>("Assets/Settings/InputReader.asset");
                if (inputReader == null)
                {
                    Debug.LogWarning("[CONTRACT] InputReader asset could not be created automatically. Re-run CONTRACT/Setup/Rebuild Stage 1 Foundation.");
                }
            }
            catch (Exception exception)
            {
                Debug.LogError($"[CONTRACT] Stage 1 setup failed: {exception}");
            }
            finally
            {
                isRunning = false;
            }
        }

        private static bool TryEnsureTmpEssentials()
        {
            if (IsTmpReady())
            {
                return true;
            }

            int attempts = SessionState.GetInt(TmpAttemptKey, 0);
            if (attempts >= MaxTmpAttempts)
            {
                Debug.LogWarning("[CONTRACT] TMP Essential Resources were not detected. Scenes will be generated without temporary overlay text.");
                return true;
            }

            SessionState.SetInt(TmpAttemptKey, attempts + 1);
            string packagePath = FindTmpEssentialsPackage();
            if (string.IsNullOrEmpty(packagePath) || !File.Exists(packagePath))
            {
                Debug.LogWarning("[CONTRACT] Could not locate the TMP Essential Resources package. Continuing without TMP text.");
                return true;
            }

            Debug.Log("[CONTRACT] Importing TextMeshPro Essential Resources...");
            AssetDatabase.ImportPackage(packagePath, false);
            AssetDatabase.Refresh();
            return IsTmpReady();
        }

        private static bool IsTmpReady()
        {
            if (Resources.Load<TMP_Settings>("TMP Settings") != null)
            {
                return true;
            }

            return AssetDatabase.LoadAssetAtPath<TMP_Settings>("Assets/TextMesh Pro/Resources/TMP Settings.asset") != null;
        }

        private static string FindTmpEssentialsPackage()
        {
            UnityEditor.PackageManager.PackageInfo packageInfo = UnityEditor.PackageManager.PackageInfo.FindForAssembly(typeof(TMP_Text).Assembly);
            if (packageInfo == null)
            {
                return null;
            }

            return Path.Combine(packageInfo.resolvedPath, "Package Resources", "TMP Essential Resources.unitypackage");
        }

        private static void EnsureFolders()
        {
            string[] folders =
            {
                "Assets/Art", "Assets/Art/Characters", "Assets/Art/Environment", "Assets/Art/Materials",
                "Assets/Art/Textures", "Assets/Art/Animations", "Assets/Audio", "Assets/Audio/Music", "Assets/Audio/SFX",
                "Assets/Prefabs", "Assets/Prefabs/Player", "Assets/Prefabs/NPCs", "Assets/Prefabs/Missions",
                "Assets/Prefabs/Environment", "Assets/Prefabs/UI", "Assets/Scenes", "Assets/Scripts",
                "Assets/Scripts/Core", "Assets/Scripts/Player", "Assets/Scripts/Camera", "Assets/Scripts/NPCs",
                "Assets/Scripts/Interaction", "Assets/Scripts/Missions", "Assets/Scripts/Economy", "Assets/Scripts/Map",
                "Assets/Scripts/UI", "Assets/Scripts/SaveSystem", "Assets/Scripts/Progression", "Assets/ScriptableObjects",
                "Assets/ScriptableObjects/Characters", "Assets/ScriptableObjects/Missions", "Assets/ScriptableObjects/Regions",
                "Assets/ScriptableObjects/Upgrades", "Assets/UI", "Assets/UI/Icons", "Assets/UI/Fonts", "Assets/UI/Sprites",
                "Assets/Settings"
            };

            foreach (string folder in folders)
            {
                EnsureFolder(folder);
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

        private static void ConfigurePlayerSettings()
        {
            PlayerSettings.companyName = "Contract Games";
            PlayerSettings.productName = "CONTRACT: SEOUL SHADOWS";
            PlayerSettings.bundleVersion = "0.1.0";
            PlayerSettings.colorSpace = ColorSpace.Linear;
            PlayerSettings.runInBackground = true;
            PlayerSettings.defaultScreenWidth = 1920;
            PlayerSettings.defaultScreenHeight = 1080;
            PlayerSettings.fullScreenMode = FullScreenMode.Windowed;
            PlayerSettings.resizableWindow = true;
            PlayerSettings.usePlayerLog = true;
        }

        private static void ConfigureTagsAndLayers()
        {
            Object[] assets = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset");
            if (assets == null || assets.Length == 0)
            {
                return;
            }

            var tagManager = new SerializedObject(assets[0]);
            SerializedProperty tags = tagManager.FindProperty("tags");
            AddTag(tags, "NPC");
            AddTag(tags, "MissionSubject");
            AddTag(tags, "Interactable");
            AddTag(tags, "ExtractionPoint");

            SerializedProperty layers = tagManager.FindProperty("layers");
            AddLayer(layers, 8, "Player");
            AddLayer(layers, 9, "NPC");
            AddLayer(layers, 10, "Interactable");
            AddLayer(layers, 11, "MissionSubject");
            AddLayer(layers, 12, "Environment");
            tagManager.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void AddTag(SerializedProperty tags, string tag)
        {
            for (int i = 0; i < tags.arraySize; i++)
            {
                if (tags.GetArrayElementAtIndex(i).stringValue == tag)
                {
                    return;
                }
            }

            tags.InsertArrayElementAtIndex(tags.arraySize);
            tags.GetArrayElementAtIndex(tags.arraySize - 1).stringValue = tag;
        }

        private static void AddLayer(SerializedProperty layers, int index, string layerName)
        {
            if (layers == null || index < 0 || index >= layers.arraySize)
            {
                return;
            }

            SerializedProperty layer = layers.GetArrayElementAtIndex(index);
            if (string.IsNullOrEmpty(layer.stringValue))
            {
                layer.stringValue = layerName;
            }
        }

        private static void ConfigureRenderPipeline()
        {
            var pipeline = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>("Assets/Settings/PC_RPAsset.asset");
            if (pipeline == null)
            {
                Debug.LogWarning("[CONTRACT] PC_RPAsset was not found; URP graphics settings were left unchanged.");
                return;
            }

            Object[] graphicsAssets = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/GraphicsSettings.asset");
            if (graphicsAssets != null && graphicsAssets.Length > 0)
            {
                var graphicsSettings = new SerializedObject(graphicsAssets[0]);
                SerializedProperty pipelineProperty = graphicsSettings.FindProperty("m_CustomRenderPipeline");
                if (pipelineProperty != null)
                {
                    pipelineProperty.objectReferenceValue = pipeline;
                    graphicsSettings.ApplyModifiedPropertiesWithoutUndo();
                }
            }
        }

        private static void ConfigureQuality()
        {
            string[] names = QualitySettings.names;
            for (int i = 0; i < names.Length; i++)
            {
                if (string.Equals(names[i], "PC", StringComparison.OrdinalIgnoreCase))
                {
                    QualitySettings.SetQualityLevel(i, true);
                    break;
                }
            }

            QualitySettings.vSyncCount = 1;
            QualitySettings.antiAliasing = 4;
            QualitySettings.shadowDistance = 60f;
            QualitySettings.lodBias = 1.5f;
        }

        private static Material CreateOrUpdateMaterial(
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
                    Debug.LogError("[CONTRACT] URP Lit shader was not found; materials could not be created.");
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

        private static InputActionAsset ConfigureInput()
        {
            const string path = "Assets/Settings/ContractInputActions.inputactions";
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
            return AssetDatabase.LoadAssetAtPath<InputActionAsset>(path);
        }

        private static InputReader CreateInputReader(InputActionAsset actions)
        {
            if (actions == null)
            {
                Debug.LogError("[CONTRACT] ContractInputActions.inputactions could not be loaded.");
                return null;
            }

            const string path = "Assets/Settings/InputReader.asset";
            InputReader reader = AssetDatabase.LoadAssetAtPath<InputReader>(path);
            if (reader == null)
            {
                reader = ScriptableObject.CreateInstance<InputReader>();
                AssetDatabase.CreateAsset(reader, path);
            }

            var serializedReader = new SerializedObject(reader);
            SerializedProperty actionsProperty = serializedReader.FindProperty("actions");
            actionsProperty.objectReferenceValue = actions;
            serializedReader.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(reader);
            AssetDatabase.SaveAssets();
            return AssetDatabase.LoadAssetAtPath<InputReader>(path);
        }

        private static VolumeProfile CreateOrUpdateVolumeProfile()
        {
            const string path = "Assets/Settings/ContractPostProcess.asset";
            VolumeProfile profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(path);
            if (profile == null)
            {
                profile = ScriptableObject.CreateInstance<VolumeProfile>();
                AssetDatabase.CreateAsset(profile, path);
            }

            Bloom bloom = GetOrAddVolumeComponent<Bloom>(profile, "Bloom");
            bloom.intensity.Override(0.55f);
            bloom.threshold.Override(1.05f);
            bloom.scatter.Override(0.65f);

            Vignette vignette = GetOrAddVolumeComponent<Vignette>(profile, "Vignette");
            vignette.intensity.Override(0.32f);
            vignette.smoothness.Override(0.45f);
            vignette.color.Override(new Color(0.01f, 0.01f, 0.015f));

            ColorAdjustments colorAdjustments = GetOrAddVolumeComponent<ColorAdjustments>(profile, "Color Adjustments");
            colorAdjustments.postExposure.Override(0.15f);
            colorAdjustments.contrast.Override(12f);
            colorAdjustments.saturation.Override(-12f);

            Tonemapping tonemapping = GetOrAddVolumeComponent<Tonemapping>(profile, "Tonemapping");
            tonemapping.mode.Override(TonemappingMode.ACES);

            EditorUtility.SetDirty(profile);
            AssetDatabase.SaveAssets();
            return AssetDatabase.LoadAssetAtPath<VolumeProfile>(path);
        }

        private static T GetOrAddVolumeComponent<T>(VolumeProfile profile, string componentName) where T : VolumeComponent
        {
            foreach (VolumeComponent component in profile.components)
            {
                if (component is T existing)
                {
                    return existing;
                }
            }

            T created = ScriptableObject.CreateInstance<T>();
            created.name = componentName;
            AssetDatabase.AddObjectToAsset(created, profile);
            profile.components.Add(created);
            return created;
        }

        private static void CreateSceneIfMissing(string scenePath, bool overwriteExistingScenes, Action createScene)
        {
            if (!overwriteExistingScenes && File.Exists(scenePath))
            {
                Debug.Log($"[CONTRACT] Stage 1: kept existing scene '{scenePath}'.");
                return;
            }

            Debug.Log($"[CONTRACT] Stage 1: generating '{scenePath}'...");
            createScene();
        }

        private static void CreateMainMenuScene(
            Material groundMaterial,
            Material buildingMaterial,
            Material accentMaterial,
            VolumeProfile profile)
        {
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            ConfigureSceneLighting(new Color(0.025f, 0.03f, 0.045f), new Color(0.06f, 0.07f, 0.09f), 0.012f);

            CreateCamera(new Vector3(0f, 4.8f, -18f), new Vector3(6f, 0f, 0f), new Color(0.012f, 0.014f, 0.022f));
            CreateSunLight(new Vector3(38f, -28f, 0f));
            CreateGround(groundMaterial, new Vector3(0f, 0f, 18f), new Vector3(6f, 1f, 6f));

            BuildStreetBlock(buildingMaterial, accentMaterial, 0f, 26f, 8, true);
            CreateVolume(profile);
            CreateOverlay("CONTRACT", "SEOUL SHADOWS", "STAGE 1  //  PROJECT FOUNDATION ONLINE");

            EditorSceneManager.SaveScene(scene, "Assets/Scenes/MainMenu.unity");
        }

        private static void CreateCharacterSelectionScene(
            Material groundMaterial,
            Material buildingMaterial,
            Material accentMaterial,
            VolumeProfile profile)
        {
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            ConfigureSceneLighting(new Color(0.018f, 0.022f, 0.032f), new Color(0.05f, 0.06f, 0.08f), 0.018f);

            CreateCamera(new Vector3(0f, 2.1f, -6.5f), new Vector3(5f, 0f, 0f), new Color(0.01f, 0.012f, 0.018f));
            CreateSunLight(new Vector3(35f, -30f, 8f));
            CreateGround(groundMaterial, Vector3.zero, new Vector3(3f, 1f, 3f));

            for (int i = -1; i <= 1; i++)
            {
                CreateCylinder($"Character Pedestal {i + 2}", new Vector3(i * 2.4f, 0.55f, 0f), new Vector3(1.35f, 0.35f, 1.35f), accentMaterial);
            }

            CreateCylinder("Backdrop Column L", new Vector3(-7f, 3f, 4f), new Vector3(1f, 6f, 1f), buildingMaterial);
            CreateCylinder("Backdrop Column R", new Vector3(7f, 3f, 4f), new Vector3(1f, 6f, 1f), buildingMaterial);
            CreateVolume(profile);
            CreateOverlay("CHARACTER SELECT", "ASSET INTEGRATION PENDING", "STAGE 1  //  SCENE READY");

            EditorSceneManager.SaveScene(scene, "Assets/Scenes/CharacterSelection.unity");
        }

        private static void CreateMissionSelectionScene(
            Material groundMaterial,
            Material buildingMaterial,
            Material accentMaterial,
            VolumeProfile profile)
        {
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            ConfigureSceneLighting(new Color(0.015f, 0.018f, 0.026f), new Color(0.045f, 0.055f, 0.075f), 0.015f);

            CreateCamera(new Vector3(0f, 3.4f, -8f), new Vector3(8f, 0f, 0f), new Color(0.008f, 0.01f, 0.016f));
            CreateSunLight(new Vector3(34f, -26f, 12f));
            CreateGround(groundMaterial, Vector3.zero, new Vector3(4f, 1f, 4f));
            CreateBox("Operations Table", new Vector3(0f, 0.9f, 1.2f), new Vector3(5f, 1.8f, 1.4f), buildingMaterial);
            CreateBox("Table Accent", new Vector3(0f, 1.82f, 1.2f), new Vector3(5.1f, 0.04f, 1.45f), accentMaterial);

            for (int i = 0; i < 4; i++)
            {
                CreateBox($"Wall Panel {i + 1}", new Vector3(-7.5f + i * 5f, 3f, 8f), new Vector3(4.6f, 6f, 0.35f), buildingMaterial);
            }

            CreateVolume(profile);
            CreateOverlay("MISSION CONTROL", "CONTRACTS AWAITING AUTHORIZATION", "STAGE 1  //  SCENE READY");

            EditorSceneManager.SaveScene(scene, "Assets/Scenes/MissionSelection.unity");
        }

        private static void CreateGameWorldScene(
            Material groundMaterial,
            Material buildingMaterial,
            Material accentMaterial,
            VolumeProfile profile)
        {
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            ConfigureSceneLighting(new Color(0.035f, 0.045f, 0.07f), new Color(0.07f, 0.085f, 0.12f), 0.009f);

            CreateCamera(new Vector3(0f, 9f, -24f), new Vector3(20f, 0f, 0f), new Color(0.015f, 0.02f, 0.03f));
            CreateSunLight(new Vector3(42f, -30f, 10f));

            var environment = new GameObject("Environment");
            CreateGround(groundMaterial, Vector3.zero, new Vector3(8f, 1f, 8f), environment.transform);
            BuildStreetBlock(buildingMaterial, accentMaterial, 0f, 10f, 7, false, environment.transform);
            BuildStreetBlock(buildingMaterial, accentMaterial, -16f, 10f, 5, false, environment.transform);
            BuildStreetBlock(buildingMaterial, accentMaterial, 16f, 10f, 5, false, environment.transform);

            var spawn = new GameObject("PlayerSpawn");
            spawn.transform.position = new Vector3(0f, 1.2f, -2f);
            spawn.transform.SetParent(environment.transform, false);

            CreateVolume(profile);
            CreateOverlay("DOWNTOWN CITY", "GAMEWORLD PROTOTYPE", "STAGE 1  //  AWAITING PLAYER CONTROLLER");

            EditorSceneManager.SaveScene(scene, "Assets/Scenes/GameWorld.unity");
        }

        private static void ConfigureSceneLighting(Color ambient, Color fog, float fogDensity)
        {
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = ambient;
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Exponential;
            RenderSettings.fogColor = fog;
            RenderSettings.fogDensity = fogDensity;
            RenderSettings.skybox = null;
        }

        private static Camera CreateCamera(Vector3 position, Vector3 eulerAngles, Color background)
        {
            var cameraObject = new GameObject("Main Camera");
            cameraObject.tag = "MainCamera";
            cameraObject.transform.position = position;
            cameraObject.transform.eulerAngles = eulerAngles;

            var camera = cameraObject.AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = background;
            camera.fieldOfView = 58f;
            camera.nearClipPlane = 0.1f;
            camera.farClipPlane = 450f;
            camera.allowHDR = true;
            camera.allowMSAA = true;

            var cameraData = camera.GetUniversalAdditionalCameraData();
            cameraData.renderPostProcessing = true;
            cameraData.antialiasing = AntialiasingMode.FastApproximateAntialiasing;

            cameraObject.AddComponent<AudioListener>();
            return camera;
        }

        private static void CreateSunLight(Vector3 eulerAngles)
        {
            var lightObject = new GameObject("Directional Light");
            lightObject.transform.eulerAngles = eulerAngles;
            var light = lightObject.AddComponent<Light>();
            light.type = LightType.Directional;
            light.color = new Color(1f, 0.89f, 0.78f);
            light.intensity = 1.35f;
            light.shadows = LightShadows.Soft;
            light.shadowStrength = 0.85f;
        }

        private static void CreateGround(Material material, Vector3 position, Vector3 scale, Transform parent = null)
        {
            var ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
            ground.name = "Ground";
            ground.transform.SetParent(parent, false);
            ground.transform.position = position;
            ground.transform.localScale = scale;
            ground.isStatic = true;
            if (material != null)
            {
                ground.GetComponent<Renderer>().sharedMaterial = material;
            }
        }

        private static void BuildStreetBlock(
            Material buildingMaterial,
            Material accentMaterial,
            float centerZ,
            float startZ,
            int count,
            bool menuComposition,
            Transform parent = null)
        {
            var root = new GameObject("Street Block");
            root.transform.SetParent(parent, false);

            for (int i = 0; i < count; i++)
            {
                float z = startZ + i * 9.5f;
                float height = 7f + (i % 4) * 3.4f;
                float width = 5.5f + (i % 3) * 0.9f;

                CreateBox($"Building L {i + 1}", new Vector3(-10.5f, height * 0.5f, z), new Vector3(width, height, 7.5f), buildingMaterial, root.transform);
                CreateBox($"Building R {i + 1}", new Vector3(10.5f, (height + 2f) * 0.5f, z), new Vector3(width, height + 2f, 7.5f), buildingMaterial, root.transform);

                if (i % 2 == 0)
                {
                    CreateBox($"Accent L {i + 1}", new Vector3(-10.5f, height + 0.15f, z), new Vector3(width * 0.9f, 0.08f, 7.6f), accentMaterial, root.transform);
                    CreateBox($"Accent R {i + 1}", new Vector3(10.5f, height + 2.15f, z), new Vector3(width * 0.9f, 0.08f, 7.6f), accentMaterial, root.transform);
                }
            }

            CreateBox("Road", new Vector3(0f, 0.02f, startZ + (count - 1) * 4.75f), new Vector3(13f, 0.06f, count * 9.5f), buildingMaterial, root.transform);
            CreateBox("Center Line", new Vector3(0f, 0.07f, startZ + (count - 1) * 4.75f), new Vector3(0.16f, 0.02f, count * 9.5f), accentMaterial, root.transform);

            if (menuComposition)
            {
                for (int i = 0; i < 5; i++)
                {
                    float z = startZ + 4f + i * 8f;
                    CreateBox($"Skyline Back {i + 1}", new Vector3((i - 2) * 7.5f, 9f + (i % 3) * 5f, z + 18f), new Vector3(4f, 18f + (i % 3) * 10f, 4f), buildingMaterial, root.transform);
                }
            }
        }

        private static void CreateBox(string name, Vector3 position, Vector3 scale, Material material, Transform parent = null)
        {
            var box = GameObject.CreatePrimitive(PrimitiveType.Cube);
            box.name = name;
            box.transform.SetParent(parent, false);
            box.transform.position = position;
            box.transform.localScale = scale;
            box.isStatic = true;
            if (material != null)
            {
                box.GetComponent<Renderer>().sharedMaterial = material;
            }
        }

        private static void CreateCylinder(string name, Vector3 position, Vector3 scale, Material material, Transform parent = null)
        {
            var cylinder = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            cylinder.name = name;
            cylinder.transform.SetParent(parent, false);
            cylinder.transform.position = position;
            cylinder.transform.localScale = scale;
            cylinder.isStatic = true;
            if (material != null)
            {
                cylinder.GetComponent<Renderer>().sharedMaterial = material;
            }
        }

        private static void CreateVolume(VolumeProfile profile)
        {
            if (profile == null)
            {
                return;
            }

            var volumeObject = new GameObject("Global Volume");
            var volume = volumeObject.AddComponent<Volume>();
            volume.isGlobal = true;
            volume.priority = 0f;
            volume.sharedProfile = profile;
        }

        private static void CreateOverlay(string title, string subtitle, string stageLabel)
        {
            if (!IsTmpReady())
            {
                return;
            }

            var canvasObject = new GameObject("UI Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            var canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;

            var scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            var eventSystemObject = new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));

            CreateTextElement(canvas.transform, "Title", title,
                new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f),
                new Vector2(80f, -70f), new Vector2(1100f, 120f), 82f, TextAlignmentOptions.TopLeft, Color.white);

            CreateTextElement(canvas.transform, "Subtitle", subtitle,
                new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f),
                new Vector2(84f, -176f), new Vector2(1100f, 64f), 28f, TextAlignmentOptions.TopLeft,
                new Color(0.82f, 0.16f, 0.2f));

            CreateTextElement(canvas.transform, "Stage Label", stageLabel,
                new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(0f, 0f),
                new Vector2(84f, 56f), new Vector2(1200f, 40f), 18f, TextAlignmentOptions.BottomLeft,
                new Color(0.55f, 0.58f, 0.65f));

            CreateAccentBar(canvas.transform);
        }

        private static void CreateTextElement(
            Transform parent,
            string name,
            string value,
            Vector2 anchorMin,
            Vector2 anchorMax,
            Vector2 pivot,
            Vector2 anchoredPosition,
            Vector2 size,
            float fontSize,
            TextAlignmentOptions alignment,
            Color color)
        {
            var textObject = new GameObject(name, typeof(RectTransform));
            textObject.transform.SetParent(parent, false);

            var rect = textObject.GetComponent<RectTransform>();
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.pivot = pivot;
            rect.anchoredPosition = anchoredPosition;
            rect.sizeDelta = size;

            var text = textObject.AddComponent<TextMeshProUGUI>();
            text.text = value;
            text.fontSize = fontSize;
            text.color = color;
            text.alignment = alignment;
            text.textWrappingMode = TextWrappingModes.NoWrap;
            text.raycastTarget = false;

            if (text.font == null && TMP_Settings.defaultFontAsset != null)
            {
                text.font = TMP_Settings.defaultFontAsset;
            }
        }

        private static void CreateAccentBar(Transform parent)
        {
            var barObject = new GameObject("Accent Bar", typeof(RectTransform), typeof(Image));
            barObject.transform.SetParent(parent, false);

            var rect = barObject.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0f, 0f);
            rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 0.5f);
            rect.anchoredPosition = new Vector2(46f, 0f);
            rect.sizeDelta = new Vector2(4f, -160f);

            var image = barObject.GetComponent<Image>();
            image.color = new Color(0.72f, 0.06f, 0.09f, 0.95f);
            image.raycastTarget = false;
        }

        private static void ConfigureBuildSettings()
        {
            EditorBuildSettings.scenes = new[]
            {
                new EditorBuildSettingsScene("Assets/Scenes/MainMenu.unity", true),
                new EditorBuildSettingsScene("Assets/Scenes/CharacterSelection.unity", true),
                new EditorBuildSettingsScene("Assets/Scenes/MissionSelection.unity", true),
                new EditorBuildSettingsScene("Assets/Scenes/GameWorld.unity", true)
            };
        }
    }
}
