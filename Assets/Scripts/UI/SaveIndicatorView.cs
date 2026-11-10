using Contract.Core;
using TMPro;
using UnityEngine;

namespace Contract.UI
{
    /// <summary>Brief "Saved" badge whenever the career is written to disk (auto or manual).</summary>
    public sealed class SaveIndicatorView : MonoBehaviour
    {
        [SerializeField] private CanvasGroup group;
        [SerializeField] private TMP_Text label;
        [SerializeField] private float holdSeconds = 1.6f;

        private float timer;

        public bool Visible => group.alpha > 0.01f;

        private void Awake() => group.alpha = 0f;

        private void OnEnable() => GameEvents.SaveCompleted += OnSaved;

        private void OnDisable() => GameEvents.SaveCompleted -= OnSaved;

        private void OnSaved(string slot)
        {
            label.text = "저장됨 · Saved";
            timer = holdSeconds;
            group.alpha = 1f;
        }

        private void Update()
        {
            if (timer > 0f) timer -= Time.unscaledDeltaTime;
            else if (group.alpha > 0f) group.alpha = Mathf.MoveTowards(group.alpha, 0f, Time.unscaledDeltaTime * 3f);
        }
    }
}
