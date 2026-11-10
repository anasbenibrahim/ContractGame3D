using System.Linq;
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
    /// End-of-contract screens: success (full reward breakdown, new balance, progress to the
    /// $1,000,000 goal, region bonus), failure (reason + retry) and the victory screen.
    /// </summary>
    public sealed class MissionResultView : MonoBehaviour
    {
        private const string ResultMenu = "result";
        private const string VictoryMenu = "victory";

        [Header("Result")]
        [SerializeField] private GameObject resultPanel;
        [SerializeField] private TMP_Text resultTitle;
        [SerializeField] private TMP_Text resultSubtitle;
        [SerializeField] private TMP_Text breakdown;
        [SerializeField] private TMP_Text totalText;
        [SerializeField] private TMP_Text balanceText;
        [SerializeField] private Image goalFill;
        [SerializeField] private Button primaryButton;
        [SerializeField] private TMP_Text primaryLabel;
        [SerializeField] private Button secondaryButton;
        [SerializeField] private TMP_Text secondaryLabel;

        [Header("Victory")]
        [SerializeField] private GameObject victoryPanel;
        [SerializeField] private TMP_Text victoryStats;
        [SerializeField] private Button victoryContinue;
        [SerializeField] private Button victoryMainMenu;

        private MissionResult current;
        private bool victoryPending;

        public bool ResultOpen => resultPanel.activeSelf;
        public bool VictoryOpen => victoryPanel.activeSelf;
        public MissionResult Current => current;
        public string BreakdownText => breakdown.text;

        private void Awake()
        {
            resultPanel.SetActive(false);
            victoryPanel.SetActive(false);
            primaryButton.onClick.AddListener(OnPrimary);
            secondaryButton.onClick.AddListener(OnSecondary);
            victoryContinue.onClick.AddListener(CloseVictory);
            if (victoryMainMenu != null) victoryMainMenu.onClick.AddListener(() =>
            {
                CloseVictory();
                GameManager.Instance?.GetService<Contract.Contracts.ISaveService>()?.AutoSave();
                GameManager.Instance?.ReturnToMainMenu();
            });
        }

        private void OnEnable()
        {
            GameEvents.MissionResultReady += Show;
            GameEvents.VictoryReached += OnVictory;
            GameEvents.MenuBack += OnBack;
        }

        private void OnDisable()
        {
            GameEvents.MissionResultReady -= Show;
            GameEvents.VictoryReached -= OnVictory;
            GameEvents.MenuBack -= OnBack;
            if (ResultOpen) CloseResult();
            if (VictoryOpen) CloseVictory();
        }

        public void Show(MissionResult result)
        {
            current = result;
            resultPanel.SetActive(true);
            GameManager.Instance?.OpenMenu(ResultMenu);

            if (result.success)
            {
                resultTitle.text = "임무 완료  <size=55%><color=#9AA3AE>CONTRACT COMPLETE</color></size>";
                resultSubtitle.text = result.missionName;
                string Line(string label, int amount, string note = "") =>
                    $"{label}<pos=72%>{(amount > 0 ? $"+${amount:N0}" : "<color=#7A808A>—</color>")}  <size=75%><color=#9AA3AE>{note}</color></size>\n";
                breakdown.text =
                    Line("기본 보상 · Base reward", result.baseReward) +
                    Line("은신 보너스 · Stealth bonus", result.stealthBonus, result.detected ? "spotted" : "never spotted") +
                    Line("신속 보너스 · Speed bonus", result.speedBonus, $"{(int)(result.elapsedSeconds / 60):00}:{(int)(result.elapsedSeconds % 60):00}") +
                    Line("선택 목표 · Optional objectives", result.optionalBonus, $"{result.optionalCompleted}/{result.optionalTotal}") +
                    (result.negotiatorBonus > 0 ? Line("협상가 · Negotiator", result.negotiatorBonus, "upgrade") : "") +
                    (result.regionBonus > 0 ? Line($"지역 완료 · Region complete", result.regionBonus, RegionName(result.regionCompletedId)) : "");
                totalText.text = $"총 수익 · Total  <color=#4CD38A>+${result.totalReward + result.regionBonus:N0}</color>";
                primaryLabel.text = "계속 · Continue";
                secondaryLabel.text = "의뢰 게시판 · Contract board";
            }
            else
            {
                resultTitle.text = "임무 실패  <size=55%><color=#9AA3AE>CONTRACT FAILED</color></size>";
                resultSubtitle.text = result.missionName;
                breakdown.text = $"<color=#FF4D5A>{result.failureReason}</color>\n\n<color=#9AA3AE>No one was harmed. The contract can be retried.</color>";
                totalText.text = "수익 없음 · No payout";
                primaryLabel.text = "다시 시도 · Retry";
                secondaryLabel.text = "닫기 · Close";
            }

            balanceText.text = $"잔액 · Balance  <b>${result.newBalance:N0}</b>";
            goalFill.fillAmount = Mathf.Clamp01(result.newBalance / (float)GameConstants.VictoryMoney);
        }

        private static string RegionName(string regionId) =>
            GameManager.Instance?.GetService<IRegionService>()?.GetRegion(regionId)?.regionName ?? "";

        private void OnPrimary()
        {
            MissionResult result = current;
            CloseResult();
            if (result != null && !result.success) MissionManager.Instance?.StartMission(result.missionId); // retry
        }

        private void OnSecondary()
        {
            MissionResult result = current;
            CloseResult();
            if (result != null && result.success) GameEvents.RaiseContractBoardRequested();
        }

        private void OnBack()
        {
            if (VictoryOpen) CloseVictory();
            else if (ResultOpen) CloseResult();
        }

        public void CloseResult()
        {
            resultPanel.SetActive(false);
            GameManager.Instance?.CloseMenu(ResultMenu);
            if (victoryPending) ShowVictory();
        }

        // ---- victory ------------------------------------------------------------------------

        // Victory is raised by the economy while the contract is still being paid out, i.e. before
        // its result is published: defer to the end of the frame so the breakdown always comes first.
        private void OnVictory() => victoryPending = true;

        private void LateUpdate()
        {
            if (victoryPending && !ResultOpen) ShowVictory();
        }

        private void ShowVictory()
        {
            victoryPending = false;
            GameSession session = GameManager.Instance?.Session;
            victoryPanel.SetActive(true);
            GameManager.Instance?.OpenMenu(VictoryMenu);
            int spent = session == null ? 0 : -session.ledger.Where(e => e.amount < 0).Sum(e => e.amount);
            victoryStats.text = session == null ? "" :
                $"잔액 · Balance  <b>${session.money:N0}</b>     총 수입 · Lifetime earnings  ${session.lifetimeEarnings:N0}     총 지출 · Spent  ${spent:N0}\n" +
                $"완료한 의뢰 · Contracts  {session.completedMissionIds.Count}     실패 · Failed  {session.missionsFailed}     완료한 지역 · Regions  {session.completedRegionIds.Count}\n" +
                $"플레이 시간 · Play time  {FinanceView.FormatTime(session.playTimeSeconds)}\n\n" +
                "<size=80%>계속 플레이하면 남은 의뢰와 업그레이드를 즐길 수 있습니다 · Keep playing to finish the remaining contracts and upgrades.</size>";
            GameManager.Instance?.GetService<Contract.Contracts.ISaveService>()?.AutoSave(); // the win is on disk
        }

        public void CloseVictory()
        {
            victoryPanel.SetActive(false);
            GameManager.Instance?.CloseMenu(VictoryMenu);
        }
    }
}
