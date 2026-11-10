using Contract.Core;
using Contract.Interaction;
using Contract.Player;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Contract.UI.Touch
{
    public enum TouchControlsMode { Auto = 0, Always = 1, Never = 2 }

    /// <summary>
    /// Android touchscreen controls for the gameplay scenes. Left: floating joystick. Right:
    /// drag-to-look surface with Jump, Sprint (toggle), Crouch (toggle) and Interact (tap or hold).
    /// Top: Pause and Map. Everything feeds the same InputReader the keyboard, mouse and gamepad
    /// use, so the player controller, camera and interaction code are shared by every device.
    /// Shown on touch devices (Auto), or always / never from the settings; hidden while a menu,
    /// the map or the pause screen is open. In GPS mode the walking route moves the operative, so
    /// the joystick and movement buttons hide (no simultaneous GPS and joystick movement).
    /// </summary>
    public sealed class TouchControls : MonoBehaviour
    {
        [SerializeField] private InputReader inputReader;
        [SerializeField] private CanvasGroup group;
        [SerializeField] private VirtualJoystick joystick;
        [SerializeField] private TouchLookArea lookArea;
        [SerializeField] private TouchButton jumpButton;
        [SerializeField] private TouchButton sprintButton;
        [SerializeField] private TouchButton crouchButton;
        [SerializeField] private TouchButton interactButton;
        [SerializeField] private TouchButton pauseButton;
        [SerializeField] private TouchButton mapButton;
        [SerializeField] private Image interactProgress;
        [SerializeField] private GameObject movementGroup;
        [SerializeField, Tooltip("Keyboard hints hidden while the touch controls show.")] private GameObject keyboardHints;
        [SerializeField, Tooltip("Sprint switches off after the joystick has been released this long (s).")] private float sprintReleaseDelay = 0.5f;

        private ThirdPersonController player;
        private InteractionController interaction;
        private bool visible;
        private float joystickIdle;

        public bool Visible => visible;
        public VirtualJoystick Joystick => joystick;
        public TouchLookArea LookArea => lookArea;
        public TouchButton Jump => jumpButton;
        public TouchButton Sprint => sprintButton;
        public TouchButton Crouch => crouchButton;
        public TouchButton Interact => interactButton;
        public TouchButton Pause => pauseButton;
        public TouchButton Map => mapButton;
        public bool MovementVisible => movementGroup == null || movementGroup.activeSelf;

        private static GameSettings Settings => GameManager.Instance?.Session?.Settings;

        /// <summary>Should touch controls show for this mode on this device?</summary>
        public static bool Wanted(TouchControlsMode mode) => InputHints.WantedFor((int)mode);

        private void Awake()
        {
            joystick.Bind(inputReader);
            lookArea.Bind(inputReader);
            jumpButton.Pressed += () => inputReader.TouchJump();
            interactButton.Pressed += () =>
            {
                inputReader.SetTouchInteractHeld(true);
                inputReader.TouchInteract();
            };
            interactButton.Released += () => inputReader.SetTouchInteractHeld(false);
            sprintButton.Toggled += on =>
            {
                inputReader.SetTouchSprint(on);
                if (on && crouchButton.On) SetCrouch(false); // sprinting stands up
            };
            crouchButton.Toggled += on =>
            {
                inputReader.SetTouchCrouch(on);
                if (on && sprintButton.On) SetSprint(false);
            };
            pauseButton.Pressed += () => inputReader.TouchPause();
            mapButton.Pressed += () => inputReader.TouchOpenMap();
            SetVisible(false, force: true);
        }

        private void OnDisable()
        {
            ReleaseAll();
        }

        private void Update()
        {
            GameSettings settings = Settings;
            var mode = (TouchControlsMode)(settings?.touchControls ?? 0);
            bool show = Wanted(mode) && GameConstants.Scenes.InGameplay && GameManager.Instance != null && !GameManager.Instance.InputBlocked;
            SetVisible(show);
            // Keyboard hints follow the control scheme, not the visibility (paused on a phone: still no WASD hints).
            if (keyboardHints != null && keyboardHints.activeSelf == Wanted(mode)) keyboardHints.SetActive(!Wanted(mode));
            if (!visible) return;

            ResolvePlayer();
            // Toggles follow the input layer (it is cleared when gameplay input is disabled).
            if (sprintButton.On != inputReader.TouchSprint) sprintButton.On = inputReader.TouchSprint;
            if (crouchButton.On != inputReader.TouchCrouch) crouchButton.On = inputReader.TouchCrouch;
            if (settings != null)
            {
                joystick.DeadZone = settings.joystickDeadZone;
                joystick.Sensitivity = settings.joystickSensitivity;
                if (!joystick.Held && !Mathf.Approximately(joystick.Size, settings.joystickSize)) joystick.Size = settings.joystickSize;
                group.alpha = Mathf.Clamp(settings.buttonOpacity, 0.3f, 1f);
                lookArea.SensitivityX = settings.touchLookSensitivityX;
                lookArea.SensitivityY = settings.touchLookSensitivityY;
            }

            // GPS mode drives the operative: hide the joystick and movement buttons.
            bool driven = player != null && player.Driver != null && player.Driver.DriverActive;
            if (movementGroup != null && movementGroup.activeSelf == driven)
            {
                movementGroup.SetActive(!driven);
                if (driven) ReleaseMovement();
            }

            // Sprint is a toggle that ends when the joystick has been let go.
            joystickIdle = joystick.Held ? 0f : joystickIdle + Time.unscaledDeltaTime;
            if (sprintButton.On && joystickIdle > sprintReleaseDelay) SetSprint(false);

            // Interact: bright when something is in reach; hold progress ring for terminals.
            interactButton.Dim = interaction != null && interaction.Current != null ? 1f : 0.35f;
            if (interactProgress != null)
            {
                float progress = interaction != null ? interaction.HoldProgress : -1f;
                interactProgress.enabled = progress >= 0f;
                interactProgress.fillAmount = Mathf.Max(0f, progress);
            }
        }

        private void ResolvePlayer()
        {
            if (player != null) return;
            GameObject go = GameObject.FindGameObjectWithTag("Player");
            if (go == null) return;
            go.TryGetComponent(out player);
            go.TryGetComponent(out interaction);
        }

        private void SetVisible(bool show, bool force = false)
        {
            if (show == visible && !force) return;
            visible = show;
            group.alpha = show ? 1f : 0f;
            group.blocksRaycasts = show;
            group.interactable = show;
            if (!show) ReleaseFingers(); // menus / map / pause: fingers let go, crouch and sprint toggles kept
        }

        private void SetSprint(bool on)
        {
            sprintButton.On = on;
            inputReader.SetTouchSprint(on);
        }

        private void SetCrouch(bool on)
        {
            crouchButton.On = on;
            inputReader.SetTouchCrouch(on);
        }

        private void ReleaseMovement()
        {
            joystick.Release();
            SetSprint(false);
            SetCrouch(false);
            jumpButton.Release();
        }

        /// <summary>Lets go of every finger (joystick, look, held buttons) without changing toggles.</summary>
        public void ReleaseFingers()
        {
            if (joystick != null) joystick.Release();
            if (lookArea != null) lookArea.Release();
            foreach (TouchButton b in new[] { jumpButton, interactButton, pauseButton, mapButton })
                if (b != null) b.Release();
            if (inputReader != null)
            {
                inputReader.SetTouchMove(Vector2.zero);
                inputReader.SetTouchInteractHeld(false);
            }
        }

        /// <summary>Every finger released and every touch input (including toggles) cleared.</summary>
        public void ReleaseAll()
        {
            ReleaseFingers();
            if (sprintButton != null) sprintButton.On = false;
            if (crouchButton != null) crouchButton.On = false;
            if (inputReader != null) inputReader.ClearTouch();
        }
    }
}
