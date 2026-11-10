using System.Linq;
using Contract.Core;
using Contract.Data;
using Contract.Regions;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Contract.UI
{
    /// <summary>
    /// Finances page of the safehouse terminal: balance and progress to the $1,000,000 goal,
    /// career figures (earned, spent, contracts, regions, play time), a balance-history chart
    /// built from the ledger, the latest transactions and every region's status and bonus.
    /// All numbers come straight from the session, so they match the save file.
    /// </summary>
    public sealed class FinanceView : MonoBehaviour
    {
        private const int ChartBars = 30;
        private const int LedgerRows = 12;

        [SerializeField] private TMP_Text balanceText;
        [SerializeField] private Image goalFill;
        [SerializeField] private TMP_Text goalText;
        [SerializeField] private TMP_Text statsText;
        [SerializeField] private RectTransform chart;
        [SerializeField] private TMP_Text chartCaption;
        [SerializeField] private RectTransform ledgerList;
        [SerializeField] private TMP_Text regionsText;

        public int ChartBarCount => chart.childCount;
        public int LedgerRowCount => ledgerList.childCount;
        public string StatsText => statsText.text;

        private void OnEnable()
        {
            GameEvents.MoneyChanged += OnMoneyChanged;
            Refresh();
        }

        private void OnDisable() => GameEvents.MoneyChanged -= OnMoneyChanged;

        private void OnMoneyChanged(int money) => Refresh();

        public void Refresh()
        {
            GameSession session = GameManager.Instance?.Session;
            if (session == null) return;

            int goal = GameConstants.VictoryMoney;
            balanceText.text = $"${session.money:N0}";
            goalFill.fillAmount = Mathf.Clamp01(session.money / (float)goal);
            goalText.text = session.victoryAchieved
                ? "<color=#4CD38A>목표 달성 · Goal reached</color>"
                : $"목표까지 · To goal  <b>${Mathf.Max(0, goal - session.money):N0}</b>   <color=#9AA3AE>({session.money / (float)goal:P1} of ${goal:N0})</color>";

            int spent = -session.ledger.Where(e => e.amount < 0).Sum(e => e.amount);
            int regionsTotal = Regions?.GetAllRegions().Count() ?? 0;
            int upgradeLevels = session.purchasedUpgradeIds.Count;
            statsText.text =
                $"총 수입 · Lifetime earnings<pos=58%><b>${session.lifetimeEarnings:N0}</b>\n" +
                $"총 지출 · Total spent<pos=58%><b>${spent:N0}</b>\n" +
                $"완료한 의뢰 · Contracts completed<pos=58%><b>{session.completedMissionIds.Count}</b>\n" +
                $"실패한 의뢰 · Contracts failed<pos=58%><b>{session.missionsFailed}</b>\n" +
                $"완료한 지역 · Regions completed<pos=58%><b>{session.completedRegionIds.Count}/{regionsTotal}</b>\n" +
                $"업그레이드 · Upgrade levels owned<pos=58%><b>{upgradeLevels}</b>\n" +
                $"플레이 시간 · Play time<pos=58%><b>{FormatTime(session.playTimeSeconds)}</b>";

            BuildChart(session);
            BuildLedger(session);
            BuildRegions(session);
        }

        public static string FormatTime(float seconds)
        {
            int total = Mathf.FloorToInt(seconds);
            return total >= 3600 ? $"{total / 3600}h {total % 3600 / 60:00}m" : $"{total / 60}m {total % 60:00}s";
        }

        private static IRegionService Regions => GameManager.Instance?.GetService<IRegionService>();

        /// <summary>Balance after each of the last transactions, scaled to the highest of them.</summary>
        private void BuildChart(GameSession session)
        {
            BoardRows.Clear(chart);
            LedgerEntry[] recent = session.ledger.Skip(Mathf.Max(0, session.ledger.Count - ChartBars)).ToArray();
            if (recent.Length == 0)
            {
                chartCaption.text = "<color=#7A808A>거래 없음 · No transactions yet</color>";
                return;
            }

            float max = Mathf.Max(1f, recent.Max(e => e.balanceAfter));
            float width = chart.rect.width > 1f ? chart.rect.width : 760f;
            float height = chart.rect.height > 1f ? chart.rect.height : 160f;
            float slot = width / ChartBars;
            for (int i = 0; i < recent.Length; i++)
            {
                GameObject bar = new GameObject("Bar", typeof(RectTransform), typeof(Image));
                bar.transform.SetParent(chart, false);
                RectTransform rt = (RectTransform)bar.transform;
                rt.anchorMin = rt.anchorMax = rt.pivot = Vector2.zero;
                rt.anchoredPosition = new Vector2(i * slot + 2f, 0f);
                rt.sizeDelta = new Vector2(slot - 4f, Mathf.Max(2f, recent[i].balanceAfter / max * height));
                Image image = bar.GetComponent<Image>();
                image.color = recent[i].amount >= 0 ? new Color(0.9f, 0.16f, 0.22f, 0.9f) : new Color(0.55f, 0.58f, 0.64f, 0.9f);
                image.raycastTarget = false;
            }

            chartCaption.text = $"잔액 추이 · Balance after each of the last {recent.Length} transactions   <color=#E62938>■</color> income   <color=#8C949F>■</color> spending";
        }

        private void BuildLedger(GameSession session)
        {
            BoardRows.Clear(ledgerList);
            LedgerEntry[] latest = session.ledger.AsEnumerable().Reverse().Take(LedgerRows).ToArray();
            if (latest.Length == 0)
            {
                Row("<color=#7A808A>아직 거래가 없습니다 · No transactions yet</color>");
                return;
            }

            foreach (LedgerEntry entry in latest)
            {
                string amount = entry.amount >= 0 ? $"<color=#4CD38A>+${entry.amount:N0}</color>" : $"<color=#FF4D5A>-${-entry.amount:N0}</color>";
                Row($"{entry.source}<pos=60%>{amount}<pos=80%><color=#9AA3AE>${entry.balanceAfter:N0}</color>");
            }
        }

        private void Row(string text)
        {
            GameObject row = new GameObject("Entry", typeof(RectTransform), typeof(LayoutElement));
            row.transform.SetParent(ledgerList, false);
            row.GetComponent<LayoutElement>().preferredHeight = 30f;
            ((RectTransform)row.transform).sizeDelta = new Vector2(0f, 30f); // the list does not control child height
            BoardRows.Label(row.transform, text, 16f);
        }

        private void BuildRegions(GameSession session)
        {
            IRegionService regions = Regions;
            if (regions == null)
            {
                regionsText.text = "";
                return;
            }

            regionsText.text = string.Join("\n", regions.GetAllRegions().OrderBy(r => r.completionBonus).ThenBy(r => r.regionName).Select(r =>
            {
                string status = session.IsRegionCompleted(r.regionId) ? "<color=#4CD38A>✓ 완료 · Complete</color>"
                    : regions.IsRegionPlayable(r.regionId) ? "<color=#FFFFFF>● 진행 가능 · Open</color>"
                    : regions.IsRegionUnlocked(r.regionId) ? "<color=#FFB020>준비 중 · Coming soon</color>"
                    : "<color=#7A808A>■ 잠김 · Locked</color>";
                return $"{r.regionName}<pos=42%>{status}<pos=78%><color=#9AA3AE>${r.completionBonus:N0}</color>";
            }));
        }
    }
}
