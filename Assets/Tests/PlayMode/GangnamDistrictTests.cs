using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Contract.Core;
using Contract.Interaction;
using Contract.Player;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Contract.Tests
{
    /// <summary>
    /// Stage 3 verification: the Gangnam district is complete, walkable, keeps every gameplay
    /// object reachable, and its Hangul signage renders. Movement is driven by a virtual gamepad
    /// through the real input pipeline (see PlayerControllerTests for why).
    /// </summary>
    public sealed class GangnamDistrictTests
    {
        private const string DistrictRoot = "District_Gangnam";
        private static readonly Rect Walkable = Rect.MinMaxRect(-85f, -70f, 85f, 95f);
        private static readonly Rect SubwayPit = Rect.MinMaxRect(24f, 9f, 31f, 12f);
        private static readonly Rect ParkingPit = Rect.MinMaxRect(-44.5f, -47f, -38.5f, -30f);

        private Gamepad gamepad;
        private InputSettings.BackgroundBehavior savedBackground;
        private ThirdPersonController player;

        [SetUp]
        public void Setup()
        {
            savedBackground = InputSystem.settings.backgroundBehavior;
            InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            gamepad = InputSystem.AddDevice<Gamepad>("DistrictTestGamepad");
        }

        [TearDown]
        public void TearDown()
        {
            if (gamepad != null) InputSystem.RemoveDevice(gamepad);
            InputSystem.settings.backgroundBehavior = savedBackground;
        }

        private IEnumerator LoadWorld()
        {
            GameManager.Instance.SetPaused(false);
            GameManager.Instance.Session.selectedCharacterId = "char_alex_morgan";
            SceneManager.LoadScene(GameConstants.Scenes.GameWorld, LoadSceneMode.Single);
            for (int i = 0; i < 5; i++) yield return null;
            Contract.NPCs.NpcManager.Instance?.DespawnAllCivilians(); // walk tests need a clear pavement
            player = Object.FindAnyObjectByType<ThirdPersonController>();
            Assert.IsNotNull(player);
        }

        private void Stick(Vector2 value) => InputSystem.QueueStateEvent(gamepad, new GamepadState { leftStick = value });

        private void Teleport(Vector3 position)
        {
            player.transform.position = position;
            Physics.SyncTransforms();
        }

        private static Transform District() => GameObject.Find(DistrictRoot)?.transform;

        // -------------------------------------------------------------------------------------

        [UnityTest]
        public IEnumerator District_ExistsWithRenderableHangulSignage()
        {
            yield return LoadWorld();
            Transform district = District();
            Assert.IsNotNull(district, "District_Gangnam missing from GameWorld");
            Assert.GreaterOrEqual(district.Find("Buildings").childCount + district.Find("FoodAlley").childCount, 25, "Too few buildings");

            TextMeshPro[] texts = district.GetComponentsInChildren<TextMeshPro>(true);
            TextMeshPro[] hangul = texts.Where(t => Regex.IsMatch(t.text, "[가-힣]")).ToArray();
            Assert.GreaterOrEqual(hangul.Length, 150, "Not enough Korean signage");

            var missing = new HashSet<char>();
            foreach (TextMeshPro t in texts)
            {
                string glyphs = Regex.Replace(t.text, @"\s", "");
                if (!t.font.HasCharacters(glyphs, out uint[] absent, true, true))
                {
                    foreach (uint c in absent) missing.Add((char)c);
                }
            }

            Assert.IsEmpty(missing, $"Glyphs that cannot render: {new string(missing.ToArray())}");
        }

        [UnityTest]
        public IEnumerator Ground_HasNoHolesAcrossTheWalkableArea()
        {
            yield return LoadWorld();
            var failures = new List<string>();
            for (float x = Walkable.xMin + 0.5f; x < Walkable.xMax; x += 2.5f)
            {
                for (float z = Walkable.yMin + 0.5f; z < Walkable.yMax; z += 2.5f)
                {
                    bool pit = SubwayPit.Contains(new Vector2(x, z)) || ParkingPit.Contains(new Vector2(x, z));
                    if (!Physics.Raycast(new Vector3(x, 150f, z), Vector3.down, out RaycastHit hit, 300f, ~0, QueryTriggerInteraction.Ignore))
                    {
                        failures.Add($"void at ({x},{z})");
                    }
                    else if (hit.point.y < (pit ? -3.6f : -0.05f))
                    {
                        failures.Add($"too low at ({x},{z}): {hit.point.y:F2}");
                    }
                }
            }

            Assert.IsEmpty(failures, string.Join("; ", failures.Take(15)));
        }

        [UnityTest]
        public IEnumerator GameplayObjects_AreNotBlockedByTheDistrict()
        {
            yield return LoadWorld();
            Transform district = District();
            var anchors = new Dictionary<string, Vector3>
            {
                ["PlayerSpawn"] = player.transform.position,
                ["SafehouseDoor"] = GameObject.Find("SafehouseDoor").transform.position,
                ["SafehouseTerminal"] = GameObject.Find("SafehouseTerminal").transform.position,
                ["KeycardPickup"] = GameObject.Find("KeycardPickup").transform.position,
                ["Extraction"] = Object.FindAnyObjectByType<ExtractionInteractable>().transform.position,
            };

            foreach (var anchor in anchors)
            {
                Vector3 p = anchor.Value;
                Collider[] blocking = Physics.OverlapCapsule(p + Vector3.up * 0.4f, p + Vector3.up * 1.6f, 0.3f, ~0, QueryTriggerInteraction.Ignore)
                    .Where(c => c.transform.IsChildOf(district)).ToArray();
                Assert.IsEmpty(blocking, $"{anchor.Key} is inside district geometry: {string.Join(", ", blocking.Select(c => c.name))}");
                Assert.IsTrue(Physics.Raycast(p + Vector3.up, Vector3.down, out RaycastHit ground, 3f, ~0, QueryTriggerInteraction.Ignore),
                    $"{anchor.Key} has no ground beneath it");
            }
        }

        [UnityTest]
        public IEnumerator Player_WalksTheBoulevardPavement()
        {
            yield return LoadWorld();
            Vector3 start = player.transform.position;
            Stick(Vector2.up);
            yield return new WaitForSeconds(5f);
            Stick(Vector2.zero);

            float travelled = player.transform.position.z - start.z;
            Assert.Greater(travelled, 15f, $"Blocked after {travelled:F1} m walking north from the spawn");
        }

        [UnityTest]
        public IEnumerator Player_CanWalkDownTheParkingRampToTheExtraction()
        {
            yield return LoadWorld();
            Teleport(new Vector3(-41.5f, 0.3f, -26f));
            yield return null;

            Stick(Vector2.down); // camera faces north, so "down" walks south into the ramp
            yield return new WaitForSeconds(5f);
            Stick(Vector2.zero);

            Vector3 p = player.transform.position;
            Assert.Less(p.y, -2.8f, $"Did not reach the basement level (y={p.y:F2}, z={p.z:F1})");
            Vector3 extraction = Object.FindAnyObjectByType<ExtractionInteractable>().transform.position;
            Assert.Less(Vector3.Distance(new Vector3(p.x, 0f, p.z), new Vector3(extraction.x, 0f, extraction.z)), 5f, "Extraction not reachable at the ramp bottom");
        }

        [UnityTest]
        public IEnumerator Player_CanWalkDownTheSubwayStairs()
        {
            yield return LoadWorld();
            Teleport(new Vector3(22f, 0.3f, 10.5f));
            yield return null;

            Stick(Vector2.right); // east, down the stairs
            yield return new WaitForSeconds(3.5f);
            Stick(Vector2.zero);

            Assert.Less(player.transform.position.y, -2.5f, $"Stairs not walkable (y={player.transform.position.y:F2})");
        }

        [UnityTest]
        public IEnumerator WorldBounds_KeepThePlayerInside()
        {
            yield return LoadWorld();
            Teleport(new Vector3(82f, 0.3f, -66f));
            yield return null;

            Stick(new Vector2(1f, -1f).normalized); // push into the south-east corner
            yield return new WaitForSeconds(2f);
            Stick(Vector2.zero);

            Vector3 p = player.transform.position;
            Assert.IsTrue(Walkable.Contains(new Vector2(p.x, p.z)), $"Player escaped the district: {p}");
        }

        [UnityTest]
        public IEnumerator Performance_FrameTimeAt1080p()
        {
            yield return LoadWorld();
            Camera cam = Camera.main;
            RenderTexture target = new RenderTexture(1920, 1080, 24);
            cam.targetTexture = target;
            int previousRate = Application.targetFrameRate;
            Application.targetFrameRate = -1;
            QualitySettings.vSyncCount = 0;

            for (int i = 0; i < 60; i++) yield return null; // warm-up (probe, glyphs, shaders)

            using var batches = Unity.Profiling.ProfilerRecorder.StartNew(Unity.Profiling.ProfilerCategory.Render, "Batches Count");
            using var setPass = Unity.Profiling.ProfilerRecorder.StartNew(Unity.Profiling.ProfilerCategory.Render, "SetPass Calls Count");
            using var triangles = Unity.Profiling.ProfilerRecorder.StartNew(Unity.Profiling.ProfilerCategory.Render, "Triangles Count");

            float total = 0f, worst = 0f;
            const int frames = 240;
            for (int i = 0; i < frames; i++)
            {
                yield return null;
                total += Time.unscaledDeltaTime;
                worst = Mathf.Max(worst, Time.unscaledDeltaTime);
            }

            float average = total / frames * 1000f;
            Debug.Log($"[Perf] GameWorld 1080p: avg {average:F1} ms ({1000f / average:F0} fps), worst {worst * 1000f:F1} ms, " +
                      $"batches {batches.LastValue}, setpass {setPass.LastValue}, tris {triangles.LastValue}");
            cam.targetTexture = null;
            Object.Destroy(target);
            Application.targetFrameRate = previousRate;

            Assert.Less(average, 50f, "Catastrophic frame time (this machine / integrated GPU)");
        }
    }
}
