using System.Collections;
using System.IO;
using System.Linq;
using Contract.Activity;
using Contract.Core;
using Contract.SaveSystem;
using Contract.UI;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace Contract.Tests
{
    /// <summary>
    /// GPS Phase 2: distance maths, the simulated route source, the location tracker lifecycle
    /// (owners, release, pause / resume, location switched off and back on), saving when the app
    /// is backgrounded and the GPS access screen. The real phone is covered by the device self-test.
    /// </summary>
    public sealed class LocationTests
    {
        private const double SeoulLat = 37.5665, SeoulLon = 126.9780;
        private LocationTracker Tracker => LocationTracker.Instance;

        [TearDown]
        public void TearDown()
        {
            Tracker.ReleaseAll();
            Tracker.UseSource(null);
            GameManager.Instance.Session.Reset();
            SaveManager.Instance.DeleteSlot(GameConstants.Save.DefaultSlot);
        }

        // ---- geodesy ---------------------------------------------------------------------------

        [Test]
        public void Haversine_MatchesReferenceDistances()
        {
            Assert.AreEqual(111195.08, Geo.DistanceMeters(0, 0, 1, 0), 0.05, "one degree of latitude");
            Assert.AreEqual(8792.90, Geo.DistanceMeters(SeoulLat, SeoulLon, 37.4979, 127.0276), 0.05, "Seoul City Hall -> Gangnam Station");
            Assert.AreEqual(0.0, Geo.DistanceMeters(SeoulLat, SeoulLon, SeoulLat, SeoulLon), 1e-9);
        }

        [TestCase(10.0, 0.0)]
        [TestCase(100.0, 45.0)]
        [TestCase(1000.0, 200.0)]
        public void Offset_IsTheInverseOfDistance(double meters, double bearing)
        {
            (double lat, double lon) = Geo.Offset(SeoulLat, SeoulLon, meters, bearing);
            Assert.AreEqual(meters, Geo.DistanceMeters(SeoulLat, SeoulLon, lat, lon), meters * 1e-6 + 1e-6);
        }

        [Test]
        public void SimulatedWalk_CoversTheExpectedDistance()
        {
            SimulatedLocationSource walk = SimulatedLocationSource.Walk(SeoulLat, SeoulLon, 90f, 1.4f, 60);
            walk.Start(5f, 0f);
            walk.Advance(60);
            Assert.IsTrue(walk.TryGetLatest(out GeoFix end));
            Assert.AreEqual(84.0, Geo.DistanceMeters(SeoulLat, SeoulLon, end.Latitude, end.Longitude), 0.01);
        }

        // ---- tracker ---------------------------------------------------------------------------

        [Test]
        public void OnPC_WithoutASource_StatusIsUnsupported()
        {
            Assert.AreEqual(PermissionState.NotRequired, DevicePermissions.Location, "PC has no runtime permissions");
            object owner = new object();
            Tracker.Acquire(owner);
            Assert.AreEqual(LocationStatus.Unsupported, Tracker.Status);
            Tracker.Release(owner);
            Assert.AreEqual(LocationStatus.Off, Tracker.Status);
        }

        [UnityTest]
        public IEnumerator Tracker_RunsOnlyWhileSomethingNeedsIt_AndReportsEachFixOnce()
        {
            SimulatedLocationSource route = SimulatedLocationSource.Walk(SeoulLat, SeoulLon, 0f, 1.4f, 30);
            route.AutoAdvance = false;
            Tracker.UseSource(route);
            Assert.AreEqual(LocationStatus.Off, Tracker.Status, "nothing asked for location yet");

            int fixes = 0;
            void Count(GeoFix f) => fixes++;
            Tracker.FixReceived += Count;
            object a = new object(), b = new object();
            Tracker.Acquire(a);
            Tracker.Acquire(b);
            Assert.AreEqual(LocationStatus.Initializing, Tracker.Status);
            route.Advance(0);
            yield return null;
            Assert.AreEqual(LocationStatus.Running, Tracker.Status);
            Assert.AreEqual(1, fixes);
            yield return null;
            yield return null;
            Assert.AreEqual(1, fixes, "the same fix was reported twice");
            route.Advance(3);
            yield return null;
            Assert.AreEqual(2, fixes, "one new (latest) fix per frame");

            Tracker.Release(a);
            Assert.AreEqual(LocationStatus.Running, Tracker.Status, "still needed by the second owner");
            Tracker.Release(b);
            Assert.AreEqual(LocationStatus.Off, Tracker.Status);
            Assert.AreEqual(LocationStatus.Off, route.Status, "updates must stop when no longer needed (battery)");
            Tracker.FixReceived -= Count;
        }

        [UnityTest]
        public IEnumerator Tracker_StopsInTheBackground_AndResumes()
        {
            SimulatedLocationSource route = SimulatedLocationSource.Walk(SeoulLat, SeoulLon, 0f, 1.4f, 30);
            route.AutoAdvance = false;
            Tracker.UseSource(route);
            object owner = new object();
            Tracker.Acquire(owner);
            route.Advance(0);
            yield return null;
            Assert.AreEqual(LocationStatus.Running, Tracker.Status);

            Tracker.SendMessage("OnApplicationPause", true);
            Assert.AreEqual(LocationStatus.Off, Tracker.Status);
            Assert.AreEqual(LocationStatus.Off, route.Status, "location must not run while the game is in the background");
            Tracker.SendMessage("OnApplicationPause", false);
            Assert.AreNotEqual(LocationStatus.Off, Tracker.Status, "session resumed");
            Tracker.Release(owner);
        }

        [UnityTest]
        public IEnumerator LocationSwitchedOff_IsReported_AndRecoveredWhenSwitchedOn()
        {
            SimulatedLocationSource route = SimulatedLocationSource.Walk(SeoulLat, SeoulLon, 0f, 1.4f, 30);
            route.ForcedStatus = LocationStatus.ServiceDisabled;
            Tracker.UseSource(route);
            object owner = new object();
            Tracker.Acquire(owner);
            yield return null;
            Assert.AreEqual(LocationStatus.ServiceDisabled, Tracker.Status);
            Assert.IsFalse(Tracker.LatestFix.HasValue);

            route.ForcedStatus = null; // the player switches location on from quick settings
            yield return new WaitForSecondsRealtime(2.5f); // periodic re-check
            Assert.AreNotEqual(LocationStatus.ServiceDisabled, Tracker.Status);
            Tracker.Release(owner);
        }

        // ---- saving when backgrounded ------------------------------------------------------------

        [Test]
        public void BackgroundingTheApp_SavesAStartedCareer_Only()
        {
            GameSession session = GameManager.Instance.Session;
            session.money = 321;
            SaveManager.Instance.SendMessage("OnApplicationPause", true);
            Assert.IsFalse(SaveManager.Instance.SlotExists(GameConstants.Save.DefaultSlot), "an unstarted session must not be saved");

            session.hasStarted = true;
            SaveManager.Instance.SendMessage("OnApplicationPause", true);
            Assert.IsTrue(SaveManager.Instance.TryGetSummary(GameConstants.Save.DefaultSlot, out var summary));
            Assert.AreEqual(321, summary.money);
        }

        // ---- GPS access screen -------------------------------------------------------------------

        [UnityTest]
        public IEnumerator AccessScreen_ExplainsAndRunsAGpsCheck()
        {
            SceneManager.LoadScene(GameConstants.Scenes.MainMenu);
            for (int i = 0; i < 3; i++) yield return null;
            LocationAccessView view = Object.FindAnyObjectByType<LocationAccessView>(FindObjectsInactive.Include);
            Object.FindObjectsByType<Button>(FindObjectsInactive.Exclude).First(b => b.name == "GpsActivity").onClick.Invoke(); // the dashboard
            Button open = Object.FindObjectsByType<Button>(FindObjectsInactive.Exclude).First(b => b.name == "GpsSetup");

            open.onClick.Invoke();
            Assert.IsTrue(view.IsOpen);
            StringAssert.Contains("No GPS", view.StatusTitle, "PC without a location source");
            Assert.IsFalse(view.PrimaryButton.gameObject.activeSelf);

            SimulatedLocationSource route = SimulatedLocationSource.Walk(SeoulLat, SeoulLon, 0f, 1.4f, 120, accuracy: 6f);
            route.AutoAdvance = false;
            Tracker.UseSource(route);
            view.Close();
            open.onClick.Invoke();
            StringAssert.Contains("Location allowed", view.StatusTitle);
            view.PrimaryButton.onClick.Invoke(); // Check GPS
            Assert.IsTrue(view.Checking);
            Assert.IsTrue(Tracker.IsActive);
            yield return new WaitForSecondsRealtime(0.6f);
            StringAssert.Contains("Searching", view.StatusTitle);
            route.Advance(1);
            yield return new WaitForSecondsRealtime(0.6f);
            StringAssert.Contains("GPS OK", view.StatusTitle);
            StringAssert.Contains("Good", view.StatusTitle, "±6 m");
            StringAssert.DoesNotContain("37.5", Field<TMP_Text>(view, "statusBody").text, "coordinates must not be shown");

            view.Close();
            Assert.IsFalse(Tracker.IsActive, "closing the screen must stop location");
        }

        private static T Field<T>(object target, string name) =>
            (T)target.GetType().GetField(name, System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).GetValue(target);
    }
}
