using System;
using System.Collections.Generic;
using Contract.Data;

namespace Contract.Character
{
    public interface ICharacterService
    {
        CharacterData GetCharacter(string characterId);
        IEnumerable<CharacterData> GetAvailableCharacters();
        IEnumerable<CharacterData> GetUnlockedCharacters();
        bool IsCharacterUnlocked(string characterId);
        void UnlockCharacter(string characterId);
        string GetSelectedCharacterId();
        void SelectCharacter(string characterId);
    }
}