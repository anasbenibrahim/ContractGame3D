using System;
using UnityEngine;

namespace Contract.Data
{
    public enum ItemType
    {
        KeyItem,
        Evidence,
        Consumable,
        Tool,
        Cosmetic,
        Collectible      // discovered by walking (GPS Activity Mode)
    }

    [CreateAssetMenu(menuName = "CONTRACT/Item Data", fileName = "ItemData")]
    public sealed class ItemData : ScriptableObject
    {
        [Header("Identity")]
        [field: SerializeField] public string itemId { get; set; }
        [field: SerializeField] public string displayName { get; set; }
        [field: SerializeField] public string description { get; set; }

        [Header("Type & Behavior")]
        [field: SerializeField] public ItemType type { get; set; }
        [field: SerializeField] public bool isConsumable { get; set; }
        [SerializeField] private int maxStack = 1;
        public int MaxStack { get => maxStack; set => maxStack = value; }

        [Header("Value")]
        [field: SerializeField] public int sellPrice { get; set; }
        [field: SerializeField] public int buyPrice { get; set; }

        [Header("Visual")]
        [field: SerializeField] public Sprite icon { get; set; }
        [field: SerializeField] public GameObject worldPrefab { get; set; }

        [Header("Effects")]
        [field: SerializeField] public float suspicionReduction { get; set; }
        [field: SerializeField] public float staminaRestore { get; set; }
    }
}