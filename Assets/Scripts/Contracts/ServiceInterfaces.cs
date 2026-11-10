using System;
using Contract.Data;
using UnityEngine;

namespace Contract.Contracts
{
    public interface ISaveService
    {
        void SaveSlot(string slotId);
        bool LoadSlot(string slotId);
        bool SlotExists(string slotId);
        string[] GetAvailableSlots();
        void DeleteSlot(string slotId);
        void AutoSave();
        bool TryGetSummary(string slotId, out SaveSummary summary);
    }

    /// <summary>What the main menu shows about a save without loading it.</summary>
    [Serializable]
    public sealed class SaveSummary
    {
        public string characterId;
        public int money;
        public int contractsCompleted;
        public int regionsCompleted;
        public float playTimeSeconds;
        public long savedTimestamp;
        public bool victoryAchieved;
        public bool restoredFromBackup;
    }

    public interface IAudioService
    {
        void SetMasterVolume(float volume);
        void SetMusicVolume(float volume);
        void SetSFXVolume(float volume);
        void SetDialogueVolume(float volume);
        void SetAmbienceVolume(float volume);
        float MasterVolume { get; }
        float MusicVolume { get; }
        float SFXVolume { get; }
        float DialogueVolume { get; }
        float AmbienceVolume { get; }
        void PlayMusic(string musicId, bool loop = true);
        void StopMusic();
        void CrossfadeMusic(string newMusicId, float fadeDuration = 2f);
        void PlaySFX(string sfxId, Vector3 position = default, float pitch = 1f, float volume = 1f);
        void PlaySFX3D(string sfxId, Vector3 position, float maxDistance = 20f, float pitch = 1f, float volume = 1f);
        void PlayUI(string uiId);
        void PlayAmbience(string ambienceId, bool loop = true);
        void StopAmbience();
        void CrossfadeAmbience(string newAmbienceId, float fadeDuration = 3f);
        void Mute(bool mute);
        bool IsMuted { get; }
        string CurrentMusicId { get; }
        string CurrentAmbienceId { get; }
        string LastSfxId { get; }
        int SfxPlayCount { get; }
    }

    public interface IEconomyService
    {
        int GetMoney();
        void AddMoney(int amount);
        /// <summary>Adds money and records <paramref name="source"/> in the ledger.</summary>
        void AddMoney(int amount, string source);
        bool TrySpend(int amount);
        /// <summary>Spends money (if affordable) and records <paramref name="source"/> in the ledger.</summary>
        bool TrySpend(int amount, string source);
        void AwardMissionReward(int amount);
        void AwardRegionBonus(int amount);
    }
}