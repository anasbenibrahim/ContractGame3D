using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Contract.Activity;
using Contract.Core;
using Contract.Data;
using Contract.Missions;
using Contract.Regions;
using Contract.SaveSystem;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Contract.Tests
{
    /// <summary>
    /// GPS Phase 5: activity missions in the mission system, checkpoints (marked by the player,
    /// walk away, come back), exploration of ~100 m areas stored as salted hashes, collectibles,
    /// and no reward ever paid twice - including after saving and loading.
    /// </summary>
    public sealed class GpsMissionTests
    {
        private static MissionManager Missions => MissionManager.Instance;
        private static ActivityController Activity => ActivityController.Instance;
        private static LocationTracker Tracker => LocationTracker.Instance;
        private static GameSession Session => GameManager.Instance.Session;

        [SetUp]
        public void Setup()
        {
            Session.Reset();
            ActivityWorld.ResetDebounce();
        }

        [TearDown]
        public void TearDown()
        {
            Activity.Stop();
            Tracker.ReleaseAll();
            Tracker.UseSource(null);
            Session.Reset();
            SaveManager.Instance.DeleteSlot(GameConstants.Save.DefaultSlot);
        }

        private static void Walked(double meters, long steps = 0)
        {
            Session.gpsTotalRealMeters = meters;
            Session.gpsTotalSteps = steps;
            ActivityRewards.Evaluate(Session);       // what the controller does after counting
            ActivityController.RaiseProgressed();
        }

        // ---- missions ------------------------------------------------------------------------------

        [Test]
        public void SixGpsMissions_LiveInTheMissionSystem_ButNotOnTheContractBoard()
        {
            MissionData[] activity = Missions.ActivityMissions.ToArray();
            CollectionAssert.AreEquivalent(new[] { "gps_01_first_steps", "gps_02_explorer", "gps_03_long_journey", "gps_04_step_challenge", "gps_05_checkpoint", "gps_06_new_discovery" },
                activity.Select(m => m.missionId));
            Assert.IsTrue(activity.All(m => m.objectives.Length > 0 && !string.IsNullOrEmpty(m.koreanName)));
            Assert.IsFalse(Missions.CanStart("gps_01_first_steps", out string reason), "GPS missions are not started from the board");
            StringAssert.Contains("walking", reason);
            Assert.IsFalse(GameManager.Instance.GetService<IRegionService>().GetAllRegions().Any(r => r.missionIds.Any(id => id.StartsWith("gps_"))));
            Assert.AreEqual(8, ActivityRewardsConfig.Load().collectibles.Count(c => c != null && c.type == ItemType.Collectible));
        }

        [Test]
        public void FirstSteps_Completes_UnlocksExplorer_WhoseProgressStartsAtUnlock()
        {
            Assert.IsTrue(Missions.IsActivityAvailable("gps_01_first_steps"));
            Assert.IsFalse(Missions.IsActivityAvailable("gps_02_explorer"));
            Walked(60);
            Assert.AreEqual(MissionState.InProgress, Missions.GetMissionState("gps_01_first_steps"));
            Assert.AreEqual(60, Missions.GetActivityProgress("gps_01_first_steps").current, 0.01);

            Walked(110);
            Assert.AreEqual(MissionState.Completed, Missions.GetMissionState("gps_01_first_steps"));
            int xp = Session.xp;
            Assert.GreaterOrEqual(xp, 100, "First Steps XP (plus the 100 m milestone)");
            Assert.IsTrue(Missions.IsActivityAvailable("gps_02_explorer"));
            Assert.AreEqual(0, Session.completedMissionIds.Count, "GPS missions must not count as contracts");

            Walked(560);
            Assert.AreNotEqual(MissionState.Completed, Missions.GetMissionState("gps_02_explorer"), "Explorer counts from its unlock (110 m), not from zero");
            Walked(620);
            Assert.AreEqual(MissionState.Completed, Missions.GetMissionState("gps_02_explorer"));
            Assert.AreEqual(1, Session.pendingCollectibleIds.Count, "Explorer reveals a virtual object");
            Assert.IsTrue(Missions.IsActivityAvailable("gps_03_long_journey"));

            ActivityController.RaiseProgressed();
            Assert.AreEqual(1, Session.pendingCollectibleIds.Count, "evaluating again paid again");
        }

        [Test]
        public void LongJourney_UnlocksANewArea_StepChallenge_UsesSteps()
        {
            Missions.UnlockMission("gps_03_long_journey");
            Walked(1000);
            Assert.AreEqual(MissionState.Completed, Missions.GetMissionState("gps_03_long_journey"));
            Assert.IsTrue(GameManager.Instance.GetService<IRegionService>().IsRegionUnlocked("region_industrial"));

            Walked(1000, steps: 1999);
            Assert.AreNotEqual(MissionState.Completed, Missions.GetMissionState("gps_04_step_challenge"));
            Walked(1000, steps: 2000);
            Assert.AreEqual(MissionState.Completed, Missions.GetMissionState("gps_04_step_challenge"));
            Assert.GreaterOrEqual(Session.money, 3000);
        }

        // ---- checkpoints -----------------------------------------------------------------------------

        private static IEnumerator Deliver(SimulatedLocationSource source, int seconds)
        {
            for (int i = 0; i < seconds; i++)
            {
                source.Advance(1);
                yield return null;
            }
        }

        [UnityTest]
        public IEnumerator MarkedCheckpoint_TriggersWhenThePlayerWalksAwayAndComesBack_Once()
        {
            // 120 m north, then back south: an out-and-back walk.
            List<GeoFix> trace = GpsTraces.Walk(new[] { (120.0, 0.0), (125.0, 180.0) }, jitter: 1.5, seed: 8);
            var source = new SimulatedLocationSource(trace) { AutoAdvance = false };
            Tracker.UseSource(source);
            Activity.StartSession();
            yield return Deliver(source, 2);
            PlayerCheckpoint marked = ActivityWorld.PlaceCheckpoint(Session, Tracker.LatestFix.Value, "Park gate");
            Assert.IsNotNull(marked, "a good fix should allow marking the spot");
            Assert.IsFalse(Session.completedCheckpointIds.Contains(marked.id), "standing on the mark must not complete it");

            yield return Deliver(source, 86);  // the far end
            Assert.IsTrue(marked.armed, "walked far enough away");
            Assert.IsFalse(Session.completedCheckpointIds.Contains(marked.id));
            int moneyBefore = Session.money;
            yield return Deliver(source, 92);  // back to the mark
            Assert.IsTrue(Session.completedCheckpointIds.Contains(marked.id), $"never triggered (walked {Session.gpsTotalRealMeters:F0} m)");
            Assert.AreEqual(moneyBefore + 5000, Session.money);
            Assert.AreEqual(MissionState.Completed, Missions.GetMissionState("gps_05_checkpoint"), "Checkpoint Discovery mission");
            int count = Session.completedCheckpointIds.Count;
            yield return Deliver(source, 4);   // lingering at the mark
            Assert.AreEqual(count, Session.completedCheckpointIds.Count);
            Assert.AreEqual(moneyBefore + 5000, Session.money, "paid twice");
        }

        [Test]
        public void Checkpoint_IgnoresAStrayFix_AndNeedsTheWalkFirst()
        {
            var designed = new GpsCheckpointDefinition { id = "cp_test", name = "Test plaza", latitude = GpsTraces.Lat, longitude = GpsTraces.Lon, radius = 20, requiredSessionMeters = 100, money = 1000, xp = 10 };
            ActivityRewardsConfig config = ActivityRewardsConfig.Load();
            config.checkpoints.Add(designed);
            try
            {
                Session.gpsSession = new ActivitySessionState { state = ActivitySessionState.Running, realMeters = 50 };
                var at = new GeoFix(GpsTraces.Lat, GpsTraces.Lon, 5f, 1);
                ActivityWorld.OnValidatedFix(Session, at);
                ActivityWorld.OnValidatedFix(Session, new GeoFix(GpsTraces.Lat, GpsTraces.Lon, 5f, 2));
                Assert.IsFalse(Session.completedCheckpointIds.Contains("cp_test"), "only 50 m walked this session");

                Session.gpsSession.realMeters = 150;
                ActivityWorld.OnValidatedFix(Session, new GeoFix(GpsTraces.Lat, GpsTraces.Lon, 5f, 3));
                (double far, double farLon) = Geo.Offset(GpsTraces.Lat, GpsTraces.Lon, 300, 90);
                ActivityWorld.OnValidatedFix(Session, new GeoFix(far, farLon, 5f, 4));
                ActivityWorld.OnValidatedFix(Session, new GeoFix(GpsTraces.Lat, GpsTraces.Lon, 5f, 5));
                Assert.IsFalse(Session.completedCheckpointIds.Contains("cp_test"), "a single fix inside must not trigger");
                ActivityWorld.OnValidatedFix(Session, new GeoFix(GpsTraces.Lat, GpsTraces.Lon, 5f, 6));
                Assert.IsTrue(Session.completedCheckpointIds.Contains("cp_test"));

                ActivityWorld.OnValidatedFix(Session, new GeoFix(GpsTraces.Lat, GpsTraces.Lon, 40f, 7));
                Assert.AreEqual(1000, Session.money);
            }
            finally
            {
                config.checkpoints.Remove(designed);
            }
        }

        // ---- exploration & collectibles ---------------------------------------------------------------

        [UnityTest]
        public IEnumerator Walking_ExploresNewAreas_CompletesNewDiscovery_VehiclesDoNot()
        {
            List<GeoFix> walk = GpsTraces.Walk(new[] { (450.0, 90.0) }, seed: 12);
            GeoFix end = walk.Last();
            List<GeoFix> ride = GpsTraces.Walk(new[] { (1500.0, 0.0) }, speed: 14, lat: end.Latitude, lon: end.Longitude, t0: end.Timestamp);
            var source = new SimulatedLocationSource(walk.Concat(ride)) { AutoAdvance = false };
            Tracker.UseSource(source);
            Activity.StartSession();
            yield return Deliver(source, walk.Count);
            int areas = Session.exploredAreas.Count;
            Assert.GreaterOrEqual(areas, 4, "a 450 m walk crosses several ~100 m areas");
            Assert.AreEqual(MissionState.Completed, Missions.GetMissionState("gps_06_new_discovery"));
            Assert.Greater(Session.pendingCollectibleIds.Count, 0, "New Discovery reveals a collectible");

            yield return Deliver(source, ride.Count);
            Assert.LessOrEqual(Session.exploredAreas.Count, areas + 1, "areas passed in a vehicle were explored");
        }

        [Test]
        public void AreaKeys_AreSaltedHashes_NotLocations()
        {
            string a = ActivityWorld.AreaKey(Session, 37.5665, 126.9780);
            Assert.AreEqual(a, ActivityWorld.AreaKey(Session, 37.56655, 126.97805), "same ~100 m area");
            Assert.AreNotEqual(a, ActivityWorld.AreaKey(Session, 37.5700, 126.9780), "different area");
            StringAssert.IsMatch("^[0-9A-F]{16}$", a, "an opaque hash, not readable coordinates");
            StringAssert.DoesNotContain("37.5", a);
            StringAssert.DoesNotContain("126.9", a);
            string salt = Session.explorationSalt;
            Session.explorationSalt = "another-career";
            Assert.AreNotEqual(a, ActivityWorld.AreaKey(Session, 37.5665, 126.9780), "the hash depends on the career's salt");
            Session.explorationSalt = salt;
        }

        [Test]
        public void Collectibles_AreDiscoveredInOrder_CollectedOnce()
        {
            string first = ActivityWorld.Discover(Session);
            Assert.AreEqual("col_yeopjeon", first);
            Assert.AreEqual("col_norigae", ActivityWorld.Discover(Session), "next one, not a duplicate");
            Assert.IsTrue(ActivityWorld.Collect(Session, first));
            CollectionAssert.Contains(Session.inventoryItemIds, first);
            Assert.AreEqual(1000, Session.money);
            Assert.AreEqual(40, Session.xp);
            Assert.IsFalse(ActivityWorld.Collect(Session, first), "collected twice");
            Assert.AreEqual(1000, Session.money);
            for (int i = 0; i < 10; i++) ActivityWorld.Discover(Session);
            Assert.AreEqual(7, Session.pendingCollectibleIds.Count, "all 8 found (one already collected), nothing invented after that");
        }

        // ---- persistence -------------------------------------------------------------------------------

        [Test]
        public void SaveAndLoad_KeepsGpsProgress_AndPaysNothingAgain()
        {
            Session.hasStarted = true;
            Walked(150);
            Session.gpsSession = new ActivitySessionState { state = ActivitySessionState.Idle };
            PlayerCheckpoint cp = ActivityWorld.PlaceCheckpoint(Session, new GeoFix(37.5, 127.0, 5f, 1), "Bench");
            ActivityWorld.AreaKey(Session, 37.5, 127.0);
            Session.exploredAreas.Add(ActivityWorld.AreaKey(Session, 37.5, 127.0));
            ActivityWorld.Discover(Session);
            int money = Session.money, xp = Session.xp;
            SaveManager.Instance.SaveSlot(GameConstants.Save.DefaultSlot);

            Session.Reset();
            Assert.IsTrue(SaveManager.Instance.LoadSlot(GameConstants.Save.DefaultSlot));
            Assert.AreEqual(MissionState.Completed, Missions.GetMissionState("gps_01_first_steps"));
            Assert.IsTrue(Missions.IsActivityAvailable("gps_02_explorer"));
            Assert.AreEqual(cp.id, Session.playerCheckpoints.Single().id);
            Assert.AreEqual(1, Session.exploredAreas.Count);
            Assert.AreEqual(1, Session.pendingCollectibleIds.Count);
            Assert.IsFalse(string.IsNullOrEmpty(Session.explorationSalt));
            ActivityController.RaiseProgressed();
            ActivityRewards.Evaluate(Session);
            Assert.AreEqual(money, Session.money, "rewards paid again after loading");
            Assert.AreEqual(xp, Session.xp);
        }
    }
}
