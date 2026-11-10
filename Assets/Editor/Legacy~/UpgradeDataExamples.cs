using System;
using Contract.Progression;
using Contract.Items;
using UnityEngine;

namespace Contract.Progression
{
    // This file serves as documentation for creating UpgradeData/ItemData assets
    // Create via: Right-click > Create > CONTRACT > Upgrade Data / Item Data
    
    // ============================================================
    // UPGRADES (Create 15+ UpgradeData assets)
    // ============================================================
    
    // MOVEMENT UPGRADES
    // upgradeId: "upgrade_sprint_speed_1"
    // upgradeName: "Enhanced Sprint I"
    // description: "Increase sprint speed by 10%"
    // category: Movement
    // cost: 5000, maxLevel: 3
    // sprintSpeedBonusPerLevel: 10
    
    // upgradeId: "upgrade_sprint_speed_2"
    // upgradeName: "Enhanced Sprint II"
    // description: "Increase sprint speed by 15%"
    // category: Movement
    // cost: 15000, maxLevel: 3
    // sprintSpeedBonusPerLevel: 15
    
    // upgradeId: "upgrade_move_speed_1"
    // upgradeName: "Light Footwork I"
    // description: "Increase base movement speed by 8%"
    // category: Movement
    // cost: 8000, maxLevel: 3
    // moveSpeedBonusPerLevel: 8
    
    // upgradeId: "upgrade_crouch_speed_1"
    // upgradeName: "Silent Movement I"
    // description: "Increase crouch movement speed by 20%"
    // category: Movement
    // cost: 10000, maxLevel: 2
    // moveSpeedBonusPerLevel: 15 (applies to crouch)
    
    // upgradeId: "upgrade_stamina_1"
    // upgradeName: "Endurance Training I"
    // description: "Increase stamina regeneration by 25%"
    // category: Movement
    // cost: 12000, maxLevel: 3
    // sprintSpeedBonusPerLevel: 5 (stamina regen)
    
    // upgradeId: "upgrade_jump_height_1"
    // upgradeName: "Vertical Leap I"
    // description: "Increase jump height by 15%"
    // category: Movement
    // cost: 8000, maxLevel: 2
    // moveSpeedBonusPerLevel: 10 (jump force)
    
    // STEALTH UPGRADES
    // upgradeId: "upgrade_stealth_rating_1"
    // upgradeName: "Ghost Step I"
    // description: "Reduce detection range by 15%"
    // category: Stealth
    // cost: 10000, maxLevel: 3
    // stealthBonusPerLevel: 15
    
    // upgradeId: "upgrade_suspicion_decay_1"
    // upgradeName: "Cool Head I"
    // description: "Suspicion decays 20% faster"
    // category: Stealth
    // cost: 8000, maxLevel: 3
    // suspicionDecayBonusPerLevel: 20
    
    // upgradeId: "upgrade_suspicion_build_1"
    // upgradeName: "Low Profile I"
    // description: "Suspicion builds 25% slower"
    // category: Stealth
    // cost: 12000, maxLevel: 3
    // suspicionBuildRate: -25 (reduces build rate)
    
    // upgradeId: "upgrade_shadow_cloak_1"
    // upgradeName: "Shadow Cloak I"
    // description: "Near-invisibility in darkness (crouched)"
    // category: Stealth
    // cost: 25000, maxLevel: 1
    // stealthBonusPerLevel: 50
    
    // upgradeId: "upgrade_silent_takedown_1"
    // upgradeName: "Silent Takedown I"
    // description: "Non-lethal takedowns generate no noise"
    // category: Stealth
    // cost: 20000, maxLevel: 1
    // stealthBonusPerLevel: 30
    
    // INTELLIGENCE UPGRADES
    // upgradeId: "upgrade_interaction_range_1"
    // upgradeName: "Extended Reach I"
    // description: "Increase interaction range by 25%"
    // category: Intelligence
    // cost: 8000, maxLevel: 3
    // interactionRangeBonusPerLevel: 25
    
    // upgradeId: "upgrade_scan_range_1"
    // upgradeName: "Enhanced Scanner I"
    // description: "Increase scanner/photo range by 30%"
    // category: Intelligence
    // cost: 10000, maxLevel: 3
    // intelligence: 30 (scan range)
    
    // upgradeId: "upgrade_hack_speed_1"
    // upgradeName: "Quick Hack I"
    // description: "Reduce hacking/interaction time by 20%"
    // category: Intelligence
    // cost: 15000, maxLevel: 3
    // intelligence: 20
    
    // upgradeId: "upgrade_intel_gathering_1"
    // upgradeName: "Information Broker I"
    // description: "Mission objectives reveal additional intel"
    // category: Intelligence
    // cost: 20000, maxLevel: 1
    // intelligence: 50
    
    // UTILITY UPGRADES
    // upgradeId: "upgrade_inventory_slots_1"
    // upgradeName: "Expanded Pockets I"
    // description: "Add 2 additional inventory slots"
    // category: Utility
    // cost: 5000, maxLevel: 3
    // inventorySlotsBonusPerLevel: 2
    
    // upgradeId: "upgrade_starting_money_1"
    // upgradeName: "Contract Advance I"
    // description: "Start missions with +$5,000"
    // category: Utility
    // cost: 15000, maxLevel: 3
    // startingMoneyBonusPerLevel: 5000
    
    // upgradeId: "upgrade_mission_reward_1"
    // upgradeName: "Negotiator I"
    // description: "Increase mission rewards by 10%"
    // category: Utility
    // cost: 25000, maxLevel: 3
    // startingMoneyBonusPerLevel: 10 (reward multiplier)
    
    // upgradeId: "upgrade_auto_save_1"
    // upgradeName: "Auto-Save Protocol I"
    // description: "Auto-save every 30 seconds instead of 60"
    // category: Utility
    // cost: 10000, maxLevel: 1
    
    // COSMETIC UPGRADES
    // upgradeId: "upgrade_outfit_tactical_1"
    // upgradeName: "Tactical Outfit"
    // description: "Unlock tactical combat outfit"
    // category: Cosmetic
    // cost: 10000, maxLevel: 1
    
    // upgradeId: "upgrade_outfit_formal_1"
    // upgradeName: "Formal Wear"
    // description: "Unlock formal gala attire"
    // category: Cosmetic
    // cost: 15000, maxLevel: 1
    
    // upgradeId: "upgrade_outfit_casual_1"
    // upgradeName: "Casual Disguise"
    // description: "Unlock casual street clothes"
    // category: Cosmetic
    // cost: 8000, maxLevel: 1
}

namespace Contract.Items
{
    // ============================================================
    // ITEMS (Create 15+ ItemData assets)
    // ============================================================
    
    // KEY ITEMS
    // itemId: "keycard"
    // displayName: "Security Keycard"
    // description: "Grants access to secure areas and extraction points."
    // type: KeyItem, isConsumable: false, maxStack: 1
    // sellPrice: 0, buyPrice: 0
    // suspicionReduction: 0, staminaRestore: 0
    
    // itemId: "master_keycard"
    // displayName: "Master Keycard"
    // description: "Opens all keycard-locked doors in the region."
    // type: KeyItem, isConsumable: false, maxStack: 1
    // sellPrice: 5000, buyPrice: 10000
    
    // itemId: "vault_code"
    // displayName: "Vault Access Code"
    // description: "Temporary code for the luxury district vault."
    // type: KeyItem, isConsumable: true, maxStack: 1
    // sellPrice: 0, buyPrice: 0
    
    // EVIDENCE
    // itemId: "documents_evidence"
    // displayName: "Classified Documents"
    // description: "Proof of corporate espionage."
    // type: Evidence, isConsumable: false, maxStack: 1
    // sellPrice: 0, buyPrice: 0
    
    // itemId: "data_drive"
    // displayName: "Encrypted Data Drive"
    // description: "Contains stolen corporate secrets."
    // type: Evidence, isConsumable: false, maxStack: 1
    // sellPrice: 0, buyPrice: 0
    
    // itemId: "shipping_manifest"
    // displayName: "Shipping Manifest"
    // description: "Lists all illegal weapons shipments."
    // type: Evidence, isConsumable: false, maxStack: 1
    // sellPrice: 0, buyPrice: 0
    
    // itemId: "black_box"
    // displayName: "Flight Recorder"
    // description: "Records the final moments of the operation."
    // type: Evidence, isConsumable: false, maxStack: 1
    // sellPrice: 0, buyPrice: 0
    
    // itemId: "star_of_seoul"
    // displayName: "Star of Seoul Diamond"
    // description: "The legendary 50-carat diamond."
    // type: Evidence, isConsumable: false, maxStack: 1
    // sellPrice: 500000, buyPrice: 0
    
    // itemId: "evidence_folder"
    // displayName: "Surveillance Photos"
    // description: "Photos of the target's illegal activities."
    // type: Evidence, isConsumable: false, maxStack: 1
    // sellPrice: 0, buyPrice: 0
    
    // itemId: "package_evidence"
    // displayName: "Suspicious Package"
    // description: "Unmarked package from the courier."
    // type: Evidence, isConsumable: false, maxStack: 1
    // sellPrice: 0, buyPrice: 0
    
    // CONSUMABLES
    // itemId: "stamina_shot"
    // displayName: "Stamina Shot"
    // description: "Instantly restores 50% stamina."
    // type: Consumable, isConsumable: true, maxStack: 5
    // sellPrice: 500, buyPrice: 1500
    // staminaRestore: 50
    
    // itemId: "calming_pills"
    // displayName: "Calming Pills"
    // description: "Reduces suspicion by 30%."
    // type: Consumable, isConsumable: true, maxStack: 3
    // sellPrice: 800, buyPrice: 2000
    // suspicionReduction: 30
    
    // itemId: "smoke_grenade"
    // displayName: "Smoke Grenade"
    // description: "Creates a visual screen for escape."
    // type: Consumable, isConsumable: true, maxStack: 3
    // sellPrice: 1000, buyPrice: 3000
    
    // itemId: "emp_grenade"
    // displayName: "EMP Grenade"
    // description: "Disables electronics in radius."
    // type: Consumable, isConsumable: true, maxStack: 2
    // sellPrice: 2000, buyPrice: 5000
    
    // TOOLS
    // itemId: "lockpick_set"
    // displayName: "Lockpick Set"
    // description: "Increases lockpick success chance by 25%."
    // type: Tool, isConsumable: false, maxStack: 1
    // sellPrice: 3000, buyPrice: 8000
    
    // itemId: "hacking_device"
    // displayName: "Hacking Device"
    // description: "Reduces hacking time by 40%."
    // type: Tool, isConsumable: false, maxStack: 1
    // sellPrice: 5000, buyPrice: 15000
    
    // itemId: "silenced_pistol"
    // displayName: "Silenced Pistol"
    // description: "Non-lethal tranquilizer rounds only."
    // type: Tool, isConsumable: false, maxStack: 1
    // sellPrice: 10000, buyPrice: 25000
    
    // itemId: "grapple_hook"
    // displayName: "Grapple Hook"
    // description: "Access elevated positions silently."
    // type: Tool, isConsumable: false, maxStack: 1
    // sellPrice: 8000, buyPrice: 20000
}