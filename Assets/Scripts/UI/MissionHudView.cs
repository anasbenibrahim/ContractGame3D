using System.Collections.Generic;
using System.Linq;
using System.Text;
using Contract.Contracts;
using Contract.Core;
using Contract.Data;
using Contract.Missions;
using Contract.Regions;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Contract.UI
{
    /// <summary>
    /// Gameplay HUD for the economy and the active contract: balance with progress to $1,000,000,
    /// "+$" popups, the contract tracker (objective checklist, timer, reward) and toast messages.
    /// Event-driven; only the timer and fades update per frame.
    /// </summary>
    public sealed class MissionHudView : MonoBehaviour
    {
        [Header("Money")]
        [SerializeField] private TMP_Text moneyText;
        [SerializeField] private TMP_Text moneyDelta;
        [SerializeField] private Image goalFill;
        [SerializeField] private TMP_Text goalLabel;

        [Header("Contract tracker")]
        [SerializeField] private GameObject tracker;
        [SerializeField] private TMP_Text missionTitle;
        [SerializeField] private TMP_Text objectivesText;
        [SerializeField] private TMP_Text timerText;
        [SerializeField] private TMP_Text rewardText;
        [SerializeField] private TMP_Text idleHint;

        [Header("Toasts")]
        [SerializeField] private CanvasGroup toast;
        [SerializeField] private TMP_Text toastText;

        private readonly Queue<string> toasts = new Queue<string>();
        private float toastTimer;
        private float deltaTimer;
        private int shownMoney = -1;

        public string ObjectivesText => objectivesText != null ? objectivesText.text : string.Empty;
        public string MoneyText => moneyText != null ? moneyText.text : string.Empty;
        public string CurrentToast => toast != null && toast.alpha > 0.01f ? toastText.text : null;

        private static MissionManager Missions => MissionManager.Instance;

        private void OnEnable()
        {
            GameEvents.MoneyChanged += OnMoneyChanged;
            GameEvents.MissionStarted += OnContractChanged;
            GameEvents.ObjectiveUpdated += OnContractChanged;
            GameEvents.ObjectiveCompleted += OnContractChanged;
            GameEvents.MissionCompleted += OnContractChanged;
            GameEvents.MissionFailed += OnMissionFailed;
            GameEvents.MissionMessage += OnMessage;
            GameEvents.RegionUnlocked += OnRegionUnlocked;
        }

        private void OnDisable()
        {
            GameEvents.MoneyChanged -= OnMoneyChanged;
            GameEvents.MissionStarted -= OnContractChanged;
            GameEvents.ObjectiveUpdated -= OnContractChanged;
            GameEvents.ObjectiveCompleted -= OnContractChanged;
            GameEvents.MissionCompleted -= OnContractChanged;
            GameEvents.MissionFailed -= OnMissionFailed;
            GameEvents.MissionMessage -= OnMessage;
            GameEvents.RegionUnlocked -= OnRegionUnlocked;
        }

        private void Start()
        {
            if (toast != null) toast.alpha = 0f;
            if (moneyDelta != null) moneyDelta.alpha = 0f;
            OnMoneyChanged(GameManager.Instance?.Session?.money ?? 0);
            Refresh();
        }

        // ---- money ---------------------------------------------------------------------------

        private void OnMoneyChanged(int money)
        {
            if (shownMoney >= 0 && money > shownMoney && moneyDelta != null)
            {
                moneyDelta.text = $"+${money - shownMoney:N0}";
                moneyDelta.alpha = 1f;
                deltaTimer = 2.5f;
            }

            shownMoney = money;
            if (moneyText != null) moneyText.text = $"${money:N0}";
            float progress = Mathf.Clamp01(money / (float)GameConstants.VictoryMoney);
            if (goalFill != null) goalFill.fillAmount = progress;
            if (goalLabel != null) goalLabel.text = $"목표 · Goal  $1,000,000   <color=#FF4D5A>{progress * 100f:F1}%</color>";
        }

        // ---- contract tracker -------------------------------------------------------------

        private void OnContractChanged(string _) => Refresh();

        private void OnMissionFailed(string missionId, string reason) => Refresh();

        public void Refresh()
        {
            MissionData mission = Missions != null ? Missions.ActiveMission : null;
            if (tracker != null) tracker.SetActive(mission != null);
            if (idleHint != null) idleHint.gameObject.SetActive(mission == null);
            if (mission == null) return;

            missionTitle.text = $"{mission.missionName}\n<size=65%><color=#9AA3AE>{RegionName(mission.regionId)} · {new string('★', mission.difficulty)}</color></size>";
            rewardText.text = $"보상 · Reward  <b>${mission.rewardMoney:N0}</b>";

            MissionObjective current = Missions.CurrentObjective;
            var sb = new StringBuilder();
            foreach (MissionObjective o in mission.objectives)
            {
                string optional = o.isOptional ? " <size=70%><color=#9AA3AE>(선택 · optional)</color></size>" : "";
                if (Missions.IsObjectiveFailed(o.objectiveId)) sb.AppendLine($"<color=#FF4D5A>×</color> <s><color=#7A808A>{o.description}</color></s>{optional}");
                else if (Missions.IsObjectiveComplete(o.objectiveId)) sb.AppendLine($"<color=#4CD38A>✓</color> <color=#7A808A>{o.description}</color>{optional}");
                else if (o == current) sb.AppendLine($"<color=#FF4D5A>▶</color> <b>{o.description}</b>{optional}");
                else sb.AppendLine($"<color=#7A808A>○</color> {o.description}{optional}");
            }

            objectivesText.text = sb.ToString().TrimEnd();
        }

        private static string RegionName(string regionId) =>
            GameManager.Instance?.GetService<IRegionService>()?.GetRegion(regionId)?.regionName ?? regionId;

        // ---- toasts -----------------------------------------------------------------------

        private void OnMessage(string korean, string english) => toasts.Enqueue($"{korean}\n<size=70%><color=#C9CED8>{english}</color></size>");

        private void OnRegionUnlocked(string regionId) =>
            toasts.Enqueue($"새 지역 개방 · New region unlocked\n<size=70%><color=#C9CED8>{RegionName(regionId)}</color></size>");

        private void Update()
        {
            float dt = Time.unscaledDeltaTime;
            MissionData mission = Missions != null ? Missions.ActiveMission : null;
            if (mission != null && timerText != null)
            {
                float t = Missions.ActiveElapsedSeconds;
                string limit = mission.timeLimitMinutes > 0 ? $" / {mission.timeLimitMinutes:00}:00" : "";
                bool late = mission.timeLimitMinutes > 0 && t > mission.timeLimitMinutes * 60f;
                timerText.text = $"<color={(late ? "#7A808A" : "#FFFFFF")}>{(int)(t / 60):00}:{(int)(t % 60):00}{limit}</color>";
            }

            if (moneyDelta != null && deltaTimer > 0f)
            {
                deltaTimer -= dt;
                moneyDelta.alpha = Mathf.Clamp01(deltaTimer);
            }

            if (toast == null) return;
            if (toastTimer > 0f)
            {
                toastTimer -= dt;
                toast.alpha = Mathf.Clamp01(toastTimer * 3f);
            }
            else if (toasts.Count > 0)
            {
                toastText.text = toasts.Dequeue();
                toastTimer = 3f;
                toast.alpha = 1f;
            }
        }
    }
}
