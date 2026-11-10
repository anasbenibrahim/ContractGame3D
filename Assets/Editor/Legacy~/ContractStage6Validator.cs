using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Contract.Core;
using Contract.Data;
using Contract.Economy;
using Contract.Items;
using Contract.Progression;
using Contract.UI;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace Contract.EditorTools
{
    public static class ContractStage6Validator
    {
        private const string GameWorldPath = "Assets/Scenes/GameWorld.unity";
        private const string Stage6MarkerPath = "ProjectSettings/ContractStage6.done";
        private const string UpgradeFolder = "Assets/ScriptableObjects/Upgrades";
        private const string ItemFolder = "Assets/ScriptableObjects/Items";
        private const string ItemLibraryPath = "Assets/ScriptableObjects/ItemLibrary.asset";
        private const string UpgradeItemPrefabPath = "Assets/Prefabs/UI/UpgradeItem.prefab";

        [MenuItem("CONTRACT/Validate/Stage 6")]
        public static void Validate()
        {
            var failures = new List<string>();
            if (!File.Exists(Stage6MarkerPath))
            {
                failures.Add("Stage 6 setup marker is missing.");
            }

            Scene scene = EditorSceneManager.OpenScene(GameWorldPath, OpenSceneMode.Single);
            ValidateScene(scene, failures);
            ValidateUpgradeData(failures);
            ValidateItemData(failures);
            ValidateShop(failures);

            if (failures.Count > 0)
            {
                foreach (string failure in failures)
                {
                    Debug.LogError($"[CONTRACT-STAGE6] {failure}");
                }

                Debug.LogError($"[CONTRACT-STAGE6] FAILED with {failures.Count} issue(s).");
                if (Application.isBatchMode)
                {
                    EditorApplication.Exit(1);
                }
                return;
            }

            Debug.Log("[CONTRACT-STAGE6] PASSED: economy, upgrades, items, and shop are valid.");
        }

        private static void ValidateScene(Scene scene, ICollection<string> failures)
        {
            int economyCount = 0;
            int upgradeCount = 0;
            int shopCount = 0;

            foreach (GameObject root in scene.GetRootGameObjects())
            {
                foreach (Transform transform in root.GetComponentsInChildren<Transform>(true))
                {
                    GameObject gameObject = transform.gameObject;
                    if (gameObject.GetComponent<EconomyManager>() != null) economyCount++;
                    if (gameObject.GetComponent<UpgradeManager>() != null) upgradeCount++;
                    if (gameObject.GetComponent<UpgradeShopController>() != null) shopCount++;
                }
            }

            if (economyCount != 1) failures.Add($"GameWorld must contain exactly one EconomyManager, found {economyCount}.");
            if (upgradeCount != 1) failures.Add($"GameWorld must contain exactly one UpgradeManager, found {upgradeCount}.");
            if (shopCount != 1) failures.Add($"GameWorld must contain exactly one UpgradeShopController, found {shopCount}.");

            EconomyManager economy = Object.FindAnyObjectByType<EconomyManager>();
            if (economy != null)
            {
                FieldInfo victoryField = typeof(EconomyManager).GetField("victoryMoney", BindingFlags.Instance | BindingFlags.NonPublic);
                int victoryMoney = victoryField != null ? (int)victoryField.GetValue(economy) : 0;
                if (victoryMoney != GameConstants.VictoryMoney)
                {
                    failures.Add($"EconomyManager victory target is {victoryMoney}, expected {GameConstants.VictoryMoney}.");
                }
            }

            UpgradeManager upgradeManager = Object.FindAnyObjectByType<UpgradeManager>();
            if (upgradeManager == null)
            {
                failures.Add("UpgradeManager not found in scene.");
                return;
            }

            FieldInfo allUpgradesField = typeof(UpgradeManager).GetField("allUpgrades", BindingFlags.Instance | BindingFlags.NonPublic);
            UpgradeData[] upgrades = allUpgradesField?.GetValue(upgradeManager) as UpgradeData[];
            if (upgrades == null || upgrades.Length < 20)
            {
                failures.Add($"UpgradeManager has {(upgrades?.Length ?? 0)} upgrades, expected at least 20.");
            }
            else
            {
                int validUpgradeCount = upgrades.Count(upgrade => upgrade != null);
                if (validUpgradeCount != upgrades.Length) failures.Add("UpgradeManager contains null upgrade entries.");
                if (upgrades.Where(upgrade => upgrade != null).Select(upgrade => upgrade.upgradeId).Distinct().Count() != validUpgradeCount)
                {
                    failures.Add("UpgradeManager contains duplicate upgrade IDs.");
                }

                var categories = upgrades.Where(upgrade => upgrade != null).Select(upgrade => upgrade.category).Distinct().Count();
                if (categories < 5) failures.Add($"UpgradeManager only covers {categories} of 5 categories.");
            }
        }

        private static void ValidateUpgradeData(ICollection<string> failures)
        {
            string[] guids = AssetDatabase.FindAssets("t:UpgradeData", new[] { UpgradeFolder });
            if (guids.Length < 20) failures.Add($"Upgrade folder contains {guids.Length} upgrades, expected at least 20.");

            var ids = new HashSet<string>();
            foreach (string guid in guids)
            {
                UpgradeData upgrade = AssetDatabase.LoadAssetAtPath<UpgradeData>(AssetDatabase.GUIDToAssetPath(guid));
                if (upgrade == null)
                {
                    failures.Add("Upgrade folder contains an unreadable UpgradeData asset.");
                    continue;
                }

                if (string.IsNullOrWhiteSpace(upgrade.upgradeId)) failures.Add($"Upgrade asset {upgrade.name} has no ID.");
                else if (!ids.Add(upgrade.upgradeId)) failures.Add($"Duplicate upgrade ID: {upgrade.upgradeId}.");
                if (string.IsNullOrWhiteSpace(upgrade.upgradeName)) failures.Add($"Upgrade {upgrade.upgradeId} has no name.");
                if (upgrade.cost <= 0) failures.Add($"Upgrade {upgrade.upgradeId} has an invalid cost.");
                if (upgrade.MaxLevel <= 0) failures.Add($"Upgrade {upgrade.upgradeId} has an invalid max level.");
            }
        }

        private static void ValidateItemData(ICollection<string> failures)
        {
            string[] guids = AssetDatabase.FindAssets("t:ItemData", new[] { ItemFolder });
            if (guids.Length < 15) failures.Add($"Item folder contains {guids.Length} items, expected at least 15.");

            var ids = new HashSet<string>();
            var types = new HashSet<ItemType>();
            foreach (string guid in guids)
            {
                ItemData item = AssetDatabase.LoadAssetAtPath<ItemData>(AssetDatabase.GUIDToAssetPath(guid));
                if (item == null)
                {
                    failures.Add("Item folder contains an unreadable ItemData asset.");
                    continue;
                }

                types.Add(item.type);
                if (string.IsNullOrWhiteSpace(item.itemId)) failures.Add($"Item asset {item.name} has no ID.");
                else if (!ids.Add(item.itemId)) failures.Add($"Duplicate item ID: {item.itemId}.");
                if (string.IsNullOrWhiteSpace(item.displayName)) failures.Add($"Item {item.itemId} has no display name.");
                if (item.MaxStack <= 0) failures.Add($"Item {item.itemId} has an invalid max stack.");
                if (item.sellPrice < 0 || item.buyPrice < 0) failures.Add($"Item {item.itemId} has a negative price.");
            }

            foreach (ItemType required in new[] { ItemType.KeyItem, ItemType.Evidence, ItemType.Consumable, ItemType.Tool })
            {
                if (!types.Contains(required)) failures.Add($"Item database is missing type {required}.");
            }

            ItemLibrary library = AssetDatabase.LoadAssetAtPath<ItemLibrary>(ItemLibraryPath);
            if (library == null)
            {
                failures.Add("ItemLibrary asset is missing.");
                return;
            }

            if (library.items == null || library.items.Length < 15)
            {
                failures.Add($"ItemLibrary has {(library.items?.Length ?? 0)} entries, expected at least 15.");
                return;
            }

            var libraryIds = new HashSet<string>();
            foreach (ItemEntry entry in library.items)
            {
                if (entry == null || string.IsNullOrWhiteSpace(entry.id) || entry.data == null)
                {
                    failures.Add("ItemLibrary contains an invalid entry.");
                    continue;
                }

                if (!libraryIds.Add(entry.id)) failures.Add($"ItemLibrary contains duplicate ID: {entry.id}.");
                if (library.FindItem(entry.id) == null) failures.Add($"ItemLibrary cannot resolve item: {entry.id}.");
            }
        }

        private static void ValidateShop(ICollection<string> failures)
        {
            UpgradeShopController shop = Object.FindAnyObjectByType<UpgradeShopController>();
            if (shop == null) return;

            string[] requiredReferences =
            {
                "shopPanel", "upgradeListContainer", "upgradeItemPrefab", "moneyText", "closeButton",
                "detailPanel", "detailNameText", "detailDescriptionText", "detailCostText",
                "detailLevelText", "detailEffectText", "purchaseButton"
            };

            foreach (string fieldName in requiredReferences)
            {
                FieldInfo field = typeof(UpgradeShopController).GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
                if (field == null || field.GetValue(shop) == null)
                {
                    failures.Add($"UpgradeShopController missing reference: {fieldName}.");
                }
            }

            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(UpgradeItemPrefabPath);
            if (prefab == null)
            {
                failures.Add("Upgrade item prefab is missing.");
                return;
            }

            if (prefab.GetComponent<UpgradeItemUI>() == null) failures.Add("Upgrade item prefab missing UpgradeItemUI.");
            if (prefab.GetComponent<Button>() == null) failures.Add("Upgrade item prefab missing Button.");
            if (prefab.GetComponentsInChildren<TMP_Text>(true).Length < 3) failures.Add("Upgrade item prefab is missing text elements.");
        }
    }
}
