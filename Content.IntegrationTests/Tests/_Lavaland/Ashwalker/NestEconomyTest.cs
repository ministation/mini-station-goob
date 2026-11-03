// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Collections.Generic;
using System.Numerics;
using Content.Goobstation.Maths.FixedPoint;
using Content.Server._Lavaland.Necropolis;
using Content.Shared._Lavaland.Necropolis;
using Content.Shared.Damage;
using Content.Shared.Damage.Components;
using Content.Shared.Damage.Prototypes;
using Content.Shared.Damage.Systems;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Systems;
using Robust.Shared.GameObjects;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.Tests._Lavaland.Ashwalker;

/// <summary>
/// Guards the TG ash walker nest economy: corpses turn into meat, meat turns into eggs,
/// and eating a corpse repairs the nest.
/// </summary>
[TestFixture]
[TestOf(typeof(AshwalkerNestSystem))]
public sealed class NestEconomyTest
{
    [Test]
    public async Task NestStartsEmptyAndEatsCorpses()
    {
        await using var pair = await PoolManager.GetServerClient();

        var server = pair.Server;
        var testMap = await pair.CreateTestMap();
        var entMan = server.ResolveDependency<IEntityManager>();
        var protoMan = server.ResolveDependency<IPrototypeManager>();

        EntityUid nest = default;
        await server.WaitPost(() => nest = entMan.SpawnEntity("LavalandAshwalkerNest", testMap.GridCoords));
        await server.WaitRunTicks(2);

        await server.WaitAssertion(() =>
        {
            Assert.That(entMan.GetComponent<AshwalkerNestComponent>(nest).Meat, Is.EqualTo(0),
                "the den already lays the starting clutch, so the nest itself starts with no meat");
        });

        EntityUid corpse = default;
        await server.WaitPost(() =>
        {
            var damage = new DamageSpecifier(protoMan.Index<DamageTypePrototype>("Blunt"), FixedPoint2.New(10));
            entMan.System<DamageableSystem>().TryChangeDamage(nest, damage, true);

            corpse = entMan.SpawnEntity("MobHuman", testMap.GridCoords.Offset(new Vector2(1f, 0f)));
            entMan.System<MobStateSystem>().ChangeMobState(corpse, MobState.Dead);
        });
        await server.WaitRunTicks(130);

        await server.WaitAssertion(() =>
        {
            var nestComp = entMan.GetComponent<AshwalkerNestComponent>(nest);
            Assert.That(nestComp.Meat, Is.EqualTo(1), "a corpse must give one meat");

            Assert.That(entMan.EntityExists(corpse), Is.False, "the nest must gib the corpse instead of deleting it silently");

            var damageable = entMan.GetComponent<DamageableComponent>(nest);
            Assert.That((float) damageable.TotalDamage, Is.LessThan(10f), "eating a corpse must heal the nest");
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task NestHatchesEggsFromMeat()
    {
        await using var pair = await PoolManager.GetServerClient();

        var server = pair.Server;
        var testMap = await pair.CreateTestMap();
        var entMan = server.ResolveDependency<IEntityManager>();

        EntityUid nest = default;
        await server.WaitPost(() => nest = entMan.SpawnEntity("LavalandAshwalkerNest", testMap.GridCoords));
        await server.WaitRunTicks(2);

        await server.WaitPost(() => entMan.GetComponent<AshwalkerNestComponent>(nest).Meat = 5);
        await server.WaitRunTicks(130);

        await server.WaitAssertion(() =>
        {
            var nestComp = entMan.GetComponent<AshwalkerNestComponent>(nest);

            Assert.That(nestComp.Meat, Is.EqualTo(1), "two eggs cost four meat");
            Assert.That(nestComp.Eggs, Is.EqualTo(2), "the TG nest has no egg cap");

            var counts = CountPrototypes(entMan);
            Assert.That(counts.GetValueOrDefault("LavalandAshwalkerEgg"), Is.EqualTo(2));
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
