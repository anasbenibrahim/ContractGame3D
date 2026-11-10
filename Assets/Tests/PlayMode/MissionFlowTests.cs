using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Contract.Contracts;
using Contract.Core;
using Contract.Data;
using Contract.Interaction;
using Contract.Missions;
using Contract.NPCs;
using Contract.Progression;
using Contract.Regions;
using Contract.UI;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Contract.Tests
{
    /// <summary>
    /// Stage 6 verification: contracts from the contract board to the payout, reward maths,
    /// duplicate-reward protection, stealth failure + retry, region progression, the $1,000,000
    /// victory, the money ledger and mission-bound objects. Played through the real input pipeline
    /// where the player acts (virtual gamepad: North = interact, Start = back).
    /// </summary>
    public sealed class MissionFlowTests
    {
        private Gamepad gamepad;
        private InputSettings.BackgroundBehavior savedBackground;
        private Transform player;
        private MissionManager missions;
        private readonly List<string> toasts = new List<string>();
        private int victories;

        [SetUp]
        public void Setup()
        {
            savedBackground = InputSystem.settings.backgroundBehavior;
            InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            gamepad = InputSystem.AddDevice<Gamepad>("MissionTestGamepad");
            toasts.Clear();
            victories = 0;
        }

        [TearDown]
        public void TearDown()
        {
            GameEvents.MissionMessage -= OnToast;
            GameEvents.VictoryReached -= OnVictory;
            if (gamepad != null) InputSystem.RemoveDevice(gamepad);
            InputSystem.settings.backgroundBehavior = savedBackground;
            GameManager.Instance?.Session.Reset();
        }

        private void OnToast(string ko, string en) => toasts.Add(en);
        private void OnVictory() => victories++;

        private IEnumerator LoadWorld()
        {
            GameManager.Instance.SetPaused(false);
            GameManager.Instance.Session.Reset(); // fresh career for every test
            GameManager.Instance.Session.selectedCharacterId = "char_alex_morgan";
            SceneManager.LoadScene(GameConstants.Scenes.GameWorld, LoadSceneMode.Single);
            for (int i = 0; i < 6; i++) yield return null;
            NpcManager.Instance.DespawnAllCivilians();
            player = GameObject.FindGameObjectWithTag("Player").transform;
            missions = MissionManager.Instance;
            GameEvents.MissionMessage += OnToast;
            GameEvents.VictoryReached += OnVictory;
        }

        private void Teleport(Vector3 position, Vector3 lookAt)
        {
            Vector3 dir = lookAt - position;
            dir.y = 0f;
            player.SetPositionAndRotation(position, Quaternion.LookRotation(dir));
            Physics.SyncTransforms();
        }

        private IEnumerator Press(GamepadButton button, float hold = 0f)
        {
            InputSystem.QueueStateEvent(gamepad, new GamepadState().WithButton(button, true));
            yield return null;
            if (hold > 0f) yield return new WaitForSeconds(hold);
            InputSystem.QueueStateEvent(gamepad, new GamepadState());
            yield return null;
            yield return null;
        }

        private IEnumerator InteractAt(Vector3 standAt, Vector3 target, float hold = 0f)
        {
            Teleport(standAt, target);
            yield return null;
            yield return null;
            yield return Press(GamepadButton.North, hold);
        }

        private static Transform Find(string path) => GameObject.Find(path).transform;
        private static GameSession Session => GameManager.Instance.Session;

        // ---- reward maths --------------------------------------------------------------------

        [Test]
        public void RewardCalculator_AppliesStealthSpeedAndOptionalBonuses()
        {
            MissionData mission = ScriptableObject.CreateInstance<MissionData>();
            mission.missionId = "test";
            mission.rewardMoney = 15000;
            mission.timeLimitMinutes = 15;

            MissionResult perfect = RewardCalculator.Calculate(mission, detected: false, elapsedSeconds: 300f, optionalCompleted: 0, optionalTotal: 0);
            Assert.AreEqual(3000, perfect.stealthBonus);
            Assert.AreEqual(1500, perfect.speedBonus);
            Assert.AreEqual(19500, perfect.totalReward);

            MissionResult sloppy = RewardCalculator.Calculate(mission, detected: true, elapsedSeconds: 1000f, optionalCompleted: 0, optionalTotal: 0);
            Assert.AreEqual(15000, sloppy.totalReward, "Spotted and late: base reward only");

            MissionResult optional = RewardCalculator.Calculate(mission, detected: true, elapsedSeconds: 1000f, optionalCompleted: 2, optionalTotal: 2);
            Assert.AreEqual(18000, optional.totalReward, "+10% per optional objective");
            Object.DestroyImmediate(mission);
        }

        [Test]
        public void UiSymbols_ExistInTheGameFont()
        {
            // Status symbols used by the HUD, contract board and result screens (no tofu boxes).
            TMPro.TMP_FontAsset font = TMPro.TMP_Settings.defaultFontAsset;
            Assert.IsTrue(font.HasCharacters("✓▶○×■★☆—·", out uint[] missing, true, true),
                $"Missing glyphs: {string.Join(" ", (missing ?? new uint[0]).Select(c => ((char)c).ToString()))}");
        }

        // ---- the full loop -------------------------------------------------------------------

        [UnityTest]
        public IEnumerator Contract01_FromTheBoardToThePayout()
        {
            yield return LoadWorld();
            var board = Object.FindAnyObjectByType<ContractBoardView>();
            var hud = Object.FindAnyObjectByType<MissionHudView>();
            var result = Object.FindAnyObjectByType<MissionResultView>();

            // 1-4. Safehouse terminal -> contract board -> select -> details -> accept.
            Transform terminal = Find("Interactables/SafehouseTerminal");
            Vector3 standAt = new Vector3(terminal.position.x, player.position.y + 0.05f, terminal.position.z + 1.3f); // inside the safehouse, facing the terminal
            yield return InteractAt(standAt, terminal.position);
            Assert.IsTrue(board.IsOpen, "Safehouse terminal did not open the contract board");
            Assert.IsTrue(GameManager.Instance.IsMenuOpen);
            Assert.AreEqual("region_downtown", board.RegionId);
            Assert.AreEqual(3, board.MissionButtons.Count, "Gangnam should list its three contracts");
            board.SelectMission("mission_downtown_01");
            board.StartButton.onClick.Invoke();
            Assert.IsFalse(board.IsOpen, "Board stays open after accepting");
            Assert.AreEqual("mission_downtown_01", Session.activeMissionId);

            // 5-6. HUD shows the objectives.
            yield return null;
            StringAssert.Contains("Find the corporate office", hud.ObjectivesText);

            // 7-10. Play it: reach the office, hack the terminal, take the documents.
            Transform mirae = Find("District_Gangnam/Buildings/MiraeTower");
            Teleport(mirae.TransformPoint(new Vector3(0f, 0.05f, 8f)), mirae.position);
            yield return new WaitForSeconds(0.3f);
            Transform secure = Find("Gangnam_MissionContent/Terminal_secure_terminal");
            yield return InteractAt(mirae.TransformPoint(new Vector3(3f, 0.05f, -7.3f)), secure.position, hold: 3.4f);
            Transform docs = Find("Gangnam_MissionContent/Pickup_documents_evidence");
            yield return InteractAt(mirae.TransformPoint(new Vector3(-7f, 0.05f, -7.1f)), docs.position);
            Assert.AreEqual(3, Session.activeObjectivesDone.Count, $"Objectives done: {string.Join(",", Session.activeObjectivesDone)}");
            StringAssert.Contains("✓", hud.ObjectivesText);

            // 11-14. Extract -> complete -> reward -> balance.
            ExtractionInteractable extraction = Object.FindAnyObjectByType<ExtractionInteractable>();
            yield return InteractAt(extraction.transform.position + new Vector3(0f, 0.05f, 1.2f), extraction.transform.position);
            Assert.IsTrue(Session.IsMissionCompleted("mission_downtown_01"), "Contract not completed at extraction");
            Assert.IsTrue(string.IsNullOrEmpty(Session.activeMissionId));

            MissionResult r = result.Current;
            Assert.IsTrue(result.ResultOpen, "No result screen");
            Assert.IsTrue(r.success);
            Assert.AreEqual(15000, r.baseReward);
            Assert.AreEqual(1500, r.speedBonus, "Finished well within 15 minutes");
            Assert.AreEqual(r.detected ? 0 : 3000, r.stealthBonus);
            Assert.AreEqual(r.totalReward, Session.money, "Balance does not match the reward");
            Assert.AreEqual($"${Session.money:N0}", hud.MoneyText, "HUD balance not updated");
            StringAssert.Contains("+$15,000", result.BreakdownText);
            LedgerEntry last = Session.ledger.Last();
            Assert.AreEqual(r.totalReward, last.amount);
            StringAssert.Contains("The Missing Documents", last.source);

            // Escape closes the result screen without pausing.
            yield return Press(GamepadButton.Start);
            Assert.IsFalse(result.ResultOpen);
            Assert.IsFalse(GameManager.Instance.IsPaused);
        }

        [UnityTest]
        public IEnumerator Extraction_BeforeTheObjectives_IsRefused()
        {
            yield return LoadWorld();
            missions.StartMission("mission_downtown_01");
            ExtractionInteractable extraction = Object.FindAnyObjectByType<ExtractionInteractable>();
            yield return InteractAt(extraction.transform.position + new Vector3(0f, 0.05f, 1.2f), extraction.transform.position);

            Assert.AreEqual("mission_downtown_01", Session.activeMissionId, "Contract ended early");
            Assert.IsFalse(Session.IsMissionCompleted("mission_downtown_01"));
            Assert.AreEqual(0, Session.money);
            Assert.IsTrue(toasts.Any(t => t.Contains("Finish the remaining objectives")), "No explanation shown");
        }

        [UnityTest]
        public IEnumerator Rewards_ArePaidOnlyOnce()
        {
            yield return LoadWorld();
            missions.StartMission("mission_downtown_02");
            missions.CompleteMission("mission_downtown_02");
            int afterFirst = Session.money;
            Assert.Greater(afterFirst, 0);

            missions.CompleteMission("mission_downtown_02");
            missions.StartMission("mission_downtown_02");
            Assert.AreEqual(afterFirst, Session.money, "Paid twice");
            Assert.IsFalse(missions.CanStart("mission_downtown_02", out string reason));
            StringAssert.Contains("Already completed", reason);
            Assert.AreEqual(1, Session.ledger.Count);
        }

        [UnityTest]
        public IEnumerator StealthContract_FailsWhenSpotted_AndCanBeRetried()
        {
            yield return LoadWorld();
            var result = Object.FindAnyObjectByType<MissionResultView>();
            missions.StartMission("mission_downtown_03");
            Transform hangyeol = Find("District_Gangnam/Buildings/HangyeolGroup");
            Teleport(hangyeol.TransformPoint(new Vector3(3.5f, 0.05f, -2.2f)), hangyeol.position);
            yield return new WaitForSeconds(0.3f);
            string enterServerRoom = missions.ActiveMission.objectives[0].objectiveId;
            Assert.Contains(enterServerRoom, Session.activeObjectivesDone, "Entering the server room did not count");

            GameEvents.RaiseNpcAlerted("guard_hangyeol_01"); // spotted on a "leave without triggering alarms" contract
            yield return null;
            Assert.IsTrue(string.IsNullOrEmpty(Session.activeMissionId), "Contract still active after failing");
            Assert.AreEqual(1, Session.missionsFailed);
            Assert.IsTrue(result.ResultOpen);
            Assert.IsFalse(result.Current.success);
            StringAssert.Contains("spotted", result.Current.failureReason);
            Assert.AreEqual(0, Session.money, "Failed contract paid out");

            // Retry from the result screen: fresh objectives, the server room counts again.
            Object.FindObjectsByType<UnityEngine.UI.Button>(FindObjectsSortMode.None).First(b => b.name == "Primary").onClick.Invoke();
            Assert.AreEqual("mission_downtown_03", Session.activeMissionId, "Retry did not restart the contract");
            Assert.IsEmpty(Session.activeObjectivesDone);
            yield return new WaitForSeconds(0.3f); // still standing in the server room
            Assert.AreEqual(1, Session.activeObjectivesDone.Count, "Location trigger did not re-arm for the retry");
        }

        [UnityTest]
        public IEnumerator CompletingGangnam_PaysTheRegionBonusOnce_AndUnlocksTheNextRegions()
        {
            yield return LoadWorld();
            IRegionService regions = GameManager.Instance.GetService<IRegionService>();
            var unlocked = new List<string>();
            GameEvents.RegionUnlocked += id => unlocked.Add(id);

            int rewards = 0;
            foreach (string id in new[] { "mission_downtown_01", "mission_downtown_02", "mission_downtown_03" })
            {
                missions.StartMission(id);
                missions.CompleteMission(id);
                rewards += missions.LastResult.totalReward;
            }

            Assert.IsTrue(Session.IsRegionCompleted("region_downtown"));
            Assert.AreEqual(100000, missions.LastResult.regionBonus);
            Assert.AreEqual(rewards + 100000, Session.money, "Region bonus not paid exactly once");
            Assert.AreEqual(0, regions.CompleteRegion("region_downtown"), "Region bonus could be paid twice");

            CollectionAssert.IsSupersetOf(unlocked, new[] { "region_industrial", "region_residential" }, "Myeongdong / Bukchon not unlocked");
            Assert.IsFalse(regions.IsRegionUnlocked("region_luxury"), "Haeundae needs both Myeongdong and Bukchon");
            Assert.IsTrue(missions.CanStart("mission_industrial_01", out _), "Myeongdong is built (Phase 7) and now unlocked");
            Assert.IsFalse(missions.CanStart("mission_residential_01", out string reason), "Unbuilt district (Bukchon) must not be playable");
            StringAssert.Contains("not available yet", reason);
        }

        [UnityTest]
        public IEnumerator ReachingOneMillion_TriggersVictoryOnce_AfterTheContractScreen()
        {
            yield return LoadWorld();
            var result = Object.FindAnyObjectByType<MissionResultView>();
            Session.money = 990000;
            missions.StartMission("mission_downtown_02");
            missions.CompleteMission("mission_downtown_02");
            yield return null;

            Assert.AreEqual(1, victories);
            Assert.IsTrue(Session.victoryAchieved);
            Assert.IsTrue(result.ResultOpen, "Contract breakdown should show first");
            Assert.IsFalse(result.VictoryOpen);
            result.CloseResult();
            Assert.IsTrue(result.VictoryOpen, "Victory screen did not follow the contract screen");

            GameManager.Instance.GetService<IEconomyService>().AddMoney(5000, "test");
            Assert.AreEqual(1, victories, "Victory raised twice");
        }

        [UnityTest]
        public IEnumerator MissionObjects_OnlyWorkDuringTheirContract()
        {
            yield return LoadWorld();
            PickupInteractable documents = Find("Gangnam_MissionContent/Pickup_documents_evidence").GetComponent<PickupInteractable>();
            Assert.IsFalse(documents.CanInteract(player.gameObject), "Documents takeable before accepting the contract");

            missions.StartMission("mission_downtown_02");
            Assert.IsFalse(documents.CanInteract(player.gameObject), "Documents takeable during another contract");

            missions.StartMission("mission_downtown_01");
            Assert.IsTrue(documents.CanInteract(player.gameObject));
            documents.Interact(player.gameObject);
            Assert.IsTrue(documents.IsCollected);

            missions.FailMission("mission_downtown_01", "test");
            missions.StartMission("mission_downtown_01"); // retry puts the documents back
            Assert.IsFalse(documents.IsCollected);
            Assert.IsTrue(documents.GetComponentInChildren<Renderer>().enabled);
        }

        [UnityTest]
        public IEnumerator Purchases_GoThroughTheEconomyAndLedger()
        {
            yield return LoadWorld();
            var changes = new List<int>();
            GameEvents.MoneyChanged += m => changes.Add(m);
            GameManager.Instance.GetService<IEconomyService>().AddMoney(50000, "test grant");
            UpgradeData upgrade = UpgradeManager.Instance.GetAvailableUpgrades().First(u => u.cost > 0 && u.cost <= 50000);

            Assert.IsTrue(UpgradeManager.Instance.PurchaseUpgrade(upgrade.upgradeId));
            Assert.AreEqual(50000 - upgrade.cost, Session.money);
            Assert.AreEqual(Session.money, changes.Last(), "HUD not notified of the purchase");
            Assert.AreEqual(-upgrade.cost, Session.ledger.Last().amount);
            StringAssert.Contains(upgrade.upgradeName, Session.ledger.Last().source);
        }

        [UnityTest]
        public IEnumerator Intelligence_SpeedsUpTerminalWork()
        {
            yield return LoadWorld();
            float alex = player.GetComponent<InteractionController>().HoldSpeedMultiplier;
            GameManager.Instance.Session.selectedCharacterId = "char_ryan_blake";
            SceneManager.LoadScene(GameConstants.Scenes.GameWorld, LoadSceneMode.Single);
            for (int i = 0; i < 6; i++) yield return null;
            float ryan = GameObject.FindGameObjectWithTag("Player").GetComponent<InteractionController>().HoldSpeedMultiplier;
            Assert.Greater(ryan, alex, "Ryan Blake (intelligence 95) should hack faster than Alex Morgan (70)");
        }
    }
}
