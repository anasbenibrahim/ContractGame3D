using Contract.Core;
using Contract.Data;
using UnityEngine;

namespace Contract.Player
{
    [RequireComponent(typeof(CharacterController))]
    [DefaultExecutionOrder(-100)]
    public sealed class ThirdPersonController : MonoBehaviour, ILocomotionSource
    {
        [Header("References")]
        [SerializeField] private InputReader inputReader;
        [SerializeField] private Transform cameraTransform;
        [SerializeField] private Transform cameraTarget;
        [SerializeField] private Transform visualRoot;

        [Header("Movement")]
        [SerializeField] private float walkSpeed = 4.5f;
        [SerializeField] private float sprintSpeed = 7.5f;
        [SerializeField] private float crouchSpeed = 2.4f;
        [SerializeField] private float acceleration = 22f;
        [SerializeField] private float deceleration = 28f;
        [SerializeField] private float turnSpeed = 14f;
        [SerializeField] private float gravity = -24f;
        [SerializeField] private float jumpHeight = 1.2f;
        [SerializeField] private float coyoteTime = 0.12f;
        [SerializeField] private float jumpBuffer = 0.14f;

        [Header("Crouch")]
        [SerializeField] private float standHeight = 1.8f;
        [SerializeField] private float crouchHeight = 1.15f;
        [SerializeField] private float crouchTransitionSpeed = 12f;
        [SerializeField] private float standCheckPadding = 0.05f;
        [SerializeField] private LayerMask obstructionMask = ~0;

        [Header("Camera Target")]
        [SerializeField] private float standCameraHeight = 1.58f;
        [SerializeField] private float crouchCameraHeight = 1.08f;

        [Header("Visual")]
        [SerializeField] private float crouchVisualScale = 0.64f;
        [SerializeField] private float bobAmplitude = 0.03f;
        [SerializeField] private float bobFrequency = 10f;

        private CharacterController controller;
        private Vector3 horizontalVelocity;
        private float verticalVelocity;
        private float lastGroundedTime = float.NegativeInfinity;
        private float jumpRequestedTime = float.NegativeInfinity;
        private float crouchBlend;
        private float bobPhase;
        private bool isCrouched;
        private bool subscribed;
        private float characterMoveMultiplier = 1f;
        private float characterSprintMultiplier = 1f;
        private bool legacyVisualEffects = true;

        // Character stats x purchased upgrades (PlayerUpgrades recomputes only when upgrades change).
        private float MoveSpeedMultiplier => PlayerUpgrades.Multiplier(UpgradeEffect.MoveSpeed) * characterMoveMultiplier;
        private float SprintSpeedMultiplier => PlayerUpgrades.Multiplier(UpgradeEffect.SprintSpeed) * characterSprintMultiplier;

        public float PlanarSpeed { get; private set; }
        public float NormalizedSpeed { get; private set; }
        public float VerticalSpeed => verticalVelocity;
        public bool Crouching => isCrouched;
        public bool Sprinting { get; private set; }
        public bool Grounded => controller != null && controller.isGrounded;

        public bool IsSprinting => Sprinting;
        public bool IsWalking => !Sprinting && !isCrouched && PlanarSpeed > 0.1f;
        public bool IsCrouching => isCrouched;

        /// <summary>0 = standing, 1 = fully crouched (smoothed, matches the collision capsule).</summary>
        public float CrouchAmount => crouchBlend;
        public Transform VisualRoot => visualRoot;
        /// <summary>Optional movement source that overrides player input while active (GPS walking).</summary>
        public IMovementDriver Driver { get; set; }

        private static Vector3 Flat(Vector3 v) => new Vector3(v.x, 0f, v.z);

        public float WalkSpeed => walkSpeed * MoveSpeedMultiplier;
        public float SprintSpeed => sprintSpeed * SprintSpeedMultiplier;
        public float CrouchSpeed => crouchSpeed * MoveSpeedMultiplier * PlayerUpgrades.Multiplier(UpgradeEffect.CrouchSpeed);
        public float JumpHeight => jumpHeight * PlayerUpgrades.Multiplier(UpgradeEffect.JumpHeight);

        private void Awake()
        {
            controller = GetComponent<CharacterController>();
            int playerLayer = gameObject.layer;
            obstructionMask.value &= ~(1 << playerLayer);

            if (cameraTransform == null && Camera.main != null)
            {
                cameraTransform = Camera.main.transform;
            }

            if (cameraTarget == null)
            {
                cameraTarget = transform.Find("CameraTarget");
            }

            if (visualRoot == null)
            {
                visualRoot = transform.Find("Visual");
            }

            controller.height = standHeight;
            controller.center = new Vector3(0f, standHeight * 0.5f, 0f);

            if (cameraTarget != null)
            {
                Vector3 localPosition = cameraTarget.localPosition;
                localPosition.y = standCameraHeight;
                cameraTarget.localPosition = localPosition;
            }

            if (visualRoot != null)
            {
                visualRoot.localScale = Vector3.one;
            }
        }

        private void OnEnable()
        {
            if (inputReader == null)
            {
                return;
            }

            inputReader.Initialize();
            inputReader.EnableGameplay();
            inputReader.JumpPressed += HandleJumpPressed;
            inputReader.PausePressed += HandlePausePressed;
            subscribed = true;
        }

        private void Start()
        {
            SetCursorLocked(GameManager.Instance == null || !GameManager.Instance.IsPaused);
            RestoreSavedPosition();
        }

        /// <summary>Continue: put the operative back where the career was saved (once).</summary>
        private void RestoreSavedPosition()
        {
            GameSession session = GameManager.Instance?.Session;
            if (session == null || !session.hasSavedPlayerPosition) return;
            session.hasSavedPlayerPosition = false;
            controller.enabled = false; // CharacterController overrides transform writes while enabled
            transform.SetPositionAndRotation(session.savedPlayerPosition + Vector3.up * 0.05f, Quaternion.Euler(0f, session.savedPlayerYaw, 0f));
            controller.enabled = true;
            Physics.SyncTransforms();
        }

        private void OnDisable()
        {
            if (subscribed && inputReader != null)
            {
                inputReader.JumpPressed -= HandleJumpPressed;
                inputReader.PausePressed -= HandlePausePressed;
                inputReader.DisableGameplay();
                subscribed = false;
            }

            SetCursorLocked(false);
        }

        private void Update()
        {
            if (inputReader == null)
            {
                return;
            }

            if (GameManager.Instance != null && GameManager.Instance.IsPaused)
            {
                horizontalVelocity = Vector3.zero;
                PublishLocomotion(false);
                return;
            }

            // Map / contract board / result screen open: ignore movement input, but keep simulating
            // (gravity, landing) so the player never hangs in the air behind a menu.
            bool blocked = GameManager.Instance != null && GameManager.Instance.InputBlocked;
            // An external driver (GPS walking) replaces stick / keyboard movement while it is active.
            bool driven = Driver != null && Driver.DriverActive;
            if (!blocked && !driven) UpdateCrouch();

            Vector2 moveInput = blocked || driven ? Vector2.zero : inputReader.Move;
            // GPS walking continues while a menu (map, dashboard) is open; pausing the game still stops it.
            Vector3 moveDirection = driven ? Vector3.ClampMagnitude(Flat(Driver.DriveDirection), 1f) : GetCameraRelativeDirection(moveInput);
            bool wantsSprint = (driven ? Driver.DriveSprint : !blocked && inputReader.IsSprinting) && !isCrouched && moveDirection.sqrMagnitude > 0.001f;
            float targetSpeed = isCrouched
                ? CrouchSpeed
                : (wantsSprint ? SprintSpeed : WalkSpeed);
            Vector3 targetVelocity = moveDirection * targetSpeed;
            float accelerationRate = moveDirection.sqrMagnitude > 0.001f ? acceleration : deceleration;
            horizontalVelocity = Vector3.MoveTowards(horizontalVelocity, targetVelocity, accelerationRate * Time.deltaTime);

            if (horizontalVelocity.sqrMagnitude > 0.01f)
            {
                Vector3 flatVelocity = new Vector3(horizontalVelocity.x, 0f, horizontalVelocity.z);
                if (flatVelocity.sqrMagnitude > 0.001f)
                {
                    Quaternion targetRotation = Quaternion.LookRotation(flatVelocity.normalized, Vector3.up);
                    transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, turnSpeed * Time.deltaTime);
                }
            }

            bool grounded = controller.isGrounded;
            if (grounded)
            {
                lastGroundedTime = Time.time;
                if (verticalVelocity < 0f)
                {
                    verticalVelocity = -2f;
                }
            }

            bool canJump = Time.time - lastGroundedTime <= coyoteTime
                && Time.time - jumpRequestedTime <= jumpBuffer
                && !isCrouched;

            if (canJump)
            {
                verticalVelocity = Mathf.Sqrt(JumpHeight * -2f * gravity);
                jumpRequestedTime = float.NegativeInfinity;
                lastGroundedTime = float.NegativeInfinity;
            }

            verticalVelocity += gravity * Time.deltaTime;

            Vector3 motion = horizontalVelocity;
            motion.y = verticalVelocity;
            CollisionFlags flags = controller.Move(motion * Time.deltaTime);

            if ((flags & CollisionFlags.Above) != 0 && verticalVelocity > 0f)
            {
                verticalVelocity = 0f;
            }

            UpdateVisual(grounded);
            PublishLocomotion(wantsSprint);
        }


        /// <summary>Per-character speed multipliers (from CharacterData stats).</summary>
        public void ApplyCharacterMultipliers(float moveMultiplier, float sprintMultiplier)
        {
            characterMoveMultiplier = Mathf.Max(0.1f, moveMultiplier);
            characterSprintMultiplier = Mathf.Max(0.1f, sprintMultiplier);
        }

        /// <summary>
        /// The legacy primitive visual is squashed for crouch and bobbed while moving. An articulated
        /// placeholder or rigged model animates itself, so those effects must be switched off.
        /// </summary>
        public void SetLegacyVisualEffects(bool enabled)
        {
            legacyVisualEffects = enabled;
            if (!enabled && visualRoot != null)
            {
                visualRoot.localScale = Vector3.one;
                visualRoot.localPosition = new Vector3(visualRoot.localPosition.x, 0f, visualRoot.localPosition.z);
            }
        }

        private void PublishLocomotion(bool sprinting)
        {
            PlanarSpeed = new Vector3(horizontalVelocity.x, 0f, horizontalVelocity.z).magnitude;
            NormalizedSpeed = SprintSpeed > 0.01f ? Mathf.Clamp01(PlanarSpeed / SprintSpeed) : 0f;
            Sprinting = sprinting;
        }

        private void UpdateCrouch()
        {
            bool crouchHeld = inputReader.IsCrouching;
            bool shouldCrouch = crouchHeld || (isCrouched && !CanStand());
            isCrouched = shouldCrouch;
            crouchBlend = Mathf.MoveTowards(crouchBlend, isCrouched ? 1f : 0f, crouchTransitionSpeed * Time.deltaTime);
            ApplyCrouch(crouchBlend);
        }

        private void ApplyCrouch(float blend)
        {
            float height = Mathf.Lerp(standHeight, crouchHeight, blend);
            controller.height = height;
            controller.center = new Vector3(0f, height * 0.5f, 0f);

            if (cameraTarget != null)
            {
                Vector3 localPosition = cameraTarget.localPosition;
                localPosition.y = Mathf.Lerp(standCameraHeight, crouchCameraHeight, blend);
                cameraTarget.localPosition = localPosition;
            }

            if (visualRoot != null && legacyVisualEffects)
            {
                Vector3 scale = visualRoot.localScale;
                scale.y = Mathf.Lerp(1f, crouchVisualScale, blend);
                visualRoot.localScale = scale;
            }
        }

        private bool CanStand()
        {
            float radius = Mathf.Max(0.01f, controller.radius - 0.02f);
            Vector3 bottom = transform.position + Vector3.up * (crouchHeight + standCheckPadding);
            Vector3 top = transform.position + Vector3.up * (standHeight - standCheckPadding);
            return !Physics.CheckCapsule(bottom, top, radius, obstructionMask, QueryTriggerInteraction.Ignore);
        }

        private Vector3 GetCameraRelativeDirection(Vector2 input)
        {
            Vector3 forward = cameraTransform != null ? cameraTransform.forward : transform.forward;
            Vector3 right = cameraTransform != null ? cameraTransform.right : transform.right;
            forward.y = 0f;
            right.y = 0f;

            if (forward.sqrMagnitude < 0.001f)
            {
                forward = transform.forward;
                forward.y = 0f;
            }

            if (right.sqrMagnitude < 0.001f)
            {
                right = transform.right;
                right.y = 0f;
            }

            forward.Normalize();
            right.Normalize();

            Vector3 direction = forward * input.y + right * input.x;
            return direction.sqrMagnitude > 1f ? direction.normalized : direction;
        }

        private void UpdateVisual(bool grounded)
        {
            if (visualRoot == null || !legacyVisualEffects)
            {
                return;
            }

            float speed = horizontalVelocity.magnitude;
            float speedFactor = Mathf.Clamp01(speed / SprintSpeed);
            bobPhase += Time.deltaTime * bobFrequency * Mathf.Lerp(0.55f, 1.35f, speedFactor);
            float bob = grounded ? Mathf.Sin(bobPhase) * bobAmplitude * speedFactor : 0f;

            Vector3 localPosition = visualRoot.localPosition;
            localPosition.y = Mathf.MoveTowards(localPosition.y, bob, Time.deltaTime * 0.5f);
            visualRoot.localPosition = localPosition;
        }

        private void HandleJumpPressed()
        {
            if ((GameManager.Instance != null && GameManager.Instance.InputBlocked) || (Driver != null && Driver.DriverActive))
            {
                return;
            }

            jumpRequestedTime = Time.time;
        }

        private void HandlePausePressed()
        {
            if (GameManager.Instance == null)
            {
                return;
            }

            if (GameManager.Instance.IsMenuOpen)
            {
                GameEvents.RaiseMenuBack(); // Escape closes the open overlay first
                return;
            }

            if (GameManager.Instance.IsMapOpen)
            {
                GameManager.Instance.CloseMap(); // Escape closes the full map first; it does not pause
                SetCursorLocked(true);
                return;
            }

            bool paused = !GameManager.Instance.IsPaused;
            GameManager.Instance.SetPaused(paused);
            SetCursorLocked(!paused);
        }

        private void OnApplicationFocus(bool hasFocus)
        {
            if (!hasFocus)
            {
                return;
            }

            if (GameManager.Instance == null || !GameManager.Instance.InputBlocked)
            {
                SetCursorLocked(true);
            }
        }

        private static void SetCursorLocked(bool locked)
        {
            Cursor.lockState = locked ? CursorLockMode.Locked : CursorLockMode.None;
            Cursor.visible = !locked;
        }
    }

    /// <summary>
    /// Something other than the player's input that moves the character (one at a time).
    /// Direction is world space; its length (0-1) scales walking speed for smooth pacing.
    /// </summary>
    public interface IMovementDriver
    {
        bool DriverActive { get; }
        Vector3 DriveDirection { get; }
        bool DriveSprint { get; }
    }
}
