// SPDX-FileCopyrightText: 2026 Space Station 14 Contributors
//
// SPDX-License-Identifier: LicenseRef-MiniStation-1.0

// SPDX-FileCopyrightText: 2026 Mini Station
// SPDX-License-Identifier: LicenseRef-MiniStation-1.0

using Robust.Shared.Prototypes;

using Content.Shared._Mini.DailyQuests;

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
    /// Scores per fisher mob. Lives on the rule so it is dropped with the rule entity.
    /// </summary>
    public Dictionary<EntityUid, FishingModePlayerScore> Scores = new();

    public float HudTimer;

    /// <summary>
    /// Last announced leader, so leader changes are announced once.
    /// </summary>
    public EntityUid? Leader;

    /// <summary>
    /// Round end coin rewards are paid once per round.
    /// </summary>
    public bool RewardsPaid;
}

public struct FishingModePlayerScore
{
    public int Fish;
    public int Score;
    public string? BestFish;
    public DailyQuestRarity BestFishRarity;
}
