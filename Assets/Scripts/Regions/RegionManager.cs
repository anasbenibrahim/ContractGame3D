using System;
using System.Collections.Generic;
using System.Linq;
using Contract.Core;
using Contract.Data;
using Contract.Regions;
using UnityEngine;

namespace Contract.Regions
{
    /// <summary>
    /// Manages region unlocking, progression, and active region tracking.
    /// </summary>
    public sealed class RegionManager : MonoBehaviour, IRegionService
    {
        public static RegionManager Instance { get; private set; }

        [Header("Region Database")]
        [SerializeField] private RegionData[] allRegions;

        private Dictionary<string, RegionData> regionsById;
        private string activeRegionId;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void CreateInstance()
        {
            if (Instance != null) return;
            GameObject go = new GameObject("[RegionManager]");
            RegionManager service = go.AddComponent<RegionManager>();
            Instance = service;
            DontDestroyOnLoad(go);

            if (GameManager.Instance != null)
            {
                GameManager.Instance.RegisterService<IRegionService>(service);
            }
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

            if (GameManager.Instance != null)
            {
                GameManager.Instance.RegisterService<IRegionService>(this);
            }

            BuildLookup();
        }

        private void BuildLookup()
        {
            regionsById = new Dictionary<string, RegionData>();

            if (allRegions == null || allRegions.Length == 0)
            {
                allRegions = GameDatabase.Load()?.Regions;
            }
            if (allRegions != null)
            {
                foreach (RegionData data in allRegions)
                {
                    if (data != null && !string.IsNullOrEmpty(data.regionId))
                    {
                        regionsById[data.regionId] = data;
                    }
                }
            }
        }

        public RegionData GetRegion(string regionId)
        {
            if (regionsById != null && regionsById.TryGetValue(regionId, out RegionData data))
            {
                return data;
            }
            return null;
        }

        public bool IsRegionUnlocked(string regionId)
        {
            GameSession session = GameManager.Instance?.Session;
            if (session == null) return false;

            RegionData region = GetRegion(regionId);
            if (region == null) return false;

            if (region.unlockedByDefault) return true;
            return session.IsRegionUnlocked(regionId);
        }

        public void UnlockRegion(string regionId)
        {
            GameSession session = GameManager.Instance?.Session;
            if (session == null) return;

            RegionData region = GetRegion(regionId);
            if (region == null) return;

            if (session.UnlockRegion(regionId))
            {
                GameEvents.RaiseRegionUnlocked(regionId);
            }
        }

        /// <summary>
        /// Marks a region complete: pays its completion bonus exactly once (through the economy, so the
        /// HUD and the $1,000,000 victory check update) and unlocks every region whose prerequisites
        /// are now all complete. Returns the bonus paid (0 if the region was already complete).
        /// </summary>
        public int CompleteRegion(string regionId)
        {
            GameSession session = GameManager.Instance?.Session;
            RegionData region = GetRegion(regionId);
            if (session == null || region == null || session.IsRegionCompleted(regionId)) return 0;

            session.completedRegionIds.Add(regionId);
            int bonus = Mathf.Max(0, region.completionBonus);
            GameManager.Instance.GetService<Contract.Contracts.IEconomyService>()?.AddMoney(bonus, $"지역 완료 · {region.regionName}");
            GameEvents.RaiseRegionCompleted(regionId);

            foreach (RegionData next in allRegions)
            {
                if (next == null || IsRegionUnlocked(next.regionId) || next.requiredRegionIds == null || next.requiredRegionIds.Length == 0) continue;
                if (next.requiredRegionIds.All(session.IsRegionCompleted)) UnlockRegion(next.regionId);
            }

            return bonus;
        }

        /// <summary>Regions are playable once unlocked and their district exists in the world.</summary>
        public bool IsRegionPlayable(string regionId)
        {
            RegionData region = GetRegion(regionId);
            return region != null && region.districtBuilt && IsRegionUnlocked(regionId);
        }

        public IEnumerable<RegionData> GetAllRegions() => allRegions.Where(r => r != null);

        public IEnumerable<RegionData> GetUnlockedRegions()
        {
            GameSession session = GameManager.Instance?.Session;
            if (session == null) yield break;

            foreach (RegionData region in allRegions)
            {
                if (region != null && IsRegionUnlocked(region.regionId))
                {
                    yield return region;
                }
            }
        }

        /// <summary>The session's district (cleared by a new career); the last set one when no session exists.</summary>
        public string GetActiveRegionId()
        {
            GameSession session = GameManager.Instance?.Session;
            if (session == null) return activeRegionId;
            return string.IsNullOrEmpty(session.currentRegionId) ? null : session.currentRegionId;
        }

        public void SetActiveRegion(string regionId)
        {
            if (!regionsById.ContainsKey(regionId)) return;
            activeRegionId = regionId;

            GameSession session = GameManager.Instance?.Session;
            if (session != null)
            {
                session.currentRegionId = regionId;
            }
        }
    }
}