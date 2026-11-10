using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Contract.Activity;
using Contract.Core;
using Contract.NPCs;
using Contract.Player;
using Contract.SaveSystem;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Contract.Tests
{
    /// <summary>
    /// GPS Phase 3: walking sessions (start / pause / resume / stop, ratio, totals, background,
    /// save / load of an interrupted session) and the virtual character walking the route in step
    /// with the real distance, without teleporting and without joystick interference.
    /// </summary>
    public sealed class ActivitySessionTests
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
            Activity.GpsModeActive = false;
            Tracker.ReleaseAll();
            Tracker.UseSource(null);
            Session.Reset();
            SaveManager.Instance.DeleteSlot(GameConstants.Save.DefaultSlot);
        }

        /// <summary>A manual route: the test decides when each fix "arrives".</summary>
        private static SimulatedLocationSource Route(List<GeoFix> fixes)
        {
            var source = new SimulatedLocationSource(fixes) { AutoAdvance = false };
            Tracker.UseSource(source);
            return source;
        }

        private static IEnumerator Deliver(SimulatedLocationSource source, int seconds)
        {
            for (int i = 0; i < seconds; i++)
            {
                source.Advance(1);
                yield return null; // the tracker delivers the newest fix each frame
            }
        }

        [UnityTest]
        public IEnumerator Session_CountsWalking_PausesAndResumes_WithoutCountingThePausedStretch()
        {
            // 0-60 s walk 84 m, 60-120 s walk another 84 m (paused: must not count), 120-180 s walk 84 m.
            SimulatedLocationSource source = Route(GpsTraces.Walk(new[] { (252.0, 0.0) }, seed: 11));
            Activity.StartSession();
            Assert.IsTrue(Activity.IsRunning);
            Assert.IsTrue(Tracker.IsActive, "session must turn location on");
            Assert.IsTrue(Activity.KeepsScreenAwake, "screen kept awake while walking (the editor ignores Screen.sleepTimeout: checked on the phone)");
            yield return Deliver(source, 60);
            double first = Activity.State.realMeters;
            Assert.AreEqual(84, first, 8);

            Activity.Pause();
            Assert.IsFalse(Tracker.IsActive, "paused session must stop location (battery)");
            source.Advance(60); // the player keeps walking while paused
            yield return null;
            Activity.Resume();
            yield return Deliver(source, 60);
            Assert.AreEqual(first + 84, Activity.State.realMeters, 10, "distance walked while paused was counted");

            Activity.Stop();
            Assert.AreEqual(ActivitySessionState.Idle, Activity.State.state);
            Assert.IsFalse(Tracker.IsActive);
            Assert.AreEqual(Activity.State.realMeters, Session.gpsTotalRealMeters, 0.001, "career total");
            Assert.Greater(Activity.State.seconds, 0f);
        }

        [UnityTest]
        public IEnumerator MovementRatio_ScalesVirtualDistance()
        {
            Session.Settings.gpsMovementRatio = 2f;
            SimulatedLocationSource source = Route(GpsTraces.Walk(new[] { (100.0, 90.0) }));
            Activity.StartSession();
            yield return Deliver(source, 75);
            Activity.Stop();
            Assert.Greater(Session.gpsTotalRealMeters, 90);
            Assert.AreEqual(Session.gpsTotalRealMeters * 2, Session.gpsTotalVirtualMeters, 0.001);
            Assert.AreEqual(Session.gpsTotalVirtualMeters, Session.gpsRouteMeters, 0.001);
        }

        [UnityTest]
        public IEnumerator StandingStill_WithDrift_AddsNothing()
        {
            SimulatedLocationSource source = Route(GpsTraces.Still(180, 6, 10f, 21));
            Activity.StartSession();
            yield return Deliver(source, 180);
            Assert.Less(Activity.State.realMeters, 2.0);
        }

        [UnityTest]
        public IEnumerator GoingToTheBackground_PausesTheSessionAndSaysWhy()
        {
            SimulatedLocationSource source = Route(GpsTraces.Walk(new[] { (60.0, 0.0) }));
            Activity.StartSession();
            yield return Deliver(source, 20);
            Activity.SendMessage("OnApplicationPause", true);
            Assert.IsTrue(Activity.IsPaused);
            Assert.IsTrue(Activity.State.interrupted, "the dashboard must be able to explain the pause");
            Assert.IsFalse(Activity.KeepsScreenAwake, "screen timeout must be released when paused");
            Assert.IsFalse(Tracker.IsActive, "location must stop in the background");
        }

        [UnityTest]
        public IEnumerator InterruptedSession_IsRestoredPaused_AndTotalsSurviveSaving()
        {
            Session.hasStarted = true;
            SimulatedLocationSource source = Route(GpsTraces.Walk(new[] { (100.0, 0.0) }));
            Activity.StartSession();
            yield return Deliver(source, 60);
            double walked = Session.gpsTotalRealMeters;
            Assert.Greater(walked, 50);
            SaveManager.Instance.SaveSlot(GameConstants.Save.DefaultSlot); // e.g. autosave just before the OS kills the game

            Session.Reset();
            Assert.IsTrue(SaveManager.Instance.LoadSlot(GameConstants.Save.DefaultSlot));
            Assert.AreEqual(walked, Session.gpsTotalRealMeters, 0.001);
            Assert.AreEqual(walked, Session.gpsSession.realMeters, 0.001);
            Assert.AreEqual(ActivitySessionState.Paused, Session.gpsSession.state, "a session cannot still be running after a restart");
            Assert.IsTrue(Session.gpsSession.interrupted);
        }

        [Test]
        public void SaveData_RejectsCorruptTotals()
        {
            Session.hasStarted = true;
            Session.gpsTotalRealMeters = double.NaN;
            Session.gpsTotalVirtualMeters = -50;
            SaveManager.Instance.SaveSlot(GameConstants.Save.DefaultSlot);
            Session.Reset();
            SaveManager.Instance.LoadSlot(GameConstants.Save.DefaultSlot);
            Assert.AreEqual(0, Session.gpsTotalRealMeters);
            Assert.AreEqual(0, Session.gpsTotalVirtualMeters);
        }

        // ---- the virtual character -------------------------------------------------------------

        [UnityTest]
        public IEnumerator Character_WalksTheRoute_InStepWithRealWalking_NoTeleport_NoJoystick()
        {
            var gamepad = InputSystem.AddDevice<Gamepad>("GpsTestPad");
            InputSettings.BackgroundBehavior background = InputSystem.settings.backgroundBehavior;
            InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            try
            {
                Session.selectedCharacterId = "char_alex_morgan";
                var source = new SimulatedLocationSource(GpsTraces.Walk(new[] { (40.0, 30.0) }, seed: 5)) { AutoAdvance = true };
                Tracker.UseSource(source);   // GPS mode needs a location source
                Activity.GpsModeActive = true;
                SceneManager.LoadScene(GameConstants.Scenes.GameWorld);
                for (int i = 0; i < 6; i++) yield return null;
                NpcManager.Instance.DespawnAllCivilians();
                GpsRoute route = GpsRoute.Current;
                Assert.IsNotNull(route, "GameWorld has no GPS route");
                Assert.Greater(route.Length, 300f);
                Transform player = GameObject.FindGameObjectWithTag("Player").transform;
                GpsRouteFollower follower = player.GetComponent<GpsRouteFollower>();
                Assert.IsTrue(follower.DriverActive);
                Assert.Less(Vector3.Distance(Flat(player.position), Flat(route.PointAt(0))), 1.5f, "placed at the start of the route");

                // Real-time walk: 1.4 m/s, one fix per second, auto-advancing with the game clock.
                Activity.StartSession();

                // The joystick is held forward the whole time: it must not move the character.
                InputSystem.QueueStateEvent(gamepad, new GamepadState { leftStick = new Vector2(-1f, 0f) });
                Vector3 last = player.position;
                float worstStep = 0f;
                ThirdPersonController controller = player.GetComponent<ThirdPersonController>();
                for (float t = 0f; t < 34f; t += Time.deltaTime)
                {
                    yield return null;
                    worstStep = Mathf.Max(worstStep, Vector3.Distance(Flat(player.position), Flat(last)) / Mathf.Max(Time.deltaTime, 1e-4f));
                    last = player.position;
                }

                InputSystem.QueueStateEvent(gamepad, new GamepadState());
                double real = Activity.State.realMeters;
                Assert.Greater(real, 30, "walking was not counted");
                yield return new WaitForSeconds(3f); // let the character settle
                Assert.AreEqual(Session.gpsRouteMeters, follower.Progress, 1.5, "character out of step with the real distance");
                Assert.Less(Vector3.Distance(Flat(player.position), Flat(route.PointAt(follower.Progress))), 2.5f, "character left the route");
                Assert.LessOrEqual(worstStep, controller.SprintSpeed * 1.3f, "the character moved faster than it can run (teleport)");
            }
            finally
            {
                InputSystem.RemoveDevice(gamepad);
                InputSystem.settings.backgroundBehavior = background;
            }
        }

        [UnityTest]
        public IEnumerator NormalMode_LeavesTheJoystickInCharge()
        {
            Session.selectedCharacterId = "char_alex_morgan";
            Activity.GpsModeActive = false;
            SceneManager.LoadScene(GameConstants.Scenes.GameWorld);
            for (int i = 0; i < 4; i++) yield return null;
            GpsRouteFollower follower = GameObject.FindGameObjectWithTag("Player").GetComponent<GpsRouteFollower>();
            Assert.IsFalse(follower.DriverActive, "GPS mode must not interfere with normal controls");
        }

        private static Vector3 Flat(Vector3 v) => new Vector3(v.x, 0f, v.z);
    }
}
