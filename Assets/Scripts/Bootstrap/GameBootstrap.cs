using System;
using Contract.Audio;
using Contract.Character;
using Contract.Core;
using Contract.Economy;
using Contract.Map;
using Contract.Missions;
using Contract.NPCs;
using Contract.Progression;
using Contract.Regions;
using Contract.SaveSystem;
using Contract.UI;
using UnityEngine;

namespace Contract.Bootstrap
{
    /// <summary>
    /// Bootstraps all game services before the first scene loads.
    /// Each service creates itself via [RuntimeInitializeOnLoadMethod] and registers with GameManager.
    /// </summary>
    public static class GameBootstrap
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void CreateGameServices()
        {
            if (GameManager.Instance != null)
            {
                return;
            }

            GameObject managerObject = new GameObject("[GameManager]");
            managerObject.AddComponent<GameManager>();
            UnityEngine.Object.DontDestroyOnLoad(managerObject);
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void EnsureServices()
        {
            if (GameManager.Instance == null)
            {
                CreateGameServices();
            }

            // Services are created by their own [RuntimeInitializeOnLoadMethod] 
            // and register themselves via GameManager.Instance.RegisterService
            // This method just ensures GameManager exists
        }
    }
}