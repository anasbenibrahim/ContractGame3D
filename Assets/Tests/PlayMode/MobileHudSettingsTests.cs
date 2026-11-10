using System.Collections;
using System.Reflection;
using System.Text.RegularExpressions;
using Contract.Activity;
using Contract.Contracts;
using Contract.Core;
using Contract.NPCs;
using Contract.Performance;
using Contract.UI;
using Contract.UI.Touch;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace Contract.Tests
{
    /// <summary>
    /// Phase 9: mobile HUD (safe area, touch-mode scaling, joystick size, button opacity) and the
    /// tabbed settings (touch, FPS target, GPS options and status without location data).
    /// </summary>
    public sealed class MobileHudSettingsTests
    {
        private static GameSession Session => GameManager.Instance.Session;

        [TearDown]
        public void TearDown()
        {
            SafeAreaFitter.Simulated = null;
            GameManager.Instance?.SetPaused(false);
            GameManager.Instance?.Session.Reset();
            GameManager.Instance?.GetService<ISaveService>()?.DeleteSlot(GameConstants.Save.DefaultSlot);
        }

        private static T Field<T>(object target, string name) =>
            (T)target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(target);

        private static IEnumerator Load(string scene, int frames = 6)
        {
            SceneManager.LoadScene(scene, LoadSceneMode.Single);
            for (int i = 0; i < frames; i++) yield return null;
        }

        private static IEnumerator World(TouchControlsMode mode)
        {
            yield return Load(GameConstants.Scenes.MainMenu, 2);
            Session.Reset();
            Session.hasStarted = true;
            Session.selectedCharacterId = "char_alex_morgan";
            Session.Settings.touchControls = (int)mode;
            yield return Load(GameConstants.Scenes.GameWorld, 8);
            NpcManager.Instance?.DespawnAllCivilians();
        }

        private static SettingsController OpenSettings()
        {
            SettingsController settings = Object.FindAnyObjectByType<SettingsController>(FindObjectsInactive.Include);
            settings.Open(null);
            return settings;
        }

        // ---- frame rate ------------------------------------------------------------------------

        [Test]
        public void FrameRate_PhonesUseTheTarget_PcsFollowVSync()
        {
            var s = new GameSettings { vSync = true, targetFramerate = 60 };
            Assert.AreEqual(60, PerformanceManager.FrameRate(s, true, 120), "phones ignore V-Sync: the target applies (not Android's 30 fps default)");
            s.targetFramerate = 120;
            Assert.AreEqual(90, PerformanceManager.FrameRate(s, true, 90), "never above the display");
            s.targetFramerate = 0;
            Assert.AreEqual(120, PerformanceManager.FrameRate(s, true, 120), "unlimited = display rate on phones");
            Assert.AreEqual(-1, PerformanceManager.FrameRate(s, false, 60), "PC with V-Sync follows the display");
            s.vSync = false;
            Assert.AreEqual(240, PerformanceManager.FrameRate(s, false, 60), "PC unlimited still caps runaway menus");
            s.targetFramerate = 30;
            Assert.AreEqual(30, PerformanceManager.FrameRate(s, false, 60));
        }

        // ---- settings panel --------------------------------------------------------------------

        [UnityTest]
        public IEnumerator Settings_HasFiveTabs_AndTouchSettingsApplyLive()
        {
            yield return World(TouchControlsMode.Always);
            SettingsController settings = OpenSettings();
            yield return null;
            Assert.AreEqual(5, settings.TabCount);
            Assert.AreEqual(2, settings.Tab, "with touch controls the panel opens on the Touch tab");
            GameObject[] pages = Field<GameObject[]>(settings, "pages");
            for (int i = 0; i < pages.Length; i++) Assert.AreEqual(i == 2, pages[i].activeSelf, $"only the Touch page shows ({i})");

            Field<Slider>(settings, "touchLookXSlider").value = 2f;
            Field<Slider>(settings, "touchLookYSlider").value = 0.5f;
            Field<Slider>(settings, "joystickSizeSlider").value = 1.3f;
            Field<Slider>(settings, "joystickDeadZoneSlider").value = 0.25f;
            Field<Slider>(settings, "buttonOpacitySlider").value = 0.5f;
            Field<Toggle>(settings, "hapticsToggle").isOn = false;
            GameSettings s = Session.Settings;
            Assert.AreEqual(2f, s.touchLookSensitivityX, 1e-4f);
            Assert.AreEqual(0.5f, s.touchLookSensitivityY, 1e-4f);
            Assert.AreEqual(1.3f, s.joystickSize, 1e-4f);
            Assert.AreEqual(0.25f, s.joystickDeadZone, 1e-4f);
            Assert.AreEqual(0.5f, s.buttonOpacity, 1e-4f);
            Assert.IsFalse(s.haptics);

            Field<Button>(settings, "touchModeNext").onClick.Invoke();
            Assert.AreEqual((int)TouchControlsMode.Never, s.touchControls, "Always -> Off");
            StringAssert.Contains("Off", Field<TMP_Text>(settings, "touchModeLabel").text);
            Field<Button>(settings, "touchModeNext").onClick.Invoke();
            Assert.AreEqual((int)TouchControlsMode.Auto, s.touchControls);

            settings.ShowTab(3);
            s.targetFramerate = 60;
            Field<Button>(settings, "fpsNext").onClick.Invoke();
            Assert.AreEqual(90, s.targetFramerate);
            Field<Button>(settings, "fpsNext").onClick.Invoke();
            Field<Button>(settings, "fpsNext").onClick.Invoke();
            Assert.AreEqual(0, s.targetFramerate, "… 120, Unlimited");
            StringAssert.Contains("Unlimited", Field<TMP_Text>(settings, "fpsLabel").text);
            Field<Button>(settings, "fpsNext").onClick.Invoke();
            Assert.AreEqual(30, s.targetFramerate, "wraps around");
            Assert.AreEqual(Application.isMobilePlatform || !s.vSync ? 30 : -1, Application.targetFrameRate, "frame rate applied");
            settings.Close();
        }

        [UnityTest]
        public IEnumerator SettingsCard_FitsTheTouchLayout()
        {
            yield return World(TouchControlsMode.Always);
            SettingsController settings = OpenSettings();
            yield return null;
            yield return null;
            Assert.IsTrue(Object.FindAnyObjectByType<MobileCanvasScale>().TouchLayout);
            var card = (RectTransform)settings.transform.Find("Card");
            Rect canvas = ((RectTransform)settings.GetComponentInParent<Canvas>().transform).rect;
            Assert.LessOrEqual(card.rect.height, canvas.height, "settings card taller than the touch HUD canvas");
            settings.Close();
        }

        [UnityTest]
        public IEnumerator GpsTab_ShowsStatusOnly_AndControlsTheActivitySettings()
        {
            yield return World(TouchControlsMode.Never);
            SettingsController settings = OpenSettings();
            settings.ShowTab(4);
            yield return null;
            // Status lines describe permissions and sensors, never a position.
            Assert.IsNotEmpty(settings.LocationStatus);
            Assert.IsNotEmpty(settings.StepStatus);
            Assert.IsFalse(Regex.IsMatch(settings.LocationStatus + settings.StepStatus, @"\d+\.\d{3,}"), "no coordinates in the status");
            if (!DevicePermissions.IsAndroidDevice) StringAssert.Contains("Android only", settings.LocationStatus);

            Field<Slider>(settings, "gpsRatioSlider").value = 1.53f;
            Assert.AreEqual(1.5f, Session.Settings.gpsMovementRatio, 1e-4f, "rounded to 0.1");
            Assert.AreEqual("1.5×", Field<TMP_Text>(settings, "gpsRatioValue").text);
            Field<Slider>(settings, "gpsAccuracySlider").value = 33.4f;
            Assert.AreEqual(33f, Session.Settings.gpsAccuracyThreshold, 1e-4f);
            Assert.AreEqual("33 m", Field<TMP_Text>(settings, "gpsAccuracyValue").text);
            Field<Toggle>(settings, "notificationsToggle").isOn = false;
            Assert.IsFalse(Session.Settings.activityNotifications);
            Field<Toggle>(settings, "gpsModeToggle").isOn = false;
            Assert.IsFalse(Session.Settings.gpsModeEnabled);
            Assert.IsFalse(ActivityController.Instance != null && ActivityController.Instance.CanUseGpsMode(out _), "GPS mode unavailable once switched off");
            settings.Close();
        }

        [Test]
        public void StatusDescriptions_CoverEveryState()
        {
            StringAssert.Contains("Allowed", SettingsController.DescribeLocation(PermissionState.Granted, true));
            StringAssert.Contains("location services are off", SettingsController.DescribeLocation(PermissionState.Granted, false));
            StringAssert.Contains("Android settings", SettingsController.DescribeLocation(PermissionState.DeniedPermanently, true));
            StringAssert.Contains("Not asked", SettingsController.DescribeLocation(PermissionState.NotAsked, true));
            StringAssert.Contains("No step sensor", SettingsController.DescribeSteps(PermissionState.Granted, false, null));
            StringAssert.Contains("Needs the Physical activity", SettingsController.DescribeSteps(PermissionState.Denied, true, null));
            StringAssert.Contains("Counting", SettingsController.DescribeSteps(PermissionState.Granted, true, StepSensorStatus.Running));
        }

        // ---- HUD ---------------------------------------------------------------------------------

        [UnityTest]
        public IEnumerator SafeArea_KeepsHudAndTouchControlsInsideTheCutouts()
        {
            yield return World(TouchControlsMode.Always);
            SafeAreaFitter.Simulated = Rect.MinMaxRect(0.06f, 0.04f, 0.97f, 1f); // e.g. a punch-hole on the left, gesture bar at the bottom
            yield return null;
            yield return null;
            foreach (SafeAreaFitter fitter in Object.FindObjectsByType<SafeAreaFitter>(FindObjectsSortMode.None))
            {
                Assert.AreEqual(new Vector2(0.06f, 0.04f), fitter.Area.anchorMin, fitter.name);
                Assert.AreEqual(new Vector2(0.97f, 1f), fitter.Area.anchorMax, fitter.name);
                Assert.Greater(fitter.Area.childCount, 0, fitter.name + " content moved into the safe area");
            }

            TouchControls controls = Object.FindAnyObjectByType<TouchControls>();
            Vector3[] corners = new Vector3[4];
            foreach (TouchButton button in new[] { controls.Jump, controls.Sprint, controls.Crouch, controls.Interact, controls.Pause, controls.Map })
            {
                ((RectTransform)button.transform).GetWorldCorners(corners);
                Assert.GreaterOrEqual(corners[0].x, Screen.width * 0.06f - 1f, button.name);
                Assert.LessOrEqual(corners[2].x, Screen.width * 0.97f + 1f, button.name);
                Assert.GreaterOrEqual(corners[0].y, Screen.height * 0.04f - 1f, button.name);
            }

            ((RectTransform)controls.Joystick.Zone).GetWorldCorners(corners);
            Assert.GreaterOrEqual(corners[0].x, Screen.width * 0.06f - 1f, "joystick zone clear of the cut-out");
        }

        [UnityTest]
        public IEnumerator TouchMode_EnlargesTheHud_AndPcKeepsItsLayout()
        {
            yield return World(TouchControlsMode.Always);
            MobileCanvasScale scale = Object.FindAnyObjectByType<MobileCanvasScale>();
            CanvasScaler scaler = scale.GetComponent<CanvasScaler>();
            yield return null;
            Assert.AreEqual(MobileCanvasScale.TouchReference, scaler.referenceResolution);
            Assert.AreEqual(CanvasScaler.ScreenMatchMode.Expand, scaler.screenMatchMode);
            MobileHudWidget corner = System.Array.Find(Object.FindObjectsByType<MobileHudWidget>(FindObjectsSortMode.None), w => w.name == "TopLeftHud");
            Assert.IsNotNull(corner, "money + contract tracker group");
            Assert.AreEqual(corner.TouchScale, corner.transform.localScale.x, 1e-4f, "money and contract text larger with touch controls");

            TouchControls controls = Object.FindAnyObjectByType<TouchControls>();
            Session.Settings.joystickSize = 1.3f;
            Session.Settings.buttonOpacity = 0.5f;
            yield return null;
            Assert.AreEqual(1.3f, controls.Joystick.Size, 1e-4f);
            Assert.AreEqual(0.5f, controls.GetComponent<CanvasGroup>().alpha, 1e-4f, "button opacity");

            Session.Settings.touchControls = (int)TouchControlsMode.Never;
            yield return null;
            yield return null;
            Assert.AreEqual(new Vector2(1920f, 1080f), scaler.referenceResolution, "PC layout restored");
            Assert.IsFalse(scale.TouchLayout);
            Assert.AreEqual(1f, corner.transform.localScale.x, 1e-4f, "PC size restored");
        }

        // ---- found on the Galaxy A22 (Phase 11) --------------------------------------------------

        [UnityTest]
        public IEnumerator EveryUiCanvas_ExpandsInsteadOfOverflowing_On20by9()
        {
            foreach (string scene in new[] { GameConstants.Scenes.MainMenu, GameConstants.Scenes.MissionSelection, GameConstants.Scenes.GameWorld })
            {
                yield return Load(scene, 4);
                foreach (CanvasScaler scaler in Object.FindObjectsByType<CanvasScaler>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                {
                    if (scaler.uiScaleMode != CanvasScaler.ScaleMode.ScaleWithScreenSize || scaler.GetComponent<TouchControls>() != null) continue;
                    Assert.AreEqual(CanvasScaler.ScreenMatchMode.Expand, scaler.screenMatchMode, $"{scene}/{scaler.name}: a 1920x1080 layout must never be squeezed below 1080 units tall");
                }
            }

            NpcManager.Instance?.DespawnAllCivilians();
        }

        [UnityTest]
        public IEnumerator TouchHints_HideKeyLabels_AndKeyboardHintsStayHiddenWhilePaused()
        {
            yield return World(TouchControlsMode.Always);
            Assert.AreEqual("뒤로 · Back", InputHints.ForLabel("뒤로 · Back   [ESC]"));
            var hints = Field<GameObject>(Object.FindAnyObjectByType<TouchControls>(), "keyboardHints");
            GameManager.Instance.SetPaused(true);
            yield return null;
            yield return null;
            Assert.IsFalse(hints.activeSelf, "no WASD hints on a phone, even behind the pause menu");
            TouchHintText back = System.Array.Find(Object.FindObjectsByType<TouchHintText>(FindObjectsInactive.Include, FindObjectsSortMode.None),
                t => t.isActiveAndEnabled && t.GetComponent<TMP_Text>().text.Contains("Back"));
            if (back != null) StringAssert.DoesNotContain("[ESC]", back.GetComponent<TMP_Text>().text);

            Session.Settings.touchControls = (int)TouchControlsMode.Never;
            GameManager.Instance.SetPaused(false);
            yield return null;
            yield return null;
            Assert.IsTrue(hints.activeSelf, "PC keeps its keyboard hints");
            Assert.AreEqual("뒤로 · Back   [ESC]", InputHints.ForLabel("뒤로 · Back   [ESC]"));
        }

        [UnityTest]
        public IEnumerator GpsSetup_OpenedFromSettings_ReturnsToSettings_NotTheDashboard()
        {
            yield return World(TouchControlsMode.Never);
            SettingsController settings = OpenSettings();
            settings.ShowTab(4);
            Field<Button>(settings, "locationButton").onClick.Invoke();
            LocationAccessView access = Object.FindAnyObjectByType<LocationAccessView>(FindObjectsInactive.Include);
            ActivityDashboardView dashboard = Object.FindAnyObjectByType<ActivityDashboardView>(FindObjectsInactive.Include);
            if (!DevicePermissions.IsAndroidDevice) access.OpenOver(); // the button is Android-only; open it the same way
            yield return null;
            Assert.IsTrue(access.IsOpen);
            access.Close();
            yield return null;
            Assert.IsTrue(settings.IsOpen, "back on the settings");
            Assert.IsFalse(dashboard != null && dashboard.IsOpen, "the dashboard must not pop up");
            settings.Close();
        }
    }
}
