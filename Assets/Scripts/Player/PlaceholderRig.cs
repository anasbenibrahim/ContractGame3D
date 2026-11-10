using System.Collections.Generic;
using UnityEngine;

namespace Contract.Player
{
    public enum RigGesture
    {
        None,
        Reach,  // interact with an object
        Photo,  // both hands raised to the face (camera)
        Talk,   // conversational hand movement
        Wave
    }

    /// <summary>
    /// Articulated placeholder humanoid (built by <see cref="PlaceholderHumanoidBuilder"/>) with
    /// procedural idle / walk / run / crouch / airborne animation. Used until rigged models are
    /// imported; driven by <see cref="ThirdPersonController"/> when present, otherwise idles
    /// (e.g. as a character-selection preview).
    /// </summary>
    public sealed class PlaceholderRig : MonoBehaviour
    {
        public sealed class Joints
        {
            public Transform Hips, Spine, Chest, Neck, Head;
            public Transform UpperArmL, ForearmL, UpperArmR, ForearmR;
            public Transform ThighL, ShinL, FootL, ThighR, ShinR, FootR;
        }

        [Header("Gait")]
        [SerializeField] private float walkStrideLength = 2.2f;
        [SerializeField] private float sprintStrideLength = 3.2f;
        [SerializeField] private float crouchHipDrop = 0.42f;
        [SerializeField] private float weightSharpness = 10f;

        private Joints joints;
        private Vector3 hipsBasePosition;
        private readonly List<Object> ownedAssets = new List<Object>();
        private ILocomotionSource driver;

        private RigGesture gesture;
        private float gestureWeight;
        private float gestureTimer;

        private float phase;
        private float moveWeight;
        private float runWeight;
        private float crouchWeight;
        private float airWeight;
        private float idleTime;

        public bool HasDriver => driver != null;

        /// <summary>Joints and the runtime assets (materials, combined meshes) this rig must destroy.</summary>
        public void Initialize(Joints builtJoints, IEnumerable<Object> owned)
        {
            joints = builtJoints;
            hipsBasePosition = joints.Hips.localPosition;
            ownedAssets.AddRange(owned);
            driver = GetComponentInParent<ILocomotionSource>();
        }

        public RigGesture CurrentGesture => gestureTimer > 0f ? gesture : RigGesture.None;

        /// <summary>Plays an upper-body gesture on top of locomotion for <paramref name="seconds"/>.</summary>
        public void PlayGesture(RigGesture newGesture, float seconds)
        {
            gesture = newGesture;
            gestureTimer = seconds;
        }

        private void LateUpdate()
        {
            if (joints == null)
            {
                return;
            }

            float dt = Time.deltaTime;
            float speed = 0f, run = 0f, crouch = 0f, air = 0f;

            if (driver != null)
            {
                speed = driver.PlanarSpeed;
                run = driver.Sprinting ? 1f : 0f;
                crouch = driver.CrouchAmount;
                air = driver.Grounded ? 0f : 1f;
            }

            float blend = 1f - Mathf.Exp(-weightSharpness * dt);
            moveWeight = Mathf.Lerp(moveWeight, Mathf.Clamp01(speed / 1.2f), blend);
            runWeight = Mathf.Lerp(runWeight, run, blend);
            crouchWeight = crouch; // already smoothed by the controller, keeps the mesh in sync with the capsule
            airWeight = Mathf.Lerp(airWeight, air, blend);
            idleTime += dt;
            gestureTimer -= dt;
            gestureWeight = Mathf.MoveTowards(gestureWeight, gestureTimer > 0f ? 1f : 0f, dt * 5f);

            float stride = Mathf.Lerp(walkStrideLength, sprintStrideLength, runWeight) * Mathf.Lerp(1f, 0.55f, crouchWeight);
            phase = Mathf.Repeat(phase + dt * (speed / Mathf.Max(0.1f, stride)) * Mathf.PI * 2f, Mathf.PI * 2f);

            Animate();
        }

        private void Animate()
        {
            float gait = moveWeight * (1f - airWeight);
            float legAmp = Mathf.Lerp(30f, 50f, runWeight) * Mathf.Lerp(1f, 0.55f, crouchWeight);
            float kneeAmp = Mathf.Lerp(45f, 90f, runWeight) * Mathf.Lerp(1f, 0.5f, crouchWeight);
            float armAmp = Mathf.Lerp(25f, 55f, runWeight) * Mathf.Lerp(1f, 0.4f, crouchWeight);
            float elbow = Mathf.Lerp(12f, 85f, runWeight * gait) + 8f;
            float breathe = Mathf.Sin(idleTime * 1.7f) * (1f - gait);

            // Swing: positive = this leg forward. Left/right are half a cycle apart.
            float swingL = Mathf.Sin(phase);
            float swingR = Mathf.Sin(phase + Mathf.PI);
            float liftL = Mathf.Max(0f, Mathf.Cos(phase));            // knee flexes while the leg swings forward
            float liftR = Mathf.Max(0f, Mathf.Cos(phase + Mathf.PI));

            // Hips: gait bob + crouch drop.
            float bob = (Mathf.Abs(swingL) - 0.5f) * 2f * Mathf.Lerp(0.02f, 0.05f, runWeight) * gait;
            joints.Hips.localPosition = hipsBasePosition + Vector3.up * (bob - crouchHipDrop * crouchWeight);
            joints.Hips.localRotation = Quaternion.Euler(0f, swingL * 6f * gait, 0f);

            float lean = Mathf.Lerp(3f, 12f, runWeight) * gait + 22f * crouchWeight + 4f * airWeight;
            joints.Spine.localRotation = Quaternion.Euler(lean, 0f, 0f);
            joints.Chest.localRotation = Quaternion.Euler(breathe * 1.2f, -swingL * 9f * gait, 0f);
            joints.Neck.localRotation = Quaternion.Euler(-lean * 0.5f, 0f, 0f);
            joints.Head.localRotation = Quaternion.Euler(-lean * 0.3f + breathe * 0.6f, swingL * 3f * gait, 0f);

            // Legs. Rotation about +X moves a hanging limb backwards, so "forward" is negative X.
            PoseLeg(joints.ThighL, joints.ShinL, joints.FootL, swingL, liftL, legAmp, kneeAmp, gait);
            PoseLeg(joints.ThighR, joints.ShinR, joints.FootR, swingR, liftR, legAmp, kneeAmp, gait);

            // Arms counter-swing with the opposite leg; abducted slightly so they clear the torso.
            PoseArm(joints.UpperArmL, joints.ForearmL, swingR, armAmp, elbow, gait, -1f, breathe);
            PoseArm(joints.UpperArmR, joints.ForearmR, swingL, armAmp, elbow, gait, 1f, breathe);
            ApplyGesture();
        }

        private void ApplyGesture()
        {
            if (gestureWeight <= 0f || gesture == RigGesture.None) return;
            float w = gestureWeight;
            float wobble = Mathf.Sin(idleTime * 7f);

            void Blend(Transform t, Vector3 euler) => t.localRotation = Quaternion.Slerp(t.localRotation, Quaternion.Euler(euler), w);

            switch (gesture)
            {
                case RigGesture.Reach:
                    Blend(joints.UpperArmR, new Vector3(-70f, 0f, 8f));
                    Blend(joints.ForearmR, new Vector3(-15f, 0f, 0f));
                    break;
                case RigGesture.Photo:
                    Blend(joints.UpperArmR, new Vector3(-80f, 0f, -22f));
                    Blend(joints.ForearmR, new Vector3(-110f, 0f, 0f));
                    Blend(joints.UpperArmL, new Vector3(-80f, 0f, 22f));
                    Blend(joints.ForearmL, new Vector3(-110f, 0f, 0f));
                    break;
                case RigGesture.Talk:
                    Blend(joints.UpperArmR, new Vector3(-25f + wobble * 6f, 0f, 10f));
                    Blend(joints.ForearmR, new Vector3(-75f + wobble * 10f, 0f, 0f));
                    break;
                case RigGesture.Wave:
                    Blend(joints.UpperArmR, new Vector3(0f, 0f, 150f));
                    Blend(joints.ForearmR, new Vector3(0f, 0f, 20f + wobble * 25f));
                    break;
            }
        }

        private void PoseLeg(Transform thigh, Transform shin, Transform foot, float swing, float lift, float amp, float kneeAmp, float gait)
        {
            float thighAngle = -swing * amp * gait - 75f * crouchWeight - 35f * airWeight;
            float kneeAngle = (lift * kneeAmp + 4f) * gait + 110f * crouchWeight + 60f * airWeight;
            thigh.localRotation = Quaternion.Euler(thighAngle, 0f, 0f);
            shin.localRotation = Quaternion.Euler(kneeAngle, 0f, 0f);
            foot.localRotation = Quaternion.Euler(-(thighAngle + kneeAngle) * 0.85f, 0f, 0f);
        }

        private void PoseArm(Transform upper, Transform forearm, float oppositeLegSwing, float amp, float elbow, float gait, float side, float breathe)
        {
            float swing = -oppositeLegSwing * amp * gait - 15f * crouchWeight - 20f * airWeight;
            float abduction = side * (6f + 19f * airWeight + breathe * 0.8f);
            upper.localRotation = Quaternion.Euler(swing, 0f, abduction);
            forearm.localRotation = Quaternion.Euler(-(elbow + 30f * crouchWeight), 0f, 0f);
        }

        private void OnDestroy()
        {
            foreach (Object asset in ownedAssets)
            {
                if (asset != null)
                {
                    Destroy(asset);
                }
            }

            ownedAssets.Clear();
        }
    }
}
