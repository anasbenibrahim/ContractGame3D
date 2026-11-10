using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Contract.Core;
using Contract.Missions;
using Contract.SaveSystem;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;

namespace Contract.Bootstrap
{
    /// <summary>
    /// Automated benchmark for built players: launch with <c>-contract-benchmark</c>. Plays the
    /// real flow (main menu → new game → operative → destination → contract → Gangnam), tours six
    /// spots of Gangnam and six of Myeongdong with V-Sync off (phones: up to the display rate), records frame-time statistics per phase, writes them to the log
    /// and to benchmark.txt in the persistent data folder, then quits (exit code 0, or 1 if the
    /// flow broke). Saves go to a separate folder so a player's career is never touched.
    /// Android: adb shell am start -n com.contractgames.seoulshadows/com.unity3d.player.UnityPlayerGameActivity
    ///          -e unity "-contract-benchmark -contract-quality 1"
    /// </summary>
    public sealed class BenchmarkRunner : MonoBehaviour
    {
        private const string Flag = "-contract-benchmark";

        private static readonly Vector3[] Tour =
        {
            new Vector3(-10.5f, 0f, -46.5f), new Vector3(0f, 0f, -20f), new Vector3(15f, 0f, 10f),
            new Vector3(-25f, 0f, 15f), new Vector3(10f, 0f, 45f), new Vector3(40f, 0f, -15f)
        };

        private static readonly Vector3[] MyeongdongTour =
        {
            new Vector3(0f, 0.2f, -40f), new Vector3(0f, 0.2f, 10f), new Vector3(-35f, 0.2f, 19f),
            new Vector3(30f, 0.2f, 18f), new Vector3(0f, 0.2f, 60f), new Vector3(60f, 0.2f, 0f)
        };

        private int quality = -1;

        private readonly StringBuilder report = new StringBuilder();
        private bool failed;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Launch()
        {
            if (!LaunchArguments.Has(Flag)) return; // desktop command line or the Android intent extra "unity"
            var go = new GameObject("[Benchmark]");
            DontDestroyOnLoad(go);
            go.AddComponent<BenchmarkRunner>();
        }

        private IEnumerator Start()
        {
            SaveManager.Instance?.RedirectSaves("Saves_Benchmark");
            if (int.TryParse(LaunchArguments.Value("-contract-quality"), out int level)) quality = level;
            yield return null;
            ApplyBenchmarkSettings();
            Line($"CONTRACT benchmark — {DateTime.Now:yyyy-MM-dd HH:mm} — {SystemInfo.graphicsDeviceName} ({SystemInfo.graphicsDeviceType}), " +
                 $"{SystemInfo.processorType}, {Screen.width}x{Screen.height}, quality {QualitySettings.names[QualitySettings.GetQualityLevel()]}");

            yield return WaitForScene(GameConstants.Scenes.MainMenu, 20f);
            yield return Measure("Main menu", 4f);

            GameManager.Instance.StartNewGame();
            ApplyBenchmarkSettings();
            yield return WaitForScene(GameConstants.Scenes.CharacterSelection, 15f);
            yield return Measure("Character selection", 3f);

            GameManager.Instance.SetSelectedCharacter("char_alex_morgan");
            GameManager.Instance.SceneLoader.LoadWorldSelection();
            yield return WaitForScene(GameConstants.Scenes.WorldSelection, 15f);
            yield return Measure("World selection", 3f);

            GameManager.Instance.SceneLoader.LoadMissionSelection();
            yield return WaitForScene(GameConstants.Scenes.MissionSelection, 15f);
            yield return Measure("Mission selection", 3f);

            MissionManager.Instance.StartMission("mission_downtown_01");
            float loadStart = Time.realtimeSinceStartup;
            GameManager.Instance.SceneLoader.LoadGameWorld();
            yield return WaitForScene(GameConstants.Scenes.GameWorld, 30f);
            Line($"GameWorld load: {Time.realtimeSinceStartup - loadStart:F2} s");
            Check(MissionManager.Instance.ActiveMission?.missionId == "mission_downtown_01", "contract carried into the city");
            yield return new WaitForSecondsRealtime(2f); // crowd spawns

            GameObject player = GameObject.FindGameObjectWithTag("Player");
            Check(player != null, "player present");
            var all = new List<float>();
            for (int i = 0; player != null && i < Tour.Length; i++)
            {
                Teleport(player, Tour[i]);
                yield return Measure($"Gangnam stop {i + 1}", 4f, all);
                if (i == 1)
                {
                    string shot = Path.Combine(Application.persistentDataPath, $"benchmark_{QualitySettings.names[QualitySettings.GetQualityLevel()]}.png");
                    ScreenCapture.CaptureScreenshot(shot);
                    Line($"Screenshot: {shot}");
                }
            }

            Summarise("Gangnam overall", all);
            Preset();

            // Myeongdong (second district) through the destination travel and its loading screen.
            MissionManager.Instance.AbandonMission();
            GameManager.Instance.GetService<Contract.Regions.IRegionService>()?.UnlockRegion("region_industrial");
            loadStart = Time.realtimeSinceStartup;
            Contract.Regions.DestinationManager.Instance.TravelTo("region_industrial");
            yield return WaitForScene(GameConstants.Scenes.Myeongdong, 30f);
            Line($"Myeongdong load: {Time.realtimeSinceStartup - loadStart:F2} s");
            yield return new WaitForSecondsRealtime(2f);
            player = GameObject.FindGameObjectWithTag("Player");
            Check(player != null, "player present in Myeongdong");
            var myeongdong = new List<float>();
            for (int i = 0; player != null && i < MyeongdongTour.Length; i++)
            {
                Teleport(player, MyeongdongTour[i]);
                yield return Measure($"Myeongdong stop {i + 1}", 4f, myeongdong);
            }

            Summarise("Myeongdong overall", myeongdong);
            Preset();
            Line($"Memory: managed {GC.GetTotalMemory(false) / 1048576f:F0} MB, Unity allocated {UnityEngine.Profiling.Profiler.GetTotalAllocatedMemoryLong() / 1048576f:F0} MB, " +
                 $"reserved {UnityEngine.Profiling.Profiler.GetTotalReservedMemoryLong() / 1048576f:F0} MB");
            Line(failed ? "RESULT: FAILED" : "RESULT: OK");

            string path = Path.Combine(Application.persistentDataPath, "benchmark.txt");
            File.WriteAllText(path, report.ToString());
            Debug.Log($"[Benchmark] Written to {path}");
            yield return null;
            Application.Quit(failed ? 1 : 0);
        }

        private void Line(string text)
        {
            report.AppendLine(text);
            Debug.Log("[Benchmark] " + text);
        }

        private void Check(bool ok, string what)
        {
            if (ok) return;
            failed = true;
            Line("CHECK FAILED: " + what);
        }

        private IEnumerator WaitForScene(string scene, float timeout)
        {
            float end = Time.realtimeSinceStartup + timeout;
            while (SceneManager.GetActiveScene().name != scene || (GameManager.Instance != null && GameManager.Instance.SceneLoader.IsLoading))
            {
                if (Time.realtimeSinceStartup > end)
                {
                    Check(false, $"scene {scene} reached");
                    yield break;
                }

                yield return null;
            }

            yield return new WaitForSecondsRealtime(0.5f);
        }

        /// <summary>V-Sync off and no frame cap (phones: their display rate) at the requested tier, through the settings so scene loads keep it.</summary>
        private void ApplyBenchmarkSettings()
        {
            GameSettings settings = GameManager.Instance?.Session?.Settings;
            if (settings == null) return;
            if (quality >= 0) settings.graphicsQuality = Mathf.Clamp(quality, 0, QualitySettings.names.Length - 1);
            settings.vSync = false;
            settings.targetFramerate = 0;
            GameEvents.RaiseSettingsApplied();
        }

        private void Preset()
        {
            var pipeline = QualitySettings.renderPipeline;
            Line($"Preset: {QualitySettings.names[QualitySettings.GetQualityLevel()]}, pipeline {(pipeline != null ? pipeline.name : "default")}, LOD bias {QualitySettings.lodBias:F2}, " +
                 $"render {Screen.width}x{Screen.height}, target {Application.targetFrameRate} fps, crowd {Contract.NPCs.NpcManager.Instance?.CivilianCount}");
        }

        private static void Teleport(GameObject player, Vector3 near)
        {
            // Nearest walkable spot that is not inside a parked car / prop (the camera needs room behind).
            NavMeshHit hit = default;
            bool found = false;
            for (int ring = 0; ring < 24 && !found; ring++)
            {
                Vector3 probe = near + Quaternion.Euler(0f, ring * 47f, 0f) * Vector3.forward * (ring == 0 ? 0f : 1.5f + ring * 0.5f);
                if (!NavMesh.SamplePosition(probe, out hit, 6f, NavMesh.AllAreas)) continue;
                int mask = ~(1 << player.layer) & ~(1 << 2);
                found = !Physics.CheckCapsule(hit.position + Vector3.up * 0.5f, hit.position + Vector3.up * 1.7f, 0.6f, mask, QueryTriggerInteraction.Ignore);
            }

            if (!found) return;
            var controller = player.GetComponent<CharacterController>();
            if (controller != null) controller.enabled = false;
            Vector3 look = new Vector3(0f, hit.position.y, 0f) - hit.position;
            player.transform.SetPositionAndRotation(hit.position + Vector3.up * 0.1f, look.sqrMagnitude > 1f ? Quaternion.LookRotation(look) : Quaternion.identity);
            if (controller != null) controller.enabled = true;
            Physics.SyncTransforms();
        }

        private IEnumerator Measure(string label, float seconds, List<float> collect = null)
        {
            for (int i = 0; i < 20; i++) yield return null; // settle after a change
            var frames = new List<float>();
            int gc = GC.CollectionCount(0);
            float end = Time.realtimeSinceStartup + seconds;
            while (Time.realtimeSinceStartup < end)
            {
                yield return null;
                frames.Add(Time.unscaledDeltaTime * 1000f);
            }

            collect?.AddRange(frames);
            Summarise(label, frames, GC.CollectionCount(0) - gc);
        }

        private void Summarise(string label, List<float> frames, int gcs = -1)
        {
            if (frames.Count == 0) return;
            var sorted = frames.OrderBy(f => f).ToList();
            float P(float q) => sorted[Mathf.Clamp((int)(sorted.Count * q), 0, sorted.Count - 1)];
            Line($"{label}: {sorted.Count} frames, avg {frames.Average():F1} ms ({1000f / frames.Average():F0} fps), median {P(0.5f):F1}, " +
                 $"p95 {P(0.95f):F1}, p99 {P(0.99f):F1}, max {sorted[sorted.Count - 1]:F1} ms, frames >50 ms: {frames.Count(f => f > 50f)}" +
                 (gcs >= 0 ? $", GC {gcs}" : ""));
        }
    }
}
