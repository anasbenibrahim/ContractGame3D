using System.Linq;
using Contract.Core;
using Contract.Data;
using Contract.Interaction;
using UnityEngine;

namespace Contract.Player
{
    /// <summary>
    /// Applies the selected character (GameSession.selectedCharacterId) to the player when the
    /// gameplay scene starts: stat-based speed and interaction multipliers, and the visual
    /// (rigged model if the CharacterData has one, otherwise an articulated placeholder humanoid).
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-50)]
    public sealed class PlayerCharacterApplier : MonoBehaviour
    {
        private const string BuiltVisualName = "CharacterVisual";

        [SerializeField] private ThirdPersonController controller;
        [SerializeField] private PlayerAnimationDriver animationDriver;
        [SerializeField] private InteractionController interactionController;

        [Tooltip("URP Lit material cloned per colour slot for the placeholder humanoid.")]
        [SerializeField] private Material placeholderBaseMaterial;

        [Tooltip("Animator Controller used for rigged humanoid models (built from Mixamo clips). Optional.")]
        [SerializeField] private RuntimeAnimatorController humanoidController;

        public CharacterData CurrentCharacter { get; private set; }
        public GameObject CurrentVisual { get; private set; }

        /// <summary>Stealth multiplier (~0.9-1.1) for NPC detection systems.</summary>
        public float StealthMultiplier { get; private set; } = 1f;

        private void Awake()
        {
            if (controller == null) controller = GetComponent<ThirdPersonController>();
            if (animationDriver == null) animationDriver = GetComponent<PlayerAnimationDriver>();
            if (interactionController == null) interactionController = GetComponent<InteractionController>();
            if (!TryGetComponent(out FootstepAudio _)) gameObject.AddComponent<FootstepAudio>();
            if (!TryGetComponent(out GpsRouteFollower _)) gameObject.AddComponent<GpsRouteFollower>(); // GPS Life Mode

            Apply(ResolveSelectedCharacter());
        }

        private void OnEnable() => GameEvents.OutfitChanged += OnOutfitChanged;

        private void OnDisable() => GameEvents.OutfitChanged -= OnOutfitChanged;

        // Equipping an outfit in the safehouse shop re-dresses the operative immediately.
        private void OnOutfitChanged(string outfitId)
        {
            if (CurrentCharacter != null) ApplyVisual(CurrentCharacter);
        }

        /// <summary>The session's selected character, falling back to the first default-unlocked one.</summary>
        public static CharacterData ResolveSelectedCharacter()
        {
            GameDatabase database = GameDatabase.Load();
            if (database == null || database.Characters.Length == 0)
            {
                return null;
            }

            string selectedId = GameManager.Instance?.Session?.selectedCharacterId;
            CharacterData selected = database.Characters.FirstOrDefault(c => c != null && c.characterId == selectedId);
            return selected != null
                ? selected
                : database.Characters.FirstOrDefault(c => c != null && c.unlockedByDefault) ?? database.Characters[0];
        }

        public void Apply(CharacterData character)
        {
            if (character == null)
            {
                Debug.LogWarning("[PlayerCharacterApplier] No character data available; keeping the default player.", this);
                return;
            }

            CurrentCharacter = character;
            ApplyStats(character);
            ApplyVisual(character);
        }

        private void ApplyStats(CharacterData character)
        {
            controller?.ApplyCharacterMultipliers(
                CharacterData.StatToMultiplier(character.moveSpeed),
                CharacterData.StatToMultiplier(character.sprintSpeed));

            interactionController?.SetRangeMultiplier(CharacterData.StatToMultiplier(character.interactionRange));
            // Intelligence: smarter operatives hack terminals / plant devices faster (Ryan Blake ~ +7.5%).
            interactionController?.SetHoldSpeedMultiplier(CharacterData.StatToMultiplier(character.intelligence));
            StealthMultiplier = CharacterData.StatToMultiplier(character.stealthRating);
        }

        private void ApplyVisual(CharacterData character)
        {
            Transform visualRoot = controller != null && controller.VisualRoot != null ? controller.VisualRoot : transform;

            // Remove the legacy primitive body and any visual built for a previous character.
            for (int i = visualRoot.childCount - 1; i >= 0; i--)
            {
                GameObject old = visualRoot.GetChild(i).gameObject;
                old.SetActive(false); // Destroy is deferred; hide it now so only the new outfit renders
                Destroy(old);
            }

            Animator rootAnimator = GetComponent<Animator>();

            if (character.characterPreviewPrefab != null)
            {
                CurrentVisual = Instantiate(character.characterPreviewPrefab, visualRoot, false);
                CurrentVisual.name = BuiltVisualName;
                Animator modelAnimator = CurrentVisual.GetComponentInChildren<Animator>();
                if (modelAnimator != null)
                {
                    if (humanoidController != null) modelAnimator.runtimeAnimatorController = humanoidController;
                    modelAnimator.applyRootMotion = false;
                }

                if (rootAnimator != null) rootAnimator.enabled = false; // the legacy clips target the primitive body
                animationDriver?.SetAnimator(modelAnimator);
            }
            else
            {
                PlaceholderRig rig = PlaceholderHumanoidBuilder.Build(visualRoot, PlayerUpgrades.Dress(character.appearance), ResolveBaseMaterial(), BuiltVisualName);
                CurrentVisual = rig.gameObject;
                if (rootAnimator != null) rootAnimator.enabled = false;
                animationDriver?.SetAnimator(null);
            }

            SetLayerRecursively(CurrentVisual, gameObject.layer);
            controller?.SetLegacyVisualEffects(false);
        }

        private Material ResolveBaseMaterial()
        {
            if (placeholderBaseMaterial != null)
            {
                return placeholderBaseMaterial;
            }

            // Fallback for scenes where the reference was not assigned.
            Shader lit = Shader.Find("Universal Render Pipeline/Lit");
            placeholderBaseMaterial = new Material(lit) { name = "PlaceholderBase (runtime)" };
            return placeholderBaseMaterial;
        }

        private static void SetLayerRecursively(GameObject root, int layer)
        {
            foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
            {
                t.gameObject.layer = layer;
            }
        }
    }
}
