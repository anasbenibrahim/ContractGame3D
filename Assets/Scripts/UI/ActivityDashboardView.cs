using System;
using System.Linq;
using Contract.Activity;
using Contract.Core;
using Contract.Data;
using Contract.Missions;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Contract.UI
{
    /// <summary>
    /// REAL GPS ACTIVITY dashboard: GPS status, real / virtual distance, steps, session time,
    /// speed, XP earned, the current GPS mission with an animated progress bar, the next
    /// checkpoint, collectibles waiting, and the session controls (start / pause / resume / stop),
    /// mark-a-spot, collect, map and GPS setup. Also the Normal / GPS Life mode switch.
    /// Text refreshes four times a second; only the bars animate every frame.
    /// </summary>
    public sealed class ActivityDashboardView : MonoBehaviour
    {
        private const string MenuId = "activity";
        private const float RefreshInterval = 0.25f;

        [SerializeField] private GameObject panel;
        [SerializeField] private Button[] openButtons;
        [SerializeField] private GameObject returnTo;
        [SerializeField] private Button closeButton;

        [Header("Mode")]
        [SerializeField] private TMP_Text modeLabel;
        [SerializeField] private Button modeButton;
        [SerializeField] private TMP_Text modeButtonLabel;
        [SerializeField] private TMP_Text modeHint;

        [Header("Status")]
        [SerializeField] private Image statusLight;
        [SerializeField] private TMP_Text statusText;
        [SerializeField] private TMP_Text bannerText;

        [Header("Stats")]
        [SerializeField] private TMP_Text realDistance;
        [SerializeField] private TMP_Text virtualDistance;
        [SerializeField] private TMP_Text steps;
        [SerializeField] private TMP_Text sessionTime;
        [SerializeField] private TMP_Text speed;
        [SerializeField] private TMP_Text xpEarned;
        [SerializeField] private TMP_Text coordinates;

        [Header("Progress")]
        [SerializeField] private TMP_Text missionText;
        [SerializeField] private Image missionFill;
        [SerializeField] private TMP_Text checkpointText;
        [SerializeField] private TMP_Text collectiblesText;
        [SerializeField] private TMP_Text missionList;

        [Header("Controls")]
        [SerializeField] private Button primaryButton;
        [SerializeField] private TMP_Text primaryLabel;
        [SerializeField] private Button stopButton;
        [SerializeField] private Button markButton;
        [SerializeField] private Button collectButton;
        [SerializeField] private Button mapButton;
        [SerializeField] private TMP_Text mapLabel;
        [SerializeField] private Button setupButton;
        [SerializeField] private Button coordinatesButton;
        [SerializeField] private LocationAccessView access;

        private float timer;
        private float missionTarget;
        private bool showCoordinates;

        public bool IsOpen => panel.activeSelf;
        public int RefreshCount { get; private set; }
        public Button PrimaryButton => primaryButton;
        public Button StopButton => stopButton;
        public Button ModeButton => modeButton;
        public Button MarkButton => markButton;
        public Button CollectButton => collectButton;
        public Button MapButton => mapButton;
        public string Text(string field) => ((TMP_Text)GetType().GetField(field, System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).GetValue(this)).text;
        public float MissionFill => missionFill.fillAmount;

        private static ActivityController Activity => ActivityController.Instance;
        private static GameSession Session => GameManager.Instance?.Session;
        private static bool InCity => GameConstants.Scenes.InGameplay;

        private void Awake()
        {
            foreach (Button b in openButtons ?? new Button[0]) b.onClick.AddListener(Open);
            closeButton.onClick.AddListener(Close);
            primaryButton.onClick.AddListener(OnPrimary);
            stopButton.onClick.AddListener(() => { Activity.Stop(); Refresh(); });
            modeButton.onClick.AddListener(ToggleMode);
            markButton.onClick.AddListener(MarkSpot);
            collectButton.onClick.AddListener(CollectAll);
            mapButton.onClick.AddListener(OpenMap);
            coordinatesButton.onClick.AddListener(() => { showCoordinates = !showCoordinates; Refresh(); });
            panel.SetActive(false);
        }

        /// <summary>Wires extra open buttons that live in a scene (the main menu's GPS ACTIVITY button).</summary>
        public void AddOpenButton(Button button) => button.onClick.AddListener(Open);

        public void Open()
        {
            if (returnTo != null) returnTo.SetActive(false);
            panel.SetActive(true);
            GameManager.Instance?.OpenMenu(MenuId);
            Refresh();
        }

        public void Close()
        {
            if (access != null && access.IsOpen) access.Close();
            panel.SetActive(false);
            GameManager.Instance?.CloseMenu(MenuId);
            if (returnTo != null) returnTo.SetActive(true);
        }

        private void OnDisable() => GameManager.Instance?.CloseMenu(MenuId);

        // ---- actions ------------------------------------------------------------------------------

        private void OnPrimary()
        {
            if (!Activity.CanUseGpsMode(out _))
            {
                access.Open(); // explain what is missing and how to fix it
                return;
            }

            if (Activity.IsRunning) Activity.Pause();
            else if (Activity.IsPaused) Activity.Resume();
            else Activity.StartSession();
            Refresh();
        }

        private void ToggleMode()
        {
            if (!Activity.GpsModeActive && !Activity.CanUseGpsMode(out _))
            {
                access.Open();
                return;
            }

            Activity.GpsModeActive = !Activity.GpsModeActive;
            Refresh();
        }

        private void MarkSpot()
        {
            GeoFix? fix = LocationTracker.Instance?.LatestFix;
            PlayerCheckpoint marked = fix.HasValue ? ActivityWorld.PlaceCheckpoint(Session, fix.Value, $"Spot {Session.playerCheckpoints.Count + 1}") : null;
            GameEvents.RaiseMissionMessage(marked != null ? "지점 표시됨" : "표시할 수 없음",
                marked != null ? "Spot marked: walk on, then come back to it." : "Needs a good GPS fix during a session (and at most 5 open spots).");
            Refresh();
        }

        private void CollectAll()
        {
            foreach (string id in Session.pendingCollectibleIds.ToList()) ActivityWorld.Collect(Session, id);
            Refresh();
        }

        private void OpenMap()
        {
            if (!InCity) return;
            Close();
            GameManager.Instance?.OpenMap();
        }

        // ---- presentation -------------------------------------------------------------------------

        private void Update()
        {
            if (!panel.activeSelf) return;
            bool back = (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
                        || (Gamepad.current != null && Gamepad.current.buttonEast.wasPressedThisFrame);
            if (back && (access == null || !access.IsOpen))
            {
                Close();
                return;
            }

            // Smooth bar, throttled text.
            missionFill.fillAmount = Mathf.MoveTowards(missionFill.fillAmount, missionTarget, Time.unscaledDeltaTime * 1.5f);
            if ((timer -= Time.unscaledDeltaTime) <= 0f) Refresh();
        }

        private static readonly Color Ok = new Color(0.3f, 0.83f, 0.54f), Warn = new Color(1f, 0.69f, 0.13f), Bad = new Color(1f, 0.3f, 0.35f), Idle = new Color(0.45f, 0.48f, 0.55f);

        public void Refresh()
        {
            timer = RefreshInterval;
            RefreshCount++;
            ActivityController a = Activity;
            GameSession s = Session;
            if (a == null || s == null) return;
            ActivitySessionState state = a.State;

            // Mode
            bool gps = a.GpsModeActive;
            bool canGps = a.CanUseGpsMode(out string why);
            modeLabel.text = gps ? "<color=#FF4D5A>GPS LIFE</color> 모드 · mode" : "<color=#FFFFFF>NORMAL</color> 모드 · mode";
            modeButtonLabel.text = gps ? "일반 모드로 · Switch to Normal" : "GPS 모드로 · Switch to GPS Life";
            modeHint.text = gps
                ? "Your real walking moves the operative along a route; the joystick is off."
                : canGps ? "Joystick / keyboard control. Start a walking session to switch to GPS Life." : why;

            // GPS status
            LocationStatus status = a.GpsStatus;
            float accuracy = LocationTracker.Instance?.LatestFix?.HorizontalAccuracy ?? 0f;
            (string text, Color color) = Describe(status, accuracy, a.IsRunning);
            statusText.text = text;
            statusLight.color = color;
            bannerText.text = a.IsPaused && state.interrupted
                ? "⏸ 일시정지됨 · Paused: the game went to the background (screen off counts). Resume when you are ready."
                : "주변을 살피세요 · Watch your surroundings, not the screen — never while crossing a road.";

            // Stats
            realDistance.text = Meters(state?.realMeters ?? 0);
            virtualDistance.text = Meters(state?.virtualMeters ?? 0);
            steps.text = a.StepStatus == StepSensorStatus.Running || a.StepStatus == StepSensorStatus.Waiting || (state?.steps ?? 0) > 0
                ? $"{state?.steps ?? 0:N0}"
                : $"<size=60%>{StepReason(a.StepStatus)}</size>";
            sessionTime.text = Clock(state?.seconds ?? 0f);
            speed.text = a.IsRunning ? $"{a.CurrentSpeed * 3.6f:F1} km/h" : "—";
            xpEarned.text = $"+{a.SessionXp:N0} XP  <size=60%><color=#9AA3AE>Lv {Experience.Level}</color></size>";
            GeoFix? fix = LocationTracker.Instance?.LatestFix;
            coordinates.text = !showCoordinates ? "위치 숨김 · Position hidden" : fix.HasValue ? $"{fix.Value.Latitude:F4}, {fix.Value.Longitude:F4}" : "—";

            // Progress
            MissionManager missions = MissionManager.Instance;
            MissionData current = missions?.ActivityMissions.FirstOrDefault(m => missions.IsActivityAvailable(m.missionId));
            if (current != null)
            {
                (double done, double target) = missions.GetActivityProgress(current.missionId);
                missionTarget = target > 0 ? Mathf.Clamp01((float)(done / target)) : 0f;
                missionText.text = $"{current.koreanName} · {current.missionName}  <color=#9AA3AE>{Progress(current, done, target)}</color>";
            }
            else
            {
                missionTarget = 1f;
                missionText.text = "모든 GPS 임무 완료 · All GPS missions complete";
            }

            missionList.text = string.Join("\n", (missions?.ActivityMissions ?? Enumerable.Empty<MissionData>()).Select(m =>
            {
                MissionState ms = missions.GetMissionState(m.missionId);
                string chip = ms == MissionState.Completed ? "<color=#4CD38A>✓</color>" : missions.IsActivityAvailable(m.missionId) ? "<color=#FF4D5A>▶</color>" : "<color=#7A808A>■</color>";
                return $"{chip} {m.koreanName} · {m.missionName}";
            }));

            checkpointText.text = NextCheckpoint(s, fix);
            int pending = s.pendingCollectibleIds.Count;
            collectiblesText.text = pending > 0 ? $"수집품 {pending}개 발견 · {pending} collectible(s) found" : $"수집품 · Collectibles {s.inventoryItemIds.Count(id => id.StartsWith("col_"))}/8";

            // Controls
            primaryLabel.text = a.IsRunning ? "일시정지 · PAUSE" : a.IsPaused ? "계속 · RESUME" : "활동 시작 · START";
            stopButton.gameObject.SetActive(a.IsRunning || a.IsPaused);
            markButton.interactable = a.IsRunning && fix.HasValue;
            collectButton.gameObject.SetActive(pending > 0);
            mapButton.interactable = InCity;
            mapLabel.text = InCity ? "지도 · VIEW MAP" : "지도 · Map (in the city)";
        }

        private static (string, Color) Describe(LocationStatus status, float accuracy, bool running)
        {
            switch (status)
            {
                case LocationStatus.Running:
                    Color c = accuracy <= GpsQuality.Good ? Ok : accuracy <= GpsQuality.Fair ? Warn : Bad;
                    return ($"GPS {GpsQuality.KoreanLabel(accuracy)} · {GpsQuality.Label(accuracy)}  <color=#9AA3AE>±{accuracy:F0} m</color>", c);
                case LocationStatus.Initializing: return ("위성 검색 중 · Searching for GPS…", Warn);
                case LocationStatus.NoSignal: return ("신호 없음 · No GPS signal — walking is not counted", Warn);
                case LocationStatus.ServiceDisabled: return ("위치 꺼짐 · Location is off on this phone", Bad);
                case LocationStatus.NeedsPermission:
                case LocationStatus.PermissionDenied: return ("위치 권한 필요 · Location access needed (GPS setup)", Bad);
                case LocationStatus.Unsupported: return ("GPS 없음 · No GPS on this device", Idle);
                case LocationStatus.Failed: return ("GPS 오류 · GPS error", Bad);
                default: return (running ? "GPS" : "대기 · Ready (GPS off until you start)", Idle);
            }
        }

        private static string StepReason(StepSensorStatus status) => status switch
        {
            StepSensorStatus.Unsupported => "걸음 센서 없음 · No step sensor",
            StepSensorStatus.NeedsPermission => "권한 필요 · Allow physical activity",
            _ => "—"
        };

        private static string Progress(MissionData m, double done, double target)
        {
            ObjectiveType type = m.objectives[0].type;
            return type switch
            {
                ObjectiveType.WalkDistance => $"{done:F0} / {target:F0} m",
                ObjectiveType.TakeSteps => $"{done:N0} / {target:N0} steps",
                ObjectiveType.ExploreAreas => $"{done:F0} / {target:F0} areas",
                _ => done >= target ? "done" : "not yet"
            };
        }

        private static string NextCheckpoint(GameSession s, GeoFix? fix)
        {
            PlayerCheckpoint open = s.playerCheckpoints.FirstOrDefault(c => !s.completedCheckpointIds.Contains(c.id));
            if (open == null) return "체크포인트 · Checkpoint: mark a safe public spot during a walk, walk on, then come back.";
            string where = fix.HasValue ? $"{Geo.DistanceMeters(fix.Value.Latitude, fix.Value.Longitude, open.latitude, open.longitude):F0} m away" : "distance unknown";
            double walkLeft = ActivityRewardsConfig.Load().playerCheckpointWalk - (s.gpsTotalRealMeters - open.metersAtPlacement);
            string rule = !open.armed ? "walk further away first" : walkLeft > 0 ? $"walk {walkLeft:F0} m more" : "head back to it";
            return $"체크포인트 · {open.name}: {where} — {rule}";
        }

        private static string Meters(double m) => m >= 1000 ? $"{m / 1000:F2} km" : $"{m:F0} m";

        private static string Clock(float seconds)
        {
            var t = TimeSpan.FromSeconds(seconds);
            return t.TotalHours >= 1 ? $"{(int)t.TotalHours}:{t.Minutes:00}:{t.Seconds:00}" : $"{t.Minutes:00}:{t.Seconds:00}";
        }
    }
}
