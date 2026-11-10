namespace Contract.Core
{
    public static class GameConstants
    {
        public const string GameName = "CONTRACT: SEOUL SHADOWS";
        public const int VictoryMoney = 1_000_000;
        public const int SaveVersion = 8;
        public const float TargetFrameRate = 60f;

        // Third-person camera
        public const float DefaultCameraDistance = 4.6f;
        public const float MinCameraDistance = 2.2f;
        public const float MaxCameraDistance = 7.5f;

        public static class Scenes
        {
            public const string MainMenu = "MainMenu";
            public const string CharacterSelection = "CharacterSelection";
            public const string MissionSelection = "MissionSelection";
            public const string GameWorld = "GameWorld";      // Seoul · Gangnam
            public const string Myeongdong = "Myeongdong";    // Seoul · Myeongdong
            public const string WorldSelection = "WorldSelection";

            /// <summary>Scenes where the operative walks a district (HUD, GPS route, saving the position ...).</summary>
            public static readonly string[] Gameplay = { GameWorld, Myeongdong };

            public static bool IsGameplay(string sceneName) => System.Array.IndexOf(Gameplay, sceneName) >= 0;

            public static bool InGameplay => IsGameplay(UnityEngine.SceneManagement.SceneManager.GetActiveScene().name);

            /// <summary>The scene a district (region) is played in; Gangnam's scene when unknown.</summary>
            public static string ForRegion(string regionId)
            {
                Contract.Data.GameDatabase db = Contract.Data.GameDatabase.Load();
                Contract.Data.RegionData region = db == null ? null : System.Array.Find(db.Regions, r => r != null && r.regionId == regionId);
                return region != null && IsGameplay(region.sceneName) ? region.sceneName : GameWorld;
            }
        }

        public static class Layers
        {
            public const string Player = "Player";
            public const string Npc = "NPC";
            public const string Interactable = "Interactable";
            public const string MissionSubject = "MissionSubject";
            public const string Environment = "Environment";
            public const string UI = "UI";
            public const string Cover = "Cover";
        }

        public static class Tags
        {
            public const string Player = "Player";
            public const string Npc = "NPC";
            public const string MissionSubject = "MissionSubject";
            public const string Interactable = "Interactable";
            public const string ExtractionPoint = "ExtractionPoint";
            public const string Safehouse = "Safehouse";
            public const string CoverPoint = "CoverPoint";
            public const string MapIcon = "MapIcon";
        }

        public static class PrefabPaths
        {
            public const string Player = "Assets/Prefabs/Player/Player.prefab";
            public const string CivilianNPC = "Assets/Prefabs/NPCs/CivilianNPC.prefab";
            public const string MissionNPC = "Assets/Prefabs/NPCs/MissionNPC.prefab";
            public const string HUD = "Assets/Prefabs/UI/HUDCanvas.prefab";
            public const string Minimap = "Assets/Prefabs/UI/MinimapCanvas.prefab";
        }

        public static class InputActions
        {
            public const string Move = "Move";
            public const string Look = "Look";
            public const string Sprint = "Sprint";
            public const string Crouch = "Crouch";
            public const string Interact = "Interact";
            public const string Jump = "Jump";
            public const string OpenMap = "OpenMap";
            public const string Pause = "Pause";
            public const string Aim = "Aim";
            public const string Cover = "Cover";
            public const string Photograph = "Photograph";
        }

        public static class UI
        {
            public const string CanvasName = "ContractUI";
            public const string HUDName = "HUD";
            public const string MinimapName = "Minimap";
            public const string FullMapName = "FullMap";
            public const string PauseName = "PauseMenu";
            public const string SettingsName = "SettingsPanel";
            public const string ObjectiveName = "ObjectiveTracker";
        }

        public static class AudioMixer
        {
            public const string Master = "Master";
            public const string Music = "Music";
            public const string SFX = "SFX";
            public const string Dialogue = "Dialogue";
            public const string Ambient = "Ambience";
        }

        public static class Save
        {
            public const string DefaultSlot = "autosave";
            public const string SlotPrefix = "save_slot_";
            public const string FileExtension = ".json";
            public const int MaxSlots = 5;
        }
    }
}