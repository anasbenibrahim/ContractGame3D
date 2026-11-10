using System;
using System.IO;
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
    [InitializeOnLoad]
    public static class ContractStage4Setup
    {
        private const string MarkerPath = "ProjectSettings/ContractStage4.done";
        private const string Stage1MarkerPath = "ProjectSettings/ContractStage1.done";
        private const string Stage2MarkerPath = "ProjectSettings/ContractStage2.done";
        private const string Stage3MarkerPath = "ProjectSettings/ContractStage3.done";
        private const string GameWorldPath = "Assets/Scenes/GameWorld.unity";
        private const string PlayerPrefabPath = "Assets/Prefabs/Player/Player.prefab";

        private static bool isRunning;

        static ContractStage4Setup()
        {
            EditorApplication.delayCall += AutoSetup;
        }

        [MenuItem("CONTRACT/Setup/Rebuild Stage 4 Interaction System")]
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

            if (!File.Exists(Stage1MarkerPath)
                || !File.Exists(Stage2MarkerPath)
                || !File.Exists(Stage3MarkerPath)
                || !File.Exists(GameWorldPath))
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
                Debug.Log("[CONTRACT] Stage 4: wiring interaction system...");
                InputReader inputReader = AssetDatabase.LoadAssetAtPath<InputReader>("Assets/Settings/InputReader.asset");
                if (inputReader == null)
                {
                    Debug.LogError("[CONTRACT] Stage 4 requires InputReader.asset.");
                    return;
                }

                int interactableLayer = LayerMask.NameToLayer("Interactable");
                if (interactableLayer < 0)
                {
                    Debug.LogError("[CONTRACT] Stage 4 requires the Interactable layer.");
                    return;
                }

                if (!ConfigurePlayerPrefab(inputReader, interactableLayer))
                {
                    return;
                }

                Scene scene = EditorSceneManager.OpenScene(GameWorldPath, OpenSceneMode.Single);
                TMP_Text promptText = BuildInteractionPrompt(scene, out GameObject promptRoot);
                if (promptText == null || promptRoot == null)
                {
                    Debug.LogError("[CONTRACT] Stage 4 requires the GameWorld UI Canvas.");
                    return;
                }

                DestroyRoot(scene, "Interactables");
                GameObject interactables = new GameObject("Interactables");
                SceneManager.MoveGameObjectToScene(interactables, scene);

                BuildSafehouseDoor(interactables.transform);
                BuildPickups(interactables.transform);
                BuildSafehouseTerminal(interactables.transform);
                ConfigureExtraction(scene, interactableLayer);
                SetLayerRecursively(interactables, interactableLayer);

                if (!ConfigurePlayerInstance(scene, inputReader, interactableLayer, promptText, promptRoot))
                {
                    Debug.LogError("[CONTRACT] Stage 4 could not configure the player interaction controller.");
                    return;
                }

                UpdateOverlay();
                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();

                File.WriteAllText(MarkerPath, $"Stage 4 completed {DateTime.UtcNow:O}\n");
                Debug.Log("[CONTRACT] Stage 4 interaction system generated successfully.");
            }
            catch (Exception exception)
            {
                Debug.LogError($"[CONTRACT] Stage 4 setup failed: {exception}");
            }
            finally
            {
                isRunning = false;
            }
        }

        private static bool ConfigurePlayerPrefab(InputReader inputReader, int interactableLayer)
        {
            if (AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPrefabPath) == null)
            {
                Debug.LogError($"[CONTRACT] Stage 4 requires {PlayerPrefabPath}.");
                return false;
            }

            GameObject contents = PrefabUtility.LoadPrefabContents(PlayerPrefabPath);
            if (contents == null)
            {
                Debug.LogError("[CONTRACT] Stage 4 could not load the player prefab.");
                return false;
            }

            try
            {
                InteractionController controller = contents.GetComponent<InteractionController>();
                if (controller == null)
                {
                    controller = contents.AddComponent<InteractionController>();
                }

                ConfigureInteraction(controller, inputReader, interactableLayer, null, null);
                PrefabUtility.SaveAsPrefabAsset(contents, PlayerPrefabPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(contents);
            }

            AssetDatabase.SaveAssets();
            return true;
        }

        private static bool ConfigurePlayerInstance(
            Scene scene,
            InputReader inputReader,
            int interactableLayer,
            TMP_Text promptText,
            GameObject promptRoot)
        {
            GameObject player = FindRoot(scene, "Player");
            if (player == null)
            {
                return false;
            }

            InteractionController controller = player.GetComponent<InteractionController>();
            if (controller == null)
            {
                controller = player.AddComponent<InteractionController>();
            }

            ConfigureInteraction(controller, inputReader, interactableLayer, promptText, promptRoot);
            return true;
        }

        private static void ConfigureInteraction(
            InteractionController controller,
            InputReader inputReader,
            int interactableLayer,
            TMP_Text promptText,
            GameObject promptRoot)
        {
            var serializedController = new SerializedObject(controller);
            serializedController.FindProperty("inputReader").objectReferenceValue = inputReader;
            serializedController.FindProperty("interactionRange").floatValue = 2.75f;
            serializedController.FindProperty("interactionMask").intValue = 1 << interactableLayer;
            serializedController.FindProperty("promptText").objectReferenceValue = promptText;
            serializedController.FindProperty("promptRoot").objectReferenceValue = promptRoot;
            serializedController.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(controller);
        }

        private static TMP_Text BuildInteractionPrompt(Scene scene, out GameObject promptRoot)
        {
            promptRoot = null;
            GameObject canvasObject = FindRoot(scene, "UI Canvas");
            if (canvasObject == null)
            {
                return null;
            }

            Transform existingPanel = canvasObject.transform.Find("Interaction Prompt Panel");
            GameObject panelObject;
            if (existingPanel == null)
            {
                panelObject = new GameObject("Interaction Prompt Panel", typeof(RectTransform), typeof(Image));
                panelObject.transform.SetParent(canvasObject.transform, false);
                RectTransform rect = panelObject.GetComponent<RectTransform>();
                rect.anchorMin = new Vector2(0.5f, 0f);
                rect.anchorMax = new Vector2(0.5f, 0f);
                rect.pivot = new Vector2(0.5f, 0f);
                rect.anchoredPosition = new Vector2(0f, 92f);
                rect.sizeDelta = new Vector2(920f, 64f);

                Image image = panelObject.GetComponent<Image>();
                image.color = new Color(0.01f, 0.015f, 0.022f, 0.72f);
                image.raycastTarget = false;
            }
            else
            {
                panelObject = existingPanel.gameObject;
            }

            promptRoot = panelObject;
            Transform existingText = panelObject.transform.Find("Interaction Prompt");
            GameObject textObject;
            TextMeshProUGUI text;

            if (existingText == null)
            {
                textObject = new GameObject("Interaction Prompt", typeof(RectTransform));
                textObject.transform.SetParent(panelObject.transform, false);
                RectTransform rect = textObject.GetComponent<RectTransform>();
                rect.anchorMin = Vector2.zero;
                rect.anchorMax = Vector2.one;
                rect.offsetMin = new Vector2(16f, 6f);
                rect.offsetMax = new Vector2(-16f, -6f);
                text = textObject.AddComponent<TextMeshProUGUI>();
                text.fontSize = 24f;
                text.alignment = TextAlignmentOptions.Center;
                text.color = new Color(0.86f, 0.91f, 0.96f);
                text.textWrappingMode = TextWrappingModes.NoWrap;
                text.raycastTarget = false;
                if (text.font == null && TMP_Settings.defaultFontAsset != null)
                {
                    text.font = TMP_Settings.defaultFontAsset;
                }
            }
            else
            {
                textObject = existingText.gameObject;
                text = textObject.GetComponent<TextMeshProUGUI>();
                if (text == null)
                {
                    text = textObject.AddComponent<TextMeshProUGUI>();
                }
            }

            text.text = string.Empty;
            EditorUtility.SetDirty(text);
            panelObject.SetActive(false);
            return text;
        }

        private static void BuildSafehouseDoor(Transform parent)
        {
            Material metal = LoadMaterial("Assets/Art/Materials/Metal_Dark.mat");
            Material accent = LoadMaterial("Assets/Art/Materials/Extraction_Green.mat");

            GameObject door = CreateChild("SafehouseDoor", parent, new Vector3(-12.9f, 0f, -50.7f));
            CreatePrimitive(
                PrimitiveType.Cube,
                "DoorPanel",
                door.transform,
                new Vector3(0f, 1.4f, 1.2f),
                new Vector3(0.12f, 2.8f, 2.35f),
                metal);
            CreatePrimitive(
                PrimitiveType.Cube,
                "DoorAccent",
                door.transform,
                new Vector3(0.08f, 2.35f, 0.7f),
                new Vector3(0.03f, 0.06f, 1.2f),
                accent,
                false);

            DoorInteractable interactable = door.AddComponent<DoorInteractable>();
            var serializedDoor = new SerializedObject(interactable);
            serializedDoor.FindProperty("openAngle").floatValue = 95f;
            serializedDoor.FindProperty("rotationSpeed").floatValue = 220f;
            serializedDoor.FindProperty("openPrompt").stringValue = "[E] CLOSE SAFEHOUSE DOOR";
            serializedDoor.FindProperty("closePrompt").stringValue = "[E] OPEN SAFEHOUSE DOOR";
            serializedDoor.FindProperty("interactionId").stringValue = "safehouse_door";
            serializedDoor.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void BuildPickups(Transform parent)
        {
            Material metal = LoadMaterial("Assets/Art/Materials/Metal_Dark.mat");
            Material neonCyan = LoadMaterial("Assets/Art/Materials/Neon_Cyan.mat");
            Material warning = LoadMaterial("Assets/Art/Materials/Warning_Yellow.mat");
            Material extractionGreen = LoadMaterial("Assets/Art/Materials/Extraction_Green.mat");

            GameObject keycard = CreateChild("KeycardPickup", parent, new Vector3(-18.8f, 1.18f, -51.7f));
            BoxCollider keycardCollider = keycard.AddComponent<BoxCollider>();
            keycardCollider.isTrigger = true;
            keycardCollider.size = new Vector3(1.1f, 1.1f, 1.1f);
            CreatePrimitive(
                PrimitiveType.Cube,
                "Keycard Visual",
                keycard.transform,
                Vector3.zero,
                new Vector3(0.35f, 0.05f, 0.22f),
                neonCyan,
                false);
            CreatePrimitive(
                PrimitiveType.Cube,
                "Keycard Chip",
                keycard.transform,
                new Vector3(0.06f, 0.035f, 0f),
                new Vector3(0.12f, 0.03f, 0.1f),
                warning,
                false);
            PickupInteractable keycardPickup = keycard.AddComponent<PickupInteractable>();
            ConfigurePickup(keycardPickup, "keycard", "KEYCARD");

            GameObject evidence = CreateChild("EvidencePickup", parent, new Vector3(5f, 0.2f, 18f));
            BoxCollider evidenceCollider = evidence.AddComponent<BoxCollider>();
            evidenceCollider.isTrigger = true;
            evidenceCollider.size = new Vector3(1.2f, 1.2f, 1.2f);
            CreatePrimitive(
                PrimitiveType.Cube,
                "Evidence Case",
                evidence.transform,
                new Vector3(0f, 0.2f, 0f),
                new Vector3(0.45f, 0.35f, 0.45f),
                warning);
            CreatePrimitive(
                PrimitiveType.Cube,
                "Evidence Screen",
                evidence.transform,
                new Vector3(0f, 0.2f, 0.24f),
                new Vector3(0.22f, 0.12f, 0.02f),
                extractionGreen,
                false);
            PickupInteractable evidencePickup = evidence.AddComponent<PickupInteractable>();
            ConfigurePickup(evidencePickup, "evidence", "BLACK BOX");

            if (metal == null)
            {
                Debug.LogWarning("[CONTRACT] Stage 4 could not load the metal material.");
            }
        }

        private static void ConfigurePickup(PickupInteractable pickup, string itemId, string displayName)
        {
            var serializedPickup = new SerializedObject(pickup);
            serializedPickup.FindProperty("itemId").stringValue = itemId;
            serializedPickup.FindProperty("displayName").stringValue = displayName;
            serializedPickup.FindProperty("hideOnCollect").boolValue = true;
            serializedPickup.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void BuildSafehouseTerminal(Transform parent)
        {
            Material metal = LoadMaterial("Assets/Art/Materials/Metal_Dark.mat");
            Material screen = LoadMaterial("Assets/Art/Materials/Neon_Magenta.mat");
            Material green = LoadMaterial("Assets/Art/Materials/Extraction_Green.mat");

            GameObject terminal = CreateChild("SafehouseTerminal", parent, new Vector3(-17.2f, 1.5f, -52.65f));
            BoxCollider terminalCollider = terminal.AddComponent<BoxCollider>();
            terminalCollider.isTrigger = true;
            terminalCollider.size = new Vector3(1.1f, 1.5f, 1.1f);
            CreatePrimitive(
                PrimitiveType.Cube,
                "Terminal Body",
                terminal.transform,
                Vector3.zero,
                new Vector3(0.55f, 0.75f, 0.28f),
                metal);
            CreatePrimitive(
                PrimitiveType.Cube,
                "Terminal Screen",
                terminal.transform,
                new Vector3(0f, 0.05f, 0.16f),
                new Vector3(0.42f, 0.35f, 0.03f),
                screen,
                false);
            GameObject indicator = CreatePrimitive(
                PrimitiveType.Cube,
                "Secure Indicator",
                terminal.transform,
                new Vector3(0.38f, 0.55f, 0.16f),
                new Vector3(0.1f, 0.1f, 0.05f),
                green,
                false);
            indicator.SetActive(false);

            SafehouseInteractable safehouse = terminal.AddComponent<SafehouseInteractable>();
            var serializedSafehouse = new SerializedObject(safehouse);
            serializedSafehouse.FindProperty("safehouseId").stringValue = "safehouse_01";
            serializedSafehouse.FindProperty("securedIndicator").objectReferenceValue = indicator;
            serializedSafehouse.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void ConfigureExtraction(Scene scene, int interactableLayer)
        {
            GameObject extraction = FindDeep(scene, "ExtractionPoint");
            if (extraction == null)
            {
                Debug.LogError("[CONTRACT] Stage 4 requires the Stage 3 ExtractionPoint marker.");
                return;
            }

            extraction.layer = interactableLayer;
            if (extraction.GetComponent<BoxCollider>() == null)
            {
                BoxCollider trigger = extraction.AddComponent<BoxCollider>();
                trigger.isTrigger = true;
                trigger.center = new Vector3(0f, 1.5f, 0f);
                trigger.size = new Vector3(7f, 3f, 7f);
            }

            ExtractionInteractable interactable = extraction.GetComponent<ExtractionInteractable>();
            if (interactable == null)
            {
                interactable = extraction.AddComponent<ExtractionInteractable>();
            }

            var serializedExtraction = new SerializedObject(interactable);
            serializedExtraction.FindProperty("extractionId").stringValue = "extraction_01";
            serializedExtraction.FindProperty("requiredItemId").stringValue = "keycard";
            serializedExtraction.FindProperty("lockedPrompt").stringValue = "EXTRACTION LOCKED // KEYCARD REQUIRED";
            serializedExtraction.FindProperty("readyPrompt").stringValue = "[E] EXTRACT";
            serializedExtraction.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void UpdateOverlay()
        {
            foreach (TMP_Text text in Object.FindObjectsByType<TMP_Text>(FindObjectsInactive.Include))
            {
                switch (text.gameObject.name)
                {
                    case "Stage Label":
                        text.text = "WASD MOVE | SHIFT SPRINT | CTRL CROUCH | SPACE JUMP | E INTERACT | ESC PAUSE // STAGE 4";
                        break;
                    case "Objective Label":
                        text.text = "OBJECTIVE // FIND THE KEYCARD // SECURE SAFEHOUSE // REACH EXTRACTION";
                        break;
                }

                EditorUtility.SetDirty(text);
            }
        }

        private static Material LoadMaterial(string path)
        {
            return AssetDatabase.LoadAssetAtPath<Material>(path);
        }

        private static GameObject CreateChild(string name, Transform parent, Vector3 localPosition)
        {
            var child = new GameObject(name);
            child.transform.SetParent(parent, false);
            child.transform.localPosition = localPosition;
            return child;
        }

        private static GameObject CreatePrimitive(
            PrimitiveType primitiveType,
            string name,
            Transform parent,
            Vector3 localPosition,
            Vector3 localScale,
            Material material,
            bool addCollider = true)
        {
            GameObject primitive = GameObject.CreatePrimitive(primitiveType);
            primitive.name = name;
            primitive.transform.SetParent(parent, false);
            primitive.transform.localPosition = localPosition;
            primitive.transform.localScale = localScale;
            primitive.isStatic = true;

            if (!addCollider)
            {
                Collider collider = primitive.GetComponent<Collider>();
                if (collider != null)
                {
                    Object.DestroyImmediate(collider);
                }
            }

            Renderer renderer = primitive.GetComponent<Renderer>();
            if (renderer != null && material != null)
            {
                renderer.sharedMaterial = material;
            }

            return primitive;
        }

        private static void DestroyRoot(Scene scene, string name)
        {
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                if (root.name == name)
                {
                    Object.DestroyImmediate(root);
                }
            }
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
    }
}
