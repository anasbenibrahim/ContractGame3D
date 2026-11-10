using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Contract.UI
{
    [System.Serializable]
    public class InventorySlotUI : MonoBehaviour
    {
        [SerializeField] private Image iconImage;
        [SerializeField] private TMP_Text countText;
        [SerializeField] private Button button;
        [SerializeField] private Image evidenceBadge;

        public void Setup(string itemId, Action onClick, bool isEvidence = false)
        {
            if (evidenceBadge != null) evidenceBadge.enabled = isEvidence;
            if (button != null)
            {
                button.onClick.RemoveAllListeners();
                button.onClick.AddListener(() => onClick?.Invoke());
            }
        }
    }
}
