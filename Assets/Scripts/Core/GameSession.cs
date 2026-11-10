using System;
using System.Collections.Generic;
using Contract.Data;
using UnityEngine;

namespace Contract.Core
{
    [Serializable]
    public sealed class GameSession
    {
        public string selectedCharacterId = string.Empty;
        public int money;
        public long lifetimeEarnings;
        public bool hasStarted;
        public bool victoryAchieved;
        public bool gameOver;

        public string currentRegionId = string.Empty;
        public string currentObjectiveId = string.Empty;
        public string activeMissionId = string.Empty;

        public List<string> completedMissionIds = new List<string>();
        public List<string> unlockedRegionIds = new List<string>();
        public List<string> purchasedUpgradeIds = new List<string>();
        public List<string> inventoryItemIds = new List<string>();
        public List<string> collectedEvidenceIds = new List<string>();
        public List<string> unlockedCharacterIds = new List<string>();
        public List<string> completedRegionIds = new List<string>();
        public List<LedgerEntry> ledger = new List<LedgerEntry>();
        public int missionsFailed;
        public string equippedOutfitId = string.Empty;

        // GPS Activity Mode (totals only: no positions are ever stored)
        public double gpsTotalRealMeters;
        public double gpsTotalVirtualMeters;
        public double gpsRouteMeters;           // the character's progress along the virtual route
        public double gpsRouteOffset;           // where on the loop that progress starts (set when GPS mode starts mid-city)
        public float gpsTotalSeconds;
        public ActivitySessionState gpsSession = new ActivitySessionState();
        public bool gpsModeActive;
        public long gpsTotalSteps;
        public int xp;
        public List<MilestoneCount> activityMilestones = new List<MilestoneCount>();

        // GPS missions, checkpoints, exploration and collectibles
        public List<string> unlockedMissionIds = new List<string>();
        public List<string> completedActivityMissionIds = new List<string>();
        public List<ActivityBaseline> activityBaselines = new List<ActivityBaseline>();
        public List<string> activityObjectivesDone = new List<string>();      // "missionId/objectiveId"
        public List<PlayerCheckpoint> playerCheckpoints = new List<PlayerCheckpoint>();
        public List<string> completedCheckpointIds = new List<string>();
        public string explorationSalt = string.Empty;                          // per career: area hashes reveal no location
        public List<string> exploredAreas = new List<string>();                // salted hashes of ~100 m cells
        public List<string> pendingCollectibleIds = new List<string>();        // discovered, not yet collected

        // Destinations (World Selection)
        public List<string> unlockedDestinationIds = new List<string>();
        public string currentDestinationId = string.Empty;
        public float playTimeSeconds;

        // Where the operative stood when the career was saved in the city (restored on Continue).
        public bool hasSavedPlayerPosition;
        public Vector3 savedPlayerPosition;
        public float savedPlayerYaw;

        // Active contract progress (kept in the session so it survives scene loads and saves).
        public List<string> activeObjectivesDone = new List<string>();
        public List<string> activeObjectivesFailed = new List<string>();
        public bool activeMissionDetected;
        public float activeMissionElapsed;

        [NonSerialized] private Dictionary<string, float> suspicionLevels;

        /// <summary>Bumped whenever the owned upgrades change (cache key for PlayerUpgrades).</summary>
        [NonSerialized] public int upgradesVersion;
        [NonSerialized] private GameSettings settings;

        public int CompletedMissionCount => completedMissionIds.Count;

        public int TotalMissionsCompleted => completedMissionIds.Count;

        public GameSettings Settings
        {
            get
            {
                if (settings == null)
                {
                    settings = new GameSettings();
                }

                return settings;
            }
            set => settings = value;
        }

        public Dictionary<string, float> SuspicionLevels
        {
            get
            {
                if (suspicionLevels == null)
                {
                    suspicionLevels = new Dictionary<string, float>();
                }

                return suspicionLevels;
            }
        }

        public bool IsMissionCompleted(string missionId)
        {
            return !string.IsNullOrEmpty(missionId) && completedMissionIds.Contains(missionId);
        }

        public bool AddMissionCompletion(string missionId)
        {
            if (string.IsNullOrEmpty(missionId) || completedMissionIds.Contains(missionId))
            {
                return false;
            }

            completedMissionIds.Add(missionId);
            return true;
        }

        public bool IsRegionCompleted(string regionId) => !string.IsNullOrEmpty(regionId) && completedRegionIds.Contains(regionId);

        /// <summary>Clears the active contract's progress (start / finish / abandon).</summary>
        public void ClearActiveMission()
        {
            activeMissionId = string.Empty;
            currentObjectiveId = string.Empty;
            activeObjectivesDone.Clear();
            activeObjectivesFailed.Clear();
            activeMissionDetected = false;
            activeMissionElapsed = 0f;
        }

        public bool IsRegionUnlocked(string regionId)
        {
            return !string.IsNullOrEmpty(regionId) && unlockedRegionIds.Contains(regionId);
        }

        public bool UnlockRegion(string regionId)
        {
            if (string.IsNullOrEmpty(regionId) || unlockedRegionIds.Contains(regionId))
            {
                return false;
            }

            unlockedRegionIds.Add(regionId);
            return true;
        }

        public bool HasUpgrade(string upgradeId) => GetUpgradeLevel(upgradeId) > 0;

        /// <summary>Upgrade levels are stored as one id entry per purchased level.</summary>
        public int GetUpgradeLevel(string upgradeId)
        {
            if (string.IsNullOrEmpty(upgradeId)) return 0;
            int level = 0;
            foreach (string id in purchasedUpgradeIds)
            {
                if (id == upgradeId) level++;
            }

            return level;
        }

        /// <summary>Records one more level of an upgrade (the caller checks price and max level).</summary>
        public bool PurchaseUpgrade(string upgradeId)
        {
            if (string.IsNullOrEmpty(upgradeId)) return false;
            purchasedUpgradeIds.Add(upgradeId);
            upgradesVersion++;
            return true;
        }

        public bool HasItem(string itemId)
        {
            return !string.IsNullOrEmpty(itemId) && inventoryItemIds.Contains(itemId);
        }

        public bool AddItem(string itemId)
        {
            if (string.IsNullOrEmpty(itemId) || inventoryItemIds.Contains(itemId))
            {
                return false;
            }

            inventoryItemIds.Add(itemId);
            return true;
        }

        public bool RemoveItem(string itemId)
        {
            return inventoryItemIds.Remove(itemId);
        }

        public bool HasEvidence(string evidenceId)
        {
            return !string.IsNullOrEmpty(evidenceId) && collectedEvidenceIds.Contains(evidenceId);
        }

        public bool AddEvidence(string evidenceId)
        {
            if (string.IsNullOrEmpty(evidenceId) || collectedEvidenceIds.Contains(evidenceId))
            {
                return false;
            }

            collectedEvidenceIds.Add(evidenceId);
            return true;
        }

        public bool IsCharacterUnlocked(string characterId)
        {
            return !string.IsNullOrEmpty(characterId) && unlockedCharacterIds.Contains(characterId);
        }

        public bool UnlockCharacter(string characterId)
        {
            if (string.IsNullOrEmpty(characterId) || unlockedCharacterIds.Contains(characterId))
            {
                return false;
            }

            unlockedCharacterIds.Add(characterId);
            return true;
        }

        public void SetSuspicion(string npcId, float level)
        {
            if (string.IsNullOrEmpty(npcId))
            {
                return;
            }

            SuspicionLevels[npcId] = Mathf.Clamp01(level);
        }

        public float GetSuspicion(string npcId)
        {
            return SuspicionLevels.TryGetValue(npcId, out float value) ? value : 0f;
        }

        public void AddMoney(int amount)
        {
            if (amount == 0)
            {
                return;
            }

            money = Math.Max(0, money + amount);
            if (amount > 0)
            {
                lifetimeEarnings += amount;
            }
        }

        public bool TrySpend(int amount)
        {
            if (amount <= 0 || money < amount)
            {
                return false;
            }

            money -= amount;
            return true;
        }

        public void Reset()
        {
            selectedCharacterId = string.Empty;
            money = 0;
            lifetimeEarnings = 0;
            hasStarted = false;
            victoryAchieved = false;
            gameOver = false;
            currentRegionId = string.Empty;
            currentObjectiveId = string.Empty;
            activeMissionId = string.Empty;
            completedMissionIds.Clear();
            unlockedRegionIds.Clear();
            purchasedUpgradeIds.Clear();
            upgradesVersion++;
            inventoryItemIds.Clear();
            collectedEvidenceIds.Clear();
            unlockedCharacterIds.Clear();
            completedRegionIds.Clear();
            ledger.Clear();
            missionsFailed = 0;
            equippedOutfitId = string.Empty;
            playTimeSeconds = 0f;
            gpsTotalRealMeters = gpsTotalVirtualMeters = gpsRouteMeters = gpsRouteOffset = 0;
            gpsTotalSeconds = 0f;
            gpsSession = new ActivitySessionState();
            gpsModeActive = false;
            gpsTotalSteps = 0;
            xp = 0;
            activityMilestones.Clear();
            unlockedMissionIds.Clear();
            completedActivityMissionIds.Clear();
            activityBaselines.Clear();
            activityObjectivesDone.Clear();
            playerCheckpoints.Clear();
            completedCheckpointIds.Clear();
            explorationSalt = string.Empty;
            exploredAreas.Clear();
            pendingCollectibleIds.Clear();
            unlockedDestinationIds.Clear();
            currentDestinationId = string.Empty;
            hasSavedPlayerPosition = false;
            ClearActiveMission();
            suspicionLevels?.Clear();
            settings = new GameSettings();
        }

        public void ResetProgression()
        {
            selectedCharacterId = string.Empty;
            money = 0;
            lifetimeEarnings = 0;
            hasStarted = false;
            victoryAchieved = false;
            gameOver = false;
            currentRegionId = string.Empty;
            currentObjectiveId = string.Empty;
            activeMissionId = string.Empty;
            completedMissionIds.Clear();
            unlockedRegionIds.Clear();
            purchasedUpgradeIds.Clear();
            upgradesVersion++;
            inventoryItemIds.Clear();
            collectedEvidenceIds.Clear();
            unlockedCharacterIds.Clear();
            completedRegionIds.Clear();
            ledger.Clear();
            missionsFailed = 0;
            equippedOutfitId = string.Empty;
            playTimeSeconds = 0f;
            gpsTotalRealMeters = gpsTotalVirtualMeters = gpsRouteMeters = gpsRouteOffset = 0;
            gpsTotalSeconds = 0f;
            gpsSession = new ActivitySessionState();
            gpsModeActive = false;
            gpsTotalSteps = 0;
            xp = 0;
            activityMilestones.Clear();
            unlockedMissionIds.Clear();
            completedActivityMissionIds.Clear();
            activityBaselines.Clear();
            activityObjectivesDone.Clear();
            playerCheckpoints.Clear();
            completedCheckpointIds.Clear();
            explorationSalt = string.Empty;
            exploredAreas.Clear();
            pendingCollectibleIds.Clear();
            unlockedDestinationIds.Clear();
            currentDestinationId = string.Empty;
            hasSavedPlayerPosition = false;
            ClearActiveMission();
            suspicionLevels?.Clear();
        }
    }

    /// <summary>The current GPS walking session (saved, so an interrupted session can be resumed).</summary>
    [Serializable]
    public sealed class ActivitySessionState
    {
        public const int Idle = 0, Running = 1, Paused = 2;

        public int state = Idle;
        public double realMeters;
        public double virtualMeters;
        public float seconds;
        public long startedUnixMs;
        public bool interrupted;     // the game closed or went to the background during the session
        public long steps;           // hardware step counter, this session
        public int xpAtStart;
    }

    /// <summary>How many times an activity milestone has been awarded (repeating milestones count up).</summary>
    [Serializable]
    public sealed class MilestoneCount
    {
        public string id;
        public int count;
    }

    /// <summary>Career totals when an activity mission became available (its progress counts from here).</summary>
    [Serializable]
    public sealed class ActivityBaseline
    {
        public string missionId;
        public double meters;
        public long steps;
        public int areas;
    }

    /// <summary>
    /// A checkpoint the player marked themselves ("come back here"). Its position is the only
    /// location the game keeps, because the feature needs it; the player can delete it.
    /// </summary>
    [Serializable]
    public sealed class PlayerCheckpoint
    {
        public string id;
        public string name;
        public double latitude;
        public double longitude;
        public float radius = 25f;
        public double metersAtPlacement;   // career metres when placed
        public bool armed;                 // the player has been far enough away to "return"
    }
}
