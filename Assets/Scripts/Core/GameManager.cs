using System;
using UnityEngine;

namespace Contract.Core
{
    [DefaultExecutionOrder(-1000)]
    public sealed class GameManager : MonoBehaviour
    {
        public static GameManager Instance { get; private set; }

        public GameSession Session { get; private set; } = new GameSession();
        public SceneLoader SceneLoader { get; private set; }
        public ServiceLocator Services { get; private set; } = new ServiceLocator();
        public bool IsPaused { get; private set; }
        public bool IsMapOpen { get; private set; }

        private readonly System.Collections.Generic.HashSet<string> openMenus = new System.Collections.Generic.HashSet<string>();

        /// <summary>A gameplay overlay (contract board, result screen) is open: no movement / camera / interaction.</summary>
        public bool IsMenuOpen => openMenus.Count > 0;

        /// <summary>True whenever gameplay input must be ignored.</summary>
        public bool InputBlocked => IsPaused || IsMapOpen || IsMenuOpen;

        public void OpenMenu(string menuId)
        {
            if (string.IsNullOrEmpty(menuId) || !openMenus.Add(menuId)) return;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        public void CloseMenu(string menuId)
        {
            if (!openMenus.Remove(menuId) || InputBlocked) return;
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
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

            Application.targetFrameRate = (int)GameConstants.TargetFrameRate;
            QualitySettings.vSyncCount = 0;
            Time.timeScale = 1f;

            // Overlays belong to the scene that opened them: never let a stale one block input after a load.
            UnityEngine.SceneManagement.SceneManager.sceneLoaded += (scene, mode) =>
            {
                if (mode == UnityEngine.SceneManagement.LoadSceneMode.Single) openMenus.Clear();
            };

            SceneLoader = GetComponent<SceneLoader>();
            if (SceneLoader == null)
            {
                SceneLoader = GetComponentInParent<SceneLoader>();
            }

            if (SceneLoader == null)
            {
                SceneLoader = gameObject.AddComponent<SceneLoader>();
            }
        }

        private void Start()
        {
            RegisterCoreServices();
        }

        /// <summary>Career play time: counts while in the city and not paused.</summary>
        private void Update()
        {
            if (Session.hasStarted && !IsPaused && GameConstants.Scenes.InGameplay)
            {
                Session.playTimeSeconds += Time.unscaledDeltaTime;
            }
        }

        private void RegisterCoreServices()
        {
            Services.Register(Session);
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
            }
        }

        public T GetService<T>()
        {
            return Services.Get<T>();
        }

        public void RegisterService<T>(T service)
        {
            Services.Register(service);
        }

        public void SetSelectedCharacter(string characterId)
        {
            if (string.IsNullOrWhiteSpace(characterId))
            {
                return;
            }

            Session.selectedCharacterId = characterId;
            GameEvents.RaiseCharacterSelected(characterId);
        }

        public void StartNewGame(string characterId = null)
        {
            Session.Reset();
            Session.hasStarted = true;

            if (!string.IsNullOrWhiteSpace(characterId))
            {
                Session.selectedCharacterId = characterId;
            }

            GameEvents.RaiseSessionStarted();
            GameEvents.RaiseNewGameStarted();
            // New career: choose an operative, then a contract, then into the city.
            SceneLoader.LoadCharacterSelection();
        }

        /// <summary>
        /// Loads the saved career and resumes it: straight back into the city when an operative has
        /// been chosen, otherwise to character selection. Returns false if there was nothing to load.
        /// </summary>
        public bool ContinueGame()
        {
            Contract.Contracts.ISaveService save = GetService<Contract.Contracts.ISaveService>();
            if (save == null || !save.LoadSlot(GameConstants.Save.DefaultSlot)) return false;

            Session.hasStarted = true;
            GameEvents.RaiseSessionStarted();
            if (string.IsNullOrEmpty(Session.selectedCharacterId)) SceneLoader.LoadCharacterSelection();
            else SceneLoader.LoadDistrict(Session.currentRegionId); // back to the district the career was in
            return true;
        }

        public void ReturnToMainMenu()
        {
            SetPaused(false);
            CloseMap();
            openMenus.Clear();
            SceneLoader.LoadMainMenu();
        }

        public void SetPaused(bool paused)
        {
            if (IsPaused == paused)
            {
                return;
            }

            IsPaused = paused;
            Time.timeScale = paused ? 0f : 1f;
            GameEvents.RaisePauseChanged(paused);
        }

        public void OpenMap(string mapId = "main")
        {
            if (IsMapOpen)
            {
                return;
            }

            IsMapOpen = true;
            GameEvents.RaiseMapOpened(mapId);
        }

        public void CloseMap()
        {
            if (!IsMapOpen)
            {
                return;
            }

            IsMapOpen = false;
            GameEvents.RaiseMapClosed("main");
        }

        public void QuitGame()
        {
            SceneLoader.Quit();
        }
    }
}