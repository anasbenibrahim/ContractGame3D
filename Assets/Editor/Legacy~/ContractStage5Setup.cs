using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Contract.Audio;
using Contract.Map;
using Contract.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace Contract.EditorTools
{
    [InitializeOnLoad]
    public static class ContractStage5Setup
    {
        private const string MarkerPath = "ProjectSettings/ContractStage5.done";
        private const string Stage1MarkerPath = "ProjectSettings/ContractStage1.done";
        private const string Stage2MarkerPath = "ProjectSettings/ContractStage2.done";
        private const string Stage3MarkerPath = "ProjectSettings/ContractStage3.done";
        private const string Stage4MarkerPath = "ProjectSettings/ContractStage4.done";
        private const string GameWorldPath = "Assets/Scenes/GameWorld.unity";
        private const string MixerPath = "Assets/Audio/ContractMixer.mixer";
        private const string MinimapTexturePath = "Assets/Art/Minimap/MinimapRT.renderTexture";
        private static readonly string[] MixerGroups = { "Master", "Music", "SFX", "Dialogue", "Ambience" };

        private static bool isRunning;

        static ContractStage5Setup()
        {
            EditorApplication.delayCall += AutoSetup;
        }

        [MenuItem("CONTRACT/Setup/Rebuild Stage 5 Map VFX Audio")]
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

            if (!File.Exists(Stage1MarkerPath) || !File.Exists(Stage2MarkerPath)
                || !File.Exists(Stage3MarkerPath) || !File.Exists(Stage4MarkerPath)
                || !File.Exists(GameWorldPath))
            {
                EditorApplication.delayCall += AutoSetup;
                return;
            }

            RunSetup();
        }

        public static void RunSetup()
        {
            if (isRunning)
            {
                return;
            }

            isRunning = true;
            try
            {
                Debug.Log("[CONTRACT] Stage 5: wiring map, VFX, audio, and HUD...");
                EnsureFolder("Assets/Audio");
                EnsureFolder("Assets/Art");
                EnsureFolder("Assets/Art/Minimap");
                EnsureFolder("Assets/Prefabs/VFX");

                EnsureMinimapLayer();
                AudioMixer mixer = EnsureAudioMixer();
                if (mixer == null)
                {
                    Debug.LogError("[CONTRACT] Stage 5 could not create or load the AudioMixer.");
                    return;
                }

                Scene scene = EditorSceneManager.OpenScene(GameWorldPath, OpenSceneMode.Single);
                DestroyRoot(scene, "[VFXManager]");
                DestroyRoot(scene, "[PostProcessingController]");
                DestroyRoot(scene, "[MapManager]");
                DestroyRoot(scene, "[AudioService]");
                EditorSceneManager.SaveScene(scene);

                VFXSystemSetup.SetupVFXSystem();
                scene = EditorSceneManager.GetActiveScene();

                BuildMapSystem(scene);
                BuildAudioSystem(scene, mixer);

                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();

                File.WriteAllText(MarkerPath, DateTime.UtcNow.ToString("o"));
                Debug.Log("[CONTRACT] Stage 5 setup complete.");
            }
            catch (Exception exception)
            {
                Debug.LogError("[CONTRACT] Stage 5 setup failed: " + exception);
            }
            finally
            {
                isRunning = false;
            }
        }

        private static void BuildMapSystem(Scene scene)
        {
            GameObject mapObject = new GameObject("[MapManager]");
            SceneManager.MoveGameObjectToScene(mapObject, scene);
            MapManager mapManager = mapObject.AddComponent<MapManager>();

            int minimapLayer = LayerMask.NameToLayer("Minimap");
            int defaultLayer = LayerMask.NameToLayer("Default");
            int minimapMask = (1 << minimapLayer) | (1 << defaultLayer);

            RenderTexture minimapTexture = EnsureMinimapTexture();

            Camera minimapCamera = CreateMapCamera(mapObject.transform, "MinimapCamera", 60f, minimapMask, -100, true);
            minimapCamera.targetTexture = minimapTexture;

            Camera fullMapCamera = CreateMapCamera(mapObject.transform, "FullMapCamera", 100f, minimapMask, -99, false);

            SerializedObject serializedMap = new SerializedObject(mapManager);
            SetObjectReference(serializedMap, "minimapCamera", minimapCamera);
            SetObjectReference(serializedMap, "fullMapCamera", fullMapCamera);
            SetObjectReference(serializedMap, "minimapRenderTexture", minimapTexture);
            serializedMap.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(mapManager);

            // Map UI (MinimapView / FullMapView) is built by Contract.Editor.World.GangnamMapBuilder.
            EditorUtility.SetDirty(mapObject);
        }

        private static Camera CreateMapCamera(Transform parent, string name, float orthographicSize, int cullingMask, int depth, bool targetMinimap)
        {
            GameObject cameraObject = new GameObject(name);
            cameraObject.transform.SetParent(parent, false);
            cameraObject.transform.localPosition = new Vector3(0f, targetMinimap ? 100f : 140f, 0f);
            cameraObject.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);

            Camera camera = cameraObject.AddComponent<Camera>();
            camera.orthographic = true;
            camera.orthographicSize = orthographicSize;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.02f, 0.02f, 0.03f, 1f);
            camera.cullingMask = cullingMask;
            camera.depth = depth;
            camera.enabled = targetMinimap;
            camera.useOcclusionCulling = false;
            camera.allowHDR = false;
            camera.allowMSAA = false;
            return camera;
        }

        private static RenderTexture EnsureMinimapTexture()
        {
            RenderTexture texture = AssetDatabase.LoadAssetAtPath<RenderTexture>(MinimapTexturePath);
            if (texture != null)
            {
                return texture;
            }

            texture = new RenderTexture(512, 512, 16, RenderTextureFormat.ARGB32)
            {
                name = "MinimapRT",
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp
            };
            AssetDatabase.CreateAsset(texture, MinimapTexturePath);
            AssetDatabase.SaveAssets();
            return texture;
        }

        private static void BuildAudioSystem(Scene scene, AudioMixer mixer)
        {
            GameObject audioObject = new GameObject("[AudioService]");
            SceneManager.MoveGameObjectToScene(audioObject, scene);
            AudioService audioService = audioObject.AddComponent<AudioService>();

            AudioSource musicSource = CreateAudioSource(audioObject.transform, "MusicSource", true);
            AudioSource sfxSource = CreateAudioSource(audioObject.transform, "SFXSource", false);
            AudioSource dialogueSource = CreateAudioSource(audioObject.transform, "DialogueSource", false);
            AudioSource ambienceSource = CreateAudioSource(audioObject.transform, "AmbienceSource", true);

            SetAudioReference(audioService, "musicSource", musicSource);
            SetAudioReference(audioService, "sfxSource", sfxSource);
            SetAudioReference(audioService, "dialogueSource", dialogueSource);
            SetAudioReference(audioService, "ambienceSource", ambienceSource);
            SetAudioReference(audioService, "audioMixer", mixer);
            EditorUtility.SetDirty(audioService);
        }


        private static void SetAudioReference(AudioService audioService, string propertyName, Object value)
        {
            PropertyInfo property = typeof(AudioService).GetProperty(
                propertyName,
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            if (property == null)
            {
                Debug.LogWarning("[CONTRACT] AudioService property not found: " + propertyName);
                return;
            }

            property.SetValue(audioService, value);
        }

        private static AudioSource CreateAudioSource(Transform parent, string name, bool loop)
        {
            GameObject sourceObject = new GameObject(name);
            sourceObject.transform.SetParent(parent, false);
            AudioSource source = sourceObject.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.loop = loop;
            source.spatialBlend = 0f;
            return source;
        }

        private static void SetObjectReference(SerializedObject serializedObject, string propertyName, Object value)
        {
            SerializedProperty property = serializedObject.FindProperty(propertyName);
            if (property == null)
            {
                Debug.LogWarning("[CONTRACT] Serialized property not found: " + propertyName);
                return;
            }

            property.objectReferenceValue = value;
        }

        private static void EnsureMinimapLayer()
        {
            SerializedObject tagManager = new SerializedObject(
                AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);
            SerializedProperty layers = tagManager.FindProperty("layers");

            for (int i = 8; i < 32; i++)
            {
                SerializedProperty layer = layers.GetArrayElementAtIndex(i);
                if (layer.stringValue == "Minimap")
                {
                    return;
                }
            }

            for (int i = 8; i < 32; i++)
            {
                SerializedProperty layer = layers.GetArrayElementAtIndex(i);
                if (string.IsNullOrEmpty(layer.stringValue))
                {
                    layer.stringValue = "Minimap";
                    tagManager.ApplyModifiedProperties();
                    Debug.Log("[CONTRACT] Added 'Minimap' layer at index " + i + ".");
                    return;
                }
            }

            Debug.LogError("[CONTRACT] No free layer slot for the Minimap layer.");
        }

        private static AudioMixer EnsureAudioMixer()
        {
            Type controllerType = Type.GetType("UnityEditor.Audio.AudioMixerController, UnityEditor.CoreModule");
            if (controllerType == null)
            {
                Debug.LogError("[CONTRACT] AudioMixerController editor type was not found.");
                return null;
            }

            AudioMixer mixer = AssetDatabase.LoadAssetAtPath<AudioMixer>(MixerPath);
            object controller = mixer == null ? null : AssetDatabase.LoadAssetAtPath(MixerPath, controllerType);
            if (mixer == null || controller == null)
            {
                MethodInfo createMethod = controllerType.GetMethod(
                    "CreateMixerControllerAtPath",
                    BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
                if (createMethod == null)
                {
                    Debug.LogError("[CONTRACT] CreateMixerControllerAtPath was not found.");
                    return null;
                }

                controller = createMethod.Invoke(null, new object[] { MixerPath });
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
                mixer = AssetDatabase.LoadAssetAtPath<AudioMixer>(MixerPath);
                controller = mixer == null ? null : AssetDatabase.LoadAssetAtPath(MixerPath, controllerType);
            }

            if (mixer == null || controller == null)
            {
                Debug.LogError("[CONTRACT] Failed to load the AudioMixer asset after creation.");
                return null;
            }

            ConfigureMixer(controllerType, controller, mixer);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            return mixer;
        }

        private static void ConfigureMixer(Type controllerType, object controller, AudioMixer mixer)
        {
            PropertyInfo masterProperty = controllerType.GetProperty("masterGroup", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            object master = masterProperty.GetValue(controller);
            ((Object)master).name = "Master";

            MethodInfo getAllGroups = controllerType.GetMethod("GetAllAudioGroupsSlow", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            MethodInfo createGroup = controllerType.GetMethod("CreateNewGroup", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            Type groupType = Type.GetType("UnityEditor.Audio.AudioMixerGroupController, UnityEditor.CoreModule");

            List<object> groups = ((IEnumerable)getAllGroups.Invoke(controller, null)).Cast<object>().ToList();
            foreach (string groupName in MixerGroups)
            {
                if (groupName == "Master")
                {
                    continue;
                }

                if (groups.Any(group => ((Object)group).name == groupName))
                {
                    continue;
                }

                groups.Add(createGroup.Invoke(controller, new object[] { groupName, false }));
            }

            List<object> children = groups
                .Where(group => ((Object)group).name != "Master")
                .ToList();
            Array childArray = Array.CreateInstance(groupType, children.Count);
            for (int i = 0; i < children.Count; i++)
            {
                childArray.SetValue(children[i], i);
            }

            groupType.GetProperty("children").SetValue(master, childArray);

            PropertyInfo exposedProperty = controllerType.GetProperty("exposedParameters", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            Type exposedType = Type.GetType("UnityEditor.Audio.ExposedAudioParameter, UnityEditor.CoreModule");
            Type groupPathType = Type.GetType("UnityEditor.Audio.AudioGroupParameterPath, UnityEditor.CoreModule");
            MethodInfo addExposed = controllerType.GetMethod("AddExposedParameter", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            MethodInfo getVolumeGuid = groupType.GetMethod("GetGUIDForVolume", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);

            Array existing = (Array)exposedProperty.GetValue(controller);
            var desiredNames = new Dictionary<string, string>();
            foreach (object group in groups)
            {
                string groupName = ((Object)group).name;
                if (!MixerGroups.Contains(groupName) || groupName == "Master" && !MixerGroups.Contains("Master"))
                {
                    continue;
                }

                object guid = getVolumeGuid.Invoke(group, null);
                desiredNames[guid.ToString()] = groupName + "Volume";

                if (!ContainsExposedGuid(exposedType, existing, guid.ToString()))
                {
                    object path = Activator.CreateInstance(groupPathType, new object[] { group, guid });
                    addExposed.Invoke(controller, new object[] { path });
                    existing = (Array)exposedProperty.GetValue(controller);
                }
            }

            Array renamed = Array.CreateInstance(exposedType, existing.Length);
            FieldInfo guidField = exposedType.GetField("guid");
            FieldInfo nameField = exposedType.GetField("name");
            for (int i = 0; i < existing.Length; i++)
            {
                object entry = existing.GetValue(i);
                object guid = guidField.GetValue(entry);
                object renamedEntry = Activator.CreateInstance(exposedType);
                guidField.SetValue(renamedEntry, guid);
                nameField.SetValue(renamedEntry, desiredNames.TryGetValue(guid.ToString(), out string name)
                    ? name
                    : nameField.GetValue(entry));
                renamed.SetValue(renamedEntry, i);
            }

            exposedProperty.SetValue(controller, renamed);
            EditorUtility.SetDirty(mixer);
        }

        private static bool ContainsExposedGuid(Type exposedType, Array entries, string guid)
        {
            FieldInfo guidField = exposedType.GetField("guid");
            foreach (object entry in entries)
            {
                if (guidField.GetValue(entry).ToString() == guid)
                {
                    return true;
                }
            }

            return false;
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

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path))
            {
                return;
            }

            string parent = Path.GetDirectoryName(path)?.Replace('\\', '/');
            string name = Path.GetFileName(path);
            if (!string.IsNullOrEmpty(parent) && !string.IsNullOrEmpty(name))
            {
                if (!AssetDatabase.IsValidFolder(parent))
                {
                    EnsureFolder(parent);
                }

                AssetDatabase.CreateFolder(parent, name);
            }
        }
    }
}
