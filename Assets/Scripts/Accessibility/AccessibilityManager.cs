using System;
using Contract.Contracts;
using Contract.Core;
using Contract.Player;
using Contract.VFX;
using UnityEngine;
using UnityEngine.UI;

namespace Contract.Accessibility
{
    /// <summary>
    /// Accessibility settings: colorblind modes, subtitles, input remapping, difficulty.
    /// </summary>
    [Serializable]
    public sealed class AccessibilitySettings
    {
        [Header("Visual")]
        public bool colorblindMode = false;
        public ColorblindType colorblindType = ColorblindType.Deuteranopia;
        public bool highContrast = false;
        public float uiScale = 1f;
        public bool reduceMotion = false;
        public bool screenShake = true;

        [Header("Audio")]
        public bool subtitles = true;
        public float subtitleSize = 1f;
        public bool subtitleBackground = true;
        public bool audioCues = true;

        [Header("Gameplay")]
        public bool autoInteract = false;
        public float interactionHoldTime = 0.5f;
        public bool toggleCrouch = true;
        public bool toggleAim = false;
        public float gameSpeed = 1f;
        public bool invincibility = false;
        public bool infiniteStamina = false;

        [Header("Input")]
        public bool swapMoveLook = false;
        public bool invertYAxis = false;
        public float mouseSensitivity = 1f;
        public float gamepadSensitivity = 1f;
        public bool vibration = true;

        public enum ColorblindType
        {
            None,
            Protanopia,
            Deuteranopia,
            Tritanopia,
            Achromatopsia
        }
    }

    /// <summary>
    /// Manages accessibility settings and applies them at runtime.
    /// </summary>
    public sealed class AccessibilityManager : MonoBehaviour
    {
        public static AccessibilityManager Instance { get; private set; }

        [Header("References")]
        [SerializeField] private PostProcessingController postProcessing;

        public AccessibilitySettings Settings { get; private set; } = new AccessibilitySettings();

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                DestroyImmediate(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);

            LoadSettings();
            ApplySettings();
        }

        private void Start()
        {
            GameEvents.SettingsApplied += OnSettingsApplied;
        }

        private void OnDestroy()
        {
            GameEvents.SettingsApplied -= OnSettingsApplied;
        }

        private void OnSettingsApplied()
        {
            LoadSettings();
            ApplySettings();
        }

        private void LoadSettings()
        {
            GameSession session = GameManager.Instance?.Session;
            if (session?.Settings != null)
            {
                // Settings are stored in GameSession.Settings
                // For now, use defaults - in a full implementation, you'd serialize AccessibilitySettings separately
            }
        }

        private void ApplySettings()
        {
            ApplyColorblindFilter();
            ApplyUIScale();
            ApplyMotionReduction();
            ApplyAudioSettings();
            ApplyGameplayAssists();
        }

        private void ApplyColorblindFilter()
        {
            if (postProcessing == null) return;

            // Would apply colorblind correction via post-processing
            // This requires a custom shader or URP color grading override
        }

        private void ApplyUIScale()
        {
            Canvas[] canvases = FindObjectsByType<Canvas>(FindObjectsInactive.Include);
            foreach (Canvas canvas in canvases)
            {
                CanvasScaler scaler = canvas.GetComponent<CanvasScaler>();
                if (scaler != null && scaler.uiScaleMode == CanvasScaler.ScaleMode.ScaleWithScreenSize)
                {
                    scaler.scaleFactor = Settings.uiScale;
                }
            }
        }

        private void ApplyMotionReduction()
        {
            if (Settings.reduceMotion)
            {
                // Disable screen shake, reduce particle effects
                VFXManager.Instance?.TriggerVignette(0f, 0f);
            }
        }

        private void ApplyAudioSettings()
        {
            IAudioService audio = GameManager.Instance?.GetService<IAudioService>();
            if (audio != null)
            {
                // Subtitles handled by UI system
                // Audio cues handled by AudioService
            }
        }

        private void ApplyGameplayAssists()
        {
            Time.timeScale = Settings.gameSpeed;

            ThirdPersonController controller = FindObjectOfType<ThirdPersonController>();
            if (controller != null)
            {
                // Would apply assists like invincibility, infinite stamina
            }
        }

        public void SetColorblindMode(AccessibilitySettings.ColorblindType type)
        {
            Settings.colorblindType = type;
            Settings.colorblindMode = type != AccessibilitySettings.ColorblindType.None;
            ApplyColorblindFilter();
        }

        public void SetHighContrast(bool enabled)
        {
            Settings.highContrast = enabled;
            // Apply high contrast UI theme
        }

        public void SetSubtitles(bool enabled)
        {
            Settings.subtitles = enabled;
        }

        public void SetGameSpeed(float speed)
        {
            Settings.gameSpeed = Mathf.Clamp(speed, 0.5f, 2f);
            Time.timeScale = Settings.gameSpeed;
        }
    }
}