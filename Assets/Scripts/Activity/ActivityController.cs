using System;
using System.Collections.Generic;
using Contract.Contracts;
using Contract.Core;
using UnityEngine;

namespace Contract.Activity
{
    /// <summary>
    /// Runs GPS walking sessions: start / pause / resume / stop, feeds location fixes through the
    /// <see cref="WalkingDistanceFilter"/>, converts counted real metres to virtual metres with the
    /// player's ratio and adds both to the career totals as they happen (so nothing is lost if the
    /// game is killed). Only totals are stored, never positions.
    /// Foreground only: going to the background (screen off included) pauses the session and says
    /// why; the screen is kept awake while a session runs.
    /// </summary>
    public sealed class ActivityController : MonoBehaviour
    {
        public static ActivityController Instance { get; private set; }

        /// <summary>Counted real metres and the matching virtual metres.</summary>
        public event Action<double, double> DistanceAdvanced;
        public event Action<int> SessionStateChanged;
        public event Action<bool> ModeChanged;

        private WalkingDistanceFilter filter;
        private double lastFixTime = double.NaN;
        private readonly Queue<(float t, double m)> recent = new Queue<(float, double)>();
        private int previousSleepTimeout;
        private bool tracking;
        private StepTracker steps;
        private IStepSource stepSource;

        public event Action<long> StepsAdvanced;

        /// <summary>Career activity totals changed (distance, steps or areas): activity missions re-check.</summary>
        public static event Action Progressed;

        public static void RaiseProgressed() => Progressed?.Invoke();

        /// <summary>Time source for step evidence and speed (tests use the simulated GPS clock).</summary>
        public Func<float> Clock { get; set; } = () => Time.realtimeSinceStartup;

        /// <summary>Step-counter state for the dashboard ("unsupported", "needs permission", ...).</summary>
        public StepSensorStatus StepStatus => steps?.Status ?? (stepSource == null ? StepSensorStatus.Unsupported : StepSensorStatus.Off);

        private static GameSession Session => GameManager.Instance?.Session;
        private static ActivityConfig Config => ActivityConfig.Load();
        public ActivitySessionState State => Session?.gpsSession;
        public bool IsRunning => State != null && State.state == ActivitySessionState.Running;
        public bool IsPaused => State != null && State.state == ActivitySessionState.Paused;
        public LocationStatus GpsStatus => LocationTracker.Instance != null ? LocationTracker.Instance.Status : LocationStatus.Off;
        public FixVerdict LastVerdict => filter?.LastVerdict ?? FixVerdict.First;
        public bool IsWalking => filter != null && filter.IsWalking;

        /// <summary>True while the controller holds location and keeps the screen awake (phones only honour the latter).</summary>
        public bool KeepsScreenAwake => tracking;

        /// <summary>
        /// Motion-sensor evidence for the distance filter (set by the step-counter module, Phase 4).
        /// Unknown = GPS only: stricter, and slow drift can still add a little distance.
        /// </summary>
        public MotionEvidence Motion { get; set; } = MotionEvidence.Unknown;

        /// <summary>Replaces the step counter (simulated in the editor and tests; null = none).</summary>
        public void UseStepSource(IStepSource source)
        {
            steps?.End();
            stepSource = source;
            steps = source == null ? null : new StepTracker(source);
            if (tracking && steps != null) steps.Begin(Clock());
        }

        /// <summary>Walking speed over the last <see cref="ActivityConfig.speedWindow"/> seconds (m/s).</summary>
        public float CurrentSpeed
        {
            get
            {
                if (!IsRunning || recent.Count == 0) return 0f;
                double sum = 0;
                foreach ((float _, double m) in recent) sum += m;
                return (float)(sum / Config.speedWindow);
            }
        }

        /// <summary>True when GPS mode is selected: the virtual character follows the walked distance.</summary>
        public bool GpsModeActive
        {
            get => Session != null && Session.gpsModeActive;
            set
            {
                if (Session == null || Session.gpsModeActive == value) return;
                if (value && !CanUseGpsMode(out _)) return;
                if (!value && IsRunning) Pause();      // keep the session: it can be resumed later
                Session.gpsModeActive = value;
                ModeChanged?.Invoke(value);
                Save();
            }
        }

        /// <summary>Whether GPS Life Mode can be switched on here, and if not, why (shown to the player).</summary>
        public bool CanUseGpsMode(out string reason)
        {
            reason = null;
            if (Session != null && !Session.Settings.gpsModeEnabled) reason = "GPS 모드 꺼짐 · GPS mode is switched off in Settings.";
            else if (LocationTracker.Instance == null || LocationTracker.Instance.SourceName == "none") reason = "GPS 없음 · This device has no GPS. GPS Life Mode needs an Android phone.";
            else if (DevicePermissions.IsAndroidDevice && DevicePermissions.Location != PermissionState.Granted) reason = "위치 권한 필요 · Allow location access first (GPS setup).";
            return reason == null;
        }

        /// <summary>XP earned since the current session started.</summary>
        public int SessionXp => State == null || Session == null ? 0 : Math.Max(0, Session.xp - State.xpAtStart);

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void CreateInstance()
        {
            if (Instance == null) new GameObject("[ActivityController]").AddComponent<ActivityController>();
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
            if (DevicePermissions.IsAndroidDevice) UseStepSource(new DeviceStepSource());
        }

        private void OnEnable()
        {
            if (LocationTracker.Instance != null) LocationTracker.Instance.FixReceived += OnFix;
        }

        private void Start()
        {
            if (LocationTracker.Instance != null)
            {
                LocationTracker.Instance.FixReceived -= OnFix;
                LocationTracker.Instance.FixReceived += OnFix;
            }
        }

        private void OnDisable()
        {
            if (LocationTracker.Instance != null) LocationTracker.Instance.FixReceived -= OnFix;
        }

        // ---- session control ---------------------------------------------------------------------

        public void StartSession()
        {
            if (Session == null || IsRunning) return;
            if (!CanUseGpsMode(out _)) return;
            if (!Session.gpsModeActive)
            {
                Session.gpsModeActive = true; // a walking session is GPS Life Mode
                ModeChanged?.Invoke(true);
            }

            if (IsPaused)
            {
                Resume();
                return;
            }

            Session.gpsSession = new ActivitySessionState
            {
                state = ActivitySessionState.Running,
                startedUnixMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                xpAtStart = Session.xp
            };
            BeginTracking();
            Changed();

            // Steps make distance far more reliable: ask once (the explanation is on the GPS screen).
            if (DevicePermissions.Activity == PermissionState.NotAsked)
                DevicePermissions.Request(DevicePermissions.ActivityRecognition, _ => { if (tracking) steps?.Begin(Clock()); });
        }

        public void Pause(bool interrupted = false)
        {
            if (!IsRunning) return;
            if (filter != null) Count(filter.Flush()); // the last metres before pausing still count
            State.state = ActivitySessionState.Paused;
            State.interrupted = interrupted;
            EndTracking();
            Changed();
            Save();
        }

        public void Resume()
        {
            if (!IsPaused || !CanUseGpsMode(out _)) return;
            if (!Session.gpsModeActive)
            {
                Session.gpsModeActive = true;
                ModeChanged?.Invoke(true);
            }

            State.state = ActivitySessionState.Running;
            State.interrupted = false;
            BeginTracking(); // a fresh track: distance moved while paused is never counted
            Changed();
        }

        public void Stop()
        {
            if (IsRunning && filter != null) Count(filter.Flush());
            if (State == null || State.state == ActivitySessionState.Idle)
            {
                EndTracking(); // e.g. the career was reset mid-session
                return;
            }

            State.state = ActivitySessionState.Idle;
            EndTracking();
            Changed();
            Save();
        }

        private void BeginTracking()
        {
            WalkingFilterSettings settings = Config.filter;
            GameSettings player = Session.Settings;
            settings.maxAccuracy = Mathf.Clamp(player.gpsAccuracyThreshold, 10f, 50f);
            filter = new WalkingDistanceFilter(settings);
            ActivityWorld.ResetDebounce();
            lastFixTime = double.NaN;
            recent.Clear();
            LocationTracker.Instance.Acquire(this);
            steps?.Begin(Clock()); // counts from now: never lifetime steps, never steps taken while paused
            Motion = MotionEvidence.Unknown;
            if (!tracking) previousSleepTimeout = Screen.sleepTimeout == SleepTimeout.NeverSleep ? SleepTimeout.SystemSetting : Screen.sleepTimeout;
            Screen.sleepTimeout = SleepTimeout.NeverSleep;
            tracking = true;
        }

        private void EndTracking()
        {
            if (!tracking) return;
            tracking = false;
            LocationTracker.Instance?.Release(this);
            steps?.End();
            Motion = MotionEvidence.Unknown;
            Screen.sleepTimeout = previousSleepTimeout;
            recent.Clear();
        }

        private void Changed() => SessionStateChanged?.Invoke(State.state);

        private static void Save() => GameManager.Instance?.GetService<ISaveService>()?.AutoSave();

        // ---- distance ------------------------------------------------------------------------------

        private void OnFix(GeoFix fix)
        {
            if (!IsRunning || filter == null) return;
            if (!double.IsNaN(lastFixTime) && fix.Timestamp - lastFixTime > Config.maxContinuityGap)
            {
                Count(filter.Flush());
                filter.Reset(); // too long without signal: do not count straight across the gap
            }

            lastFixTime = fix.Timestamp;
            Count(filter.Add(fix, Motion));
            FixVerdict verdict = filter.LastVerdict;
            if (verdict != FixVerdict.PoorAccuracy && verdict != FixVerdict.Duplicate && verdict != FixVerdict.Implausible && verdict != FixVerdict.Vehicle)
            {
                int areas = Session.exploredAreas.Count;
                ActivityWorld.OnValidatedFix(Session, fix); // checkpoints, exploration (never from a vehicle or a bad fix)
                if (Session.exploredAreas.Count != areas) RaiseProgressed();
            }
        }

        private void Count(double realMeters)
        {
            if (realMeters <= 0 || Session == null) return;
            double ratio = Mathf.Clamp(Session.Settings.gpsMovementRatio, 0.5f, 3f);
            double virtualMeters = realMeters * ratio;
            ActivitySessionState s = State;
            s.realMeters += realMeters;
            s.virtualMeters += virtualMeters;
            Session.gpsTotalRealMeters += realMeters;
            Session.gpsTotalVirtualMeters += virtualMeters;
            Session.gpsRouteMeters += virtualMeters;
            recent.Enqueue((Clock(), realMeters));
            DistanceAdvanced?.Invoke(realMeters, virtualMeters);
            ActivityRewards.Evaluate(Session);
            RaiseProgressed();
        }

        private void Update()
        {
            if (tracking && !IsRunning && !IsPaused) EndTracking(); // session replaced (new game / load)
            if (!IsRunning) return;
            float now = Clock();
            if (steps != null)
            {
                long added = steps.Tick(now);
                if (added > 0)
                {
                    State.steps += added;
                    Session.gpsTotalSteps += added;
                    StepsAdvanced?.Invoke(added);
                    ActivityRewards.Evaluate(Session);
                    RaiseProgressed();
                }

                Motion = steps.Evidence(now);
            }

            State.seconds += Time.unscaledDeltaTime;
            Session.gpsTotalSeconds += Time.unscaledDeltaTime;
            while (recent.Count > 0 && now - recent.Peek().t > Config.speedWindow) recent.Dequeue();
        }

        // The tracker stops location in the background; the session pauses and tells the player why.
        private void OnApplicationPause(bool paused)
        {
            if (paused && IsRunning) Pause(interrupted: true);
        }
    }
}
