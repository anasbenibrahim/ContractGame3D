using System.Collections;
using System.IO;
using System.Linq;
using Contract.Contracts;
using Contract.Core;
using Contract.Data;
using Contract.Interaction;
using Contract.Missions;
using Contract.NPCs;
using Contract.Player;
using Contract.Progression;
using Contract.SaveSystem;
using Contract.UI;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Contract.Tests
{
    /// <summary>
    /// Stage 8 verification: the upgrade catalogue and every effect in game, multi-level
    /// purchases, outfits, the terminal's upgrade and finance pages, saving / loading (all career
    /// fields, position, backup recovery, old saves, no accidental overwrite), play time and victory.
    /// </summary>
    public sealed class ProgressionTests
    {
        private Gamepad gamepad;
        private InputSettings.BackgroundBehavior savedBackground;

        private static GameSession Session => GameManager.Instance.Session;
        private static ISaveService Save => GameManager.Instance.GetService<ISaveService>();
        private static UpgradeManager Upgrades => UpgradeManager.Instance;
        private static string Slot => GameConstants.Save.DefaultSlot;

        [SetUp]
        public void Setup()
        {
            savedBackground = InputSystem.settings.backgroundBehavior;
            InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            gamepad = InputSystem.AddDevice<Gamepad>("ProgressionTestGamepad");
            GameManager.Instance.SetPaused(false);
            Session.Reset();
            Save.DeleteSlot(Slot);
        }

        [TearDown]
        public void TearDown()
        {
            if (gamepad != null) InputSystem.RemoveDevice(gamepad);
            InputSystem.settings.backgroundBehavior = savedBackground;
            GameManager.Instance.SetPaused(false);
            Session.Reset();
            Save.DeleteSlot(Slot);
        }

        private static IEnumerator LoadWorld()
        {
            Session.selectedCharacterId = "char_alex_morgan";
            SceneManager.LoadScene(GameConstants.Scenes.GameWorld, LoadSceneMode.Single);
            for (int i = 0; i < 6; i++) yield return null;
            NpcManager.Instance.DespawnAllCivilians();
        }

        private static void Grant(int amount) => GameManager.Instance.GetService<IEconomyService>().AddMoney(amount, "test grant");

        private static void BuyAll(string id)
        {
            while (Upgrades.CanPurchase(id, out _)) Assert.IsTrue(Upgrades.PurchaseUpgrade(id));
        }

        private static IEnumerator WaitForScene(string scene, float timeout = 8f)
        {
            float end = Time.realtimeSinceStartup + timeout;
            while (SceneManager.GetActiveScene().name != scene || GameManager.Instance.SceneLoader.IsLoading)
            {
                if (Time.realtimeSinceStartup > end) Assert.Fail($"Scene '{scene}' was not reached");
                yield return null;
            }

            for (int i = 0; i < 3; i++) yield return null;
        }

        private static T Field<T>(object target, string name) =>
            (T)target.GetType().GetField(name, System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).GetValue(target);

        // ---- catalogue -----------------------------------------------------------------------

        [Test]
        public void Catalogue_EveryUpgradeHasARealEffectAndHonestText()
        {
            UpgradeData[] all = GameDatabase.Load().Upgrades;
            Assert.AreEqual(13, all.Length);
            CollectionAssert.AllItemsAreUnique(all.Select(u => u.upgradeId));
            foreach (UpgradeData u in all)
            {
                Assert.AreNotEqual(UpgradeEffect.None, u.effect, u.upgradeId);
                Assert.IsFalse(string.IsNullOrWhiteSpace(u.koreanName), u.upgradeId + " Korean name");
                Assert.IsFalse(string.IsNullOrWhiteSpace(u.description), u.upgradeId + " description");
                Assert.Greater(u.cost, 0, u.upgradeId);
                Assert.GreaterOrEqual(u.MaxLevel, 1, u.upgradeId);
                if (u.IsOutfit) Assert.AreEqual(UpgradeCategory.Cosmetic, u.category, u.upgradeId);
                else
                {
                    Assert.Greater(u.valuePerLevel, 0f, u.upgradeId);
                    Assert.IsNotEmpty(UpgradeShopView.Effect(u, 1), u.upgradeId + " has no effect text");
                }
            }

            // Every gameplay effect is offered by exactly one upgrade.
            var effects = all.Where(u => !u.IsOutfit).Select(u => u.effect).ToArray();
            CollectionAssert.AllItemsAreUnique(effects);
            Assert.AreEqual(System.Enum.GetValues(typeof(UpgradeEffect)).Length - 2, effects.Length, "an effect has no upgrade (None and Outfit excluded)");
        }

        [Test]
        public void MultiLevelPurchases_PersistInTheSession_AndPricesRise()
        {
            Grant(200000);
            const string id = "upgrade_move_speed_1";
            int[] expected = { 8000, 12000, 18000 };
            foreach (int price in expected)
            {
                int before = Session.money;
                Assert.AreEqual(price, Upgrades.GetNextCost(id));
                Assert.IsTrue(Upgrades.PurchaseUpgrade(id));
                Assert.AreEqual(before - price, Session.money);
            }

            Assert.AreEqual(3, Upgrades.GetUpgradeLevel(id));
            Assert.AreEqual(3, Session.purchasedUpgradeIds.Count(x => x == id), "one session entry per level");
            Assert.IsFalse(Upgrades.PurchaseUpgrade(id), "bought beyond max level");
            Assert.IsFalse(Upgrades.CanPurchase(id, out string reason));
            StringAssert.Contains("Max level", reason);
            Assert.AreEqual(0.18f, PlayerUpgrades.Bonus(UpgradeEffect.MoveSpeed), 0.0001f);
            Assert.AreEqual(3, Session.ledger.Count(e => e.source.Contains("Light Footwork")));
        }

        [Test]
        public void UpgradeBonuses_FollowTheSession_AfterANewCareer()
        {
            Grant(500000);
            BuyAll("upgrade_move_speed_1");
            Assert.AreEqual(0.18f, PlayerUpgrades.Bonus(UpgradeEffect.MoveSpeed), 0.0001f);
            Session.Reset(); // new career: same list object, same number of levels bought below
            Grant(500000);
            BuyAll("upgrade_sprint_speed_1");
            Assert.AreEqual(0f, PlayerUpgrades.Bonus(UpgradeEffect.MoveSpeed), 0.0001f, "stale bonus from the previous career");
            Assert.AreEqual(0.24f, PlayerUpgrades.Bonus(UpgradeEffect.SprintSpeed), 0.0001f);
        }

        [Test]
        public void Purchases_AreRefusedWithoutMoney()
        {
            Grant(5000);
            Assert.IsFalse(Upgrades.CanPurchase("upgrade_move_speed_1", out string reason));
            StringAssert.Contains("Not enough money", reason);
            Assert.IsFalse(Upgrades.PurchaseUpgrade("upgrade_move_speed_1"));
            Assert.AreEqual(5000, Session.money);
            Assert.AreEqual(0, Upgrades.GetUpgradeLevel("upgrade_move_speed_1"));
        }

        // ---- effects in game -------------------------------------------------------------------

        [UnityTest]
        public IEnumerator MovementAndIntelligenceUpgrades_ChangeThePlayer()
        {
            yield return LoadWorld();
            GameObject player = GameObject.FindGameObjectWithTag("Player");
            ThirdPersonController controller = player.GetComponent<ThirdPersonController>();
            InteractionController interaction = player.GetComponent<InteractionController>();
            float walk = controller.WalkSpeed, sprint = controller.SprintSpeed, crouch = controller.CrouchSpeed, jump = controller.JumpHeight;
            float range = interaction.InteractionRange, hold = interaction.HoldSpeedMultiplier;

            Grant(900000);
            foreach (string id in new[] { "upgrade_move_speed_1", "upgrade_sprint_speed_1", "upgrade_crouch_speed_1", "upgrade_jump_height_1",
                         "upgrade_hack_speed_1", "upgrade_interaction_range_1" })
            {
                BuyAll(id);
            }

            Assert.AreEqual(walk * 1.18f, controller.WalkSpeed, 0.001f, "Light Footwork x3");
            Assert.AreEqual(sprint * 1.24f, controller.SprintSpeed, 0.001f, "Enhanced Sprint x3");
            Assert.AreEqual(crouch * 1.18f * 1.5f, controller.CrouchSpeed, 0.001f, "Silent Movement x2 (+ walk bonus)");
            Assert.AreEqual(jump * 1.3f, controller.JumpHeight, 0.001f, "Vertical Leap x2");
            Assert.AreEqual(range * 1.45f, interaction.InteractionRange, 0.001f, "Extended Reach x3");
            Assert.AreEqual(hold * 1.45f, interaction.HoldSpeedMultiplier, 0.001f, "Quick Hack x3");
        }

        [UnityTest]
        public IEnumerator MoveSpeedUpgrade_MovesThePlayerFurther()
        {
            yield return LoadWorld();
            Transform player = GameObject.FindGameObjectWithTag("Player").transform;
            Vector3 start = player.position;

            IEnumerator Walk(float seconds)
            {
                player.GetComponent<CharacterController>().enabled = false;
                player.position = start;
                player.GetComponent<CharacterController>().enabled = true;
                Physics.SyncTransforms();
                yield return null;
                InputSystem.QueueStateEvent(gamepad, new GamepadState { leftStick = new Vector2(0f, 1f) });
                yield return new WaitForSeconds(seconds);
                InputSystem.QueueStateEvent(gamepad, new GamepadState());
                yield return new WaitForSeconds(0.4f);
            }

            yield return Walk(0.6f);
            float baseline = Vector3.Distance(Flat(start), Flat(player.position));
            Grant(100000);
            BuyAll("upgrade_move_speed_1");
            yield return Walk(0.6f);
            float upgraded = Vector3.Distance(Flat(start), Flat(player.position));
            Assert.Greater(baseline, 1f, "player did not walk");
            Assert.Greater(upgraded, baseline * 1.08f, $"upgraded walk {upgraded:F2} m vs {baseline:F2} m");
        }

        private static Vector3 Flat(Vector3 v) => new Vector3(v.x, 0f, v.z);

        [UnityTest]
        public IEnumerator StealthUpgrades_SlowSuspicion_AndSpeedUpCooldown()
        {
            yield return LoadWorld();
            Transform player = GameObject.FindGameObjectWithTag("Player").transform;
            NpcController guard = GameObject.Find("Gangnam_Population/Security/guard_mirae_02").GetComponent<NpcController>();
            Transform post = GameObject.Find("Gangnam_Population/PointsOfInterest/POI_Post_MiraeOffice").transform;
            guard.Configure("guard_mirae_02", NpcType.Guard, NpcArchetype.Security, null, null, "mirae", new[] { post });
            for (float t = 0f; t < 12f && guard.CurrentState != NpcState.Dwelling; t += Time.deltaTime) yield return null;
            Assert.AreEqual(NpcState.Dwelling, guard.CurrentState, "Guard never reached its post");
            yield return new WaitForSeconds(0.5f);
            Vector3 inView = guard.transform.position + post.forward * 4f + Vector3.up * 0.05f;
            Vector3 outside = player.position;

            void Place(Vector3 at)
            {
                CharacterController cc = player.GetComponent<CharacterController>();
                cc.enabled = false;
                Vector3 look = guard.transform.position - at;
                look.y = 0f;
                player.SetPositionAndRotation(at, Quaternion.LookRotation(look));
                cc.enabled = true;
                Physics.SyncTransforms();
            }

            IEnumerator Measure(System.Action<float, float> result)
            {
                guard.SetSuspicion(0f);
                Place(inView);
                yield return new WaitForSeconds(0.4f);
                float gained = guard.NormalizedSuspicion;
                Place(outside);
                guard.SetSuspicion(0.5f);
                yield return null;
                float start = guard.NormalizedSuspicion;
                yield return new WaitForSeconds(0.25f);
                result(gained, start - guard.NormalizedSuspicion);
                guard.SetSuspicion(0f);
                yield return new WaitForSeconds(0.2f);
            }

            float gainA = 0f, decayA = 0f, gainB = 0f, decayB = 0f;
            yield return Measure((g, d) => { gainA = g; decayA = d; });
            Grant(900000);
            BuyAll("upgrade_silent_takedown_1"); // Quiet Steps x3: -30% suspicion gain
            BuyAll("upgrade_cool_head_1");       // Cool Head x2: +60% cooldown
            yield return Measure((g, d) => { gainB = g; decayB = d; });

            Assert.Greater(gainA, 0.01f, "guard did not notice the trespasser");
            Assert.Greater(decayA, 0.001f, "suspicion did not cool down");
            Assert.AreEqual(0.7f, gainB / gainA, 0.12f, $"Quiet Steps: gain {gainA:F3} -> {gainB:F3}");
            Assert.AreEqual(1.6f, decayB / decayA, 0.25f, $"Cool Head: cooldown {decayA:F3} -> {decayB:F3}");
            Assert.AreEqual(1f, PlayerUpgrades.Reduction(UpgradeEffect.CrouchStealth), 0.001f, "Shadow Cloak not owned yet");
            BuyAll("upgrade_shadow_cloak_1");
            Assert.AreEqual(0.65f, PlayerUpgrades.Reduction(UpgradeEffect.CrouchStealth), 0.001f, "Shadow Cloak: crouched gain x0.65");
        }

        [Test]
        public void Negotiator_IsPaidOnTopOfTheContractReward()
        {
            MissionData mission = ScriptableObject.CreateInstance<MissionData>();
            mission.missionId = "test";
            mission.rewardMoney = 20000;
            mission.timeLimitMinutes = 0;
            MissionResult result = RewardCalculator.Calculate(mission, true, 0f, 0, 0, 0.15f);
            Assert.AreEqual(3000, result.negotiatorBonus);
            Assert.AreEqual(23000, result.totalReward);
            Object.DestroyImmediate(mission);
        }

        [Test]
        public void Negotiator_IsAppliedByTheMissionManager()
        {
            Grant(200000);
            BuyAll("upgrade_mission_reward_1"); // +15%
            int before = Session.money;
            MissionManager.Instance.StartMission("mission_downtown_01");
            MissionManager.Instance.CompleteMission("mission_downtown_01");
            MissionResult result = MissionManager.Instance.LastResult;
            Assert.AreEqual(2200, result.negotiatorBonus, "15% of $15,000 rounded to $100");
            Assert.AreEqual(before + result.totalReward, Session.money);
        }

        // ---- outfits -------------------------------------------------------------------------

        [UnityTest]
        public IEnumerator Outfits_AreUnlockedEquippedAndWorn()
        {
            yield return LoadWorld();
            PlayerCharacterApplier applier = GameObject.FindGameObjectWithTag("Player").GetComponent<PlayerCharacterApplier>();
            UpgradeData hoodie = Upgrades.GetUpgrade("upgrade_outfit_casual_1");
            GameObject before = applier.CurrentVisual;
            Assert.IsFalse(Wears(applier, hoodie.outfitTopColor), "already wearing the hoodie colour");

            Assert.IsFalse(Upgrades.EquipOutfit(hoodie.upgradeId), "equipped an outfit that is not owned");
            Grant(10000);
            Assert.IsTrue(Upgrades.PurchaseUpgrade(hoodie.upgradeId));
            yield return null;
            Assert.AreEqual(hoodie.upgradeId, Session.equippedOutfitId, "new outfit is worn straight away");
            Assert.AreNotSame(before, applier.CurrentVisual, "visual was not rebuilt");
            Assert.IsTrue(Wears(applier, hoodie.outfitTopColor), "player is not wearing the hoodie");
            Assert.IsFalse(Upgrades.PurchaseUpgrade(hoodie.upgradeId), "outfits are bought once");

            Assert.IsTrue(Upgrades.EquipOutfit(""));
            yield return null;
            Assert.IsFalse(Wears(applier, hoodie.outfitTopColor), "taking the outfit off did not restore the clothes");
        }

        private static bool Wears(PlayerCharacterApplier applier, Color color) =>
            applier.CurrentVisual.GetComponentsInChildren<Renderer>().SelectMany(r => r.sharedMaterials)
                .Any(m => m != null && m.HasProperty("_BaseColor") && Close(m.GetColor("_BaseColor"), color));

        private static bool Close(Color a, Color b) => Mathf.Abs(a.r - b.r) + Mathf.Abs(a.g - b.g) + Mathf.Abs(a.b - b.b) < 0.02f;

        // ---- terminal pages --------------------------------------------------------------------

        [UnityTest]
        public IEnumerator Terminal_UpgradesAndFinancePages_Work()
        {
            yield return LoadWorld();
            ContractBoardView board = Object.FindAnyObjectByType<ContractBoardView>();
            GameEvents.RaiseContractBoardRequested();
            yield return null;
            Assert.IsTrue(board.IsOpen);
            Assert.AreEqual(ContractBoardView.TerminalPage.Contracts, board.Page);

            Grant(60000);
            Field<UnityEngine.UI.Button>(board, "upgradesTab").onClick.Invoke();
            yield return null;
            UpgradeShopView shop = Object.FindAnyObjectByType<UpgradeShopView>();
            Assert.IsNotNull(shop, "upgrades page not shown");
            Assert.AreEqual(4, shop.ListedIds.Count, "four movement upgrades");
            shop.Select("upgrade_sprint_speed_1");
            shop.BuyButton.onClick.Invoke();
            Assert.AreEqual(1, Upgrades.GetUpgradeLevel("upgrade_sprint_speed_1"));
            Assert.AreEqual(54000, Session.money);
            StringAssert.Contains("+16% sprint speed", Field<TMP_Text>(shop, "detailEffect").text, "next level shown");

            shop.SelectCategory(UpgradeCategory.Cosmetic);
            Assert.AreEqual(3, shop.ListedIds.Count, "three outfits");
            shop.Select("upgrade_outfit_tactical_1");
            shop.BuyButton.onClick.Invoke();
            Assert.AreEqual("upgrade_outfit_tactical_1", Session.equippedOutfitId);
            Assert.IsTrue(shop.EquipButton.gameObject.activeSelf);
            shop.EquipButton.onClick.Invoke();
            Assert.AreEqual("", Session.equippedOutfitId, "take off");

            Field<UnityEngine.UI.Button>(board, "financeTab").onClick.Invoke();
            yield return null;
            FinanceView finance = Object.FindAnyObjectByType<FinanceView>();
            Assert.IsNotNull(finance, "finance page not shown");
            Assert.IsNull(Object.FindAnyObjectByType<UpgradeShopView>(), "upgrades page still visible");
            Assert.AreEqual(3, finance.LedgerRowCount, "grant + two purchases");
            Assert.AreEqual(3, finance.ChartBarCount);
            StringAssert.Contains("$60,000", finance.StatsText, "lifetime earnings");
            StringAssert.Contains("$18,000", finance.StatsText, "total spent (6,000 + 12,000)");
            Assert.AreEqual(Session.money / (float)GameConstants.VictoryMoney, Field<UnityEngine.UI.Image>(finance, "goalFill").fillAmount, 0.0001f);

            GameEvents.RaiseMenuBack();
            yield return null;
            Assert.IsFalse(board.IsOpen);
            GameEvents.RaiseContractBoardRequested();
            yield return null;
            Assert.AreEqual(ContractBoardView.TerminalPage.Contracts, board.Page, "terminal reopens on contracts");
            GameEvents.RaiseMenuBack();
        }

        // ---- saving ----------------------------------------------------------------------------

        [Test]
        public void Save_RoundTripsTheWholeCareer()
        {
            Session.hasStarted = true;
            Session.selectedCharacterId = "char_ryan_blake";
            Grant(250000);
            Upgrades.PurchaseUpgrade("upgrade_move_speed_1");
            Upgrades.PurchaseUpgrade("upgrade_move_speed_1");
            Upgrades.PurchaseUpgrade("upgrade_outfit_formal_1");
            Session.unlockedCharacterIds.Add("char_ryan_blake");
            Session.completedMissionIds.Add("mission_downtown_01");
            Session.completedRegionIds.Add("region_downtown");
            Session.missionsFailed = 2;
            Session.playTimeSeconds = 1234.5f;
            Session.activeMissionId = "mission_downtown_02";
            Session.activeObjectivesDone.Add("obj_a");
            Session.activeObjectivesFailed.Add("obj_b");
            Session.activeMissionDetected = true;
            Session.activeMissionElapsed = 77f;
            Session.Settings.vSync = false;
            Session.Settings.mouseSensitivity = 2.25f;
            GameSession expected = JsonUtility.FromJson<GameSession>(JsonUtility.ToJson(Session));
            int ledgerCount = Session.ledger.Count;

            Save.SaveSlot(Slot);
            Session.Reset();
            Assert.IsTrue(Save.LoadSlot(Slot));

            Assert.AreEqual(expected.money, Session.money);
            Assert.AreEqual(expected.lifetimeEarnings, Session.lifetimeEarnings);
            Assert.AreEqual("char_ryan_blake", Session.selectedCharacterId);
            Assert.AreEqual(2, Session.GetUpgradeLevel("upgrade_move_speed_1"), "upgrade levels");
            Assert.AreEqual("upgrade_outfit_formal_1", Session.equippedOutfitId);
            CollectionAssert.AreEqual(expected.unlockedCharacterIds, Session.unlockedCharacterIds);
            CollectionAssert.AreEqual(expected.completedMissionIds, Session.completedMissionIds);
            CollectionAssert.AreEqual(expected.completedRegionIds, Session.completedRegionIds);
            Assert.AreEqual(ledgerCount, Session.ledger.Count, "ledger");
            Assert.AreEqual(expected.ledger.Last().source, Session.ledger.Last().source);
            Assert.AreEqual(expected.ledger.Last().balanceAfter, Session.ledger.Last().balanceAfter);
            Assert.AreEqual(2, Session.missionsFailed);
            Assert.AreEqual(1234.5f, Session.playTimeSeconds, 0.01f);
            Assert.AreEqual("mission_downtown_02", Session.activeMissionId);
            CollectionAssert.AreEqual(new[] { "obj_a" }, Session.activeObjectivesDone);
            CollectionAssert.AreEqual(new[] { "obj_b" }, Session.activeObjectivesFailed);
            Assert.IsTrue(Session.activeMissionDetected);
            Assert.AreEqual(77f, Session.activeMissionElapsed, 0.01f);
            Assert.IsFalse(Session.Settings.vSync);
            Assert.AreEqual(2.25f, Session.Settings.mouseSensitivity, 0.001f);
            Assert.IsTrue(Session.hasStarted);
            Assert.AreEqual(0.12f, PlayerUpgrades.Bonus(UpgradeEffect.MoveSpeed), 0.0001f, "loaded upgrades take effect");
        }

        [Test]
        public void DamagedSave_FallsBackToTheBackup()
        {
            Session.money = 100;
            Save.SaveSlot(Slot);
            Session.money = 200;
            Save.SaveSlot(Slot); // the 100 save becomes the backup
            File.WriteAllText(SaveManager.Instance.GetSlotPath(Slot), "{ this is not json");

            Session.Reset();
            Assert.IsTrue(Save.LoadSlot(Slot), "backup not used");
            Assert.AreEqual(100, Session.money);
            Assert.IsTrue(Save.TryGetSummary(Slot, out SaveSummary summary));
            Assert.IsTrue(summary.restoredFromBackup);
        }

        [Test]
        public void AutoSave_NeverOverwritesACareerWithAnUnstartedSession()
        {
            Session.hasStarted = true;
            Session.money = 777;
            Save.SaveSlot(Slot);
            Session.Reset(); // e.g. launched the game and quit from the main menu
            Save.AutoSave();
            Assert.IsTrue(Save.LoadSlot(Slot));
            Assert.AreEqual(777, Session.money);
        }

        [Test]
        public void VersionTwoSave_StillLoads()
        {
            Directory.CreateDirectory(Path.GetDirectoryName(SaveManager.Instance.GetSlotPath(Slot)));
            File.WriteAllText(SaveManager.Instance.GetSlotPath(Slot),
                "{\"version\":2,\"slotId\":\"autosave\",\"selectedCharacterId\":\"char_alex_morgan\",\"money\":4321,\"hasStarted\":true," +
                "\"completedMissionIds\":[\"mission_downtown_01\"],\"purchasedUpgradeIds\":[\"upgrade_sprint_speed_1\"],\"masterVolume\":0.5}");
            Assert.IsTrue(Save.LoadSlot(Slot));
            Assert.AreEqual(4321, Session.money);
            Assert.AreEqual(1, Session.GetUpgradeLevel("upgrade_sprint_speed_1"));
            Assert.AreEqual(0.5f, Session.Settings.masterVolume, 0.001f);
            Assert.IsTrue(Session.Settings.vSync, "missing v3 field keeps its default");
            Assert.IsEmpty(Session.ledger);
        }

        [UnityTest]
        public IEnumerator Continue_RestoresWhereTheOperativeStood()
        {
            yield return LoadWorld();
            Session.hasStarted = true;
            Transform player = GameObject.FindGameObjectWithTag("Player").transform;
            Vector3 spot = player.position + player.forward * 3f;
            CharacterController cc = player.GetComponent<CharacterController>();
            cc.enabled = false;
            player.SetPositionAndRotation(spot, Quaternion.Euler(0f, 123f, 0f));
            cc.enabled = true;
            Physics.SyncTransforms();
            yield return null;
            Vector3 saved = player.position;
            Save.SaveSlot(Slot);

            Session.Reset();
            SceneManager.LoadScene(GameConstants.Scenes.MainMenu);
            yield return null;
            yield return null;
            Assert.IsTrue(GameManager.Instance.ContinueGame());
            yield return WaitForScene(GameConstants.Scenes.GameWorld);
            Transform restored = GameObject.FindGameObjectWithTag("Player").transform;
            Assert.Less(Vector3.Distance(Flat(saved), Flat(restored.position)), 0.3f, $"player at {restored.position}, saved {saved}");
            Assert.AreEqual(123f, restored.eulerAngles.y, 1f);
            Assert.IsFalse(Session.hasSavedPlayerPosition, "position is applied once");
            NpcManager.Instance?.DespawnAllCivilians();
        }

        [UnityTest]
        public IEnumerator PauseMenuSave_WritesTheFile_AndShowsSaved()
        {
            yield return LoadWorld();
            Session.hasStarted = true;
            Session.money = 4242;
            PauseMenuController pause = Object.FindAnyObjectByType<PauseMenuController>();
            pause.OnPause();
            yield return null;
            Field<UnityEngine.UI.Button>(pause, "saveButton").onClick.Invoke();
            yield return null;
            Assert.IsTrue(Object.FindAnyObjectByType<SaveIndicatorView>().Visible, "no Saved badge");
            Assert.IsTrue(Save.TryGetSummary(Slot, out SaveSummary summary));
            Assert.AreEqual(4242, summary.money);
            pause.OnResume();
        }

        [UnityTest]
        public IEnumerator MainMenu_ContinueShowsTheSaveSummary()
        {
            Session.hasStarted = true;
            Session.selectedCharacterId = "char_daniel_cross";
            Session.money = 98765;
            Save.SaveSlot(Slot);
            Session.Reset();
            SceneManager.LoadScene(GameConstants.Scenes.MainMenu);
            for (int i = 0; i < 3; i++) yield return null;
            string detail = Field<TMP_Text>(Object.FindAnyObjectByType<MainMenuController>(), "continueDetail").text;
            StringAssert.Contains("Daniel Cross", detail);
            StringAssert.Contains("$98,765", detail);
        }

        // ---- play time and victory ----------------------------------------------------------------

        [UnityTest]
        public IEnumerator PlayTime_CountsInTheCity_ButNotWhilePaused()
        {
            yield return LoadWorld();
            Session.hasStarted = true;
            float start = Session.playTimeSeconds;
            yield return new WaitForSecondsRealtime(0.5f);
            float running = Session.playTimeSeconds - start;
            Assert.AreEqual(0.5f, running, 0.15f);
            GameManager.Instance.SetPaused(true);
            float pausedAt = Session.playTimeSeconds;
            yield return new WaitForSecondsRealtime(0.4f);
            Assert.AreEqual(pausedAt, Session.playTimeSeconds, 0.0001f);
            GameManager.Instance.SetPaused(false);
        }

        [UnityTest]
        public IEnumerator Victory_ShowsCareerStats_SavesTheWin_AndCanReturnToTheMenu()
        {
            yield return LoadWorld();
            Session.hasStarted = true;
            Session.playTimeSeconds = 3725f;
            Grant(GameConstants.VictoryMoney);
            yield return null;
            yield return null;
            MissionResultView view = Object.FindAnyObjectByType<MissionResultView>();
            Assert.IsTrue(view.VictoryOpen, "victory screen");
            string stats = Field<TMP_Text>(view, "victoryStats").text;
            StringAssert.Contains("$1,000,000", stats);
            StringAssert.Contains("1h 02m", stats, "play time");
            Assert.IsTrue(Save.TryGetSummary(Slot, out SaveSummary summary) && summary.victoryAchieved, "win not saved");

            Field<UnityEngine.UI.Button>(view, "victoryMainMenu").onClick.Invoke();
            yield return WaitForScene(GameConstants.Scenes.MainMenu);
            Assert.IsFalse(GameManager.Instance.IsMenuOpen);
        }
    }
}
