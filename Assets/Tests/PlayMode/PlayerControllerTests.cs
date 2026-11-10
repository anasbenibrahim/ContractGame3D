using System.Collections;
using System.Linq;
using Contract.CameraSystem;
using Contract.Core;
using Contract.Data;
using Contract.Player;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Contract.Tests
{
    /// <summary>
    /// Stage 2 verification in the real GameWorld scene.
    ///
    /// Input is driven through a virtual Gamepad added to the live Input System, so it flows through
    /// the game's own ContractInputActions -> InputReader -> controller/camera path. A gamepad is used
    /// because the headless editor routes keyboard/mouse events to the (non-existent) focused Game
    /// View; gamepads are not focus-gated. The keyboard/mouse mapping itself is verified separately by
    /// <see cref="KeyboardAndMouseBindings_MatchControlScheme"/>.
    /// </summary>
    public sealed class PlayerControllerTests
    {
        private Gamepad gamepad;
        private GamepadState padState;
        private InputSettings.BackgroundBehavior savedBackgroundBehavior;
        private ThirdPersonController player;
        private ThirdPersonCamera playerCamera;

        [SetUp]
        public void Setup()
        {
            // The headless editor never has focus; keep devices enabled anyway.
            savedBackgroundBehavior = InputSystem.settings.backgroundBehavior;
            InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;

            gamepad = InputSystem.AddDevice<Gamepad>("TestGamepad");
            padState = new GamepadState();
        }

        [TearDown]
        public void TearDown()
        {
            if (GameManager.Instance != null) GameManager.Instance.SetPaused(false);
            if (gamepad != null) InputSystem.RemoveDevice(gamepad);
            InputSystem.settings.backgroundBehavior = savedBackgroundBehavior;
        }

        // ---- virtual gamepad helpers ------------------------------------------------------

        private void Send() => InputSystem.QueueStateEvent(gamepad, padState);

        private void LeftStick(Vector2 value) { padState.leftStick = value; Send(); }

        private void RightStick(Vector2 value) { padState.rightStick = value; Send(); }

        private void Button(GamepadButton button, bool down) { padState = padState.WithButton(button, down); Send(); }

        private IEnumerator Tap(GamepadButton button)
        {
            Button(button, true);
            yield return null;
            Button(button, false);
            yield return null;
        }

        private IEnumerator LoadWorld(string characterId = "char_alex_morgan")
        {
            GameManager.Instance.SetPaused(false);
            GameManager.Instance.Session.selectedCharacterId = characterId;
            GameManager.Instance.Session.Settings = new GameSettings();
            SceneManager.LoadScene(GameConstants.Scenes.GameWorld, LoadSceneMode.Single);
            yield return null;
            yield return null;

            Contract.NPCs.NpcManager.Instance?.DespawnAllCivilians(); // movement tests need a clear pavement
            player = Object.FindAnyObjectByType<ThirdPersonController>();
            playerCamera = Object.FindAnyObjectByType<ThirdPersonCamera>();
            Assert.IsNotNull(player, "Player not found in GameWorld");
            Assert.IsNotNull(playerCamera, "ThirdPersonCamera not found in GameWorld");

            for (int i = 0; i < 120 && !player.Grounded; i++)
            {
                yield return null;
            }

            Assert.IsTrue(player.Grounded, "Player never landed on the ground");
        }

        private static Vector3 Flat(Vector3 v) => new Vector3(v.x, 0f, v.z);

        // ---- tests --------------------------------------------------------------------------

        [Test]
        public void KeyboardAndMouseBindings_MatchControlScheme()
        {
            InputActionAsset actions = InputSystem.actions;
            Assert.IsNotNull(actions, "No project-wide input actions configured");
            Assert.AreEqual("ContractInputActions", actions.name);

            void Expect(string action, params string[] paths)
            {
                InputAction a = actions.FindAction("Player/" + action, true);
                string[] bound = a.bindings.Select(b => b.path).ToArray();
                foreach (string path in paths)
                {
                    CollectionAssert.Contains(bound, path, $"{action} is not bound to {path}");
                }
            }

            Expect("Move", "<Keyboard>/w", "<Keyboard>/a", "<Keyboard>/s", "<Keyboard>/d");
            Expect("Sprint", "<Keyboard>/leftShift");
            Expect("Crouch", "<Keyboard>/leftCtrl");
            Expect("Jump", "<Keyboard>/space");
            Expect("Interact", "<Keyboard>/e");
            Expect("OpenMap", "<Keyboard>/m");
            Expect("Pause", "<Keyboard>/escape");
            Expect("Look", "<Mouse>/delta");
            // Touches must not drive the camera as a pointer (a finger on the joystick would turn it):
            // touch look comes from the on-screen look surface instead (Phase 8).
            CollectionAssert.DoesNotContain(actions.FindAction("Player/Look", true).bindings.Select(b => b.path).ToArray(), "<Pointer>/delta");
            Expect("Zoom", "<Mouse>/scroll/y");
        }

        [UnityTest]
        public IEnumerator Move_GoesCameraRelativeForward()
        {
            yield return LoadWorld();
            Vector3 start = player.transform.position;
            Vector3 cameraForward = Flat(playerCamera.transform.forward).normalized;

            LeftStick(Vector2.up);
            yield return new WaitForSeconds(0.8f);
            LeftStick(Vector2.zero);

            Vector3 moved = Flat(player.transform.position - start);
            Assert.Greater(moved.magnitude, 2f, $"Player only moved {moved.magnitude:F2} m");
            Assert.Greater(Vector3.Dot(moved.normalized, cameraForward), 0.9f, "Movement is not camera-relative forward");
        }

        [UnityTest]
        public IEnumerator Sprint_IsFasterThanWalk()
        {
            yield return LoadWorld();

            LeftStick(Vector2.up);
            yield return new WaitForSeconds(0.6f);
            float walkSpeed = player.PlanarSpeed;
            LeftStick(Vector2.zero);
            yield return new WaitForSeconds(0.3f);

            Button(GamepadButton.LeftStick, true);
            LeftStick(Vector2.down); // come back the other way to stay clear of obstacles
            yield return new WaitForSeconds(0.6f);
            float sprintSpeed = player.PlanarSpeed;
            bool sprinting = player.Sprinting;
            LeftStick(Vector2.zero);
            Button(GamepadButton.LeftStick, false);

            Assert.IsTrue(sprinting, "Sprint flag not set while sprint held");
            Assert.AreEqual(player.WalkSpeed, walkSpeed, 0.3f, "Walk did not reach walk speed");
            Assert.Greater(sprintSpeed, walkSpeed * 1.4f, $"Sprint {sprintSpeed:F2} not faster than walk {walkSpeed:F2}");
        }

        [UnityTest]
        public IEnumerator Crouch_LowersCapsuleAndSpeed_ThenStandsUp()
        {
            yield return LoadWorld();
            CharacterController capsule = player.GetComponent<CharacterController>();
            float standingHeight = capsule.height;

            Button(GamepadButton.East, true);
            yield return new WaitForSeconds(0.4f);
            Assert.IsTrue(player.IsCrouching, "Not crouching while crouch held");
            Assert.Less(capsule.height, standingHeight - 0.4f, "Capsule did not shrink");
            Assert.Greater(player.CrouchAmount, 0.95f, "Crouch blend incomplete");

            LeftStick(Vector2.up);
            yield return new WaitForSeconds(0.5f);
            float crouchSpeed = player.PlanarSpeed;
            LeftStick(Vector2.zero);
            Button(GamepadButton.East, false);
            Assert.Greater(crouchSpeed, 0.5f, "Cannot move while crouched");
            Assert.Less(crouchSpeed, player.WalkSpeed * 0.75f, "Crouch-walk is not slower than walking");

            yield return new WaitForSeconds(0.4f);
            Assert.IsFalse(player.IsCrouching, "Still crouching after crouch released");
            Assert.AreEqual(standingHeight, capsule.height, 0.01f, "Capsule did not return to standing height");
        }

        [UnityTest]
        public IEnumerator Jump_LeavesGroundAndLands()
        {
            yield return LoadWorld();
            float groundY = player.transform.position.y;

            yield return Tap(GamepadButton.South);

            float peak = groundY;
            bool leftGround = false;
            for (float t = 0f; t < 2f; t += Time.deltaTime)
            {
                peak = Mathf.Max(peak, player.transform.position.y);
                leftGround |= !player.Grounded;
                if (leftGround && player.Grounded) break;
                yield return null;
            }

            Assert.IsTrue(leftGround, "Player never left the ground");
            Assert.Greater(peak - groundY, 0.8f, $"Jump apex only {peak - groundY:F2} m");
            Assert.IsTrue(player.Grounded, "Player did not land");
        }

        [UnityTest]
        public IEnumerator Look_RotatesCamera()
        {
            yield return LoadWorld();
            float startYaw = playerCamera.Yaw;
            float startPitch = playerCamera.Pitch;

            RightStick(new Vector2(1f, 0.6f));
            yield return new WaitForSeconds(0.3f);
            RightStick(Vector2.zero);

            Assert.Greater(playerCamera.Yaw - startYaw, 10f, "Horizontal look did not turn the camera");
            Assert.Less(playerCamera.Pitch, startPitch, "Vertical look did not tilt the camera");
        }

        [UnityTest]
        public IEnumerator Zoom_ChangesCameraDistance_AndIsStoredInSettings()
        {
            yield return LoadWorld();
            float start = playerCamera.TargetDistance;

            Button(GamepadButton.DpadUp, true);
            yield return new WaitForSeconds(0.4f);
            Button(GamepadButton.DpadUp, false);
            yield return null;

            Assert.Less(playerCamera.TargetDistance, start - 0.3f, "Zoom-in did not move the camera closer");
            Assert.AreEqual(playerCamera.TargetDistance, GameManager.Instance.Session.Settings.cameraDistance, 0.001f,
                "Zoom level not stored in settings");
        }

        [UnityTest]
        public IEnumerator Settings_ChangeCameraSensitivityAndDistance()
        {
            yield return LoadWorld();
            GameSettings settings = GameManager.Instance.Session.Settings;

            settings.mouseSensitivity = 1f;
            GameEvents.RaiseSettingsApplied();
            float baseSensitivity = playerCamera.MouseSensitivity;

            settings.mouseSensitivity = 2f;
            settings.cameraDistance = 3f;
            GameEvents.RaiseSettingsApplied();

            Assert.AreEqual(baseSensitivity * 2f, playerCamera.MouseSensitivity, 0.0001f);
            Assert.AreEqual(3f, playerCamera.TargetDistance, 0.0001f);
        }

        [UnityTest]
        public IEnumerator Pause_TogglesGameAndTimeScale()
        {
            yield return LoadWorld();

            yield return Tap(GamepadButton.Start);
            Assert.IsTrue(GameManager.Instance.IsPaused, "Pause did not pause");
            Assert.AreEqual(0f, Time.timeScale);

            yield return Tap(GamepadButton.Start);
            Assert.IsFalse(GameManager.Instance.IsPaused, "Pause did not resume");
            Assert.AreEqual(1f, Time.timeScale);
        }

        [UnityTest]
        public IEnumerator SelectedCharacter_ChangesStatsAndAppearance(
            [Values("char_alex_morgan", "char_daniel_cross", "char_ryan_blake")] string characterId)
        {
            yield return LoadWorld(characterId);
            PlayerCharacterApplier applier = player.GetComponent<PlayerCharacterApplier>();
            Assert.IsNotNull(applier, "PlayerCharacterApplier missing from the Player prefab");

            CharacterData data = applier.CurrentCharacter;
            Assert.AreEqual(characterId, data.characterId, "Wrong character applied");

            // Stats -> movement.
            Assert.AreEqual(4.5f * CharacterData.StatToMultiplier(data.moveSpeed), player.WalkSpeed, 0.001f, "Move stat not applied");

            // Appearance: articulated rig replaced the legacy primitive body.
            Assert.IsNotNull(applier.CurrentVisual.GetComponent<PlaceholderRig>(), "Placeholder rig not built");
            Assert.AreEqual(1, player.VisualRoot.childCount, "Legacy primitive body was not removed");
            Assert.Greater(applier.CurrentVisual.GetComponentsInChildren<MeshRenderer>().Length, 30, "Rig suspiciously simple");

            Material top = applier.CurrentVisual.GetComponentsInChildren<MeshRenderer>()
                .Select(r => r.sharedMaterial).First(m => m.name == "Top");
            Color applied = top.GetColor("_BaseColor");
            Color expected = data.appearance.topColor;
            Assert.Less(Mathf.Abs(applied.r - expected.r) + Mathf.Abs(applied.g - expected.g) + Mathf.Abs(applied.b - expected.b), 0.01f,
                $"Outfit colour not applied: {applied} vs {expected}");
        }

        [UnityTest]
        public IEnumerator PlaceholderRig_AnimatesWhileWalking_AndCrouchPoseLowersHips()
        {
            yield return LoadWorld();
            Transform[] bones = player.VisualRoot.GetComponentsInChildren<Transform>();
            Transform thigh = bones.First(t => t.name == "ThighL");
            Transform hips = bones.First(t => t.name == "Hips");
            Quaternion idle = thigh.localRotation;
            float standingHipsY = hips.position.y - player.transform.position.y;

            LeftStick(Vector2.up);
            float maxAngle = 0f;
            for (float t = 0f; t < 0.8f; t += Time.deltaTime)
            {
                maxAngle = Mathf.Max(maxAngle, Quaternion.Angle(idle, thigh.localRotation));
                yield return null;
            }

            LeftStick(Vector2.zero);
            Assert.Greater(maxAngle, 15f, "Legs did not swing while walking");

            Button(GamepadButton.East, true);
            yield return new WaitForSeconds(0.4f);
            float crouchHipsY = hips.position.y - player.transform.position.y;
            Button(GamepadButton.East, false);
            Assert.Less(crouchHipsY, standingHipsY - 0.3f, "Crouch pose did not lower the hips");
        }
    }
}
