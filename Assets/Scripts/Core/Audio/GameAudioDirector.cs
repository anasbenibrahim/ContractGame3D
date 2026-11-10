using System.Collections.Generic;
using Contract.Contracts;
using Contract.Core;
using Contract.Data;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Contract.Audio
{
    /// <summary>
    /// Decides what plays: music and ambience per scene (menus: title theme; Gangnam: lo-fi city
    /// groove over the night street), tension music while any guard is suspicious, and the sound
    /// of game events (objectives, contracts, pickups, photos, purchases, alerts, victory).
    /// While paused, 3D world sounds pause; music and UI keep playing.
    /// </summary>
    public sealed class GameAudioDirector : MonoBehaviour
    {
        public static GameAudioDirector Instance { get; private set; }

        private const float TensionThreshold = 0.25f;
        private const float CalmDelay = 4f;

        private readonly Dictionary<string, float> suspicion = new Dictionary<string, float>();
        private readonly HashSet<string> alerted = new HashSet<string>();
        private float calmTimer;
        private bool tense;
        private float suspicionCueCooldown;

        public bool Tense => tense;

        private static IAudioService Audio => GameManager.Instance?.GetService<IAudioService>();
        private static bool InCity => GameConstants.Scenes.InGameplay;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void CreateInstance()
        {
            if (Instance == null) new GameObject("[GameAudioDirector]").AddComponent<GameAudioDirector>();
        }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        private void Start() => OnSceneChanged(default, SceneManager.GetActiveScene());

        private void OnEnable()
        {
            SceneManager.activeSceneChanged += OnSceneChanged;
            GameEvents.SuspicionChanged += OnSuspicion;
            GameEvents.NpcAlerted += OnAlerted;
            GameEvents.ObjectiveCompleted += OnObjective;
            GameEvents.MissionResultReady += OnResult;
            GameEvents.MissionStarted += OnMissionStarted;
            GameEvents.ItemCollected += OnItem;
            GameEvents.InteractionPerformed += OnInteraction;
            GameEvents.UpgradePurchased += OnPurchase;
            GameEvents.VictoryReached += OnVictory;
            GameEvents.ContractBoardRequested += OnPage;
            GameEvents.MapOpened += OnMap;
            GameEvents.PauseChanged += OnPause;
            GameEvents.SaveCompleted += OnSaved;
        }

        private void OnDisable()
        {
            SceneManager.activeSceneChanged -= OnSceneChanged;
            GameEvents.SuspicionChanged -= OnSuspicion;
            GameEvents.NpcAlerted -= OnAlerted;
            GameEvents.ObjectiveCompleted -= OnObjective;
            GameEvents.MissionResultReady -= OnResult;
            GameEvents.MissionStarted -= OnMissionStarted;
            GameEvents.ItemCollected -= OnItem;
            GameEvents.InteractionPerformed -= OnInteraction;
            GameEvents.UpgradePurchased -= OnPurchase;
            GameEvents.VictoryReached -= OnVictory;
            GameEvents.ContractBoardRequested -= OnPage;
            GameEvents.MapOpened -= OnMap;
            GameEvents.PauseChanged -= OnPause;
            GameEvents.SaveCompleted -= OnSaved;
        }

        // ---- scene music -----------------------------------------------------------------------

        private void OnSceneChanged(Scene previous, Scene next)
        {
            suspicion.Clear();
            alerted.Clear();
            tense = false;
            AudioListener.pause = false;
            IAudioService audio = Audio;
            if (audio == null) return;

            if (GameConstants.Scenes.IsGameplay(next.name))
            {
                audio.CrossfadeMusic("music_city", 2f);
                audio.CrossfadeAmbience("amb_city_night", 2f);
                return;
            }

            switch (next.name)
            {
                case GameConstants.Scenes.CharacterSelection:
                    audio.CrossfadeMusic("music_menu", 2f);
                    audio.CrossfadeAmbience("amb_interior", 1.5f);
                    break;
                case GameConstants.Scenes.MainMenu:
                case GameConstants.Scenes.MissionSelection:
                case GameConstants.Scenes.WorldSelection:
                    audio.CrossfadeMusic("music_menu", 2f);
                    audio.CrossfadeAmbience("amb_city_night", 2f);
                    break;
            }
        }

        // ---- tension -----------------------------------------------------------------------------

        private void OnSuspicion(string npcId, float level)
        {
            float previous = suspicion.TryGetValue(npcId, out float p) ? p : 0f;
            suspicion[npcId] = level;
            if (level <= 0.001f) alerted.Remove(npcId);
            if (previous < 0.3f && level >= 0.3f && suspicionCueCooldown <= 0f)
            {
                Audio?.PlaySFX("suspicion");
                suspicionCueCooldown = 3f;
            }
        }

        private void OnAlerted(string npcId)
        {
            alerted.Add(npcId);
            Audio?.PlaySFX("alert");
        }

        private void Update()
        {
            suspicionCueCooldown -= Time.unscaledDeltaTime;
            if (!InCity) return;

            float highest = 0f;
            foreach (float level in suspicion.Values) highest = Mathf.Max(highest, level);
            bool danger = alerted.Count > 0 || highest >= TensionThreshold;

            if (danger)
            {
                calmTimer = CalmDelay;
                if (!tense)
                {
                    tense = true;
                    Audio?.CrossfadeMusic("music_tension", 0.8f);
                }
            }
            else if (tense && (calmTimer -= Time.unscaledDeltaTime) <= 0f)
            {
                tense = false;
                Audio?.CrossfadeMusic("music_city", 3f);
            }
        }

        // ---- event cues ----------------------------------------------------------------------------

        private void OnObjective(string objectiveId) => Audio?.PlaySFX("objective_complete");

        private void OnResult(MissionResult result) => Audio?.PlaySFX(result.success ? "contract_complete" : "contract_failed");

        private void OnMissionStarted(string missionId) => Audio?.PlaySFX("interact");

        private void OnItem(string itemId) => Audio?.PlaySFX("pickup");

        private void OnInteraction(string id) => Audio?.PlaySFX(id != null && id.StartsWith("photo:") ? "photo_shutter" : "hold_complete");

        private void OnPurchase(string upgradeId) => Audio?.PlaySFX("purchase");

        private void OnVictory() => Audio?.PlaySFX("victory");

        private void OnPage() => Audio?.PlaySFX("page");

        private void OnMap(string mapId) => Audio?.PlaySFX("page");

        private void OnSaved(string slot) => Audio?.PlaySFX("save", default, 1f, 0.6f);

        /// <summary>World sounds (footsteps, 3D cues) stop while paused; music and UI continue.</summary>
        private void OnPause(bool paused) => AudioListener.pause = paused;
    }
}
