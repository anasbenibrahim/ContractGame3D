using System;

namespace Contract.Core
{
    [Serializable]
    public sealed class GameSettings
    {
        /// <summary>Choices for the FPS target (0 = unlimited).</summary>
        public static readonly int[] FrameRates = { 30, 60, 90, 120, 0 };

        public float masterVolume = 1f;
        public float musicVolume = 0.8f;
        public float sfxVolume = 1f;
        public float dialogueVolume = 0.9f;
        public float mouseSensitivity = 1f;
        public float gamepadSensitivity = 1f;
        public float cameraDistance = GameConstants.DefaultCameraDistance;
        public bool invertCameraY;
        public int graphicsQuality = -1;

        // GPS Activity Mode
        public bool gpsModeEnabled = true;
        public float gpsMovementRatio = 1f;       // virtual metres per real metre (0.5 - 3)
        public float gpsAccuracyThreshold = 25f;  // ignore fixes less accurate than this (m)
        public bool activityNotifications = true;

        // Touchscreen controls (Android)
        public int touchControls;                  // TouchControlsMode: 0 auto (touch devices), 1 always, 2 never
        public float joystickDeadZone = 0.12f;     // share of the joystick radius ignored (0 - 0.5)
        public float joystickSensitivity = 1f;     // joystick deflection multiplier (0.5 - 2)
        public float touchLookSensitivityX = 1f;   // horizontal drag-to-look multiplier (0.2 - 3)
        public float touchLookSensitivityY = 0.8f; // vertical drag-to-look multiplier (0.2 - 3)
        public bool haptics = true;                // short vibration on touch-button presses
        public float joystickSize = 1f;            // joystick scale (0.7 - 1.4)
        public float buttonOpacity = 0.85f;        // touch controls opacity (0.3 - 1)
        public bool vSync = true;
        public int targetFramerate = 60;          // 30 / 60 / 90 / 120; 0 = unlimited (display rate on phones). Phones ignore V-Sync.
        public string language = "en";
        public bool showMinimap = true;
        public bool showObjective = true;
        public bool showHUD = true;
        public bool subtitles = true;
    }
}