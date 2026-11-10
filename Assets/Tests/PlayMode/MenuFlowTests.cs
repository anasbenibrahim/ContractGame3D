using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Contract.Contracts;
using Contract.Core;
using Contract.Missions;
using Contract.NPCs;
using Contract.UI;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace Contract.Tests
{
    /// <summary>
    /// Stage 7 verification: the menu flow from the main menu to the city through the real scene
    /// loader, button wiring in every scene, Continue, live settings, the pause menu (driven by a
    /// virtual gamepad) and the gameplay HUD widgets.
    /// </summary>
    public sealed class MenuFlowTests
    {
        private Gamepad gamepad;
        private InputSettings.BackgroundBehavior savedBackground;

        private static GameSession Session => GameManager.Instance.Session;
        private static ISaveService Save => GameManager.Instance.GetService<ISaveService>();

        [SetUp]
        public void Setup()
        {
            savedBackground = InputSystem.settings.backgroundBehavior;
            InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            gamepad = InputSystem.AddDevice<Gamepad>("MenuTestGamepad");
        }

        [TearDown]
        public void TearDown()
        {
            if (gamepad != null) InputSystem.RemoveDevice(gamepad);
            InputSystem.settings.backgroundBehavior = savedBackground;
            GameManager.Instance?.SetPaused(false);
            GameManager.Instance?.Session.Reset();
            Save?.DeleteSlot(GameConstants.Save.DefaultSlot); // test save folder only (TestEnvironment)
        }

        // ---- helpers -------------------------------------------------------------------------

        private static IEnumerator Load(string scene)
        {
            SceneManager.LoadScene(scene, LoadSceneMode.Single);
            for (int i = 0; i < 4; i++) yield return null;
        }

        /// <summary>Waits for a load started through the game's SceneLoader (fade out, load, fade in).</summary>
        private static IEnumerator WaitForScene(string scene, float timeout = 8f)
        {
            float end = Time.realtimeSinceStartup + timeout;
            while (SceneManager.GetActiveScene().name != scene || GameManager.Instance.SceneLoader.IsLoading)
            {
                if (Time.realtimeSinceStartup > end) Assert.Fail($"Scene '{scene}' was not reached (active: {SceneManager.GetActiveScene().name})");
                yield return null;
            }

            yield return null;
            yield return null;
        }

        private IEnumerator Press(GamepadButton button)
        {
            InputSystem.QueueStateEvent(gamepad, new GamepadState().WithButton(button, true));
            yield return null;
            InputSystem.QueueStateEvent(gamepad, new GamepadState());
            yield return null;
            yield return null;
        }

        private static T Field<T>(object target, string name) =>
            (T)target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(target);

        private static Button ButtonNamed(string name) =>
            Object.FindObjectsByType<Button>(FindObjectsInactive.Include).First(b => b.name == name && b.gameObject.scene == SceneManager.GetActiveScene());

        /// <summary>Runtime (AddListener) plus serialized listeners on a UnityEvent.</summary>
        private static int ListenerCount(UnityEventBase evt)
        {
            object calls = typeof(UnityEventBase).GetField("m_Calls", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(evt);
            var runtime = (System.Collections.IList)calls.GetType().GetField("m_RuntimeCalls", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(calls);
            return runtime.Count + evt.GetPersistentEventCount();
        }

        // ---- the flow ------------------------------------------------------------------------

        [UnityTest]
        public IEnumerator NewGame_CharacterSelection_WorldSelection_MissionSelection_IntoTheCity()
        {
            Save.DeleteSlot(GameConstants.Save.DefaultSlot);
            yield return Load(GameConstants.Scenes.MainMenu);
            MainMenuController menu = Object.FindAnyObjectByType<MainMenuController>();
            Assert.IsFalse(menu.ContinueButton.interactable, "Continue must be disabled without a save");

            ButtonNamed("NewGame").onClick.Invoke();
            yield return WaitForScene(GameConstants.Scenes.CharacterSelection);
            Assert.IsTrue(Session.hasStarted);

            CharacterSelectionController select = Object.FindAnyObjectByType<CharacterSelectionController>();
            RectTransform cards = Field<RectTransform>(select, "cardContainer");
            Assert.AreEqual(3, cards.childCount, "one card per operative");
            Assert.IsNotNull(select.Stage.Current, "3D preview should show the default operative");

            // Ryan Blake is locked and costs $50,000.
            select.Select("char_ryan_blake");
            Assert.AreEqual("char_ryan_blake", select.SelectedId);
            Assert.IsFalse(select.ConfirmButton.interactable, "a locked operative cannot be confirmed");
            Assert.IsTrue(select.UnlockButton.gameObject.activeSelf);
            Assert.IsFalse(select.UnlockButton.interactable, "no money yet");

            GameManager.Instance.GetService<IEconomyService>().AddMoney(60000, "test grant");
            select.Select("char_ryan_blake");
            Assert.IsTrue(select.UnlockButton.interactable);
            select.UnlockButton.onClick.Invoke();
            Assert.AreEqual(10000, Session.money, "unlock is paid through the economy");
            Assert.IsTrue(Session.unlockedCharacterIds.Contains("char_ryan_blake"));
            Assert.IsTrue(select.ConfirmButton.interactable);
            Assert.AreEqual("char_ryan_blake", select.SelectedId, "selection kept after unlocking");

            select.ConfirmButton.onClick.Invoke();
            yield return WaitForScene(GameConstants.Scenes.WorldSelection);
            Assert.AreEqual("char_ryan_blake", Session.selectedCharacterId);

            WorldSelectionView world = Object.FindAnyObjectByType<WorldSelectionView>();
            Assert.AreEqual("dest_seoul", world.SelectedId, "Seoul is the default destination");
            Assert.IsTrue(world.EnterButton.gameObject.activeSelf);
            world.EnterButton.onClick.Invoke();
            yield return WaitForScene(GameConstants.Scenes.MissionSelection);

            ContractBoardView board = Object.FindAnyObjectByType<ContractBoardView>();
            Assert.IsTrue(board.IsOpen, "mission selection opens the board immediately");
            Assert.AreEqual("region_downtown", board.RegionId);
            Assert.AreEqual(3, board.MissionButtons.Count);
            board.SelectMission("mission_downtown_01");
            board.StartButton.onClick.Invoke();
            yield return WaitForScene(GameConstants.Scenes.GameWorld);

            Assert.AreEqual("mission_downtown_01", MissionManager.Instance.ActiveMission?.missionId, "contract carried into the city");
            Assert.IsFalse(GameManager.Instance.InputBlocked, "no menu should block the player on arrival");
            StringAssert.Contains("Ryan Blake", Field<TMP_Text>(Object.FindAnyObjectByType<CharacterBadgeView>(), "nameText").text);
            NpcManager.Instance?.DespawnAllCivilians();
        }

        [UnityTest]
        public IEnumerator MissionSelection_Back_WorldSelection_CharacterSelection_MainMenu()
        {
            yield return Load(GameConstants.Scenes.MainMenu);
            yield return Load(GameConstants.Scenes.MissionSelection);
            yield return Press(GamepadButton.East);
            yield return WaitForScene(GameConstants.Scenes.WorldSelection);
            yield return Press(GamepadButton.East);
            yield return WaitForScene(GameConstants.Scenes.CharacterSelection);
            yield return Press(GamepadButton.East);
            yield return WaitForScene(GameConstants.Scenes.MainMenu);
        }

        [UnityTest]
        public IEnumerator EveryButton_InEveryScene_DoesSomething(
            [Values("MainMenu", "CharacterSelection", "WorldSelection", "MissionSelection", "GameWorld", "Myeongdong")] string scene)
        {
            Session.selectedCharacterId = "char_alex_morgan";
            yield return Load(GameConstants.Scenes.MainMenu);
            if (scene != GameConstants.Scenes.MainMenu) yield return Load(scene);

            Button[] buttons = Object.FindObjectsByType<Button>(FindObjectsInactive.Include)
                .Where(b => b.gameObject.scene.name == scene)
                .Where(b => b.GetComponentInParent<Canvas>(true).name != "UI Canvas") // prototype shop UI, rebuilt in Stage 8
                .ToArray();
            Assert.Greater(buttons.Length, 3, "scene has no UI buttons");
            string[] dead = buttons.Where(b => ListenerCount(b.onClick) == 0).Select(b => b.transform.parent.name + "/" + b.name).ToArray();
            CollectionAssert.IsEmpty(dead, "buttons without a handler");
            NpcManager.Instance?.DespawnAllCivilians();
        }

        // ---- continue ------------------------------------------------------------------------

        [UnityTest]
        public IEnumerator Continue_LoadsTheSavedCareer_IntoTheCity()
        {
            Session.Reset();
            Session.selectedCharacterId = "char_daniel_cross";
            Session.money = 12345;
            Save.SaveSlot(GameConstants.Save.DefaultSlot);
            Session.Reset();

            yield return Load(GameConstants.Scenes.MainMenu);
            MainMenuController menu = Object.FindAnyObjectByType<MainMenuController>();
            Assert.IsTrue(menu.ContinueButton.interactable, "Continue should be enabled when a save exists");
            menu.ContinueButton.onClick.Invoke();
            yield return WaitForScene(GameConstants.Scenes.GameWorld);

            Assert.AreEqual(12345, Session.money);
            Assert.AreEqual("char_daniel_cross", Session.selectedCharacterId);
            StringAssert.Contains("Daniel Cross", Field<TMP_Text>(Object.FindAnyObjectByType<CharacterBadgeView>(), "nameText").text);
            NpcManager.Instance?.DespawnAllCivilians();
        }

        [UnityTest]
        public IEnumerator NewGame_WithASave_AsksBeforeOverwriting()
        {
            Session.money = 500;
            Save.SaveSlot(GameConstants.Save.DefaultSlot);
            yield return Load(GameConstants.Scenes.MainMenu);
            MainMenuController menu = Object.FindAnyObjectByType<MainMenuController>();

            ButtonNamed("NewGame").onClick.Invoke();
            Assert.IsTrue(menu.ConfirmOpen, "confirmation expected");
            ButtonNamed("No").onClick.Invoke();
            Assert.IsFalse(menu.ConfirmOpen);
            Assert.AreEqual(GameConstants.Scenes.MainMenu, SceneManager.GetActiveScene().name);
            Assert.AreEqual(500, Session.money, "cancel keeps the career");

            ButtonNamed("NewGame").onClick.Invoke();
            ButtonNamed("Yes").onClick.Invoke();
            yield return WaitForScene(GameConstants.Scenes.CharacterSelection);
            Assert.AreEqual(0, Session.money, "new career starts from zero");
        }

        // ---- settings ------------------------------------------------------------------------

        [UnityTest]
        public IEnumerator Settings_ApplyLive_AndBackReturnsToTheMenu()
        {
            yield return Load(GameConstants.Scenes.MainMenu);
            SettingsController settings = Object.FindAnyObjectByType<SettingsController>(FindObjectsInactive.Include);
            GameObject mainPanel = Field<GameObject>(Object.FindAnyObjectByType<MainMenuController>(), "mainPanel");

            ButtonNamed("Settings").onClick.Invoke();
            Assert.IsTrue(settings.IsOpen);
            Assert.IsFalse(mainPanel.activeSelf, "main buttons hide behind settings");

            Field<Slider>(settings, "mouseSensitivitySlider").value = 2.5f;
            Field<Slider>(settings, "cameraDistanceSlider").value = GameConstants.MaxCameraDistance;
            Field<Toggle>(settings, "invertYToggle").isOn = true;
            Toggle vSync = Field<Toggle>(settings, "vSyncToggle");
            vSync.isOn = !vSync.isOn;
            Assert.AreEqual(2.5f, Session.Settings.mouseSensitivity, 0.001f);
            Assert.AreEqual(GameConstants.MaxCameraDistance, Session.Settings.cameraDistance, 0.001f);
            Assert.IsTrue(Session.Settings.invertCameraY);
            Assert.AreEqual(Session.Settings.vSync ? 1 : 0, QualitySettings.vSyncCount, "V-Sync applied to the renderer");

            // Quality cycles through the levels available on the active build target.
            int originalLevel = QualitySettings.GetQualityLevel();
            Field<Button>(settings, "qualityNext").onClick.Invoke();
            Assert.AreEqual(Session.Settings.graphicsQuality, QualitySettings.GetQualityLevel(), "quality applied");
            Assert.AreEqual(QualitySettings.names[QualitySettings.GetQualityLevel()], Field<TMP_Text>(settings, "qualityLabel").text);
            Field<Button>(settings, "qualityPrevious").onClick.Invoke();
            Assert.AreEqual(originalLevel, QualitySettings.GetQualityLevel(), "previous undoes next");

            yield return Press(GamepadButton.East); // B = back
            Assert.IsFalse(settings.IsOpen);
            Assert.IsTrue(mainPanel.activeSelf);
        }

        // ---- pause + HUD ---------------------------------------------------------------------

        private static IEnumerator LoadWorld()
        {
            Session.Reset();
            Session.selectedCharacterId = "char_alex_morgan";
            SceneManager.LoadScene(GameConstants.Scenes.GameWorld, LoadSceneMode.Single);
            for (int i = 0; i < 6; i++) yield return null;
            NpcManager.Instance.DespawnAllCivilians();
        }

        [UnityTest]
        public IEnumerator Pause_Settings_Resume_AndMainMenu()
        {
            yield return LoadWorld();
            Assert.AreEqual(GameConstants.DefaultCameraDistance, Session.Settings.cameraDistance, 0.001f, "loading the settings panel must not change settings");
            Assert.AreEqual(1f, Session.Settings.mouseSensitivity, 0.001f, "loading the settings panel must not change settings");
            PauseMenuController pause = Object.FindAnyObjectByType<PauseMenuController>();
            SettingsController settings = Object.FindAnyObjectByType<SettingsController>(FindObjectsInactive.Include);

            yield return Press(GamepadButton.Start);
            Assert.IsTrue(GameManager.Instance.IsPaused, "Start should pause");
            Assert.IsTrue(pause.IsOpen);
            Assert.AreEqual(0f, Time.timeScale);

            ButtonNamed("Settings").onClick.Invoke();
            Assert.IsTrue(settings.IsOpen);
            Assert.IsFalse(pause.IsOpen, "pause card hides behind settings");

            yield return Press(GamepadButton.Start); // unpause straight from settings
            Assert.IsFalse(GameManager.Instance.IsPaused);
            Assert.IsFalse(settings.IsOpen);
            Assert.IsFalse(pause.IsOpen, "pause panel must not reappear after unpausing from settings");
            Assert.AreEqual(1f, Time.timeScale);

            yield return Press(GamepadButton.Start);
            ButtonNamed("Resume").onClick.Invoke();
            Assert.IsFalse(GameManager.Instance.IsPaused);
            Assert.IsFalse(pause.IsOpen);

            yield return Press(GamepadButton.Start);
            ButtonNamed("MainMenu").onClick.Invoke();
            yield return WaitForScene(GameConstants.Scenes.MainMenu);
            Assert.IsFalse(GameManager.Instance.IsPaused);
            Assert.AreEqual(1f, Time.timeScale);
        }

        [UnityTest]
        public IEnumerator Hud_PromptAppearsAtTheTerminal_AndDetectionMeterReactsToSuspicion()
        {
            yield return LoadWorld();
            Transform player = GameObject.FindGameObjectWithTag("Player").transform;
            Transform terminal = GameObject.Find("Interactables/SafehouseTerminal").transform;
            // HUD content lives under the canvas's safe-area container (Phase 9).
            GameObject prompt = System.Array.Find(GameObject.Find("GameHUD").GetComponentsInChildren<Transform>(true), t => t.name == "InteractionPrompt").gameObject;
            Assert.IsFalse(prompt.activeSelf, "no prompt away from interactables");

            Vector3 standAt = new Vector3(terminal.position.x, player.position.y + 0.05f, terminal.position.z + 1.3f);
            Vector3 look = terminal.position - standAt;
            look.y = 0f;
            player.SetPositionAndRotation(standAt, Quaternion.LookRotation(look));
            Physics.SyncTransforms();
            for (int i = 0; i < 4; i++) yield return null;
            Assert.IsTrue(prompt.activeSelf, "prompt should appear facing the terminal");
            StringAssert.Contains("E", prompt.GetComponentInChildren<TMP_Text>().text);

            DetectionMeterView meter = Object.FindAnyObjectByType<DetectionMeterView>();
            Assert.IsFalse(meter.Visible);
            GameEvents.RaiseSuspicionChanged("test_guard", 0.6f);
            yield return new WaitForSecondsRealtime(0.4f);
            Assert.IsTrue(meter.Visible, "meter fades in on suspicion");
            Assert.AreEqual(0.6f, Field<Image>(meter, "fill").fillAmount, 0.01f);
            GameEvents.RaiseNpcAlerted("test_guard");
            yield return null;
            StringAssert.Contains("SPOTTED", Field<TMP_Text>(meter, "label").text);
            GameEvents.RaiseSuspicionChanged("test_guard", 0f);
            yield return new WaitForSecondsRealtime(0.4f);
            Assert.IsFalse(meter.Visible, "meter fades out when suspicion clears");
        }

        [UnityTest]
        public IEnumerator SceneLoads_FadeThroughBlack_AndClearAfterwards()
        {
            yield return Load(GameConstants.Scenes.MainMenu);
            GameManager.Instance.SceneLoader.LoadCharacterSelection();
            yield return new WaitForSecondsRealtime(0.15f);
            CanvasGroup fader = GameManager.Instance.SceneLoader.GetComponentInChildren<CanvasGroup>(true);
            Assert.IsNotNull(fader, "screen fader");
            Assert.Greater(fader.alpha, 0.2f, "fading out during the load");
            yield return WaitForScene(GameConstants.Scenes.CharacterSelection);
            Assert.Greater(fader.alpha, 0f, "new scene fades in rather than popping");
            yield return new WaitForSecondsRealtime(0.5f); // fade-in is 0.35 s
            Assert.AreEqual(0f, fader.alpha, 0.001f);
            Assert.IsFalse(fader.blocksRaycasts, "fader must not swallow clicks after the load");
        }
    }
}
