using System.Collections.Generic;
using System.Linq;
using Contract.Core;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Contract.UI
{
    /// <summary>
    /// Detection meter: shows the highest guard suspicion while anyone is suspicious, turning red
    /// ("발각 · Spotted") when a guard is alerted. Hidden when nobody has noticed the player.
    /// </summary>
    public sealed class DetectionMeterView : MonoBehaviour
    {
        [SerializeField] private CanvasGroup group;
        [SerializeField] private Image fill;
        [SerializeField] private TMP_Text label;

        private readonly Dictionary<string, float> levels = new Dictionary<string, float>();
        private readonly HashSet<string> alerted = new HashSet<string>();

        public float Level => levels.Count == 0 ? 0f : levels.Values.Max();
        public bool Spotted => alerted.Count > 0;
        public bool Visible => group.alpha > 0.5f;

        private void OnEnable()
        {
            GameEvents.SuspicionChanged += OnSuspicion;
            GameEvents.NpcAlerted += OnAlerted;
        }

        private void OnDisable()
        {
            GameEvents.SuspicionChanged -= OnSuspicion;
            GameEvents.NpcAlerted -= OnAlerted;
        }

        private void Start() => group.alpha = 0f;

        private void OnSuspicion(string npcId, float level)
        {
            levels[npcId] = level;
            if (level <= 0.001f) alerted.Remove(npcId);
        }

        private void OnAlerted(string npcId) => alerted.Add(npcId);

        private void LateUpdate()
        {
            float level = Level;
            bool show = level > 0.01f || Spotted;
            group.alpha = Mathf.MoveTowards(group.alpha, show ? 1f : 0f, Time.unscaledDeltaTime * 4f);
            fill.fillAmount = Spotted ? 1f : level;
            fill.color = Spotted ? new Color(1f, 0.23f, 0.28f) : Color.Lerp(new Color(1f, 0.85f, 0.3f), new Color(1f, 0.45f, 0.15f), level);
            label.text = Spotted ? "<color=#FF4D5A>발각 · SPOTTED</color>" : $"주의 · SUSPICION  {Mathf.RoundToInt(level * 100f)}%";
        }
    }
}
