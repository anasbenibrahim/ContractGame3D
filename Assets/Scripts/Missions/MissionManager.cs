using System.Collections.Generic;
using System.Linq;
using Contract.Contracts;
using Contract.Core;
using Contract.Data;
using Contract.Regions;
using UnityEngine;

namespace Contract.Missions
{
    /// <summary>
    /// Runs contracts end to end: start -> objective signals from the world -> extraction ->
    /// reward breakdown -> money (via the economy) -> region progression -> autosave.
    /// Mission data assets are read-only; all progress lives in the GameSession, so it survives
    /// scene loads, is saved with the game and never leaks into the ScriptableObjects.
    /// </summary>
    public sealed partial class MissionManager : MonoBehaviour, IMissionService
    {
        public static MissionManager Instance { get; private set; }

        [Header("Mission Database")]
        [SerializeField] private MissionData[] allMissions;

        private Dictionary<string, MissionData> missionsById;
        private string failedMissionId;

        public MissionResult LastResult { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void CreateInstance()
        {
            if (Instance != null) return;
            GameObject go = new GameObject("[MissionManager]");
            MissionManager service = go.AddComponent<MissionManager>();
            Instance = service;
            DontDestroyOnLoad(go);
            GameManager.Instance?.RegisterService<IMissionService>(service);
        }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                DestroyImmediate(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);
            GameManager.Instance?.RegisterService<IMissionService>(this);
            BuildLookup();
        }

        private void OnEnable()
        {
            GameEvents.ObjectiveSignal += OnObjectiveSignal;
            GameEvents.NpcAlerted += OnNpcAlerted;
            EnableActivityMissions();
        }

        private void OnDisable()
        {
            GameEvents.ObjectiveSignal -= OnObjectiveSignal;
            GameEvents.NpcAlerted -= OnNpcAlerted;
            DisableActivityMissions();
        }

        private void BuildLookup()
        {
            missionsById = new Dictionary<string, MissionData>();
            if (allMissions == null || allMissions.Length == 0) allMissions = GameDatabase.Load()?.Missions;
            if (allMissions == null) return;
            foreach (MissionData data in allMissions)
            {
                if (data != null && !string.IsNullOrEmpty(data.missionId)) missionsById[data.missionId] = data;
            }
        }

        private static GameSession Session => GameManager.Instance?.Session;
        private static IRegionService Regions => GameManager.Instance?.GetService<IRegionService>();
        private static IEconomyService Economy => GameManager.Instance?.GetService<IEconomyService>();

        private void Update()
        {
            // Contract clock: scaled time, so it stops while paused.
            GameSession session = Session;
            if (session != null && !string.IsNullOrEmpty(session.activeMissionId)) session.activeMissionElapsed += Time.deltaTime;
        }

        // ---- queries --------------------------------------------------------------------------

        public MissionData GetMission(string missionId) =>
            missionId != null && missionsById != null && missionsById.TryGetValue(missionId, out MissionData data) ? data : null;

        public MissionData ActiveMission => GetMission(Session?.activeMissionId);

        public MissionState GetMissionState(string missionId)
        {
            GameSession session = Session;
            if (session == null || GetMission(missionId) == null) return MissionState.NotStarted;
            if (session.IsMissionCompleted(missionId) || session.completedActivityMissionIds.Contains(missionId)) return MissionState.Completed;
            if (GetMission(missionId).category == MissionCategory.Activity) return IsActivityAvailable(missionId) ? MissionState.InProgress : MissionState.NotStarted;
            if (session.activeMissionId == missionId) return MissionState.InProgress;
            if (failedMissionId == missionId) return MissionState.Failed;
            return MissionState.NotStarted;
        }

        public bool IsObjectiveComplete(string objectiveId) => Session != null && Session.activeObjectivesDone.Contains(objectiveId);

        public bool IsObjectiveFailed(string objectiveId) => Session != null && Session.activeObjectivesFailed.Contains(objectiveId);

        public float ActiveElapsedSeconds => Session?.activeMissionElapsed ?? 0f;

        /// <summary>The first incomplete required objective (what the HUD highlights).</summary>
        public MissionObjective CurrentObjective
        {
            get
            {
                MissionData mission = ActiveMission;
                return mission?.objectives?.FirstOrDefault(o => !o.isOptional && !IsObjectiveComplete(o.objectiveId) && o.type != ObjectiveType.AvoidDetection);
            }
        }

        public bool CanStart(string missionId, out string reason)
        {
            MissionData mission = GetMission(missionId);
            GameSession session = Session;
            if (mission == null || session == null) { reason = "알 수 없는 임무 · Unknown contract"; return false; }
            if (mission.category == MissionCategory.Activity) { reason = "GPS 활동 임무 · Progresses by walking"; return false; }
            if (session.IsMissionCompleted(missionId)) { reason = "이미 완료됨 · Already completed"; return false; }
            IRegionService regions = Regions;
            if (regions != null && mission.requiresRegionUnlocked && !regions.IsRegionUnlocked(mission.regionId))
            {
                reason = "지역 잠김 · Region locked";
                return false;
            }

            if (regions != null && !regions.IsRegionPlayable(mission.regionId))
            {
                reason = "지역 준비 중 · District not available yet";
                return false;
            }

            reason = null;
            return true;
        }

        public IEnumerable<MissionData> GetAvailableMissions(string regionId) =>
            missionsById == null ? Enumerable.Empty<MissionData>() : missionsById.Values.Where(m => m.regionId == regionId && CanStart(m.missionId, out _));

        /// <summary>Every mission of a region regardless of state, in region order (contract board).</summary>
        public IEnumerable<MissionData> GetRegionMissions(string regionId)
        {
            RegionData region = Regions?.GetRegion(regionId);
            if (region?.missionIds == null) return Enumerable.Empty<MissionData>();
            return region.missionIds.Select(GetMission).Where(m => m != null);
        }

        public IEnumerable<MissionData> GetCompletedMissions() =>
            missionsById == null ? Enumerable.Empty<MissionData>() : missionsById.Values.Where(m => GetMissionState(m.missionId) == MissionState.Completed);

        // ---- lifecycle ------------------------------------------------------------------------

        public void StartMission(string missionId)
        {
            if (!CanStart(missionId, out string reason))
            {
                GameEvents.RaiseMissionMessage("임무를 시작할 수 없습니다", $"Cannot start contract: {reason}");
                return;
            }

            GameSession session = Session;
            if (session.activeMissionId == missionId) return;
            if (!string.IsNullOrEmpty(session.activeMissionId)) AbandonMission(); // only one contract at a time

            failedMissionId = null;
            session.ClearActiveMission();
            session.activeMissionId = missionId;
            Regions?.SetActiveRegion(GetMission(missionId).regionId);
            GameEvents.RaiseMissionStarted(missionId);
            AnnounceCurrentObjective();
        }

        public void AbandonMission()
        {
            GameSession session = Session;
            if (session == null || string.IsNullOrEmpty(session.activeMissionId)) return;
            string id = session.activeMissionId;
            session.ClearActiveMission();
            GameEvents.RaiseMissionMessage("임무 포기", $"Contract abandoned: {GetMission(id)?.missionName}");
            GameEvents.RaiseObjectiveUpdated(string.Empty);
        }

        private void OnObjectiveSignal(ObjectiveType type, string targetId)
        {
            OnActivitySignal(type, targetId);
            MissionData mission = ActiveMission;
            if (mission?.objectives == null) return;

            foreach (MissionObjective objective in mission.objectives)
            {
                if (objective.type != type || IsObjectiveComplete(objective.objectiveId) || IsObjectiveFailed(objective.objectiveId)) continue;
                if (objective.targetId != targetId && objective.targetLocationId != targetId) continue;

                if (type == ObjectiveType.Extract && !ReadyToExtract(mission))
                {
                    GameEvents.RaiseMissionMessage("아직 탈출할 수 없습니다", "Finish the remaining objectives before extracting");
                    return;
                }

                CompleteObjective(mission.missionId, objective.objectiveId);
                return;
            }
        }

        private bool ReadyToExtract(MissionData mission) => mission.objectives
            .Where(o => !o.isOptional && o.type != ObjectiveType.Extract && o.type != ObjectiveType.AvoidDetection)
            .All(o => IsObjectiveComplete(o.objectiveId));

        public void CompleteObjective(string missionId, string objectiveId)
        {
            GameSession session = Session;
            MissionData mission = GetMission(missionId);
            MissionObjective objective = mission?.FindObjective(objectiveId);
            if (session == null || objective == null || session.activeMissionId != missionId || IsObjectiveComplete(objectiveId)) return;

            session.activeObjectivesDone.Add(objectiveId);
            GameEvents.RaiseObjectiveCompleted(objectiveId);
            GameEvents.RaiseMissionMessage("목표 완료", objective.description);

            if (objective.type == ObjectiveType.Extract) CompleteMission(missionId);
            else AnnounceCurrentObjective();
        }

        private void AnnounceCurrentObjective()
        {
            MissionObjective next = CurrentObjective;
            if (Session != null) Session.currentObjectiveId = next?.objectiveId ?? string.Empty;
            GameEvents.RaiseObjectiveUpdated(next?.objectiveId ?? string.Empty);
        }

        private void OnNpcAlerted(string npcId)
        {
            GameSession session = Session;
            MissionData mission = ActiveMission;
            if (session == null || mission == null) return;

            session.activeMissionDetected = true;
            foreach (MissionObjective objective in mission.objectives.Where(o => o.type == ObjectiveType.AvoidDetection))
            {
                if (IsObjectiveFailed(objective.objectiveId)) continue;
                session.activeObjectivesFailed.Add(objective.objectiveId);
                if (!objective.isOptional)
                {
                    FailMission(mission.missionId, "경비원에게 발각됨 · You were spotted by security");
                    return;
                }
            }

            GameEvents.RaiseMissionMessage("발각됨", "Spotted - the stealth bonus is lost");
        }

        public void CompleteMission(string missionId)
        {
            GameSession session = Session;
            MissionData mission = GetMission(missionId);
            if (session == null || mission == null || session.IsMissionCompleted(missionId)) return; // never pay twice

            // Stealth objectives succeed if the player was never spotted.
            foreach (MissionObjective o in mission.objectives.Where(o => o.type == ObjectiveType.AvoidDetection && !IsObjectiveFailed(o.objectiveId)))
            {
                if (!session.activeObjectivesDone.Contains(o.objectiveId)) session.activeObjectivesDone.Add(o.objectiveId);
            }

            MissionObjective[] optional = mission.objectives.Where(o => o.isOptional).ToArray();
            int optionalDone = optional.Count(o => IsObjectiveComplete(o.objectiveId));
            MissionResult result = RewardCalculator.Calculate(mission, session.activeMissionDetected, session.activeMissionElapsed, optionalDone, optional.Length,
                PlayerUpgrades.Bonus(UpgradeEffect.MissionReward));

            session.AddMissionCompletion(missionId);
            Economy?.AddMoney(result.totalReward, $"임무 완료 · {mission.missionName}");
            foreach (string itemId in mission.rewardItemIds ?? new string[0])
            {
                if (session.AddItem(itemId)) GameEvents.RaiseItemAddedToInventory(itemId);
            }

            session.ClearActiveMission();

            // Region progression: all of the region's contracts done -> bonus + unlocks.
            RegionData region = Regions?.GetRegion(mission.regionId);
            if (region?.missionIds != null && region.missionIds.All(session.IsMissionCompleted))
            {
                result.regionBonus = Regions.CompleteRegion(region.regionId);
                result.regionCompletedId = result.regionBonus > 0 ? region.regionId : null;
            }

            result.newBalance = session.money;
            LastResult = result;
            GameEvents.RaiseMissionCompleted(missionId);
            GameEvents.RaiseMissionResultReady(result);
            GameEvents.RaiseObjectiveUpdated(string.Empty);
            GameManager.Instance.GetService<ISaveService>()?.AutoSave();
        }

        public void FailMission(string missionId) => FailMission(missionId, "임무 실패 · Contract failed");

        /// <summary>Fails the active contract (non-lethal: the player can retry it straight away).</summary>
        public void FailMission(string missionId, string reason)
        {
            GameSession session = Session;
            MissionData mission = GetMission(missionId);
            if (session == null || mission == null || session.activeMissionId != missionId) return;

            failedMissionId = missionId;
            session.missionsFailed++;
            session.ClearActiveMission();
            LastResult = new MissionResult { missionId = missionId, missionName = mission.missionName, success = false, failureReason = reason, newBalance = session.money };
            GameEvents.RaiseMissionFailed(missionId, reason);
            GameEvents.RaiseMissionResultReady(LastResult);
            GameEvents.RaiseObjectiveUpdated(string.Empty);
        }
    }
}
