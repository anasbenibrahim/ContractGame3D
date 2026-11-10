using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using Contract.Core;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

namespace Contract.EditorTools
{
    public static class ContractStage1Validator
    {
        private static readonly string[] ScenePaths =
        {
            "Assets/Scenes/MainMenu.unity",
            "Assets/Scenes/CharacterSelection.unity",
            "Assets/Scenes/MissionSelection.unity",
            "Assets/Scenes/GameWorld.unity"
        };

        private static readonly string[] RequiredPlayerActions =
        {
            "Move", "Look", "Sprint", "Crouch", "Interact", "Jump", "OpenMap", "Pause"
        };

        [MenuItem("CONTRACT/Validate/Stage 1")]
        public static void Validate()
        {
            var failures = new List<string>();
            ValidateInput(failures);
            ValidateTagsAndLayers(failures);
            ValidateBuildSettings(failures);
            ValidateScenes(failures);

            if (failures.Count > 0)
            {
                foreach (string failure in failures)
                {
                    Debug.LogError($"[CONTRACT-VALIDATION] {failure}");
                }

                Debug.LogError($"[CONTRACT-VALIDATION] FAILED with {failures.Count} issue(s).");
                if (Application.isBatchMode)
                {
                    EditorApplication.Exit(1);
                }

                return;
            }

            Debug.Log("[CONTRACT-VALIDATION] PASSED: Stage 1 scenes, input, tags, layers, URP and build settings are valid.");
        }

        private static void ValidateInput(ICollection<string> failures)
        {
            InputActionAsset inputActions = AssetDatabase.LoadAssetAtPath<InputActionAsset>("Assets/Settings/ContractInputActions.inputactions");
            if (inputActions == null)
            {
                failures.Add("ContractInputActions.inputactions is missing or failed to import.");
                return;
            }

            InputActionMap playerMap = inputActions.FindActionMap("Player", false);
            if (playerMap == null)
            {
                failures.Add("Input actions are missing the Player map.");
                return;
            }

            foreach (string actionName in RequiredPlayerActions)
            {
                if (playerMap.FindAction(actionName, false) == null)
                {
                    failures.Add($"Input action '{actionName}' is missing.");
                }
            }

            InputAction move = playerMap.FindAction("Move", false);
            if (move != null && !HasMoveComposite(move))
            {
                failures.Add("Move action is missing a 2DVector composite for WASD. Keyboard movement would not produce a direction.");
            }

            InputAction interact = playerMap.FindAction("Interact", false);
            if (interact != null && interact.interactions.Contains("Hold"))
            {
                failures.Add("Interact action still uses a Hold interaction; Stage 1 requires a press interaction.");
            }

            InputActionMap uiMap = inputActions.FindActionMap("UI", false);
            if (uiMap == null)
            {
                failures.Add("Input actions are missing the UI map.");
            }
            else
            {
                InputAction navigate = uiMap.FindAction("Navigate", false);
                if (navigate == null)
                {
                    failures.Add("Input actions are missing the UI Navigate action.");
                }
                else
                {
                    foreach (InputBinding binding in navigate.bindings)
                    {
                        if (!binding.isPartOfComposite)
                        {
                            continue;
                        }

                        if (binding.name != "up" && binding.name != "down" && binding.name != "left" && binding.name != "right")
                        {
                            failures.Add($"UI Navigate composite part '{binding.name}' is invalid.");
                        }
                    }
                }
            }

            InputReader reader = AssetDatabase.LoadAssetAtPath<InputReader>("Assets/Settings/InputReader.asset");
            if (reader == null)
            {
                failures.Add("InputReader.asset is missing.");
            }
            else
            {
                var serializedReader = new SerializedObject(reader);
                SerializedProperty actionsProperty = serializedReader.FindProperty("actions");
                if (actionsProperty == null || actionsProperty.objectReferenceValue == null)
                {
                    failures.Add("InputReader.asset does not reference ContractInputActions.");
                }
            }
        }

        private static bool HasMoveComposite(InputAction move)
        {
            bool hasComposite = false;
            bool hasUp = false;
            bool hasDown = false;
            bool hasLeft = false;
            bool hasRight = false;

            foreach (InputBinding binding in move.bindings)
            {
                if (binding.isComposite && binding.path == "2DVector")
                {
                    hasComposite = true;
                }

                if (!binding.isPartOfComposite)
                {
                    continue;
                }

                if (binding.name == "up")
                {
                    hasUp = true;
                }
                else if (binding.name == "down")
                {
                    hasDown = true;
                }
                else if (binding.name == "left")
                {
                    hasLeft = true;
                }
                else if (binding.name == "right")
                {
                    hasRight = true;
                }
            }

            return hasComposite && hasUp && hasDown && hasLeft && hasRight;
        }

        private static void ValidateTagsAndLayers(ICollection<string> failures)
        {
            foreach (string tag in new[] { "NPC", "MissionSubject", "Interactable", "ExtractionPoint" })
            {
                if (!UnityEditorInternal.InternalEditorUtility.tags.Contains(tag))
                {
                    failures.Add($"Tag '{tag}' is missing.");
                }
            }

            foreach (string layer in new[] { "Player", "NPC", "Interactable", "MissionSubject", "Environment" })
            {
                if (LayerMask.NameToLayer(layer) < 0)
                {
                    failures.Add($"Layer '{layer}' is missing.");
                }
            }
        }

        private static void ValidateBuildSettings(ICollection<string> failures)
        {
            List<string> enabledScenes = EditorBuildSettings.scenes
                .Where(scene => scene.enabled)
                .Select(scene => scene.path)
                .ToList();

            foreach (string scenePath in ScenePaths)
            {
                if (!enabledScenes.Contains(scenePath))
                {
                    failures.Add($"Build settings do not include '{scenePath}'.");
                }
            }
        }

        private static void ValidateScenes(ICollection<string> failures)
        {
            foreach (string scenePath in ScenePaths)
            {
                if (AssetDatabase.LoadAssetAtPath<SceneAsset>(scenePath) == null)
                {
                    failures.Add($"Scene asset '{scenePath}' is missing.");
                    continue;
                }

                Scene scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
                ValidateScene(scenePath, scene, failures);
            }
        }

        private static void ValidateScene(string scenePath, Scene scene, ICollection<string> failures)
        {
            GameObject[] roots = scene.GetRootGameObjects();
            var allObjects = new List<GameObject>();
            foreach (GameObject root in roots)
            {
                allObjects.AddRange(root.GetComponentsInChildren<Transform>(true).Select(transform => transform.gameObject));
            }

            int missingScripts = 0;
            foreach (GameObject gameObject in allObjects)
            {
                Component[] components = gameObject.GetComponents<Component>();
                missingScripts += components.Count(component => component == null);
            }

            if (missingScripts > 0)
            {
                failures.Add($"'{scenePath}' contains {missingScripts} missing script reference(s).");
            }

            if (allObjects.All(gameObject => gameObject.GetComponent<Camera>() == null))
            {
                failures.Add($"'{scenePath}' has no camera.");
            }

            if (allObjects.All(gameObject => gameObject.GetComponent<Light>() == null))
            {
                failures.Add($"'{scenePath}' has no light.");
            }

            if (allObjects.All(gameObject => gameObject.GetComponent<AudioListener>() == null))
            {
                failures.Add($"'{scenePath}' has no audio listener.");
            }

            if (allObjects.All(gameObject => gameObject.GetComponent<Volume>() == null))
            {
                failures.Add($"'{scenePath}' has no global volume.");
            }

            if (allObjects.All(gameObject => gameObject.GetComponent<Canvas>() == null))
            {
                failures.Add($"'{scenePath}' has no UI canvas.");
            }

            EventSystem eventSystem = allObjects.Select(gameObject => gameObject.GetComponent<EventSystem>()).FirstOrDefault(component => component != null);
            if (eventSystem == null)
            {
                failures.Add($"'{scenePath}' has no EventSystem.");
            }
            else
            {
                var inputModule = eventSystem.GetComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>();
                if (inputModule == null)
                {
                    failures.Add($"'{scenePath}' EventSystem does not use InputSystemUIInputModule.");
                }
            }

            TextMeshProUGUI[] texts = allObjects.SelectMany(gameObject => gameObject.GetComponents<TextMeshProUGUI>()).ToArray();
            if (texts.Length == 0)
            {
                failures.Add($"'{scenePath}' has no TextMeshPro text.");
            }

            foreach (TextMeshProUGUI text in texts)
            {
                if (text.font == null)
                {
                    failures.Add($"'{scenePath}' contains a TMP text object without a font asset.");
                }
            }

            Camera camera = allObjects.Select(gameObject => gameObject.GetComponent<Camera>()).FirstOrDefault(component => component != null);
            if (camera != null && camera.GetUniversalAdditionalCameraData() == null)
            {
                failures.Add($"'{scenePath}' camera is missing URP camera data.");
            }

            if (scenePath.EndsWith("GameWorld.unity", StringComparison.Ordinal))
            {
                if (allObjects.All(gameObject => gameObject.name != "PlayerSpawn"))
                {
                    failures.Add("GameWorld scene is missing the PlayerSpawn marker.");
                }
            }

            Debug.Log($"[CONTRACT-VALIDATION] {scenePath}: {allObjects.Count} GameObjects, {texts.Length} TMP labels, missing scripts={missingScripts}.");
        }
    }
}
