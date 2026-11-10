using Contract.Activity;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Contract.UI
{
    /// <summary>
    /// GPS Activity access screen (main menu): explains why location and physical-activity access
    /// are needed before Android asks, handles every permission / device state with a clear next
    /// step (allow, ask again, open app settings, turn location on), and runs a live GPS check
    /// (status, accuracy rating, fix count; no coordinates shown). Location runs only while the
    /// check is on and stops when the screen closes.
    /// </summary>
    public sealed class LocationAccessView : MonoBehaviour
    {
        [SerializeField] private GameObject panel;
        [SerializeField] private Button openButton;
        [SerializeField] private GameObject returnTo;

        [Header("Location")]
        [SerializeField] private TMP_Text statusTitle;
        [SerializeField] private TMP_Text statusBody;
        [SerializeField] private Image statusLight;
        [SerializeField] private Button primaryButton;
        [SerializeField] private TMP_Text primaryLabel;
        [SerializeField] private Button stopButton;

        [Header("Step counter")]
        [SerializeField] private TMP_Text activityText;
        [SerializeField] private Button activityButton;
        [SerializeField] private TMP_Text activityLabel;

        [SerializeField] private Button closeButton;

        private enum Action { None, AllowLocation, OpenAppSettings, TurnOnLocation, StartCheck }

        private Action primary;
        private bool checking;
        private float refreshTimer;

        public bool IsOpen => panel.activeSelf;
        public bool Checking => checking;
        public Button PrimaryButton => primaryButton;
        public string StatusTitle => statusTitle.text;

        private static LocationTracker Tracker => LocationTracker.Instance;

        private void Awake()
        {
            openButton.onClick.AddListener(Open);
            closeButton.onClick.AddListener(Close);
            primaryButton.onClick.AddListener(OnPrimary);
            stopButton.onClick.AddListener(StopCheck);
            activityButton.onClick.AddListener(OnActivityPermission);
            panel.SetActive(false);
        }

        private void OnEnable()
        {
            if (Tracker != null) Tracker.StatusChanged += OnStatus;
        }

        private void OnDisable()
        {
            if (Tracker != null) Tracker.StatusChanged -= OnStatus;
            StopCheck();
        }

        private void OnStatus(LocationStatus status) => Refresh();

        private bool overlay;

        public void Open()
        {
            overlay = false;
            if (returnTo != null) returnTo.SetActive(false);
            panel.SetActive(true);
            Refresh();
        }

        /// <summary>Opens on top of another screen (Settings): Back closes it and returns there, not to the dashboard.</summary>
        public void OpenOver()
        {
            overlay = true;
            panel.SetActive(true);
            Refresh();
        }

        public void Close()
        {
            StopCheck();
            panel.SetActive(false);
            if (!overlay && returnTo != null) returnTo.SetActive(true);
            overlay = false;
        }

        public void StartCheck()
        {
            checking = true;
            Tracker.Acquire(this);
            Refresh();
        }

        public void StopCheck()
        {
            if (!checking) return;
            checking = false;
            Tracker?.Release(this);
            if (panel != null && panel.activeSelf) Refresh();
        }

        private void OnPrimary()
        {
            switch (primary)
            {
                case Action.AllowLocation:
                    DevicePermissions.Request(DevicePermissions.FineLocation, state =>
                    {
                        if (state == PermissionState.Granted) StartCheck();
                        else Refresh();
                    });
                    break;
                case Action.OpenAppSettings:
                    DevicePermissions.OpenAppSettings();
                    break;
                case Action.TurnOnLocation:
                    DevicePermissions.OpenLocationSettings();
                    break;
                case Action.StartCheck:
                    StartCheck();
                    break;
            }
        }

        private void OnActivityPermission()
        {
            PermissionState state = DevicePermissions.Activity;
            if (state == PermissionState.DeniedPermanently) DevicePermissions.OpenAppSettings();
            else DevicePermissions.Request(DevicePermissions.ActivityRecognition, _ => Refresh());
        }

        private void Update()
        {
            if (!panel.activeSelf) return;
            bool back = (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
                        || (Gamepad.current != null && Gamepad.current.buttonEast.wasPressedThisFrame);
            if (back)
            {
                Close();
                return;
            }

            if ((refreshTimer -= Time.unscaledDeltaTime) <= 0f) Refresh(); // twice a second, not every frame
        }

        // ---- presentation ---------------------------------------------------------------------

        private static readonly Color Ok = new Color(0.3f, 0.83f, 0.54f), Warn = new Color(1f, 0.69f, 0.13f), Bad = new Color(1f, 0.3f, 0.35f), Idle = new Color(0.45f, 0.48f, 0.55f);

        private void Refresh()
        {
            refreshTimer = 0.5f;
            if (Tracker == null) return;
            stopButton.gameObject.SetActive(checking);

            bool noLocationSource = Tracker.SourceName == "none"; // PC, unless a simulated route is installed
            if (noLocationSource)
            {
                Show("GPS 없음 · No GPS on this device",
                    "GPS Activity Mode turns real walking into progress in the game. It needs an Android phone with location (GPS). " +
                    "On PC you can keep playing normally.", Idle, Action.None, null);
            }
            else if (!checking)
            {
                ShowPermission();
            }
            else
            {
                ShowLive();
            }

            ShowActivity();
        }

        private void ShowPermission()
        {
            const string why = "We use your location only while a walking session is running, on this phone, to measure how far you walk. " +
                               "Nothing is uploaded and your route is not stored — only totals such as distance walked.";
            switch (DevicePermissions.Location)
            {
                case PermissionState.NotAsked:
                    Show("위치 권한 · Location access", why + "\n\nAndroid will ask next: choose <b>While using the app</b> and keep <b>Precise</b> on.", Idle, Action.AllowLocation, "위치 허용 · Allow location");
                    break;
                case PermissionState.Denied:
                    Show("위치 권한 거부됨 · Location declined", why + "\n\nWithout it, GPS Activity Mode is unavailable. You can ask again.", Warn, Action.AllowLocation, "다시 요청 · Ask again");
                    break;
                case PermissionState.DeniedPermanently:
                    Show("설정에서 허용 필요 · Allow in settings",
                        "Android will not ask again. To enable: <b>Settings → Apps → Contract Seoul Shadows → Permissions → Location → Allow only while using the app</b>, with <b>Use precise location</b> on.",
                        Bad, Action.OpenAppSettings, "설정 열기 · Open settings");
                    break;
                default:
                    Show("위치 권한 허용됨 · Location allowed", "Run a GPS check to see the signal quality where you are. Best outdoors with a clear view of the sky.",
                        Ok, Action.StartCheck, "GPS 확인 · Check GPS");
                    break;
            }
        }

        private void ShowLive()
        {
            LocationTracker t = Tracker;
            switch (t.Status)
            {
                case LocationStatus.ServiceDisabled:
                    Show("위치 꺼짐 · Location is off", "Your phone's location is switched off. Turn it on (quick settings or Settings → Location), then come back.",
                        Bad, Action.TurnOnLocation, "위치 켜기 · Turn on location");
                    break;
                case LocationStatus.NeedsPermission:
                case LocationStatus.PermissionDenied:
                    checking = false;
                    t.Release(this);
                    ShowPermission();
                    break;
                case LocationStatus.Initializing:
                    Show("위성 검색 중 · Searching…", "Waiting for the first GPS fix. Outdoors this usually takes 5–30 seconds.", Warn, Action.None, null);
                    break;
                case LocationStatus.NoSignal:
                    Show("신호 없음 · No GPS signal", "No fresh position for a while. Move away from tall buildings or go outside. Walking will not count until the signal returns.",
                        Warn, Action.None, null);
                    break;
                case LocationStatus.Failed:
                    Show("GPS 오류 · GPS error", "Android reported a location failure. Check that location is on, then try again.", Bad, Action.StartCheck, "다시 시도 · Retry");
                    break;
                case LocationStatus.Running:
                    float accuracy = t.LatestFix?.HorizontalAccuracy ?? 0f;
                    Color color = accuracy <= GpsQuality.Good ? Ok : accuracy <= GpsQuality.Fair ? Warn : Bad;
                    Show($"GPS 정상 · GPS OK — {GpsQuality.KoreanLabel(accuracy)} · {GpsQuality.Label(accuracy)}",
                        $"Accuracy ±{accuracy:F0} m · {t.FixCount} updates received.\n\nAlways check your surroundings — never look at the screen while crossing a road.",
                        color, Action.None, null);
                    break;
                default:
                    Show("GPS", t.Status.ToString(), Idle, Action.None, null);
                    break;
            }
        }

        private void ShowActivity()
        {
            if (!DevicePermissions.IsAndroidDevice)
            {
                activityText.text = "걸음 수 · Step counter: Android phones only";
                activityButton.gameObject.SetActive(false);
                return;
            }

            if (!DeviceStepSource.HardwarePresent)
            {
                activityText.text = "걸음 수 · Step counter: <color=#FFB020>not available on this phone</color> — distance uses GPS only (less accurate, short walks may be missed)";
                activityButton.gameObject.SetActive(false);
                return;
            }

            PermissionState state = DevicePermissions.Activity;
            activityText.text = state == PermissionState.Granted
                ? "걸음 수 · Step counter: <color=#4CD38A>allowed</color> (physical activity)"
                : "걸음 수 · Step counter: needs the <b>Physical activity</b> permission";
            activityButton.gameObject.SetActive(state != PermissionState.Granted);
            activityLabel.text = state == PermissionState.DeniedPermanently ? "설정 열기 · Open settings" : "활동 허용 · Allow activity";
        }

        private void Show(string title, string body, Color light, Action action, string label)
        {
            statusTitle.text = title;
            statusBody.text = body;
            statusLight.color = light;
            primary = action;
            primaryButton.gameObject.SetActive(action != Action.None);
            if (label != null) primaryLabel.text = label;
        }
    }
}
