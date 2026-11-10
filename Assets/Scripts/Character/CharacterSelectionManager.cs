using System;
using System.Collections.Generic;
using Contract.Character;
using Contract.Core;
using Contract.Data;
using UnityEngine;

namespace Contract.Character
{
    /// <summary>
    /// Manages character data, selection, unlocking, and preview.
    /// </summary>
    public sealed class CharacterSelectionManager : MonoBehaviour, ICharacterService
    {
        public static CharacterSelectionManager Instance { get; private set; }

        [Header("Character Database")]
        [SerializeField] private CharacterData[] allCharacters;

        private Dictionary<string, CharacterData> charactersById;
        private string selectedCharacterId;

        public string GetSelectedCharacterId() => selectedCharacterId;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void CreateInstance()
        {
            if (Instance != null) return;
            GameObject go = new GameObject("[CharacterSelectionManager]");
            CharacterSelectionManager service = go.AddComponent<CharacterSelectionManager>();
            Instance = service;
            DontDestroyOnLoad(go);

            if (GameManager.Instance != null)
            {
                GameManager.Instance.RegisterService<ICharacterService>(service);
            }
        }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                DestroyImmediate(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);

            if (GameManager.Instance != null)
            {
                GameManager.Instance.RegisterService<ICharacterService>(this);
            }

            BuildLookup();
        }

        private void BuildLookup()
        {
            charactersById = new Dictionary<string, CharacterData>();

            if (allCharacters == null || allCharacters.Length == 0)
            {
                allCharacters = GameDatabase.Load()?.Characters;
            }
            if (allCharacters != null)
            {
                foreach (CharacterData data in allCharacters)
                {
                    if (data != null && !string.IsNullOrEmpty(data.characterId))
                    {
                        charactersById[data.characterId] = data;
                    }
                }
            }
        }

        public CharacterData GetCharacter(string characterId)
        {
            if (charactersById != null && charactersById.TryGetValue(characterId, out CharacterData data))
            {
                return data;
            }
            return null;
        }

        public IEnumerable<CharacterData> GetAvailableCharacters()
        {
            if (allCharacters == null) yield break;
            foreach (CharacterData data in allCharacters)
            {
                if (data != null) yield return data;
            }
        }

        public IEnumerable<CharacterData> GetUnlockedCharacters()
        {
            GameSession session = GameManager.Instance?.Session;
            if (session == null)
            {
                foreach (CharacterData data in GetAvailableCharacters())
                {
                    if (data.unlockedByDefault) yield return data;
                }
                yield break;
            }

            foreach (CharacterData data in GetAvailableCharacters())
            {
                if (session.IsCharacterUnlocked(data.characterId) || data.unlockedByDefault)
                {
                    yield return data;
                }
            }
        }

        public bool IsCharacterUnlocked(string characterId)
        {
            GameSession session = GameManager.Instance?.Session;
            if (session == null) return false;

            CharacterData data = GetCharacter(characterId);
            if (data == null) return false;

            if (data.unlockedByDefault) return true;
            return session.IsCharacterUnlocked(characterId);
        }

        public void UnlockCharacter(string characterId)
        {
            GameSession session = GameManager.Instance?.Session;
            if (session == null) return;

            if (session.UnlockCharacter(characterId))
            {
                GameEvents.RaiseCharacterUnlocked(characterId);
            }
        }

        public void SelectCharacter(string characterId)
        {
            if (string.IsNullOrEmpty(characterId)) return;

            CharacterData data = GetCharacter(characterId);
            if (data == null) return;

            selectedCharacterId = characterId;
            GameManager.Instance?.SetSelectedCharacter(characterId);
        }
    }
}