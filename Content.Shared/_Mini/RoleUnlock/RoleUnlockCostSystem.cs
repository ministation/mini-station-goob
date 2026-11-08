// SPDX-FileCopyrightText: 2026 Casha
// SPDX-License-Identifier: LicenseRef-MiniStation-1.0

using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using Content.Shared._Mini.AntagUnlock;
using Content.Shared._Mini.JobUnlock;
using Content.Shared.Players.PlayTimeTracking;
using Content.Shared.Preferences;
using Content.Shared.Roles;
using Robust.Shared.GameObjects;
using Robust.Shared.Prototypes;

namespace Content.Shared._Mini.RoleUnlock;

public sealed class RoleUnlockCostSystem : EntitySystem
{
    [Dependency] private readonly IPrototypeManager _proto = default!;
    [Dependency] private readonly SharedRoleSystem _roles = default!;

    public bool TryGetJobUnlockCost(
        ProtoId<JobPrototype> jobId,
        IReadOnlyDictionary<string, TimeSpan> playTimes,
        HumanoidCharacterProfile? profile,
        out int cost)
    {
        cost = 0;

        if (!_proto.TryIndex(jobId, out var job) || !job.SetPreference)
            return false;

        var requirements = _roles.GetRoleRequirements(job);
        if (JobRequirements.TryRequirementsMet(
                requirements,
                playTimes,
                out _,
                EntityManager,
                _proto,
                profile))
        {
            return false;
        }

        var remaining = GetMaxUnmetPlaytime(requirements, playTimes, profile);
        if (remaining <= TimeSpan.Zero)
            return false;

        if (!TryResolveJobTier(job, out var tier, out var settings))
            return false;

        cost = ComputeCost(remaining, tier, settings);
        return true;
    }

    public bool TryGetAntagUnlockCost(
        ProtoId<AntagPrototype> antagId,
        IReadOnlyDictionary<string, TimeSpan> playTimes,
        HumanoidCharacterProfile? profile,
        int? shopTokenCost,
        out int cost)
    {
        cost = 0;

        if (!_proto.TryIndex(antagId, out var antag) || !antag.SetPreference)
            return false;

        var requirements = _roles.GetRoleRequirements(antag);
        if (JobRequirements.TryRequirementsMet(
                requirements,
                playTimes,
                out _,
                EntityManager,
                _proto,
                profile))
        {
            return false;
        }

        var remaining = GetMaxUnmetPlaytime(requirements, playTimes, profile);
        if (remaining <= TimeSpan.Zero)
            return false;

        if (!TryResolveAntagTier(antag, shopTokenCost, out var tier, out var settings))
            return false;

        cost = ComputeCost(remaining, tier, settings);
        return true;
    }

    public static int ComputeCost(
        TimeSpan remaining,
        RoleUnlockTierData tier,
        RoleUnlockPricingSettings settings)
    {
        var coinsPerStep = tier.Command ? settings.CommandCoinsPerStep : settings.RegularCoinsPerStep;
        var steps = Math.Ceiling(remaining.TotalHours / settings.HoursPerCoinStep);
        var raw = (int) (steps * coinsPerStep);
        return Math.Clamp(raw, settings.MinCost, tier.MaxCost);
    }

    public TimeSpan GetMaxUnmetPlaytime(
        HashSet<JobRequirement>? requirements,
        IReadOnlyDictionary<string, TimeSpan> playTimes,
        HumanoidCharacterProfile? profile)
    {
        if (requirements == null)
            return TimeSpan.Zero;

        var max = TimeSpan.Zero;
        foreach (var requirement in requirements)
        {
            var deficit = GetUnmetTime(requirement, playTimes, profile);
            if (deficit > max)
                max = deficit;
        }

        return max;
    }

    private TimeSpan GetUnmetTime(
        JobRequirement requirement,
        IReadOnlyDictionary<string, TimeSpan> playTimes,
        HumanoidCharacterProfile? profile)
    {
        if (requirement.Inverted)
            return TimeSpan.Zero;

        switch (requirement)
        {
            case OverallPlaytimeRequirement overall:
            {
                var current = playTimes.GetValueOrDefault(PlayTimeTrackingShared.TrackerOverall);
                var diff = overall.Time - current;
                return diff > TimeSpan.Zero ? diff : TimeSpan.Zero;
            }
            case DepartmentTimeRequirement department:
            {
                var playtime = TimeSpan.Zero;
                var departmentProto = _proto.Index(department.Department);
                foreach (var other in departmentProto.Roles)
                {
                    var tracker = _proto.Index(other).PlayTimeTracker;
                    playTimes.TryGetValue(tracker, out var otherTime);
                    playtime += otherTime;
                }

                var diff = department.Time - playtime;
                return diff > TimeSpan.Zero ? diff : TimeSpan.Zero;
            }
            case RoleTimeRequirement role:
            {
                playTimes.TryGetValue(role.Role, out var roleTime);
                var diff = role.Time - roleTime;
                return diff > TimeSpan.Zero ? diff : TimeSpan.Zero;
            }
            default:
                return TimeSpan.Zero;
        }
    }

    private bool TryResolveJobTier(
        JobPrototype job,
        [NotNullWhen(true)] out RoleUnlockTierData? tier,
        [NotNullWhen(true)] out RoleUnlockPricingSettings? settings)
    {
        tier = null;
        settings = null;

        if (!_proto.TryIndex<JobUnlockCatalogPrototype>(JobUnlockCatalogPrototype.DefaultId, out var catalog))
            return false;

        settings = catalog.Pricing;

        foreach (var entry in catalog.Tiers)
        {
            if (entry.Jobs == null)
                continue;

            foreach (var jobId in entry.Jobs)
            {
                if (jobId == job.ID)
                {
                    tier = entry;
                    return true;
                }
            }
        }

        RoleUnlockTierData? weightTier = null;
        foreach (var entry in catalog.Tiers)
        {
            if (entry.MinWeight is not int minWeight || job.RealDisplayWeight < minWeight)
                continue;

            if (weightTier == null || minWeight > weightTier.MinWeight)
                weightTier = entry;
        }

        if (weightTier != null)
        {
            tier = weightTier;
            return true;
        }

        tier = new RoleUnlockTierData
        {
            MaxCost = catalog.DefaultTier.MaxCost,
            Command = catalog.DefaultTier.Command,
        };
        return true;
    }

    private bool TryResolveAntagTier(
        AntagPrototype antag,
        int? shopTokenCost,
        [NotNullWhen(true)] out RoleUnlockTierData? tier,
        [NotNullWhen(true)] out RoleUnlockPricingSettings? settings)
    {
        tier = null;
        settings = null;

        if (!_proto.TryIndex<AntagUnlockCatalogPrototype>(AntagUnlockCatalogPrototype.DefaultId, out var catalog))
            return false;

        settings = catalog.Pricing;

        foreach (var entry in catalog.Tiers)
        {
            if (entry.Antags == null)
                continue;

            foreach (var antagId in entry.Antags)
            {
                if (antagId == antag.ID)
                {
                    tier = entry;
                    return true;
                }
            }
        }

        var maxCost = catalog.DefaultTier.MaxCost;
        if (catalog.UseShopCostMultiplier && shopTokenCost is int shopCost && shopCost > 0)
            maxCost = Math.Max(maxCost, shopCost * catalog.ShopCostMultiplier);

        tier = new RoleUnlockTierData
        {
            MaxCost = maxCost,
            Command = catalog.DefaultTier.Command,
        };
        return true;
    }
}
