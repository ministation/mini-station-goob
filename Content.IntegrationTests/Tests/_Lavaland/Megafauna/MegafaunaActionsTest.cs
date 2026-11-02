// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Collections.Generic;
using Content.Goobstation.Maths.FixedPoint;
using Content.Shared._Lavaland.Anger.Components;
using Content.Shared.Actions;
using Content.Shared.Actions.Components;
using Robust.Shared.GameObjects;

namespace Content.IntegrationTests.Tests._Lavaland.Megafauna;

/// <summary>
/// Regression test: every megafauna mob must have its actions granted on map init,
/// otherwise its selector tree can never perform an action and the mob stays inert.
/// </summary>
[TestFixture]
[TestOf(typeof(ActionGrantComponent))]
public sealed class MegafaunaActionsTest
{
    private static readonly (string Proto, string[] Actions)[] Protos =
    {
        ("LavalandBossMegaLegion", new[] { "ActionLegionSkulls", "ActionLegionCharge", "ActionLegionSentinel" }),
        ("LavalandBossAshDrake", new[] { "ActionDrakeFireCone", "ActionDrakeLavaPools", "ActionDrakeSlam" }),
        ("LavalandBossColossus", new[]
        {
            "ActionColossusBoltsSpread", "ActionColossusBoltsRing",
            "ActionColossusBoltsSpiral", "ActionColossusCounterSpiral",
        }),
        ("LavalandBossBloodDrunk", new[] { "ActionBdhDash", "ActionBdhSawBarrage" }),
        ("LavalandBossBloodDrunkGuidance", new[] { "ActionBdhDash", "ActionBdhSawBarrage" }),
        ("LavalandBossBloodDrunkHunter", new[] { "ActionBdhDash", "ActionBdhSawBarrage", "ActionBdhDashChain" }),
        ("LavalandEliteLegionnaire", new[]
        {
            "ActionEliteLegionnaireDash", "ActionEliteLegionnaireSummon", "ActionEliteLegionnaireSkulls",
        }),
        ("LavalandElitePandora", new[]
        {
            "ActionElitePandoraBlink", "ActionElitePandoraBox", "ActionElitePandoraCrosses",
        }),
        ("LavalandEliteHerald", new[] { "ActionEliteHeraldOmni", "ActionEliteHeraldTri" }),
        ("LavalandEliteGoliathBroodmother", new[] { "ActionEliteBroodSlam", "ActionEliteBroodSpawn" }),
        ("LavalandTendrilGoliath", new[] { "ActionTendrilSpikes", "ActionTendrilChasers" }),
    };

    [Test]
    public async Task AllMegafaunaAreGrantedActions()
    {
        await using var pair = await PoolManager.GetServerClient();

        var server = pair.Server;
        var testMap = await pair.CreateTestMap();
        var entMan = server.ResolveDependency<IEntityManager>();

        foreach (var (proto, expectedActions) in Protos)
        {
            EntityUid mob = default;

            await server.WaitPost(() =>
            {
                mob = entMan.SpawnEntity(proto, testMap.GridCoords);
            });

            await server.WaitRunTicks(5);

            await server.WaitAssertion(() =>
            {
                Assert.That(entMan.HasComponent<ActionGrantComponent>(mob), Is.True,
                    $"{proto} is missing ActionGrant, its actions are never granted");
                Assert.That(entMan.TryGetComponent<ActionsComponent>(mob, out var actions), Is.True,
                    $"{proto} is missing Actions");

                var granted = new List<string>();
                foreach (var action in actions!.Actions)
                {
                    if (entMan.Deleted(action))
                        continue;
                    var actionMeta = entMan.GetComponent<MetaDataComponent>(action);
                    granted.Add(actionMeta.EntityPrototype?.ID ?? action.ToString());
                }

                foreach (var expected in expectedActions)
                {
                    Assert.That(granted, Does.Contain(expected),
                        $"{proto} did not receive action {expected}, granted: {string.Join(", ", granted)}");
                }

                Assert.That(entMan.TryGetComponent<AngerComponent>(mob, out var anger), Is.True,
                    $"{proto} is missing Anger, its anger-scaled delays stay at minimum");
                Assert.That(anger!.TotalHp > FixedPoint2.Zero, Is.True,
                    $"{proto} should have a positive Anger totalHp");
            });

            // Do not stack dozens of mobs and their actions on the test map.
            await server.WaitPost(() => entMan.DeleteEntity(mob));
        }

        await pair.CleanReturnAsync();
    }
}
