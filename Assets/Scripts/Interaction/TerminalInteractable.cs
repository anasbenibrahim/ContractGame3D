using Contract.Core;
using Contract.Data;
using TMPro;
using UnityEngine;

namespace Contract.Interaction
{
    /// <summary>
    /// Fictional computer terminal / device. Hold interact to access it; raises an Interact
    /// objective signal with <see cref="targetId"/> on completion. Optional world-space screen text.
    /// </summary>
    public sealed class TerminalInteractable : InteractableBase
    {
        [SerializeField] private string targetId = "terminal";
        [SerializeField] private string actionLabel = "단말기 접속 · Access terminal";
        [SerializeField] private string description;
        [SerializeField] private float holdSeconds = 3f;
        [SerializeField] private bool singleUse = true;
        [Tooltip("If set, only usable while this contract is active.")]
        [SerializeField] private string missionId;

        [Header("Screen (optional)")]
        [SerializeField] private TMP_Text screen;
        [SerializeField] private string idleScreen = "보안 단말기\nSECURE TERMINAL";
        [SerializeField] private string doneScreen = "접속 완료\nACCESS GRANTED";

        public string TargetId => targetId;
        public bool Used { get; private set; }

        public override float HoldDuration => holdSeconds;
        public override string Description => description;

        public void Configure(string id, string label, string desc, float seconds, TMP_Text screenText)
        {
            targetId = id;
            actionLabel = label;
            description = desc;
            holdSeconds = seconds;
            screen = screenText;
            RefreshScreen();
        }

        private void Start() => RefreshScreen();

        private void OnEnable() => GameEvents.MissionStarted += OnMissionStarted;

        private void OnDisable() => GameEvents.MissionStarted -= OnMissionStarted;

        private void OnMissionStarted(string startedMissionId)
        {
            if (string.IsNullOrEmpty(missionId) || startedMissionId != missionId) return;
            Used = false; // retrying the contract re-arms its terminal
            RefreshScreen();
        }

        public override bool CanInteract(GameObject interactor) =>
            !(singleUse && Used) && (string.IsNullOrEmpty(missionId) || GameManager.Instance?.Session?.activeMissionId == missionId);

        public void SetMission(string id) => missionId = id;

        public override string GetPrompt(GameObject interactor) => $"[E] {actionLabel}";

        public override void Interact(GameObject interactor)
        {
            if (!CanInteract(interactor)) return;
            Used = true;
            RefreshScreen();
            GameEvents.RaiseInteractionPerformed($"terminal:{targetId}");
            GameEvents.RaiseObjectiveSignal(ObjectiveType.Interact, targetId);
        }

        private void RefreshScreen()
        {
            if (screen != null) screen.text = Used ? doneScreen : idleScreen;
        }
    }
}
