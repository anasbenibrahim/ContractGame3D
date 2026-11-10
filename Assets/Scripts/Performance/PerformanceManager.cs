using Contract.Core;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

namespace Contract.Performance
{
    /// <summary>
    /// Applies the player's graphics settings (quality tier Low / Medium / High and V-Sync) at
    /// start-up, after loading a career and whenever settings change. On the very first launch
    /// (quality "auto", -1) it recommends a tier from the GPU once; after that the player's choice
    /// is final. It never lowers quality behind the player's back. On phones each tier uses the
    /// Android preset (MobileQualityProfile: its own URP asset, LOD bias, crowd size and
    /// post-processing), and menus are capped at 30 fps to save battery.
    /// </summary>
    public sealed class PerformanceManager : MonoBehaviour
    {
        public const int Low = 0, Medium = 1, High = 2;

        public static PerformanceManager Instance { get; private set; }

        /// <summary>Phones (or a test / measurement forcing the phone presets in the editor).</summary>
        public static bool UseMobileProfile => ForceMobileProfile ?? Application.isMobilePlatform;

        /// <summary>Editor tests and measurements only. Restore the quality settings afterwards (see <see cref="RestorePcPipelines"/>).</summary>
        public static bool? ForceMobileProfile { get; set; }

        public const int MenuFrameRate = 30;

        private MobileQualityProfile mobileProfile;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void CreateInstance()
        {
            if (Instance == null) new GameObject("[PerformanceManager]").AddComponent<PerformanceManager>();
        }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject); // the copy placed in GameWorld by older builds
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);
            ApplySettings();
        }

        private void OnEnable()
        {
            GameEvents.SettingsApplied += ApplySettings;
            GameEvents.LoadCompleted += OnLoaded;
            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        private void OnDisable()
        {
            GameEvents.SettingsApplied -= ApplySettings;
            GameEvents.LoadCompleted -= OnLoaded;
            SceneManager.sceneLoaded -= OnSceneLoaded;
        }

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode) => ApplySettings(); // menu / gameplay frame rate, new camera

        private void OnLoaded(string slot) => ApplySettings();

        /// <summary>Pushes the session's graphics settings to Unity (choosing a tier first if still "auto").</summary>
        public void ApplySettings()
        {
            GameSettings settings = GameManager.Instance?.Session?.Settings;
            if (settings == null) return;
            int levels = QualitySettings.names.Length;
            if (settings.graphicsQuality < 0 || settings.graphicsQuality >= levels)
            {
                int recommended = UseMobileProfile ? RecommendMobile(SystemInfo.graphicsDeviceName, SystemInfo.systemMemorySize)
                    : Recommend(SystemInfo.graphicsDeviceName, SystemInfo.graphicsMemorySize);
                settings.graphicsQuality = Mathf.Min(levels - 1, recommended);
                Debug.Log($"[Performance] First launch on {SystemInfo.graphicsDeviceName} ({SystemInfo.graphicsMemorySize} MB): quality {QualitySettings.names[settings.graphicsQuality]}");
            }

            if (QualitySettings.GetQualityLevel() != settings.graphicsQuality) QualitySettings.SetQualityLevel(settings.graphicsQuality, true);
            bool mobile = UseMobileProfile;
            ApplyMobilePreset(mobile ? settings.graphicsQuality : -1);
            QualitySettings.vSyncCount = settings.vSync && !mobile ? 1 : 0;
            Application.targetFrameRate = FrameRate(settings, mobile, DisplayRefreshRate(), GameConstants.Scenes.InGameplay);
        }

        /// <summary>The Android preset for a quality level (-1: PC tiers, no crowd limit).</summary>
        private void ApplyMobilePreset(int level)
        {
            MobileQualityProfile.Tier tier = null;
            if (level >= 0)
            {
                if (mobileProfile == null) mobileProfile = MobileQualityProfile.Load();
                tier = mobileProfile != null ? mobileProfile.ForLevel(level) : null;
            }

            PerformanceBudget.MaxCivilians = tier?.maxCivilians ?? 0;
            if (tier == null) return;
            if (tier.pipeline != null && QualitySettings.renderPipeline != tier.pipeline) QualitySettings.renderPipeline = tier.pipeline;
            QualitySettings.lodBias = tier.lodBias;
            UnityEngine.Camera main = UnityEngine.Camera.main;
            if (main != null && main.TryGetComponent(out UniversalAdditionalCameraData data)) data.renderPostProcessing = tier.postProcessing;
        }

        /// <summary>Puts every quality level back on its PC pipeline (editor tests that forced the phone presets).</summary>
        public static void RestorePcPipelines(UnityEngine.Rendering.RenderPipelineAsset[] pipelines, float[] lodBiases)
        {
            int current = QualitySettings.GetQualityLevel();
            for (int i = 0; i < pipelines.Length; i++)
            {
                QualitySettings.SetQualityLevel(i, false);
                QualitySettings.renderPipeline = pipelines[i];
                QualitySettings.lodBias = lodBiases[i];
            }

            QualitySettings.SetQualityLevel(current, true);
        }

        private static int DisplayRefreshRate()
        {
            double hz = Screen.currentResolution.refreshRateRatio.value;
            return hz > 1 ? Mathf.RoundToInt((float)hz) : 60;
        }

        /// <summary>
        /// Frame-rate cap. Phones ignore V-Sync and default to 30 fps, so their cap is always the FPS
        /// target (unlimited = the display's refresh rate). PCs with V-Sync follow the display (-1);
        /// without V-Sync they use the FPS target (unlimited still caps runaway menus at 240).
        /// </summary>
        public static int FrameRate(GameSettings settings, bool mobile, int displayHz, bool inGameplay = true)
        {
            int target = settings.targetFramerate;
            if (mobile)
            {
                int rate = target > 0 ? Mathf.Min(target, Mathf.Max(30, displayHz)) : Mathf.Max(30, displayHz);
                return inGameplay ? rate : Mathf.Min(rate, MenuFrameRate); // menus: battery
            }

            if (settings.vSync) return -1;
            return target > 0 ? target : 240;
        }

        /// <summary>
        /// Starting tier for a phone, from its GPU family and RAM (a first-launch suggestion the
        /// player can change; not measured on these devices yet): recent flagship GPUs (Adreno
        /// 730+, Mali-G710+/Immortalis, Xclipse) with 8 GB+ High; Adreno 6xx / Mali-G7x / G6x and
        /// 4 GB+ Medium; everything else (older Mali, PowerVR, under 4 GB) Low.
        /// </summary>
        public static int RecommendMobile(string deviceName, int systemMemoryMB)
        {
            string gpu = (deviceName ?? "").ToLowerInvariant();
            if (systemMemoryMB > 0 && systemMemoryMB < 3500) return Low;
            int adreno = Number(gpu, "adreno");
            int mali = Number(gpu, "mali-g");
            bool flagship = adreno >= 730 || mali >= 710 || gpu.Contains("immortalis") || gpu.Contains("xclipse");
            if (flagship) return systemMemoryMB >= 7500 ? High : Medium;
            bool midRange = (adreno >= 610 && adreno < 730) || (mali >= 57 && mali < 710 && mali != 52);
            return midRange && systemMemoryMB >= 3500 ? Medium : Low;
        }

        /// <summary>The model number after a GPU family name ("adreno (tm) 740" -> 740, "mali-g78" -> 78), or 0.</summary>
        private static int Number(string gpu, string family)
        {
            int i = gpu.IndexOf(family, System.StringComparison.Ordinal);
            if (i < 0) return 0;
            i += family.Length;
            while (i < gpu.Length && !char.IsDigit(gpu[i]) && i < gpu.IndexOf(family, System.StringComparison.Ordinal) + family.Length + 8) i++;
            int start = i;
            while (i < gpu.Length && char.IsDigit(gpu[i])) i++;
            return i > start && int.TryParse(gpu.Substring(start, i - start), out int n) ? n : 0;
        }

        /// <summary>
        /// Starting tier for a GPU: software renderers and Intel UHD-class integrated graphics Low;
        /// Iris Xe / Arc / recent Radeon integrated graphics Medium; dedicated GPUs High with 4 GB+
        /// of video memory, otherwise Medium.
        /// </summary>
        public static int Recommend(string deviceName, int memoryMB)
        {
            string gpu = (deviceName ?? "").ToLowerInvariant();
            if (gpu.Contains("llvmpipe") || gpu.Contains("swiftshader") || gpu.Contains("software")) return Low;
            // Intel's discrete Arc cards (A380 ... A770, B580) are dedicated GPUs.
            if (gpu.Contains("arc(tm) a") || gpu.Contains("arc(tm) b")) return memoryMB >= 4000 ? High : Medium;
            // Stronger integrated chips first (Iris Xe / Arc / recent Radeon iGPUs ~2x an Intel UHD).
            if ((gpu.Contains("iris") && gpu.Contains("xe")) || gpu.Contains("arc(tm)") || gpu.Contains(" arc ") || gpu.Contains("680m") || gpu.Contains("780m") || gpu.Contains("890m") || gpu.Contains("apple m"))
                return Medium;
            // Intel UHD / HD and older Radeon Vega iGPUs: measured 63 fps on Low, 38 on Medium, 22 on High (UHD 630-class, 1080p).
            if (gpu.Contains("intel") || gpu.Contains("uhd") || gpu.Contains("radeon(tm) graphics") || gpu.Contains("radeon graphics") || gpu.Contains("vega"))
                return Low;
            return memoryMB >= 4000 ? High : Medium;
        }
    }
}
