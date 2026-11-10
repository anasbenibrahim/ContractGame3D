using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Contract.Core;
using Contract.Data;
using Contract.Interaction;
using Contract.Missions;
using Contract.NPCs;
using Contract.Player;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Contract.Tests
{
    /// <summary>
    /// Stage 4 verification: NavMesh rules, the civilian crowd, guards (non-lethal), mission
    /// characters and every mission interaction's objective signal - all in the real GameWorld,
    /// driven through the real input pipeline with a virtual gamepad (Interact = north button).
    /// </summary>
    public sealed class NpcAndInteractionTests
    {
        private Gamepad gamepad;
        private InputSettings.BackgroundBehavior savedBackground;
        private ThirdPersonController player;
        private readonly List<(ObjectiveType type, string id)> signals = new List<(ObjectiveType, string)>();
        private readonly List<string> alerts = new List<string>();
        private readonly List<string> spoken = new List<string>();

        [SetUp]
        public void Setup()
        {
            savedBackground = InputSystem.settings.backgroundBehavior;
            InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            gamepad = InputSystem.AddDevice<Gamepad>("NpcTestGamepad");
            signals.Clear();
            alerts.Clear();
            spoken.Clear();
        }

        [TearDown]
        public void TearDown()
        {
            GameEvents.ObjectiveSignal -= OnSignal;
            GameEvents.NpcAlerted -= OnAlert;
            GameEvents.NpcSpoke -= OnSpoke;
            if (gamepad != null) InputSystem.RemoveDevice(gamepad);
            InputSystem.settings.backgroundBehavior = savedBackground;
        }

        private void OnSignal(ObjectiveType type, string id) => signals.Add((type, id));
        private void OnAlert(string id) => alerts.Add(id);
        private void OnSpoke(string id, string ko, string en) => spoken.Add(ko);

        private IEnumerator LoadWorld(bool crowd = true)
        {
            GameManager.Instance.SetPaused(false);
            GameManager.Instance.Session.selectedCharacterId = "char_alex_morgan";
            SceneManager.LoadScene(GameConstants.Scenes.GameWorld, LoadSceneMode.Single);
            for (int i = 0; i < 5; i++) yield return null;
            if (!crowd) NpcManager.Instance.DespawnAllCivilians();
            player = Object.FindAnyObjectByType<ThirdPersonController>();
            GameEvents.ObjectiveSignal += OnSignal;
            GameEvents.NpcAlerted += OnAlert;
            GameEvents.NpcSpoke += OnSpoke;
        }

        private void Teleport(Vector3 position, Vector3 lookAt)
        {
            Vector3 dir = lookAt - position;
            dir.y = 0f;
            player.transform.SetPositionAndRotation(position, Quaternion.LookRotation(dir));
            Physics.SyncTransforms();
        }

        private IEnumerator Press(float hold = 0f)
        {
            InputSystem.QueueStateEvent(gamepad, new GamepadState().WithButton(GamepadButton.North, true));
            yield return null;
            if (hold > 0f) yield return new WaitForSeconds(hold);
            InputSystem.QueueStateEvent(gamepad, new GamepadState());
            yield return null;
            yield return null;
        }

        private IEnumerator InteractAt(Vector3 standAt, Vector3 target, float hold = 0f)
        {
            Teleport(standAt, target);
            yield return null;
            yield return null;
            yield return Press(hold);
        }

        private static Transform Find(string path) => GameObject.Find(path)?.transform;

        private static bool OnRoad(Vector3 p)
        {
            Rect[] walkable =
            {
                Rect.MinMaxRect(8f, 7f, 90f, 100f), Rect.MinMaxRect(-90f, 7f, -8f, 100f), Rect.MinMaxRect(8f, -75f, 90f, -7f), Rect.MinMaxRect(-90f, -75f, -8f, -7f),
                Rect.MinMaxRect(-8f, 8.5f, 8f, 12.5f), Rect.MinMaxRect(-8f, -12.5f, 8f, -8.5f), Rect.MinMaxRect(8.5f, -7f, 12.5f, 7f), Rect.MinMaxRect(-12.5f, -7f, -8.5f, 7f)
            };
            Vector2 xz = new Vector2(p.x, p.z);
            return !walkable.Any(r => r.Contains(xz));
        }

        // ---- NavMesh ---------------------------------------------------------------------

        [UnityTest]
        public IEnumerator NavMesh_CoversPavementsInteriorsStairsAndRamp_ButNotRoads()
        {
            yield return LoadWorld(crowd: false);
            Transform mirae = Find("District_Gangnam/Buildings/MiraeTower");
            Transform hangyeol = Find("District_Gangnam/Buildings/HangyeolGroup");

            var walkable = new Dictionary<string, Vector3>
            {
                ["W pavement"] = new Vector3(-11f, 0.15f, -30f), ["E pavement"] = new Vector3(11f, 0.15f, 40f), ["food alley"] = new Vector3(40f, 0.15f, 42f),
                ["zebra N"] = new Vector3(0f, 0f, 10.5f), ["zebra E"] = new Vector3(10.5f, 0f, 0f),
                ["Mirae lobby"] = mirae.TransformPoint(new Vector3(0f, 0f, 6f)), ["Mirae office"] = mirae.TransformPoint(new Vector3(0f, 0f, -9.5f)),
                ["server room"] = hangyeol.TransformPoint(new Vector3(0f, 0f, -2.3f)),
                ["subway bottom"] = new Vector3(30.3f, -3.1f, 10.5f), ["parking bottom"] = new Vector3(-41.5f, -3.2f, -45.8f)
            };
            foreach (var p in walkable)
                Assert.IsTrue(NavMesh.SamplePosition(p.Value, out _, 0.6f, NavMesh.AllAreas), $"No NavMesh at {p.Key}");

            foreach (Vector3 road in new[] { new Vector3(0f, 0f, -30f), new Vector3(-4f, 0f, 40f), new Vector3(40f, 0f, 0f), new Vector3(0f, 0f, 0f) })
                Assert.IsFalse(NavMesh.SamplePosition(road, out _, 0.4f, NavMesh.AllAreas), $"Road at {road} should not be walkable");

            // Crossing the boulevard must route over a zebra crossing.
            NavMeshPath path = new NavMeshPath();
            Assert.IsTrue(NavMesh.CalculatePath(new Vector3(-11f, 0.15f, -30f), new Vector3(11f, 0.15f, -30f), NavMesh.AllAreas, path));
            Assert.AreEqual(NavMeshPathStatus.PathComplete, path.status);
            for (int i = 1; i < path.corners.Length; i++)
            {
                Vector3 mid = (path.corners[i] + path.corners[i - 1]) / 2f;
                Assert.IsFalse(OnRoad(mid), $"Path segment crosses the road outside a crossing at {mid}");
            }
        }

        // ---- Crowd ---------------------------------------------------------------------------

        [UnityTest]
        public IEnumerator Crowd_SpawnsVariedNpcsThatWalkOnPavements()
        {
            yield return LoadWorld();
            yield return new WaitForSeconds(1f);
            IReadOnlyList<NpcController> crowd = NpcManager.Instance.Civilians;
            Assert.AreEqual(NpcManager.Instance.MaxCivilians, crowd.Count, "Crowd not filled");
            Assert.GreaterOrEqual(crowd.Select(c => c.Archetype).Distinct().Count(), 3, "Crowd lacks variety");

            foreach (NpcController npc in crowd)
            {
                Assert.IsTrue(npc.GetComponent<NavMeshAgent>().isOnNavMesh, $"{npc.name} not on the NavMesh");
                PlaceholderRig rig = npc.GetComponentInChildren<PlaceholderRig>(true); // far NPCs are culled (inactive)
                Assert.IsNotNull(rig, $"{npc.name} has no body");
                Assert.LessOrEqual(npc.GetComponentsInChildren<MeshRenderer>(true).Length, 20, $"{npc.name} body not merged for the crowd");
            }

            Dictionary<NpcController, Vector3> start = crowd.ToDictionary(n => n, n => n.transform.position);
            int roadViolations = 0;
            for (float t = 0f; t < 10f; t += 0.5f)
            {
                yield return new WaitForSeconds(0.5f);
                roadViolations += crowd.Count(n => n.transform.position.y < 0.1f && OnRoad(n.transform.position) && n.transform.position.y > -1f);
            }

            float[] moved = crowd.Select(n => Vector3.Distance(start[n], n.transform.position)).OrderBy(d => d).ToArray();
            Assert.Greater(moved[moved.Length / 2], 3f, "Median civilian barely moved in 10 s");
            Assert.AreEqual(0, roadViolations, "Civilians walked on the road outside crossings");
        }

        [UnityTest]
        public IEnumerator Talk_StopsTheCivilianAndShowsAKoreanLine()
        {
            yield return LoadWorld();
            NpcController npc = NpcManager.Instance.Civilians[0];
            foreach (NpcController other in NpcManager.Instance.Civilians.Skip(1).ToList()) NpcManager.Instance.Despawn(other);
            npc.GetComponent<NavMeshAgent>().isStopped = true;
            yield return null;

            Vector3 front = npc.transform.position + npc.transform.forward * 1.3f;
            yield return InteractAt(front, npc.transform.position);

            Assert.AreEqual(NpcState.Talking, npc.CurrentState);
            Assert.IsTrue(npc.Speech.IsSpeaking, "No speech bubble");
            Assert.IsTrue(Regex.IsMatch(npc.Speech.LastLine, "[가-힣]"), $"Line is not Korean: {npc.Speech.LastLine}");
            Assert.IsNotEmpty(spoken);
            Assert.AreEqual(RigGesture.Talk, player.GetComponentInChildren<PlaceholderRig>().CurrentGesture, "Player did not gesture");
        }

        // ---- Guards --------------------------------------------------------------------------

        [UnityTest]
        public IEnumerator Guard_IgnoresVisitorsInThePublicLobby()
        {
            yield return LoadWorld(crowd: false);
            NpcController guard = Find("Gangnam_Population/Security/guard_mirae_01").GetComponent<NpcController>();
            Teleport(guard.transform.position + guard.transform.forward * 3f, guard.transform.position);
            yield return new WaitForSeconds(3f);
            Assert.AreEqual(0f, guard.Suspicion, 0.001f, "Guard became suspicious of a visitor in a public area");
        }

        [UnityTest]
        public IEnumerator Guard_WarnsTrespasserInStaffOffice_ThenStandsDown()
        {
            yield return LoadWorld(crowd: false);
            Transform mirae = Find("District_Gangnam/Buildings/MiraeTower");
            NpcController guard = Find("Gangnam_Population/Security/guard_mirae_02").GetComponent<NpcController>();
            // Send the guard to its office post and wait until it stands watch there.
            Transform post = Find("Gangnam_Population/PointsOfInterest/POI_Post_MiraeOffice");
            guard.Configure("guard_mirae_02", NpcType.Guard, NpcArchetype.Security, null, null, "mirae", new[] { post });
            for (float t = 0f; t < 12f && guard.CurrentState != NpcState.Dwelling; t += Time.deltaTime) yield return null;
            Assert.AreEqual(NpcState.Dwelling, guard.CurrentState, "Guard never reached its post");
            yield return new WaitForSeconds(0.5f); // turn to face the watched direction

            // Step into the staff office in front of the guard (it watches along the post's forward).
            Teleport(guard.transform.position + post.forward * 4f + Vector3.up * 0.05f, guard.transform.position);
            for (float t = 0f; t < 8f && alerts.Count == 0; t += Time.deltaTime) yield return null;
            Assert.Contains("guard_mirae_02", alerts, "Guard never noticed the trespasser");
            Assert.AreEqual(NpcState.Alert, guard.CurrentState);
            Assert.IsTrue(Regex.IsMatch(guard.Speech.LastLine, "[가-힣]"), "Guard gave no warning");

            Teleport(new Vector3(11.5f, 0.2f, 25f), new Vector3(11.5f, 0.2f, 30f)); // back out on the street
            for (float t = 0f; t < 15f && guard.CurrentState == NpcState.Alert; t += Time.deltaTime) yield return null;
            Assert.AreNotEqual(NpcState.Alert, guard.CurrentState, "Guard never stood down");
            Assert.IsTrue(player.isActiveAndEnabled, "Player was harmed / disabled");
        }

        // ---- Mission interactions ------------------------------------------------------------

        [UnityTest]
        public IEnumerator MissionInteractables_EmitTheirObjectiveSignals()
        {
            yield return LoadWorld(crowd: false);
            Transform mirae = Find("District_Gangnam/Buildings/MiraeTower");
            Transform hangyeol = Find("District_Gangnam/Buildings/HangyeolGroup");

            GameManager.Instance.Session.Reset();
            MissionManager.Instance.StartMission("mission_downtown_01"); // mission objects only work during their contract
            // Reach the office (location trigger) by walking in through the front door area.
            Teleport(mirae.TransformPoint(new Vector3(0f, 0.05f, 8f)), mirae.position);
            yield return new WaitForSeconds(0.3f);
            CollectionAssert.Contains(signals, (ObjectiveType.ReachLocation, "office_building"));

            Transform terminal = Find("Gangnam_MissionContent/Terminal_secure_terminal");
            yield return InteractAt(mirae.TransformPoint(new Vector3(3f, 0.05f, -7.3f)), terminal.position, hold: 3.4f);
            CollectionAssert.Contains(signals, (ObjectiveType.Interact, "secure_terminal"), "Terminal hold did not complete");
            Assert.IsTrue(terminal.GetComponent<TerminalInteractable>().Used);

            Transform docs = Find("Gangnam_MissionContent/Pickup_documents_evidence");
            yield return InteractAt(mirae.TransformPoint(new Vector3(-7f, 0.05f, -7.1f)), docs.position);
            CollectionAssert.Contains(signals, (ObjectiveType.CollectItem, "documents_evidence"));

            // Server room: location + 4 s plant.
            MissionManager.Instance.StartMission("mission_downtown_03");
            Teleport(hangyeol.TransformPoint(new Vector3(3.5f, 0.05f, -2.2f)), hangyeol.TransformPoint(new Vector3(0f, 0f, -3.5f)));
            yield return new WaitForSeconds(0.3f);
            CollectionAssert.Contains(signals, (ObjectiveType.ReachLocation, "server_room"));
            Transform rack = Find("Gangnam_MissionContent/Terminal_server_rack");
            yield return InteractAt(hangyeol.TransformPoint(new Vector3(0f, 0.05f, -2.4f)), rack.position, hold: 4.4f);
            CollectionAssert.Contains(signals, (ObjectiveType.Interact, "server_rack"));

            signals.Clear();

            MissionManager.Instance.StartMission("mission_downtown_02");
            Transform drive = Find("Gangnam_MissionContent/Pickup_data_drive");
            yield return InteractAt(new Vector3(-12f, 0.2f, 60f), drive.position);
            CollectionAssert.Contains(signals, (ObjectiveType.CollectItem, "data_drive"));

            // Extraction (needs the safehouse keycard).
            GameEvents.RaiseItemCollected("keycard");
            GameManager.Instance.Session.activeMissionId = "mission_downtown_01";
            ExtractionInteractable extraction = Object.FindAnyObjectByType<ExtractionInteractable>();
            yield return InteractAt(extraction.transform.position + new Vector3(0f, 0.05f, 1.2f), extraction.transform.position);
            CollectionAssert.Contains(signals, (ObjectiveType.Extract, "extraction_downtown"));
            Assert.AreEqual("mission_downtown_01", GameManager.Instance.Session.activeMissionId,
                "Extraction must not abandon the active mission (audit bug)");
            MissionManager.Instance.AbandonMission();
        }

        [UnityTest]
        public IEnumerator HoldInteraction_ReleasedEarly_DoesNotComplete()
        {
            yield return LoadWorld(crowd: false);
            GameManager.Instance.Session.Reset();
            MissionManager.Instance.StartMission("mission_downtown_01");
            Transform mirae = Find("District_Gangnam/Buildings/MiraeTower");
            Transform terminal = Find("Gangnam_MissionContent/Terminal_secure_terminal");
            yield return InteractAt(mirae.TransformPoint(new Vector3(3f, 0.05f, -7.3f)), terminal.position, hold: 1f);
            yield return new WaitForSeconds(3f);
            Assert.IsFalse(terminal.GetComponent<TerminalInteractable>().Used, "Terminal completed without a full hold");
            CollectionAssert.DoesNotContain(signals, (ObjectiveType.Interact, "secure_terminal"));
        }

        [UnityTest]
        public IEnumerator MissionCharacters_HaveIdentityStatusRewardAndCanBeIdentified()
        {
            yield return LoadWorld(crowd: false);
            GameManager.Instance.Session.Reset();
            MissionSubject kim = Find("Gangnam_Population/MissionCharacters/subject_ceo_kim").GetComponent<MissionSubject>();
            Assert.AreEqual("subject_ceo_kim", kim.SubjectId);
            Assert.AreEqual("region_downtown", kim.RegionId);
            Assert.AreEqual("mission_downtown_01", kim.Mission.missionId);
            Assert.AreEqual(15000, kim.Reward);
            Assert.AreEqual(MissionState.NotStarted, kim.Status);
            StringAssert.Contains("Kim Min-jun", kim.DisplayName);
            Assert.AreEqual(SubjectAction.Talk, kim.CurrentAction, "Objective actions must wait for the contract");

            MissionManager.Instance.StartMission("mission_downtown_01");
            Assert.AreEqual(MissionState.InProgress, kim.Status);

            kim.GetComponent<NavMeshAgent>().isStopped = true;
            yield return InteractAt(kim.transform.position + kim.transform.forward * 1.3f, kim.transform.position);
            Assert.IsTrue(kim.Identified);
            CollectionAssert.Contains(signals, (ObjectiveType.IdentifySubject, "ceo_kim"));
        }

        [UnityTest]
        public IEnumerator Courier_CanOnlyBePhotographedDuringTheHandoff()
        {
            yield return LoadWorld(crowd: false);
            GameManager.Instance.Session.Reset();
            MissionManager.Instance.StartMission("mission_downtown_02");
            MissionSubject park = Find("Gangnam_Population/MissionCharacters/subject_courier_park").GetComponent<MissionSubject>();
            NavMeshAgent agent = park.GetComponent<NavMeshAgent>();

            // Away from the contact: identify works, photograph is not offered.
            agent.Warp(new Vector3(-11.2f, 0.15f, 62f));
            agent.isStopped = true;
            yield return InteractAt(park.transform.position + Vector3.right * 1.3f, park.transform.position);
            Assert.IsTrue(park.Identified);
            Assert.IsFalse(park.InExchange);
            Assert.AreNotEqual(SubjectAction.Photograph, park.CurrentAction);

            // At the handoff with the contact: photograph the exchange.
            agent.Warp(new Vector3(-10.6f, 0.15f, 35.2f));
            agent.isStopped = true;
            yield return null;
            Assert.IsTrue(park.InExchange);
            Assert.AreEqual(SubjectAction.Photograph, park.CurrentAction);
            yield return InteractAt(new Vector3(-9.3f, 0.2f, 34.6f), park.transform.position);
            CollectionAssert.Contains(signals, (ObjectiveType.PhotographSubject, "courier_park"));
            Assert.AreEqual(RigGesture.Photo, player.GetComponentInChildren<PlaceholderRig>().CurrentGesture);
        }

        [UnityTest]
        public IEnumerator Escort_SubjectFollowsThePlayerToTheExtraction()
        {
            yield return LoadWorld(crowd: false);
            GameManager.Instance.Session.Reset();
            MissionManager.Instance.StartMission("mission_downtown_01");
            MissionSubject kim = Find("Gangnam_Population/MissionCharacters/subject_ceo_kim").GetComponent<MissionSubject>();
            kim.Configure("subject_ceo_kim", "ceo_kim", "김민준", "Kim Min-jun", "Escort test", "region_downtown",
                false, false, true, null, "...", "...");
            NavMeshAgent agent = kim.GetComponent<NavMeshAgent>();
            agent.Warp(new Vector3(-41.5f, 0.15f, -24f)); // near the parking ramp head
            yield return null;

            yield return InteractAt(new Vector3(-41.5f, 0.2f, -22.7f), kim.transform.position);
            Assert.IsTrue(kim.Npc.IsFollowing, "Subject did not start following");

            Teleport(new Vector3(-41.5f, -3.1f, -42f), new Vector3(-41.5f, -3.1f, -45f)); // player waits at the extraction
            for (float t = 0f; t < 20f && !kim.EscortDelivered; t += Time.deltaTime) yield return null;
            Assert.IsTrue(kim.EscortDelivered, $"Subject never reached the extraction (at {kim.transform.position})");
            CollectionAssert.Contains(signals, (ObjectiveType.EscortSubject, "ceo_kim"));
        }

        [UnityTest]
        public IEnumerator Performance_FrameTimeWithCrowdAt1080p()
        {
            yield return LoadWorld();
            Camera cam = Camera.main;
            RenderTexture target = new RenderTexture(1920, 1080, 24);
            cam.targetTexture = target;
            Application.targetFrameRate = -1;
            QualitySettings.vSyncCount = 0;
            for (int i = 0; i < 60; i++) yield return null;

            float total = 0f, worst = 0f;
            for (int i = 0; i < 240; i++)
            {
                yield return null;
                total += Time.unscaledDeltaTime;
                worst = Mathf.Max(worst, Time.unscaledDeltaTime);
            }

            float average = total / 240f * 1000f;
            Debug.Log($"[Perf] GameWorld + {NpcManager.Instance.Civilians.Count} civilians, 1080p: avg {average:F1} ms ({1000f / average:F0} fps), worst {worst * 1000f:F1} ms");
            cam.targetTexture = null;
            Object.Destroy(target);
            Assert.Less(average, 50f);
        }
    }
}
