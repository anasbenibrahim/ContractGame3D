using System.Collections.Generic;
using System.Linq;
using Contract.Contracts;
using Contract.Core;
using Contract.Data;
using UnityEngine;

namespace Contract.Regions
{
    /// <summary>
    /// South Korea destinations (Seoul, Busan, Jeju, Incheon, Gyeongju, Daegu): which are built,
    /// unlocked and selected, unlocking with in-game money, completion and the districts each
    /// one offers. A destination is "built" only when one of its districts is built and its scene
    /// is in the build, so nothing without content can be unlocked or entered.
    /// </summary>
    public sealed class DestinationManager : MonoBehaviour
    {
        public static DestinationManager Instance { get; private set; }

        private DestinationData[] all = new DestinationData[0];

        private static GameSession Session => GameManager.Instance?.Session;
        private static IRegionService Regions => GameManager.Instance?.GetService<IRegionService>();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void CreateInstance()
        {
            if (Instance == null) new GameObject("[DestinationManager]").AddComponent<DestinationManager>();
        }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);
            all = GameDatabase.Load()?.Destinations?.Where(d => d != null).ToArray() ?? new DestinationData[0];
        }

        public IEnumerable<DestinationData> All => all;

        public DestinationData Get(string id) => all.FirstOrDefault(d => d.destinationId == id);

        public DestinationData ForRegion(string regionId) => all.FirstOrDefault(d => d.districts.Any(r => r != null && r.regionId == regionId));

        /// <summary>The selected destination (defaults to the first unlocked built one).</summary>
        public DestinationData Current
        {
            get
            {
                DestinationData selected = Get(Session?.currentDestinationId);
                return selected != null && IsUnlocked(selected) && IsBuilt(selected) ? selected : all.FirstOrDefault(d => IsUnlocked(d) && IsBuilt(d));
            }
        }

        // ---- state -----------------------------------------------------------------------------

        /// <summary>A district is built when its content exists and its scene is in the build.</summary>
        public static bool IsDistrictBuilt(RegionData region) =>
            region != null && region.districtBuilt && !string.IsNullOrEmpty(region.sceneName) && Application.CanStreamedLevelBeLoaded(region.sceneName);

        public bool IsBuilt(DestinationData d) => d != null && d.districts.Any(IsDistrictBuilt);

        public bool IsUnlocked(DestinationData d) =>
            d != null && Session != null && (d.unlockedByDefault || Session.unlockedDestinationIds.Contains(d.destinationId));

        public bool CanUnlock(DestinationData d, out string reason)
        {
            reason = null;
            if (d == null || Session == null) reason = "알 수 없는 목적지 · Unknown destination";
            else if (IsUnlocked(d)) reason = "이미 열림 · Already unlocked";
            else if (!IsBuilt(d)) reason = "개발 중 · In development: not playable yet";
            else if (Session.money < d.unlockCost) reason = $"자금 부족 · Needs ${d.unlockCost:N0}";
            return reason == null;
        }

        /// <summary>Pays the unlock cost through the economy (ledger) and unlocks the destination.</summary>
        public bool Unlock(DestinationData d)
        {
            if (!CanUnlock(d, out _)) return false;
            IEconomyService economy = GameManager.Instance.GetService<IEconomyService>();
            if (d.unlockCost > 0 && (economy == null || !economy.TrySpend(d.unlockCost, $"목적지 · {d.displayName}"))) return false;
            Session.unlockedDestinationIds.Add(d.destinationId);
            GameEvents.RaiseMissionMessage($"{d.koreanName} 열림", $"Destination unlocked: {d.displayName}");
            GameManager.Instance.GetService<ISaveService>()?.AutoSave();
            return true;
        }

        /// <summary>Selects the destination the contract board and travel use.</summary>
        public bool Select(DestinationData d)
        {
            if (!IsUnlocked(d) || !IsBuilt(d) || Session == null) return false;
            Session.currentDestinationId = d.destinationId;
            return true;
        }

        // ---- districts and progress ------------------------------------------------------------------

        public IEnumerable<RegionData> Districts(DestinationData d) => d?.districts.Where(r => r != null) ?? Enumerable.Empty<RegionData>();

        /// <summary>Districts the player can enter now (built and unlocked).</summary>
        public IEnumerable<RegionData> PlayableDistricts(DestinationData d) =>
            Districts(d).Where(r => IsDistrictBuilt(r) && Regions != null && Regions.IsRegionUnlocked(r.regionId));

        public bool CanUnlockDistrict(RegionData region, out string reason)
        {
            reason = null;
            if (region == null || Session == null || Regions == null) reason = "알 수 없는 지역 · Unknown district";
            else if (Regions.IsRegionUnlocked(region.regionId)) reason = "이미 열림 · Already unlocked";
            else if (!IsDistrictBuilt(region)) reason = "개발 중 · In development";
            else if (!IsUnlocked(ForRegion(region.regionId))) reason = "목적지 잠김 · Unlock the destination first";
            else if (Session.money < region.unlockCost) reason = $"자금 부족 · Needs ${region.unlockCost:N0}";
            return reason == null;
        }

        public bool UnlockDistrict(RegionData region)
        {
            if (!CanUnlockDistrict(region, out _)) return false;
            IEconomyService economy = GameManager.Instance.GetService<IEconomyService>();
            if (region.unlockCost > 0 && (economy == null || !economy.TrySpend(region.unlockCost, $"지역 · {region.regionName}"))) return false;
            Regions.UnlockRegion(region.regionId);
            GameManager.Instance.GetService<ISaveService>()?.AutoSave();
            return true;
        }

        public int ContractCount(DestinationData d) => Districts(d).Sum(r => r.missionIds?.Length ?? 0);

        /// <summary>Share of the destination's contracts completed (0-1).</summary>
        public float Completion(DestinationData d)
        {
            int total = ContractCount(d);
            if (total == 0 || Session == null) return 0f;
            int done = Districts(d).Sum(r => r.missionIds?.Count(Session.IsMissionCompleted) ?? 0);
            return done / (float)total;
        }

        /// <summary>Loads a district's scene with the destination's loading screen (name, preview, tip).</summary>
        public void TravelTo(string regionId)
        {
            RegionData region = Regions?.GetRegion(regionId);
            DestinationData destination = ForRegion(regionId);
            if (destination != null) Select(destination);
            GameManager.Instance.SceneLoader.SetLoadingInfo(new SceneLoader.LoadingInfo
            {
                title = region?.regionName ?? destination?.displayName ?? "",
                subtitle = destination != null ? $"{destination.koreanName} · {destination.displayName}" : "",
                tip = Tip(destination),
                preview = destination?.preview
            });
            GameManager.Instance.SceneLoader.LoadDistrict(regionId);
        }

        /// <summary>A loading-screen tip for the destination (cycles by call).</summary>
        public string Tip(DestinationData d)
        {
            if (d?.loadingTips == null || d.loadingTips.Length == 0) return string.Empty;
            return d.loadingTips[Mathf.Abs(System.Environment.TickCount) % d.loadingTips.Length];
        }
    }
}
