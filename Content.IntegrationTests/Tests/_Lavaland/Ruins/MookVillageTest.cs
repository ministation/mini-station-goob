// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Collections.Generic;
using Content.Server._Lavaland.Ruins;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;

namespace Content.IntegrationTests.Tests._Lavaland.Ruins;

[TestFixture]
[TestOf(typeof(MookVillageSystem))]
public sealed class MookVillageTest
{
    [Test]
    public async Task VillageSpawnsHutsMooksAndTotems()
    {
        await using var pair = await PoolManager.GetServerClient();

        var server = pair.Server;
        var testMap = await pair.CreateTestMap();
        var entMan = server.ResolveDependency<IEntityManager>();

        EntityUid marker = default;
        await server.WaitPost(() => marker = entMan.SpawnEntity("LavalandMookVillageSpawner", testMap.GridCoords));

        await server.WaitRunTicks(2);

        await server.WaitAssertion(() =>
        {
            var counts = CountPrototypes(entMan);

            Assert.Multiple(() =>
            {
                Assert.That(counts.GetValueOrDefault("WallWood"), Is.GreaterThanOrEqualTo(30),
                    "the village must have its hut walls");
                Assert.That(counts.GetValueOrDefault("LavalandMookWoodFloor"), Is.GreaterThanOrEqualTo(30),
                    "the huts must have wooden floors");
                Assert.That(counts.GetValueOrDefault("Bonfire"), Is.GreaterThanOrEqualTo(1),
                    "the village needs its bonfire");
                Assert.That(counts.GetValueOrDefault("MobLavalandGutlunch"), Is.GreaterThanOrEqualTo(5),
                    "the village must be inhabited");
                Assert.That(counts.GetValueOrDefault("LavalandMookTotem"), Is.GreaterThanOrEqualTo(2),
                    "the village must have its totems");
                Assert.That(counts.GetValueOrDefault("LavalandMookMaterialStand"), Is.EqualTo(1),
                    "the material stand is a unique village piece");
            });

            Assert.That(entMan.EntityExists(marker), Is.True,
                "the marker must stay alive for the prototype sweep");
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
