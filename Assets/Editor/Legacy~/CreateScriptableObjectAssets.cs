using System;
using Contract.Character;
using Contract.Data;
using Contract.Items;
using Contract.Missions;
using Contract.Progression;
using Contract.Regions;
using Contract.Audio;
using UnityEngine;
using UnityEditor;

namespace Contract.EditorTools
{
    /// <summary>
    /// Creates all ScriptableObject assets with sample data.
    /// Run: CONTRACT/Setup/Create ScriptableObject Assets
    /// </summary>
    public static class CreateScriptableObjectAssets
    {
        private const string AssetPath = "Assets/ScriptableObjects";

        [MenuItem("CONTRACT/Setup/Create ScriptableObject Assets")]
        public static void CreateAllAssets()
        {
            CreateFolders();
            CreateCharacters();
            CreateRegions();
            CreateMissions();
            CreateUpgrades();
            CreateItems();
            CreateAudioLibrary();
            CreateItemLibrary();

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[CONTRACT] All ScriptableObject assets created successfully.");
        }

        private static void CreateFolders()
        {
            string[] folders = {
                "Characters", "Missions", "Regions", "Upgrades", "Items"
            };

            foreach (string folder in folders)
            {
                string path = $"{AssetPath}/{folder}";
                if (!AssetDatabase.IsValidFolder(path))
                {
                    AssetDatabase.CreateFolder(AssetPath, folder);
                }
            }
        }

        private static void CreateCharacters()
        {
            // Alex Morgan - Speed Specialist
            CharacterData alex = CreateAsset<CharacterData>("Characters/char_alex_morgan");
            alex.characterId = "char_alex_morgan";
            alex.displayName = "Alex Morgan";
            alex.specialty = "Speed";
            alex.description = "Former Olympic sprinter turned operative. Unmatched movement speed and agility.";
            alex.moveSpeed = 90f;
            alex.sprintSpeed = 95f;
            alex.stealthRating = 65f;
            alex.intelligence = 70f;
            alex.suspicionDecay = 80f;
            alex.suspicionBuildRate = 60f;
            alex.interactionRange = 70f;
            alex.lockpickChance = 50f;
            alex.startingMoney = 5000;
            alex.unlockedByDefault = true;
            alex.themeColor = new Color(1f, 0.2f, 0.2f);

            // Daniel Cross - Stealth Specialist
            CharacterData daniel = CreateAsset<CharacterData>("Characters/char_daniel_cross");
            daniel.characterId = "char_daniel_cross";
            daniel.displayName = "Daniel Cross";
            daniel.specialty = "Stealth";
            daniel.description = "Ghost-like infiltrator. Enemies struggle to detect him even at close range.";
            daniel.moveSpeed = 70f;
            daniel.sprintSpeed = 75f;
            daniel.stealthRating = 95f;
            daniel.intelligence = 80f;
            daniel.suspicionDecay = 90f;
            daniel.suspicionBuildRate = 30f;
            daniel.interactionRange = 80f;
            daniel.lockpickChance = 85f;
            daniel.startingMoney = 3000;
            daniel.unlockedByDefault = true;
            daniel.themeColor = new Color(0.2f, 0.5f, 1f);

            // Ryan Blake - Intelligence Specialist
            CharacterData ryan = CreateAsset<CharacterData>("Characters/char_ryan_blake");
            ryan.characterId = "char_ryan_blake";
            ryan.displayName = "Ryan Blake";
            ryan.specialty = "Intelligence";
            ryan.description = "Analyst turned field agent. Superior information gathering and hacking abilities.";
            ryan.moveSpeed = 75f;
            ryan.sprintSpeed = 80f;
            ryan.stealthRating = 80f;
            ryan.intelligence = 95f;
            ryan.suspicionDecay = 70f;
            ryan.suspicionBuildRate = 50f;
            ryan.interactionRange = 95f;
            ryan.lockpickChance = 95f;
            ryan.startingMoney = 10000;
            ryan.unlockedByDefault = false;
            ryan.unlockCost = 50000;
            ryan.themeColor = new Color(0.2f, 1f, 0.3f);
        }

        private static void CreateRegions()
        {
            // Downtown City
            RegionData downtown = CreateAsset<RegionData>("Regions/region_downtown");
            downtown.regionId = "region_downtown";
            downtown.regionName = "Downtown Seoul";
            downtown.description = "Neon-lit business district. High-security corporate towers and crowded streets.";
            downtown.sceneName = "GameWorld";
            downtown.atmosphereColor = new Color(0.05f, 0.07f, 0.12f);
            downtown.unlockedByDefault = true;
            downtown.missionIds = new[] { "mission_downtown_01", "mission_downtown_02", "mission_downtown_03" };
            downtown.safehouseId = "safehouse_downtown";
            downtown.extractionId = "extraction_downtown";
            downtown.civilianNPCCount = 15;
            downtown.missionNPCCount = 3;
            downtown.centerPosition = Vector3.zero;
            downtown.regionSize = new Vector3(120f, 20f, 120f);

            // Industrial District
            RegionData industrial = CreateAsset<RegionData>("Regions/region_industrial");
            industrial.regionId = "region_industrial";
            industrial.regionName = "Industrial District";
            industrial.description = "Factories, warehouses, and shipping yards. Heavy machinery provides cover.";
            industrial.sceneName = "GameWorld";
            industrial.atmosphereColor = new Color(0.1f, 0.1f, 0.12f);
            industrial.unlockedByDefault = false;
            industrial.requiredRegionIds = new[] { "region_downtown" };
            industrial.unlockCost = 25000;
            industrial.missionIds = new[] { "mission_industrial_01", "mission_industrial_02" };
            industrial.safehouseId = "safehouse_industrial";
            industrial.extractionId = "extraction_industrial";
            industrial.civilianNPCCount = 8;
            industrial.missionNPCCount = 2;
            industrial.centerPosition = new Vector3(200f, 0f, 0f);
            industrial.regionSize = new Vector3(100f, 20f, 100f);

            // Residential District
            RegionData residential = CreateAsset<RegionData>("Regions/region_residential");
            residential.regionId = "region_residential";
            residential.regionName = "Residential District";
            residential.description = "Apartment complexes and quiet streets. Civilians are more observant at night.";
            residential.sceneName = "GameWorld";
            residential.atmosphereColor = new Color(0.12f, 0.08f, 0.06f);
            residential.unlockedByDefault = false;
            residential.requiredRegionIds = new[] { "region_downtown" };
            residential.unlockCost = 50000;
            residential.missionIds = new[] { "mission_residential_01", "mission_residential_02", "mission_residential_03" };
            residential.safehouseId = "safehouse_residential";
            residential.extractionId = "extraction_residential";
            residential.civilianNPCCount = 20;
            residential.missionNPCCount = 2;
            residential.centerPosition = new Vector3(-200f, 0f, 0f);
            residential.regionSize = new Vector3(120f, 20f, 120f);

            // Luxury District
            RegionData luxury = CreateAsset<RegionData>("Regions/region_luxury");
            luxury.regionId = "region_luxury";
            luxury.regionName = "Luxury District";
            luxury.description = "High-end boutiques, clubs, and penthouses. Heavy security, high-value targets.";
            luxury.sceneName = "GameWorld";
            luxury.atmosphereColor = new Color(0.15f, 0.12f, 0.05f);
            luxury.unlockedByDefault = false;
            luxury.requiredRegionIds = new[] { "region_industrial", "region_residential" };
            luxury.unlockCost = 100000;
            luxury.missionIds = new[] { "mission_luxury_01", "mission_luxury_02" };
            luxury.safehouseId = "safehouse_luxury";
            luxury.extractionId = "extraction_luxury";
            luxury.civilianNPCCount = 10;
            luxury.missionNPCCount = 3;
            luxury.centerPosition = new Vector3(0f, 0f, 200f);
            luxury.regionSize = new Vector3(100f, 20f, 100f);

            // Harbor District
            RegionData harbor = CreateAsset<RegionData>("Regions/region_harbor");
            harbor.regionId = "region_harbor";
            harbor.regionName = "Harbor District";
            harbor.description = "Shipping containers, docks, and warehouses. Final extraction point for the campaign.";
            harbor.sceneName = "GameWorld";
            harbor.atmosphereColor = new Color(0.05f, 0.08f, 0.15f);
            harbor.unlockedByDefault = false;
            harbor.requiredRegionIds = new[] { "region_luxury" };
            harbor.unlockCost = 200000;
            harbor.missionIds = new[] { "mission_harbor_01", "mission_harbor_02", "mission_harbor_03" };
            harbor.safehouseId = "safehouse_harbor";
            harbor.extractionId = "extraction_harbor";
            harbor.civilianNPCCount = 5;
            harbor.missionNPCCount = 4;
            harbor.centerPosition = new Vector3(0f, 0f, -200f);
            harbor.regionSize = new Vector3(150f, 20f, 150f);
        }

        private static void CreateMissions()
        {
            // Mission 1: The Missing Documents
            MissionData m1 = CreateAsset<MissionData>("Missions/mission_downtown_01");
            m1.missionId = "mission_downtown_01";
            m1.missionName = "The Missing Documents";
            m1.description = "Retrieve classified documents from a corporate office before they're destroyed.";
            m1.regionId = "region_downtown";
            m1.subjectId = "subject_ceo_kim";
            m1.subjectName = "CEO Kim Min-jun";
            m1.rewardMoney = 15000;
            m1.rewardXP = 100;
            m1.difficulty = 1;
            m1.timeLimitMinutes = 15;
            m1.objectives = new MissionObjective[]
            {
                new MissionObjective { objectiveId = "obj_reach_office", description = "Find the corporate office", type = ObjectiveType.ReachLocation, targetLocationId = "office_building" },
                new MissionObjective { objectiveId = "obj_access_terminal", description = "Access the secure terminal", type = ObjectiveType.Interact, targetId = "secure_terminal" },
                new MissionObjective { objectiveId = "obj_collect_docs", description = "Retrieve the documents", type = ObjectiveType.CollectItem, targetId = "documents_evidence" },
                new MissionObjective { objectiveId = "obj_extract", description = "Reach extraction point", type = ObjectiveType.Extract, targetLocationId = "extraction_downtown" }
            };

            // Mission 2: Silent Exchange
            MissionData m2 = CreateAsset<MissionData>("Missions/mission_downtown_02");
            m2.missionId = "mission_downtown_02";
            m2.missionName = "Silent Exchange";
            m2.description = "Intercept a data handoff between two couriers in a crowded plaza.";
            m2.regionId = "region_downtown";
            m2.subjectId = "subject_courier_park";
            m2.subjectName = "Courier Park Soo-min";
            m2.rewardMoney = 25000;
            m2.rewardXP = 150;
            m2.difficulty = 2;
            m2.timeLimitMinutes = 10;
            m2.objectives = new MissionObjective[]
            {
                new MissionObjective { objectiveId = "obj_identify_courier", description = "Identify the courier", type = ObjectiveType.IdentifySubject, targetId = "courier_park" },
                new MissionObjective { objectiveId = "obj_photo_exchange", description = "Photograph the exchange", type = ObjectiveType.PhotographSubject, targetId = "courier_park" },
                new MissionObjective { objectiveId = "obj_collect_drive", description = "Retrieve the data drive", type = ObjectiveType.CollectItem, targetId = "data_drive" },
                new MissionObjective { objectiveId = "obj_extract2", description = "Reach extraction point", type = ObjectiveType.Extract, targetLocationId = "extraction_downtown" }
            };

            // Mission 3: Corporate Espionage
            MissionData m3 = CreateAsset<MissionData>("Missions/mission_downtown_03");
            m3.missionId = "mission_downtown_03";
            m3.missionName = "Corporate Espionage";
            m3.description = "Infiltrate the rival company's server room and plant surveillance software.";
            m3.regionId = "region_downtown";
            m3.subjectId = "subject_cto_lee";
            m3.subjectName = "CTO Lee Jin-woo";
            m3.rewardMoney = 40000;
            m3.rewardXP = 200;
            m3.difficulty = 3;
            m3.timeLimitMinutes = 20;
            m3.objectives = new MissionObjective[]
            {
                new MissionObjective { objectiveId = "obj_reach_server", description = "Enter the server room", type = ObjectiveType.ReachLocation, targetLocationId = "server_room" },
                new MissionObjective { objectiveId = "obj_plant_software", description = "Plant the surveillance software", type = ObjectiveType.Interact, targetId = "server_rack" },
                new MissionObjective { objectiveId = "obj_avoid_alarms", description = "Leave without triggering alarms", type = ObjectiveType.AvoidDetection },
                new MissionObjective { objectiveId = "obj_extract3", description = "Reach extraction point", type = ObjectiveType.Extract, targetLocationId = "extraction_downtown" }
            };

            // Mission 4: Warehouse Heist
            MissionData m4 = CreateAsset<MissionData>("Missions/mission_industrial_01");
            m4.missionId = "mission_industrial_01";
            m4.missionName = "Warehouse Heist";
            m4.description = "Steal prototype tech from a heavily guarded warehouse.";
            m4.regionId = "region_industrial";
            m4.subjectId = "subject_guard_captain";
            m4.subjectName = "Captain Han Dae-sung";
            m4.rewardMoney = 35000;
            m4.rewardXP = 180;
            m4.difficulty = 2;
            m4.timeLimitMinutes = 15;
            m4.objectives = new MissionObjective[]
            {
                new MissionObjective { objectiveId = "obj_enter_warehouse", description = "Enter the warehouse", type = ObjectiveType.ReachLocation, targetLocationId = "warehouse_main" },
                new MissionObjective { objectiveId = "obj_locate_captain", description = "Locate the guard captain", type = ObjectiveType.IdentifySubject, targetId = "guard_captain" },
                new MissionObjective { objectiveId = "obj_steal_prototype", description = "Steal the prototype", type = ObjectiveType.CollectItem, targetId = "prototype_tech" },
                new MissionObjective { objectiveId = "obj_extract4", description = "Reach extraction point", type = ObjectiveType.Extract, targetLocationId = "extraction_industrial" }
            };

            // Mission 5: Smuggler's Trail
            MissionData m5 = CreateAsset<MissionData>("Missions/mission_industrial_02");
            m5.missionId = "mission_industrial_02";
            m5.missionName = "Smuggler's Trail";
            m5.description = "Track a weapons smuggler through the shipping yards.";
            m5.regionId = "region_industrial";
            m5.subjectId = "subject_smuggler_choi";
            m5.subjectName = "Choi Min-ho";
            m5.rewardMoney = 50000;
            m5.rewardXP = 250;
            m5.difficulty = 3;
            m5.timeLimitMinutes = 18;
            m5.objectives = new MissionObjective[]
            {
                new MissionObjective { objectiveId = "obj_find_container", description = "Find the shipping container", type = ObjectiveType.ReachLocation, targetLocationId = "container_yard" },
                new MissionObjective { objectiveId = "obj_hack_manifest", description = "Hack the manifest terminal", type = ObjectiveType.Interact, targetId = "manifest_terminal" },
                new MissionObjective { objectiveId = "obj_escort_informant", description = "Escort the informant", type = ObjectiveType.EscortSubject, targetId = "informant_kim" },
                new MissionObjective { objectiveId = "obj_extract5", description = "Reach extraction point", type = ObjectiveType.Extract, targetLocationId = "extraction_industrial" }
            };

            // Mission 6: Neighborhood Watch
            MissionData m6 = CreateAsset<MissionData>("Missions/mission_residential_01");
            m6.missionId = "mission_residential_01";
            m6.missionName = "Neighborhood Watch";
            m6.description = "Investigate a suspicious resident without alerting the community.";
            m6.regionId = "region_residential";
            m6.subjectId = "subject_teacher_yoon";
            m6.subjectName = "Yoon Ji-hye";
            m6.rewardMoney = 30000;
            m6.rewardXP = 160;
            m6.difficulty = 2;
            m6.timeLimitMinutes = 20;
            m6.objectives = new MissionObjective[]
            {
                new MissionObjective { objectiveId = "obj_enter_apartment", description = "Enter the apartment complex", type = ObjectiveType.ReachLocation, targetLocationId = "apartment_complex" },
                new MissionObjective { objectiveId = "obj_locate_unit", description = "Locate the target's unit", type = ObjectiveType.IdentifySubject, targetId = "unit_404" },
                new MissionObjective { objectiveId = "obj_gather_evidence", description = "Gather evidence", type = ObjectiveType.PhotographSubject, targetId = "evidence_folder" },
                new MissionObjective { objectiveId = "obj_extract6", description = "Reach extraction point", type = ObjectiveType.Extract, targetLocationId = "extraction_residential" }
            };

            // Mission 7: Midnight Courier
            MissionData m7 = CreateAsset<MissionData>("Missions/mission_residential_02");
            m7.missionId = "mission_residential_02";
            m7.missionName = "Midnight Courier";
            m7.description = "Intercept a package delivery in a quiet neighborhood.";
            m7.regionId = "region_residential";
            m7.subjectId = "subject_delivery_kang";
            m7.subjectName = "Kang Seo-joon";
            m7.rewardMoney = 45000;
            m7.rewardXP = 220;
            m7.difficulty = 3;
            m7.timeLimitMinutes = 12;
            m7.objectives = new MissionObjective[]
            {
                new MissionObjective { objectiveId = "obj_stakeout", description = "Stake out the drop point", type = ObjectiveType.ReachLocation, targetLocationId = "park_bench" },
                new MissionObjective { objectiveId = "obj_confirm_courier", description = "Confirm the courier", type = ObjectiveType.IdentifySubject, targetId = "courier_kang" },
                new MissionObjective { objectiveId = "obj_retrieve_package", description = "Retrieve the package", type = ObjectiveType.CollectItem, targetId = "package_evidence" },
                new MissionObjective { objectiveId = "obj_extract7", description = "Reach extraction point", type = ObjectiveType.Extract, targetLocationId = "extraction_residential" }
            };

            // Mission 8: Safe House Compromise
            MissionData m8 = CreateAsset<MissionData>("Missions/mission_residential_03");
            m8.missionId = "mission_residential_03";
            m8.missionName = "Safe House Compromise";
            m8.description = "Your safe house has been discovered. Extract with the evidence before cleanup crew arrives.";
            m8.regionId = "region_residential";
            m8.subjectId = "subject_cleaner_park";
            m8.subjectName = "Park Cleaner Team";
            m8.rewardMoney = 60000;
            m8.rewardXP = 300;
            m8.difficulty = 4;
            m8.timeLimitMinutes = 8;
            m8.objectives = new MissionObjective[]
            {
                new MissionObjective { objectiveId = "obj_gather_evidence2", description = "Gather all evidence", type = ObjectiveType.CollectItem, targetId = "evidence_cache" },
                new MissionObjective { objectiveId = "obj_evade_crew", description = "Evade the cleanup crew", type = ObjectiveType.AvoidDetection },
                new MissionObjective { objectiveId = "obj_extract8", description = "Reach extraction point", type = ObjectiveType.Extract, targetLocationId = "extraction_residential" }
            };

            // Mission 9: High Society
            MissionData m9 = CreateAsset<MissionData>("Missions/mission_luxury_01");
            m9.missionId = "mission_luxury_01";
            m9.missionName = "High Society";
            m9.description = "Infiltrate an exclusive gala to bug a crime lord's penthouse.";
            m9.regionId = "region_luxury";
            m9.subjectId = "subject_crime_lord_jung";
            m9.subjectName = "Jung Tae-sik";
            m9.rewardMoney = 75000;
            m9.rewardXP = 400;
            m9.difficulty = 4;
            m9.timeLimitMinutes = 25;
            m9.objectives = new MissionObjective[]
            {
                new MissionObjective { objectiveId = "obj_enter_gala", description = "Enter the gala", type = ObjectiveType.ReachLocation, targetLocationId = "gala_venue" },
                new MissionObjective { objectiveId = "obj_locate_crimelord", description = "Locate the crime lord", type = ObjectiveType.IdentifySubject, targetId = "jung_taesik" },
                new MissionObjective { objectiveId = "obj_plant_device", description = "Plant the listening device", type = ObjectiveType.Interact, targetId = "penthouse_office" },
                new MissionObjective { objectiveId = "obj_extract9", description = "Reach extraction point", type = ObjectiveType.Extract, targetLocationId = "extraction_luxury" }
            };

            // Mission 10: Diamond Heist
            MissionData m10 = CreateAsset<MissionData>("Missions/mission_luxury_02");
            m10.missionId = "mission_luxury_02";
            m10.missionName = "Diamond Heist";
            m10.description = "Steal the 'Star of Seoul' diamond from a high-security vault.";
            m10.regionId = "region_luxury";
            m10.subjectId = "subject_curator_han";
            m10.subjectName = "Han Mi-kyung";
            m10.rewardMoney = 100000;
            m10.rewardXP = 500;
            m10.difficulty = 5;
            m10.timeLimitMinutes = 30;
            m10.objectives = new MissionObjective[]
            {
                new MissionObjective { objectiveId = "obj_enter_museum", description = "Enter the museum", type = ObjectiveType.ReachLocation, targetLocationId = "museum_vault" },
                new MissionObjective { objectiveId = "obj_disable_lasers", description = "Disable the laser grid", type = ObjectiveType.Interact, targetId = "laser_control" },
                new MissionObjective { objectiveId = "obj_steal_diamond", description = "Steal the diamond", type = ObjectiveType.CollectItem, targetId = "star_of_seoul" },
                new MissionObjective { objectiveId = "obj_avoid_alarms2", description = "Escape without triggering alarms", type = ObjectiveType.AvoidDetection },
                new MissionObjective { objectiveId = "obj_extract10", description = "Reach extraction point", type = ObjectiveType.Extract, targetLocationId = "extraction_luxury" }
            };

            // Mission 11: Final Shipment
            MissionData m11 = CreateAsset<MissionData>("Missions/mission_harbor_01");
            m11.missionId = "mission_harbor_01";
            m11.missionName = "Final Shipment";
            m11.description = "Stop the final weapons shipment before it leaves the harbor.";
            m11.regionId = "region_harbor";
            m11.subjectId = "subject_captain_lee";
            m11.subjectName = "Captain Lee Chang-ho";
            m11.rewardMoney = 125000;
            m11.rewardXP = 600;
            m11.difficulty = 5;
            m11.timeLimitMinutes = 20;
            m11.objectives = new MissionObjective[]
            {
                new MissionObjective { objectiveId = "obj_board_ship", description = "Board the cargo ship", type = ObjectiveType.ReachLocation, targetLocationId = "cargo_ship" },
                new MissionObjective { objectiveId = "obj_locate_captain", description = "Locate the captain", type = ObjectiveType.IdentifySubject, targetId = "captain_lee" },
                new MissionObjective { objectiveId = "obj_disable_engines", description = "Disable the ship's engines", type = ObjectiveType.Interact, targetId = "engine_room" },
                new MissionObjective { objectiveId = "obj_seize_manifest", description = "Seize the manifest", type = ObjectiveType.CollectItem, targetId = "shipping_manifest" },
                new MissionObjective { objectiveId = "obj_extract11", description = "Reach extraction point", type = ObjectiveType.Extract, targetLocationId = "extraction_harbor" }
            };

            // Mission 12: Endgame
            MissionData m12 = CreateAsset<MissionData>("Missions/mission_harbor_02");
            m12.missionId = "mission_harbor_02";
            m12.missionName = "Endgame";
            m12.description = "Confront the mastermind behind the conspiracy. Final extraction.";
            m12.regionId = "region_harbor";
            m12.subjectId = "subject_mastermind";
            m12.subjectName = "The Director";
            m12.rewardMoney = 200000;
            m12.rewardXP = 1000;
            m12.difficulty = 5;
            m12.timeLimitMinutes = 30;
            m12.objectives = new MissionObjective[]
            {
                new MissionObjective { objectiveId = "obj_reach_command", description = "Reach the command center", type = ObjectiveType.ReachLocation, targetLocationId = "command_center" },
                new MissionObjective { objectiveId = "obj_confront_director", description = "Confront the Director", type = ObjectiveType.IdentifySubject, targetId = "director" },
                new MissionObjective { objectiveId = "obj_upload_evidence", description = "Upload the evidence", type = ObjectiveType.Interact, targetId = "evidence_server" },
                new MissionObjective { objectiveId = "obj_extract12", description = "Final extraction", type = ObjectiveType.Extract, targetLocationId = "extraction_harbor" }
            };

            // Mission 13: Cleanup
            MissionData m13 = CreateAsset<MissionData>("Missions/mission_harbor_03");
            m13.missionId = "mission_harbor_03";
            m13.missionName = "Cleanup";
            m13.description = "Eliminate remaining loose ends. Secure the region permanently.";
            m13.regionId = "region_harbor";
            m13.subjectId = "subject_lieutenant_kim";
            m13.subjectName = "Lieutenant Kim";
            m13.rewardMoney = 150000;
            m13.rewardXP = 750;
            m13.difficulty = 4;
            m13.timeLimitMinutes = 15;
            m13.objectives = new MissionObjective[]
            {
                new MissionObjective { objectiveId = "obj_clear_warehouse", description = "Clear the warehouse district", type = ObjectiveType.ReachLocation, targetLocationId = "warehouse_district" },
                new MissionObjective { objectiveId = "obj_locate_lieutenant", description = "Locate the lieutenant", type = ObjectiveType.IdentifySubject, targetId = "lieutenant_kim" },
                new MissionObjective { objectiveId = "obj_secure_blackbox", description = "Secure the black box", type = ObjectiveType.CollectItem, targetId = "black_box" },
                new MissionObjective { objectiveId = "obj_extract13", description = "Final extraction", type = ObjectiveType.Extract, targetLocationId = "extraction_harbor" }
            };
        }

        private static void CreateUpgrades()
        {
            // Movement Upgrades
            CreateUpgrade("upgrade_sprint_speed_1", "Enhanced Sprint I", "Increase sprint speed by 10%", UpgradeCategory.Movement, 5000, 3, sprintBonus: 10);
            CreateUpgrade("upgrade_sprint_speed_2", "Enhanced Sprint II", "Increase sprint speed by 15%", UpgradeCategory.Movement, 15000, 3, sprintBonus: 15);
            CreateUpgrade("upgrade_move_speed_1", "Light Footwork I", "Increase base movement speed by 8%", UpgradeCategory.Movement, 8000, 3, moveBonus: 8);
            CreateUpgrade("upgrade_crouch_speed_1", "Silent Movement I", "Increase crouch movement speed by 20%", UpgradeCategory.Movement, 10000, 2, moveBonus: 15);
            CreateUpgrade("upgrade_stamina_1", "Endurance Training I", "Increase stamina regeneration by 25%", UpgradeCategory.Movement, 12000, 3, sprintBonus: 5);
            CreateUpgrade("upgrade_jump_height_1", "Vertical Leap I", "Increase jump height by 15%", UpgradeCategory.Movement, 8000, 2, moveBonus: 10);

            // Stealth Upgrades
            CreateUpgrade("upgrade_stealth_rating_1", "Ghost Step I", "Reduce detection range by 15%", UpgradeCategory.Stealth, 10000, 3, stealthBonus: 15);
            CreateUpgrade("upgrade_suspicion_decay_1", "Cool Head I", "Suspicion decays 20% faster", UpgradeCategory.Stealth, 8000, 3, suspicionDecayBonus: 20);
            CreateUpgrade("upgrade_suspicion_build_1", "Low Profile I", "Suspicion builds 25% slower", UpgradeCategory.Stealth, 12000, 3, suspicionBuildBonus: 25);
            CreateUpgrade("upgrade_shadow_cloak_1", "Shadow Cloak I", "Near-invisibility in darkness (crouched)", UpgradeCategory.Stealth, 25000, 1, stealthBonus: 50);
            CreateUpgrade("upgrade_silent_takedown_1", "Silent Takedown I", "Non-lethal takedowns generate no noise", UpgradeCategory.Stealth, 20000, 1, stealthBonus: 30);

            // Intelligence Upgrades
            CreateUpgrade("upgrade_interaction_range_1", "Extended Reach I", "Increase interaction range by 25%", UpgradeCategory.Intelligence, 8000, 3, interactionBonus: 25);
            CreateUpgrade("upgrade_scan_range_1", "Enhanced Scanner I", "Increase scanner/photo range by 30%", UpgradeCategory.Intelligence, 10000, 3, scanBonus: 30);
            CreateUpgrade("upgrade_hack_speed_1", "Quick Hack I", "Reduce hacking/interaction time by 20%", UpgradeCategory.Intelligence, 15000, 3, hackBonus: 20);
            CreateUpgrade("upgrade_intel_gathering_1", "Information Broker I", "Mission objectives reveal additional intel", UpgradeCategory.Intelligence, 20000, 1, intelBonus: 50);

            // Utility Upgrades
            CreateUpgrade("upgrade_inventory_slots_1", "Expanded Pockets I", "Add 2 additional inventory slots", UpgradeCategory.Utility, 5000, 3, inventoryBonus: 2);
            CreateUpgrade("upgrade_starting_money_1", "Contract Advance I", "Start missions with +$5,000", UpgradeCategory.Utility, 15000, 3, moneyBonus: 5000);
            CreateUpgrade("upgrade_mission_reward_1", "Negotiator I", "Increase mission rewards by 10%", UpgradeCategory.Utility, 25000, 3, rewardBonus: 10);
            CreateUpgrade("upgrade_auto_save_1", "Auto-Save Protocol I", "Auto-save every 30 seconds instead of 60", UpgradeCategory.Utility, 10000, 1);

            // Cosmetic Upgrades
            CreateUpgrade("upgrade_outfit_tactical_1", "Tactical Outfit", "Unlock tactical combat outfit", UpgradeCategory.Cosmetic, 10000, 1);
            CreateUpgrade("upgrade_outfit_formal_1", "Formal Wear", "Unlock formal gala attire", UpgradeCategory.Cosmetic, 15000, 1);
            CreateUpgrade("upgrade_outfit_casual_1", "Casual Disguise", "Unlock casual street clothes", UpgradeCategory.Cosmetic, 8000, 1);
        }

        private static void CreateUpgrade(string id, string name, string desc, UpgradeCategory cat, int cost, int maxLevel,
            float moveBonus = 0, float sprintBonus = 0, float stealthBonus = 0, float suspicionDecayBonus = 0,
            float suspicionBuildBonus = 0, float interactionBonus = 0, float scanBonus = 0, float hackBonus = 0,
            float intelBonus = 0, int inventoryBonus = 0, int moneyBonus = 0, int rewardBonus = 0)
        {
            UpgradeData upgrade = CreateAsset<UpgradeData>($"Upgrades/{id}");
            upgrade.upgradeId = id;
            upgrade.upgradeName = name;
            upgrade.description = desc;
            upgrade.category = cat;
            upgrade.cost = cost;
            upgrade.MaxLevel = maxLevel;
            upgrade.moveSpeedBonusPerLevel = moveBonus;
            upgrade.sprintSpeedBonusPerLevel = sprintBonus;
            upgrade.stealthBonusPerLevel = stealthBonus;
            upgrade.suspicionDecayBonusPerLevel = suspicionDecayBonus;
            upgrade.interactionRangeBonusPerLevel = interactionBonus;
            upgrade.inventorySlotsBonusPerLevel = inventoryBonus;
            upgrade.startingMoneyBonusPerLevel = moneyBonus;
        }

        private static void CreateItems()
        {
            RemoveLegacyItemAssets();

            // Key Items
            CreateItem("keycard", "Security Keycard", "Grants access to secure areas and extraction points.", ItemType.KeyItem, false, 1, 0, 0);
            CreateItem("master_keycard", "Master Keycard", "Opens all keycard-locked doors in the region.", ItemType.KeyItem, false, 1, 5000, 10000);
            CreateItem("vault_code", "Vault Access Code", "Temporary code for the luxury district vault.", ItemType.KeyItem, true, 1, 0, 0);

            // Evidence
            CreateItem("documents_evidence", "Classified Documents", "Proof of corporate espionage.", ItemType.Evidence, false, 1, 0, 0);
            CreateItem("data_drive", "Encrypted Data Drive", "Contains stolen corporate secrets.", ItemType.Evidence, false, 1, 0, 0);
            CreateItem("shipping_manifest", "Shipping Manifest", "Lists all illegal weapons shipments.", ItemType.Evidence, false, 1, 0, 0);
            CreateItem("black_box", "Flight Recorder", "Records the final moments of the operation.", ItemType.Evidence, false, 1, 0, 0);
            CreateItem("star_of_seoul", "Star of Seoul Diamond", "The legendary 50-carat diamond.", ItemType.Evidence, false, 1, 500000, 0);
            CreateItem("evidence_folder", "Surveillance Photos", "Photos of the target's illegal activities.", ItemType.Evidence, false, 1, 0, 0);
            CreateItem("package_evidence", "Suspicious Package", "Unmarked package from the courier.", ItemType.Evidence, false, 1, 0, 0);

            // Consumables
            CreateItem("stamina_shot", "Stamina Shot", "Instantly restores 50% stamina.", ItemType.Consumable, true, 5, 500, 1500, staminaRestore: 50);
            CreateItem("calming_pills", "Calming Pills", "Reduces suspicion by 30%.", ItemType.Consumable, true, 3, 800, 2000, suspicionReduction: 30);
            CreateItem("smoke_grenade", "Smoke Grenade", "Creates a visual screen for escape.", ItemType.Consumable, true, 3, 1000, 3000);
            CreateItem("emp_grenade", "EMP Grenade", "Disables electronics in radius.", ItemType.Consumable, true, 2, 2000, 5000);

            // Tools
            CreateItem("lockpick_set", "Lockpick Set", "Increases lockpick success chance by 25%.", ItemType.Tool, false, 1, 3000, 8000);
            CreateItem("hacking_device", "Hacking Device", "Reduces hacking time by 40%.", ItemType.Tool, false, 1, 5000, 15000);
            CreateItem("silenced_pistol", "Silenced Pistol", "Non-lethal tranquilizer rounds only.", ItemType.Tool, false, 1, 10000, 25000);
            CreateItem("grapple_hook", "Grapple Hook", "Access elevated positions silently.", ItemType.Tool, false, 1, 8000, 20000);
        }


        private static void RemoveLegacyItemAssets()
        {
            string[] legacyGuids = AssetDatabase.FindAssets("t:ItemData", new[] { $"{AssetPath}/Upgrades" });
            foreach (string guid in legacyGuids)
            {
                AssetDatabase.DeleteAsset(AssetDatabase.GUIDToAssetPath(guid));
            }
        }

        private static void CreateItem(string id, string name, string desc, ItemType type, bool consumable, int maxStack, int sellPrice, int buyPrice,
            float suspicionReduction = 0, float staminaRestore = 0)
        {
            ItemData item = CreateAsset<ItemData>($"Items/{id}");
            item.itemId = id;
            item.displayName = name;
            item.description = desc;
            item.type = type;
            item.isConsumable = consumable;
            item.MaxStack = maxStack;
            item.sellPrice = sellPrice;
            item.buyPrice = buyPrice;
            item.suspicionReduction = suspicionReduction;
            item.staminaRestore = staminaRestore;
        }

        private static void CreateAudioLibrary()
        {
            AudioLibrary library = CreateAsset<AudioLibrary>("AudioLibrary");
            library.musicTracks = new AudioEntry[0];
            library.sfxEntries = new AudioEntry[0];
            library.ambienceEntries = new AudioEntry[0];
        }

        private static void CreateItemLibrary()
        {
            ItemLibrary library = CreateAsset<ItemLibrary>("ItemLibrary");
            string[] guids = AssetDatabase.FindAssets("t:ItemData", new[] { $"{AssetPath}/Items" });
            var entries = new System.Collections.Generic.List<ItemEntry>();
            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                ItemData item = AssetDatabase.LoadAssetAtPath<ItemData>(path);
                if (item != null && !string.IsNullOrEmpty(item.itemId))
                {
                    entries.Add(new ItemEntry { id = item.itemId, data = item });
                }
            }

            entries.Sort((a, b) => string.Compare(a.id, b.id, StringComparison.Ordinal));
            library.items = entries.ToArray();
            EditorUtility.SetDirty(library);
        }

        private static T CreateAsset<T>(string path) where T : ScriptableObject
        {
            string fullPath = $"{AssetPath}/{path}.asset";
            T asset = AssetDatabase.LoadAssetAtPath<T>(fullPath);
            if (asset == null)
            {
                asset = ScriptableObject.CreateInstance<T>();
                AssetDatabase.CreateAsset(asset, fullPath);
            }
            EditorUtility.SetDirty(asset);
            return asset;
        }
    }
}