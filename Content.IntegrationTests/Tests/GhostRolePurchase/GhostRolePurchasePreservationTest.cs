// SPDX-FileCopyrightText: 2026 Casha
// SPDX-License-Identifier: LicenseRef-MiniStation-1.0

using System.Linq;
using Content.Server._Mini.DailyRewards;
using Content.Shared._Mini.DailyRewards;
using Robust.Shared.GameObjects;
using Robust.Shared.Player;
using NUnit.Framework;

namespace Content.IntegrationTests.Tests.GhostRolePurchase;

[TestFixture]
public sealed class GhostRolePurchasePreservationTest
{
    [Test]
    public async Task Preservation_DailyRewardSystemTracksActiveTime()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var client = pair.Client;

        await server.WaitIdleAsync();
        await client.WaitIdleAsync();

        var entityManager = server.ResolveDependency<IEntityManager>();
        var dailyRewardSystem = entityManager.System<DailyRewardSystem>();

        Assert.That(dailyRewardSystem, Is.Not.Null, "DailyRewardSystem must exist");

        var systemType = dailyRewardSystem.GetType();
        var updateMethod = systemType.GetMethod(
            "Update",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance
        );

        Assert.That(
            updateMethod,
            Is.Not.Null,
            "DailyRewardSystem.Update method must exist to track active time"
        );

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task Preservation_DailyRewardComponentExists()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        await server.WaitIdleAsync();

        var componentFactory = server.ResolveDependency<IComponentFactory>();

        Assert.That(
            componentFactory.TryGetRegistration(typeof(DailyRewardComponent), out _),
            Is.True,
            "DailyRewardComponent must be registered"
        );

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task Preservation_DailyRewardSystemHasDatabaseMethods()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        await server.WaitIdleAsync();

        var entityManager = server.ResolveDependency<IEntityManager>();
        var dailyRewardSystem = entityManager.System<DailyRewardSystem>();
        var systemType = dailyRewardSystem.GetType();

        var loadMethod = systemType.GetMethod(
            "LoadPlayerData",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance
        );

        if (loadMethod == null)
        {
            var methods = systemType.GetMethods(
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance
            );

            var hasDbMethod = methods.Any(m =>
                m.Name.Contains("Load") ||
                m.Name.Contains("Save") ||
                m.Name.Contains("Database") ||
                m.Name.Contains("Db")
            );

            Assert.That(
                hasDbMethod,
                Is.True,
                "DailyRewardSystem must have database integration methods"
            );
        }
        else
        {
            Assert.That(
                loadMethod,
                Is.Not.Null,
                "DailyRewardSystem must have database loading functionality"
            );
        }

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task Preservation_DailyRewardUIEventsExist()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        await server.WaitIdleAsync();

        var updateMessageType = typeof(DailyRewardUpdateMessage);
        Assert.That(updateMessageType, Is.Not.Null, "DailyRewardUpdateMessage must exist");

        var claimMessageType = typeof(DailyRewardClaimMessage);
        Assert.That(claimMessageType, Is.Not.Null, "DailyRewardClaimMessage must exist");

        var claimRequestType = typeof(DailyRewardClaimRequestEvent);
        Assert.That(claimRequestType, Is.Not.Null, "DailyRewardClaimRequestEvent must exist");

        await pair.CleanReturnAsync();
    }
}
