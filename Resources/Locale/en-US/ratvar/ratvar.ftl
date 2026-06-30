# SPDX-FileCopyrightText: 2024 MACMAN2003 <macman2003c@gmail.com>
# SPDX-FileCopyrightText: 2025 Aiden <28298836+Aidenkrz@users.noreply.github.com>
#
# SPDX-License-Identifier: MIT

ratvar-has-risen = RATVAR HAS AWOKEN
ratvar-has-risen-sender = ???

ratvar-spawn-start =
    Ratvar's righteous are about to free him from his prison!
    Do not allow this at any cost!
    The coordinates are { $position }
ratvar-spawn-end = Bring me Nar'Sie!
ratvar-name = Ratvar

ratvar-winstate-idle = [color=green]The Ratvar righteous failed to free their master![/color]
ratvar-winstate-summoning = [color=yellow]The Ratvar righteous did not free their master in time![/color]
ratvar-winstate-righteouswon = [color=crimson]The Ratvar righteous freed their master! All hail Ratvar![/color]

ratvar-righteous-briefing = You are one of Ratvar's righteous. Complete all tasks and release Ratvar from his prison. Glory to the clockwork!
ratvar-righteous-message = Welcome to the Ratvar righteous! Remember, your cult is a peaceful one and does not engage in slaughter!

chat-radio-ratvar = Ratvar
tool-quality-ratvar-screwing-name = Ratvar Screwing
tool-quality-ratvar-screwing-tool-name = Ratvar Screwing Tool
tool-quality-ratvar-anchoring-name = Ratvar Anchoring
tool-quality-ratvar-anchoring-tool-name = Ratvar Anchoring Tool

fibers-brass = brass fibers
objective-issuer-ratvar = Ratvar

ratvar-roundend-win = The Cult of Ratvar has triumphed! Ratvar rises from the depths!
ratvar-roundend-loss = The Cult of Ratvar has failed. The darkness recedes.
ratvar-roundend-stats-1 = There were { $righteousCount } [color=#b87333]Righteous of Ratvar[/color]
ratvar-roundend-stats-2 = [color=#b87333]Righteous[/color] placed { $beaconCount } beacons
ratvar-roundend-stats-3 = [color=#b87333]Righteous[/color] accumulated { $power } units of power

# Weapons
ent-RatvarSword = Rustless Sword
ent-RatvarSpear = Ratvar's Spear
ent-RatvarSlab = Clockwork Slab
    .desc = Tool of Ratvar's righteous.
ent-RatvarShield = Brass Shield
ent-RatvarHammer = Clockwork Hammer
ent-RatvarSwordsSwordsman = Swordsman
    .desc = Speeds up your strikes with this sword for 9 seconds, but changes damage to 7.
ent-RatvarSwordBloodshed = Bloodshed
    .desc = Increases blood loss by 100 units after striking with the sword.
ent-RatvarSpearElectricalTouch = Electric Shock
    .desc = On hit (must be held in two hands) emits a weak EMP to humanoids and a strong EMP to cyborgs and mechs.
ent-RatvarSpearConfusion = Confusion
    .desc = After striking (must be held in two hands), your target cannot walk in a straight line for 15 seconds.
ent-RatvarHammmerKnockOff = Knockback
    .desc = After hitting (must be held in two hands), knock the enemy back a great distance.

# Weapon enchantments
enchant-name-RatvarSword-0 = Swordsman
enchant-name-RatvarSword-1 = Bloodshed
enchant-name-RatvarSpear-0 = Electric Shock
enchant-name-RatvarSpear-1 = Confusion
enchant-name-RatvarSlab-0 = Stun
enchant-name-RatvarSlab-1 = Create Passage
enchant-name-RatvarSlab-2 = Terraform
enchant-name-RatvarSlab-3 = Teleport
enchant-name-RatvarSlab-4 = Hide Machinery
enchant-name-RatvarShard-0 = Reconstruction
enchant-name-RatvarShard-1 = Electromagnetic Pulse
enchant-name-RatvarHammer-0 = Knockback

# Weapon actions
action-name-RatvarSlabStun = Stun
action-desc-RatvarSlabStun = Stuns the target
action-name-RatvarSlabDoors = Create Passage
action-desc-RatvarSlabDoors = Opens doors and lockers
action-name-RatvarSlabWalls = Terraform
action-desc-RatvarSlabWalls = When used on a NORMAL wall, turns it into a false wall.
action-name-RatvarSlabTeleport = Teleport
action-desc-RatvarSlabTeleport = Teleport to the altar or a visible area
action-name-RatvarSlabHidings = Hide Machinery
action-desc-RatvarSlabHidings = Masks Ratvarian constructions as bushes and lockers

# Armor
ent-ClothingOuterRobesRatvar = Righteous Robe
    .desc = Unremarkable robes.
ent-ClothingOuterCuirassRatvar = Cuirass
    .desc = Armor that protects from fire. (not really)
ent-ClothingHandsRatvar = Copper Gauntlets
    .desc = Heavy copper gauntlets.
ent-ClothingHeadHelmetRatvar = Brass Helmet
    .desc = It's probably heavy...
ent-ClothingShoesRatvar = Treads
    .desc = Heavy boots.

# Items
ent-RatvarIntegrationCog = Integration Cog
    .desc = Tool of Ratvar's righteous.
ent-RatvarShard = Strange Shard
    .desc = Single-use crystal.
ent-RatvarSoulVessel = Soul Vessel
    .desc = A container blessed by Ratvar, holding a righteous soul.
ent-RatvarShardReconstruct = Reconstruction
    .desc = In a 4-tile radius, converts all cyborgs into brass cyborgs, and changes walls and floors to brass versions.
ent-RatvarShardEmp = Electromagnetic Pulse
    .desc = In a 4-tile radius, emits a strong EMP. In a 6-tile radius, a weak EMP.

# Mobs
ent-MobMouseRatvar = Brass Mouse
    .desc = Squeak!
ent-MobRatvarMarauder = Clockwork Marauder
    .desc = Smells of brass.
ent-MobRatvarDark = Ratvar
ent-MobRatvarCyborg = Brass Cyborg
    .desc = Watching a brass cyborg in motion is a delight to the eyes.
ent-RatvarBorgWeaponModule = Ratvar Spear Module

# Ghost Roles
ghost-role-name-RatvarSoulVessel = Soul Vessel
ghost-role-desc-RatvarSoulVessel = Take the soul vessel to later become a Clockwork Marauder.
ghost-role-rules-RatvarSoulVessel = Obey and follow the Ratvar righteous!
ghost-role-name-RatvarDark = Ratvar
ghost-role-desc-RatvarDark = The god freed from imprisonment.
ghost-role-rules-RatvarDark = Kill everyone except the righteous, show your wrath!

# Structures
ent-RatvarWorkshop = Ratvar's Workshop
ent-RatvarPortal = Ratvar's Portal
    .desc = Ratvar's arrival is imminent...
ent-RatvarBeacon = Herald's Beacon
ent-RatvarAltar = Gear Altar
    .desc = A strange brass platform of rotating gears. It demands something in exchange for...

# Craft
craft-category-RatvarWeapon = Weapons
craft-category-RatvarArmor = Armor
craft-category-RatvarOthers = Consumables
craft-category-RatvarConstruction = Cult of Ratvar

craft-recipe-RatvarSlab = Clockwork Slab
craft-recipe-RatvarSpear = Ratvar's Spear
craft-recipe-RatvarHammer = Clockwork Hammer
craft-recipe-RatvarSword = Rustless Sword
craft-recipe-RatvarShield = Brass Shield
craft-recipe-RatvarClockRobe = Righteous Robe
craft-recipe-RatvarCuirass = Cuirass
craft-recipe-RatvarGauntlets = Gauntlets
craft-recipe-RatvarTreads = Treads
craft-recipe-RatvarHelmet = Helmet
craft-recipe-RatvarIntegrationCog = Integration Cog
craft-recipe-RatvarSoulVessel = Soul Vessel
craft-recipe-RatvarMarauder = Clockwork Marauder Shell
craft-recipe-RatvarStrangeShard = Strange Shard

# Midas Touch items
ent-MidasTouchClockworkSlab = Clockwork Slab
ent-MidasTouchBrass = Integration Cog

# Actions (from actions.yml)
ent-RatvarMidasTouch = Midas' Hand
    .desc = Midas' Hand is the first and foremost spell granted by the Lightbearer to the righteous.
ent-RatvarClockMagic = Enchant Item
    .desc = Allows you to choose an enchantment for an item.
ent-ActionRatvarSlabStun = Stun
    .desc = Stuns the target
ent-ActionRatvarSlabDoors = Create Passage
    .desc = Opens doors and lockers
ent-ActionRatvarSlabWalls = Terraform
    .desc = When used on a NORMAL wall, turns it into a false wall.
ent-ActionRatvarSlabTeleport = Teleport
    .desc = Teleport to the altar or a visible area
ent-ActionRatvarSlabHidings = Hide Machinery
    .desc = Masks Ratvarian constructions as bushes and lockers

action-name-ratvar-midas-touch = Midas' Hand
action-desc-ratvar-midas-touch = Midas' Hand is the first and foremost spell granted by the Lightbearer to the righteous.
action-name-ratvar-clock-magic = Enchant Item
action-desc-ratvar-clock-magic = Allows you to choose an enchantment for an item.

# Objectives
objective-description-RatvarConvertObjective = Use the Altar to convert a crew member.
objective-description-RatvarBeaconsObjective = Build beacons to gain as much power as possible.
objective-description-RatvarPowerObjective = Accumulate power so we have the strength to summon Ratvar!
objective-description-RatvarSummonObjective = We are ready to summon Ratvar!

# Antags
antag-name-RatvarRighteous = Ratvar Righteous
antag-objective-RatvarRighteous = Free Ratvar from imprisonment.

# Mind roles
ent-MindRoleRatvar = Ratvar Cultist Role
mind-role-name-ratvar = Ratvar Cultist Role

# Enchantments table labels
enchantment-table-name-ratvar-sword = Sword Enchantments
enchantment-table-name-ratvar-spear = Spear Enchantments
enchantment-table-name-ratvar-slab = Slab Enchantments
enchantment-table-name-ratvar-shard = Shard Enchantments
enchantment-table-name-ratvar-hammer = Hammer Enchantments
