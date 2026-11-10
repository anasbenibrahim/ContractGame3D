using System.Collections;
using System.Linq;
using Contract.Activity;
using Contract.Core;
using Contract.NPCs;
using Contract.Player;
using Contract.SaveSystem;
using Contract.UI;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace Contract.Tests
{
    /// <summary>
    /// GPS Phase 6: the REAL GPS ACTIVITY dashboard (session controls, live stats, throttled
    /// refresh, missions, checkpoints, collectibles, map) and Normal / GPS Life mode switching.
    /// </summary>
    public sealed class ActivityDashboardTests
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
            Tracker.ReleaseAll();
            Tracker.UseSource(null);
            Session.Reset();
            SaveManager.Instance.DeleteSlot(GameConstants.Save.DefaultSlot);
        }

        private static IEnumerator OpenDashboardFromMainMenu()
        {
            SceneManager.LoadScene(GameConstants.Scenes.MainMenu);
            for (int i = 0; i < 3; i++) yield return null;
            Object.FindObjectsByType<Button>(FindObjectsInactive.Exclude).First(b => b.name == "GpsActivity").onClick.Invoke();
        }

        private static SimulatedLocationSource Walk(double meters) =>
            new SimulatedLocationSource(GpsTraces.Walk(new[] { (meters, 0.0) })) { AutoAdvance = false };

        private static IEnumerator Deliver(SimulatedLocationSource source, int seconds)
        {
            for (int i = 0; i < seconds; i++)
            {
                source.Advance(1);
                yield return null;
            }
        }

        [UnityTest]
        public IEnumerator Dashboard_StartPauseResumeStop_WithLiveStats()
        {
            SimulatedLocationSource gps = Walk(200);
            Tracker.UseSource(gps);
            Activity.UseStepSource(new SimulatedStepSource(1000));
            yield return OpenDashboardFromMainMenu();
            ActivityDashboardView dash = Object.FindAnyObjectByType<ActivityDashboardView>();
            Assert.IsTrue(dash.IsOpen);
            StringAssert.Contains("START", dash.Text("primaryLabel"));
            Assert.IsFalse(dash.StopButton.gameObject.activeSelf);

            dash.PrimaryButton.onClick.Invoke();                    // START
            Assert.IsTrue(Activity.IsRunning);
            Assert.IsTrue(Activity.GpsModeActive, "a walking session is GPS Life Mode");
            StringAssert.Contains("PAUSE", dash.Text("primaryLabel"));
            Assert.IsTrue(dash.StopButton.gameObject.activeSelf);
            yield return Deliver(gps, 90);
            yield return new WaitForSecondsRealtime(0.3f);
            double real = Activity.State.realMeters;
            Assert.Greater(real, 80);
            StringAssert.Contains($"{real:F0} m", dash.Text("realDistance"));
            StringAssert.Contains("GPS", dash.Text("statusText"));
            StringAssert.Contains("Good", dash.Text("statusText"));
            StringAssert.Contains("GPS LIFE", dash.Text("modeLabel"));
            Assert.AreEqual(Contract.Data.MissionState.Completed, Contract.Missions.MissionManager.Instance.GetMissionState("gps_01_first_steps"));
            StringAssert.Contains("Explorer", dash.Text("missionText"), "the next GPS mission is shown");
            Assert.Greater(dash.MissionFill, 0f, "progress bar");
            StringAssert.Contains("hidden", dash.Text("coordinates"), "position hidden by default");

            dash.PrimaryButton.onClick.Invoke();                    // PAUSE
            Assert.IsTrue(Activity.IsPaused);
            StringAssert.Contains("RESUME", dash.Text("primaryLabel"));
            dash.PrimaryButton.onClick.Invoke();                    // RESUME
            Assert.IsTrue(Activity.IsRunning);
            dash.StopButton.onClick.Invoke();                       // STOP
            Assert.IsFalse(Activity.IsRunning || Activity.IsPaused);
            StringAssert.Contains("START", dash.Text("primaryLabel"));
        }

        [UnityTest]
        public IEnumerator Dashboard_RefreshesAFewTimesASecond_NotEveryFrame()
        {
            Tracker.UseSource(Walk(50));
            yield return OpenDashboardFromMainMenu();
            ActivityDashboardView dash = Object.FindAnyObjectByType<ActivityDashboardView>();
            int before = dash.RefreshCount;
            int frames = 0;
            for (float t = 0; t < 1f; t += Time.unscaledDeltaTime)
            {
                frames++;
                yield return null;
            }

            int refreshes = dash.RefreshCount - before;
            Assert.LessOrEqual(refreshes, 6, $"{refreshes} refreshes over {frames} frames");
            Assert.GreaterOrEqual(refreshes, 3);
        }

        [UnityTest]
        public IEnumerator Dashboard_ExplainsWhatIsMissing_OnAPhoneWithoutGps()
        {
            yield return OpenDashboardFromMainMenu();                // PC, no simulated source
            ActivityDashboardView dash = Object.FindAnyObjectByType<ActivityDashboardView>();
            Assert.IsFalse(Activity.CanUseGpsMode(out string why));
            StringAssert.Contains(why, dash.Text("modeHint"));
            dash.PrimaryButton.onClick.Invoke();                     // START without GPS
            Assert.IsFalse(Activity.IsRunning);
            Assert.IsTrue(Object.FindAnyObjectByType<LocationAccessView>().IsOpen, "the GPS setup screen should explain");
            StringAssert.Contains("No GPS", Object.FindAnyObjectByType<LocationAccessView>().StatusTitle);
            dash.ModeButton.onClick.Invoke();
            Assert.IsFalse(Activity.GpsModeActive, "GPS mode must not switch on without GPS");
            Assert.IsTrue(dash.Text("steps").Contains("No step sensor") || dash.Text("steps").Contains("—"), "no invented steps");
        }

        [UnityTest]
        public IEnumerator Dashboard_MarkSpot_Collect_AndMapOnlyInTheCity()
        {
            SimulatedLocationSource gps = Walk(30);
            Tracker.UseSource(gps);
            yield return OpenDashboardFromMainMenu();
            ActivityDashboardView dash = Object.FindAnyObjectByType<ActivityDashboardView>();
            Assert.IsFalse(dash.MarkButton.interactable, "marking needs a running session with a fix");
            dash.PrimaryButton.onClick.Invoke();
            yield return Deliver(gps, 3);
            dash.Refresh();
            Assert.IsTrue(dash.MarkButton.interactable);
            dash.MarkButton.onClick.Invoke();
            Assert.AreEqual(1, Session.playerCheckpoints.Count);
            StringAssert.Contains("Spot 1", dash.Text("checkpointText"));

            ActivityWorld.Discover(Session);
            dash.Refresh();
            Assert.IsTrue(dash.CollectButton.gameObject.activeSelf);
            dash.CollectButton.onClick.Invoke();
            Assert.AreEqual(0, Session.pendingCollectibleIds.Count);
            CollectionAssert.Contains(Session.inventoryItemIds, "col_yeopjeon");
            Assert.IsFalse(dash.MapButton.interactable, "no map in the main menu");
        }

        // ---- mode switching in the city ------------------------------------------------------------

        [UnityTest]
        public IEnumerator ModeSwitch_InTheCity_NoTeleport_JoystickHandover_AndSaved()
        {
            Session.hasStarted = true;
            Session.selectedCharacterId = "char_alex_morgan";
            Tracker.UseSource(Walk(100));
            SceneManager.LoadScene(GameConstants.Scenes.GameWorld);
            for (int i = 0; i < 6; i++) yield return null;
            NpcManager.Instance.DespawnAllCivilians();
            yield return new WaitForSeconds(1.2f);                   // past the spawn window
            Transform player = GameObject.FindGameObjectWithTag("Player").transform;
            GpsRouteFollower follower = player.GetComponent<GpsRouteFollower>();
            ActivityHudStrip strip = Object.FindAnyObjectByType<ActivityHudStrip>();
            yield return new WaitForSecondsRealtime(0.3f);
            StringAssert.Contains("NORMAL", strip.Label);
            Assert.IsFalse(follower.DriverActive);

            Vector3 before = player.position;
            ActivityDashboardView dash = Object.FindAnyObjectByType<ActivityDashboardView>(FindObjectsInactive.Include);
            dash.Open();
            dash.ModeButton.onClick.Invoke();                         // -> GPS Life
            Assert.IsTrue(Activity.GpsModeActive);
            Assert.IsTrue(SaveManager.Instance.SlotExists(GameConstants.Save.DefaultSlot), "switching modes saves progress");
            yield return null;
            yield return null;
            Assert.IsTrue(follower.DriverActive, "the route now drives the operative");
            Assert.Less(Vector3.Distance(before, player.position), 0.5f, "switching mode must not teleport the operative");
            yield return new WaitForSecondsRealtime(0.3f);
            StringAssert.Contains("GPS LIFE", strip.Label);

            dash.MapButton.onClick.Invoke();
            Assert.IsTrue(GameManager.Instance.IsMapOpen, "VIEW MAP in the city");
            GameManager.Instance.CloseMap();

            dash.Open();
            dash.PrimaryButton.onClick.Invoke();                      // start a session
            Assert.IsTrue(Activity.IsRunning);
            dash.ModeButton.onClick.Invoke();                         // -> Normal
            Assert.IsFalse(Activity.GpsModeActive);
            Assert.IsTrue(Activity.IsPaused, "switching to Normal pauses the walk, it does not discard it");
            yield return null;
            Assert.IsFalse(follower.DriverActive, "joystick back in charge");
            dash.Close();
        }
    }
}
