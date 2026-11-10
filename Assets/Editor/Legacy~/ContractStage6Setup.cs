using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Contract.Data;
using Contract.Economy;
using Contract.Progression;
using Contract.UI;
using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace Contract.EditorTools
{
    [InitializeOnLoad]
    public static class ContractStage6Setup
    {
        private const string MarkerPath = "ProjectSettings/ContractStage6.done";
        private const string Stage5MarkerPath = "ProjectSettings/ContractStage5.done";
        private const string GameWorldPath = "Assets/Scenes/GameWorld.unity";
        private const string UpgradeFolder = "Assets/ScriptableObjects/Upgrades";
        private const string ItemFolder = "Assets/ScriptableObjects/Items";
        private const string UpgradeItemPrefabPath = "Assets/Prefabs/UI/UpgradeItem.prefab";

        private static bool isRunning;

        static ContractStage6Setup()
        {
            EditorApplication.delayCall += AutoSetup;
        }

        [MenuItem("CONTRACT/Setup/Rebuild Stage 6 Economy Progression")]
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

            if (!File.Exists(Stage5MarkerPath) || !File.Exists(GameWorldPath))
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
                Debug.Log("[CONTRACT] Stage 6: wiring economy, upgrades, items, and shop...");
                CreateScriptableObjectAssets.CreateAllAssets();
                EnsureFolder("Assets/Prefabs/UI");

                Scene scene = EditorSceneManager.OpenScene(GameWorldPath, OpenSceneMode.Single);
                DestroyRoot(scene, "[EconomyManager]");
                DestroyRoot(scene, "[UpgradeManager]");
                DestroyRoot(scene, "[UpgradeShop]");
                DestroyByName(scene, "Upgrade Shop Panel");
                DestroyByName(scene, "Shop Open Button");

                CreateManager<EconomyManager>(scene, "[EconomyManager]");
                UpgradeManager upgradeManager = CreateManager<UpgradeManager>(scene, "[UpgradeManager]");
                AssignUpgrades(upgradeManager);

                Canvas canvas = Object.FindAnyObjectByType<Canvas>();
                if (canvas == null)
                {
                    Debug.LogError("[CONTRACT] Stage 6 requires the GameWorld UI Canvas.");
                    return;
                }

                GameObject upgradePrefab = BuildUpgradeItemPrefab();
                UpgradeShopController shop = BuildShop(scene, canvas, upgradePrefab);

                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();

                File.WriteAllText(MarkerPath, DateTime.UtcNow.ToString("o"));
                Debug.Log("[CONTRACT] Stage 6 setup complete.");
            }
            catch (Exception exception)
            {
                Debug.LogError("[CONTRACT] Stage 6 setup failed: " + exception);
            }
            finally
            {
                isRunning = false;
            }
        }

        private static T CreateManager<T>(Scene scene, string name) where T : Component
        {
            GameObject gameObject = new GameObject(name);
            SceneManager.MoveGameObjectToScene(gameObject, scene);
            return gameObject.AddComponent<T>();
        }

        private static void AssignUpgrades(UpgradeManager upgradeManager)
        {
            string[] guids = AssetDatabase.FindAssets("t:UpgradeData", new[] { UpgradeFolder });
            var upgrades = guids
                .Select(guid => AssetDatabase.LoadAssetAtPath<UpgradeData>(AssetDatabase.GUIDToAssetPath(guid)))
                .Where(data => data != null)
                .OrderBy(data => data.upgradeId, StringComparer.Ordinal)
                .ToArray();

            SerializedObject serialized = new SerializedObject(upgradeManager);
            SerializedProperty property = serialized.FindProperty("allUpgrades");
            if (property == null)
            {
                Debug.LogError("[CONTRACT] UpgradeManager.allUpgrades field not found.");
                return;
            }

            property.arraySize = upgrades.Length;
            for (int i = 0; i < upgrades.Length; i++)
            {
                property.GetArrayElementAtIndex(i).objectReferenceValue = upgrades[i];
            }

            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(upgradeManager);
        }

        private static UpgradeShopController BuildShop(Scene scene, Canvas canvas, GameObject upgradePrefab)
        {
            GameObject shopRoot = new GameObject("[UpgradeShop]");
            SceneManager.MoveGameObjectToScene(shopRoot, scene);
            UpgradeShopController controller = shopRoot.AddComponent<UpgradeShopController>();

            GameObject panel = CreatePanel(canvas.transform, "Upgrade Shop Panel", new Color(0.03f, 0.04f, 0.07f, 0.97f));
            SetStretch(panel.GetComponent<RectTransform>(), 80f, 80f, 80f, 80f);

            CreateText(panel.transform, "Shop Title", "UPGRADE SHOP", 34f, new Color(1f, 0.82f, 0.35f), TextAlignmentOptions.Left,
                new Vector2(0f, 1f), new Vector2(0.6f, 1f), new Vector2(28f, -20f), new Vector2(-10f, -70f));

            TMP_Text moneyText = CreateText(panel.transform, "Money", "$0", 26f, new Color(0.35f, 1f, 0.55f), TextAlignmentOptions.Right,
                new Vector2(0.6f, 1f), new Vector2(1f, 1f), new Vector2(10f, -20f), new Vector2(-150f, -70f));

            Button closeButton = CreateButton(panel.transform, "Close Button", "CLOSE", new Color(0.55f, 0.12f, 0.15f, 1f),
                new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-126f, -22f), new Vector2(-24f, -68f));

            GameObject listPanel = CreatePanel(panel.transform, "Upgrade List", new Color(0.05f, 0.06f, 0.09f, 0.9f));
            SetStretch(listPanel.GetComponent<RectTransform>(), 24f, 900f, 24f, 96f);
            ScrollRect scroll = listPanel.AddComponent<ScrollRect>();
            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 30f;

            GameObject viewport = CreatePanel(listPanel.transform, "Viewport", new Color(0f, 0f, 0f, 0f));
            RectTransform viewportRect = viewport.GetComponent<RectTransform>();
            SetStretch(viewportRect, 12f, 12f, 12f, 12f);
            viewport.AddComponent<RectMask2D>();

            GameObject content = new GameObject("Content", typeof(RectTransform));
            content.transform.SetParent(viewport.transform, false);
            RectTransform contentRect = content.GetComponent<RectTransform>();
            contentRect.anchorMin = new Vector2(0f, 1f);
            contentRect.anchorMax = new Vector2(1f, 1f);
            contentRect.pivot = new Vector2(0.5f, 1f);
            contentRect.anchoredPosition = Vector2.zero;
            contentRect.sizeDelta = Vector2.zero;

            VerticalLayoutGroup layout = content.AddComponent<VerticalLayoutGroup>();
            layout.spacing = 8f;
            layout.padding = new RectOffset(0, 0, 0, 0);
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            content.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            scroll.viewport = viewportRect;
            scroll.content = contentRect;

            GameObject detailPanel = CreatePanel(panel.transform, "Upgrade Details", new Color(0.06f, 0.07f, 0.11f, 0.95f));
            SetStretch(detailPanel.GetComponent<RectTransform>(), 920f, 24f, 24f, 96f);

            TMP_Text detailName = CreateText(detailPanel.transform, "Detail Name", "Select an upgrade", 28f, Color.white,
                TextAlignmentOptions.Left, new Vector2(0f, 1f), new Vector2(1f, 1f),
                new Vector2(24f, -20f), new Vector2(-24f, -68f));
            TMP_Text detailLevel = CreateText(detailPanel.transform, "Detail Level", string.Empty, 20f, new Color(0.65f, 0.8f, 1f),
                TextAlignmentOptions.Left, new Vector2(0f, 1f), new Vector2(1f, 1f),
                new Vector2(24f, -70f), new Vector2(-24f, -104f));
            TMP_Text detailDescription = CreateText(detailPanel.transform, "Detail Description", string.Empty, 20f, new Color(0.82f, 0.84f, 0.9f),
                TextAlignmentOptions.TopLeft, new Vector2(0f, 1f), new Vector2(1f, 1f),
                new Vector2(24f, -112f), new Vector2(-24f, -210f));
            TMP_Text detailEffect = CreateText(detailPanel.transform, "Detail Effect", string.Empty, 19f, new Color(0.95f, 0.85f, 0.45f),
                TextAlignmentOptions.TopLeft, new Vector2(0f, 1f), new Vector2(1f, 1f),
                new Vector2(24f, -218f), new Vector2(-24f, -340f));
            TMP_Text detailCost = CreateText(detailPanel.transform, "Detail Cost", string.Empty, 24f, Color.white,
                TextAlignmentOptions.Left, new Vector2(0f, 0f), new Vector2(1f, 0f),
                new Vector2(24f, 86f), new Vector2(-24f, 128f));

            Button purchaseButton = CreateButton(detailPanel.transform, "Purchase Button", "PURCHASE", new Color(0.12f, 0.52f, 0.25f, 1f),
                new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(24f, 20f), new Vector2(-24f, 76f));

            Image upgradeIcon = CreateImage(detailPanel.transform, "Upgrade Icon", new Color(0.18f, 0.2f, 0.28f, 1f),
                new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(24f, 148f), new Vector2(116f, 240f));

            SerializedObject serialized = new SerializedObject(controller);
            SetObject(serialized, "shopPanel", panel);
            SetObject(serialized, "upgradeListContainer", content.transform);
            SetObject(serialized, "upgradeItemPrefab", upgradePrefab);
            SetObject(serialized, "moneyText", moneyText);
            SetObject(serialized, "closeButton", closeButton);
            SetObject(serialized, "detailPanel", detailPanel);
            SetObject(serialized, "detailNameText", detailName);
            SetObject(serialized, "detailDescriptionText", detailDescription);
            SetObject(serialized, "detailCostText", detailCost);
            SetObject(serialized, "detailLevelText", detailLevel);
            SetObject(serialized, "detailEffectText", detailEffect);
            SetObject(serialized, "purchaseButton", purchaseButton);
            SetObject(serialized, "upgradeIcon", upgradeIcon);
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(controller);

            Button openButton = CreateButton(canvas.transform, "Shop Open Button", "SHOP", new Color(0.18f, 0.3f, 0.55f, 0.95f),
                new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-154f, -24f), new Vector2(-24f, -68f));
            UnityEventTools.AddPersistentListener(openButton.onClick, controller.OpenShop);

            return controller;
        }

        private static GameObject BuildUpgradeItemPrefab()
        {
            GameObject root = new GameObject("UpgradeItem", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
            RectTransform rect = root.GetComponent<RectTransform>();
            rect.sizeDelta = new Vector2(0f, 64f);
            root.AddComponent<LayoutElement>().preferredHeight = 64f;

            Image background = root.GetComponent<Image>();
            background.color = new Color(0.1f, 0.12f, 0.17f, 0.98f);

            Button button = root.GetComponent<Button>();
            button.targetGraphic = background;
            button.transition = Selectable.Transition.ColorTint;

            Image accent = CreateImage(root.transform, "Category Color", new Color(0.3f, 0.55f, 1f, 1f),
                new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(0f, 0f), new Vector2(8f, 0f));

            TMP_Text nameText = CreateText(root.transform, "Name", "Upgrade", 21f, Color.white,
                TextAlignmentOptions.Left, new Vector2(0f, 0f), new Vector2(0.62f, 1f),
                new Vector2(22f, 0f), new Vector2(-4f, 0f));
            TMP_Text levelText = CreateText(root.transform, "Level", "Lv.0/0", 17f, new Color(0.65f, 0.8f, 1f),
                TextAlignmentOptions.Right, new Vector2(0.62f, 0f), new Vector2(1f, 1f),
                new Vector2(4f, 8f), new Vector2(-18f, -6f));
            TMP_Text costText = CreateText(root.transform, "Cost", "$0", 17f, new Color(0.4f, 1f, 0.6f),
                TextAlignmentOptions.Right, new Vector2(0.62f, 0f), new Vector2(1f, 1f),
                new Vector2(4f, -8f), new Vector2(-18f, 6f));
            Image icon = CreateImage(root.transform, "Icon", new Color(0.18f, 0.2f, 0.28f, 1f),
                new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(14f, -20f), new Vector2(54f, 20f));

            UpgradeItemUI itemUI = root.AddComponent<UpgradeItemUI>();
            SerializedObject serialized = new SerializedObject(itemUI);
            SetObject(serialized, "nameText", nameText);
            SetObject(serialized, "levelText", levelText);
            SetObject(serialized, "costText", costText);
            SetObject(serialized, "iconImage", icon);
            SetObject(serialized, "categoryColor", accent);
            SetObject(serialized, "button", button);
            serialized.ApplyModifiedPropertiesWithoutUndo();

            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, UpgradeItemPrefabPath);
            Object.DestroyImmediate(root);
            return prefab;
        }

        private static GameObject CreatePanel(Transform parent, string name, Color color)
        {
            GameObject gameObject = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            gameObject.transform.SetParent(parent, false);
            gameObject.GetComponent<Image>().color = color;
            return gameObject;
        }

        private static Image CreateImage(Transform parent, string name, Color color, Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax)
        {
            GameObject gameObject = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            gameObject.transform.SetParent(parent, false);
            Image image = gameObject.GetComponent<Image>();
            image.color = color;
            image.raycastTarget = false;
            SetAnchors(image.rectTransform, anchorMin, anchorMax, offsetMin, offsetMax);
            return image;
        }

        private static TMP_Text CreateText(Transform parent, string name, string value, float fontSize, Color color,
            TextAlignmentOptions alignment, Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax)
        {
            GameObject gameObject = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer));
            gameObject.transform.SetParent(parent, false);
            TextMeshProUGUI text = gameObject.AddComponent<TextMeshProUGUI>();
            text.text = value;
            text.fontSize = fontSize;
            text.color = color;
            text.alignment = alignment;
            text.raycastTarget = false;
            if (TMP_Settings.defaultFontAsset != null)
            {
                text.font = TMP_Settings.defaultFontAsset;
            }

            SetAnchors(text.rectTransform, anchorMin, anchorMax, offsetMin, offsetMax);
            return text;
        }

        private static Button CreateButton(Transform parent, string name, string label, Color color,
            Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax)
        {
            GameObject gameObject = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
            gameObject.transform.SetParent(parent, false);
            Image image = gameObject.GetComponent<Image>();
            image.color = color;
            Button button = gameObject.GetComponent<Button>();
            button.targetGraphic = image;
            button.transition = Selectable.Transition.ColorTint;
            SetAnchors(button.GetComponent<RectTransform>(), anchorMin, anchorMax, offsetMin, offsetMax);

            TMP_Text text = CreateText(gameObject.transform, "Label", label, 20f, Color.white,
                TextAlignmentOptions.Center, Vector2.zero, Vector2.one, new Vector2(6f, 4f), new Vector2(-6f, -4f));
            text.raycastTarget = false;
            return button;
        }

        private static void SetAnchors(RectTransform rect, Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax)
        {
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.offsetMin = offsetMin;
            rect.offsetMax = offsetMax;
        }

        private static void SetStretch(RectTransform rect, float left, float right, float bottom, float top)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(left, bottom);
            rect.offsetMax = new Vector2(-right, -top);
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

        private static void DestroyByName(Scene scene, string name)
        {
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                foreach (Transform transform in root.GetComponentsInChildren<Transform>(true))
                {
                    if (transform.name == name)
                    {
                        Object.DestroyImmediate(transform.gameObject);
                        return;
                    }
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
