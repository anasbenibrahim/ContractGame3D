using System.Collections.Generic;
using System.Linq;
using Contract.Activity;
using Contract.Contracts;
using Contract.Core;
using Contract.Data;
using Contract.Regions;

namespace Contract.Missions
{
    /// <summary>
    /// Activity missions (GPS Activity Mode) inside the same mission system as contracts. They are
    /// not started from the contract board: once available (by default or unlocked by another
    /// mission / milestone) they progress from real walking - distance, steps, explored areas
    /// counted from when they became available - and from checkpoint / collectible signals, then
    /// complete once with their rewards.
    /// </summary>
    public sealed partial class MissionManager
    {
        /// <summary>An activity (GPS) mission was completed.</summary>
        public static event System.Action<string> ActivityMissionCompleted;

        public IEnumerable<MissionData> ActivityMissions => missionsById?.Values.Where(m => m.category == MissionCategory.Activity).OrderBy(m => m.missionId)
                                                         ?? Enumerable.Empty<MissionData>();

        private void EnableActivityMissions()
        {
            ActivityController.Progressed += EvaluateActivityMissions;
            ActivityRewards.UnlockMission = UnlockMission;
        }

        private void DisableActivityMissions()
        {
            ActivityController.Progressed -= EvaluateActivityMissions;
            if (ActivityRewards.UnlockMission == (System.Action<string>)UnlockMission) ActivityRewards.UnlockMission = null;
        }

        public bool IsActivityAvailable(string missionId)
        {
            GameSession session = Session;
            MissionData mission = GetMission(missionId);
            if (session == null || mission == null || mission.category != MissionCategory.Activity) return false;
            if (session.completedActivityMissionIds.Contains(missionId)) return false;
            return mission.unlockedByDefault || session.unlockedMissionIds.Contains(missionId);
        }

        /// <summary>Makes an activity mission available (idempotent). Its progress counts from now.</summary>
        public void UnlockMission(string missionId)
        {
            GameSession session = Session;
            MissionData mission = GetMission(missionId);
            if (session == null || mission == null || mission.category != MissionCategory.Activity) return;
            if (session.unlockedMissionIds.Contains(missionId) || session.completedActivityMissionIds.Contains(missionId)) return;
            session.unlockedMissionIds.Add(missionId);
            Baseline(session, missionId, create: true);
            GameEvents.RaiseMissionMessage("새 GPS 임무", $"New GPS mission: {mission.missionName}");
        }

        /// <summary>Progress of the mission's first incomplete objective: (current, target).</summary>
        public (double current, double target) GetActivityProgress(string missionId)
        {
            MissionData mission = GetMission(missionId);
            GameSession session = Session;
            if (mission?.objectives == null || session == null) return (0, 0);
            MissionObjective objective = mission.objectives.FirstOrDefault(o => !o.isOptional && !ActivityObjectiveDone(session, mission, o)) ?? mission.objectives.FirstOrDefault();
            if (objective == null) return (0, 0);
            double target = objective.type == ObjectiveType.ReachCheckpoint || objective.type == ObjectiveType.FindCollectible ? 1 : objective.targetValue;
            return (System.Math.Min(target, Measure(session, mission, objective)), target);
        }

        /// <summary>Checks every available activity mission and completes those whose objectives are met.</summary>
        public void EvaluateActivityMissions()
        {
            GameSession session = Session;
            if (session == null) return;
            foreach (MissionData mission in ActivityMissions.ToList())
            {
                if (!IsActivityAvailable(mission.missionId)) continue;
                Baseline(session, mission.missionId, create: true);
                if (mission.objectives != null && mission.objectives.Where(o => !o.isOptional).All(o => ActivityObjectiveDone(session, mission, o)))
                {
                    CompleteActivityMission(mission);
                }
            }
        }

        private static ActivityBaseline Baseline(GameSession session, string missionId, bool create)
        {
            ActivityBaseline baseline = session.activityBaselines.FirstOrDefault(b => b.missionId == missionId);
            if (baseline == null && create)
            {
                // Missions available from the start count from the start of the career; unlocked ones from their unlock.
                bool fromStart = Instance != null && Instance.GetMission(missionId)?.unlockedByDefault == true;
                baseline = new ActivityBaseline
                {
                    missionId = missionId,
                    meters = fromStart ? 0 : session.gpsTotalRealMeters,
                    steps = fromStart ? 0 : session.gpsTotalSteps,
                    areas = fromStart ? 0 : session.exploredAreas.Count
                };
                session.activityBaselines.Add(baseline);
            }

            return baseline;
        }

        private static double Measure(GameSession session, MissionData mission, MissionObjective o)
        {
            ActivityBaseline b = Baseline(session, mission.missionId, create: false);
            switch (o.type)
            {
                case ObjectiveType.WalkDistance: return session.gpsTotalRealMeters - (b?.meters ?? session.gpsTotalRealMeters);
                case ObjectiveType.TakeSteps: return session.gpsTotalSteps - (b?.steps ?? session.gpsTotalSteps);
                case ObjectiveType.ExploreAreas: return session.exploredAreas.Count - (b?.areas ?? session.exploredAreas.Count);
                default: return session.activityObjectivesDone.Contains($"{mission.missionId}/{o.objectiveId}") ? 1 : 0;
            }
        }

        private static bool ActivityObjectiveDone(GameSession session, MissionData mission, MissionObjective o)
        {
            switch (o.type)
            {
                case ObjectiveType.WalkDistance:
                case ObjectiveType.TakeSteps:
                case ObjectiveType.ExploreAreas:
                    return Measure(session, mission, o) >= o.targetValue;
                default:
                    return session.activityObjectivesDone.Contains($"{mission.missionId}/{o.objectiveId}");
            }
        }

        /// <summary>Checkpoint / collectible signals complete matching objectives of available activity missions.</summary>
        private void OnActivitySignal(ObjectiveType type, string targetId)
        {
            GameSession session = Session;
            if (session == null || (type != ObjectiveType.ReachCheckpoint && type != ObjectiveType.FindCollectible)) return;
            bool any = false;
            foreach (MissionData mission in ActivityMissions)
            {
                if (!IsActivityAvailable(mission.missionId) || mission.objectives == null) continue;
                foreach (MissionObjective o in mission.objectives.Where(o => o.type == type && (string.IsNullOrEmpty(o.targetId) || o.targetId == targetId)))
                {
                    string key = $"{mission.missionId}/{o.objectiveId}";
                    if (session.activityObjectivesDone.Contains(key)) continue;
                    session.activityObjectivesDone.Add(key);
                    any = true;
                }
            }

            if (any) EvaluateActivityMissions();
        }

        private void CompleteActivityMission(MissionData mission)
        {
            GameSession session = Session;
            if (session.completedActivityMissionIds.Contains(mission.missionId)) return;
            session.completedActivityMissionIds.Add(mission.missionId); // recorded first: never paid twice
            string label = $"GPS 임무 · {mission.missionName}";
            if (mission.rewardMoney > 0) Economy?.AddMoney(mission.rewardMoney, label);
            if (mission.rewardXP > 0) Experience.Add(mission.rewardXP, label);
            foreach (string next in mission.unlocksMissionIds ?? new string[0]) UnlockMission(next);
            if (!string.IsNullOrEmpty(mission.unlockRegionId)) Regions?.UnlockRegion(mission.unlockRegionId);
            foreach (string _ in mission.rewardItemIds ?? new string[0]) ActivityWorld.Discover(session); // virtual objects to collect

            string rewards = string.Join(" · ", new[] { mission.rewardMoney > 0 ? $"+${mission.rewardMoney:N0}" : null, mission.rewardXP > 0 ? $"+{mission.rewardXP} XP" : null }.Where(r => r != null));
            GameEvents.RaiseMissionMessage($"{mission.koreanName} 완료", $"GPS mission complete: {mission.missionName} {rewards}".Trim());
            ActivityMissionCompleted?.Invoke(mission.missionId); // not MissionCompleted: that one means a contract
            GameManager.Instance.GetService<ISaveService>()?.AutoSave();
        }
    }
}
