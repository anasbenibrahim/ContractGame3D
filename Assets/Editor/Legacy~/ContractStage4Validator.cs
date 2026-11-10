using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Contract.Core;
using Contract.Interaction;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace Contract.EditorTools
{
    public static class ContractStage4Validator
    {
        private const string GameWorldPath = "Assets/Scenes/GameWorld.unity";
        private const string PlayerPrefabPath = "Assets/Prefabs/Player/Player.prefab";
        private const string InputReaderPath = "Assets/Settings/InputReader.asset";
        private const string Stage4MarkerPath = "ProjectSettings/ContractStage4.done";

        [MenuItem("CONTRACT/Validate/Stage 4")]
        public static void Validate()
        {
            var failures = new List<string>();
            if (!File.Exists(Stage4MarkerPath))
            {
                failures.Add("Stage 4 setup marker is missing.");
            }

            Scene scene = EditorSceneManager.OpenScene(GameWorldPath, OpenSceneMode.Single);
            InputReader inputReader = AssetDatabase.LoadAssetAtPath<InputReader>(InputReaderPath);
            int interactableLayer = LayerMask.NameToLayer("Interactable");

            ValidateSceneBasics(scene, failures);
            ValidatePrefab(inputReader, interactableLayer, failures);
            ValidatePlayer(scene, inputReader, interactableLayer, failures);
            ValidatePrompt(failures);
            ValidateInteractables(scene, interactableLayer, failures);
            ValidateDoor(scene, interactableLayer, failures);
            ValidatePickups(interactableLayer, failures);
            ValidateSafehouseTerminal(scene, interactableLayer, failures);
            ValidateExtraction(scene, interactableLayer, failures);
            ValidateOverlay(failures);

            if (failures.Count > 0)
            {
                foreach (string failure in failures)
                {
                    Debug.LogError($"[CONTRACT-STAGE4] {failure}");
                }

                Debug.LogError($"[CONTRACT-STAGE4] FAILED with {failures.Count} issue(s).");
                if (Application.isBatchMode)
                {
                    EditorApplication.Exit(1);
                }

                return;
            }

            Debug.Log("[CONTRACT-STAGE4] PASSED: interaction controller, prompt, doors, pickups, safehouse terminal and extraction are valid.");
        }

        private static void ValidateSceneBasics(Scene scene, ICollection<string> failures)
        {
            int missingScripts = 0;
            int cameraCount = 0;
            int controllerCount = 0;
            int playerCount = 0;
            int interactionControllerCount = 0;
            int interactableRootCount = 0;

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

                    if (gameObject.GetComponent<Contract.Player.ThirdPersonController>() != null)
                    {
                        controllerCount++;
                    }

                    if (gameObject.GetComponent<InteractionController>() != null)
                    {
                        interactionControllerCount++;
                    }

                    if (gameObject.name == "Player" && gameObject.transform.parent == null)
                    {
                        playerCount++;
                    }

                    if (gameObject.name == "Interactables" && gameObject.transform.parent == null)
                    {
                        interactableRootCount++;
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

            if (playerCount != 1)
            {
                failures.Add($"GameWorld must contain exactly one Player root, found {playerCount}.");
            }

            if (interactionControllerCount != 1)
            {
                failures.Add($"GameWorld must contain exactly one InteractionController, found {interactionControllerCount}.");
            }

            if (interactableRootCount != 1)
            {
                failures.Add($"GameWorld must contain exactly one Interactables root, found {interactableRootCount}.");
            }
        }

        private static void ValidatePrefab(InputReader inputReader, int interactableLayer, ICollection<string> failures)
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPrefabPath);
            if (prefab == null)
            {
                failures.Add($"Player prefab is missing at {PlayerPrefabPath}.");
                return;
            }

            InteractionController controller = prefab.GetComponent<InteractionController>();
            if (controller == null)
            {
                failures.Add("Player prefab has no InteractionController.");
                return;
            }

            InputReader assignedReader = GetObject<InputReader>(controller, "inputReader");
            int mask = GetInt(controller, "interactionMask");
            if (assignedReader != inputReader)
            {
                failures.Add("Player prefab InteractionController does not reference InputReader.asset.");
            }

            if (interactableLayer < 0 || (mask & (1 << interactableLayer)) == 0)
            {
                failures.Add("Player prefab InteractionController mask does not include the Interactable layer.");
            }
        }

        private static void ValidatePlayer(
            Scene scene,
            InputReader inputReader,
            int interactableLayer,
            ICollection<string> failures)
        {
            GameObject player = FindRoot(scene, "Player");
            if (player == null)
            {
                failures.Add("GameWorld has no Player root.");
                return;
            }

            InteractionController controller = player.GetComponent<InteractionController>();
            if (controller == null)
            {
                failures.Add("Player has no InteractionController.");
                return;
            }

            InputReader assignedReader = GetObject<InputReader>(controller, "inputReader");
            TMP_Text promptText = GetObject<TMP_Text>(controller, "promptText");
            GameObject promptRoot = GetObject<GameObject>(controller, "promptRoot");
            int mask = GetInt(controller, "interactionMask");

            if (assignedReader != inputReader)
            {
                failures.Add("Player InteractionController does not reference InputReader.asset.");
            }

            if (promptText == null || promptText.gameObject.name != "Interaction Prompt")
            {
                failures.Add("Player InteractionController does not reference the Interaction Prompt text.");
            }

            if (promptRoot == null || promptRoot.name != "Interaction Prompt Panel")
            {
                failures.Add("Player InteractionController does not reference the Interaction Prompt Panel.");
            }

            if (interactableLayer < 0 || (mask & (1 << interactableLayer)) == 0)
            {
                failures.Add("Player InteractionController mask does not include the Interactable layer.");
            }
        }

        private static void ValidatePrompt(ICollection<string> failures)
        {
            TMP_Text prompt = Object.FindObjectsByType<TMP_Text>(FindObjectsInactive.Include)
                .FirstOrDefault(text => text.gameObject.name == "Interaction Prompt");
            if (prompt == null)
            {
                failures.Add("GameWorld is missing the Interaction Prompt text.");
                return;
            }

            if (prompt.font == null)
            {
                failures.Add("Interaction Prompt text has no font asset.");
            }

            Transform parent = prompt.transform.parent;
            if (parent == null || parent.name != "Interaction Prompt Panel")
            {
                failures.Add("Interaction Prompt text is not inside the Interaction Prompt Panel.");
            }
            else if (parent.GetComponent<Image>() == null)
            {
                failures.Add("Interaction Prompt Panel has no background Image.");
            }
        }

        private static void ValidateInteractables(Scene scene, int interactableLayer, ICollection<string> failures)
        {
            GameObject interactables = FindRoot(scene, "Interactables");
            if (interactables == null)
            {
                failures.Add("GameWorld has no Interactables root.");
                return;
            }

            if (interactables.transform.parent != null)
            {
                failures.Add("Interactables root must stay outside the Environment hierarchy.");
            }

            int wrongLayerCount = 0;
            foreach (Transform transform in interactables.GetComponentsInChildren<Transform>(true))
            {
                if (transform.gameObject.layer != interactableLayer)
                {
                    wrongLayerCount++;
                }
            }

            if (wrongLayerCount > 0)
            {
                failures.Add($"Interactables contains {wrongLayerCount} object(s) outside the Interactable layer.");
            }
        }

        private static void ValidateDoor(Scene scene, int interactableLayer, ICollection<string> failures)
        {
            GameObject door = FindDeep(scene, "SafehouseDoor");
            if (door == null)
            {
                failures.Add("GameWorld is missing the SafehouseDoor.");
                return;
            }

            if (door.GetComponent<DoorInteractable>() == null)
            {
                failures.Add("SafehouseDoor has no DoorInteractable component.");
            }

            if (door.GetComponentsInChildren<Collider>(true).Length == 0)
            {
                failures.Add("SafehouseDoor has no collider for interaction.");
            }

            if (door.layer != interactableLayer)
            {
                failures.Add($"SafehouseDoor layer is {door.layer}, expected {interactableLayer}.");
            }
        }

        private static void ValidatePickups(int interactableLayer, ICollection<string> failures)
        {
            PickupInteractable[] pickups = Object.FindObjectsByType<PickupInteractable>(FindObjectsInactive.Include);
            if (pickups.Length < 2)
            {
                failures.Add($"GameWorld must contain at least two pickups, found {pickups.Length}.");
            }

            bool hasKeycard = false;
            foreach (PickupInteractable pickup in pickups)
            {
                if (pickup.gameObject.layer != interactableLayer)
                {
                    failures.Add($"{pickup.name} is not on the Interactable layer.");
                }

                if (pickup.GetComponent<Collider>() == null)
                {
                    failures.Add($"{pickup.name} has no interaction collider.");
                }

                string itemId = GetString(pickup, "itemId");
                if (string.IsNullOrEmpty(itemId))
                {
                    failures.Add($"{pickup.name} has no item id.");
                }
                else if (itemId == "keycard")
                {
                    hasKeycard = true;
                }
            }

            if (!hasKeycard)
            {
                failures.Add("GameWorld is missing the keycard pickup.");
            }
        }

        private static void ValidateSafehouseTerminal(Scene scene, int interactableLayer, ICollection<string> failures)
        {
            GameObject terminal = FindDeep(scene, "SafehouseTerminal");
            if (terminal == null)
            {
                failures.Add("GameWorld is missing the SafehouseTerminal.");
                return;
            }

            SafehouseInteractable safehouse = terminal.GetComponent<SafehouseInteractable>();
            if (safehouse == null)
            {
                failures.Add("SafehouseTerminal has no SafehouseInteractable component.");
                return;
            }

            if (string.IsNullOrEmpty(GetString(safehouse, "safehouseId")))
            {
                failures.Add("SafehouseTerminal has no safehouse id.");
            }

            if (GetObject<GameObject>(safehouse, "securedIndicator") == null)
            {
                failures.Add("SafehouseTerminal has no secured indicator.");
            }

            if (terminal.layer != interactableLayer)
            {
                failures.Add($"SafehouseTerminal layer is {terminal.layer}, expected {interactableLayer}.");
            }
        }

        private static void ValidateExtraction(Scene scene, int interactableLayer, ICollection<string> failures)
        {
            GameObject extraction = FindDeep(scene, "ExtractionPoint");
            if (extraction == null)
            {
                failures.Add("GameWorld is missing the ExtractionPoint marker.");
                return;
            }

            ExtractionInteractable interactable = extraction.GetComponent<ExtractionInteractable>();
            if (interactable == null)
            {
                failures.Add("ExtractionPoint has no ExtractionInteractable component.");
                return;
            }

            if (GetString(interactable, "requiredItemId") != "keycard")
            {
                failures.Add("ExtractionPoint is not configured to require the keycard.");
            }

            BoxCollider trigger = extraction.GetComponent<BoxCollider>();
            if (trigger == null || !trigger.isTrigger)
            {
                failures.Add("ExtractionPoint has no trigger collider for interaction.");
            }

            if (extraction.layer != interactableLayer)
            {
                failures.Add($"ExtractionPoint layer is {extraction.layer}, expected {interactableLayer}.");
            }
        }

        private static void ValidateOverlay(ICollection<string> failures)
        {
            TMP_Text[] texts = Object.FindObjectsByType<TMP_Text>(FindObjectsInactive.Include);
            TMP_Text stageLabel = texts.FirstOrDefault(text => text.gameObject.name == "Stage Label");
            TMP_Text objective = texts.FirstOrDefault(text => text.gameObject.name == "Objective Label");

            if (stageLabel == null || !stageLabel.text.Contains("STAGE 4"))
            {
                failures.Add("GameWorld overlay is not updated for Stage 4.");
            }

            if (objective == null || !objective.text.Contains("KEYCARD") || !objective.text.Contains("EXTRACTION"))
            {
                failures.Add("GameWorld objective does not describe the keycard and extraction flow.");
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

        private static string GetString(Object target, string propertyName)
        {
            if (target == null)
            {
                return string.Empty;
            }

            var serializedObject = new SerializedObject(target);
            SerializedProperty property = serializedObject.FindProperty(propertyName);
            return property?.stringValue ?? string.Empty;
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
    }
}
