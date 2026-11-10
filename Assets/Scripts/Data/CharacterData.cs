using System;
using UnityEngine;

namespace Contract.Data
{
    [CreateAssetMenu(menuName = "CONTRACT/Character Data", fileName = "CharacterData")]
    public sealed class CharacterData : ScriptableObject
    {
        [Header("Identity")]
        [field: SerializeField] public string characterId { get; set; }
        [field: SerializeField] public string displayName { get; set; }
        [field: SerializeField] public string specialty { get; set; }
        [field: SerializeField] public string description { get; set; }

        [Header("Preview")]
        [Tooltip("Optional rigged humanoid model (e.g. Mixamo FBX prefab). Used in-game and in the selection preview. When empty, a placeholder humanoid is built from 'appearance'.")]
        [field: SerializeField] public GameObject characterPreviewPrefab { get; set; }
        [field: SerializeField] public Sprite avatarSprite { get; set; }
        [field: SerializeField] public Color themeColor { get; set; } = Color.red;

        [Header("Appearance")]
        [field: SerializeField] public CharacterAppearance appearance { get; set; } = new CharacterAppearance();

        [Header("Stats")]
        [field: SerializeField, Range(0f, 100f)] public float moveSpeed { get; set; } = 100f;
        [field: SerializeField, Range(0f, 100f)] public float sprintSpeed { get; set; } = 100f;
        [field: SerializeField, Range(0f, 100f)] public float stealthRating { get; set; } = 100f;
        [field: SerializeField, Range(0f, 100f)] public float intelligence { get; set; } = 100f;
        [field: SerializeField, Range(0f, 100f)] public float suspicionDecay { get; set; } = 100f;
        [field: SerializeField, Range(0f, 100f)] public float suspicionBuildRate { get; set; } = 100f;
        [field: SerializeField, Range(0f, 100f)] public float interactionRange { get; set; } = 100f;
        [field: SerializeField, Range(0f, 100f)] public float lockpickChance { get; set; } = 100f;
        [field: SerializeField, Range(0f, 100f)] public float startingMoney { get; set; }

        [Header("Progression")]
        [field: SerializeField] public bool unlockedByDefault { get; set; } = true;
        [field: SerializeField] public string unlockRequirement { get; set; }
        [field: SerializeField] public int unlockCost { get; set; }

        public float GetMoveSpeedMultiplier() => moveSpeed / 100f;
        public float GetSprintSpeedMultiplier() => sprintSpeed / 100f;
        public float GetStealthMultiplier() => stealthRating / 100f;
        public float GetSuspicionDecayMultiplier() => suspicionDecay / 100f;
        public float GetSuspicionBuildMultiplier() => suspicionBuildRate / 100f;
        public float GetInteractionRangeMultiplier() => interactionRange / 100f;

        /// <summary>
        /// Converts a 0-100 stat into a gameplay multiplier centred on 80 (the roster average),
        /// so stats differentiate characters (roughly +/-10%) without breaking level tuning.
        /// </summary>
        public static float StatToMultiplier(float stat) => 1f + (Mathf.Clamp(stat, 0f, 100f) - 80f) / 200f;
    }
}