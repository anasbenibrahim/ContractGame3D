using System.Collections;
using Contract.CameraSystem;
using Contract.Contracts;
using Contract.Core;
using Contract.Map;
using Contract.NPCs;
using Contract.Player;
using Contract.SaveSystem;
using Contract.UI.Touch;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using TouchPhase = UnityEngine.InputSystem.TouchPhase;

namespace Contract.Tests
{
    /// <summary>
    /// Phase 8: touchscreen controls driven by a virtual touchscreen through the real EventSystem
    /// (InputSystemUIInputModule), with several fingers at once, into the same InputReader,
    /// player controller and camera the keyboard and gamepad use.
    /// </summary>
    public sealed class TouchControlsTests
    {
        private Touchscreen screen;
        private InputSettings.BackgroundBehavior savedBackground;
        private InputSettings.EditorInputBehaviorInPlayMode savedEditorBehaviour;
        private TouchControls controls;
        private ThirdPersonController player;
        private ThirdPersonCamera cam;
        private InputReader reader;

        private static GameSession Session => GameManager.Instance.Session;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            savedBackground = InputSystem.settings.backgroundBehavior;
            InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            // Batch mode has no focused Game view: let pointer (touch) input reach the game anyway.
            savedEditorBehaviour = InputSystem.settings.editorInputBehaviorInPlayMode;
            InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            screen = InputSystem.AddDevice<Touchscreen>("TestTouchscreen");
            SceneManager.LoadScene(GameConstants.Scenes.MainMenu);
            yield return null;
            yield return null;
            Session.Reset();
            Session.hasStarted = true;
            Session.selectedCharacterId = "char_alex_morgan";
            Session.Settings.touchControls = (int)TouchControlsMode.Always;
            SceneManager.LoadScene(GameConstants.Scenes.GameWorld);
            for (int i = 0; i < 10; i++) yield return null;
            NpcManager.Instance?.DespawnAllCivilians();
            controls = Object.FindAnyObjectByType<TouchControls>();
            player = GameObject.FindGameObjectWithTag("Player").GetComponent<ThirdPersonController>();
            cam = Object.FindAnyObjectByType<ThirdPersonCamera>();
            reader = (InputReader)typeof(TouchControls).GetField("inputReader", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).GetValue(controls);
            yield return new WaitForSeconds(0.5f); // settle on the ground
        }

        [TearDown]
        public void TearDown()
        {
            if (screen != null) InputSystem.RemoveDevice(screen);
            InputSystem.settings.backgroundBehavior = savedBackground;
            InputSystem.settings.editorInputBehaviorInPlayMode = savedEditorBehaviour;
            GameManager.Instance?.SetPaused(false);
            GameManager.Instance?.CloseMap();
            reader?.ClearTouch();
            GameManager.Instance?.Session.Reset();
            GameManager.Instance?.GetService<ISaveService>()?.DeleteSlot(GameConstants.Save.DefaultSlot);
        }

        // ---- touch helpers --------------------------------------------------------------------

        private void Touch(int id, TouchPhase phase, Vector2 position, Vector2 delta = default) =>
            InputSystem.QueueStateEvent(screen, new TouchState { touchId = id, phase = phase, position = position, delta = delta });

        private static IEnumerator Frames(int n = 2)
        {
            for (int i = 0; i < n; i++) yield return null;
        }

        /// <summary>Drags a finger in small steps (so the UI module produces drag events).</summary>
        private IEnumerator Drag(int id, Vector2 from, Vector2 to, int steps = 8)
        {
            Vector2 last = from;
            for (int i = 1; i <= steps; i++)
            {
                Vector2 p = Vector2.Lerp(from, to, i / (float)steps);
                Touch(id, TouchPhase.Moved, p, p - last);
                last = p;
                yield return null;
            }
        }

        private static Vector2 ScreenOf(Component c) => RectTransformUtility.WorldToScreenPoint(null, ((RectTransform)c.transform).position);

        private Vector2 JoystickStart => new Vector2(Screen.width * 0.15f, Screen.height * 0.3f);
        private Vector2 LookStart => new Vector2(Screen.width * 0.6f, Screen.height * 0.6f);
        private float JoystickPixels => controls.Joystick.Radius * controls.Joystick.Zone.lossyScale.x;

        // ---- joystick ---------------------------------------------------------------------------

        [Test]
        public void JoystickShaping_DeadZoneRescalesAndSensitivityClamps()
        {
            Assert.AreEqual(Vector2.zero, VirtualJoystick.Shape(new Vector2(0.1f, 0f), 0.12f, 1f), "inside the dead zone");
            Assert.AreEqual(0.5f, VirtualJoystick.Shape(new Vector2(0f, 0.56f), 0.12f, 1f).y, 1e-4f, "rescaled from the dead-zone edge");
            Assert.AreEqual(1f, VirtualJoystick.Shape(new Vector2(0f, 1f), 0.12f, 1f).magnitude, 1e-4f);
            Assert.AreEqual(1f, VirtualJoystick.Shape(new Vector2(0.7f, 0f), 0.12f, 2f).magnitude, 1e-4f, "sensitivity never exceeds full speed");
            Assert.Less(VirtualJoystick.Shape(new Vector2(0.5f, 0f), 0f, 0.5f).magnitude, 0.26f, "low sensitivity");
            Assert.AreEqual(90f, TouchLookArea.ToDegrees(new Vector2(540f, 0f), 1080f, 1f, 1f).x, 1e-3f, "half the screen height = 90°");
        }

        [UnityTest]
        public IEnumerator Joystick_MovesThePlayer_AndResetsWhenTheFingerLifts()
        {
            Assert.IsTrue(controls.Visible, "touch controls shown (mode Always) in the city");
            Vector3 start = player.transform.position;
            Touch(1, TouchPhase.Began, JoystickStart);
            yield return Frames();
            Assert.IsTrue(controls.Joystick.Held, "finger captured by the joystick");
            yield return Drag(1, JoystickStart, JoystickStart + Vector2.up * JoystickPixels * 1.5f);
            Assert.Greater(reader.Move.y, 0.95f, "full deflection forward");
            yield return new WaitForSeconds(1f);
            Assert.Greater(Vector3.Distance(Flat(start), Flat(player.transform.position)), 1.5f, "operative walked");

            Touch(1, TouchPhase.Ended, JoystickStart + Vector2.up * JoystickPixels * 1.5f);
            yield return Frames();
            Assert.IsFalse(controls.Joystick.Held);
            Assert.AreEqual(Vector2.zero, reader.Move, "released joystick stops the operative");
            Assert.AreEqual(Vector2.zero, controls.Joystick.Value);
            Vector3 stopped = player.transform.position;
            yield return new WaitForSeconds(0.5f);
            Assert.Less(Vector3.Distance(Flat(stopped), Flat(player.transform.position)), 0.6f, "no drift after release");
        }

        private static Vector3 Flat(Vector3 v) => new Vector3(v.x, 0f, v.z);

        // ---- camera and multi-touch -----------------------------------------------------------------

        [UnityTest]
        public IEnumerator LookDrag_RotatesTheCamera_WhileAnotherFingerHoldsTheJoystick()
        {
            Touch(1, TouchPhase.Began, JoystickStart);
            yield return Frames();
            yield return Drag(1, JoystickStart, JoystickStart + Vector2.right * JoystickPixels);

            float yaw = cam.Yaw;
            Touch(2, TouchPhase.Began, LookStart);
            yield return Frames();
            Assert.IsTrue(controls.LookArea.Held, "second finger on the look surface");
            yield return Drag(2, LookStart, LookStart + new Vector2(Screen.height * 0.25f, 0f));
            yield return Frames();
            Assert.AreEqual(45f, Mathf.DeltaAngle(yaw, cam.Yaw), 6f, "quarter screen-height drag = 45° at sensitivity 1");
            Assert.IsTrue(controls.Joystick.Held, "the joystick finger is unaffected");
            Assert.Greater(reader.Move.x, 0.9f);

            Touch(2, TouchPhase.Ended, LookStart);
            Touch(1, TouchPhase.Ended, JoystickStart);
            yield return Frames();
            Assert.IsFalse(controls.LookArea.Held);
            Assert.IsFalse(controls.Joystick.Held);
        }

        [UnityTest]
        public IEnumerator DraggingFromAButton_DoesNotMoveTheCamera()
        {
            Vector2 jump = ScreenOf(controls.Jump);
            float yaw = cam.Yaw;
            int jumps = 0;
            System.Action onJump = () => jumps++;
            reader.JumpPressed += onJump;
            Touch(3, TouchPhase.Began, jump);
            yield return Frames();
            yield return Drag(3, jump, jump + new Vector2(-Screen.height * 0.3f, 0f));
            Touch(3, TouchPhase.Ended, jump);
            yield return Frames(3);
            reader.JumpPressed -= onJump;
            Assert.AreEqual(1, jumps, "jump fired once, on touch down");
            Assert.AreEqual(0f, Mathf.DeltaAngle(yaw, cam.Yaw), 0.5f, "camera did not move");
        }

        // ---- buttons --------------------------------------------------------------------------------

        [UnityTest]
        public IEnumerator CrouchAndSprint_AreToggles_ThatCancelEachOther()
        {
            Vector2 crouch = ScreenOf(controls.Crouch), sprint = ScreenOf(controls.Sprint);
            Touch(4, TouchPhase.Began, crouch);
            yield return Frames();
            Touch(4, TouchPhase.Ended, crouch);
            yield return new WaitForSeconds(0.3f);
            Assert.IsTrue(controls.Crouch.On);
            Assert.IsTrue(player.IsCrouching, "tap toggles crouch on (no holding)");

            // Sprinting while moving stands the operative up.
            Touch(1, TouchPhase.Began, JoystickStart);
            yield return Frames();
            yield return Drag(1, JoystickStart, JoystickStart + Vector2.up * JoystickPixels * 1.5f);
            Touch(5, TouchPhase.Began, sprint);
            yield return Frames();
            Touch(5, TouchPhase.Ended, sprint);
            yield return new WaitForSeconds(0.5f);
            Assert.IsTrue(controls.Sprint.On);
            Assert.IsFalse(controls.Crouch.On, "sprint cancels crouch");
            Assert.IsTrue(player.IsSprinting, "sprinting with the joystick held");

            Touch(1, TouchPhase.Ended, JoystickStart);
            yield return new WaitForSeconds(1f);
            Assert.IsFalse(controls.Sprint.On, "sprint ends once the joystick is released");
            Assert.IsFalse(reader.IsSprinting);
        }

        [UnityTest]
        public IEnumerator Interact_TapFiresAndHoldIsReported_WithHaptics()
        {
            Vector2 interact = ScreenOf(controls.Interact);
            int presses = 0;
            System.Action onInteract = () => presses++;
            reader.InteractPressed += onInteract;
            int ticks = Haptics.RequestCount;
            Touch(6, TouchPhase.Began, interact);
            yield return Frames(3);
            Assert.AreEqual(1, presses);
            Assert.IsTrue(reader.IsInteractHeld, "holding the button holds Interact (terminal hacks)");
            Touch(6, TouchPhase.Ended, interact);
            yield return Frames();
            reader.InteractPressed -= onInteract;
            Assert.IsFalse(reader.IsInteractHeld);
            Assert.Greater(Haptics.RequestCount, ticks, "haptic tick requested on press");

            Session.Settings.haptics = false;
            ticks = Haptics.RequestCount;
            Touch(7, TouchPhase.Began, interact);
            yield return Frames();
            Touch(7, TouchPhase.Ended, interact);
            yield return Frames();
            Assert.AreEqual(ticks, Haptics.RequestCount, "haptics off in the settings");
        }

        [UnityTest]
        public IEnumerator PauseButton_PausesAndHidesTheControls_ResumeShowsThem()
        {
            Touch(1, TouchPhase.Began, JoystickStart);
            yield return Frames();
            Vector2 pause = ScreenOf(controls.Pause);
            Touch(8, TouchPhase.Began, pause);
            yield return Frames(3);
            Touch(8, TouchPhase.Ended, pause);
            yield return Frames();
            Assert.IsTrue(GameManager.Instance.IsPaused, "pause button pauses");
            Assert.IsFalse(controls.Visible, "controls hidden behind the pause menu");
            Assert.IsFalse(controls.Joystick.Held, "fingers released when hiding");
            Assert.AreEqual(Vector2.zero, reader.Move);
            Touch(1, TouchPhase.Ended, JoystickStart);

            GameManager.Instance.SetPaused(false);
            yield return Frames(3);
            Assert.IsTrue(controls.Visible);
        }

        [UnityTest]
        public IEnumerator MapButton_OpensTheMap_AndPinchZooms()
        {
            Vector2 mapButton = ScreenOf(controls.Map);
            Touch(9, TouchPhase.Began, mapButton);
            yield return Frames(3);
            Touch(9, TouchPhase.Ended, mapButton);
            yield return Frames(4);
            FullMapView map = Object.FindAnyObjectByType<FullMapView>();
            Assert.IsTrue(map != null && map.IsOpen, "map opened from the touch button");
            Assert.IsFalse(controls.Visible);

            float zoom = map.Zoom;
            Vector2 c = new Vector2(Screen.width / 2f, Screen.height / 2f);
            float gap = Screen.height * 0.08f;
            Touch(10, TouchPhase.Began, c - Vector2.right * gap);
            Touch(11, TouchPhase.Began, c + Vector2.right * gap);
            yield return Frames();
            for (int i = 1; i <= 8; i++)
            {
                float g = gap * (1f + i * 0.1f);
                Touch(10, TouchPhase.Moved, c - Vector2.right * g);
                Touch(11, TouchPhase.Moved, c + Vector2.right * g);
                yield return null;
            }

            Touch(10, TouchPhase.Ended, c);
            Touch(11, TouchPhase.Ended, c);
            yield return Frames();
            Assert.Greater(map.Zoom, zoom * 1.3f, "pinching out zooms in");
        }

        // ---- modes ----------------------------------------------------------------------------------

        private sealed class FakeGpsDriver : IMovementDriver
        {
            public bool DriverActive => true;
            public Vector3 DriveDirection => Vector3.zero;
            public bool DriveSprint => false;
        }

        [UnityTest]
        public IEnumerator GpsMode_HidesTheJoystick_SoTheyNeverMoveTogether()
        {
            IMovementDriver previous = player.Driver;
            Touch(1, TouchPhase.Began, JoystickStart);
            yield return Frames();
            player.Driver = new FakeGpsDriver();
            yield return Frames(2);
            Assert.IsFalse(controls.MovementVisible, "joystick, jump, sprint and crouch hidden in GPS mode");
            Assert.IsFalse(controls.Joystick.Held, "the joystick finger is released");
            Assert.AreEqual(Vector2.zero, reader.Move);
            Assert.IsTrue(controls.Visible, "camera, interact, map and pause stay");
            player.Driver = previous;
            yield return Frames(2);
            Assert.IsTrue(controls.MovementVisible);
            Touch(1, TouchPhase.Ended, JoystickStart);
        }

        [UnityTest]
        public IEnumerator NeverMode_HidesTouchControls_AndRestoresKeyboardHints()
        {
            var hints = (GameObject)typeof(TouchControls).GetField("keyboardHints", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).GetValue(controls);
            Assert.IsNotNull(hints, "keyboard hints wired");
            Assert.IsTrue(InputHints.Touch);
            Assert.IsTrue(hints == null || !hints.activeSelf, "keyboard hints hidden with touch controls");
            Assert.AreEqual("EXTRACT", InputHints.ForPrompt("[E] EXTRACT"), "no key label in touch prompts");

            Session.Settings.touchControls = (int)TouchControlsMode.Never;
            yield return Frames(2);
            Assert.IsFalse(controls.Visible);
            Assert.IsFalse(InputHints.Touch);
            Assert.AreEqual("[E] EXTRACT", InputHints.ForPrompt("[E] EXTRACT"));
            if (hints != null) Assert.IsTrue(hints.activeSelf, "keyboard hints back for PC");
            Assert.IsTrue(TouchControls.Wanted(TouchControlsMode.Always));
            Assert.IsFalse(TouchControls.Wanted(TouchControlsMode.Never));
        }

        [UnityTest]
        public IEnumerator TouchSettings_AreSaved_AndInvalidValuesClamped()
        {
            GameSettings s = Session.Settings;
            s.touchControls = 2;
            s.joystickDeadZone = 0.3f;
            s.joystickSensitivity = 1.5f;
            s.touchLookSensitivityX = 2f;
            s.touchLookSensitivityY = 9f; // out of range
            s.haptics = false;
            SaveManager.Instance.SaveSlot(GameConstants.Save.DefaultSlot);
            Session.Reset();
            Assert.IsTrue(SaveManager.Instance.LoadSlot(GameConstants.Save.DefaultSlot));
            s = Session.Settings;
            Assert.AreEqual(2, s.touchControls);
            Assert.AreEqual(0.3f, s.joystickDeadZone, 1e-4f);
            Assert.AreEqual(1.5f, s.joystickSensitivity, 1e-4f);
            Assert.AreEqual(2f, s.touchLookSensitivityX, 1e-4f);
            Assert.AreEqual(3f, s.touchLookSensitivityY, 1e-4f, "clamped to the allowed range");
            Assert.IsFalse(s.haptics);
            yield return null;
        }
    }
}
