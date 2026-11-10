using Contract.Core;
using UnityEngine;

namespace Contract.UI
{
    /// <summary>
    /// Enlarges one HUD widget (money, contract tracker, toast ...) around its pivot while the
    /// touch controls are in use, for readability on a phone. Used on canvases that also hold
    /// full-screen panels, where scaling the whole canvas could push those panels off-screen.
    /// </summary>
    public sealed class MobileHudWidget : MonoBehaviour
    {
        [SerializeField] private float touchScale = 1.25f;

        private Vector3 pcScale;
        private bool? touch;

        public float TouchScale => touchScale;

        private void Awake() => pcScale = transform.localScale;

        private void LateUpdate()
        {
            bool now = InputHints.Touch;
            if (touch == now) return;
            touch = now;
            transform.localScale = now ? pcScale * touchScale : pcScale;
        }
    }
}
