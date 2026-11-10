using System;
using Contract.Data;
using Contract.Missions;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Contract.UI
{
    [System.Serializable]
    public class MissionItemUI : MonoBehaviour
    {
        [SerializeField] private TMP_Text nameText;
        [SerializeField] private TMP_Text rewardText;
        [SerializeField] private Image completionIcon;
        [SerializeField] private Sprite completedSprite;
        [SerializeField] private Sprite availableSprite;
        [SerializeField] private Button button;

        public event Action OnSelected;

        private MissionData missionData;

        public void Setup(MissionData data)
        {
            missionData = data;
            MissionManager mgr = MissionManager.Instance;
            MissionState state = mgr?.GetMissionState(data.missionId) ?? MissionState.NotStarted;

            if (nameText != null) nameText.text = data.missionName;
            if (rewardText != null) rewardText.text = $"${data.rewardMoney:N0}";
            if (completionIcon != null)
            {
                completionIcon.sprite = state == MissionState.Completed ? completedSprite : availableSprite;
            }
            if (button != null)
            {
                button.onClick.RemoveAllListeners();
                button.onClick.AddListener(() => OnSelected?.Invoke());
            }
        }
    }
}
