using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Contract.UI
{
    /// <summary>
    /// Hover / select / press feedback for buttons: gentle scale-up and an accent edge on hover
    /// (mouse or gamepad focus), a quick squash on press. Unscaled time so it works while paused.
    /// </summary>
    [RequireComponent(typeof(Selectable))]
    public sealed class UIButtonFX : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler, IPointerUpHandler, ISelectHandler, IDeselectHandler,
        IPointerClickHandler, ISubmitHandler
    {
        [SerializeField] private float hoverScale = 1.04f;
        [SerializeField] private float pressScale = 0.96f;
        [SerializeField] private float speed = 14f;
        [SerializeField] private Graphic accentEdge;

        private Vector3 baseScale = Vector3.one;
        private bool hovered, pressed;
        private Selectable selectable;

        public void SetAccent(Graphic edge) => accentEdge = edge;

        private void Awake()
        {
            selectable = GetComponent<Selectable>();
            baseScale = transform.localScale;
            if (accentEdge != null) accentEdge.canvasRenderer.SetAlpha(0f);
        }

        private void OnDisable()
        {
            hovered = pressed = false;
            transform.localScale = baseScale;
            if (accentEdge != null) accentEdge.canvasRenderer.SetAlpha(0f);
        }

        public void OnPointerEnter(PointerEventData e)
        {
            hovered = true;
            Sound("ui_hover");
        }

        public void OnPointerClick(PointerEventData e) => Sound("ui_click");
        public void OnSubmit(BaseEventData e) => Sound("ui_click");

        private void Sound(string id)
        {
            if (selectable == null || selectable.IsInteractable())
                Contract.Core.GameManager.Instance?.GetService<Contract.Contracts.IAudioService>()?.PlayUI(id);
        }
        public void OnPointerExit(PointerEventData e) => hovered = pressed = false;
        public void OnPointerDown(PointerEventData e) => pressed = true;
        public void OnPointerUp(PointerEventData e) => pressed = false;
        public void OnSelect(BaseEventData e)
        {
            if (!hovered) Sound("ui_hover"); // gamepad / keyboard focus (the mouse already played it on enter)
            hovered = true;
        }
        public void OnDeselect(BaseEventData e) => hovered = false;

        private void Update()
        {
            bool active = selectable == null || selectable.IsInteractable();
            float target = !active ? 1f : pressed ? pressScale : hovered ? hoverScale : 1f;
            float t = 1f - Mathf.Exp(-speed * Time.unscaledDeltaTime);
            transform.localScale = Vector3.Lerp(transform.localScale, baseScale * target, t);
            if (accentEdge != null)
            {
                float alpha = Mathf.Lerp(accentEdge.canvasRenderer.GetAlpha(), active && hovered ? 1f : 0f, t);
                accentEdge.canvasRenderer.SetAlpha(alpha);
            }
        }
    }
}
