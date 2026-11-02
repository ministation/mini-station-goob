// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Collections.Generic;
using System.Linq;
using Content.Client.DamageState;
using Content.Server.NPC.HTN;
using Content.Shared.EntityTable;
using Content.Shared.EntityTable.EntitySelectors;
using Content.Shared.NPC.Components;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Shared.GameObjects;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.Tests._Lavaland.Mobs;

/// <summary>
/// Guards the TG lavaland mobs added in the parity pass: they must spawn with AI, their converted
/// RSI assets must load with every visual state, and the planet spawn tables must reference them.
/// </summary>
[TestFixture]
public sealed class LavalandMobParityTest
{
    private static readonly string[] NewMobs =
    {
        "LavalandMobBileworm",
        "LavalandMobGoldgrub",
        "LavalandMobRaptorRed",
        "LavalandMobRaptorWhite",
        "LavalandMobRaptorPurple",
        "LavalandMobRaptorGreen",
        "LavalandMobRaptorYellow",
        "LavalandMobRaptorBlue",
        "LavalandMobRaptorBlack",
        "LavalandMobRaptorYoungRed",
        "LavalandMobRaptorYoungWhite",
        "LavalandMobRaptorYoungPurple",
        "LavalandMobRaptorYoungGreen",
        "LavalandMobRaptorYoungYellow",
        "LavalandMobRaptorYoungBlue",
        "LavalandMobRaptorYoungBlack",
    };

    private static readonly string[] HeavyTableMobs =
    {
        "LavalandMobBileworm",
        "LavalandMobGoldgrub",
        "LavalandMobRaptorRed",
        "LavalandMobRaptorWhite",
        "LavalandMobRaptorPurple",
        "LavalandMobRaptorGreen",
        "LavalandMobRaptorYellow",
        "LavalandMobRaptorBlue",
        "LavalandMobRaptorBlack",
    };

    private static readonly string[] LightTableMobs =
    {
        "LavalandMobRaptorYoungRed",
        "LavalandMobRaptorYoungWhite",
        "LavalandMobRaptorYoungPurple",
        "LavalandMobRaptorYoungGreen",
        "LavalandMobRaptorYoungYellow",
        "LavalandMobRaptorYoungBlue",
        "LavalandMobRaptorYoungBlack",
    };

    [Test]
    public async Task NewMobsSpawnWithAi()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var entMan = server.ResolveDependency<IEntityManager>();
        var testMap = await pair.CreateTestMap();
        var spawned = new EntityUid[NewMobs.Length];

        await server.WaitPost(() =>
        {
            for (var i = 0; i < NewMobs.Length; i++)
                spawned[i] = entMan.SpawnEntity(NewMobs[i], testMap.GridCoords);
        });

        await server.WaitAssertion(() =>
        {
            for (var i = 0; i < NewMobs.Length; i++)
            {
                var uid = spawned[i];
                var id = NewMobs[i];
                Assert.Multiple(() =>
                {
                    Assert.That(entMan.HasComponent<HTNComponent>(uid), $"{id} has no HTN component");
                    Assert.That(entMan.HasComponent<NpcFactionMemberComponent>(uid), $"{id} has no NPC faction component");
                });
            }
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task NewMobSpritesResolve()
    {
        var settings = new PoolSettings { Connected = true };
        await using var pair = await PoolManager.GetServerClient(settings);
        var client = pair.Client;
        var spawned = new EntityUid[NewMobs.Length];

        await client.WaitPost(() =>
        {
            for (var i = 0; i < NewMobs.Length; i++)
            {
                spawned[i] = client.EntMan.Spawn(NewMobs[i]);
                client.EntMan.RunMapInit(spawned[i], client.MetaData(spawned[i]));
            }
        });

        await client.WaitAssertion(() =>
        {
            for (var i = 0; i < NewMobs.Length; i++)
            {
                var uid = spawned[i];
                var id = NewMobs[i];
                var sprite = client.EntMan.GetComponent<SpriteComponent>(uid);
                var rsis = sprite.AllLayers
                    .Select(layer => layer.ActualRsi)
                    .Where(rsi => rsi != null)
                    .Cast<RSI>()
                    .ToList();

                Assert.That(rsis, Is.Not.Empty, $"{id} has no loaded sprite RSI");

                var visualStates = client.EntMan.GetComponent<DamageStateVisualsComponent>(uid);
                var states = visualStates.States.Values.SelectMany(x => x.Values).Distinct().ToList();
                Assert.That(states, Is.Not.Empty, $"{id} has no damage state visual states");

                foreach (var state in states)
                {
                    Assert.That(rsis.Any(rsi => rsi.TryGetState(state, out _)),
                        $"{id} sprite RSI does not have state {state}");
                }
            }
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task PlanetSpawnTablesContainNewMobs()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var protoMan = server.ResolveDependency<IPrototypeManager>();

        await server.WaitAssertion(() =>
        {
            var heavy = CollectTableIds(protoMan, "LavalandHeavyMobs");
            foreach (var id in HeavyTableMobs)
                Assert.That(heavy, Does.Contain(id), $"LavalandHeavyMobs does not spawn {id}");

            // Existing entries must stay in place.
            Assert.That(heavy, Does.Contain("MobLegion"), "LavalandHeavyMobs lost MobLegion");
            Assert.That(heavy, Does.Contain("MobLavalandGoliath"), "LavalandHeavyMobs lost MobLavalandGoliath");
            Assert.That(heavy, Does.Contain("LavalandMobBrimdemon"), "LavalandHeavyMobs lost LavalandMobBrimdemon");

            var light = CollectTableIds(protoMan, "LavalandLightMobs");
            foreach (var id in LightTableMobs)
                Assert.That(light, Does.Contain(id), $"LavalandLightMobs does not spawn {id}");
        });

        await pair.CleanReturnAsync();
    }

    private static HashSet<string> CollectTableIds(IPrototypeManager protoMan, string tableId)
    {
        var ids = new HashSet<string>();
        Collect(protoMan, protoMan.Index<EntityTablePrototype>(tableId).Table, ids);
        return ids;
    }

    private static void Collect(IPrototypeManager protoMan, EntityTableSelector selector, HashSet<string> ids)
    {
        switch (selector)
        {
            case GroupSelector group:
                foreach (var child in group.Children)
                    Collect(protoMan, child, ids);
                break;
            case AllSelector all:
                foreach (var child in all.Children)
                    Collect(protoMan, child, ids);
                break;
            case NestedSelector nested:
                Collect(protoMan, protoMan.Index(nested.TableId).Table, ids);
                break;
            case EntSelector ent:
                ids.Add(ent.Id.Id);
                break;
        }
    }
}
