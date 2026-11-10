using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Contract.Contracts;
using Contract.Core;
using Contract.Data;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Contract.SaveSystem
{
    /// <summary>
    /// Persists the career (GameSession) as JSON. Writes are atomic (temp file, then swap) and
    /// keep the previous save as a .bak backup that is used if the main file is damaged.
    /// Auto-saves every minute in an active career, after contracts and purchases, and on quit,
    /// but never overwrites a save with a career that has not started (e.g. quitting from the
    /// main menu without pressing Continue).
    /// </summary>
    public sealed class SaveManager : MonoBehaviour, ISaveService
    {
        public static SaveManager Instance { get; private set; }

        private const int MaxLedgerEntries = 500;
        private const string BackupExtension = ".bak";

        [SerializeField] private bool autoSaveEnabled = true;
        [SerializeField] private float autoSaveInterval = 60f;
        [SerializeField] private string saveRootFolder = "Saves";

        private string SaveDirectory => Path.Combine(Application.persistentDataPath, saveRootFolder);
        private float autoSaveTimer;

        private static GameSession Session => GameManager.Instance?.Session;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void CreateInstance()
        {
            if (Instance != null) return;
            new GameObject("[SaveManager]").AddComponent<SaveManager>();
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
            GameManager.Instance?.RegisterService<ISaveService>(this);
        }

        private void Update()
        {
            GameSession session = Session;
            if (!autoSaveEnabled || session == null || !session.hasStarted) return;

            autoSaveTimer += Time.unscaledDeltaTime;
            if (autoSaveTimer >= autoSaveInterval)
            {
                autoSaveTimer = 0f;
                AutoSave();
            }
        }

        private void OnApplicationQuit() => AutoSave();

        // Android rarely calls OnApplicationQuit: the OS kills backgrounded apps. Save when leaving the foreground.
        private void OnApplicationPause(bool paused)
        {
            if (paused) AutoSave();
        }

        /// <summary>Writes saves to another folder under persistentDataPath (benchmark runs, tests).</summary>
        public void RedirectSaves(string folder) => saveRootFolder = folder;

        // ---- slots ---------------------------------------------------------------------------

        public string GetSlotPath(string slotId) => Path.Combine(SaveDirectory, slotId + GameConstants.Save.FileExtension);

        private string BackupPath(string slotId) => GetSlotPath(slotId) + BackupExtension;

        public bool SlotExists(string slotId) => File.Exists(GetSlotPath(slotId)) || File.Exists(BackupPath(slotId));

        public string[] GetAvailableSlots()
        {
            if (!Directory.Exists(SaveDirectory)) return Array.Empty<string>();
            return Directory.GetFiles(SaveDirectory, "*" + GameConstants.Save.FileExtension).Select(Path.GetFileNameWithoutExtension).ToArray();
        }

        /// <summary>Saves the active career; does nothing before a career has started.</summary>
        public void AutoSave()
        {
            GameSession session = Session;
            if (session != null && session.hasStarted) SaveSlot(GameConstants.Save.DefaultSlot);
        }

        public void SaveSlot(string slotId)
        {
            GameSession session = Session;
            if (session == null) return;

            try
            {
                string json = JsonUtility.ToJson(BuildSaveData(session, slotId), true);
                Directory.CreateDirectory(SaveDirectory);
                string path = GetSlotPath(slotId);
                string temp = path + ".tmp";
                File.WriteAllText(temp, json, Encoding.UTF8);
                if (File.Exists(path))
                {
                    File.Copy(path, BackupPath(slotId), true); // previous save survives a bad write
                    File.Delete(path);
                }

                File.Move(temp, path);
                autoSaveTimer = 0f;
                GameEvents.RaiseSaveCompleted(slotId);
            }
            catch (Exception exception)
            {
                Debug.LogError($"[SaveManager] Failed to save slot '{slotId}': {exception.Message}");
            }
        }

        public bool LoadSlot(string slotId)
        {
            GameSession session = Session;
            if (session == null) return false;

            SaveData data = Read(slotId, out bool fromBackup);
            if (data == null) return false;
            if (fromBackup) Debug.LogWarning($"[SaveManager] Save '{slotId}' was damaged; loaded the backup instead.");

            ApplySaveData(session, data);
            GameEvents.RaiseLoadCompleted(slotId);
            return true;
        }

        public bool TryGetSummary(string slotId, out SaveSummary summary)
        {
            SaveData data = Read(slotId, out bool fromBackup);
            summary = data == null ? null : new SaveSummary
            {
                characterId = data.selectedCharacterId,
                money = data.money,
                contractsCompleted = data.completedMissionIds?.Length ?? 0,
                regionsCompleted = data.completedRegionIds?.Length ?? 0,
                playTimeSeconds = data.playTimeSeconds,
                savedTimestamp = data.savedTimestamp,
                victoryAchieved = data.victoryAchieved,
                restoredFromBackup = fromBackup
            };
            return summary != null;
        }

        public void DeleteSlot(string slotId)
        {
            foreach (string path in new[] { GetSlotPath(slotId), BackupPath(slotId), GetSlotPath(slotId) + ".tmp" })
            {
                if (File.Exists(path)) File.Delete(path);
            }
        }

        /// <summary>The slot's data, falling back to the backup if the main file is missing or damaged.</summary>
        private SaveData Read(string slotId, out bool fromBackup)
        {
            fromBackup = false;
            SaveData data = TryParse(GetSlotPath(slotId));
            if (data != null) return data;
            data = TryParse(BackupPath(slotId));
            fromBackup = data != null;
            return data;
        }

        private static SaveData TryParse(string path)
        {
            if (!File.Exists(path)) return null;
            try
            {
                SaveData data = JsonUtility.FromJson<SaveData>(File.ReadAllText(path, Encoding.UTF8));
                if (data == null || data.version <= 0 || data.version > GameConstants.SaveVersion) return null;
                return data;
            }
            catch (Exception exception)
            {
                Debug.LogWarning($"[SaveManager] Could not read '{Path.GetFileName(path)}': {exception.Message}");
                return null;
            }
        }

        // ---- session <-> file ------------------------------------------------------------------

        private static SaveData BuildSaveData(GameSession session, string slotId)
        {
            var data = new SaveData
            {
                version = GameConstants.SaveVersion,
                slotId = slotId,
                selectedCharacterId = session.selectedCharacterId,
                money = session.money,
                lifetimeEarnings = session.lifetimeEarnings,
                hasStarted = session.hasStarted,
                victoryAchieved = session.victoryAchieved,
                gameOver = session.gameOver,
                currentRegionId = session.currentRegionId,
                currentObjectiveId = session.currentObjectiveId,
                activeMissionId = session.activeMissionId,
                completedMissionIds = session.completedMissionIds.ToArray(),
                unlockedRegionIds = session.unlockedRegionIds.ToArray(),
                purchasedUpgradeIds = session.purchasedUpgradeIds.ToArray(),
                inventoryItemIds = session.inventoryItemIds.ToArray(),
                collectedEvidenceIds = session.collectedEvidenceIds.ToArray(),
                unlockedCharacterIds = session.unlockedCharacterIds.ToArray(),
                completedRegionIds = session.completedRegionIds.ToArray(),
                ledger = session.ledger.Skip(Math.Max(0, session.ledger.Count - MaxLedgerEntries)).ToArray(),
                missionsFailed = session.missionsFailed,
                equippedOutfitId = session.equippedOutfitId,
                playTimeSeconds = session.playTimeSeconds,
                activeObjectivesDone = session.activeObjectivesDone.ToArray(),
                activeObjectivesFailed = session.activeObjectivesFailed.ToArray(),
                activeMissionDetected = session.activeMissionDetected,
                activeMissionElapsed = session.activeMissionElapsed,
                masterVolume = session.Settings.masterVolume,
                musicVolume = session.Settings.musicVolume,
                sfxVolume = session.Settings.sfxVolume,
                mouseSensitivity = session.Settings.mouseSensitivity,
                cameraDistance = session.Settings.cameraDistance,
                invertCameraY = session.Settings.invertCameraY,
                gamepadSensitivity = session.Settings.gamepadSensitivity,
                graphicsQuality = session.Settings.graphicsQuality,
                vSync = session.Settings.vSync,
                gpsTotalRealMeters = session.gpsTotalRealMeters,
                gpsTotalVirtualMeters = session.gpsTotalVirtualMeters,
                gpsRouteMeters = session.gpsRouteMeters,
                gpsRouteOffset = session.gpsRouteOffset,
                gpsTotalSeconds = session.gpsTotalSeconds,
                gpsSession = session.gpsSession,
                gpsModeActive = session.gpsModeActive,
                gpsModeEnabled = session.Settings.gpsModeEnabled,
                gpsMovementRatio = session.Settings.gpsMovementRatio,
                gpsAccuracyThreshold = session.Settings.gpsAccuracyThreshold,
                activityNotifications = session.Settings.activityNotifications,
                touchControls = session.Settings.touchControls,
                joystickDeadZone = session.Settings.joystickDeadZone,
                joystickSensitivity = session.Settings.joystickSensitivity,
                touchLookSensitivityX = session.Settings.touchLookSensitivityX,
                touchLookSensitivityY = session.Settings.touchLookSensitivityY,
                haptics = session.Settings.haptics,
                joystickSize = session.Settings.joystickSize,
                buttonOpacity = session.Settings.buttonOpacity,
                targetFramerate = session.Settings.targetFramerate,
                gpsTotalSteps = session.gpsTotalSteps,
                xp = session.xp,
                activityMilestones = session.activityMilestones.ToArray(),
                unlockedMissionIds = session.unlockedMissionIds.ToArray(),
                completedActivityMissionIds = session.completedActivityMissionIds.ToArray(),
                activityBaselines = session.activityBaselines.ToArray(),
                activityObjectivesDone = session.activityObjectivesDone.ToArray(),
                playerCheckpoints = session.playerCheckpoints.ToArray(),
                completedCheckpointIds = session.completedCheckpointIds.ToArray(),
                explorationSalt = session.explorationSalt,
                exploredAreas = session.exploredAreas.ToArray(),
                pendingCollectibleIds = session.pendingCollectibleIds.ToArray(),
                unlockedDestinationIds = session.unlockedDestinationIds.ToArray(),
                currentDestinationId = session.currentDestinationId,
                savedTimestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
            };

            if (session.SuspicionLevels.Count > 0)
            {
                data.suspicionKeys = session.SuspicionLevels.Keys.ToArray();
                data.suspicionValues = session.SuspicionLevels.Values.ToArray();
            }

            // Saved in the city: remember where the operative stands (restored on Continue).
            GameObject player = GameConstants.Scenes.InGameplay
                ? GameObject.FindGameObjectWithTag("Player") : null;
            if (player != null)
            {
                data.hasPlayerPosition = true;
                data.playerPosition = player.transform.position;
                data.playerYaw = player.transform.eulerAngles.y;
            }

            return data;
        }

        private static double Valid(double value) => double.IsNaN(value) || double.IsInfinity(value) || value < 0 ? 0 : value;

        private static void ApplySaveData(GameSession session, SaveData data)
        {
            List<string> List(string[] values) => values != null ? new List<string>(values) : new List<string>();

            session.selectedCharacterId = data.selectedCharacterId ?? string.Empty;
            session.money = data.money;
            session.lifetimeEarnings = data.lifetimeEarnings;
            session.hasStarted = data.hasStarted;
            session.victoryAchieved = data.victoryAchieved;
            session.gameOver = data.gameOver;
            session.currentRegionId = data.currentRegionId ?? string.Empty;
            session.currentObjectiveId = data.currentObjectiveId ?? string.Empty;
            session.activeMissionId = data.activeMissionId ?? string.Empty;
            session.completedMissionIds = List(data.completedMissionIds);
            session.unlockedRegionIds = List(data.unlockedRegionIds);
            session.purchasedUpgradeIds = List(data.purchasedUpgradeIds);
            session.upgradesVersion++;
            session.inventoryItemIds = List(data.inventoryItemIds);
            session.collectedEvidenceIds = List(data.collectedEvidenceIds);
            session.unlockedCharacterIds = List(data.unlockedCharacterIds);
            session.completedRegionIds = List(data.completedRegionIds);
            session.ledger = data.ledger != null ? new List<LedgerEntry>(data.ledger) : new List<LedgerEntry>();
            session.missionsFailed = data.missionsFailed;
            session.equippedOutfitId = data.equippedOutfitId ?? string.Empty;
            session.playTimeSeconds = data.playTimeSeconds;
            session.activeObjectivesDone = List(data.activeObjectivesDone);
            session.activeObjectivesFailed = List(data.activeObjectivesFailed);
            session.activeMissionDetected = data.activeMissionDetected;
            session.activeMissionElapsed = data.activeMissionElapsed;

            session.hasSavedPlayerPosition = data.hasPlayerPosition && data.playerPosition != null;
            if (session.hasSavedPlayerPosition)
            {
                session.savedPlayerPosition = data.playerPosition;
                session.savedPlayerYaw = data.playerYaw;
            }

            GameSettings settings = session.Settings;
            settings.masterVolume = data.masterVolume;
            settings.musicVolume = data.musicVolume;
            settings.sfxVolume = data.sfxVolume;
            settings.mouseSensitivity = data.mouseSensitivity > 0f ? data.mouseSensitivity : 1f;
            settings.cameraDistance = data.cameraDistance > 0f ? data.cameraDistance : GameConstants.DefaultCameraDistance;
            settings.invertCameraY = data.invertCameraY;
            settings.gamepadSensitivity = data.gamepadSensitivity > 0f ? data.gamepadSensitivity : 1f;
            settings.graphicsQuality = data.graphicsQuality;
            settings.vSync = data.vSync;
            settings.gpsModeEnabled = data.gpsModeEnabled;
            settings.gpsMovementRatio = Mathf.Clamp(data.gpsMovementRatio > 0f ? data.gpsMovementRatio : 1f, 0.5f, 3f);
            settings.gpsAccuracyThreshold = Mathf.Clamp(data.gpsAccuracyThreshold > 0f ? data.gpsAccuracyThreshold : 25f, 10f, 50f);
            settings.activityNotifications = data.activityNotifications;
            settings.touchControls = Mathf.Clamp(data.touchControls, 0, 2);
            settings.joystickDeadZone = Mathf.Clamp(float.IsNaN(data.joystickDeadZone) ? 0.12f : data.joystickDeadZone, 0f, 0.5f);
            settings.joystickSensitivity = Mathf.Clamp(data.joystickSensitivity > 0f ? data.joystickSensitivity : 1f, 0.5f, 2f);
            settings.touchLookSensitivityX = Mathf.Clamp(data.touchLookSensitivityX > 0f ? data.touchLookSensitivityX : 1f, 0.2f, 3f);
            settings.touchLookSensitivityY = Mathf.Clamp(data.touchLookSensitivityY > 0f ? data.touchLookSensitivityY : 0.8f, 0.2f, 3f);
            settings.haptics = data.haptics;
            settings.joystickSize = Mathf.Clamp(data.joystickSize > 0f ? data.joystickSize : 1f, 0.7f, 1.4f);
            settings.buttonOpacity = Mathf.Clamp(data.buttonOpacity > 0f ? data.buttonOpacity : 0.85f, 0.3f, 1f);
            settings.targetFramerate = GameSettings.FrameRates.Contains(data.targetFramerate) ? data.targetFramerate : 60;

            // GPS totals: never negative / NaN (a damaged or edited file must not break progression).
            session.gpsTotalRealMeters = Valid(data.gpsTotalRealMeters);
            session.gpsTotalVirtualMeters = Valid(data.gpsTotalVirtualMeters);
            session.gpsRouteMeters = Valid(data.gpsRouteMeters);
            session.gpsRouteOffset = Valid(data.gpsRouteOffset);
            session.gpsTotalSeconds = (float)Valid(data.gpsTotalSeconds);
            session.gpsModeActive = data.gpsModeActive;
            session.gpsTotalSteps = Math.Max(0, data.gpsTotalSteps);
            session.xp = Math.Max(0, data.xp);
            session.activityMilestones = (data.activityMilestones ?? new MilestoneCount[0])
                .Where(m => m != null && !string.IsNullOrEmpty(m.id)).Select(m => new MilestoneCount { id = m.id, count = Math.Max(0, m.count) }).ToList();
            session.unlockedMissionIds = List(data.unlockedMissionIds);
            session.completedActivityMissionIds = List(data.completedActivityMissionIds);
            session.activityBaselines = (data.activityBaselines ?? new ActivityBaseline[0]).Where(b => b != null && !string.IsNullOrEmpty(b.missionId)).ToList();
            session.activityObjectivesDone = List(data.activityObjectivesDone);
            session.playerCheckpoints = (data.playerCheckpoints ?? new PlayerCheckpoint[0])
                .Where(c => c != null && !string.IsNullOrEmpty(c.id) && Math.Abs(c.latitude) <= 90 && Math.Abs(c.longitude) <= 180).ToList();
            session.completedCheckpointIds = List(data.completedCheckpointIds);
            session.explorationSalt = data.explorationSalt ?? string.Empty;
            session.exploredAreas = List(data.exploredAreas);
            session.unlockedDestinationIds = List(data.unlockedDestinationIds);
            session.currentDestinationId = data.currentDestinationId ?? string.Empty;
            session.pendingCollectibleIds = List(data.pendingCollectibleIds).Where(id => !session.inventoryItemIds.Contains(id)).Distinct().ToList();
            session.gpsSession = data.gpsSession ?? new ActivitySessionState();
            if (session.gpsSession.state == ActivitySessionState.Running)
            {
                // The game closed mid-walk: resume as paused, never count the gap.
                session.gpsSession.state = ActivitySessionState.Paused;
                session.gpsSession.interrupted = true;
            }

            session.SuspicionLevels.Clear();
            if (data.suspicionKeys != null && data.suspicionValues != null)
            {
                for (int i = 0; i < data.suspicionKeys.Length && i < data.suspicionValues.Length; i++)
                {
                    session.SuspicionLevels[data.suspicionKeys[i]] = data.suspicionValues[i];
                }
            }
        }
    }
}
