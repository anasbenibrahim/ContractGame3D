using Contract.Contracts;
using Contract.Core;
using UnityEngine;

namespace Contract.Player
{
    /// <summary>
    /// Footsteps for the player: one step per stride of distance actually covered on the ground
    /// (longer strides when sprinting), quieter when crouched, plus a landing thud after a fall.
    /// </summary>
    [RequireComponent(typeof(ThirdPersonController))]
    public sealed class FootstepAudio : MonoBehaviour
    {
        [SerializeField] private float walkStride = 0.72f;
        [SerializeField] private float sprintStride = 1.05f;
        [SerializeField] private float crouchStride = 0.55f;

        private ThirdPersonController controller;
        private Vector3 lastPosition;
        private float travelled;
        private float airTime;

        public int StepCount { get; private set; }
        public int LandingCount { get; private set; }

        private void Awake()
        {
            controller = GetComponent<ThirdPersonController>();
            lastPosition = transform.position;
        }

        private void Update()
        {
            Vector3 delta = transform.position - lastPosition;
            lastPosition = transform.position;
            if (Time.deltaTime <= 0f) return;

            if (!controller.Grounded)
            {
                airTime += Time.deltaTime;
                travelled = 0f;
                return;
            }

            if (airTime > 0.35f)
            {
                LandingCount++;
                Play("land_impact", 0.9f);
            }

            airTime = 0f;
            delta.y = 0f;
            float distance = delta.magnitude;
            if (distance > 2f) return; // teleport, not a step
            travelled += distance;

            float stride = controller.IsCrouching ? crouchStride : controller.IsSprinting ? sprintStride : walkStride;
            if (travelled < stride) return;
            travelled -= stride;
            StepCount++;
            Play("footstep", controller.IsCrouching ? 0.25f : controller.IsSprinting ? 0.85f : 0.55f);
        }

        private void Play(string id, float volume)
        {
            GameManager.Instance?.GetService<IAudioService>()?.PlaySFX3D(id, transform.position, 18f, Random.Range(0.93f, 1.07f), volume);
        }
    }
}
