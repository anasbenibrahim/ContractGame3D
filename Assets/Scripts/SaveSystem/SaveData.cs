using System;
using Contract.Core;
using Contract.Data;
using UnityEngine;

namespace Contract.SaveSystem
{
    /// <summary>
    /// On-disk career (JSON). Version 3 adds region completion, the money ledger, failed
    /// contracts, in-progress contract state, the equipped outfit, play time, V-Sync and the
    /// operative's position in the city. Older files load with those fields at their defaults.
    /// </summary>
    [Serializable]
    public sealed class SaveData
    {
        public int version = GameConstants.SaveVersion;
        public string slotId = GameConstants.Save.DefaultSlot;
        public string selectedCharacterId;
        public int money;
        public long lifetimeEarnings;
        public bool hasStarted;
        public bool victoryAchieved;
        public bool gameOver;
        public string currentRegionId;
        public string currentObjectiveId;
        public string activeMissionId;
        public string[] completedMissionIds;
        public string[] unlockedRegionIds;
        public string[] purchasedUpgradeIds;      // one entry per purchased level
        public string[] inventoryItemIds;
        public string[] collectedEvidenceIds;
        public string[] unlockedCharacterIds;
        public float[] suspicionValues;
        public string[] suspicionKeys;

        // v3: progression and finances
        public string[] completedRegionIds;
        public LedgerEntry[] ledger;
        public int missionsFailed;
        public string equippedOutfitId;
        public float playTimeSeconds;

        // v3: contract in progress
        public string[] activeObjectivesDone;
        public string[] activeObjectivesFailed;
        public bool activeMissionDetected;
        public float activeMissionElapsed;

        // v4: GPS Activity Mode (totals and the current session; never positions)
        public double gpsTotalRealMeters;
        public double gpsTotalVirtualMeters;
        public double gpsRouteMeters;
        public double gpsRouteOffset;   // v7
        public float gpsTotalSeconds;
        public ActivitySessionState gpsSession;
        public bool gpsModeActive;
        public bool gpsModeEnabled = true;
        public float gpsMovementRatio = 1f;
        public float gpsAccuracyThreshold = 25f;
        public bool activityNotifications = true;

        // Touch controls (absent in older saves: the initialisers below are the defaults)
        public int touchControls;
        public float joystickDeadZone = 0.12f;
        public float joystickSensitivity = 1f;
        public float touchLookSensitivityX = 1f;
        public float touchLookSensitivityY = 0.8f;
        public bool haptics = true;
        public float joystickSize = 1f;
        public float buttonOpacity = 0.85f;
        public int targetFramerate = 60;

        // v5: steps, XP and awarded activity milestones
        public long gpsTotalSteps;
        public int xp;
        public MilestoneCount[] activityMilestones;

        // v6: GPS missions, checkpoints, exploration (hashed areas), collectibles
        public string[] unlockedMissionIds;
        public string[] completedActivityMissionIds;
        public ActivityBaseline[] activityBaselines;
        public string[] activityObjectivesDone;
        public PlayerCheckpoint[] playerCheckpoints;
        public string[] completedCheckpointIds;
        public string explorationSalt;
        public string[] exploredAreas;
        public string[] pendingCollectibleIds;

        // v8: destinations
        public string[] unlockedDestinationIds;
        public string currentDestinationId;

        // v3: where the operative stood in the city
        public bool hasPlayerPosition;
        public Vector3Data playerPosition;
        public float playerYaw;

        // settings
        public float masterVolume = 1f;
        public float musicVolume = 0.8f;
        public float sfxVolume = 1f;
        public float mouseSensitivity = 1f;
        public float cameraDistance = GameConstants.DefaultCameraDistance;
        public bool invertCameraY;
        public float gamepadSensitivity = 1f;
        public int graphicsQuality = -1;
        public bool vSync = true;
        public long savedTimestamp;
    }

    [Serializable]
    public sealed class Vector3Data
    {
        public float x;
        public float y;
        public float z;

        public Vector3Data() { }
        public Vector3Data(float x, float y, float z) { this.x = x; this.y = y; this.z = z; }

        public static implicit operator Vector3(Vector3Data d) => new Vector3(d.x, d.y, d.z);
        public static implicit operator Vector3Data(Vector3 v) => new Vector3Data(v.x, v.y, v.z);
    }
}
