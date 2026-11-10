using System.Collections;
using System.Linq;
using Contract.Activity;
using Contract.Contracts;
using Contract.Core;
using Contract.Data;
using Contract.Interaction;
using Contract.Map;
using Contract.Missions;
using Contract.NPCs;
using Contract.Regions;
using Contract.SaveSystem;
using Contract.UI;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Contract.Tests
{
    /// <summary>
    /// Phase 7: South Korea destinations, the World Selection screen, travel with the loading
    /// screen and the second playable district (Seoul · Myeongdong) with both of its contracts.
    /// </summary>
    public sealed class DestinationTests
    {
        private static GameSession Session => GameManager.Instance.Session;
        private static DestinationManager Destinations => DestinationManager.Instance;
        private static IRegionService Regions => GameManager.Instance.GetService<IRegionService>();

        [TearDown]
        public void TearDown()
        {
            GameManager.Instance?.SetPaused(false);
            MissionManager.Instance?.AbandonMission();
            NpcManager.Instance?.DespawnAllCivilians();
            GameManager.Instance?.Session.Reset();
            GameManager.Instance?.GetService<ISaveService>()?.DeleteSlot(GameConstants.Save.DefaultSlot);
        }

        private static IEnumerator Load(string scene)
        {
            SceneManager.LoadScene(scene, LoadSceneMode.Single);
            for (int i = 0; i < 4; i++) yield return null;
        }

        private static IEnumerator WaitForScene(string scene, float timeout = 20f)
        {
            float end = Time.realtimeSinceStartup + timeout;
            while (SceneManager.GetActiveScene().name != scene || GameManager.Instance.SceneLoader.IsLoading)
            {
                if (Time.realtimeSinceStartup > end) Assert.Fail($"Scene '{scene}' was not reached (active: {SceneManager.GetActiveScene().name})");
                yield return null;
            }

            yield return null;
            yield return null;
        }

        private static IEnumerator Boot()
        {
            yield return Load(GameConstants.Scenes.MainMenu);
            Session.Reset();
            Session.hasStarted = true;
            Session.selectedCharacterId = "char_alex_morgan";
        }

        // ---- data ------------------------------------------------------------------------------

        [UnityTest]
        public IEnumerator SixDestinations_InProgressionOrder_WithAccurateHangul()
        {
            yield return Boot();
            DestinationData[] all = Destinations.All.ToArray();
            CollectionAssert.AreEqual(new[] { "dest_seoul", "dest_daegu", "dest_incheon", "dest_gyeongju", "dest_busan", "dest_jeju" },
                all.Select(d => d.destinationId).ToArray());
            CollectionAssert.AreEqual(new[] { "서울", "대구", "인천", "경주", "부산", "제주" }, all.Select(d => d.koreanName).ToArray());
            foreach (DestinationData d in all)
            {
                Assert.IsFalse(string.IsNullOrWhiteSpace(d.description), d.destinationId + " description");
                Assert.IsFalse(string.IsNullOrWhiteSpace(d.displayName), d.destinationId + " name");
            }

            DestinationData seoul = Destinations.Get("dest_seoul");
            CollectionAssert.IsSubsetOf(new[] { "region_downtown", "region_industrial", "region_residential" }, seoul.districts.Select(r => r.regionId).ToArray());
            CollectionAssert.IsSubsetOf(new[] { "region_luxury", "region_harbor" }, Destinations.Get("dest_busan").districts.Select(r => r.regionId).ToArray());
        }

        [UnityTest]
        public IEnumerator OnlyBuiltDistricts_ArePlayable_AndUnbuiltDestinationsCannotBeUnlocked()
        {
            yield return Boot();
            Assert.IsTrue(DestinationManager.IsDistrictBuilt(Regions.GetRegion("region_downtown")), "Gangnam");
            Assert.IsTrue(DestinationManager.IsDistrictBuilt(Regions.GetRegion("region_industrial")), "Myeongdong");
            Assert.IsFalse(DestinationManager.IsDistrictBuilt(Regions.GetRegion("region_residential")), "Bukchon has no scene yet");
            Assert.IsFalse(DestinationManager.IsDistrictBuilt(Regions.GetRegion("region_luxury")), "Haeundae has no scene yet");

            DestinationData seoul = Destinations.Get("dest_seoul");
            Assert.IsTrue(Destinations.IsBuilt(seoul) && Destinations.IsUnlocked(seoul), "Seoul open from the start");
            Assert.IsNotNull(seoul.preview, "Seoul has a preview rendered from a built district");
            Assert.AreSame(seoul, Destinations.Current);

            GameManager.Instance.GetService<IEconomyService>().AddMoney(1000000, "test grant");
            foreach (DestinationData d in Destinations.All.Where(d => d != seoul))
            {
                Assert.IsFalse(Destinations.IsBuilt(d), d.destinationId + " has no built district");
                Assert.IsNull(d.preview, d.destinationId + " must not show a preview of content that does not exist");
                Assert.IsFalse(Destinations.CanUnlock(d, out string why), d.destinationId);
                StringAssert.Contains("In development", why);
                Assert.IsFalse(Destinations.Unlock(d));
                Assert.IsFalse(Destinations.Select(d), "cannot travel to " + d.destinationId);
            }

            Assert.AreEqual(1000000, Session.money, "nothing was charged");
        }

        [UnityTest]
        public IEnumerator BuyingADistrict_IsPaidThroughTheLedger_AndSaved()
        {
            yield return Boot();
            RegionData myeongdong = Regions.GetRegion("region_industrial");
            Assert.IsFalse(Regions.IsRegionUnlocked("region_industrial"));
            Assert.IsFalse(Destinations.CanUnlockDistrict(myeongdong, out string why));
            StringAssert.Contains("Needs", why);

            GameManager.Instance.GetService<IEconomyService>().AddMoney(myeongdong.unlockCost, "test grant");
            Assert.IsTrue(Destinations.UnlockDistrict(myeongdong));
            Assert.AreEqual(0, Session.money);
            Assert.IsTrue(Regions.IsRegionUnlocked("region_industrial"));
            Assert.IsFalse(Destinations.UnlockDistrict(myeongdong), "only once");

            Session.currentDestinationId = "dest_seoul";
            SaveManager.Instance.SaveSlot(GameConstants.Save.DefaultSlot);
            Session.Reset();
            Assert.IsTrue(SaveManager.Instance.LoadSlot(GameConstants.Save.DefaultSlot));
            Assert.IsTrue(Regions.IsRegionUnlocked("region_industrial"));
            Assert.AreEqual("dest_seoul", Session.currentDestinationId);
        }

        // ---- World Selection screen -------------------------------------------------------------

        [UnityTest]
        public IEnumerator WorldSelection_ShowsEveryDestination_AndOnlyLetsYouEnterBuiltOnes()
        {
            yield return Boot();
            yield return Load(GameConstants.Scenes.WorldSelection);
            WorldSelectionView view = Object.FindAnyObjectByType<WorldSelectionView>();
            Assert.AreEqual(6, view.CardCount);
            Assert.AreEqual("dest_seoul", view.SelectedId);
            Assert.IsTrue(view.EnterButton.gameObject.activeSelf && view.EnterButton.interactable);
            Assert.IsFalse(view.UnlockButton.gameObject.activeSelf, "Seoul is already open");

            // Myeongdong is built but still locked: offered as a district purchase.
            Assert.IsTrue(view.DistrictButton.gameObject.activeSelf);
            Assert.IsFalse(view.DistrictButton.interactable, "no money yet");
            GameManager.Instance.GetService<IEconomyService>().AddMoney(25000, "test grant");
            view.Select("dest_seoul");
            Assert.IsTrue(view.DistrictButton.interactable);
            view.DistrictButton.onClick.Invoke();
            Assert.IsTrue(Regions.IsRegionUnlocked("region_industrial"));
            Assert.IsFalse(view.DistrictButton.gameObject.activeSelf, "no more built districts to buy in Seoul");

            view.Select("dest_busan");
            StringAssert.Contains("개발 중", view.StatusText);
            Assert.IsFalse(view.EnterButton.gameObject.activeSelf, "an unbuilt destination cannot be entered");
            Assert.IsFalse(view.UnlockButton.interactable, "nor unlocked");

            view.Select("dest_seoul");
            view.EnterButton.onClick.Invoke();
            yield return WaitForScene(GameConstants.Scenes.MissionSelection);
            Assert.AreEqual("dest_seoul", Session.currentDestinationId);
            ContractBoardView board = Object.FindAnyObjectByType<ContractBoardView>();
            Assert.IsTrue(board.IsOpen);
        }

        // ---- Myeongdong -------------------------------------------------------------------------

        [UnityTest]
        public IEnumerator TravelToMyeongdong_ShowsTheLoadingScreen_AndArrivesWithTheContract()
        {
            yield return Boot();
            Regions.UnlockRegion("region_industrial");
            MissionManager.Instance.StartMission("mission_industrial_01");
            Assert.AreEqual("mission_industrial_01", MissionManager.Instance.ActiveMission?.missionId, "Myeongdong contracts can start once it is unlocked");

            Destinations.TravelTo("region_industrial");
            bool sawLoadingScreen = false;
            string title = null;
            float end = Time.realtimeSinceStartup + 25f;
            while (SceneManager.GetActiveScene().name != GameConstants.Scenes.Myeongdong || GameManager.Instance.SceneLoader.IsLoading)
            {
                if (GameManager.Instance.SceneLoader.LoadingScreenVisible)
                {
                    sawLoadingScreen = true;
                    title = GameManager.Instance.SceneLoader.LoadingTitle;
                }

                if (Time.realtimeSinceStartup > end) Assert.Fail("Myeongdong not reached");
                yield return null;
            }

            Assert.IsTrue(sawLoadingScreen, "travel shows the loading screen");
            StringAssert.Contains("Myeongdong", title);
            yield return new WaitForSeconds(0.5f);

            Assert.AreEqual("mission_industrial_01", MissionManager.Instance.ActiveMission?.missionId, "contract carried into Myeongdong");
            Assert.IsFalse(GameManager.Instance.InputBlocked);
            GameObject player = GameObject.FindGameObjectWithTag("Player");
            Assert.IsNotNull(player);
            Assert.IsTrue(Physics.Raycast(player.transform.position + Vector3.up, Vector3.down, 3f), "player stands on ground");
            Assert.IsTrue(NavMesh.SamplePosition(player.transform.position, out _, 2f, NavMesh.AllAreas), "Myeongdong has a navmesh");
            Assert.AreEqual("명동", Object.FindAnyObjectByType<MapManager>().RegionKorean);
            Assert.IsNotNull(GpsRoute.Current, "GPS walking route present");
            Assert.Greater(GpsRoute.Current.Length, 200f);
            Assert.IsNull(GameObject.Find("District_Gangnam"), "no Gangnam geometry left in Myeongdong");
            Assert.IsNotNull(Object.FindAnyObjectByType<ContractBoardInteractable>(), "safehouse contract terminal");
        }

        [UnityTest]
        public IEnumerator Myeongdong_HasEveryTargetItsContractsNeed()
        {
            yield return Boot();
            yield return Load(GameConstants.Scenes.Myeongdong);

            var locations = Object.FindObjectsByType<LocationTrigger>(FindObjectsInactive.Include).Select(l => l.LocationId)
                .Concat(Object.FindObjectsByType<ExtractionInteractable>(FindObjectsInactive.Include).Select(e => e.ExtractionId)).ToList();
            var targets = Object.FindObjectsByType<MissionSubject>(FindObjectsInactive.Include).Select(s => s.TargetId)
                .Concat(Object.FindObjectsByType<PickupInteractable>(FindObjectsInactive.Include).Select(p => p.ItemId))
                .Concat(Object.FindObjectsByType<TerminalInteractable>(FindObjectsInactive.Include).Select(t => t.TargetId)).ToList();
            var subjects = Object.FindObjectsByType<MissionSubject>(FindObjectsInactive.Include).Select(s => s.SubjectId).ToList();

            foreach (string id in Regions.GetRegion("region_industrial").missionIds)
            {
                MissionData mission = MissionManager.Instance.GetMission(id);
                Assert.IsNotNull(mission, id);
                CollectionAssert.Contains(subjects, mission.subjectId, $"{id}: dossier subject in the district");
                foreach (MissionObjective o in mission.objectives)
                {
                    if (!string.IsNullOrEmpty(o.targetLocationId)) CollectionAssert.Contains(locations, o.targetLocationId, $"{id}: {o.description}");
                    if (!string.IsNullOrEmpty(o.targetId)) CollectionAssert.Contains(targets, o.targetId, $"{id}: {o.description}");
                }
            }

            Assert.IsTrue(Object.FindObjectsByType<NpcController>(FindObjectsInactive.Include).Any(n => n.Type == NpcType.Guard), "stockroom guards");
            NpcManager.Instance?.DespawnAllCivilians();
        }
    }
}
