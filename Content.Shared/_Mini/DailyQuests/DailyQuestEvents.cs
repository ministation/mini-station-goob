// SPDX-FileCopyrightText: 2026 Casha
// Мини-станция/Freaky-station, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/ministation/mini-station-goob/master/LICENSE.TXT
namespace Content.Shared._Mini.DailyQuests;

/// <summary>
/// Raised on paper when it is successfully stamped.
/// </summary>
[ByRefEvent]
public readonly record struct PaperStampedEvent(EntityUid User);

/// <summary>
/// Raised when a player successfully heals another entity with a medical item.
/// </summary>
[ByRefEvent]
public readonly record struct MedicalHealAppliedEvent(EntityUid User, EntityUid Target);

/// <summary>
/// Raised when a hypospray successfully injects another entity.
/// </summary>
[ByRefEvent]
public readonly record struct HyposprayPatientInjectedEvent(EntityUid User, EntityUid Target);

/// <summary>
/// Raised when a player successfully cuffs someone.
/// </summary>
[ByRefEvent]
public readonly record struct PlayerCuffedEvent(EntityUid User);

/// <summary>
/// Raised when a salvage magnet offer is claimed.
/// </summary>
[ByRefEvent]
public readonly record struct SalvageMagnetClaimedEvent(EntityUid Actor);

/// <summary>
/// Raised when a forensic scanner finishes scanning a target.
/// </summary>
[ByRefEvent]
public readonly record struct ForensicScanCompletedEvent(EntityUid User);

/// <summary>
/// Raised when a harvestable plant is collected.
/// </summary>
[ByRefEvent]
public readonly record struct PlantHarvestedEvent(EntityUid User);

/// <summary>
/// Raised when a floor cleaner finishes cleaning decals or footprints.
/// </summary>
[ByRefEvent]
public readonly record struct FloorCleanedEvent(EntityUid User, int Count);

/// <summary>
/// Raised when a research console unlocks a technology.
/// </summary>
[ByRefEvent]
public readonly record struct TechnologyUnlockedEvent(EntityUid Actor);

/// <summary>
/// Raised when a microwave finishes producing a meal.
/// </summary>
[ByRefEvent]
public readonly record struct MicrowaveMealProducedEvent(EntityUid Microwave, EntityUid? User);

/// <summary>
/// Raised when a lathe finishes producing an item.
/// </summary>
[ByRefEvent]
public readonly record struct LatheItemProducedEvent(EntityUid Lathe, EntityUid? User);

/// <summary>
/// Raised when a cargo bounty label is printed.
/// </summary>
[ByRefEvent]
public readonly record struct CargoBountyLabelPrintedEvent(EntityUid Actor);

/// <summary>
/// Raised when a cargo bounty is fulfilled by selling a matching crate.
/// </summary>
[ByRefEvent]
public readonly record struct CargoBountyFulfilledEvent(EntityUid Station);

/// <summary>
/// Raised when a player claims mining points from a mining lathe.
/// </summary>
[ByRefEvent]
public readonly record struct MiningPointsClaimedEvent(EntityUid Actor);

/// <summary>
/// Raised when a player voluntarily performs an emote.
/// </summary>
[ByRefEvent]
public readonly record struct EmotePerformedEvent(EntityUid User);

/// <summary>
/// Raised when a welder successfully completes a tool do-after.
/// </summary>
[ByRefEvent]
public readonly record struct StructureWeldedEvent(EntityUid User);
