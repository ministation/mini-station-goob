// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Collections.Generic;
using Content.Server._Lavaland.Ruins;
using Robust.Shared.GameObjects;

namespace Content.IntegrationTests.Tests._Lavaland.Ruins;

[TestFixture]
[TestOf(typeof(PuzzleRoomSystem))]
public sealed class PuzzleRoomTest
{
    private static readonly HashSet<string> PuzzleLoot = new()
    {
        "LavalandMiningSoulstone",
        "LavalandSpookyLantern",
        "LavalandPrisonCube",
    };

    [Test]
    public async Task PuzzleRoomSpawnsPillarsAndFilledChest()
    {
        await using var pair = await PoolManager.GetServerClient();

        var server = pair.Server;
        var testMap = await pair.CreateTestMap();
        var entMan = server.ResolveDependency<IEntityManager>();

        await server.WaitPost(() => entMan.SpawnEntity("LavalandPuzzleRoomSpawner", testMap.GridCoords));

        await server.WaitRunTicks(2);

        await server.WaitAssertion(() =>
        {
            var pillars = 0;
            var loot = 0;
            var chest = false;

            foreach (var uid in entMan.GetEntities())
            {
                // The pool is shared with other tests: only count entities on this test's map,
                // otherwise leftovers from other tests inflate the counts.
                if (entMan.GetComponent<TransformComponent>(uid).MapID != testMap.MapId)
                    continue;

                if (!entMan.TryGetComponent<MetaDataComponent>(uid, out var meta) || meta.EntityPrototype is not { } proto)
                    continue;

                if (proto.ID == "LavalandPuzzlePillar")
                    pillars++;
                else if (proto.ID == "LavalandPuzzleCrate")
                    chest = true;
                else if (PuzzleLoot.Contains(proto.ID))
                    loot++;
            }

            Assert.Multiple(() =>
            {
                Assert.That(pillars, Is.EqualTo(8), "the puzzle room needs eight puzzle pillars");
                Assert.That(chest, Is.True, "the puzzle room needs the puzzling chest");
                Assert.That(loot, Is.GreaterThanOrEqualTo(1), "the puzzling chest must be filled from LavalandLootPuzzle");
            });
        });

        await pair.CleanReturnAsync();
    }
}
