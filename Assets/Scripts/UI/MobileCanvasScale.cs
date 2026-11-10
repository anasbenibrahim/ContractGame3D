using Contract.Core;
using UnityEngine;
using UnityEngine.UI;

namespace Contract.UI
{
    /// <summary>
    /// Makes a HUD canvas larger while the touch controls are in use (phones are held further
    /// from small screens than a monitor): the reference resolution drops to 1600x900 with
    /// "expand" matching, about 1.2x bigger text and widgets on a phone, while the canvas never
    /// becomes smaller than 1600x900 units on any aspect ratio so corner elements cannot collide.
    /// The PC scaling is restored when touch controls are off.
    /// </summary>
    [RequireComponent(typeof(CanvasScaler))]
    public sealed class MobileCanvasScale : MonoBehaviour
    {
        public static readonly Vector2 TouchReference = new Vector2(1600f, 900f);

        private CanvasScaler scaler;
        private Vector2 pcReference;
        private CanvasScaler.ScreenMatchMode pcMode;
        private float pcMatch;
        private bool? touch;

        public bool TouchLayout => touch == true;

        private void Awake()
        {
            scaler = GetComponent<CanvasScaler>();
            pcReference = scaler.referenceResolution;
            pcMode = scaler.screenMatchMode;
            pcMatch = scaler.matchWidthOrHeight;
        }

        private void LateUpdate()
        {
            bool now = InputHints.Touch;
            if (touch == now) return;
            touch = now;
            scaler.referenceResolution = now ? TouchReference : pcReference;
            scaler.screenMatchMode = now ? CanvasScaler.ScreenMatchMode.Expand : pcMode;
            scaler.matchWidthOrHeight = pcMatch;
        }
    }
}
