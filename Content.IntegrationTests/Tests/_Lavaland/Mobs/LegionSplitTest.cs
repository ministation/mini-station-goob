// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Collections.Generic;
using Content.Server._Lavaland.Mobs;
using Content.Shared.Damage;
using Content.Shared.Damage.Components;
using Content.Shared.Damage.Systems;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;

namespace Content.IntegrationTests.Tests._Lavaland.Mobs;

[TestFixture]
[TestOf(typeof(LegionSplitComponent))]
public sealed class LegionSplitTest
{
    [Test]
    public async Task LegionSplitsOnDeath()
    {
        await using var pair = await PoolManager.GetServerClient();

        var server = pair.Server;
        var testMap = await pair.CreateTestMap();
        var entMan = server.ResolveDependency<IEntityManager>();

        EntityUid boss = default;
        await server.WaitPost(() =>
        {
            boss = entMan.SpawnEntity("LavalandBossMegaLegion", testMap.GridCoords);
        });

        await server.WaitRunTicks(5);

        // Kill the boss -> 3 medium legions.
        await server.WaitPost(() =>
        {
            var damageable = entMan.GetComponent<DamageableComponent>(boss);
            entMan.System<DamageableSystem>().TryChangeDamage(
                boss,
                new DamageSpecifier() { DamageDict = { { "Blunt", 2000f } } },
                true);
        });

        await server.WaitRunTicks(10);

        await server.WaitAssertion(() =>
        {
            Assert.That(entMan.Deleted(boss), Is.True, "boss should die and delete itself");
            Assert.That(CountPrototypes(entMan, "LavalandBossLegionMedium"), Is.EqualTo(3),
                "boss death should spawn 3 medium legions");
            Assert.That(CountPrototypes(entMan, "LavalandBossLegionSmall"), Is.EqualTo(0),
                "medium legions should not split yet");
        });

        // Kill one medium -> 3 small legions.
        await server.WaitPost(() =>
        {
            foreach (var uid in EnumeratePrototypes(entMan, "LavalandBossLegionMedium"))
            {
                entMan.System<DamageableSystem>().TryChangeDamage(
                    uid,
                    new DamageSpecifier() { DamageDict = { { "Blunt", 2000f } } },
                    true);
                break;
            }
        });

        await server.WaitRunTicks(10);

        await server.WaitAssertion(() =>
        {
            Assert.That(CountPrototypes(entMan, "LavalandBossLegionMedium"), Is.EqualTo(2),
                "one medium legion should have died");
            Assert.That(CountPrototypes(entMan, "LavalandBossLegionSmall"), Is.EqualTo(3),
                "medium death should spawn 3 small legions");
        });

        // Kill the remaining mediums, then all smalls -> the chain stops.
        await server.WaitPost(() =>
        {
            foreach (var uid in EnumeratePrototypes(entMan, "LavalandBossLegionMedium"))
            {
                entMan.System<DamageableSystem>().TryChangeDamage(
                    uid,
                    new DamageSpecifier() { DamageDict = { { "Blunt", 2000f } } },
                    true);
            }
        });

        await server.WaitRunTicks(10);

        await server.WaitPost(() =>
        {
            foreach (var uid in EnumeratePrototypes(entMan, "LavalandBossLegionSmall"))
            {
                entMan.System<DamageableSystem>().TryChangeDamage(
                    uid,
                    new DamageSpecifier() { DamageDict = { { "Blunt", 2000f } } },
                    true);
            }
        });

        await server.WaitRunTicks(10);

        await server.WaitAssertion(() =>
        {
            Assert.That(CountPrototypes(entMan, "LavalandBossLegionMedium"), Is.EqualTo(0));
            Assert.That(CountPrototypes(entMan, "LavalandBossLegionSmall"), Is.EqualTo(0),
                "small legions have no splits left, the chain must stop");
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
