using System.Collections.Generic;
using Contract.Data;

namespace Contract.Missions
{
    public interface IMissionService
    {
        MissionData GetMission(string missionId);
        MissionState GetMissionState(string missionId);
        void StartMission(string missionId);
        void CompleteObjective(string missionId, string objectiveId);
        void CompleteMission(string missionId);
        void FailMission(string missionId);
        IEnumerable<MissionData> GetAvailableMissions(string regionId);
        IEnumerable<MissionData> GetCompletedMissions();

        MissionData ActiveMission { get; }
        bool CanStart(string missionId, out string reason);
        bool IsObjectiveComplete(string objectiveId);
        void AbandonMission();
    }
}
