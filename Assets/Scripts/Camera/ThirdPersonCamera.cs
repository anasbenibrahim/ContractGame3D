using Contract.Core;
using Contract.Player;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Contract.CameraSystem
{
    [DefaultExecutionOrder(100)]
    public sealed class ThirdPersonCamera : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private InputReader inputReader;
        [SerializeField] private Transform target;

        [Header("Orbit")]
        [SerializeField] private float distance = 4.6f;
        [SerializeField] private float shoulderOffset = 0.45f;
        [SerializeField] private float minPitch = -35f;
        [SerializeField] private float maxPitch = 65f;
        [SerializeField] private float initialYaw = 0f;
        [SerializeField] private float initialPitch = 14f;

        [Header("Sensitivity")]
        [SerializeField] private float mouseSensitivity = 0.12f;
        [SerializeField] private float gamepadSensitivity = 180f;
        [SerializeField] private float gamepadDeadzone = 0.12f;

        [Header("Collision")]
        [SerializeField] private LayerMask collisionMask = ~0;
        [SerializeField] private float collisionRadius = 0.25f;
        [SerializeField] private float collisionPadding = 0.15f;
        [SerializeField] private float minimumDistance = 0.6f;

        [Header("Smoothing")]
        [SerializeField] private float positionSmoothTime = 0.05f;

        [Header("Zoom")]
        [SerializeField] private float zoomStep = 0.6f;
        [SerializeField] private float zoomSmoothTime = 0.12f;

        [Header("Sprint Field of View")]
        [SerializeField] private float baseFieldOfView = 60f;
        [SerializeField] private float sprintFieldOfViewBoost = 6f;
        [SerializeField] private float fieldOfViewSharpness = 6f;

        public float Distance
        {
            get => distance;
            set => distance = Mathf.Max(minimumDistance, value);
        }

        public float MouseSensitivity
        {
            get => mouseSensitivity;
            set => mouseSensitivity = Mathf.Max(0.01f, value);
        }

        public float GamepadSensitivity
        {
            get => gamepadSensitivity;
            set => gamepadSensitivity = Mathf.Max(1f, value);
        }

        private float yaw;
        private float pitch;
        private Vector3 currentPosition;
        private Vector3 smoothVelocity;

        // Base values from the inspector; settings act as multipliers on top of them.
        private float baseMouseSensitivity;
        private float baseGamepadSensitivity;
        private bool invertY;
        private float targetDistance;
        private float distanceVelocity;
        private Camera cameraComponent;
        private ThirdPersonController player;

        public float Yaw => yaw;
        public float Pitch => pitch;
        public float TargetDistance => targetDistance;

        private void Awake()
        {
            baseMouseSensitivity = mouseSensitivity;
            baseGamepadSensitivity = gamepadSensitivity;
            targetDistance = distance;
            cameraComponent = GetComponent<Camera>();
            if (cameraComponent != null) baseFieldOfView = cameraComponent.fieldOfView;

            yaw = initialYaw;
            pitch = Mathf.Clamp(initialPitch, minPitch, maxPitch);

            int playerLayer = LayerMask.NameToLayer("Player");
            if (playerLayer >= 0)
            {
                collisionMask.value &= ~(1 << playerLayer);
            }
        }

        private void OnEnable()
        {
            GameEvents.SettingsApplied += ApplySettings;
        }

        private void OnDisable()
        {
            GameEvents.SettingsApplied -= ApplySettings;
        }

        /// <summary>Applies sensitivity / distance / invert from the session settings.</summary>
        public void ApplySettings()
        {
            GameSettings settings = GameManager.Instance?.Session?.Settings;
            if (settings == null)
            {
                return;
            }

            mouseSensitivity = baseMouseSensitivity * Mathf.Max(0.05f, settings.mouseSensitivity);
            gamepadSensitivity = baseGamepadSensitivity * Mathf.Max(0.05f, settings.gamepadSensitivity);
            invertY = settings.invertCameraY;
            targetDistance = Mathf.Clamp(settings.cameraDistance, GameConstants.MinCameraDistance, GameConstants.MaxCameraDistance);
        }

        private void Start()
        {
            if (target != null) player = target.GetComponentInParent<ThirdPersonController>();
            if (yaw == initialYaw && target != null && player != null)
            {
                yaw = player.transform.eulerAngles.y; // start behind the player
            }

            ApplySettings();
            distance = targetDistance;
            ComputeCamera(out Vector3 position, out Quaternion rotation);
            currentPosition = position;
            transform.SetPositionAndRotation(position, rotation);
        }

        private void LateUpdate()
        {
            if (target == null)
            {
                return;
            }

            if (inputReader != null && !IsPaused())
            {
                Vector2 look = inputReader.Look;
                bool gamepadLook = IsGamepadLook();

                if (gamepadLook)
                {
                    if (Mathf.Abs(look.x) < gamepadDeadzone)
                    {
                        look.x = 0f;
                    }

                    if (Mathf.Abs(look.y) < gamepadDeadzone)
                    {
                        look.y = 0f;
                    }
                }

                if (look.sqrMagnitude > 0f)
                {
                    float sensitivity = gamepadLook ? gamepadSensitivity * Time.unscaledDeltaTime : mouseSensitivity;
                    yaw += look.x * sensitivity;
                    float vertical = invertY ? -look.y : look.y;
                    pitch = Mathf.Clamp(pitch - vertical * sensitivity, minPitch, maxPitch);
                }

                HandleZoom(inputReader.Zoom);
            }

            // Touch drag-to-look (degrees, already scaled by the touch sensitivity settings).
            Vector2 touchLook = inputReader != null ? inputReader.ConsumeTouchLook() : Vector2.zero;
            if (touchLook.sqrMagnitude > 0f && !IsPaused())
            {
                yaw += touchLook.x;
                pitch = Mathf.Clamp(pitch - (invertY ? -touchLook.y : touchLook.y), minPitch, maxPitch);
            }

            distance = Mathf.SmoothDamp(distance, targetDistance, ref distanceVelocity, zoomSmoothTime, Mathf.Infinity, Time.unscaledDeltaTime);
            UpdateFieldOfView();

            ComputeCamera(out Vector3 desiredPosition, out Quaternion desiredRotation);
            currentPosition = Vector3.SmoothDamp(
                currentPosition,
                desiredPosition,
                ref smoothVelocity,
                positionSmoothTime,
                Mathf.Infinity,
                Time.unscaledDeltaTime);

            transform.SetPositionAndRotation(currentPosition, desiredRotation);
            UpdatePlayerVisibility();
        }

        // ---- close-quarters: never fill the screen with the back of the operative's head ----

        [Header("Close quarters")]
        [SerializeField] private float hidePlayerDistance = 0.85f;
        private bool playerHidden;
        private float hiddenRefresh;

        /// <summary>True while walls have pushed the camera so close that the operative is hidden (shadow kept).</summary>
        public bool PlayerHidden => playerHidden;

        private void UpdatePlayerVisibility()
        {
            Transform body = player != null ? player.transform : target;
            if (body == null) return;
            Vector3 head = target.position;
            bool hide = Vector3.Distance(transform.position, head) < hidePlayerDistance;
            hiddenRefresh -= Time.unscaledDeltaTime;
            if (hide == playerHidden && !(hide && hiddenRefresh <= 0f)) return; // refresh while hidden: outfits rebuild renderers
            playerHidden = hide;
            hiddenRefresh = 0.5f;
            foreach (Renderer r in body.GetComponentsInChildren<Renderer>())
            {
                if (r is ParticleSystemRenderer) continue;
                r.shadowCastingMode = hide ? UnityEngine.Rendering.ShadowCastingMode.ShadowsOnly : UnityEngine.Rendering.ShadowCastingMode.On;
            }
        }

        private void ComputeCamera(out Vector3 position, out Quaternion rotation)
        {
            rotation = Quaternion.Euler(pitch, yaw, 0f);
            Vector3 pivot = target.position;
            Vector3 shoulder = rotation * new Vector3(shoulderOffset, 0f, 0f);
            Vector3 focus = pivot + shoulder;
            Vector3 back = rotation * Vector3.back;
            Vector3 desired = focus + back * distance;
            Vector3 direction = desired - focus;
            float maxDistance = direction.magnitude;

            if (maxDistance > 0.001f)
            {
                Vector3 normalized = direction / maxDistance;
                if (Physics.SphereCast(
                        focus,
                        collisionRadius,
                        normalized,
                        out RaycastHit hit,
                        maxDistance,
                        collisionMask,
                        QueryTriggerInteraction.Ignore))
                {
                    float hitDistance = Mathf.Max(minimumDistance, hit.distance - collisionPadding);
                    desired = focus + normalized * hitDistance;
                }
            }

            position = desired;
        }

        private void HandleZoom(float zoomInput)
        {
            if (Mathf.Abs(zoomInput) < 0.01f)
            {
                return;
            }

            float steps;
            Mouse mouse = Mouse.current;
            if (mouse != null && Mathf.Abs(mouse.scroll.ReadValue().y) > 0.01f)
            {
                // Wheel: ~120 per notch on Windows, ~1 per notch on some Linux/macOS setups.
                steps = Mathf.Abs(zoomInput) > 1.5f ? zoomInput / 120f : zoomInput;
            }
            else
            {
                steps = zoomInput * Time.unscaledDeltaTime * 4f; // held d-pad: continuous zoom
            }
            targetDistance = Mathf.Clamp(targetDistance - steps * zoomStep, GameConstants.MinCameraDistance, GameConstants.MaxCameraDistance);

            GameSettings settings = GameManager.Instance?.Session?.Settings;
            if (settings != null)
            {
                settings.cameraDistance = targetDistance; // remembered and saved with the other settings
            }
        }

        private void UpdateFieldOfView()
        {
            if (cameraComponent == null)
            {
                return;
            }

            float targetFov = baseFieldOfView + (player != null && player.Sprinting && player.PlanarSpeed > 0.5f ? sprintFieldOfViewBoost : 0f);
            float blend = 1f - Mathf.Exp(-fieldOfViewSharpness * Time.unscaledDeltaTime);
            cameraComponent.fieldOfView = Mathf.Lerp(cameraComponent.fieldOfView, targetFov, blend);
        }

        private bool IsPaused()
        {
            // The full map uses the zoom / look inputs for itself.
            return GameManager.Instance != null && GameManager.Instance.InputBlocked;
        }

        private static bool IsGamepadLook()
        {
            Gamepad gamepad = Gamepad.current;
            return gamepad != null && gamepad.rightStick.ReadValue().sqrMagnitude > 0.01f;
        }

        private void OnValidate()
        {
            distance = Mathf.Max(0.1f, distance);
            collisionRadius = Mathf.Max(0.01f, collisionRadius);
            collisionPadding = Mathf.Max(0f, collisionPadding);
            minimumDistance = Mathf.Clamp(minimumDistance, 0.1f, distance);
            positionSmoothTime = Mathf.Max(0f, positionSmoothTime);
        }
    }
}
