using System;
using Contract.Missions;
using Contract.Regions;
using UnityEngine;

namespace Contract.Missions
{
    // This file serves as documentation for creating MissionData/RegionData assets
    // Create via: Right-click > Create > CONTRACT > Mission Data / Region Data
    
    // ============================================================
    // REGIONS (Create 5 RegionData assets)
    // ============================================================
    
    // REGION 1: Downtown City
    // regionId: "region_downtown"
    // regionName: "Downtown Seoul"
    // description: "Neon-lit business district. High-security corporate towers and crowded streets."
    // sceneName: "GameWorld"
    // atmosphereColor: Dark blue (0.05, 0.07, 0.12)
    // unlockedByDefault: true
    // missionIds: ["mission_downtown_01", "mission_downtown_02", "mission_downtown_03"]
    // safehouseId: "safehouse_downtown"
    // extractionId: "extraction_downtown"
    // civilianNPCCount: 15, missionNPCCount: 3
    // centerPosition: (0, 0, 0), regionSize: (120, 20, 120)
    
    // REGION 2: Industrial District
    // regionId: "region_industrial"
    // regionName: "Industrial District"
    // description: "Factories, warehouses, and shipping yards. Heavy machinery provides cover."
    // sceneName: "GameWorld"
    // atmosphereColor: Steel gray (0.1, 0.1, 0.12)
    // unlockedByDefault: false, requiredRegionIds: ["region_downtown"], unlockCost: 25000
    // missionIds: ["mission_industrial_01", "mission_industrial_02"]
    // safehouseId: "safehouse_industrial"
    // extractionId: "extraction_industrial"
    // civilianNPCCount: 8, missionNPCCount: 2
    // centerPosition: (200, 0, 0), regionSize: (100, 20, 100)
    
    // REGION 3: Residential District
    // regionId: "region_residential"
    // regionName: "Residential District"
    // description: "Apartment complexes and quiet streets. Civilians are more observant at night."
    // sceneName: "GameWorld"
    // atmosphereColor: Warm dark (0.12, 0.08, 0.06)
    // unlockedByDefault: false, requiredRegionIds: ["region_downtown"], unlockCost: 50000
    // missionIds: ["mission_residential_01", "mission_residential_02", "mission_residential_03"]
    // safehouseId: "safehouse_residential"
    // extractionId: "extraction_residential"
    // civilianNPCCount: 20, missionNPCCount: 2
    // centerPosition: (-200, 0, 0), regionSize: (120, 20, 120)
    
    // REGION 4: Luxury District (Gangnam-inspired)
    // regionId: "region_luxury"
    // regionName: "Luxury District"
    // description: "High-end boutiques, clubs, and penthouses. Heavy security, high-value targets."
    // sceneName: "GameWorld"
    // atmosphereColor: Gold tint (0.15, 0.12, 0.05)
    // unlockedByDefault: false, requiredRegionIds: ["region_industrial", "region_residential"], unlockCost: 100000
    // missionIds: ["mission_luxury_01", "mission_luxury_02"]
    // safehouseId: "safehouse_luxury"
    // extractionId: "extraction_luxury"
    // civilianNPCCount: 10, missionNPCCount: 3
    // centerPosition: (0, 0, 200), regionSize: (100, 20, 100)
    
    // REGION 5: Harbor District
    // regionId: "region_harbor"
    // regionName: "Harbor District"
    // description: "Shipping containers, docks, and warehouses. Final extraction point for the campaign."
    // sceneName: "GameWorld"
    // atmosphereColor: Cold blue (0.05, 0.08, 0.15)
    // unlockedByDefault: false, requiredRegionIds: ["region_luxury"], unlockCost: 200000
    // missionIds: ["mission_harbor_01", "mission_harbor_02", "mission_harbor_03"]
    // safehouseId: "safehouse_harbor"
    // extractionId: "extraction_harbor"
    // civilianNPCCount: 5, missionNPCCount: 4
    // centerPosition: (0, 0, -200), regionSize: (150, 20, 150)
    
    // ============================================================
    // MISSIONS (Create 12+ MissionData assets)
    // ============================================================
    
    // MISSION 1: Downtown - The Missing Documents
    // missionId: "mission_downtown_01"
    // missionName: "The Missing Documents"
    // description: "Retrieve classified documents from a corporate office before they're destroyed."
    // regionId: "region_downtown"
    // subjectId: "subject_ceo_kim"
    // subjectName: "CEO Kim Min-jun"
    // rewardMoney: 15000, rewardXP: 100
    // difficulty: 1, timeLimitMinutes: 15
    // objectives:
    //   1. ReachLocation: "Find the corporate office" (targetLocationId: "office_building")
    //   2. Interact: "Access the secure terminal" (targetId: "secure_terminal")
    //   3. CollectItem: "Retrieve the documents" (targetId: "documents_evidence")
    //   4. Extract: "Reach extraction point" (targetLocationId: "extraction_downtown")
    
    // MISSION 2: Downtown - Silent Exchange
    // missionId: "mission_downtown_02"
    // missionName: "Silent Exchange"
    // description: "Intercept a data handoff between two couriers in a crowded plaza."
    // regionId: "region_downtown"
    // subjectId: "subject_courier_park"
    // subjectName: "Courier Park Soo-min"
    // rewardMoney: 25000, rewardXP: 150
    // difficulty: 2, timeLimitMinutes: 10
    // objectives:
    //   1. IdentifySubject: "Identify the courier" (targetId: "courier_park")
    //   2. PhotographSubject: "Photograph the exchange" (targetId: "courier_park")
    //   3. CollectItem: "Retrieve the data drive" (targetId: "data_drive")
    //   4. Extract: "Reach extraction point" (targetLocationId: "extraction_downtown")
    
    // MISSION 3: Downtown - Corporate Espionage
    // missionId: "mission_downtown_03"
    // missionName: "Corporate Espionage"
    // description: "Infiltrate the rival company's server room and plant surveillance software."
    // regionId: "region_downtown"
    // subjectId: "subject_cto_lee"
    // subjectName: "CTO Lee Jin-woo"
    // rewardMoney: 40000, rewardXP: 200
    // difficulty: 3, timeLimitMinutes: 20
    // objectives:
    //   1. ReachLocation: "Enter the server room" (targetLocationId: "server_room")
    //   2. Interact: "Plant the surveillance software" (targetId: "server_rack")
    //   3. AvoidDetection: "Leave without triggering alarms"
    //   4. Extract: "Reach extraction point" (targetLocationId: "extraction_downtown")
    
    // MISSION 4: Industrial - Warehouse Heist
    // missionId: "mission_industrial_01"
    // missionName: "Warehouse Heist"
    // description: "Steal prototype tech from a heavily guarded warehouse."
    // regionId: "region_industrial"
    // subjectId: "subject_guard_captain"
    // subjectName: "Captain Han Dae-sung"
    // rewardMoney: 35000, rewardXP: 180
    // difficulty: 2, timeLimitMinutes: 15
    // objectives:
    //   1. ReachLocation: "Enter the warehouse" (targetLocationId: "warehouse_main")
    //   2. IdentifySubject: "Locate the guard captain" (targetId: "guard_captain")
    //   3. CollectItem: "Steal the prototype" (targetId: "prototype_tech")
    //   4. Extract: "Reach extraction point" (targetLocationId: "extraction_industrial")
    
    // MISSION 5: Industrial - Smuggler's Trail
    // missionId: "mission_industrial_02"
    // missionName: "Smuggler's Trail"
    // description: "Track a weapons smuggler through the shipping yards."
    // regionId: "region_industrial"
    // subjectId: "subject_smuggler_choi"
    // subjectName: "Choi Min-ho"
    // rewardMoney: 50000, rewardXP: 250
    // difficulty: 3, timeLimitMinutes: 18
    // objectives:
    //   1. ReachLocation: "Find the shipping container" (targetLocationId: "container_yard")
    //   2. Interact: "Hack the manifest terminal" (targetId: "manifest_terminal")
    //   3. EscortSubject: "Escort the informant" (targetId: "informant_kim")
    //   4. Extract: "Reach extraction point" (targetLocationId: "extraction_industrial")
    
    // MISSION 6: Residential - Neighborhood Watch
    // missionId: "mission_residential_01"
    // missionName: "Neighborhood Watch"
    // description: "Investigate a suspicious resident without alerting the community."
    // regionId: "region_residential"
    // subjectId: "subject_teacher_yoon"
    // subjectName: "Yoon Ji-hye"
    // rewardMoney: 30000, rewardXP: 160
    // difficulty: 2, timeLimitMinutes: 20
    // objectives:
    //   1. ReachLocation: "Enter the apartment complex" (targetLocationId: "apartment_complex")
    //   2. IdentifySubject: "Locate the target's unit" (targetId: "unit_404")
    //   3. PhotographSubject: "Gather evidence" (targetId: "evidence_folder")
    //   4. Extract: "Reach extraction point" (targetLocationId: "extraction_residential")
    
    // MISSION 7: Residential - Midnight Courier
    // missionId: "mission_residential_02"
    // missionName: "Midnight Courier"
    // description: "Intercept a package delivery in a quiet neighborhood."
    // regionId: "region_residential"
    // subjectId: "subject_delivery_kang"
    // subjectName: "Kang Seo-joon"
    // rewardMoney: 45000, rewardXP: 220
    // difficulty: 3, timeLimitMinutes: 12
    // objectives:
    //   1. ReachLocation: "Stake out the drop point" (targetLocationId: "park_bench")
    //   2. IdentifySubject: "Confirm the courier" (targetId: "courier_kang")
    //   3. CollectItem: "Retrieve the package" (targetId: "package_evidence")
    //   4. Extract: "Reach extraction point" (targetLocationId: "extraction_residential")
    
    // MISSION 8: Residential - Safe House Compromise
    // missionId: "mission_residential_03"
    // missionName: "Safe House Compromise"
    // description: "Your safe house has been discovered. Extract with the evidence before cleanup crew arrives."
    // regionId: "region_residential"
    // subjectId: "subject_cleaner_park"
    // subjectName: "Park Cleaner Team"
    // rewardMoney: 60000, rewardXP: 300
    // difficulty: 4, timeLimitMinutes: 8
    // objectives:
    //   1. CollectItem: "Gather all evidence" (targetId: "evidence_cache")
    //   2. AvoidDetection: "Evade the cleanup crew"
    //   3. Extract: "Reach extraction point" (targetLocationId: "extraction_residential")
    
    // MISSION 9: Luxury - High Society
    // missionId: "mission_luxury_01"
    // missionName: "High Society"
    // description: "Infiltrate an exclusive gala to bug a crime lord's penthouse."
    // regionId: "region_luxury"
    // subjectId: "subject_crime_lord_jung"
    // subjectName: "Jung Tae-sik"
    // rewardMoney: 75000, rewardXP: 400
    // difficulty: 4, timeLimitMinutes: 25
    // objectives:
    //   1. ReachLocation: "Enter the gala" (targetLocationId: "gala_venue")
    //   2. IdentifySubject: "Locate the crime lord" (targetId: "jung_taesik")
    //   3. Interact: "Plant the listening device" (targetId: "penthouse_office")
    //   4. Extract: "Reach extraction point" (targetLocationId: "extraction_luxury")
    
    // MISSION 10: Luxury - Diamond Heist
    // missionId: "mission_luxury_02"
    // missionName: "Diamond Heist"
    // description: "Steal the 'Star of Seoul' diamond from a high-security vault."
    // regionId: "region_luxury"
    // subjectId: "subject_curator_han"
    // subjectName: "Han Mi-kyung"
    // rewardMoney: 100000, rewardXP: 500
    // difficulty: 5, timeLimitMinutes: 30
    // objectives:
    //   1. ReachLocation: "Enter the museum" (targetLocationId: "museum_vault")
    //   2. Interact: "Disable the laser grid" (targetId: "laser_control")
    //   3. CollectItem: "Steal the diamond" (targetId: "star_of_seoul")
    //   4. AvoidDetection: "Escape without triggering alarms"
    //   5. Extract: "Reach extraction point" (targetLocationId: "extraction_luxury")
    
    // MISSION 11: Harbor - Final Shipment
    // missionId: "mission_harbor_01"
    // missionName: "Final Shipment"
    // description: "Stop the final weapons shipment before it leaves the harbor."
    // regionId: "region_harbor"
    // subjectId: "subject_captain_lee"
    // subjectName: "Captain Lee Chang-ho"
    // rewardMoney: 125000, rewardXP: 600
    // difficulty: 5, timeLimitMinutes: 20
    // objectives:
    //   1. ReachLocation: "Board the cargo ship" (targetLocationId: "cargo_ship")
    //   2. IdentifySubject: "Locate the captain" (targetId: "captain_lee")
    //   3. Interact: "Disable the ship's engines" (targetId: "engine_room")
    //   4. CollectItem: "Seize the manifest" (targetId: "shipping_manifest")
    //   5. Extract: "Reach extraction point" (targetLocationId: "extraction_harbor")
    
    // MISSION 12: Harbor - Endgame
    // missionId: "mission_harbor_02"
    // missionName: "Endgame"
    // description: "Confront the mastermind behind the conspiracy. Final extraction."
    // regionId: "region_harbor"
    // subjectId: "subject_mastermind"
    // subjectName: "The Director"
    // rewardMoney: 200000, rewardXP: 1000
    // difficulty: 5, timeLimitMinutes: 30
    // objectives:
    //   1. ReachLocation: "Reach the command center" (targetLocationId: "command_center")
    //   2. IdentifySubject: "Confront the Director" (targetId: "director")
    //   3. Interact: "Upload the evidence" (targetId: "evidence_server")
    //   4. Extract: "Final extraction" (targetLocationId: "extraction_harbor")
    
    // MISSION 13: Harbor - Cleanup
    // missionId: "mission_harbor_03"
    // missionName: "Cleanup"
    // description: "Eliminate remaining loose ends. Secure the region permanently."
    // regionId: "region_harbor"
    // subjectId: "subject_lieutenant_kim"
    // subjectName: "Lieutenant Kim"
    // rewardMoney: 150000, rewardXP: 750
    // difficulty: 4, timeLimitMinutes: 15
    // objectives:
    //   1. ReachLocation: "Clear the warehouse district" (targetLocationId: "warehouse_district")
    //   2. IdentifySubject: "Locate the lieutenant" (targetId: "lieutenant_kim")
    //   3. CollectItem: "Secure the black box" (targetId: "black_box")
    //   4. Extract: "Final extraction" (targetLocationId: "extraction_harbor")
}