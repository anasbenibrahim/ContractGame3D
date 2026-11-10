using System.IO;
using System.Linq;
using Contract.Data;
using UnityEditor;
using UnityEngine;
using UnityEngine.Audio;

namespace Contract.Editor
{
    /// <summary>
    /// Builds Assets/Resources/GameDatabase.asset from every content ScriptableObject in the project.
    /// Run from the menu, or headless: -executeMethod Contract.Editor.GameDatabaseBuilder.Build
    /// </summary>
    public static class GameDatabaseBuilder
    {
        private const string AssetPath = "Assets/Resources/GameDatabase.asset";

        [MenuItem("CONTRACT/Setup/Build Game Database")]
        public static void Build()
        {
            if (!AssetDatabase.IsValidFolder("Assets/Resources"))
            {
                AssetDatabase.CreateFolder("Assets", "Resources");
            }

            GameDatabase database = AssetDatabase.LoadAssetAtPath<GameDatabase>(AssetPath);
            if (database == null)
            {
                database = ScriptableObject.CreateInstance<GameDatabase>();
                AssetDatabase.CreateAsset(database, AssetPath);
            }

            database.EditorAssign(
                FindAll<CharacterData>(),
                FindAll<RegionData>(),
                FindAll<MissionData>(),
                FindAll<UpgradeData>(),
                FindAll<AudioLibrary>().FirstOrDefault(),
                AssetDatabase.LoadAssetAtPath<AudioMixer>("Assets/Audio/ContractMixer.mixer"),
                FindAll<DestinationData>());

            EditorUtility.SetDirty(database);
            AssetDatabase.SaveAssets();

            Debug.Log($"[GameDatabaseBuilder] Built {AssetPath}: " +
                      $"{database.Characters.Length} characters, {database.Regions.Length} regions, " +
                      $"{database.Missions.Length} missions, {database.Upgrades.Length} upgrades, {database.Destinations.Length} destinations, " +
                      $"audio library: {(database.AudioLibrary != null ? database.AudioLibrary.name : "none")}, " +
                      $"mixer: {(database.AudioMixer != null ? database.AudioMixer.name : "none")}.");
        }

        private static T[] FindAll<T>() where T : ScriptableObject
        {
            return AssetDatabase.FindAssets($"t:{typeof(T).Name}")
                .Select(AssetDatabase.GUIDToAssetPath)
                .OrderBy(Path.GetFileName)
                .Select(AssetDatabase.LoadAssetAtPath<T>)
                .Where(asset => asset != null)
                .ToArray();
        }
    }
}
