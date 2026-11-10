using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Contract.Activity;
using Contract.Core;
using Contract.Regions;
using Contract.SaveSystem;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Contract.Tests
{
    /// <summary>
    /// GPS Phase 4: session steps from a cumulative hardware counter, step evidence feeding the
    /// distance filter, XP and levels, and activity milestones that pay exactly once.
    /// </summary>
    public sealed class StepAndXpTests
    {
        private static ActivityController Activity => ActivityController.Instance;
        private static LocationTracker Tracker => LocationTracker.Instance;
        private static GameSession Session => GameManager.Instance.Session;

        [SetUp]
        public void Setup() => Session.Reset();

        [TearDown]
        public void TearDown()
        {
            Activity.Stop();
            Activity.UseStepSource(null);
            Activity.Clock = () => Time.realtimeSinceStartup;
            Tracker.ReleaseAll();
            Tracker.UseSource(null);
            Session.Reset();
            SaveManager.Instance.DeleteSlot(GameConstants.Save.DefaultSlot);
        }

        // ---- step tracker ----------------------------------------------------------------------

        [Test]
        public void SessionSteps_StartFromTheHardwareTotal_NotTheLifetimeCount()
        {
            var sensor = new SimulatedStepSource(bootTotal: 48213);
            var steps = new StepTracker(sensor);
            steps.Begin(0f);
            steps.Tick(0f);                       // baseline read
            Assert.AreEqual(0, steps.Steps, "the phone's lifetime total leaked into the session");
            sensor.Cumulative += 120;
            Assert.AreEqual(120, steps.Tick(1f));
            Assert.AreEqual(120, steps.Steps);
        }

        [Test]
        public void CounterReset_AfterAReboot_KeepsTheSessionSteps()
        {
            var sensor = new SimulatedStepSource(5000);
            var steps = new StepTracker(sensor);
            steps.Begin(0f);
            steps.Tick(0f);
            sensor.Cumulative = 5300;
            steps.Tick(1f);
            sensor.Cumulative = 40;               // rebooted: the counter restarted
            steps.Tick(2f);
            Assert.AreEqual(340, steps.Steps);
            sensor.Cumulative = 100;
            steps.Tick(3f);
            Assert.AreEqual(400, steps.Steps);
        }

        [Test]
        public void Evidence_StepsMeanWalking_SilenceMeansStill_NoSensorMeansUnknown()
        {
            var sensor = new SimulatedStepSource(1000);
            var steps = new StepTracker(sensor);
            steps.Begin(0f);
            steps.Tick(0f);
            sensor.Cumulative += 8;
            steps.Tick(2f);
            Assert.AreEqual(MotionEvidence.Stepping, steps.Evidence(3f));
            Assert.AreEqual(MotionEvidence.Stepping, steps.Evidence(2f + StepTracker.SteppingWindow - 0.1f));
            Assert.AreEqual(MotionEvidence.Still, steps.Evidence(2f + StepTracker.StillAfter + 0.1f));

            var silent = new StepTracker(new SimulatedStepSource(0) { HasReading = false });
            silent.Begin(0f);
            silent.Tick(30f);
            Assert.AreEqual(MotionEvidence.Unknown, silent.Evidence(30f), "a sensor that never reported must not claim 'still'");

            var none = new StepTracker(new SimulatedStepSource(0) { Supported = false });
            none.Begin(0f);
            Assert.AreEqual(StepSensorStatus.Unsupported, none.Status);
            Assert.AreEqual(MotionEvidence.Unknown, none.Evidence(30f));
            Assert.AreEqual(0, none.Steps, "no sensor: no invented steps");
        }

        // ---- XP ----------------------------------------------------------------------------------

        [Test]
        public void Xp_LevelsUp_AndRaisesEvents()
        {
            Assert.AreEqual(1, Experience.LevelFor(0));
            Assert.AreEqual(2, Experience.LevelFor(100));
            Assert.AreEqual(3, Experience.LevelFor(300));
            Assert.AreEqual(0.5f, Experience.Progress(200), 1e-4f);
            var levels = new List<int>();
            void OnLevel(int l) => levels.Add(l);
            GameEvents.LevelUp += OnLevel;
            Experience.Add(650, "test");
            GameEvents.LevelUp -= OnLevel;
            Assert.AreEqual(650, Session.xp);
            CollectionAssert.AreEqual(new[] { 2, 3, 4 }, levels, "one event per level crossed");
            Experience.Add(-50, "test");
            Assert.AreEqual(650, Session.xp, "XP never goes down");
        }

        // ---- milestones ----------------------------------------------------------------------------

        [Test]
        public void Milestones_DefaultTable_MatchesTheBrief()
        {
            ActivityRewardsConfig config = ActivityRewardsConfig.Load();
            Assert.IsTrue(config.name.Length > 0 && Resources.Load<ActivityRewardsConfig>("ActivityRewards") != null, "Resources/ActivityRewards.asset missing");
            ActivityMilestone m100 = config.milestones.Single(m => m.id == "walk_100m");
            ActivityMilestone m500 = config.milestones.Single(m => m.id == "walk_500m");
            ActivityMilestone m1k = config.milestones.Single(m => m.id == "walk_1km");
            ActivityMilestone steps = config.milestones.Single(m => m.id == "steps_2000");
            Assert.IsFalse(string.IsNullOrEmpty(m100.unlockMissionId), "100 m unlocks a mission");
            Assert.Greater(m500.money, 0, "500 m gives a reward");
            Assert.IsFalse(string.IsNullOrEmpty(m1k.unlockRegionId), "1 km unlocks an area");
            Assert.IsTrue(steps.repeat);
            Assert.AreEqual(2000, steps.threshold);
            Assert.Greater(steps.xp, 0, "2,000 steps earn XP");
        }

        [Test]
        public void Milestones_PayOnce_EvenWhenEvaluatedAgainOrReloaded()
        {
            Session.hasStarted = true;
            string unlocked = null;
            System.Action<string> previous = ActivityRewards.UnlockMission;
            ActivityRewards.UnlockMission = id => unlocked = id;
            try
            {
                Session.gpsTotalRealMeters = 120;
                Assert.AreEqual(1, ActivityRewards.Evaluate(Session));
                Assert.AreEqual("gps_02_explorer", unlocked);
                Assert.AreEqual(50, Session.xp);
                Assert.AreEqual(0, ActivityRewards.Evaluate(Session), "paid twice");

                Session.gpsTotalRealMeters = 1050;
                Assert.AreEqual(2, ActivityRewards.Evaluate(Session), "500 m and 1 km");
                Assert.AreEqual(5000, Session.money);
                Assert.AreEqual("활동 · 500 m walked", Session.ledger.Last().source);
                Assert.IsTrue(GameManager.Instance.GetService<IRegionService>().IsRegionUnlocked("region_industrial"));

                SaveManager.Instance.SaveSlot(GameConstants.Save.DefaultSlot);
                Session.Reset();
                SaveManager.Instance.LoadSlot(GameConstants.Save.DefaultSlot);
                int xp = Session.xp;
                Assert.AreEqual(0, ActivityRewards.Evaluate(Session), "a reload must not pay again");
                Assert.AreEqual(xp, Session.xp);
                Assert.AreEqual(5000, Session.money);
            }
            finally
            {
                ActivityRewards.UnlockMission = previous; // the mission manager's hook
            }
        }

        [Test]
        public void RepeatingMilestone_PaysForEveryFullThreshold()
        {
            Session.gpsTotalSteps = 1999;
            ActivityRewards.Evaluate(Session);
            Assert.AreEqual(0, ActivityRewards.Awarded(Session, "steps_2000"));
            Session.gpsTotalSteps = 6100;
            ActivityRewards.Evaluate(Session);
            Assert.AreEqual(3, ActivityRewards.Awarded(Session, "steps_2000"));
            Assert.AreEqual(300, Session.xp);
        }

        // ---- the controller with a step sensor --------------------------------------------------------

        [UnityTest]
        public IEnumerator Session_CountsSteps_AndStandingStillAddsNoDistance_EvenWithDrift()
        {
            var sensor = new SimulatedStepSource(30000);
            Activity.UseStepSource(sensor);
            var gps = new SimulatedLocationSource(GpsTraces.Wander(120, 8, 30, 3, 12f, 3)) { AutoAdvance = false };
            Tracker.UseSource(gps);
            Activity.Clock = () => (float)gps.Clock; // steps and GPS on the same (simulated) time, as on a phone
            Activity.StartSession();
            yield return null;                    // baseline
            double atStill = -1;
            for (int t = 0; t < 120; t++)
            {
                gps.Advance(1);
                yield return null;
                if (t == 2) sensor.Cumulative += 3;          // a couple of steps at the start, then nothing
                if (Activity.Motion == MotionEvidence.Still && atStill < 0) atStill = Activity.State.realMeters;
            }

            Assert.AreEqual(3, Activity.State.steps, "session steps");
            Assert.AreEqual(3, Session.gpsTotalSteps);
            Assert.GreaterOrEqual(atStill, 0, "the step counter never reported 'still'");
            Assert.AreEqual(atStill, Activity.State.realMeters, 1e-9, "distance added while the step counter said 'still'");
            Assert.Less(Activity.State.realMeters, 8.0, "the few steps at the start can count a little GPS noise, not more");
        }

        [UnityTest]
        public IEnumerator PausedSession_DoesNotCountStepsTakenMeanwhile()
        {
            var sensor = new SimulatedStepSource(100);
            Activity.UseStepSource(sensor);
            Tracker.UseSource(new SimulatedLocationSource(GpsTraces.Walk(new[] { (10.0, 0.0) })) { AutoAdvance = false });
            Activity.StartSession();
            yield return null;
            sensor.Cumulative += 50;
            yield return null;
            Activity.Pause();
            sensor.Cumulative += 400;             // walked with the session paused
            Activity.Resume();
            yield return null;                    // new baseline
            sensor.Cumulative += 25;
            yield return null;
            Assert.AreEqual(75, Activity.State.steps);
        }

        [UnityTest]
        public IEnumerator TwoThousandSteps_EarnXpThroughTheSession()
        {
            var sensor = new SimulatedStepSource(0);
            Activity.UseStepSource(sensor);
            Tracker.UseSource(new SimulatedLocationSource(GpsTraces.Walk(new[] { (10.0, 0.0) })) { AutoAdvance = false });
            Activity.StartSession();
            yield return null;
            sensor.Cumulative = 2050;
            yield return null;
            Assert.AreEqual(1, ActivityRewards.Awarded(Session, "steps_2000"));
            Assert.AreEqual(Contract.Data.MissionState.Completed, Contract.Missions.MissionManager.Instance.GetMissionState("gps_04_step_challenge"));
            Assert.AreEqual(100 + 200, Session.xp, "2,000-step milestone + Step Challenge mission");
        }
    }
}
