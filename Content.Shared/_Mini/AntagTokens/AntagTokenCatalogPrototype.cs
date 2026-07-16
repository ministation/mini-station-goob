// SPDX-FileCopyrightText: 2026 Casha
// Мини-станция/Freaky-station, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/ministation/mini-station-goob/master/LICENSE.TXT

using System.Collections.Generic;
using Content.Shared.Humanoid.Prototypes;
using Content.Shared.Roles;
using Robust.Shared.Prototypes;

namespace Content.Shared._Mini.AntagTokens;

[DataDefinition]
public sealed partial class AntagTokenListingEntry
{
    [DataField(required: true)]
    public string Id = default!;

    [DataField(required: true)]
    public string NameLocKey = default!;

    [DataField(required: true)]
    public string DescriptionLocKey = default!;

    [DataField(required: true)]
    public int Cost;

    [DataField(required: true)]
    public string IconPath = default!;

    [DataField(required: true)]
    public AntagPurchaseMode Mode;

    [DataField]
    public string? AntagId;

    [DataField]
    public string? GameRuleId;

    [DataField]
    public string? TagLocKey;

    [DataField]
    public int MinimumPlayers;

    [DataField]
    public bool RequiresInRound;

    [DataField]
    public bool RequiresPreRoundLobby;

    [DataField]
    public int MinimumTimeFromRoundStart;

    [DataField]
    public string? UnavailableReasonLocKey;

    [DataField]
    public string? GhostRulesLocKey;

    [DataField]
    public string? GhostAutoJoinEntityProto;

    [DataField]
    public int? FreeMinimumSponsorLevel;

    [DataField]
    public List<ProtoId<JobPrototype>>? JobBlacklist;

    [DataField]
    public List<ProtoId<SpeciesPrototype>>? SpeciesBlacklist;

    [DataField]
    public string? RequiresPresetGameRuleId;

    /// <summary>
    /// When true, station job / department gates are skipped (off-station or separate entity antags).
    /// </summary>
    [DataField]
    public bool IgnoresStationJob;

    /// <summary>Game rule used when a Dual listing resolves to GhostRule (defaults to <see cref="GameRuleId"/>).</summary>
    [DataField]
    public string? GhostGameRuleId;

    [DataField]
    public string? GhostTagLocKey;

    public AntagRoleDefinition ToDefinition()
    {
        return new AntagRoleDefinition(
            Id,
            NameLocKey,
            DescriptionLocKey,
            Cost,
            IconPath,
            Mode,
            AntagId,
            GameRuleId,
            TagLocKey,
            MinimumPlayers,
            RequiresInRound,
            RequiresPreRoundLobby,
            MinimumTimeFromRoundStart,
            UnavailableReasonLocKey,
            GhostRulesLocKey,
            GhostAutoJoinEntityProto,
            FreeMinimumSponsorLevel ?? -1,
            JobBlacklist,
            SpeciesBlacklist,
            RequiresPresetGameRuleId,
            IgnoresStationJob,
            GhostGameRuleId,
            GhostTagLocKey);
    }
}

[Prototype("antagTokenCatalog")]
public sealed partial class AntagTokenCatalogPrototype : IPrototype
{
    public const string DefaultId = "Default";

    [IdDataField]
    public string ID { get; private set; } = default!;

    [DataField(required: true)]
    public List<AntagTokenListingEntry> Listings = new();
}
