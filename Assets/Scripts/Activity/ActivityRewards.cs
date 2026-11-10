using System;
using System.Collections.Generic;
using System.Linq;
using Contract.Contracts;
using Contract.Core;
using Contract.Regions;
using UnityEngine;

namespace Contract.Activity
{
    /// <summary>
    /// Awards activity milestones from the career totals. Each award is recorded in the session
    /// before it is paid, and counts are saved, so a milestone is never paid twice - not after a
    /// restart, a reload or a repeated evaluation.
    /// </summary>
    public static class ActivityRewards
    {
        /// <summary>Unlock hook for GPS missions (set by the mission layer in Phase 5).</summary>
        public static Action<string> UnlockMission;

        public static double Measure(GameSession session, MilestoneMeasure measure) =>
            measure == MilestoneMeasure.Steps ? session.gpsTotalSteps : session.gpsTotalRealMeters;

        /// <summary>Times a milestone is due given the totals (repeating: one per full threshold).</summary>
        public static int Due(ActivityMilestone m, double value)
        {
            if (m.threshold <= 0) return 0;
            if (!m.repeat) return value >= m.threshold ? 1 : 0;
            return (int)Math.Min(int.MaxValue, Math.Floor(value / m.threshold + 1e-9));
        }

        public static int Awarded(GameSession session, string id) => session.activityMilestones.FirstOrDefault(c => c.id == id)?.count ?? 0;

        /// <summary>Pays every milestone that is due and not yet awarded. Returns how many awards were made.</summary>
        public static int Evaluate(GameSession session, ActivityRewardsConfig config = null)
        {
            if (session == null) return 0;
            config ??= ActivityRewardsConfig.Load();
            int awards = 0;
            foreach (ActivityMilestone m in config.milestones)
            {
                if (m == null || string.IsNullOrEmpty(m.id)) continue;
                int due = Due(m, Measure(session, m.measure));
                MilestoneCount record = session.activityMilestones.FirstOrDefault(c => c.id == m.id);
                if (record == null)
                {
                    record = new MilestoneCount { id = m.id };
                    session.activityMilestones.Add(record);
                }

                while (record.count < due)
                {
                    record.count++;          // recorded first: a failure while paying can never pay twice
                    Grant(session, m);
                    awards++;
                }
            }

            if (awards > 0) GameManager.Instance?.GetService<ISaveService>()?.AutoSave();
            return awards;
        }

        private static void Grant(GameSession session, ActivityMilestone m)
        {
            string label = $"활동 · {m.title}";
            if (m.money > 0)
            {
                IEconomyService economy = GameManager.Instance?.GetService<IEconomyService>();
                if (economy != null) economy.AddMoney(m.money, label);
                else session.AddMoney(m.money);
            }

            if (m.xp > 0) Experience.Add(m.xp, label);
            if (!string.IsNullOrEmpty(m.unlockRegionId)) GameManager.Instance?.GetService<IRegionService>()?.UnlockRegion(m.unlockRegionId);
            if (!string.IsNullOrEmpty(m.unlockMissionId)) UnlockMission?.Invoke(m.unlockMissionId);
            if (!string.IsNullOrEmpty(m.itemId) && session.AddItem(m.itemId)) GameEvents.RaiseItemCollected(m.itemId);

            if (session.Settings.activityNotifications)
            {
                string reward = string.Join(" · ", new[]
                {
                    m.money > 0 ? $"+${m.money:N0}" : null, m.xp > 0 ? $"+{m.xp} XP" : null
                }.Where(r => r != null));
                GameEvents.RaiseMissionMessage($"{m.titleKorean} 달성", $"{m.title} reached {reward}".Trim());
            }

            GameEvents.RaiseActivityMilestoneReached(m.id);
        }
    }
}
