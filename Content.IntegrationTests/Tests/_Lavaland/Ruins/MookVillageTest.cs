// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Collections.Generic;
using Content.Server._Lavaland.Ruins;
using Content.Shared.Damage;
using Content.Shared.Damage.Components;
using Content.Shared.Damage.Systems;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;

namespace Content.IntegrationTests.Tests._Lavaland.Ruins;

/// <summary>
/// The mook village landmark must be built out of the TG pieces and populated
/// by every mook variant, including the tribal chief.
/// </summary>
[TestFixture]
[TestOf(typeof(MookVillageSystem))]
public sealed class MookVillageTest
{
    [Test]
    public async Task VillageBuildsAndSpawnsMooks()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var entMan = server.ResolveDependency<IEntityManager>();
        var mapSys = server.System<SharedMapSystem>();

        await server.WaitPost(() =>
        {
            mapSys.CreateMap(out var map);
            entMan.SpawnEntity("LavalandMookVillageSpawner", new MapCoordinates(0, 0, map));
        });

        await server.WaitRunTicks(5);

        await server.WaitAssertion(() =>
        {
            var counts = CountPrototypes(entMan);
            Assert.Multiple(() =>
            {
                Assert.That(counts.GetValueOrDefault("LavalandMookWoodFloor"), Is.GreaterThan(0),
                    "the village must be built");
                Assert.That(counts.GetValueOrDefault("MobLavalandMook"), Is.GreaterThan(0),
                    "the village must be populated by mook wanderers");
                Assert.That(counts.GetValueOrDefault("MobLavalandMookWorker"), Is.GreaterThan(0),
                    "the village must be populated by mook workers");
                Assert.That(counts.GetValueOrDefault("MobLavalandMookBard"), Is.GreaterThan(0),
                    "the village must be populated by a mook bard");
                Assert.That(counts.GetValueOrDefault("MobLavalandMookChief"), Is.GreaterThan(0),
                    "the village must be populated by a tribal chief");
            });
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task WandererHealsInjuredFactionMobs()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var entMan = server.ResolveDependency<IEntityManager>();
        var mapSys = server.System<SharedMapSystem>();
        var damageableSys = server.System<DamageableSystem>();

        var patient = EntityUid.Invalid;

        await server.WaitPost(() =>
        {
            mapSys.CreateMap(out var map);
            // The wanderer has the healing aura, the worker is the one being healed.
            entMan.SpawnEntity("MobLavalandMook", new MapCoordinates(0, 0, map));
            patient = entMan.SpawnEntity("MobLavalandMookWorker", new MapCoordinates(1, 0, map));
            damageableSys.TryChangeDamage(patient, new DamageSpecifier
            {
                DamageDict = { { "Blunt", 30f } },
            }, ignoreResistances: true);
        });

        await server.WaitAssertion(() =>
        {
            Assert.That(entMan.GetComponent<DamageableComponent>(patient).TotalDamage.Float(), Is.GreaterThan(0f),
                "the worker must take damage before healing starts");
        });

        await server.WaitRunTicks(180);

        await server.WaitAssertion(() =>
        {
            Assert.That(entMan.GetComponent<DamageableComponent>(patient).TotalDamage.Float(), Is.EqualTo(0f),
                "the mook healer must heal an injured mob of its faction");
        });

        await pair.CleanReturnAsync();
    }

    private static Dictionary<string, int> CountPrototypes(IEntityManager entMan)
    {
        var counts = new Dictionary<string, int>();
        foreach (var uid in entMan.GetEntities())
        {
            if (!entMan.TryGetComponent<MetaDataComponent>(uid, out var meta))
                continue;

            if (meta.EntityPrototype is not { } proto)
                continue;

            counts[proto.ID] = counts.GetValueOrDefault(proto.ID) + 1;
        }

        return counts;
    }
}
