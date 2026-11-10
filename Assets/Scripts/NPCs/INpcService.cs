using System;

namespace Contract.NPCs
{
    public interface INpcService
    {
        void SpawnCivilians();
        void SpawnMissionNPCs();
        void UpdateSuspicion(string npcId, float delta);
        void SetSuspicion(string npcId, float level);
        float GetSuspicion(string npcId);
        void AlertNPC(string npcId);
    }
}