using System;
using System.Collections.Generic;
using Contract.Data;

namespace Contract.Regions
{
    public interface IRegionService
    {
        RegionData GetRegion(string regionId);
        bool IsRegionUnlocked(string regionId);
        void UnlockRegion(string regionId);
        int CompleteRegion(string regionId);
        bool IsRegionPlayable(string regionId);
        IEnumerable<RegionData> GetAllRegions();
        IEnumerable<RegionData> GetUnlockedRegions();
        string GetActiveRegionId();
        void SetActiveRegion(string regionId);
    }
}