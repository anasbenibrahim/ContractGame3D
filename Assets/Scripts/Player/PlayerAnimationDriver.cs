using System;
using Contract.Contracts;
using Contract.Core;
using Contract.VFX;
using UnityEngine;

namespace Contract.Player
{
    /// <summary>
    /// Enhanced animation driver with footstep events, blend tree parameters, and state transitions.
    /// </summary>
    public sealed class PlayerAnimationDriver : MonoBehaviour
    {
        [Header("Animator")]
        [SerializeField] private Animator animator;

        [Header("Footstep Events")]
        [SerializeField] private AnimationEventReceiver eventReceiver;

        [Header("Blend Tree Parameters")]
        [SerializeField] private string speedParam = "Speed";
        [SerializeField] private string verticalSpeedParam = "VerticalSpeed";
        [SerializeField] private string crouchParam = "Crouch";
        [SerializeField] private string groundedParam = "Grounded";
        [SerializeField] private string aimParam = "Aim";
        [SerializeField] private string interactParam = "Interact";

        [Header("Footstep Settings")]
        [SerializeField] private float walkFootstepInterval = 0.5f;
        [SerializeField] private float runFootstepInterval = 0.35f;
        [SerializeField] private float sprintFootstepInterval = 0.25f;
        [SerializeField] private float crouchFootstepInterval = 0.7f;

        [Header("Surface Detection")]
        [SerializeField] private LayerMask groundLayers = ~0;
        [SerializeField] private float raycastDistance = 1.2f;

        private ThirdPersonController controller;
        private IAudioService audioService;
        private float lastFootstepTime;
        private int footstepSide; // 0 = left, 1 = right
        private string lastSurfaceTag;
        private bool hasInteractParam;
        private bool hasAimParam;

        private static readonly int SpeedHash = Animator.StringToHash("Speed");
        private static readonly int VerticalSpeedHash = Animator.StringToHash("VerticalSpeed");
        private static readonly int CrouchHash = Animator.StringToHash("Crouch");
        private static readonly int GroundedHash = Animator.StringToHash("Grounded");
        private static readonly int AimHash = Animator.StringToHash("Aim");
        private static readonly int InteractHash = Animator.StringToHash("Interact");

        private void Awake()
        {
            if (animator == null) animator = GetComponent<Animator>();
            controller = GetComponent<ThirdPersonController>();
            groundLayers &= ~(1 << gameObject.layer); // never detect the player's own collider as ground
            CacheParameters();
            
            if (eventReceiver != null)
            {
                eventReceiver.OnAnimationEvent += OnAnimationEvent;
            }
        }

        private void Start()
        {
            audioService = GameManager.Instance?.GetService<IAudioService>();
        }

        /// <summary>
        /// Switches the Animator being driven (e.g. to a rigged character model), or null when the
        /// visual animates itself (procedural placeholder rig). Footsteps keep working either way.
        /// </summary>
        public void SetAnimator(Animator newAnimator)
        {
            animator = newAnimator;
            CacheParameters();
        }

        private void CacheParameters()
        {
            hasInteractParam = HasParameter(InteractHash);
            hasAimParam = HasParameter(AimHash);
        }

        private bool HasParameter(int hash)
        {
            if (animator == null || animator.runtimeAnimatorController == null) return false;
            foreach (AnimatorControllerParameter parameter in animator.parameters)
            {
                if (parameter.nameHash == hash) return true;
            }

            return false;
        }

        private void Update()
        {
            if (controller == null) return;

            // Speed (0 idle, 0.5 walk, 1 sprint) - also used for footstep cadence
            float speed = controller.IsSprinting ? 1f : (controller.PlanarSpeed > 0.1f ? 0.5f : 0f);

            if (animator != null && animator.isActiveAndEnabled && animator.runtimeAnimatorController != null)
            {
                animator.SetFloat(SpeedHash, speed, 0.1f, Time.deltaTime);
                animator.SetFloat(VerticalSpeedHash, controller.VerticalSpeed, 0.1f, Time.deltaTime);
                animator.SetBool(CrouchHash, controller.IsCrouching);
                animator.SetBool(GroundedHash, controller.Grounded);
            }

            HandleFootsteps(speed);
        }

        private void HandleFootsteps(float speed)
        {
            if (!controller.Grounded || speed <= 0f) return;

            float interval;
            if (controller.IsSprinting) interval = sprintFootstepInterval;
            else if (controller.IsCrouching) interval = crouchFootstepInterval;
            else if (controller.IsWalking) interval = walkFootstepInterval;
            else interval = runFootstepInterval;

            if (Time.time - lastFootstepTime >= interval)
            {
                lastFootstepTime = Time.time;
                footstepSide = 1 - footstepSide; // Alternate left/right
                
                // Play footstep sound via VFX event
                PlayFootstepSound();
                
                // Trigger VFX particles
                VFXManager.Instance?.PlayFootstep(transform.position, speed);
            }
        }

        private void PlayFootstepSound()
        {
            if (audioService == null) return;

            // Determine surface from raycast
            RaycastHit hit;
            if (Physics.Raycast(transform.position + Vector3.up * 0.1f, Vector3.down, out hit, raycastDistance, groundLayers))
            {
                string surfaceTag = hit.collider.tag;
                string footstepId = GetFootstepId(surfaceTag);
                
                if (!string.IsNullOrEmpty(footstepId))
                {
                    audioService.PlaySFX3D(footstepId, transform.position, 10f, 1f, 0.5f);
                }
            }
        }

        private string GetFootstepId(string surfaceTag)
        {
            switch (surfaceTag)
            {
                case "Concrete": return "footstep_concrete";
                case "Metal": return "footstep_metal";
                case "Wood": return "footstep_wood";
                case "Grass": return "footstep_grass";
                case "Gravel": return "footstep_gravel";
                case "Carpet": return "footstep_carpet";
                case "Tile": return "footstep_tile";
                default: return "footstep_default";
            }
        }

        private void OnAnimationEvent(string eventName)
        {
            switch (eventName)
            {
                case "FootstepL":
                    footstepSide = 0;
                    PlayFootstepSound();
                    VFXManager.Instance?.PlayFootstep(transform.position, 0f);
                    break;
                case "FootstepR":
                    footstepSide = 1;
                    PlayFootstepSound();
                    VFXManager.Instance?.PlayFootstep(transform.position, 1f);
                    break;
                case "Land":
                    VFXManager.Instance?.PlayImpact(transform.position, Vector3.up);
                    audioService?.PlaySFX3D("land_impact", transform.position, 8f);
                    break;
                case "InteractStart":
                    if (hasInteractParam) animator.SetTrigger(InteractHash);
                    break;
                case "InteractEnd":
                    if (hasInteractParam) animator.ResetTrigger(InteractHash);
                    break;
            }
        }

        public void TriggerInteraction()
        {
            if (hasInteractParam)
            {
                animator.SetTrigger(InteractHash);
            }
        }

        public void SetAim(bool aiming)
        {
            if (hasAimParam)
            {
                animator.SetBool(AimHash, aiming);
            }
        }
    }
}