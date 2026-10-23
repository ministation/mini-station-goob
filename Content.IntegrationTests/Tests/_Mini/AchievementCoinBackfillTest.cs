// SPDX-FileCopyrightText: 2026 Mini Station
// Мини-станция/Freaky-station, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/ministation/mini-station-goob/master/LICENSE.TXT

#nullable enable annotations

using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Content.Goobstation.Common.CCVar;
using Content.Goobstation.Server.Achievements;
using Content.Server._Mini.AntagTokens;
using Content.Server.Database;
using NUnit.Framework;
using Robust.Server.Player;
using Robust.Shared.GameObjects;
using Robust.Shared.Network;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Robust.UnitTesting;

namespace Content.IntegrationTests.Tests._Mini;

/// <summary>
/// Regression test for achievements that were unlocked before the coin reward mechanic reached the live
/// server. Their rewards must be paid retroactively on load and exactly once. Achievements unlocked after
/// the mechanic went live already received their coins when they unlocked and must not be paid again.
/// </summary>
[TestFixture]
public sealed class AchievementCoinBackfillTest
{
    // The reward build was deployed at 2026-10-21 14:18:26 UTC (17:18:26 MSK).
    private static readonly DateTime CorrectCutoff = new(2026, 10, 21, 14, 18, 26, DateTimeKind.Utc);

    // Real unlock from the live build that had achievements but no coin rewards (08:59 MSK).
    private static readonly DateTime BeforeMechanicUnlock = new(2026, 10, 21, 5, 59, 26, DateTimeKind.Utc);

    // 18:00 MSK: already after the reward build took over, but still before the wrong cutoff that the fix
    // originally used (21:36 MSK). A regression back to the wrong cutoff would pay these coins again.
    private static readonly DateTime DangerWindowUnlock = new(2026, 10, 21, 15, 0, 0, DateTimeKind.Utc);

    [Test]
    public async Task BackfillPaysPreMechanicUnlocksExactlyOnce()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings
        {
            Connected = true,
            DummyTicker = true,
            // The test writes achievement rows into the pair database, so it must not be recycled.
            Destructive = true,
        });

        var server = pair.Server;
        await server.WaitIdleAsync();

        var entMan = server.ResolveDependency<IEntityManager>();
        var playerMan = server.ResolveDependency<IPlayerManager>();
        var protoMan = server.ResolveDependency<IPrototypeManager>();
        var db = server.ResolveDependency<IServerDbManager>();
        var manager = server.ResolveDependency<AchievementManager>();
        var system = entMan.System<AchievementSystem>();
        var tokens = entMan.System<AntagTokenSystem>();

        server.CfgMan.SetCVar(GoobCVars.AchievementsEnabled, true);
        await pair.RunTicksSync(2);

        var session = playerMan.Sessions.Single();
        var userId = session.UserId;
        await PoolManager.WaitUntil(server, () => manager.IsLoaded(session));
        Assert.That(manager.IsLoaded(session), Is.True, "Achievement data was not loaded for the test session");

        // Three one-coin achievements keep the balance assertion simple.
        var protos = protoMan.EnumeratePrototypes<AchievementPrototype>()
            .Where(proto => !proto.Hidden && proto.GetCoinReward() == 1)
            .OrderBy(proto => proto.ID)
            .ToList();
        Assert.That(protos.Count, Is.GreaterThanOrEqualTo(3), "Expected at least three one-coin achievements");

        var beforeMechanicProto = protos[0];
        var dangerWindowProto = protos[1];
        var afterMechanicProto = protos[2];
        var afterMechanicUnlock = DateTime.UtcNow;

        // Seed database rows like the ones the live server saved, then mirror them in the in-memory state
        // that the manager would have loaded from those rows.
        await db.AddAchievementProgressAsync(userId, beforeMechanicProto.ID, 1, BeforeMechanicUnlock);
        await db.AddAchievementProgressAsync(userId, dangerWindowProto.ID, 1, DangerWindowUnlock);
        await db.AddAchievementProgressAsync(userId, afterMechanicProto.ID, 1, afterMechanicUnlock);

        var beforeEntry = AddLoadedEntry(manager, userId, beforeMechanicProto.ID, BeforeMechanicUnlock);
        var dangerEntry = AddLoadedEntry(manager, userId, dangerWindowProto.ID, DangerWindowUnlock);
        var afterEntry = AddLoadedEntry(manager, userId, afterMechanicProto.ID, afterMechanicUnlock);

        var balanceBefore = 0;
        await server.WaitPost(() => balanceBefore = tokens.GetBalance(userId));

        await InvokeBackfill(server, system, session);
        await pair.RunTicksSync(3);

        var balanceAfter = 0;
        await server.WaitPost(() => balanceAfter = tokens.GetBalance(userId));

        var expectedBalance = balanceBefore + beforeMechanicProto.GetCoinReward();
        Assert.Multiple(() =>
        {
            Assert.That(balanceAfter, Is.EqualTo(expectedBalance),
                "Only the achievement unlocked before the reward mechanic may be paid retroactively");
            Assert.That(GetPaidFlag(beforeEntry), Is.True, "The retroactively paid achievement must be marked as paid");
            Assert.That(GetPaidFlag(dangerEntry), Is.True,
                "An achievement unlocked while the reward build was already live must be marked as paid");
            Assert.That(GetPaidFlag(afterEntry), Is.True,
                "An achievement unlocked after the mechanic went live must be recorded as already paid");
        });

        // A second load must not pay the same achievements again.
        await InvokeBackfill(server, system, session);
        await pair.RunTicksSync(3);

        var balanceAfterSecondBackfill = 0;
        await server.WaitPost(() => balanceAfterSecondBackfill = tokens.GetBalance(userId));
        Assert.That(balanceAfterSecondBackfill, Is.EqualTo(balanceAfter),
            "The retroactive payout must not be repeated on the next load");

        // The paid flags must reach the database, otherwise a relog would pay the rewards again.
        var savedRows = await WaitForSavedRows(db, userId, beforeMechanicProto.ID, dangerWindowProto.ID, afterMechanicProto.ID);
        Assert.Multiple(() =>
        {
            Assert.That(savedRows[beforeMechanicProto.ID].CoinRewardPaid, Is.True,
                "The paid flag of the retroactively paid achievement was not saved");
            Assert.That(savedRows[dangerWindowProto.ID].CoinRewardPaid, Is.True,
                "The paid flag of the already paid achievement was not saved");
            Assert.That(savedRows[afterMechanicProto.ID].CoinRewardPaid, Is.True,
                "The paid flag of the after-mechanic achievement was not saved");
        });

        // Simulate a relog: reload the rows from the database exactly like the player load path does. The
        // loaded flags must keep every reward from being paid a second time.
        await ReloadFromDatabase(manager, session);
        Assert.That(manager.GetPendingCoinRewards(session), Is.Empty,
            "The paid flags were not restored when achievement progress was reloaded from the database");

        await InvokeBackfill(server, system, session);
        await pair.RunTicksSync(3);

        var balanceAfterReload = 0;
        await server.WaitPost(() => balanceAfterReload = tokens.GetBalance(userId));
        Assert.That(balanceAfterReload, Is.EqualTo(balanceAfter),
            "Reloading the achievement data must not pay the retroactive rewards a second time");

        await pair.CleanReturnAsync();
    }

    private static async Task<Dictionary<string, AchievementProgress>> WaitForSavedRows(
        IServerDbManager db,
        NetUserId userId,
        params string[] achievementIds)
    {
        Dictionary<string, AchievementProgress> rows = new();
        for (var attempt = 0; attempt < 100; attempt++)
        {
            await Task.Delay(50);
            rows = (await db.GetAchievementProgressAsync(userId))
                .Where(row => achievementIds.Contains(row.AchievementId))
                .ToDictionary(row => row.AchievementId);

            if (achievementIds.All(id => rows.TryGetValue(id, out var row) && row.CoinRewardPaid))
                return rows;
        }

        return rows;
    }

    private static async Task ReloadFromDatabase(AchievementManager manager, ICommonSession session)
    {
        var loadData = typeof(AchievementManager)
            .GetMethod("LoadData", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.That(loadData, Is.Not.Null, "AchievementManager.LoadData was not found");

        var task = (Task?) loadData!.Invoke(manager, new object[] { session, CancellationToken.None });
        Assert.That(task, Is.Not.Null, "AchievementManager.LoadData did not return a task");
        await task!;
    }

    private static object AddLoadedEntry(
        AchievementManager manager,
        NetUserId userId,
        string achievementId,
        DateTime unlockedAt)
    {
        var playersField = typeof(AchievementManager)
            .GetField("_players", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.That(playersField, Is.Not.Null, "AchievementManager._players was not found");
        var players = (IDictionary) playersField!.GetValue(manager)!;
        var entries = (IDictionary) players[userId]!;

        var entryType = typeof(AchievementManager).GetNestedType("AchievementEntry", BindingFlags.NonPublic);
        Assert.That(entryType, Is.Not.Null, "AchievementManager.AchievementEntry was not found");
        var entry = Activator.CreateInstance(entryType!)!;
        entryType!.GetField("Progress")!.SetValue(entry, 1);
        entryType.GetField("UnlockedAt")!.SetValue(entry, unlockedAt);
        entryType.GetField("CoinRewardPaid")!.SetValue(entry, false);
        entries[achievementId] = entry;
        return entry;
    }

    private static bool GetPaidFlag(object entry)
    {
        var field = entry.GetType().GetField("CoinRewardPaid");
        Assert.That(field, Is.Not.Null, "AchievementEntry.CoinRewardPaid was not found");
        return (bool) field!.GetValue(entry)!;
    }

    private static async Task InvokeBackfill(
        RobustIntegrationTest.IntegrationInstance server,
        AchievementSystem system,
        ICommonSession session)
    {
        var method = typeof(AchievementSystem)
            .GetMethod("BackfillCoinRewards", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.That(method, Is.Not.Null, "AchievementSystem.BackfillCoinRewards was not found");

        await server.WaitPost(() =>
        {
            try
            {
                method!.Invoke(system, new object[] { session });
            }
            catch (TargetInvocationException e) when (e.InnerException != null)
            {
                throw e.InnerException;
            }
        });
    }
}
