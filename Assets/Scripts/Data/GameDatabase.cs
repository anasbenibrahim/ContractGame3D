using UnityEngine;
using UnityEngine.Audio;

namespace Contract.Data
{
    /// <summary>
    /// Single source of truth for all game content.
    /// Lives at Resources/GameDatabase so that persistent services created at boot
    /// (before any content scene is loaded) can always find their data, regardless of
    /// which scene the game was started from.
    /// </summary>
    [CreateAssetMenu(menuName = "CONTRACT/Game Database", fileName = "GameDatabase")]
    public sealed class GameDatabase : ScriptableObject
    {
        public const string ResourcePath = "GameDatabase";

        [SerializeField] private CharacterData[] characters = new CharacterData[0];
        [SerializeField] private RegionData[] regions = new RegionData[0];
        [SerializeField] private MissionData[] missions = new MissionData[0];
        [SerializeField] private UpgradeData[] upgrades = new UpgradeData[0];
        [SerializeField] private DestinationData[] destinations = new DestinationData[0];
        [SerializeField] private AudioLibrary audioLibrary;
        [SerializeField] private AudioMixer audioMixer;

        public CharacterData[] Characters => characters;
        public RegionData[] Regions => regions;
        public MissionData[] Missions => missions;
        public UpgradeData[] Upgrades => upgrades;
        public DestinationData[] Destinations => destinations;
        public AudioLibrary AudioLibrary => audioLibrary;
        public AudioMixer AudioMixer => audioMixer;

        private static GameDatabase cached;

        /// <summary>Loads (once) and returns the database. Logs an error if it is missing.</summary>
        public static GameDatabase Load()
        {
            if (cached == null)
            {
                cached = Resources.Load<GameDatabase>(ResourcePath);
                if (cached == null)
                {
                    Debug.LogError($"[GameDatabase] Missing Resources/{ResourcePath}.asset. Run CONTRACT > Setup > Build Game Database.");
                }
            }

            return cached;
        }

#if UNITY_EDITOR
        /// <summary>Editor-only: assigns content when (re)building the database asset.</summary>
        public void EditorAssign(CharacterData[] c, RegionData[] r, MissionData[] m, UpgradeData[] u, AudioLibrary audio, AudioMixer mixer, DestinationData[] d)
        {
            destinations = d;
            characters = c;
            regions = r;
            missions = m;
            upgrades = u;
            audioLibrary = audio;
            audioMixer = mixer;
        }
#endif
    }
}
