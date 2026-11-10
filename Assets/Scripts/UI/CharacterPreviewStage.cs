using Contract.Data;
using Contract.Player;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Contract.UI
{
    /// <summary>
    /// Turntable for the character-selection preview: builds the selected operative (rigged model
    /// if assigned, otherwise the placeholder humanoid), auto-rotates slowly, can be dragged to
    /// spin, waves when selected and gets a camera push-in when confirmed.
    /// </summary>
    public sealed class CharacterPreviewStage : MonoBehaviour
    {
        [SerializeField] private Transform turntable;
        [SerializeField] private Material baseMaterial;
        [SerializeField] private Camera stageCamera;
        [SerializeField] private float autoRotateSpeed = 18f;
        [SerializeField] private float dragSpeed = 0.35f;

        private GameObject current;
        private PlaceholderRig rig;
        private float yaw = 200f;
        private float idleTimer;
        private bool confirmed;
        private Vector3 cameraStart;

        public PlaceholderRig Rig => rig;
        public GameObject Current => current;
        public float Yaw => yaw;

        private void Awake()
        {
            if (stageCamera != null) cameraStart = stageCamera.transform.position;
        }

        public void Show(CharacterData character)
        {
            if (current != null) Destroy(current);
            confirmed = false;
            if (stageCamera != null) stageCamera.transform.position = cameraStart;

            if (character.characterPreviewPrefab != null)
            {
                current = Instantiate(character.characterPreviewPrefab, turntable, false);
                rig = null;
            }
            else
            {
                rig = PlaceholderHumanoidBuilder.Build(turntable, Contract.Core.PlayerUpgrades.Dress(character.appearance), baseMaterial, "Preview");
                current = rig.gameObject;
            }

            rig?.PlayGesture(RigGesture.Wave, 1.6f); // greets the player on selection
        }

        /// <summary>Confirmation beat: a nod-like talk gesture and a slow camera push-in.</summary>
        public void PlayConfirm()
        {
            confirmed = true;
            rig?.PlayGesture(RigGesture.Talk, 2f);
        }

        private bool touchDragging;

        public void Spin(float degrees)
        {
            yaw += degrees;
            idleTimer = 2.5f; // pause the auto-rotation after the player drags
        }

        private void Update()
        {
            float dt = Time.unscaledDeltaTime;
            Mouse mouse = Mouse.current;
            if (mouse != null && mouse.rightButton.isPressed) Spin(-mouse.delta.ReadValue().x * dragSpeed);
            // Touch: a one-finger drag that starts over the stage (the middle of the screen, between the side panels).
            Touchscreen screen = Touchscreen.current;
            if (screen != null)
            {
                var touch = screen.primaryTouch;
                if (touch.press.wasPressedThisFrame)
                {
                    float x = touch.position.ReadValue().x / Mathf.Max(1, Screen.width);
                    touchDragging = x > 0.3f && x < 0.7f;
                }

                if (!touch.press.isPressed) touchDragging = false;
                if (touchDragging) Spin(-touch.delta.ReadValue().x * dragSpeed);
            }
            Gamepad pad = Gamepad.current;
            if (pad != null && Mathf.Abs(pad.rightStick.x.ReadValue()) > 0.2f) Spin(-pad.rightStick.x.ReadValue() * 120f * dt);

            idleTimer -= dt;
            if (idleTimer <= 0f) yaw += autoRotateSpeed * dt;
            turntable.localRotation = Quaternion.Euler(0f, yaw, 0f);

            if (confirmed && stageCamera != null)
            {
                Vector3 target = Vector3.Lerp(cameraStart, turntable.position + Vector3.up * 1.4f, 0.35f);
                stageCamera.transform.position = Vector3.Lerp(stageCamera.transform.position, target, 1f - Mathf.Exp(-2.5f * dt));
            }
        }
    }
}
