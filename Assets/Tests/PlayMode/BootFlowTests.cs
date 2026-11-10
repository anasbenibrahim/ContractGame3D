using System.Collections;
using System.Linq;
using System.Reflection;
using Contract.Character;
using Contract.Core;
using Contract.Data;
using Contract.Map;
using Contract.Missions;
using Contract.Progression;
using Contract.Regions;
using Contract.UI;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Contract.Tests
{
    /// <summary>
    /// Verifies the real boot order: the test runner enters Play mode in an empty scene, so the
    /// persistent services are created BEFORE any content scene loads — the same situation as a
    /// player launching the game from MainMenu. These tests fail if a service ends up empty or a
    /// scene-owned manager leaks across scenes.
    /// </summary>
    public sealed class BootFlowTests
    {
        private static IEnumerator Load(string sceneName)
        {
            SceneManager.LoadScene(sceneName, LoadSceneMode.Single);
            // One frame for Awake/OnEnable/Start, one more for anything deferred to the next Update.
            yield return null;
            yield return null;
        }

        [Test]
        public void GameDatabase_IsPresentAndPopulated()
        {
            GameDatabase database = GameDatabase.Load();
            Assert.IsNotNull(database, "Resources/GameDatabase.asset is missing.");
            Assert.AreEqual(3, database.Characters.Length, "characters");
            Assert.AreEqual(5, database.Regions.Length, "regions");
            Assert.AreEqual(13, database.Missions.Count(m => m.category == MissionCategory.Contract), "contracts");
            Assert.AreEqual(6, database.Missions.Count(m => m.category == MissionCategory.Activity), "GPS activity missions");
            Assert.Greater(database.Upgrades.Length, 0, "upgrades");
            Assert.IsNotNull(database.AudioLibrary, "audio library");
            Assert.IsNotNull(database.AudioMixer, "audio mixer");
            Assert.IsFalse(database.Missions.Any(m => m == null), "null mission entry");
        }

        [UnityTest]
        public IEnumerator MainMenu_PersistentServicesHaveContent()
        {
            yield return Load(GameConstants.Scenes.MainMenu);

            Assert.IsNotNull(GameManager.Instance, "GameManager");
            Assert.IsNotNull(MissionManager.Instance, "MissionManager");
            Assert.IsNotNull(MissionManager.Instance.GetMission("mission_downtown_01"), "MissionManager has no mission data");
            Assert.IsNotNull(RegionManager.Instance.GetRegion("region_downtown"), "RegionManager has no region data");
            Assert.AreEqual(3, CharacterSelectionManager.Instance.GetAvailableCharacters().Count(), "CharacterSelectionManager");
            Assert.Greater(UpgradeManager.Instance.GetAvailableUpgrades().Count(), 0, "UpgradeManager");
            Assert.IsNull(UIManager.Instance, "Gameplay UIManager must not exist in the main menu");
            Assert.IsNull(MapManager.Instance, "MapManager must not exist in the main menu");
        }

        [UnityTest]
        public IEnumerator CharacterSelection_ShowsAllCharacters()
        {
            yield return Load(GameConstants.Scenes.MainMenu);
            yield return Load(GameConstants.Scenes.CharacterSelection);

            CharacterSelectionController controller = Object.FindAnyObjectByType<CharacterSelectionController>();
            Assert.IsNotNull(controller, "CharacterSelectionController");
            RectTransform cards = (RectTransform)typeof(CharacterSelectionController)
                .GetField("cardContainer", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(controller);
            string[] labels = cards.GetComponentsInChildren<TMP_Text>().Select(t => t.text).ToArray();

            foreach (CharacterData character in CharacterSelectionManager.Instance.GetAvailableCharacters())
            {
                Assert.IsTrue(labels.Any(l => l.Contains(character.displayName)), $"Card for {character.displayName} not populated");
            }
        }

        [UnityTest]
        public IEnumerator MissionSelection_OpensTheContractBoard()
        {
            yield return Load(GameConstants.Scenes.MainMenu);
            yield return Load(GameConstants.Scenes.MissionSelection);

            ContractBoardView board = Object.FindAnyObjectByType<ContractBoardView>();
            Assert.IsNotNull(board, "ContractBoardView");
            Assert.IsTrue(board.IsOpen, "Board should open in scene mode");
            Assert.Greater(RegionManager.Instance.GetUnlockedRegions().Count(), 0, "No unlocked regions");
            Assert.Greater(board.MissionButtons.Count, 0, "No contracts listed");
        }

        [UnityTest]
        public IEnumerator GameWorld_SceneManagersAreSceneOwned_AndDoNotLeak()
        {
            yield return Load(GameConstants.Scenes.MainMenu);
            yield return Load(GameConstants.Scenes.GameWorld);

            Assert.IsNotNull(UIManager.Instance, "UIManager missing in GameWorld");
            Assert.AreEqual(GameConstants.Scenes.GameWorld, UIManager.Instance.gameObject.scene.name, "UIManager should live in GameWorld");
            Assert.IsNotNull(MapManager.Instance, "MapManager missing in GameWorld");
            Assert.AreEqual(GameConstants.Scenes.GameWorld, MapManager.Instance.gameObject.scene.name, "MapManager should live in GameWorld");

            yield return Load(GameConstants.Scenes.MainMenu);

            Assert.IsNull(UIManager.Instance, "UIManager leaked into MainMenu");
            Assert.IsNull(MapManager.Instance, "MapManager leaked into MainMenu");
        }
    }
}
