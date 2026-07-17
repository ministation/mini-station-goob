// SPDX-FileCopyrightText: 2026 Casha
// Мини-станция/Freaky-station, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/ministation/mini-station-goob/master/LICENSE.TXT

using System;
using System.Collections.Generic;
using Content.Shared.Humanoid.Prototypes;
using Content.Shared.Roles;
using Robust.Shared.Prototypes;

namespace Content.Shared._Mini.AntagTokens;

public static class AntagTokenCatalog
{
    public const string BalanceEntryId = "balance";
    public const string MonthlyEarnedEntryId = "monthly-earned";
    public const string MonthlyYearEntryId = "monthly-year";
    public const string MonthlyMonthEntryId = "monthly-month";
    public const string LastDonorBonusClaimEntryId = "last-donor-bonus-claim";
    public const string DepositSelectionTokenId = "deposit";
    public const string DepositUsedRoleCreditEntryId = "deposit-used-role-credit";

    public const string CurrencyIconPath = "/Textures/_Mini/Interface/Antags/token_currency.png";

    public const string ThiefRole = "thief";
    public const string AgentRole = "agent";
    public const string NinjaRole = "ninja";
    public const string DragonRole = "dragon";
    public const string AbductorRole = "abductor";
    public const string InitialInfectedRole = "initial_infected";
    public const string RevenantRole = "revenant";
    public const string YaoRole = "yao";
    public const string HeadRevRole = "headrev";
    public const string CosmicCultRole = "cosmic_cult";
    public const string DevilRole = "devil";
    public const string BlobRole = "blob";
    public const string WizardRole = "wizard";
    public const string SlaughterDemonRole = "slaughter_demon";
    public const string SlasherRole = "slasher";
    public const string ChangelingRole = "changeling";
    public const string HereticRole = "heretic";
    public const string ShadowlingRole = "shadowling";
    public const string XenomorphRole = "xenomorph";
    public const string BingleRole = "bingle";
    public const string ParadoxCloneRole = "paradox_clone";
    public const string RatKingRole = "rat_king";
    public const string WraithRole = "wraith";
    public const string HasturRole = "hastur";
    public const string VoxRole = "vox";
    public const string BorerRole = "borer";
    public const string RatvarCultRole = "ratvar_cult";
    public const string BloodCultRole = "blood_cultist";
    public const string MothershipCoreRole = "mothership_core";

    /// <summary>Alias kept for older references; operative uses <see cref="YaoRole"/>.</summary>
    public const string NukeopsRole = YaoRole;

    public static readonly (TimeSpan Threshold, int RewardAmount)[] OnlineRewardMilestones = [];

    private static readonly Dictionary<int, int> SponsorMonthlyCaps = new()
    {
        [1] = 20,
        [2] = 30,
        [3] = 40,
        [4] = 60,
        [5] = 100,
    };

    public static int? GetSponsorMonthlyCap(int sponsorLevel)
    {
        return SponsorMonthlyCaps.GetValueOrDefault(sponsorLevel);
    }

    public static string GetRoleCreditEntryId(string roleId)
    {
        return $"role-credit:{roleId}";
    }

    public const string GhostAutoPendingUsedRoleCreditEntryId = "ghost-auto-pending-used-role-credit";

    public const string LastDonorDailyFreeAntagDayEntryId = "last-donor-daily-free-antag-day";

    public const string DepositUsedDonorDailyFreeEntryId = "deposit-used-donor-daily-free";

    public const string GhostAutoPendingUsedDonorDailyFreeEntryId = "ghost-auto-pending-used-donor-daily-free";

    /// <summary>
    /// Persisted while the player has successfully taken a ghost-rule token role this station round (survives disconnect / crash before pending rows are cleared).
    /// </summary>
    public const string GhostAntagConsumedMarkEntryId = "ghost-antag-consumed-mark";

    public static string GetGhostAutoPendingEntryId(string roleId)
    {
        return $"ghost-auto-pending:{roleId}";
    }

    /// <summary>
    /// Simple roles that may be purchased again even if bought in the previous round.
    /// </summary>
    public static bool IsExemptFromLastRoundPurchaseRepeat(string roleId, AntagPurchaseMode mode)
    {
        if (mode == AntagPurchaseMode.GhostRule)
            return true;

        return roleId is ThiefRole or AgentRole;
    }

    /// <summary>
    /// Dual listings act as lobby deposit before round start and ghost rule in-round.
    /// </summary>
    public static AntagPurchaseMode GetEffectiveMode(AntagRoleDefinition role, bool inPreRoundLobby, bool inRound)
    {
        if (role.Mode != AntagPurchaseMode.Dual)
            return role.Mode;

        if (inPreRoundLobby)
            return AntagPurchaseMode.LobbyDeposit;

        if (inRound)
            return AntagPurchaseMode.GhostRule;

        return AntagPurchaseMode.Unavailable;
    }

    public static AntagRoleDefinition ResolveEffectiveRole(AntagRoleDefinition role, bool inPreRoundLobby, bool inRound)
    {
        if (role.Mode != AntagPurchaseMode.Dual)
            return role;

        var mode = GetEffectiveMode(role, inPreRoundLobby, inRound);
        return mode switch
        {
            AntagPurchaseMode.LobbyDeposit => AsLobbyDeposit(role),
            AntagPurchaseMode.GhostRule => AsGhostRule(role),
            _ => role with { Mode = AntagPurchaseMode.Unavailable },
        };
    }

    /// <summary>Lobby/roundstart projection of a Dual listing (also used when fulfilling a pending deposit).</summary>
    public static AntagRoleDefinition AsLobbyDeposit(AntagRoleDefinition role)
    {
        if (role.Mode is not (AntagPurchaseMode.Dual or AntagPurchaseMode.LobbyDeposit))
            return role;

        return role with
        {
            Mode = AntagPurchaseMode.LobbyDeposit,
            RequiresPreRoundLobby = true,
            RequiresInRound = false,
            MinimumTimeFromRoundStart = 0,
            TagLocKey = role.TagLocKey ?? "antag-token-window-tag-queue",
        };
    }

    public static AntagRoleDefinition AsGhostRule(AntagRoleDefinition role)
    {
        if (role.Mode is not (AntagPurchaseMode.Dual or AntagPurchaseMode.GhostRule))
            return role;

        return role with
        {
            Mode = AntagPurchaseMode.GhostRule,
            RequiresInRound = true,
            RequiresPreRoundLobby = false,
            GameRuleId = role.GhostGameRuleId ?? role.GameRuleId,
            TagLocKey = role.GhostTagLocKey ?? "antag-token-window-tag-ghost",
            // Lone-op midround does not require the Nukeops preset.
            RequiresPresetGameRuleId = null,
            AntagId = null,
        };
    }

    public static bool SupportsGhostAutoJoin(AntagRoleDefinition role)
    {
        return role.Mode is AntagPurchaseMode.GhostRule or AntagPurchaseMode.Dual
               && !string.IsNullOrWhiteSpace(role.GhostAutoJoinEntityProto);
    }
}

public enum AntagPurchaseMode : byte
{
    LobbyDeposit,
    GhostRule,
    Unavailable,
    /// <summary>Lobby deposit in preround; ghost rule once the round is running.</summary>
    Dual,
}

public sealed record AntagRoleDefinition(
    string Id,
    string NameLocKey,
    string DescriptionLocKey,
    int Cost,
    string IconPath,
    AntagPurchaseMode Mode,
    string? AntagId = null,
    string? GameRuleId = null,
    string? TagLocKey = null,
    int MinimumPlayers = 0,
    bool RequiresInRound = false,
    bool RequiresPreRoundLobby = false,
    int MinimumTimeFromRoundStart = 0,
    string? UnavailableReasonLocKey = null,
    string? GhostRulesLocKey = null,
    string? GhostAutoJoinEntityProto = null,
    int FreeMinimumSponsorLevel = -1,
    IReadOnlyList<ProtoId<JobPrototype>>? JobBlacklist = null,
    IReadOnlyList<ProtoId<SpeciesPrototype>>? SpeciesBlacklist = null,
    string? RequiresPresetGameRuleId = null,
    bool IgnoresStationJob = false,
    string? GhostGameRuleId = null,
    string? GhostTagLocKey = null);
