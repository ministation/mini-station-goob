// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Collections.Generic;
using Content.Shared.EntityTable;
using Robust.Shared.GameObjects;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.Tests._Lavaland.Loot;

/// <summary>
/// Regression test for the TG lavaland loot port: every new artifact prototype and
/// every loot table must load, and the tendril table must spawn entities that initialize.
/// </summary>
[TestFixture]
public sealed class MiningLootTest
{
    private const int MinimumNewPrototypes = 20;

    private static readonly string[] NewPrototypes =
    {
        "LavalandEyeOfGod",
        "LavalandRodOfAsclepius",
        "LavalandImmortalityTalisman",
        "LavalandBookOfBabel",
        "LavalandJacobsLadder",
        "LavalandMementoMori",
        "LavalandConcussiveGauntlets",
        "LavalandBerserkerArmor",
        "LavalandBerserkerHelmet",
        "LavalandDarkShard",
        "LavalandCursedKatana",
        "LavalandParadoxBag",
        "LavalandWarpCubeRed",
        "LavalandWarpCubeBlue",
        "LavalandStrangeElixir",
        "LavalandSpookyLantern",
        "LavalandFriendlyWisp",
        "LavalandShipInABottle",
        "LavalandMiningSoulstone",
        "LavalandPrisonCube",
        "LavalandGuardianCreatorMiner",
        "LavalandSummonItemSpellbook",
        "LavalandWildhunterKnife",
    };

    private static readonly string[] Tables =
    {
        "LavalandLootTendril",
        "LavalandLootDemonic",
        "LavalandLootDragon",
        "LavalandLootBubblegum",
        "LavalandLootColossus",
        "LavalandLootPuzzle",
    };

    [Test]
    public async Task NewPrototypesAndTablesLoad()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var protoMan = server.ResolveDependency<IPrototypeManager>();

        await server.WaitAssertion(() =>
        {
            Assert.That(NewPrototypes.Length, Is.GreaterThan(MinimumNewPrototypes - 1),
                "test should cover more than twenty new prototypes");

            foreach (var id in NewPrototypes)
            {
                Assert.That(protoMan.HasIndex<EntityPrototype>(id), Is.True,
                    $"missing mining loot prototype {id}");
            }

            foreach (var id in Tables)
            {
                Assert.That(protoMan.HasIndex<EntityTablePrototype>(id), Is.True,
                    $"missing mining loot table {id}");
            }
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task TendrilTableSpawnsInitializedEntities()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var entMan = server.ResolveDependency<IEntityManager>();
        var protoMan = server.ResolveDependency<IPrototypeManager>();
        var testMap = await pair.CreateTestMap();

        var table = entMan.System<EntityTableSystem>();
        var tendril = protoMan.Index<EntityTablePrototype>("LavalandLootTendril");

        var spawned = new List<EntityUid>();

        for (var i = 0; i < 30; i++)
        {
            await server.WaitPost(() =>
            {
                foreach (var proto in table.GetSpawns(tendril))
                    spawned.Add(entMan.SpawnEntity(proto, testMap.GridCoords));
            });
        }

        await server.WaitRunTicks(5);

        await server.WaitAssertion(() =>
        {
            Assert.That(spawned, Is.Not.Empty, "the tendril table should roll something");

            foreach (var uid in spawned)
            {
                Assert.That(entMan.Deleted(uid), Is.False, "tendril loot must not delete itself");
                Assert.That(entMan.HasComponent<MetaDataComponent>(uid), Is.True);
                var meta = entMan.GetComponent<MetaDataComponent>(uid);
                Assert.That(meta.EntityPrototype, Is.Not.Null, "spawned loot must keep its prototype");
            }
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task EveryNewArtifactSpawns()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var entMan = server.ResolveDependency<IEntityManager>();
        var testMap = await pair.CreateTestMap();

        var spawned = new List<(string Proto, EntityUid Uid)>();

        await server.WaitPost(() =>
        {
            foreach (var proto in NewPrototypes)
                spawned.Add((proto, entMan.SpawnEntity(proto, testMap.GridCoords)));
        });

        await server.WaitRunTicks(5);

        await server.WaitAssertion(() =>
        {
            foreach (var (proto, uid) in spawned)
            {
                Assert.That(entMan.Deleted(uid), Is.False, $"{proto} deleted itself on map init");
                var meta = entMan.GetComponent<MetaDataComponent>(uid);
                Assert.That(meta.EntityPrototype?.ID, Is.EqualTo(proto), $"{proto} lost its prototype");
            }
        });

        await pair.CleanReturnAsync();
    }
}
