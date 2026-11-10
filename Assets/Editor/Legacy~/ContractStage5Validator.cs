using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Contract.Audio;
using Contract.Contracts;
using Contract.Core;
using Contract.Map;
using Contract.Player;
using Contract.UI;
using Contract.VFX;
using Contract.CameraSystem;
using TMPro;
using UnityEngine.Audio;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace Contract.EditorTools
{
    public static class ContractStage5Validator
    {
        private const string GameWorldPath = "Assets/Scenes/GameWorld.unity";
        private const string Stage5MarkerPath = "ProjectSettings/ContractStage5.done";

        [MenuItem("CONTRACT/Validate/Stage 5")]
        public static void Validate()
        {
            var failures = new List<string>();
            if (!File.Exists(Stage5MarkerPath))
            {
                failures.Add("Stage 5 setup marker is missing.");
            }

            Scene scene = EditorSceneManager.OpenScene(GameWorldPath, OpenSceneMode.Single);
            ValidateSceneBasics(scene, failures);
            ValidateMinimap(failures);
            ValidateFullMap(failures);
            ValidateVFX(failures);
            ValidateCinemachine(failures);
            ValidateAudio(failures);

            if (failures.Count > 0)
            {
                foreach (string failure in failures)
                {
                    Debug.LogError($"[CONTRACT-STAGE5] {failure}");
                }

                Debug.LogError($"[CONTRACT-STAGE5] FAILED with {failures.Count} issue(s).");
                if (Application.isBatchMode)
                {
                    EditorApplication.Exit(1);
                }
                return;
            }

            Debug.Log("[CONTRACT-STAGE5] PASSED: minimap, full map, VFX, Cinemachine, and audio are valid.");
        }

        private static void ValidateSceneBasics(Scene scene, ICollection<string> failures)
        {
            int missingScripts = 0;
            int cameraCount = 0;
            int controllerCount = 0;
            int spawnCount = 0;
            int playerCount = 0;
            int mapManagerCount = 0;
            int vfxManagerCount = 0;
            int postProcessingCount = 0;

            foreach (GameObject root in scene.GetRootGameObjects())
            {
                foreach (Transform transform in root.GetComponentsInChildren<Transform>(true))
                {
                    GameObject gameObject = transform.gameObject;
                    if (gameObject.GetComponents<Component>().Any(component => component == null))
                    {
                        missingScripts++;
                    }

                    if (gameObject.GetComponent<Camera>() != null)
                    {
                        cameraCount++;
                    }

                    if (gameObject.GetComponent<Contract.Player.ThirdPersonController>() != null)
                    {
                        controllerCount++;
                    }

                    if (gameObject.name == "PlayerSpawn")
                    {
                        spawnCount++;
                    }

                    if (gameObject.name == "Player" && gameObject.transform.parent == null)
                    {
                        playerCount++;
                    }

                    if (gameObject.GetComponent<MapManager>() != null)
                    {
                        mapManagerCount++;
                    }

                    if (gameObject.GetComponent<VFXManager>() != null)
                    {
                        vfxManagerCount++;
                    }

                    if (gameObject.GetComponent<PostProcessingController>() != null)
                    {
                        postProcessingCount++;
                    }
                }
            }

            if (missingScripts > 0)
            {
                failures.Add($"GameWorld contains {missingScripts} missing script reference(s).");
            }

            if (cameraCount < 1)
            {
                failures.Add($"GameWorld must contain at least one camera, found {cameraCount}.");
            }

            if (controllerCount != 1)
            {
                failures.Add($"GameWorld must contain exactly one player controller, found {controllerCount}.");
            }

            if (spawnCount != 1)
            {
                failures.Add($"GameWorld must contain exactly one PlayerSpawn marker, found {spawnCount}.");
            }

            if (playerCount != 1)
            {
                failures.Add($"GameWorld must contain exactly one Player root, found {playerCount}.");
            }

            if (mapManagerCount != 1)
            {
                failures.Add($"GameWorld must contain exactly one MapManager, found {mapManagerCount}.");
            }

            if (vfxManagerCount != 1)
            {
                failures.Add($"GameWorld must contain exactly one VFXManager, found {vfxManagerCount}.");
            }

            if (postProcessingCount != 1)
            {
                failures.Add($"GameWorld must contain exactly one PostProcessingController, found {postProcessingCount}.");
            }
        }

        private static void ValidateMinimap(ICollection<string> failures)
        {
            MapManager mapManager = Object.FindAnyObjectByType<MapManager>();
            if (mapManager == null)
            {
                failures.Add("MapManager not found in scene.");
                return;
            }

            // The map is pre-rendered from Minimap-layer geometry at runtime (see MapManager.RenderStaticMap).
            if (Object.FindAnyObjectByType<MinimapView>() == null)
            {
                failures.Add("MinimapView not found in scene.");
            }

            int minimapLayer = LayerMask.NameToLayer("Minimap");
            if (minimapLayer < 0)
            {
                failures.Add("Minimap layer not defined in Tags and Layers.");
            }
            else if (Camera.main != null && (Camera.main.cullingMask & (1 << minimapLayer)) != 0)
            {
                failures.Add("Main camera must not render the Minimap layer.");
            }
        }

        private static void ValidateFullMap(ICollection<string> failures)
        {
            FullMapView fullMapUI = Object.FindAnyObjectByType<FullMapView>();
            if (fullMapUI == null)
            {
                failures.Add("FullMapView not found in scene.");
            }

        }

        private static void ValidateVFX(ICollection<string> failures)
        {
            VFXManager vfxManager = Object.FindAnyObjectByType<VFXManager>();
            if (vfxManager == null)
            {
                failures.Add("VFXManager not found in scene.");
                return;
            }

            PostProcessingController ppController = Object.FindAnyObjectByType<PostProcessingController>();
            if (ppController == null)
            {
                failures.Add("PostProcessingController not found in scene.");
            }
            else
            {
                Volume volume = ppController.GetComponent<Volume>();
                if (volume == null)
                {
                    failures.Add("PostProcessingController missing Volume component.");
                }
                else
                {
                    VolumeProfile profile = volume.profile;
                    if (profile == null)
                    {
                        failures.Add("PostProcessingController missing VolumeProfile.");
                    }
                    else
                    {
                        if (!profile.TryGet(out Vignette _)) failures.Add("VolumeProfile missing Vignette effect.");
                        if (!profile.TryGet(out ChromaticAberration _)) failures.Add("VolumeProfile missing ChromaticAberration effect.");
                        if (!profile.TryGet(out ColorAdjustments _)) failures.Add("VolumeProfile missing ColorAdjustments effect.");
                    }
                }
            }

            // Check particle prefabs
            string[] requiredParticles = { "interactionParticles", "suspicionParticles", "alertParticles", "extractionParticles", "footstepParticles", "impactParticles", "hackParticles", "pickupParticles" };
            foreach (string fieldName in requiredParticles)
            {
                var field = typeof(VFXManager).GetField(fieldName, System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public);
                if (field != null)
                {
                    GameObject prefab = field.GetValue(vfxManager) as GameObject;
                    if (prefab == null)
                    {
                        failures.Add($"VFXManager missing particle prefab: {fieldName}");
                    }
                    else if (prefab.GetComponent<ParticleSystem>() == null)
                    {
                        failures.Add($"VFXManager particle prefab {fieldName} missing ParticleSystem component.");
                    }
                }
            }
        }

        private static void ValidateCinemachine(ICollection<string> failures)
        {
            ThirdPersonCamera thirdPersonCamera = Object.FindAnyObjectByType<ThirdPersonCamera>();
            if (thirdPersonCamera == null)
            {
                failures.Add("ThirdPersonCamera not found on Main Camera.");
            }

            // Check camera target
            GameObject player = GameObject.FindGameObjectWithTag("Player");
            if (player != null)
            {
                Transform cameraTarget = player.transform.Find("CameraTarget");
                if (cameraTarget == null)
                {
                    failures.Add("Player missing CameraTarget child transform.");
                }
            }
        }

        private static void ValidateAudio(ICollection<string> failures)
        {
            AudioService audioService = Object.FindAnyObjectByType<AudioService>();
            if (audioService == null)
            {
                failures.Add("AudioService not found in scene.");
                return;
            }

            if (audioService.musicSource == null) failures.Add("AudioService missing musicSource.");
            if (audioService.sfxSource == null) failures.Add("AudioService missing sfxSource.");
            if (audioService.dialogueSource == null) failures.Add("AudioService missing dialogueSource.");
            if (audioService.ambienceSource == null) failures.Add("AudioService missing ambienceSource.");

            if (audioService.audioMixer == null)
            {
                failures.Add("AudioService missing AudioMixer reference.");
            }
            else
            {
                // Check exposed parameters
                float test;
                if (!audioService.audioMixer.GetFloat("MasterVolume", out test)) failures.Add("AudioMixer missing MasterVolume parameter.");
                if (!audioService.audioMixer.GetFloat("MusicVolume", out test)) failures.Add("AudioMixer missing MusicVolume parameter.");
                if (!audioService.audioMixer.GetFloat("SFXVolume", out test)) failures.Add("AudioMixer missing SFXVolume parameter.");
                if (!audioService.audioMixer.GetFloat("DialogueVolume", out test)) failures.Add("AudioMixer missing DialogueVolume parameter.");
                if (!audioService.audioMixer.GetFloat("AmbienceVolume", out test)) failures.Add("AudioMixer missing AmbienceVolume parameter.");
            }

            // Check AudioMixer groups
            AudioMixerGroup[] groups = audioService.audioMixer.FindMatchingGroups("Master");
            if (groups.Length == 0) failures.Add("AudioMixer missing Master group.");

            groups = audioService.audioMixer.FindMatchingGroups("Music");
            if (groups.Length == 0) failures.Add("AudioMixer missing Music group.");

            groups = audioService.audioMixer.FindMatchingGroups("SFX");
            if (groups.Length == 0) failures.Add("AudioMixer missing SFX group.");

            groups = audioService.audioMixer.FindMatchingGroups("Dialogue");
            if (groups.Length == 0) failures.Add("AudioMixer missing Dialogue group.");

            groups = audioService.audioMixer.FindMatchingGroups("Ambience");
            if (groups.Length == 0) failures.Add("AudioMixer missing Ambience group.");
        }
    }
}