using System;
using Contract.Core;
using Contract.Items;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Contract.UI
{
    /// <summary>
    /// Displays collected items and evidence in the HUD.
    /// </summary>
    public sealed class InventoryUI : MonoBehaviour
    {
        [Header("UI References")]
        [SerializeField] private Transform inventoryContainer;
        [SerializeField] private GameObject inventorySlotPrefab;
        [SerializeField] private int maxVisibleSlots = 8;

        [Header("Detail Panel")]
        [SerializeField] private GameObject detailPanel;
        [SerializeField] private Image detailIcon;
        [SerializeField] private TMP_Text detailName;
        [SerializeField] private TMP_Text detailDescription;

        private GameSession session;

        private void Awake()
        {
            if (detailPanel != null) detailPanel.SetActive(false);
        }

        private void Start()
        {
            session = GameManager.Instance?.Session;
            if (session != null)
            {
                GameEvents.ItemAddedToInventory += OnItemAdded;
                RefreshInventory();
            }
        }

        private void OnDestroy()
        {
            GameEvents.ItemAddedToInventory -= OnItemAdded;
        }

        private void OnItemAdded(string itemId)
        {
            RefreshInventory();
        }

        public void RefreshInventory()
        {
            if (session == null || inventoryContainer == null || inventorySlotPrefab == null) return;

            foreach (Transform child in inventoryContainer)
            {
                Destroy(child.gameObject);
            }

            foreach (string itemId in session.inventoryItemIds)
            {
                GameObject slot = Instantiate(inventorySlotPrefab, inventoryContainer);
                InventorySlotUI slotUI = slot.GetComponent<InventorySlotUI>();
                if (slotUI != null)
                {
                    slotUI.Setup(itemId, () => ShowDetail(itemId));
                }
            }

            foreach (string itemId in session.collectedEvidenceIds)
            {
                GameObject slot = Instantiate(inventorySlotPrefab, inventoryContainer);
                InventorySlotUI slotUI = slot.GetComponent<InventorySlotUI>();
                if (slotUI != null)
                {
                    slotUI.Setup(itemId, () => ShowDetail(itemId), true);
                }
            }
        }

        private void ShowDetail(string itemId)
        {
            if (detailPanel != null) detailPanel.SetActive(true);
            // Would need an ItemLibrary to get item data
        }
    }

}
