using System;
using Contract.Data;
using UnityEngine;

namespace Contract.Items
{
    [CreateAssetMenu(menuName = "CONTRACT/Item Library", fileName = "ItemLibrary")]
    public sealed class ItemLibrary : ScriptableObject
    {
        [field: SerializeField] public ItemEntry[] items { get; set; }

        public ItemData FindItem(string itemId)
        {
            if (items == null) return null;
            foreach (ItemEntry entry in items)
            {
                if (entry != null && entry.id == itemId)
                {
                    return entry.data;
                }
            }
            return null;
        }

        public Sprite FindIcon(string itemId)
        {
            ItemData data = FindItem(itemId);
            return data?.icon;
        }
    }

    [Serializable]
    public sealed class ItemEntry
    {
        public string id;
        public ItemData data;
    }
}