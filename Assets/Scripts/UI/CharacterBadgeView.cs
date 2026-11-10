using Contract.Player;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Contract.UI
{
    /// <summary>HUD badge with the active operative's name and specialty.</summary>
    public sealed class CharacterBadgeView : MonoBehaviour
    {
        [SerializeField] private TMP_Text nameText;
        [SerializeField] private Image swatch;

        private void Start()
        {
            PlayerCharacterApplier applier = FindAnyObjectByType<PlayerCharacterApplier>();
            if (applier == null || applier.CurrentCharacter == null) return;
            nameText.text = $"{applier.CurrentCharacter.displayName}\n<size=70%><color=#9AA3AE>{applier.CurrentCharacter.specialty.ToUpperInvariant()}</color></size>";
            swatch.color = applier.CurrentCharacter.appearance.topColor;
        }
    }
}
