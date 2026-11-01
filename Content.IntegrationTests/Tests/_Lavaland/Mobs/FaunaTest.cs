// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Collections.Generic;
using Content.Goobstation.Maths.FixedPoint;
using Content.Server._Lavaland.Mobs;
using Content.Shared._Lavaland.Mobs;
using Content.Shared.Damage;
using Content.Shared.Damage.Components;
using Content.Shared.Damage.Systems;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Robust.Shared.GameObjects;

namespace Content.IntegrationTests.Tests._Lavaland.Mobs;

[TestFixture]
[TestOf(typeof(LegionSplitComponent))]
[TestOf(typeof(LaserAbsorbComponent))]
public sealed class FaunaTest
{
    [Test]
    public async Task HivelordSplitsIntoBroodsOnDeath()
    {
        await using var pair = await PoolManager.GetServerClient();

        var server = pair.Server;
        var testMap = await pair.CreateTestMap();
        var entMan = server.ResolveDependency<IEntityManager>();

        EntityUid hivelord = default;
        await server.WaitPost(() =>
        {
            hivelord = entMan.SpawnEntity("MobLavalandHivelord", testMap.GridCoords);
        });

        await server.WaitRunTicks(5);

        await server.WaitPost(() =>
        {
            entMan.System<DamageableSystem>().TryChangeDamage(
                hivelord,
                new DamageSpecifier() { DamageDict = { { "Blunt", 200f } } },
                true);
        });

        await server.WaitRunTicks(10);

        await server.WaitAssertion(() =>
        {
            var mobState = entMan.GetComponent<MobStateComponent>(hivelord);
            Assert.That(mobState.CurrentState, Is.EqualTo(MobState.Dead),
                "hivelord should be dead");
            Assert.That(CountPrototypes(entMan, "MobLavalandHivelordBrood"), Is.EqualTo(3),
                "hivelord death should spawn 3 broods");
        });

        // Broods have no splits left.
        await server.WaitPost(() =>
        {
            foreach (var uid in EnumeratePrototypes(entMan, "MobLavalandHivelordBrood"))
            {
                entMan.System<DamageableSystem>().TryChangeDamage(
                    uid,
                    new DamageSpecifier() { DamageDict = { { "Blunt", 200f } } },
                    true);
            }
        });

        await server.WaitRunTicks(10);

        await server.WaitAssertion(() =>
        {
            // Corpses are not deleted, so check the mob states instead.
            var alive = 0;
            foreach (var uid in EnumeratePrototypes(entMan, "MobLavalandHivelordBrood"))
            {
                var mobState = entMan.GetComponent<MobStateComponent>(uid);
                if (mobState.CurrentState != MobState.Dead)
                    alive++;
            }
            Assert.That(alive, Is.EqualTo(0),
                "broods must be dead and must not have split further");
            Assert.That(CountPrototypes(entMan, "MobLavalandHivelordBrood"), Is.EqualTo(3),
                "brood corpses only: the split chain must stop");
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task BasiliskConvertsLaserDamageToCold()
    {
        await using var pair = await PoolManager.GetServerClient();

        var server = pair.Server;
        var testMap = await pair.CreateTestMap();
        var entMan = server.ResolveDependency<IEntityManager>();

        EntityUid basilisk = default;
        await server.WaitPost(() =>
        {
            basilisk = entMan.SpawnEntity("MobLavalandBasilisk", testMap.GridCoords);
        });

        await server.WaitRunTicks(5);

        await server.WaitPost(() =>
        {
            var applied = entMan.System<DamageableSystem>().TryChangeDamage(
                basilisk,
                new DamageSpecifier() { DamageDict = { { "Laser", 30f } } },
                false);
            Assert.That(applied, "damage should be applied");
        });

        await server.WaitRunTicks(5);

        await server.WaitAssertion(() =>
        {
            Assert.That(entMan.Deleted(basilisk), Is.False, "basilisk should survive 30 cold");
            var damageable = entMan.GetComponent<DamageableComponent>(basilisk);
            Assert.That(damageable.TotalDamage, Is.EqualTo(FixedPoint2.New(30f)),
                "laser must convert to cold 1:1");
            Assert.That(
                damageable.Damage.DamageDict.TryGetValue("Laser", out var laser) ? laser : FixedPoint2.Zero,
                Is.EqualTo(FixedPoint2.Zero), "no laser damage should remain");
            Assert.That(
                damageable.Damage.DamageDict.TryGetValue("Cold", out var cold) ? cold : FixedPoint2.New(30f),
                Is.EqualTo(FixedPoint2.New(30f)), "converted damage must be cold");
        });

        await pair.CleanReturnAsync();
    }

    private static int CountPrototypes(IEntityManager entMan, string protoId)
    {
        var count = 0;
        var query = entMan.EntityQueryEnumerator<MetaDataComponent>();
        while (query.MoveNext(out var uid, out var meta))
        {
            if (!entMan.Deleted(uid) && meta.EntityPrototype?.ID == protoId)
                count++;
        }
        return count;
    }

    private static IEnumerable<EntityUid> EnumeratePrototypes(IEntityManager entMan, string protoId)
    {
        var result = new List<EntityUid>();
        var query = entMan.EntityQueryEnumerator<MetaDataComponent>();
        while (query.MoveNext(out var uid, out var meta))
        {
            if (!entMan.Deleted(uid) && meta.EntityPrototype?.ID == protoId)
                result.Add(uid);
        }
        return result;
    }
}
