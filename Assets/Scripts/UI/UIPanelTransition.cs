using UnityEngine;

namespace Contract.UI
{
    /// <summary>
    /// Opening animation for a panel: fades in and settles from a slight scale-down (unscaled
    /// time, so it also plays on the pause menu). Purely visual: the panel is usable at once.
    /// </summary>
    [RequireComponent(typeof(CanvasGroup))]
    public sealed class UIPanelTransition : MonoBehaviour
    {
        [SerializeField] private float duration = 0.18f;
        [SerializeField] private float startScale = 0.96f;

        private CanvasGroup group;
        private float time;

        public bool Finished => time >= duration;

        private void Awake() => group = GetComponent<CanvasGroup>();

        private void OnEnable()
        {
            time = 0f;
            Apply(0f);
        }

        private void Update()
        {
            if (time >= duration) return;
            time += Time.unscaledDeltaTime;
            Apply(Mathf.Clamp01(time / duration));
        }

        private void Apply(float k)
        {
            float eased = 1f - (1f - k) * (1f - k) * (1f - k);
            if (group == null) group = GetComponent<CanvasGroup>();
            group.alpha = eased;
            transform.localScale = Vector3.one * Mathf.Lerp(startScale, 1f, eased);
        }
    }
}
