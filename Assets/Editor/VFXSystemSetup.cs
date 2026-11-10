using System;
using Contract.VFX;
using UnityEngine;
using UnityEditor;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Object = UnityEngine.Object;

namespace Contract.EditorTools
{
    /// <summary>
    /// Sets up VFX system: PostProcessingController and VFXManager.
    /// Run: CONTRACT/Setup/Setup VFX System
    /// </summary>
    public static class VFXSystemSetup
    {
        [MenuItem("CONTRACT/Setup/Setup VFX System")]
        public static void SetupVFXSystem()
        {
            UnityEditor.SceneManagement.EditorSceneManager.OpenScene("Assets/Scenes/GameWorld.unity", UnityEditor.SceneManagement.OpenSceneMode.Single);

            SetupPostProcessingController();
            SetupVFXManager();
            CreateParticlePrefabs();

            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene());
            UnityEditor.SceneManagement.EditorSceneManager.SaveScene(UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene());
            UnityEditor.AssetDatabase.SaveAssets();

            Debug.Log("[CONTRACT] VFX system setup complete.");
        }

        private const string ProfilePath = "Assets/Settings/PostProcessingProfile.asset";

        private static void SetupPostProcessingController()
        {
            GameObject go = new GameObject("[PostProcessingController]");
            PostProcessingController controller = go.AddComponent<PostProcessingController>();

            // Create global Volume
            Volume volume = go.AddComponent<Volume>();
            volume.isGlobal = true;
            volume.priority = 100;

            VolumeProfile profile = GetOrCreateProfile();
            volume.sharedProfile = profile;

            controller.GlobalVolume = volume;
            controller.Profile = profile;

            EditorUtility.SetDirty(controller);
            EditorUtility.SetDirty(volume);
            AssetDatabase.SaveAssets();
        }

        private static VolumeProfile GetOrCreateProfile()
        {
            if (!AssetDatabase.IsValidFolder("Assets/Settings"))
            {
                AssetDatabase.CreateFolder("Assets", "Settings");
            }

            VolumeProfile profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(ProfilePath);
            if (profile == null)
            {
                profile = ScriptableObject.CreateInstance<VolumeProfile>();
                AssetDatabase.CreateAsset(profile, ProfilePath);
            }

            if (!profile.TryGet(out Vignette vignette))
            {
                vignette = profile.Add<Vignette>(true);
            }

            vignette.intensity.value = 0.32f;
            vignette.smoothness.value = 0.45f;
            vignette.color.value = new Color(0.01f, 0.01f, 0.015f);
            vignette.active = true;

            if (!profile.TryGet(out ChromaticAberration ca))
            {
                ca = profile.Add<ChromaticAberration>(true);
            }

            ca.intensity.value = 0f;
            ca.active = true;

            if (!profile.TryGet(out ColorAdjustments ca2))
            {
                ca2 = profile.Add<ColorAdjustments>(true);
            }

            ca2.postExposure.value = 0.15f;
            ca2.contrast.value = 12f;
            ca2.saturation.value = -12f;
            ca2.active = true;

            foreach (VolumeComponent component in profile.components)
            {
                if (!AssetDatabase.Contains(component))
                {
                    AssetDatabase.AddObjectToAsset(component, profile);
                }
            }

            EditorUtility.SetDirty(profile);
            return profile;
        }

        private static void SetupVFXManager()
        {
            GameObject go = new GameObject("[VFXManager]");
            VFXManager vfxManager = go.AddComponent<VFXManager>();

            // Create particle prefabs if they don't exist
            string prefabPath = "Assets/Prefabs/VFX/";
            if (!AssetDatabase.IsValidFolder(prefabPath))
            {
                string guid = AssetDatabase.CreateFolder("Assets/Prefabs", "VFX");
                prefabPath = AssetDatabase.GUIDToAssetPath(guid) + "/";
            }

            vfxManager.InteractionParticles = GetOrCreateParticlePrefab(prefabPath + "InteractionParticles.prefab", "Interaction", ParticlePresets.CreateInteractionParticles);
            vfxManager.SuspicionParticles = GetOrCreateParticlePrefab(prefabPath + "SuspicionParticles.prefab", "Suspicion", ParticlePresets.CreateSuspicionParticles);
            vfxManager.AlertParticles = GetOrCreateParticlePrefab(prefabPath + "AlertParticles.prefab", "Alert", ParticlePresets.CreateAlertParticles);
            vfxManager.ExtractionParticles = GetOrCreateParticlePrefab(prefabPath + "ExtractionParticles.prefab", "Extraction", ParticlePresets.CreateExtractionParticles);
            vfxManager.FootstepParticles = GetOrCreateParticlePrefab(prefabPath + "FootstepParticles.prefab", "Footstep", ParticlePresets.CreateFootstepParticles);
            vfxManager.ImpactParticles = GetOrCreateParticlePrefab(prefabPath + "ImpactParticles.prefab", "Impact", ParticlePresets.CreateImpactParticles);
            vfxManager.HackParticles = GetOrCreateParticlePrefab(prefabPath + "HackParticles.prefab", "Hack", ParticlePresets.CreateHackParticles);
            vfxManager.PickupParticles = GetOrCreateParticlePrefab(prefabPath + "PickupParticles.prefab", "Pickup", ParticlePresets.CreatePickupParticles);

            EditorUtility.SetDirty(vfxManager);
        }

        private static GameObject GetOrCreateParticlePrefab(string path, string name, System.Func<ParticleSystem, ParticleSystem> createFn)
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab != null) return prefab;

            GameObject go = new GameObject(name);
            ParticleSystem ps = go.AddComponent<ParticleSystem>();
            ps = createFn(ps);

            PrefabUtility.SaveAsPrefabAsset(go, path);
            Object.DestroyImmediate(go);

            return AssetDatabase.LoadAssetAtPath<GameObject>(path);
        }

        private static void CreateParticlePrefabs()
        {
            // Prefabs are created in SetupVFXManager via GetOrCreateParticlePrefab
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }
    }

    internal static class ParticlePresets
    {
        public static ParticleSystem CreateInteractionParticles(ParticleSystem ps)
        {
            var main = ps.main;
            main.duration = 0.5f;
            main.loop = false;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.3f, 0.8f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(1f, 3f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.1f, 0.3f);
            main.startColor = new ParticleSystem.MinMaxGradient(new Color(0.2f, 0.8f, 1f, 1f), new Color(0f, 0.5f, 1f, 0f));
            main.gravityModifier = 0f;

            var emission = ps.emission;
            emission.rateOverTime = 0f;
            emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 10, 20) });

            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = 0.2f;

            var velocity = ps.velocityOverLifetime;
            velocity.enabled = true;
            velocity.space = ParticleSystemSimulationSpace.World;
            velocity.y = new ParticleSystem.MinMaxCurve(-1f, 1f);
            velocity.x = new ParticleSystem.MinMaxCurve(0f, 0f); // all axes must share one curve mode
            velocity.z = new ParticleSystem.MinMaxCurve(0f, 0f); // all axes must share one curve mode

            var colorOverLifetime = ps.colorOverLifetime;
            colorOverLifetime.enabled = true;
            colorOverLifetime.color = new ParticleSystem.MinMaxGradient(Color.white, new Color(1f, 1f, 1f, 0f));

            return ps;
        }

        public static ParticleSystem CreateSuspicionParticles(ParticleSystem ps)
        {
            var main = ps.main;
            main.duration = 1f;
            main.loop = true;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.5f, 1.5f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.5f, 1.5f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.05f, 0.15f);
            main.startColor = new ParticleSystem.MinMaxGradient(new Color(1f, 0.8f, 0f, 0.6f), new Color(1f, 0.3f, 0f, 0f));
            main.gravityModifier = -0.1f;

            var emission = ps.emission;
            emission.rateOverTime = 5f;

            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = 30f;
            shape.radius = 0.1f;

            var velocity = ps.velocityOverLifetime;
            velocity.enabled = true;
            velocity.space = ParticleSystemSimulationSpace.Local;
            velocity.y = new ParticleSystem.MinMaxCurve(0.5f, 1f);
            velocity.x = new ParticleSystem.MinMaxCurve(0f, 0f); // all axes must share one curve mode
            velocity.z = new ParticleSystem.MinMaxCurve(0f, 0f); // all axes must share one curve mode

            return ps;
        }

        public static ParticleSystem CreateAlertParticles(ParticleSystem ps)
        {
            var main = ps.main;
            main.duration = 0.3f;
            main.loop = false;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.2f, 0.5f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(5f, 10f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.2f, 0.5f);
            main.startColor = new ParticleSystem.MinMaxGradient(new Color(1f, 0.1f, 0.1f, 1f), new Color(1f, 0f, 0f, 0f));
            main.gravityModifier = 0f;

            var emission = ps.emission;
            emission.rateOverTime = 0f;
            emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 30, 50) });

            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = 0.5f;

            var velocity = ps.velocityOverLifetime;
            velocity.enabled = true;
            velocity.space = ParticleSystemSimulationSpace.World;
            velocity.y = new ParticleSystem.MinMaxCurve(-2f, 2f);
            velocity.x = new ParticleSystem.MinMaxCurve(0f, 0f); // all axes must share one curve mode
            velocity.z = new ParticleSystem.MinMaxCurve(0f, 0f); // all axes must share one curve mode

            return ps;
        }

        public static ParticleSystem CreateExtractionParticles(ParticleSystem ps)
        {
            var main = ps.main;
            main.duration = 2f;
            main.loop = true;
            main.startLifetime = new ParticleSystem.MinMaxCurve(1f, 2f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(1f, 3f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.2f, 0.5f);
            main.startColor = new ParticleSystem.MinMaxGradient(new Color(0f, 1f, 0.3f, 0.8f), new Color(0f, 0.5f, 0.2f, 0f));
            main.gravityModifier = -0.2f;

            var emission = ps.emission;
            emission.rateOverTime = 15f;

            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Circle;
            shape.radius = 1.5f;
            shape.position = new Vector3(0f, 0.1f, 0f);
            shape.rotation = new Vector3(90f, 0f, 0f);

            var velocity = ps.velocityOverLifetime;
            velocity.enabled = true;
            velocity.space = ParticleSystemSimulationSpace.Local;
            velocity.y = new ParticleSystem.MinMaxCurve(0.5f, 1.5f);
            velocity.x = new ParticleSystem.MinMaxCurve(0f, 0f); // all axes must share one curve mode
            velocity.z = new ParticleSystem.MinMaxCurve(0f, 0f); // all axes must share one curve mode

            var rotation = ps.rotationOverLifetime;
            rotation.enabled = true;
            rotation.z = new ParticleSystem.MinMaxCurve(-45f, 45f);

            return ps;
        }

        public static ParticleSystem CreateFootstepParticles(ParticleSystem ps)
        {
            var main = ps.main;
            main.duration = 0.2f;
            main.loop = false;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.1f, 0.3f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.5f, 1.5f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.05f, 0.15f);
            main.startColor = new ParticleSystem.MinMaxGradient(new Color(0.4f, 0.3f, 0.2f, 0.5f), new Color(0.3f, 0.2f, 0.1f, 0f));
            main.gravityModifier = 1f;

            var emission = ps.emission;
            emission.rateOverTime = 0f;
            emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 3, 8) });

            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Hemisphere;
            shape.radius = 0.1f;

            var velocity = ps.velocityOverLifetime;
            velocity.enabled = true;
            velocity.space = ParticleSystemSimulationSpace.World;
            velocity.y = new ParticleSystem.MinMaxCurve(0.5f, 1.5f);
            velocity.x = new ParticleSystem.MinMaxCurve(0f, 0f); // all axes must share one curve mode
            velocity.z = new ParticleSystem.MinMaxCurve(0f, 0f); // all axes must share one curve mode

            return ps;
        }

        public static ParticleSystem CreateImpactParticles(ParticleSystem ps)
        {
            var main = ps.main;
            main.duration = 0.3f;
            main.loop = false;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.2f, 0.5f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(2f, 5f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.1f, 0.3f);
            main.startColor = new ParticleSystem.MinMaxGradient(new Color(0.6f, 0.5f, 0.4f, 1f), new Color(0.4f, 0.3f, 0.2f, 0f));
            main.gravityModifier = 1f;

            var emission = ps.emission;
            emission.rateOverTime = 0f;
            emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 15, 30) });

            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Hemisphere;
            shape.radius = 0.1f;

            var velocity = ps.velocityOverLifetime;
            velocity.enabled = true;
            velocity.space = ParticleSystemSimulationSpace.World;
            velocity.y = new ParticleSystem.MinMaxCurve(1f, 3f);
            velocity.x = new ParticleSystem.MinMaxCurve(0f, 0f); // all axes must share one curve mode
            velocity.z = new ParticleSystem.MinMaxCurve(0f, 0f); // all axes must share one curve mode

            return ps;
        }

        public static ParticleSystem CreateHackParticles(ParticleSystem ps)
        {
            var main = ps.main;
            main.duration = 1f;
            main.loop = true;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.3f, 0.8f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.5f, 1.5f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.03f, 0.08f);
            main.startColor = new ParticleSystem.MinMaxGradient(new Color(0f, 1f, 1f, 0.7f), new Color(0f, 0.5f, 1f, 0f));
            main.gravityModifier = -0.1f;

            var emission = ps.emission;
            emission.rateOverTime = 20f;

            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = new Vector3(0.5f, 0.5f, 0.1f);

            var velocity = ps.velocityOverLifetime;
            velocity.enabled = true;
            velocity.space = ParticleSystemSimulationSpace.Local;
            velocity.z = new ParticleSystem.MinMaxCurve(-0.5f, 0.5f);
            velocity.x = new ParticleSystem.MinMaxCurve(0f, 0f); // all axes must share one curve mode
            velocity.y = new ParticleSystem.MinMaxCurve(0f, 0f); // all axes must share one curve mode

            return ps;
        }

        public static ParticleSystem CreatePickupParticles(ParticleSystem ps)
        {
            var main = ps.main;
            main.duration = 0.5f;
            main.loop = false;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.5f, 1f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(1f, 2f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.15f, 0.3f);
            main.startColor = new ParticleSystem.MinMaxGradient(new Color(1f, 0.9f, 0.2f, 1f), new Color(1f, 0.5f, 0f, 0f));
            main.gravityModifier = -0.3f;

            var emission = ps.emission;
            emission.rateOverTime = 0f;
            emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 20, 40) });

            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = 0.3f;

            var velocity = ps.velocityOverLifetime;
            velocity.enabled = true;
            velocity.space = ParticleSystemSimulationSpace.World;
            velocity.y = new ParticleSystem.MinMaxCurve(1f, 3f);
            velocity.x = new ParticleSystem.MinMaxCurve(0f, 0f); // all axes must share one curve mode
            velocity.z = new ParticleSystem.MinMaxCurve(0f, 0f); // all axes must share one curve mode

            var rotation = ps.rotationOverLifetime;
            rotation.enabled = true;
            rotation.z = new ParticleSystem.MinMaxCurve(-180f, 180f);

            return ps;
        }
    }
}