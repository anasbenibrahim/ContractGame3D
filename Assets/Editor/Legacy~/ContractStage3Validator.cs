using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace Contract.EditorTools
{
    public static class ContractStage3Validator
    {
        private const string GameWorldPath = "Assets/Scenes/GameWorld.unity";
        private const string Stage3MarkerPath = "ProjectSettings/ContractStage3.done";

        private static readonly string[] RequiredMaterials =
        {
            "Assets/Art/Materials/Ground_Dark.mat",
            "Assets/Art/Materials/Road_Asphalt.mat",
            "Assets/Art/Materials/Sidewalk_Concrete.mat",
            "Assets/Art/Materials/Building_Slate.mat",
            "Assets/Art/Materials/Building_Steel.mat",
            "Assets/Art/Materials/Metal_Dark.mat",
            "Assets/Art/Materials/Window_Warm.mat",
            "Assets/Art/Materials/Neon_Cyan.mat",
            "Assets/Art/Materials/Neon_Magenta.mat",
            "Assets/Art/Materials/Extraction_Green.mat",
            "Assets/Art/Materials/Warning_Yellow.mat",
            "Assets/Art/Materials/Foliage_Dark.mat",
            "Assets/Art/Materials/Road_Marking.mat",
            "Assets/Art/Materials/Prop_Crate.mat"
        };

        [MenuItem("CONTRACT/Validate/Stage 3")]
        public static void Validate()
        {
            var failures = new List<string>();
            if (!File.Exists(Stage3MarkerPath))
            {
                failures.Add("Stage 3 setup marker is missing.");
            }

            Scene scene = EditorSceneManager.OpenScene(GameWorldPath, OpenSceneMode.Single);
            ValidateSceneBasics(scene, failures);
            ValidateMaterials(failures);
            ValidateEnvironment(scene, failures);
            ValidateCity(scene, failures);
            ValidateSafehouse(scene, failures);
            ValidateExtraction(scene, failures);
            ValidateMarkers(scene, failures);
            ValidateBounds(scene, failures);
            ValidateLighting(scene, failures);
            ValidateOverlay(failures);
            ValidateSpawn(scene, failures);

            if (failures.Count > 0)
            {
                foreach (string failure in failures)
                {
                    Debug.LogError($"[CONTRACT-STAGE3] {failure}");
                }

                Debug.LogError($"[CONTRACT-STAGE3] FAILED with {failures.Count} issue(s).");
                if (Application.isBatchMode)
                {
                    EditorApplication.Exit(1);
                }

                return;
            }

            Debug.Log("[CONTRACT-STAGE3] PASSED: city environment, safehouse, extraction, markers, bounds, lighting and overlay are valid.");
        }

        private static void ValidateSceneBasics(Scene scene, ICollection<string> failures)
        {
            int missingScripts = 0;
            int cameraCount = 0;
            int controllerCount = 0;
            int spawnCount = 0;
            int playerCount = 0;

            foreach (GameObject root in scene.GetRootGameObjects())
            {
                foreach (Transform transform in root.GetComponentsInChildren<Transform>(true))
                {
                    GameObject gameObject = transform.gameObject;
                    if (gameObject.GetComponents<Component>().Any(component => component == null))
                    {
                        missingScripts++;
                    }

                    if (gameObject.GetComponent<Camera>() != null
                        && gameObject.name != "MinimapCamera"
                        && gameObject.name != "FullMapCamera")
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
                }
            }

            if (missingScripts > 0)
            {
                failures.Add($"GameWorld contains {missingScripts} missing script reference(s).");
            }

            if (cameraCount != 1)
            {
                failures.Add($"GameWorld must contain exactly one camera, found {cameraCount}.");
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
        }

        private static void ValidateMaterials(ICollection<string> failures)
        {
            foreach (string path in RequiredMaterials)
            {
                if (AssetDatabase.LoadAssetAtPath<Material>(path) == null)
                {
                    failures.Add($"Stage 3 material is missing: {path}");
                }
            }

            foreach (string path in new[]
                     {
                         "Assets/Art/Materials/Window_Warm.mat",
                         "Assets/Art/Materials/Neon_Cyan.mat",
                         "Assets/Art/Materials/Neon_Magenta.mat",
                         "Assets/Art/Materials/Extraction_Green.mat",
                         "Assets/Art/Materials/Warning_Yellow.mat"
                     })
            {
                Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (material != null && (!material.IsKeywordEnabled("_EMISSION") || material.GetColor("_EmissionColor").maxColorComponent <= 1f))
                {
                    failures.Add($"Emissive material is not configured: {path}");
                }
            }
        }

        private static void ValidateEnvironment(Scene scene, ICollection<string> failures)
        {
            int environmentLayer = LayerMask.NameToLayer("Environment");
            if (environmentLayer < 0)
            {
                failures.Add("Environment layer is missing.");
                return;
            }

            GameObject environment = FindRoot(scene, "Environment");
            if (environment == null)
            {
                failures.Add("GameWorld has no Environment root.");
                return;
            }

            int wrongLayerCount = 0;
            foreach (Transform transform in environment.GetComponentsInChildren<Transform>(true))
            {
                if (transform.gameObject.layer != environmentLayer)
                {
                    wrongLayerCount++;
                }
            }

            if (wrongLayerCount > 0)
            {
                failures.Add($"Environment contains {wrongLayerCount} object(s) outside the Environment layer.");
            }

            if (environment.GetComponentsInChildren<Renderer>(true).Length < 150)
            {
                failures.Add("Environment does not contain enough city renderers.");
            }

            GameObject ground = FindDeep(scene, "Ground");
            if (ground == null)
            {
                failures.Add("GameWorld is missing the Ground object.");
            }
            else if (ground.transform.localScale.x < 10f || ground.transform.localScale.z < 10f)
            {
                failures.Add("Ground is too small for the Stage 3 city.");
            }

            GameObject road = FindDeep(scene, "Road");
            if (road == null)
            {
                failures.Add("GameWorld is missing the Road object.");
            }
            else if (road.transform.localScale.z < 100f || road.transform.localScale.x < 14f)
            {
                failures.Add("Road does not match the Stage 3 city dimensions.");
            }

            if (FindDeep(scene, "Sidewalk L") == null || FindDeep(scene, "Sidewalk R") == null)
            {
                failures.Add("GameWorld is missing one or both sidewalks.");
            }
        }

        private static void ValidateCity(Scene scene, ICollection<string> failures)
        {
            int mainBuildings = 0;
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                foreach (Transform transform in root.GetComponentsInChildren<Transform>(true))
                {
                    if (!transform.name.StartsWith("Building_Main_", StringComparison.Ordinal))
                    {
                        continue;
                    }

                    mainBuildings++;
                    Renderer renderer = transform.GetComponent<Renderer>();
                    if (renderer != null && renderer.bounds.size.y < 10f)
                    {
                        failures.Add($"{transform.name} is too short for a city building.");
                    }
                }
            }

            if (mainBuildings < 10)
            {
                failures.Add($"City must contain at least 10 main buildings, found {mainBuildings}.");
            }

            if (CountByName(scene, "BackgroundTower_") < 6)
            {
                failures.Add("City is missing background towers.");
            }

            if (CountByName(scene, "Streetlight_") < 6)
            {
                failures.Add("City is missing streetlights.");
            }

            if (CountByName(scene, "Prop_") < 15)
            {
                failures.Add("City is missing street props.");
            }

            if (CountByName(scene, "Lane_Center_") < 10)
            {
                failures.Add("Road lane markings are incomplete.");
            }

            if (FindDeep(scene, "ExtractionZone") == null)
            {
                failures.Add("GameWorld is missing the ExtractionZone.");
            }
        }

        private static void ValidateSafehouse(Scene scene, ICollection<string> failures)
        {
            GameObject safehouse = FindDeep(scene, "Safehouse");
            if (safehouse == null)
            {
                failures.Add("GameWorld is missing the Safehouse.");
                return;
            }

            if (safehouse.GetComponentsInChildren<Renderer>(true).Length < 8)
            {
                failures.Add("Safehouse does not contain enough geometry.");
            }

            if (safehouse.GetComponentsInChildren<Light>(true).All(light => light.type != LightType.Point))
            {
                failures.Add("Safehouse has no interior point light.");
            }

            GameObject marker = FindDeep(scene, "SafehouseMarker");
            if (marker == null)
            {
                failures.Add("GameWorld is missing the SafehouseMarker.");
            }
            else
            {
                if (marker.tag != "Interactable")
                {
                    failures.Add("SafehouseMarker is not tagged 'Interactable'.");
                }

                int interactableLayer = LayerMask.NameToLayer("Interactable");
                if (marker.layer != interactableLayer)
                {
                    failures.Add($"SafehouseMarker layer is {marker.layer}, expected {interactableLayer}.");
                }
            }
        }

        private static void ValidateExtraction(Scene scene, ICollection<string> failures)
        {
            GameObject pad = FindDeep(scene, "ExtractionPad");
            if (pad == null || pad.GetComponent<Renderer>() == null)
            {
                failures.Add("GameWorld is missing the ExtractionPad.");
            }
            else
            {
                Material material = pad.GetComponent<Renderer>().sharedMaterial;
                if (material == null || !material.name.Contains("Extraction_Green"))
                {
                    failures.Add("ExtractionPad does not use the green extraction material.");
                }
            }

            if (FindDeep(scene, "ExtractionBeacon") == null)
            {
                failures.Add("GameWorld is missing the ExtractionBeacon.");
            }

            GameObject marker = FindDeep(scene, "ExtractionPoint");
            if (marker == null)
            {
                failures.Add("GameWorld is missing the ExtractionPoint marker.");
                return;
            }

            if (marker.tag != "ExtractionPoint")
            {
                failures.Add("ExtractionPoint marker is not tagged 'ExtractionPoint'.");
            }

            int interactableLayer = LayerMask.NameToLayer("Interactable");
            if (marker.layer != interactableLayer)
            {
                failures.Add($"ExtractionPoint marker layer is {marker.layer}, expected {interactableLayer}.");
            }

            BoxCollider trigger = marker.GetComponent<BoxCollider>();
            if (trigger == null || !trigger.isTrigger)
            {
                failures.Add("ExtractionPoint marker has no trigger collider.");
            }
        }

        private static void ValidateMarkers(Scene scene, ICollection<string> failures)
        {
            GameObject markers = FindRoot(scene, "WorldMarkers");
            if (markers == null)
            {
                failures.Add("GameWorld has no WorldMarkers root.");
                return;
            }

            int missionLayer = LayerMask.NameToLayer("MissionSubject");
            int npcLayer = LayerMask.NameToLayer("NPC");
            int missionTargets = 0;
            int npcSpawns = 0;
            int patrolPoints = 0;

            foreach (Transform transform in markers.GetComponentsInChildren<Transform>(true))
            {
                if (transform.name.StartsWith("MissionTarget_", StringComparison.Ordinal))
                {
                    missionTargets++;
                    if (transform.tag != "MissionSubject" || transform.gameObject.layer != missionLayer)
                    {
                        failures.Add($"{transform.name} must be tagged MissionSubject on the MissionSubject layer.");
                    }
                }
                else if (transform.name.StartsWith("NPC_Spawn_", StringComparison.Ordinal))
                {
                    npcSpawns++;
                    if (transform.tag != "NPC" || transform.gameObject.layer != npcLayer)
                    {
                        failures.Add($"{transform.name} must be tagged NPC on the NPC layer.");
                    }
                }
                else if (transform.name.StartsWith("PatrolPoint_", StringComparison.Ordinal))
                {
                    patrolPoints++;
                }
            }

            if (missionTargets < 2)
            {
                failures.Add($"WorldMarkers has too few mission targets, found {missionTargets}.");
            }

            if (npcSpawns < 4)
            {
                failures.Add($"WorldMarkers has too few NPC spawns, found {npcSpawns}.");
            }

            if (patrolPoints < 4)
            {
                failures.Add($"WorldMarkers has too few patrol points, found {patrolPoints}.");
            }
        }

        private static void ValidateBounds(Scene scene, ICollection<string> failures)
        {
            GameObject bounds = FindRoot(scene, "WorldBounds");
            if (bounds == null)
            {
                failures.Add("GameWorld has no WorldBounds root.");
                return;
            }

            BoxCollider[] colliders = bounds.GetComponentsInChildren<BoxCollider>(true);
            if (colliders.Length != 4)
            {
                failures.Add($"WorldBounds must contain exactly four box colliders, found {colliders.Length}.");
            }

            foreach (BoxCollider collider in colliders)
            {
                if (Mathf.Max(collider.bounds.size.x, collider.bounds.size.z) < 100f)
                {
                    failures.Add($"{collider.name} boundary is too short.");
                }
            }

            if (bounds.GetComponentsInChildren<Renderer>(true).Any(renderer => renderer.enabled))
            {
                failures.Add("WorldBounds renderers must be disabled.");
            }

            int environmentLayer = LayerMask.NameToLayer("Environment");
            if (bounds.GetComponentsInChildren<Transform>(true).Any(transform => transform.gameObject.layer != environmentLayer))
            {
                failures.Add("WorldBounds must stay on the Environment layer.");
            }
        }

        private static void ValidateLighting(Scene scene, ICollection<string> failures)
        {
            if (!RenderSettings.fog)
            {
                failures.Add("Stage 3 requires fog.");
            }
            else if (RenderSettings.fogDensity < 0.001f || RenderSettings.fogDensity > 0.02f)
            {
                failures.Add($"Fog density is out of range: {RenderSettings.fogDensity:0.0000}.");
            }

            Light[] directionalLights = Object.FindObjectsByType<Light>(FindObjectsInactive.Include)
                .Where(light => light.type == LightType.Directional)
                .ToArray();
            if (directionalLights.Length != 1)
            {
                failures.Add($"GameWorld must contain exactly one directional light, found {directionalLights.Length}.");
            }
            else if (directionalLights[0].intensity < 0.5f)
            {
                failures.Add("Directional light is too dim for the city.");
            }

            Camera camera = FindRoot(scene, "Main Camera")?.GetComponent<Camera>();
            if (camera != null && camera.farClipPlane < 300f)
            {
                failures.Add("Main Camera far clip plane is too short for the city.");
            }
        }

        private static void ValidateOverlay(ICollection<string> failures)
        {
            TMP_Text[] texts = Object.FindObjectsByType<TMP_Text>(FindObjectsInactive.Include);
            TMP_Text stageLabel = texts.FirstOrDefault(text => text.gameObject.name == "Stage Label");
            TMP_Text title = texts.FirstOrDefault(text => text.gameObject.name == "Title");
            TMP_Text objective = texts.FirstOrDefault(text => text.gameObject.name == "Objective Label");

            if (stageLabel == null)
            {
                failures.Add("GameWorld is missing the Stage Label text.");
            }
            else if (!stageLabel.text.Contains("STAGE"))
            {
                failures.Add("GameWorld overlay is missing a stage label.");
            }

            if (title == null || !title.text.Contains("DOWNTOWN"))
            {
                failures.Add("GameWorld title is not updated for the downtown district.");
            }

            if (objective == null || !objective.text.Contains("EXTRACTION"))
            {
                failures.Add("GameWorld is missing the extraction objective label.");
            }
        }

        private static void ValidateSpawn(Scene scene, ICollection<string> failures)
        {
            GameObject spawn = FindDeep(scene, "PlayerSpawn");
            if (spawn == null)
            {
                failures.Add("GameWorld has no PlayerSpawn marker.");
                return;
            }

            int environmentLayer = LayerMask.NameToLayer("Environment");
            if (spawn.layer != environmentLayer)
            {
                failures.Add($"PlayerSpawn layer is {spawn.layer}, expected {environmentLayer}.");
            }

            if (spawn.transform.parent == null || spawn.transform.parent.name != "Environment")
            {
                failures.Add("PlayerSpawn is not parented under the Environment root.");
            }

            GameObject player = FindRoot(scene, "Player");
            if (player == null)
            {
                failures.Add("GameWorld has no Player object.");
                return;
            }

            float horizontalDistance = Vector2.Distance(
                new Vector2(player.transform.position.x, player.transform.position.z),
                new Vector2(spawn.transform.position.x, spawn.transform.position.z));
            if (horizontalDistance > 0.1f)
            {
                failures.Add("Player is not positioned at PlayerSpawn.");
            }

            if (Mathf.Abs(player.transform.position.y - spawn.transform.position.y) > 0.25f)
            {
                failures.Add("Player height does not match PlayerSpawn.");
            }
        }

        private static int CountByName(Scene scene, string prefix)
        {
            int count = 0;
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                foreach (Transform transform in root.GetComponentsInChildren<Transform>(true))
                {
                    if (transform.name.StartsWith(prefix, StringComparison.Ordinal))
                    {
                        count++;
                    }
                }
            }

            return count;
        }

        private static GameObject FindRoot(Scene scene, string name)
        {
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                if (root.name == name)
                {
                    return root;
                }
            }

            return null;
        }

        private static GameObject FindDeep(Scene scene, string name)
        {
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                foreach (Transform transform in root.GetComponentsInChildren<Transform>(true))
                {
                    if (transform.name == name)
                    {
                        return transform.gameObject;
                    }
                }
            }

            return null;
        }
    }
}
