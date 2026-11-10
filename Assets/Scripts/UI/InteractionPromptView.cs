using Contract.Interaction;
using UnityEngine;
using UnityEngine.UI;

namespace Contract.UI
{
    /// <summary>Hold-progress bar under the interaction prompt (the prompt text itself is written by InteractionController).</summary>
    public sealed class InteractionPromptView : MonoBehaviour
    {
        [SerializeField] private Image holdFill;
        [SerializeField] private GameObject holdBar;

        private InteractionController interaction;

        private void Start()
        {
            GameObject player = GameObject.FindGameObjectWithTag("Player");
            if (player != null) interaction = player.GetComponent<InteractionController>();
        }

        private void LateUpdate()
        {
            float progress = interaction != null ? interaction.HoldProgress : -1f;
            holdBar.SetActive(progress >= 0f);
            if (progress >= 0f) holdFill.fillAmount = progress;
        }
    }
}
