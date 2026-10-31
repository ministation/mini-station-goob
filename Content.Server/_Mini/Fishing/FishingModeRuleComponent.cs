// SPDX-FileCopyrightText: 2026 Space Station 14 Contributors
//
// SPDX-License-Identifier: AGPL-3.0-or-later

// SPDX-FileCopyrightText: 2026 Mini Station
// Мини-станция/Freaky-station, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/ministation/mini-station-goob/master/LICENSE.TXT

using Robust.Shared.Prototypes;

using Content.Shared._Mini.DailyQuests;
using Content.Shared._Mini.Fishing.Prototypes;

namespace Content.Server._Mini.Fishing;

/// <summary>
/// Fishing mode: a peaceful round where everyone fishes. Antags are off because the
/// game preset only adds this rule and no antag schedulers.
/// </summary>
[RegisterComponent]
public sealed partial class FishingModeRuleComponent : Component
{
    /// <summary>
    /// How many water tiles to place on station plating.
    /// </summary>
    [DataField]
    public int WaterPools = 8;

    [DataField]
    public EntProtoId Rod = "FishingRod";

    [DataField]
    public List<EntProtoId> Baits = new();

    [DataField]
    public float HudIntervalSeconds = 1f;

    /// <summary>
    /// Only these fishing sources work in the mode: toilets, vending machines,
    /// hydroponics trays and aquariums lose their FishingSource on rule start.
    /// </summary>
    [DataField]
    public List<ProtoId<FishingSourcePrototype>> AllowedSources = new();

    /// <summary>
    /// Scores per fisher mob. Lives on the rule so it is dropped with the rule entity.
    /// </summary>
    public Dictionary<EntityUid, FishingModePlayerScore> Scores = new();

    public float HudTimer;

    /// <summary>
    /// Last announced leader, so leader changes are announced once.
    /// </summary>
    public EntityUid? Leader;
}

public struct FishingModePlayerScore
{
    public int Fish;
    public int Score;
    public string? BestFish;
    public DailyQuestRarity BestFishRarity;
}
