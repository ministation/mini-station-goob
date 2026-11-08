// SPDX-FileCopyrightText: 2026 Casha
// SPDX-License-Identifier: LicenseRef-MiniStation-1.0

using Content.Server._Mini.DailyRewards;
using Content.Server._Mini.GhostRolePurchase;
using Content.Shared._Mini.GhostRolePurchase;
using Robust.Shared.GameObjects;
using NUnit.Framework;

namespace Content.IntegrationTests.Tests.GhostRolePurchase;

[TestFixture]
public sealed class GhostRolePurchaseBugConditionTest
{
    [Test]
    public async Task BugCondition_MissingTicketSystemAndTimer()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        await server.WaitIdleAsync();

        var entityManager = server.ResolveDependency<IEntityManager>();
        var componentFactory = server.ResolveDependency<IComponentFactory>();

        Assert.That(
            componentFactory.TryGetRegistration(typeof(GhostRoleTicketComponent), out _),
            Is.True,
            "GhostRoleTicketComponent is not registered. Bug condition: missing ticket system."
        );

        Assert.That(
            componentFactory.TryGetRegistration(typeof(GhostRolePurchaseTimerComponent), out _),
            Is.True,
            "GhostRolePurchaseTimerComponent is not registered. Bug condition: missing timer system."
        );

        Assert.That(
            entityManager.System<GhostRolePurchaseTimerSystem>(),
            Is.Not.Null,
            "GhostRolePurchaseTimerSystem is not registered. Bug condition: missing timer management."
        );

        Assert.That(
            entityManager.System<GhostRolePurchaseSystem>(),
            Is.Not.Null,
            "GhostRolePurchaseSystem is not registered. Bug condition: missing purchase system."
        );

        Assert.That(
            GhostRolePriceCatalog.Prices.Count,
            Is.GreaterThan(0),
            "GhostRolePriceCatalog has no prices. Bug condition: missing price configuration."
        );

        var dailyRewardSystem = entityManager.System<DailyRewardSystem>();
        var systemType = dailyRewardSystem.GetType();

        var grantTicketsForPlaytimeMethod = systemType.GetMethod(
            "GrantTicketsForPlaytime",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance
        );

        Assert.That(
            grantTicketsForPlaytimeMethod,
            Is.Not.Null,
            "DailyRewardSystem.GrantTicketsForPlaytime method not found. Bug condition: missing playtime ticket integration."
        );

        var grantTicketsForStreakMethod = systemType.GetMethod(
            "GrantTicketsForStreak",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance
        );

        Assert.That(
            grantTicketsForStreakMethod,
            Is.Not.Null,
            "DailyRewardSystem.GrantTicketsForStreak method not found. Bug condition: missing streak ticket integration."
        );

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task BugCondition_MissingGameModeValidation()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        await server.WaitIdleAsync();

        var entityManager = server.ResolveDependency<IEntityManager>();
        var purchaseSystem = entityManager.System<GhostRolePurchaseSystem>();
        var systemType = purchaseSystem.GetType();

        var isRoleAvailableMethod = systemType.GetMethod(
            "IsRoleAvailableInGameMode",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance
        );

        Assert.That(
            isRoleAvailableMethod,
            Is.Not.Null,
            "GhostRolePurchaseSystem.IsRoleAvailableInGameMode method not found. Bug condition: missing game mode validation."
        );

        await pair.CleanReturnAsync();
    }
}
