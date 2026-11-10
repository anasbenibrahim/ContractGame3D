using System.Collections;
using System.Linq;
using Contract.Core;
using Contract.NPCs;
using NUnit.Framework;
using Unity.AI.Navigation;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Contract.Tests
{
    /// <summary>
    /// Phase 11: regressions for problems first seen on the Galaxy A22 (SM-A226B) that the editor
    /// did not show: missing scripts in scenes and NPCs enabling before the NavMesh is registered.
    /// </summary>
    public sealed class DeviceFindingsTests
    {
        [UnityTest]
        public IEnumerator NoScene_HasComponentsWithMissingScripts(
            [Values("MainMenu", "CharacterSelection", "WorldSelection", "MissionSelection", "GameWorld", "Myeongdong")] string scene)
        {
            SceneManager.LoadScene(scene);
            yield return null;
            yield return null;
            string[] broken = SceneManager.GetActiveScene().GetRootGameObjects()
                .SelectMany(r => r.GetComponentsInChildren<Transform>(true))
                .Where(t => t.GetComponents<Component>().Any(c => c == null))
                .Select(t => t.name).ToArray();
            CollectionAssert.IsEmpty(broken, "objects with a missing script");
            NpcManager.Instance?.DespawnAllCivilians();
        }

        [UnityTest]
        public IEnumerator Npc_EnabledBeforeTheNavMesh_AttachesOnceItExists()
        {
            SceneManager.LoadScene(GameConstants.Scenes.MainMenu);
            yield return null;
            GameManager.Instance.Session.Reset();
            GameManager.Instance.Session.selectedCharacterId = "char_alex_morgan";
            SceneManager.LoadScene(GameConstants.Scenes.GameWorld);
            for (int i = 0; i < 6; i++) yield return null;
            NpcManager.Instance?.DespawnAllCivilians();

            NavMeshSurface surface = Object.FindAnyObjectByType<NavMeshSurface>();
            NpcController guard = Object.FindObjectsByType<NpcController>(FindObjectsSortMode.None).First(n => n.Type == NpcType.Guard);
            var agent = guard.GetComponent<NavMeshAgent>();
            surface.RemoveData();
            guard.gameObject.SetActive(false);
            LogAssert.ignoreFailingMessages = true; // Unity logs "Failed to create agent ..." here, as on the device
            guard.gameObject.SetActive(true);
            yield return null;
            Assert.IsFalse(agent.isOnNavMesh, "reproduced: the agent could not attach");
            surface.AddData();
            yield return new WaitForSeconds(1.5f);
            LogAssert.ignoreFailingMessages = false;
            Assert.IsTrue(agent.isOnNavMesh, "the guard attached itself once the NavMesh was registered");
        }
    }
}
