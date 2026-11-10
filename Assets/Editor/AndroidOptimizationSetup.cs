using System.Collections.Generic;
using System.Linq;
using Contract.Performance;
using UnityEditor;
using UnityEditor.Build;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Contract.Editor
{
    /// <summary>
    /// Phase 10: Android graphics presets and build optimisation. Idempotent.
    ///  - Three phone URP assets (Android_Low / Medium / High) on the lean renderer, and the
    ///    MobileQualityProfile that maps them to the Low / Medium / High quality levels together
    ///    with a LOD bias, crowd size and post-processing flag. PC tiers are not touched.
    ///  - Android player settings: ASTC textures, static batching (the city is built from many
    ///    small meshes sharing few materials), frame pacing, landscape, IL2CPP ARM64, API 26+.
    ///  - LOD culling for small props, vehicles and sign text in the district prefabs, so distant
    ///    detail stops costing draw calls (the culling distance scales with the preset's LOD bias).
    /// Headless: -executeMethod Contract.Editor.AndroidOptimizationSetup.Apply
    /// </summary>
    public static class AndroidOptimizationSetup
    {
        public const string ProfilePath = "Assets/Resources/MobileQualityProfile.asset";
        private const string Source = "Assets/Settings/Mobile_RPAsset.asset";
        private const string LeanRenderer = "Assets/Settings/Mobile_Renderer.asset";

        /// <summary>Screen-height share below which an object is culled (before the LOD bias).</summary>
        public const float PropCull = 0.025f, VehicleCull = 0.02f, TextCull = 0.03f;

        private static readonly string[] DistrictPrefabs =
        {
            "Assets/Prefabs/Environment/District_Gangnam.prefab",
            "Assets/Prefabs/Environment/District_Myeongdong.prefab"
        };

        private static readonly string[] Vehicles = { "Taxi", "Car_", "Bus", "Scooter", "Truck", "Van" };

        private struct Preset
        {
            public string name;
            public float renderScale, shadowDistance, lodBias;
            public bool hdr, softShadows, postProcessing;
            public int msaa, shadowResolution, cascades, softQuality, lightMode, lightLimit, civilians;
        }

        // Additional-light modes: 0 off, 1 per pixel, 2 per vertex.
        private static readonly Preset[] Presets =
        {
            new Preset { name = "Low", renderScale = 0.7f, hdr = false, msaa = 1, shadowResolution = 512, shadowDistance = 25f, cascades = 1,
                softShadows = false, softQuality = 1, lightMode = 2, lightLimit = 2, lodBias = 0.6f, civilians = 10, postProcessing = false },
            new Preset { name = "Medium", renderScale = 0.85f, hdr = true, msaa = 1, shadowResolution = 1024, shadowDistance = 35f, cascades = 1,
                softShadows = true, softQuality = 1, lightMode = 1, lightLimit = 4, lodBias = 0.9f, civilians = 16, postProcessing = true },
            new Preset { name = "High", renderScale = 1f, hdr = true, msaa = 2, shadowResolution = 2048, shadowDistance = 50f, cascades = 2,
                softShadows = true, softQuality = 2, lightMode = 1, lightLimit = 6, lodBias = 1.2f, civilians = 24, postProcessing = true },
        };

        [MenuItem("CONTRACT/Setup/Apply Android Optimisation")]
        public static void Apply()
        {
            BuildProfile();
            ConfigurePlayer();
            KeepPostProcessingResources();
            int groups = DistrictPrefabs.Sum(AddLodCulling);
            AssetDatabase.SaveAssets();
            Debug.Log($"[AndroidOptimizationSetup] Phone presets, player settings and {groups} LOD groups applied.");
        }

        // ---- presets ----------------------------------------------------------------------------

        private static void BuildProfile()
        {
            var renderer = AssetDatabase.LoadAssetAtPath<ScriptableRendererData>(LeanRenderer);
            var tiers = new List<MobileQualityProfile.Tier>();
            foreach (Preset p in Presets)
            {
                string path = $"Assets/Settings/Android_{p.name}_RPAsset.asset";
                if (AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(path) == null) AssetDatabase.CopyAsset(Source, path);
                var asset = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(path);
                var so = new SerializedObject(asset);
                void Set(string field, System.Action<SerializedProperty> write)
                {
                    SerializedProperty prop = so.FindProperty(field);
                    if (prop == null) throw new System.InvalidOperationException($"URP asset field {field} not found");
                    write(prop);
                }

                Set("m_RenderScale", x => x.floatValue = p.renderScale);
                Set("m_SupportsHDR", x => x.boolValue = p.hdr);
                Set("m_HDRColorBufferPrecision", x => x.intValue = 0); // 32-bit R11G11B10: cheap on tile GPUs
                Set("m_MSAA", x => x.intValue = p.msaa);
                Set("m_MainLightShadowsSupported", x => x.boolValue = true);
                Set("m_MainLightShadowmapResolution", x => x.intValue = p.shadowResolution);
                Set("m_ShadowDistance", x => x.floatValue = p.shadowDistance);
                Set("m_ShadowCascadeCount", x => x.intValue = p.cascades);
                Set("m_SoftShadowsSupported", x => x.boolValue = p.softShadows);
                Set("m_SoftShadowQuality", x => x.intValue = p.softQuality);
                Set("m_AdditionalLightsRenderingMode", x => x.intValue = p.lightMode);
                Set("m_AdditionalLightsPerObjectLimit", x => x.intValue = p.lightLimit);
                Set("m_AdditionalLightShadowsSupported", x => x.boolValue = false);
                Set("m_RequireDepthTexture", x => x.boolValue = false);
                Set("m_RequireOpaqueTexture", x => x.boolValue = false);
                Set("m_SupportsTerrainHoles", x => x.boolValue = false);
                Set("m_ReflectionProbeBlending", x => x.boolValue = p.name == "High");
                Set("m_SupportsLightCookies", x => x.boolValue = false);
                Set("m_ColorGradingLutSize", x => x.intValue = p.name == "Low" ? 16 : 32);
                Set("m_UseSRPBatcher", x => x.boolValue = true);
                SerializedProperty list = so.FindProperty("m_RendererDataList");
                list.arraySize = 1;
                list.GetArrayElementAtIndex(0).objectReferenceValue = renderer;
                so.FindProperty("m_DefaultRendererIndex").intValue = 0;
                so.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(asset);
                tiers.Add(new MobileQualityProfile.Tier { name = p.name, pipeline = asset, lodBias = p.lodBias, maxCivilians = p.civilians, postProcessing = p.postProcessing });
            }

            System.IO.Directory.CreateDirectory("Assets/Resources");
            var profile = AssetDatabase.LoadAssetAtPath<MobileQualityProfile>(ProfilePath);
            if (profile == null)
            {
                profile = ScriptableObject.CreateInstance<MobileQualityProfile>();
                AssetDatabase.CreateAsset(profile, ProfilePath);
            }

            profile.tiers = tiers.ToArray();
            EditorUtility.SetDirty(profile);
        }

        // ---- player settings ----------------------------------------------------------------------

        public static void ConfigurePlayer()
        {
            NamedBuildTarget android = NamedBuildTarget.Android;
            PlayerSettings.SetScriptingBackend(android, ScriptingImplementation.IL2CPP);
            PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
            PlayerSettings.Android.minSdkVersion = AndroidSdkVersions.AndroidApiLevel26;
            PlayerSettings.Android.targetSdkVersion = AndroidSdkVersions.AndroidApiLevelAuto; // newest installed
            PlayerSettings.SetUseDefaultGraphicsAPIs(BuildTarget.Android, false);
            PlayerSettings.SetGraphicsAPIs(BuildTarget.Android, new[] { GraphicsDeviceType.Vulkan, GraphicsDeviceType.OpenGLES3 });
            EditorUserBuildSettings.androidBuildSubtarget = MobileTextureSubtarget.ASTC;
            PlayerSettings.Android.textureCompressionFormats = new[] { TextureCompressionFormat.ASTC, TextureCompressionFormat.ETC2 };
            PlayerSettings.SetStaticBatchingForPlatform(BuildTarget.Android, true);
            PlayerSettings.Android.optimizedFramePacing = true;
            PlayerSettings.SetMobileMTRendering(android, true);
            PlayerSettings.defaultInterfaceOrientation = UIOrientation.AutoRotation;
            PlayerSettings.allowedAutorotateToPortrait = false;
            PlayerSettings.allowedAutorotateToPortraitUpsideDown = false;
            PlayerSettings.allowedAutorotateToLandscapeLeft = true;
            PlayerSettings.allowedAutorotateToLandscapeRight = true;
            // First launch on a phone picks the tier from the GPU (PerformanceManager.RecommendMobile); Medium until then.
            var quality = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/QualitySettings.asset")[0]);
            SerializedProperty defaults = quality.FindProperty("m_PerPlatformDefaultQuality");
            for (int i = 0; i < defaults.arraySize; i++)
            {
                SerializedProperty entry = defaults.GetArrayElementAtIndex(i);
                if (entry.FindPropertyRelative("first").stringValue == "Android") entry.FindPropertyRelative("second").intValue = 1;
            }

            quality.ApplyModifiedPropertiesWithoutUndo();
        }

        /// <summary>
        /// URP's "strip unused post-processing resources" removes the depth-of-field shader (no volume
        /// uses DoF); URP then disables every post-processing pass in the player (found on the
        /// Galaxy A22: "PostProcessing render passes will not execute"). Keep the resources.
        /// </summary>
        public static void KeepPostProcessingResources()
        {
            var stripping = GraphicsSettings.GetRenderPipelineSettings<URPShaderStrippingSetting>();
            if (stripping != null) stripping.stripUnusedPostProcessingVariants = false;
            Object global = AssetDatabase.LoadAssetAtPath<Object>("Assets/Settings/UniversalRenderPipelineGlobalSettings.asset");
            if (global != null)
            {
                var so = new SerializedObject(global);
                SerializedProperty legacy = so.FindProperty("m_StripUnusedPostProcessingVariants");
                if (legacy != null) legacy.boolValue = false;
                SerializedProperty migrated = so.FindProperty("m_URPShaderStrippingSetting.m_StripUnusedPostProcessingVariantsAndResources");
                if (migrated != null) migrated.boolValue = false;
                so.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(global);
            }
        }

        // ---- LOD culling ---------------------------------------------------------------------------

        private static bool HasTargetAncestor(Transform t, HashSet<Transform> targets)
        {
            for (Transform p = t.parent; p != null; p = p.parent)
                if (targets.Contains(p)) return true;
            return false;
        }

        /// <summary>Adds single-level LOD groups (cull when small on screen) to props, vehicles and sign text.</summary>
        private static int AddLodCulling(string prefabPath)
        {
            if (AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath) == null) return 0;
            GameObject root = PrefabUtility.LoadPrefabContents(prefabPath);
            int count = 0;
            try
            {
                var targets = new List<(GameObject go, float cull)>();
                foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
                {
                    if (t == root.transform) continue;
                    bool prop = PrefabUtility.IsAnyPrefabInstanceRoot(t.gameObject) && PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(t.gameObject).Contains("/Environment/Korean/");
                    bool cart = t.name.StartsWith("Cart_");
                    bool text = t.GetComponent<TMPro.TextMeshPro>() != null;
                    if (!prop && !cart && !text) continue;
                    float cull = text ? TextCull : Vehicles.Any(v => t.name.Contains(v)) ? VehicleCull : PropCull;
                    targets.Add((t.gameObject, cull));
                }

                // Text inside a culled prop (bus-stop signs, bin labels ...) belongs to the prop's group only:
                // a renderer in two LOD groups is an error (found on device).
                var targetSet = new HashSet<Transform>(targets.Select(x => x.go.transform));
                targets = targets.Where(x => !HasTargetAncestor(x.go.transform, targetSet)).ToList();
                foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
                    if (HasTargetAncestor(t, targetSet) && t.TryGetComponent(out LODGroup nested)) Object.DestroyImmediate(nested);

                foreach ((GameObject go, float cull) in targets)
                {
                    Renderer[] renderers = go.GetComponentsInChildren<Renderer>(true).Where(r => !(r is ParticleSystemRenderer)).ToArray();
                    if (renderers.Length == 0) continue;
                    if (!go.TryGetComponent(out LODGroup group)) group = go.AddComponent<LODGroup>();
                    group.fadeMode = LODFadeMode.None;
                    group.SetLODs(new[] { new LOD(cull, renderers) });
                    group.RecalculateBounds();
                    count++;
                }

                PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }

            return count;
        }
    }
}
