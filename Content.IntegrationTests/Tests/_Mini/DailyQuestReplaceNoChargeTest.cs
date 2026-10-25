// SPDX-FileCopyrightText: 2026 Mini Station
// Мини-станция/Freaky-station, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/ministation/mini-station-goob/master/LICENSE.TXT

#nullable enable annotations

using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Content.Server._Mini.AntagTokens;
using Content.Server._Mini.DailyQuests;
using Content.Shared._Mini.DailyQuests;
using NUnit.Framework;
using Robust.Server.Player;
using Robust.Shared.GameObjects;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Robust.UnitTesting;

namespace Content.IntegrationTests.Tests._Mini;

/// <summary>
/// A quest replacement that the server rejects (quest already has progress) must not charge coins.
/// The old code spent the paid-replace cost before validating the request, so every denied attempt
/// silently took one coin from the player.
/// </summary>
[TestFixture]
public sealed class DailyQuestReplaceNoChargeTest
{
    [Test]
    public async Task DeniedReplaceDoesNotSpendBalance()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings
        {
            Connected = true,
            DummyTicker = true,
            // The test mutates the player's wallet rows in the pair database.
            Destructive = true,
        });

        var server = pair.Server;
        await server.WaitIdleAsync();
        await pair.RunTicksSync(10);

        var entMan = server.ResolveDependency<IEntityManager>();
        var playerMan = server.ResolveDependency<IPlayerManager>();
        var protoMan = server.ResolveDependency<IPrototypeManager>();
        var tokens = entMan.System<AntagTokenSystem>();
        var quests = entMan.System<DailyQuestSystem>();

        var session = playerMan.Sessions.Single();
        // A progress-based quest is required: time-based quests are replaceable in the lobby and
        // the rejection path under test would not be reached.
        var anyQuestId = protoMan.EnumeratePrototypes<DailyQuestPrototype>()
            .First(p => p.QuestType is not (DailyQuestType.SurviveRound
                or DailyQuestType.NoMeleeHits
                or DailyQuestType.NoDamageTaken)).ID;

        var balanceBefore = 0;
        await server.WaitPost(() =>
        {
            Assert.That(tokens.SetBalance(session.UserId, 5), Is.True,
                "The wallet must be loaded before the test seeds a balance");

            SeedQuestState(quests, session, anyQuestId, progress: 1);

            // The request is rejected (progress > 0), and nothing may be charged for it.
            quests.TryReplaceQuest(session, anyQuestId);

            balanceBefore = tokens.GetBalance(session.UserId);
        });

        Assert.That(balanceBefore, Is.EqualTo(5),
            "A rejected quest replacement must not spend the player's coins");

        // Return the pair through the clean path. A dirty return only warns, but CI runs the
        // integration suite with NUnit.MapWarningTo=Failed, so the warning fails the test.
        await pair.CleanReturnAsync();
    }

    private static void SeedQuestState(DailyQuestSystem quests, ICommonSession session, string questId, int progress)
    {
        var systemType = typeof(DailyQuestSystem);
        var stateType = systemType.GetNestedType("PlayerDailyQuestState", BindingFlags.NonPublic)
                        ?? throw new InvalidOperationException("PlayerDailyQuestState type not found");
        var slotType = systemType.GetNestedType("DailyQuestSlot", BindingFlags.NonPublic)
                       ?? throw new InvalidOperationException("DailyQuestSlot type not found");

        var state = Activator.CreateInstance(stateType, nonPublic: true)
                    ?? throw new InvalidOperationException("Failed to create PlayerDailyQuestState");

        var slot = Activator.CreateInstance(slotType, nonPublic: true)
                   ?? throw new InvalidOperationException("Failed to create DailyQuestSlot");
        slotType.GetField("QuestId")!.SetValue(slot, questId);
        slotType.GetField("Progress")!.SetValue(slot, progress);

        var slots = (IList)Activator.CreateInstance(typeof(List<>).MakeGenericType(slotType))!;
        slots.Add(slot);

        stateType.GetField("LoadedFromDb")!.SetValue(state, true);
        stateType.GetField("PlayerId")!.SetValue(state, session.UserId.UserId);
        stateType.GetField("QuestDate")!.SetValue(state, DailyQuestWeek.GetCurrentWeekStart());
        stateType.GetField("DailyReplaceUsed")!.SetValue(state, true);
        stateType.GetField("Slots")!.SetValue(state, slots);

        var states = (IDictionary)systemType
            .GetField("_states", BindingFlags.Instance | BindingFlags.NonPublic)!
            .GetValue(quests)!;
        states[session.UserId] = state;
    }
}
