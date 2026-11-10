using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Contract.EditorTools
{
    /// <summary>
    /// Automated build pipeline for CONTRACT: SEOUL SHADOWS.
    /// Run: CONTRACT/Build/Build PC Standalone
    /// </summary>
    public class BuildPipeline : IPreprocessBuildWithReport, IPostprocessBuildWithReport
    {
        public int callbackOrder => 0;

        private const string GameName = "Contract Seoul Shadows"; // no ":" — used in save/build paths
        private const string CompanyName = "Contract Games";
        private const string BuildPath = "Builds/PC/";

        [MenuItem("CONTRACT/Build/Build PC Standalone (Development)")]
        public static void BuildDevelopment()
        {
            BuildPlayerOptions options = new BuildPlayerOptions
            {
                scenes = GetBuildScenes(),
                locationPathName = Path.Combine(BuildPath, "Contract_SeoulShadows_Dev.exe"),
                target = BuildTarget.StandaloneWindows64,
                options = BuildOptions.Development | BuildOptions.AllowDebugging | BuildOptions.ConnectWithProfiler,
            };

            BuildPlayerPipeline(options, "Development");
        }

        [MenuItem("CONTRACT/Build/Build PC Standalone (Release)")]
        public static void BuildRelease()
        {
            BuildPlayerOptions options = new BuildPlayerOptions
            {
                scenes = GetBuildScenes(),
                locationPathName = Path.Combine(BuildPath, "Contract_SeoulShadows_Windows/Contract_SeoulShadows.exe"),
                target = BuildTarget.StandaloneWindows64,
                options = BuildOptions.CompressWithLz4HC | BuildOptions.StrictMode,
            };

            BuildPlayerPipeline(options, "Release");
        }

        /// <summary>
        /// Headless Windows (64-bit, Mono) build, also from Linux: exits 0 / 1.
        /// Needs the "Windows Build Support (Mono)" module.
        /// -executeMethod Contract.EditorTools.BuildPipeline.BuildWindowsCI
        /// </summary>
        public static void BuildWindowsCI()
        {
            BuildPlayerOptions options = new BuildPlayerOptions
            {
                scenes = GetBuildScenes(),
                locationPathName = Path.Combine(BuildPath, "Contract_SeoulShadows_Windows/Contract_SeoulShadows.exe"),
                target = BuildTarget.StandaloneWindows64,
                options = BuildOptions.CompressWithLz4HC | BuildOptions.StrictMode,
            };

            bool ok = UnityEditor.BuildPipeline.IsBuildTargetSupported(BuildTargetGroup.Standalone, BuildTarget.StandaloneWindows64)
                      && BuildPlayerPipeline(options, "Windows Release");
            if (!ok) Debug.LogError("[Build] Windows build failed (is Windows Build Support installed?)");
            EditorApplication.Exit(ok ? 0 : 1);
        }

        [MenuItem("CONTRACT/Build/Build Linux Standalone (Release)")]
        public static void BuildLinux()
        {
            BuildPlayerOptions options = new BuildPlayerOptions
            {
                scenes = GetBuildScenes(),
                locationPathName = Path.Combine(BuildPath, "Contract_SeoulShadows_Linux/Contract_SeoulShadows.x86_64"),
                target = BuildTarget.StandaloneLinux64,
                options = BuildOptions.CompressWithLz4HC | BuildOptions.StrictMode,
            };

            BuildPlayerPipeline(options, "Linux Release");
        }

        /// <summary>
        /// Headless Linux build: exits with 0 on success and 1 on failure (validation or build).
        /// -executeMethod Contract.EditorTools.BuildPipeline.BuildLinuxCI
        /// </summary>
        public static void BuildLinuxCI()
        {
            BuildPlayerOptions options = new BuildPlayerOptions
            {
                scenes = GetBuildScenes(),
                locationPathName = Path.Combine(BuildPath, "Contract_SeoulShadows_Linux/Contract_SeoulShadows.x86_64"),
                target = BuildTarget.StandaloneLinux64,
                options = BuildOptions.CompressWithLz4HC | BuildOptions.StrictMode,
            };

            bool ok = BuildPlayerPipeline(options, "Linux Release");
            EditorApplication.Exit(ok ? 0 : 1);
        }

        /// <summary>
        /// Android APK (IL2CPP, ARM64, debug-signed) for device testing. Add -development for a
        /// development build. Exits 0 / 1.
        /// -executeMethod Contract.EditorTools.BuildPipeline.BuildAndroidCI
        /// </summary>
        [MenuItem("CONTRACT/Build/Build Android APK (device test)")]
        public static void BuildAndroidMenu() => BuildAndroid(false);

        public static void BuildAndroidCI()
        {
            bool development = Environment.GetCommandLineArgs().Contains("-development");
            EditorApplication.Exit(BuildAndroid(development) ? 0 : 1);
        }

        private static bool BuildAndroid(bool development)
        {
            if (!UnityEditor.BuildPipeline.IsBuildTargetSupported(BuildTargetGroup.Android, BuildTarget.Android))
            {
                Debug.LogError("[Build] Android build support is not installed.");
                return false;
            }

            PlayerSettings.productName = GameName;
            PlayerSettings.companyName = CompanyName;
            PlayerSettings.SetScriptingBackend(NamedBuildTarget.Android, ScriptingImplementation.IL2CPP);
            PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
            PlayerSettings.Android.minSdkVersion = AndroidSdkVersions.AndroidApiLevel26;
            PlayerSettings.SetManagedStrippingLevel(NamedBuildTarget.Android, ManagedStrippingLevel.Low);
            PlayerSettings.defaultInterfaceOrientation = UIOrientation.AutoRotation;
            PlayerSettings.allowedAutorotateToPortrait = false;
            PlayerSettings.allowedAutorotateToPortraitUpsideDown = false;
            PlayerSettings.allowedAutorotateToLandscapeLeft = true;
            PlayerSettings.allowedAutorotateToLandscapeRight = true;
            EditorUserBuildSettings.buildAppBundle = false;
            Contract.Editor.AndroidOptimizationSetup.ConfigurePlayer(); // ASTC, static batching, Vulkan + GLES3, frame pacing

            var options = new BuildPlayerOptions
            {
                scenes = GetBuildScenes(),
                locationPathName = "Builds/Android/ContractSeoulShadows.apk",
                target = BuildTarget.Android,
                options = development ? BuildOptions.Development : BuildOptions.None,
            };
            Directory.CreateDirectory("Builds/Android");
            Debug.Log($"[Build] Android {(development ? "development" : "release")} build -> {options.locationPathName}");
            BuildReport report = UnityEditor.BuildPipeline.BuildPlayer(options);
            Debug.Log($"[Build] Android result: {report.summary.result}, {report.summary.totalSize / 1048576f:F1} MB, {report.summary.totalTime.TotalSeconds:F0} s");
            return report.summary.result == BuildResult.Succeeded;
        }

        [MenuItem("CONTRACT/Build/Build All Platforms")]
        public static void BuildAllPlatforms()
        {
            BuildDevelopment();
            BuildRelease();
            BuildLinux();
        }

        private static string[] GetBuildScenes()
        {
            return new[]
            {
                "Assets/Scenes/MainMenu.unity",
                "Assets/Scenes/CharacterSelection.unity",
                "Assets/Scenes/WorldSelection.unity",
                "Assets/Scenes/MissionSelection.unity",
                "Assets/Scenes/GameWorld.unity",
                "Assets/Scenes/Myeongdong.unity",
            };
        }

        private static bool BuildPlayerPipeline(BuildPlayerOptions options, string buildType)
        {
            // Pre-build validation
            if (!ValidateBuild())
            {
                Debug.LogError($"[Build] Pre-build validation failed for {buildType}. Aborting.");
                return false;
            }

            if (!UnityEditor.BuildPipeline.IsBuildTargetSupported(BuildTargetGroup.Standalone, options.target))
            {
                Debug.LogError($"[Build] {options.target} build support is not installed (Unity Hub > Installs > Add modules).");
                return false;
            }

            // Ensure build directory exists
            string directory = Path.GetDirectoryName(options.locationPathName);
            if (!Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            // Set player settings
            ApplyPlayerSettings();

            Debug.Log($"[Build] Starting {buildType} build to {options.locationPathName}");

            // Build
            BuildReport report = UnityEditor.BuildPipeline.BuildPlayer(options);

            // Post-build
            BuildSummary summary = report.summary;
            if (summary.result == BuildResult.Succeeded)
            {
                Debug.Log($"[Build] {buildType} build succeeded!");
                Debug.Log($"[Build] Size: {summary.totalSize / (1024 * 1024):F1} MB");
                Debug.Log($"[Build] Time: {summary.totalTime.TotalSeconds:F1}s");
                Debug.Log($"[Build] Output: {options.locationPathName}");

                // Copy additional files
                CopyBuildDependencies(options.locationPathName);
                return true;
            }
            else
            {
                Debug.LogError($"[Build] {buildType} build failed: {summary.result}");
                foreach (var step in report.steps)
                {
                    foreach (var message in step.messages)
                    {
                        if (message.type == LogType.Error)
                        {
                            Debug.LogError($"[Build] {message.content}");
                        }
                    }
                }
            }
            return false;
        }

        private static bool ValidateBuild()
        {
            // Check all scenes exist
            string[] scenes = GetBuildScenes();
            foreach (string scene in scenes)
            {
                if (!File.Exists(scene))
                {
                    Debug.LogError($"[Build] Missing scene: {scene}");
                    return false;
                }
            }

            // Check required assets
            string[] requiredAssets = {
                "Assets/ScriptableObjects/Characters/char_alex_morgan.asset",
                "Assets/ScriptableObjects/Characters/char_daniel_cross.asset",
                "Assets/ScriptableObjects/Characters/char_ryan_blake.asset",
                "Assets/ScriptableObjects/Regions/region_downtown.asset",
                "Assets/Settings/ContractInputActions.inputactions",
                "Assets/Settings/InputReader.asset",
                "Assets/Prefabs/Player/Player.prefab",
            };

            foreach (string asset in requiredAssets)
            {
                if (!File.Exists(asset))
                {
                    Debug.LogError($"[Build] Missing required asset: {asset}");
                    return false;
                }
            }

            // Run validation tests
            bool allTestsPass = true;
            // Note: In a real scenario, you'd run the automated test suite here

            return allTestsPass;
        }

        private static void ApplyPlayerSettings()
        {
            PlayerSettings.productName = GameName;
            PlayerSettings.companyName = CompanyName;
            PlayerSettings.defaultScreenWidth = 1920;
            PlayerSettings.defaultScreenHeight = 1080;
            PlayerSettings.fullScreenMode = FullScreenMode.FullScreenWindow;
            PlayerSettings.resizableWindow = true;
            PlayerSettings.runInBackground = true;
            PlayerSettings.displayResolutionDialog = ResolutionDialogSetting.Enabled;
            PlayerSettings.usePlayerLog = true;
            PlayerSettings.stripEngineCode = true;
            // Low: the game and its packages reach some types only through serialization / reflection.
            PlayerSettings.SetManagedStrippingLevel(NamedBuildTarget.Standalone, ManagedStrippingLevel.Low);

            // Graphics
            PlayerSettings.colorSpace = ColorSpace.Linear;
            PlayerSettings.SetGraphicsAPIs(BuildTarget.StandaloneWindows64, new[] { UnityEngine.Rendering.GraphicsDeviceType.Direct3D11 });
            PlayerSettings.SetGraphicsAPIs(BuildTarget.StandaloneLinux64, new[] { UnityEngine.Rendering.GraphicsDeviceType.Vulkan, UnityEngine.Rendering.GraphicsDeviceType.OpenGLCore });

            // Quality
            QualitySettings.SetQualityLevel(1, true); // PC quality

            // Scripting
            PlayerSettings.SetScriptingBackend(NamedBuildTarget.Standalone, ScriptingImplementation.Mono2x);
            PlayerSettings.SetApiCompatibilityLevel(NamedBuildTarget.Standalone, ApiCompatibilityLevel.NET_Standard);

            // Splash
            PlayerSettings.SplashScreen.show = false;

            Debug.Log("[Build] Player settings applied");
        }

        private static void CopyBuildDependencies(string buildPath)
        {
            string buildDir = Path.GetDirectoryName(buildPath);

            // Copy any additional files needed
            // Example: ReadMe, license, etc.

            // Create a version file
            string versionPath = Path.Combine(buildDir, "VERSION.txt");
            File.WriteAllText(versionPath, $"CONTRACT: SEOUL SHADOWS\nBuild Date: {DateTime.Now:yyyy-MM-dd HH:mm}\nUnity Version: {Application.unityVersion}\n");
        }

        public void OnPreprocessBuild(BuildReport report)
        {
            Debug.Log($"[Build] Pre-process build: {report.summary.outputPath}");
        }

        public void OnPostprocessBuild(BuildReport report)
        {
            if (report.summary.result == BuildResult.Succeeded)
            {
                Debug.Log($"[Build] Post-process: Build completed successfully at {report.summary.outputPath}");
            }
        }
    }
}