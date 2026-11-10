using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Contract.Core
{
    [CreateAssetMenu(menuName = "CONTRACT/Input Reader", fileName = "InputReader")]
    public sealed class InputReader : ScriptableObject
    {
        [SerializeField] private InputActionAsset actions;

        private InputActionMap playerMap;
        private InputAction moveAction;
        private InputAction lookAction;
        private InputAction sprintAction;
        private InputAction crouchAction;
        private InputAction interactAction;
        private InputAction jumpAction;
        private InputAction openMapAction;
        private InputAction pauseAction;
        private InputAction zoomAction;
        private bool isInitialized;
        private bool eventsBound;

        public event Action InteractPressed;
        public event Action JumpPressed;
        public event Action OpenMapPressed;
        public event Action PausePressed;

        // ---- unified interface: devices (keyboard/mouse, gamepad) through the actions, plus the touch layer ----

        /// <summary>Movement from WASD / left stick / the touch joystick (magnitude 0-1).</summary>
        public Vector2 Move => Vector2.ClampMagnitude((moveAction != null ? moveAction.ReadValue<Vector2>() : Vector2.zero) + (TouchActive ? touchMove : Vector2.zero), 1f);
        /// <summary>Mouse delta or right stick. Touch look arrives separately in degrees (<see cref="ConsumeTouchLook"/>).</summary>
        public Vector2 Look => lookAction != null ? lookAction.ReadValue<Vector2>() : Vector2.zero;
        public bool IsSprinting => (sprintAction != null && sprintAction.IsPressed()) || (TouchActive && touchSprint);
        public bool IsCrouching => (crouchAction != null && crouchAction.IsPressed()) || (TouchActive && touchCrouch);
        public bool IsInteractHeld => (interactAction != null && interactAction.IsPressed()) || (TouchActive && touchInteractHeld);

        // ---- touch layer (written by the on-screen controls; ignored while gameplay input is disabled) ----

        [NonSerialized] private Vector2 touchMove;
        [NonSerialized] private Vector2 touchLook;
        [NonSerialized] private bool touchSprint, touchCrouch, touchInteractHeld;

        private bool TouchActive => GameplayEnabled;

        public void SetTouchMove(Vector2 value) => touchMove = Vector2.ClampMagnitude(value, 1f);

        /// <summary>Adds a camera rotation from a touch drag (degrees: x = yaw, y = pitch up).</summary>
        public void AddTouchLook(Vector2 degrees)
        {
            if (TouchActive) touchLook += degrees;
        }

        /// <summary>Touch rotation accumulated since the last call (the camera consumes it once per frame).</summary>
        public Vector2 ConsumeTouchLook()
        {
            Vector2 value = touchLook;
            touchLook = Vector2.zero;
            return TouchActive ? value : Vector2.zero;
        }

        public void SetTouchSprint(bool on) => touchSprint = on;
        public void SetTouchCrouch(bool on) => touchCrouch = on;
        public void SetTouchInteractHeld(bool held) => touchInteractHeld = held;
        public bool TouchSprint => touchSprint;
        public bool TouchCrouch => touchCrouch;

        public void TouchInteract() { if (TouchActive) InteractPressed?.Invoke(); }
        public void TouchJump() { if (TouchActive) JumpPressed?.Invoke(); }
        public void TouchOpenMap() { if (TouchActive) OpenMapPressed?.Invoke(); }
        public void TouchPause() { if (TouchActive) PausePressed?.Invoke(); }

        /// <summary>Releases every touch input (finger lifted, controls hidden, gameplay disabled).</summary>
        public void ClearTouch()
        {
            touchMove = touchLook = Vector2.zero;
            touchSprint = touchCrouch = touchInteractHeld = false;
        }

        /// <summary>Camera zoom axis (mouse wheel / d-pad). Positive = zoom in. Optional action.</summary>
        public float Zoom => zoomAction != null ? zoomAction.ReadValue<float>() : 0f;
        public bool GameplayEnabled => playerMap != null && playerMap.enabled;

        private void OnEnable()
        {
            Initialize();
            BindEvents();
        }

        private void OnValidate()
        {
            isInitialized = false;
            Initialize();
        }

        private void OnDisable()
        {
            DisableGameplay();
            UnbindEvents();
        }

        public void Initialize()
        {
            if (isInitialized)
            {
                return;
            }

            if (actions == null)
            {
                Debug.LogWarning("[InputReader] No InputActionAsset assigned.", this);
                return;
            }

            playerMap = actions.FindActionMap("Player", false);
            if (playerMap == null)
            {
                Debug.LogError("[InputReader] Missing 'Player' action map.", this);
                return;
            }

            moveAction = playerMap.FindAction("Move", false);
            lookAction = playerMap.FindAction("Look", false);
            sprintAction = playerMap.FindAction("Sprint", false);
            crouchAction = playerMap.FindAction("Crouch", false);
            interactAction = playerMap.FindAction("Interact", false);
            jumpAction = playerMap.FindAction("Jump", false);
            openMapAction = playerMap.FindAction("OpenMap", false);
            pauseAction = playerMap.FindAction("Pause", false);
            zoomAction = playerMap.FindAction("Zoom", false); // optional

            if (moveAction == null || lookAction == null || sprintAction == null || crouchAction == null
                || interactAction == null || jumpAction == null || openMapAction == null || pauseAction == null)
            {
                Debug.LogError("[InputReader] The Player map is missing one or more required actions.", this);
                return;
            }

            isInitialized = true;
        }

        public void EnableGameplay()
        {
            Initialize();
            // Binding here (not only in OnEnable) keeps Jump/Interact/Pause/Map working when the
            // asset was loaded before its actions were valid, or when domain reload is disabled.
            BindEvents();
            playerMap?.Enable();
        }

        public void DisableGameplay()
        {
            playerMap?.Disable();
            ClearTouch();
        }

        private void BindEvents()
        {
            if (eventsBound || !isInitialized)
            {
                return;
            }

            if (interactAction != null)
            {
                interactAction.performed += HandleInteract;
            }

            if (jumpAction != null)
            {
                jumpAction.performed += HandleJump;
            }

            if (openMapAction != null)
            {
                openMapAction.performed += HandleOpenMap;
            }

            if (pauseAction != null)
            {
                pauseAction.performed += HandlePause;
            }

            eventsBound = true;
        }

        private void UnbindEvents()
        {
            if (!eventsBound)
            {
                return;
            }

            if (interactAction != null)
            {
                interactAction.performed -= HandleInteract;
            }

            if (jumpAction != null)
            {
                jumpAction.performed -= HandleJump;
            }

            if (openMapAction != null)
            {
                openMapAction.performed -= HandleOpenMap;
            }

            if (pauseAction != null)
            {
                pauseAction.performed -= HandlePause;
            }

            eventsBound = false;
        }

        private void HandleInteract(InputAction.CallbackContext context) => InteractPressed?.Invoke();
        private void HandleJump(InputAction.CallbackContext context) => JumpPressed?.Invoke();
        private void HandleOpenMap(InputAction.CallbackContext context) => OpenMapPressed?.Invoke();
        private void HandlePause(InputAction.CallbackContext context) => PausePressed?.Invoke();
    }
}
