using System.Collections.Generic;
using System.Linq;
using System.Text;
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
    /// In-world contract board (safehouse terminal): regions with their status, the region's
    /// contracts, a detail view (story, subject, objectives, reward and bonus rules) and
    /// Start / Abandon. Uses the same MissionManager the Mission Selection scene uses.
    /// </summary>
    public sealed class ContractBoardView : MonoBehaviour
    {
        private const string MenuId = "contracts";

        [SerializeField] private GameObject panel;
        [SerializeField] private RectTransform regionTabs;
        [SerializeField] private RectTransform missionList;
        [SerializeField] private TMP_Text regionHeader;
        [SerializeField] private TMP_Text detailTitle;
        [SerializeField] private TMP_Text detailBody;
        [SerializeField] private TMP_Text detailObjectives;
        [SerializeField] private TMP_Text detailReward;
        [SerializeField] private TMP_Text statusText;
        [SerializeField] private Button startButton;
        [SerializeField] private Button abandonButton;
        [SerializeField] private Button closeButton;
        [Header("Terminal pages (contracts / upgrades / finances)")]
        [SerializeField] private GameObject contractsPage;
        [SerializeField] private GameObject upgradesPage;
        [SerializeField] private GameObject financePage;
        [SerializeField] private Button contractsTab;
        [SerializeField] private Button upgradesTab;
        [SerializeField] private Button financeTab;

        [Tooltip("Mission Selection scene: open on start, Back returns to character selection, accepting loads the city.")]
        [SerializeField] private bool sceneMode;

        private readonly List<Button> missionButtons = new List<Button>();
        private string regionId;
        private string selectedMissionId;

        public bool IsOpen => panel != null && panel.activeSelf;
        public string SelectedMissionId => selectedMissionId;
        public string RegionId => regionId;
        public IReadOnlyList<Button> MissionButtons => missionButtons;
        public Button StartButton => startButton;
        public TerminalPage Page { get; private set; } = TerminalPage.Contracts;

        public enum TerminalPage { Contracts, Upgrades, Finances }

        private static MissionManager Missions => MissionManager.Instance;
        private static IRegionService Regions => GameManager.Instance?.GetService<IRegionService>();

        private void Awake()
        {
            panel.SetActive(false);
            startButton.onClick.AddListener(StartSelected);
            abandonButton.onClick.AddListener(() =>
            {
                Missions?.AbandonMission();
                Rebuild();
            });
            closeButton.onClick.AddListener(Close);
            if (upgradesPage != null && upgradesPage.TryGetComponent(out UpgradeShopView shop)) shop.Wire();
            if (contractsTab != null) contractsTab.onClick.AddListener(() => ShowPage(TerminalPage.Contracts));
            if (upgradesTab != null) upgradesTab.onClick.AddListener(() => ShowPage(TerminalPage.Upgrades));
            if (financeTab != null) financeTab.onClick.AddListener(() => ShowPage(TerminalPage.Finances));
        }

        /// <summary>Switches the terminal page; the upgrade and finance pages refresh when shown.</summary>
        public void ShowPage(TerminalPage page)
        {
            Page = page;
            if (contractsPage != null) contractsPage.SetActive(page == TerminalPage.Contracts);
            if (upgradesPage != null) upgradesPage.SetActive(page == TerminalPage.Upgrades);
            if (financePage != null) financePage.SetActive(page == TerminalPage.Finances);
            Tint(contractsTab, page == TerminalPage.Contracts);
            Tint(upgradesTab, page == TerminalPage.Upgrades);
            Tint(financeTab, page == TerminalPage.Finances);
            if (page == TerminalPage.Contracts) Rebuild();
        }

        private static void Tint(Button tab, bool selected)
        {
            if (tab != null) tab.targetGraphic.color = selected ? BoardRows.Selected : BoardRows.Normal;
        }

        private void Start()
        {
            if (!sceneMode) return;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            Open();
        }

        private void OnEnable()
        {
            GameEvents.ContractBoardRequested += Open;
            GameEvents.MenuBack += OnBack;
        }

        private void OnDisable()
        {
            GameEvents.ContractBoardRequested -= Open;
            GameEvents.MenuBack -= OnBack;
            if (IsOpen && !sceneMode) Close(); // scene mode: the scene is unloading, don't navigate
        }

        private void OnBack()
        {
            if (IsOpen) Close();
        }

        private void Update()
        {
            // Mission Selection has no player to route Escape through: read it directly.
            if (!sceneMode) return;
            bool back = (UnityEngine.InputSystem.Keyboard.current != null && UnityEngine.InputSystem.Keyboard.current.escapeKey.wasPressedThisFrame)
                        || (UnityEngine.InputSystem.Gamepad.current != null && UnityEngine.InputSystem.Gamepad.current.buttonEast.wasPressedThisFrame);
            if (back) Close();
        }

        public void Open()
        {
            if (IsOpen) return;
            panel.SetActive(true);
            GameManager.Instance?.OpenMenu(MenuId);
            regionId = Missions?.ActiveMission?.regionId ?? Regions?.GetActiveRegionId();
            if (Regions != null && !ShownRegions(Regions).Any(r => r.regionId == regionId))
                regionId = ShownRegions(Regions).FirstOrDefault(r => Regions.IsRegionPlayable(r.regionId))?.regionId ?? ShownRegions(Regions).FirstOrDefault()?.regionId;
            selectedMissionId = Missions?.ActiveMission?.missionId;
            ShowPage(TerminalPage.Contracts);
        }

        public void Close()
        {
            if (sceneMode)
            {
                GameManager.Instance?.SceneLoader.LoadWorldSelection(); // Back to the destinations
                return;
            }

            panel.SetActive(false);
            GameManager.Instance?.CloseMenu(MenuId);
        }

        public void SelectRegion(string id)
        {
            regionId = id;
            selectedMissionId = null;
            Rebuild();
        }

        public void SelectMission(string missionId)
        {
            selectedMissionId = missionId;
            ShowDetail();
        }

        public void StartSelected()
        {
            if (Missions == null || string.IsNullOrEmpty(selectedMissionId)) return;
            if (!Missions.CanStart(selectedMissionId, out string reason))
            {
                statusText.text = reason;
                return;
            }

            Missions.StartMission(selectedMissionId);
            string district = Missions.GetMission(selectedMissionId).regionId;
            if (sceneMode)
            {
                DestinationManager.Instance.TravelTo(district); // into the contract's district with the contract active
                return;
            }

            Close(); // back into the city with the tracker showing the first objective ...
            if (GameConstants.Scenes.ForRegion(district) != UnityEngine.SceneManagement.SceneManager.GetActiveScene().name)
                DestinationManager.Instance.TravelTo(district); // ... after travelling if it is in another district
        }

        // ---- building ------------------------------------------------------------------------

        private void Rebuild()
        {
            BuildRegionTabs();
            BuildMissionList();
            if (string.IsNullOrEmpty(selectedMissionId)) selectedMissionId = Missions?.GetRegionMissions(regionId).FirstOrDefault()?.missionId;
            ShowDetail();
        }

        private void BuildRegionTabs()
        {
            Clear(regionTabs);
            IRegionService regions = Regions;
            if (regions == null) return;
            foreach (RegionData region in ShownRegions(regions).OrderBy(r => ProgressionDepth(r, regions)).ThenBy(r => r.regionName))
            {
                string status = regions.IsRegionPlayable(region.regionId) ? "" : regions.IsRegionUnlocked(region.regionId)
                    ? "  <size=70%><color=#FFB020>준비 중</color></size>" : "  <size=70%><color=#7A808A>잠김</color></size>";
                string id = region.regionId;
                Button tab = Row(regionTabs, $"{region.regionName}{status}", region.regionId == regionId, 200f);
                tab.onClick.AddListener(() => SelectRegion(id));
            }
        }

        /// <summary>The current destination's districts (all regions if destinations are not set up).</summary>
        private static IEnumerable<RegionData> ShownRegions(IRegionService regions)
        {
            DestinationData destination = DestinationManager.Instance?.Current;
            return destination != null ? DestinationManager.Instance.Districts(destination) : regions.GetAllRegions();
        }

        /// <summary>Position in the unlock chain (Gangnam 0, its successors 1, ...), for tab order.</summary>
        private static int ProgressionDepth(RegionData region, IRegionService regions, int guard = 0)
        {
            if (region.requiredRegionIds == null || region.requiredRegionIds.Length == 0 || guard > 8) return 0;
            return 1 + region.requiredRegionIds.Select(regions.GetRegion).Where(r => r != null).Select(r => ProgressionDepth(r, regions, guard + 1)).DefaultIfEmpty(0).Max();
        }

        private void BuildMissionList()
        {
            Clear(missionList);
            missionButtons.Clear();
            if (Missions == null) return;

            RegionData region = Regions?.GetRegion(regionId);
            int done = region?.missionIds?.Count(id => Missions.GetMissionState(id) == MissionState.Completed) ?? 0;
            int total = region?.GetMissionCount() ?? 0;
            string availability = Regions != null && Regions.IsRegionPlayable(regionId) ? "" :
                Regions != null && Regions.IsRegionUnlocked(regionId) ? "   <color=#FFB020>지역 준비 중 · District coming soon</color>" : "   <color=#FF4D5A>잠김 · Locked</color>";
            regionHeader.text = $"{region?.regionName}   <size=70%><color=#9AA3AE>{done}/{total} 완료 · completed   보너스 · Bonus ${region?.completionBonus ?? 0:N0}</color></size>{availability}";

            foreach (MissionData mission in Missions.GetRegionMissions(regionId))
            {
                string id = mission.missionId;
                Button row = Row(missionList, $"{StateChip(mission)}  {mission.missionName}   <color=#9AA3AE>${mission.rewardMoney:N0}</color>", id == selectedMissionId, 520f);
                row.onClick.AddListener(() => SelectMission(id));
                missionButtons.Add(row);
            }
        }

        private string StateChip(MissionData mission)
        {
            switch (Missions.GetMissionState(mission.missionId))
            {
                case MissionState.Completed: return "<color=#4CD38A>✓ 완료</color>";
                case MissionState.InProgress: return "<color=#FF4D5A>▶ 진행 중</color>";
                case MissionState.Failed: return "<color=#FFB020>× 실패</color>";
                default: return Missions.CanStart(mission.missionId, out _) ? "<color=#FFFFFF>● 가능</color>" : "<color=#7A808A>■ 잠김</color>";
            }
        }

        private void ShowDetail()
        {
            MissionData mission = Missions?.GetMission(selectedMissionId);
            bool hasMission = mission != null;
            detailTitle.text = hasMission ? mission.missionName : "의뢰를 선택하세요 · Select a contract";
            detailBody.text = hasMission
                ? $"{mission.description}\n\n<color=#9AA3AE>대상 · Subject:</color> {mission.subjectName}\n<color=#9AA3AE>난이도 · Difficulty:</color> {new string('★', mission.difficulty)}{new string('☆', 5 - mission.difficulty)}" +
                  (mission.timeLimitMinutes > 0 ? $"\n<color=#9AA3AE>제한 시간 · Time limit:</color> {mission.timeLimitMinutes} min" : "")
                : "";

            var sb = new StringBuilder();
            if (hasMission)
            {
                foreach (MissionObjective o in mission.objectives)
                {
                    sb.AppendLine($"<color=#FF4D5A>■</color> {o.description}{(o.isOptional ? " <color=#9AA3AE>(선택 · optional)</color>" : "")}");
                }
            }

            detailObjectives.text = sb.ToString().TrimEnd();
            detailReward.text = hasMission
                ? $"<size=130%><b>${mission.rewardMoney:N0}</b></size>\n<color=#9AA3AE>+20% 은신 · never spotted   +10% 신속 · within time   +10% per optional</color>"
                : "";

            bool active = hasMission && Missions.GetMissionState(mission.missionId) == MissionState.InProgress;
            string reason = null;
            bool startable = hasMission && !active && Missions.CanStart(mission.missionId, out reason);
            statusText.text = active ? "진행 중인 의뢰 · Contract in progress" : hasMission && !startable ? reason : "";

            startButton.gameObject.SetActive(!active);
            startButton.interactable = startable;
            abandonButton.gameObject.SetActive(active);
        }

        private static void Clear(RectTransform container) => BoardRows.Clear(container);

        private static Button Row(RectTransform parent, string label, bool selected, float width) => BoardRows.Row(parent, label, selected, width);
    }
}
