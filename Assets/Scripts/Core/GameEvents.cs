using System;
using Contract.Data;
using UnityEngine;

namespace Contract.Core
{
    /// <summary>
    /// Central event bus for the entire game. Static events are reset on
    /// subsystem registration (domain reload) to prevent stale listeners.
    /// </summary>
    public static class GameEvents
    {
        public static event Action<string> SceneLoadStarted;
        public static event Action<string> SceneLoadCompleted;
        public static event Action<string> CharacterSelected;
        public static event Action<string> CharacterUnlocked;
        public static event Action<int> MoneyChanged;
        public static event Action<string> MissionCompleted;
        public static event Action<string> MissionStarted;
        public static event Action<string> ObjectiveCompleted;
        public static event Action<string> ObjectiveUpdated;
        public static event Action<string> RegionUnlocked;
        public static event Action<string> RegionCompleted;
        public static event Action<bool> PauseChanged;
        public static event Action SessionStarted;
        public static event Action SessionReset;
        public static event Action VictoryReached;
        public static event Action<string> InteractionPerformed;
        public static event Action<string> ItemCollected;
        public static event Action<string> ItemAddedToInventory;
        public static event Action<string> SafehouseSecured;
        public static event Action<string> ExtractionActivated;
        public static event Action<string> MapOpened;
        public static event Action<string> MapClosed;
        public static event Action<string> NpcAlerted;
        public static event Action<string, float> SuspicionChanged;
        public static event Action<string> UpgradePurchased;
        public static event Action<string> OutfitChanged;                  // upgradeId ("" = own clothes)
        public static event Action<int, string> XpGained;                  // amount, source
        public static event Action<int> LevelUp;                           // new level
        public static event Action<string> ActivityMilestoneReached;       // milestone id
        public static event Action<float, float, float> AudioVolumeChanged;
        public static event Action SettingsApplied;
        public static event Action<string> SaveCompleted;
        public static event Action<string> LoadCompleted;
        public static event Action NewGameStarted;
        public static event Action GameOver;
        public static event Action CoverEntered;
        public static event Action CoverExited;

        // Localization
        public static event Action<string> LanguageChanged;

        // Gameplay -> missions. Raised by interactions, triggers and mission characters; the mission
        // system matches (type, id) against the active mission's objectives.
        public static event Action<ObjectiveType, string> ObjectiveSignal;

        // Missions
        public static event Action<string, string> MissionFailed;          // missionId, reason
        public static event Action<MissionResult> MissionResultReady;      // reward breakdown / failure
        public static event Action<string, string> MissionMessage;         // Korean, English (HUD toast)
        public static event Action ContractBoardRequested;
        public static event Action MenuBack;                               // Escape / Start while an overlay is open

        public static void RaiseMissionFailed(string missionId, string reason) => MissionFailed?.Invoke(missionId, reason);
        public static void RaiseMissionResultReady(MissionResult result) => MissionResultReady?.Invoke(result);
        public static void RaiseMissionMessage(string korean, string english) => MissionMessage?.Invoke(korean, english);
        public static void RaiseContractBoardRequested() => ContractBoardRequested?.Invoke();
        public static void RaiseMenuBack() => MenuBack?.Invoke();

        // NPC dialogue (npcId, Korean line, English subtitle) for subtitles / HUD.
        public static event Action<string, string, string> NpcSpoke;

        public static void RaiseObjectiveSignal(ObjectiveType type, string targetId)
        {
            if (string.IsNullOrEmpty(targetId)) return;
            Debug.Log($"[Objective] {type}: {targetId}");
            ObjectiveSignal?.Invoke(type, targetId);
        }

        public static void RaiseNpcSpoke(string npcId, string korean, string english) => NpcSpoke?.Invoke(npcId, korean, english);

        public static void RaiseSceneLoadStarted(string sceneName) => SceneLoadStarted?.Invoke(sceneName);
        public static void RaiseSceneLoadCompleted(string sceneName) => SceneLoadCompleted?.Invoke(sceneName);
        public static void RaiseCharacterSelected(string characterId) => CharacterSelected?.Invoke(characterId);
        public static void RaiseCharacterUnlocked(string characterId) => CharacterUnlocked?.Invoke(characterId);
        public static void RaiseMoneyChanged(int totalMoney) => MoneyChanged?.Invoke(totalMoney);
        public static void RaiseMissionCompleted(string missionId) => MissionCompleted?.Invoke(missionId);
        public static void RaiseMissionStarted(string missionId) => MissionStarted?.Invoke(missionId);
        public static void RaiseObjectiveCompleted(string objectiveId) => ObjectiveCompleted?.Invoke(objectiveId);
        public static void RaiseObjectiveUpdated(string objectiveId) => ObjectiveUpdated?.Invoke(objectiveId);
        public static void RaiseRegionUnlocked(string regionId) => RegionUnlocked?.Invoke(regionId);
        public static void RaiseRegionCompleted(string regionId) => RegionCompleted?.Invoke(regionId);
        public static void RaisePauseChanged(bool isPaused) => PauseChanged?.Invoke(isPaused);
        public static void RaiseSessionStarted() => SessionStarted?.Invoke();
        public static void RaiseSessionReset() => SessionReset?.Invoke();
        public static void RaiseVictoryReached() => VictoryReached?.Invoke();
        public static void RaiseInteractionPerformed(string interactionId) => InteractionPerformed?.Invoke(interactionId);
        public static void RaiseItemCollected(string itemId) => ItemCollected?.Invoke(itemId);
        public static void RaiseItemAddedToInventory(string itemId) => ItemAddedToInventory?.Invoke(itemId);
        public static void RaiseSafehouseSecured(string safehouseId) => SafehouseSecured?.Invoke(safehouseId);
        public static void RaiseExtractionActivated(string extractionId) => ExtractionActivated?.Invoke(extractionId);
        public static void RaiseMapOpened(string mapId) => MapOpened?.Invoke(mapId);
        public static void RaiseMapClosed(string mapId) => MapClosed?.Invoke(mapId);
        public static void RaiseNpcAlerted(string npcId) => NpcAlerted?.Invoke(npcId);
        public static void RaiseSuspicionChanged(string npcId, float level) => SuspicionChanged?.Invoke(npcId, level);
        public static void RaiseUpgradePurchased(string upgradeId) => UpgradePurchased?.Invoke(upgradeId);
        public static void RaiseOutfitChanged(string upgradeId) => OutfitChanged?.Invoke(upgradeId);
        public static void RaiseXpGained(int amount, string source) => XpGained?.Invoke(amount, source);
        public static void RaiseLevelUp(int level) => LevelUp?.Invoke(level);
        public static void RaiseActivityMilestoneReached(string id) => ActivityMilestoneReached?.Invoke(id);
        public static void RaiseAudioVolumeChanged(float master, float music, float sfx) => AudioVolumeChanged?.Invoke(master, music, sfx);
        public static void RaiseSettingsApplied() => SettingsApplied?.Invoke();
        public static void RaiseSaveCompleted(string slot) => SaveCompleted?.Invoke(slot);
        public static void RaiseLoadCompleted(string slot) => LoadCompleted?.Invoke(slot);
        public static void RaiseNewGameStarted() => NewGameStarted?.Invoke();
        public static void RaiseGameOver() => GameOver?.Invoke();
        public static void RaiseCoverEntered() => CoverEntered?.Invoke();
        public static void RaiseCoverExited() => CoverExited?.Invoke();

        // Localization
        public static void RaiseLanguageChanged(string langCode) => LanguageChanged?.Invoke(langCode);

        // VFX Events
        public static event Action<Color, float> ScreenFlash;
        public static event Action<float, float> VignetteChange;
        public static event Action<float, float> ChromaticAberrationChange;

        public static void RaiseScreenFlash(Color color, float duration) => ScreenFlash?.Invoke(color, duration);
        public static void RaiseVignetteChange(float intensity, float duration) => VignetteChange?.Invoke(intensity, duration);
        public static void RaiseChromaticAberrationChange(float intensity, float duration) => ChromaticAberrationChange?.Invoke(intensity, duration);

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            SceneLoadStarted = null;
            SceneLoadCompleted = null;
            CharacterSelected = null;
            CharacterUnlocked = null;
            MoneyChanged = null;
            MissionCompleted = null;
            MissionStarted = null;
            ObjectiveCompleted = null;
            ObjectiveUpdated = null;
            RegionUnlocked = null;
            RegionCompleted = null;
            PauseChanged = null;
            SessionStarted = null;
            SessionReset = null;
            VictoryReached = null;
            InteractionPerformed = null;
            ItemCollected = null;
            ItemAddedToInventory = null;
            SafehouseSecured = null;
            ExtractionActivated = null;
            MapOpened = null;
            MapClosed = null;
            NpcAlerted = null;
            SuspicionChanged = null;
            UpgradePurchased = null;
            OutfitChanged = null;
            XpGained = null;
            LevelUp = null;
            ActivityMilestoneReached = null;
            AudioVolumeChanged = null;
            SettingsApplied = null;
            SaveCompleted = null;
            LoadCompleted = null;
            NewGameStarted = null;
            GameOver = null;
            CoverEntered = null;
            CoverExited = null;
            ScreenFlash = null;
            VignetteChange = null;
            ChromaticAberrationChange = null;
            LanguageChanged = null;
            ObjectiveSignal = null;
            NpcSpoke = null;
            MissionFailed = null;
            MissionResultReady = null;
            MissionMessage = null;
            ContractBoardRequested = null;
            MenuBack = null;
        }
    }
}