using Contract.Activity;
using Contract.Contracts;
using Contract.Core;
using Contract.UI.Touch;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Contract.UI
{
    /// <summary>
    /// Settings panel shared by the main menu and the pause menu, in five tabs: Audio, Controls
    /// (mouse / gamepad / camera), Touch (Android controls), Graphics (quality, FPS target) and
    /// GPS (activity mode, movement ratio, accuracy, notifications, step-counter and location
    /// permission status). Every change applies live and is stored in the session settings, which
    /// are saved with the career. Status lines never show coordinates or any location data.
    /// </summary>
    public sealed class SettingsController : MonoBehaviour
    {
        [SerializeField] private GameObject panel;

        [Header("Tabs")]
        [SerializeField] private Button[] tabButtons = new Button[0];
        [SerializeField] private GameObject[] pages = new GameObject[0];

        [Header("Audio")]
        [SerializeField] private Slider masterVolumeSlider;
        [SerializeField] private Slider musicVolumeSlider;
        [SerializeField] private Slider sfxVolumeSlider;

        [Header("Controls")]
        [SerializeField] private Slider mouseSensitivitySlider;
        [SerializeField] private Slider gamepadSensitivitySlider;
        [SerializeField] private Slider cameraDistanceSlider;
        [SerializeField] private Toggle invertYToggle;

        [Header("Touch")]
        [SerializeField] private Button touchModePrevious;
        [SerializeField] private Button touchModeNext;
        [SerializeField] private TMP_Text touchModeLabel;
        [SerializeField] private Slider touchLookXSlider;
        [SerializeField] private Slider touchLookYSlider;
        [SerializeField] private Slider joystickSizeSlider;
        [SerializeField] private Slider joystickDeadZoneSlider;
        [SerializeField] private Slider buttonOpacitySlider;
        [SerializeField] private Toggle hapticsToggle;

        [Header("Graphics")]
        [SerializeField] private Button qualityPrevious;
        [SerializeField] private Button qualityNext;
        [SerializeField] private TMP_Text qualityLabel;
        [SerializeField] private Button fpsPrevious;
        [SerializeField] private Button fpsNext;
        [SerializeField] private TMP_Text fpsLabel;
        [SerializeField] private Toggle fullscreenToggle;
        [SerializeField] private Toggle vSyncToggle;

        [Header("GPS")]
        [SerializeField] private Toggle gpsModeToggle;
        [SerializeField] private Slider gpsRatioSlider;
        [SerializeField] private TMP_Text gpsRatioValue;
        [SerializeField] private Slider gpsAccuracySlider;
        [SerializeField] private TMP_Text gpsAccuracyValue;
        [SerializeField] private Toggle notificationsToggle;
        [SerializeField] private TMP_Text stepStatusText;
        [SerializeField] private TMP_Text locationStatusText;
        [SerializeField] private Button locationButton;

        [Header("Buttons")]
        [SerializeField] private Button backButton;
        [SerializeField] private Button defaultsButton;

        private GameObject returnTo;
        private bool refreshing;
        private int tab;

        public bool IsOpen => panel != null && panel.activeSelf;
        public int Tab => tab;
        public int TabCount => pages.Length;
        public string LocationStatus => locationStatusText != null ? locationStatusText.text : "";
        public string StepStatus => stepStatusText != null ? stepStatusText.text : "";
        private static GameSettings Settings => GameManager.Instance?.Session?.Settings;

        private static readonly string[] TouchModes = { "자동 · Auto", "항상 · Always", "끄기 · Off" };

        private void Awake()
        {
            // Ranges first: changing a slider's range clamps its value and fires onValueChanged,
            // which must not write those clamped values into the player's settings.
            refreshing = true;
            Range(masterVolumeSlider, 0f, 1f);
            Range(musicVolumeSlider, 0f, 1f);
            Range(sfxVolumeSlider, 0f, 1f);
            Range(mouseSensitivitySlider, 0.2f, 3f);
            Range(gamepadSensitivitySlider, 0.2f, 3f);
            Range(cameraDistanceSlider, GameConstants.MinCameraDistance, GameConstants.MaxCameraDistance);
            Range(touchLookXSlider, 0.2f, 3f);
            Range(touchLookYSlider, 0.2f, 3f);
            Range(joystickSizeSlider, 0.7f, 1.4f);
            Range(joystickDeadZoneSlider, 0f, 0.5f);
            Range(buttonOpacitySlider, 0.3f, 1f);
            Range(gpsRatioSlider, 0.5f, 3f);
            Range(gpsAccuracySlider, 10f, 50f);
            refreshing = false;

            Listen(masterVolumeSlider, (s, v) => s.masterVolume = v);
            Listen(musicVolumeSlider, (s, v) => s.musicVolume = v);
            Listen(sfxVolumeSlider, (s, v) => s.sfxVolume = v);
            Listen(mouseSensitivitySlider, (s, v) => s.mouseSensitivity = v);
            Listen(gamepadSensitivitySlider, (s, v) => s.gamepadSensitivity = v);
            Listen(cameraDistanceSlider, (s, v) => s.cameraDistance = v);
            Listen(touchLookXSlider, (s, v) => s.touchLookSensitivityX = v);
            Listen(touchLookYSlider, (s, v) => s.touchLookSensitivityY = v);
            Listen(joystickSizeSlider, (s, v) => s.joystickSize = v);
            Listen(joystickDeadZoneSlider, (s, v) => s.joystickDeadZone = v);
            Listen(buttonOpacitySlider, (s, v) => s.buttonOpacity = v);
            Listen(gpsRatioSlider, (s, v) =>
            {
                s.gpsMovementRatio = Mathf.Round(v * 10f) / 10f;
                ShowGpsValues(s);
            });
            Listen(gpsAccuracySlider, (s, v) =>
            {
                s.gpsAccuracyThreshold = Mathf.Round(v);
                ShowGpsValues(s);
            });
            Listen(invertYToggle, (s, v) => s.invertCameraY = v);
            Listen(hapticsToggle, (s, v) => s.haptics = v);
            Listen(vSyncToggle, (s, v) => s.vSync = v);
            Listen(notificationsToggle, (s, v) => s.activityNotifications = v);
            Listen(gpsModeToggle, (s, v) =>
            {
                s.gpsModeEnabled = v;
                // Switching GPS mode off while walking returns to joystick / keyboard control (the session pauses).
                if (!v && ActivityController.Instance != null && ActivityController.Instance.GpsModeActive) ActivityController.Instance.GpsModeActive = false;
            });
            if (fullscreenToggle != null) fullscreenToggle.onValueChanged.AddListener(v => Screen.fullScreen = v);
            Click(qualityPrevious, () => StepQuality(-1));
            Click(qualityNext, () => StepQuality(1));
            Click(fpsPrevious, () => StepFrameRate(-1));
            Click(fpsNext, () => StepFrameRate(1));
            Click(touchModePrevious, () => StepTouchMode(-1));
            Click(touchModeNext, () => StepTouchMode(1));
            Click(locationButton, OpenLocationSetup);
            for (int i = 0; i < tabButtons.Length; i++)
            {
                int index = i;
                Click(tabButtons[i], () => ShowTab(index));
            }

            Click(backButton, Close);
            Click(defaultsButton, ResetDefaults);

            // Desktop-only rows are hidden on phones (fullscreen and V-Sync do nothing there).
            if (Application.isMobilePlatform)
            {
                if (fullscreenToggle != null) fullscreenToggle.transform.parent.gameObject.SetActive(false);
                if (vSyncToggle != null) vSyncToggle.transform.parent.gameObject.SetActive(false);
            }

            panel.SetActive(false);
        }

        private static void Range(Slider slider, float min, float max)
        {
            if (slider == null) return;
            slider.minValue = min;
            slider.maxValue = max;
        }

        private void Listen(Slider slider, System.Action<GameSettings, float> edit)
        {
            if (slider != null) slider.onValueChanged.AddListener(v => Change(s => edit(s, v)));
        }

        private void Listen(Toggle toggle, System.Action<GameSettings, bool> edit)
        {
            if (toggle != null) toggle.onValueChanged.AddListener(v => Change(s => edit(s, v)));
        }

        private static void Click(Button button, UnityEngine.Events.UnityAction action)
        {
            if (button != null) button.onClick.AddListener(action);
        }

        private void OnEnable() => GameEvents.PauseChanged += OnPauseChanged;

        private void OnDisable() => GameEvents.PauseChanged -= OnPauseChanged;

        private void OnPauseChanged(bool paused)
        {
            if (paused || !IsOpen) return;
            returnTo = null; // unpausing (Escape) closes settings; the pause panel hides itself
            Close();
        }

        /// <summary>Opens the panel, hiding <paramref name="from"/> until Back is pressed.</summary>
        public void Open(GameObject from)
        {
            returnTo = from;
            if (returnTo != null) returnTo.SetActive(false);
            panel.SetActive(true);
            // Phones open on the Touch tab, PCs on Audio.
            ShowTab(InputHints.Touch && pages.Length > 2 ? 2 : 0);
        }

        public void Close()
        {
            panel.SetActive(false);
            if (returnTo != null) returnTo.SetActive(true);
            returnTo = null;
        }

        public void ShowTab(int index)
        {
            if (pages.Length == 0) return;
            tab = Mathf.Clamp(index, 0, pages.Length - 1);
            for (int i = 0; i < pages.Length; i++)
            {
                if (pages[i] != null) pages[i].SetActive(i == tab);
                if (i < tabButtons.Length && tabButtons[i] != null && tabButtons[i].targetGraphic is Image image)
                    image.color = i == tab ? BoardRows.Selected : BoardRows.Normal;
            }

            Refresh();
        }

        private void Update()
        {
            if (IsOpen && pages.Length > 4 && tab == 4) RefreshStatus(); // permission / sensor state can change in the system dialog
        }

        private void Refresh()
        {
            GameSettings s = Settings;
            if (s == null) return;
            refreshing = true;
            Set(masterVolumeSlider, s.masterVolume);
            Set(musicVolumeSlider, s.musicVolume);
            Set(sfxVolumeSlider, s.sfxVolume);
            Set(mouseSensitivitySlider, s.mouseSensitivity);
            Set(gamepadSensitivitySlider, s.gamepadSensitivity);
            Set(cameraDistanceSlider, s.cameraDistance);
            Set(invertYToggle, s.invertCameraY);
            Set(touchLookXSlider, s.touchLookSensitivityX);
            Set(touchLookYSlider, s.touchLookSensitivityY);
            Set(joystickSizeSlider, s.joystickSize);
            Set(joystickDeadZoneSlider, s.joystickDeadZone);
            Set(buttonOpacitySlider, s.buttonOpacity);
            Set(hapticsToggle, s.haptics);
            if (touchModeLabel != null) touchModeLabel.text = TouchModes[Mathf.Clamp(s.touchControls, 0, TouchModes.Length - 1)];
            Set(fullscreenToggle, Screen.fullScreen);
            Set(vSyncToggle, s.vSync);
            if (qualityLabel != null) qualityLabel.text = QualitySettings.names[Mathf.Clamp(s.graphicsQuality, 0, QualitySettings.names.Length - 1)];
            if (fpsLabel != null) fpsLabel.text = s.targetFramerate > 0 ? $"{s.targetFramerate} FPS" : "무제한 · Unlimited";
            Set(gpsModeToggle, s.gpsModeEnabled);
            Set(gpsRatioSlider, s.gpsMovementRatio);
            Set(gpsAccuracySlider, s.gpsAccuracyThreshold);
            Set(notificationsToggle, s.activityNotifications);
            ShowGpsValues(s);
            RefreshStatus();
            refreshing = false;
        }

        private static void Set(Slider slider, float value)
        {
            if (slider != null) slider.value = value;
        }

        private static void Set(Toggle toggle, bool value)
        {
            if (toggle != null) toggle.isOn = value;
        }

        private void ShowGpsValues(GameSettings s)
        {
            if (gpsRatioValue != null) gpsRatioValue.text = $"{s.gpsMovementRatio:0.0}×";
            if (gpsAccuracyValue != null) gpsAccuracyValue.text = $"{s.gpsAccuracyThreshold:0} m";
        }

        // ---- GPS status (state only: never coordinates) ---------------------------------------------

        private void RefreshStatus()
        {
            if (locationStatusText != null) locationStatusText.text = DescribeLocation(DevicePermissions.Location, Input.location.isEnabledByUser);
            if (stepStatusText != null) stepStatusText.text = DescribeSteps(DevicePermissions.Activity, StepCounter.current != null, ActivityController.Instance?.StepStatus);
            if (locationButton != null) locationButton.gameObject.SetActive(DevicePermissions.IsAndroidDevice);
        }

        public static string DescribeLocation(PermissionState permission, bool serviceOn) => permission switch
        {
            PermissionState.NotRequired => "<color=#9AA3AE>이 기기에서는 해당 없음 · Not used on this device (Android only)</color>",
            PermissionState.Granted when !serviceOn => "<color=#FFB020>허용됨 · 위치 서비스 꺼짐 · Allowed, but location services are off</color>",
            PermissionState.Granted => "<color=#4CD38A>허용됨 · Allowed (while the game is open)</color>",
            PermissionState.DeniedPermanently => "<color=#FF4D5A>거부됨 · Denied: enable it in Android settings</color>",
            PermissionState.Denied => "<color=#FF4D5A>거부됨 · Denied</color>",
            _ => "<color=#FFB020>아직 요청 안 함 · Not asked yet</color>"
        };

        public static string DescribeSteps(PermissionState permission, bool sensor, StepSensorStatus? live)
        {
            if (live == StepSensorStatus.Running) return "<color=#4CD38A>작동 중 · Counting steps</color>";
            if (permission == PermissionState.NotRequired) return "<color=#9AA3AE>이 기기에서는 해당 없음 · Not used on this device (Android only)</color>";
            if (!sensor) return "<color=#9AA3AE>센서 없음 · No step sensor: GPS distance only</color>";
            return permission == PermissionState.Granted
                ? "<color=#4CD38A>사용 가능 · Ready</color>"
                : "<color=#FFB020>권한 필요 · Needs the Physical activity permission</color>";
        }

        /// <summary>Opens the GPS setup screen (explains why, then asks Android, or points to settings).</summary>
        private void OpenLocationSetup()
        {
            LocationAccessView access = FindAnyObjectByType<LocationAccessView>(FindObjectsInactive.Include);
            if (access != null) access.OpenOver();
            else if (DevicePermissions.Location == PermissionState.DeniedPermanently) DevicePermissions.OpenAppSettings();
        }

        // ---- changes ------------------------------------------------------------------------------

        private void Change(System.Action<GameSettings> edit)
        {
            GameSettings s = Settings;
            if (s == null || refreshing) return;
            edit(s);
            Apply(s);
        }

        /// <summary>Pushes settings to the running systems (audio mixer, camera, quality, frame rate).</summary>
        public static void Apply(GameSettings s)
        {
            IAudioService audio = GameManager.Instance?.GetService<IAudioService>();
            audio?.SetMasterVolume(s.masterVolume);
            audio?.SetMusicVolume(s.musicVolume);
            audio?.SetSFXVolume(s.sfxVolume);
            if (s.graphicsQuality >= 0 && s.graphicsQuality < QualitySettings.names.Length && QualitySettings.GetQualityLevel() != s.graphicsQuality)
            {
                QualitySettings.SetQualityLevel(s.graphicsQuality, true);
            }

            GameEvents.RaiseSettingsApplied(); // camera: sensitivity / distance / invert; performance: V-Sync / frame rate
        }

        private void StepQuality(int direction)
        {
            int count = QualitySettings.names.Length;
            Change(s => s.graphicsQuality = (s.graphicsQuality + direction + count) % count);
            Refresh();
        }

        private void StepFrameRate(int direction)
        {
            int[] rates = GameSettings.FrameRates;
            Change(s =>
            {
                int i = System.Array.IndexOf(rates, s.targetFramerate);
                s.targetFramerate = rates[((i < 0 ? 1 : i) + direction + rates.Length) % rates.Length];
            });
            Refresh();
        }

        private void StepTouchMode(int direction)
        {
            Change(s => s.touchControls = (s.touchControls + direction + 3) % 3);
            Refresh();
        }

        private void ResetDefaults()
        {
            GameSession session = GameManager.Instance?.Session;
            if (session == null) return;
            session.Settings = new GameSettings { graphicsQuality = QualitySettings.GetQualityLevel() };
            Apply(session.Settings);
            Refresh();
        }
    }
}
