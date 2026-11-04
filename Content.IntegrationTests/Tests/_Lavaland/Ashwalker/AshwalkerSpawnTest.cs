// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Collections.Generic;
using Content.Server._Lavaland.Ashwalker;
using Content.Server._Lavaland.Ruins;
using Content.Shared._Lavaland.Ashwalker;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Inventory;
using Robust.Shared.GameObjects;

namespace Content.IntegrationTests.Tests._Lavaland.Ashwalker;

/// <summary>
/// Guards the ash walker spawn: the tribal kit from the starting gear prototype and the den egg clutch.
/// </summary>
[TestFixture]
[TestOf(typeof(AshwalkerTribeSystem))]
[TestOf(typeof(AshwalkerDenSpawnerComponent))]
public sealed class AshwalkerSpawnTest
{
    private static readonly (string Slot, string Prototype)[] TribalKit =
    {
        ("jumpsuit", "ClothingUniformJumpsuitGladiator"),
        ("outerClothing", "ClothingOuterArmorBone"),
        ("back", "AshwalkerSatchel"),
        ("head", "ClothingHeadHelmetBone"),
        ("shoes", "ClothingShoesCult"),
    };

    [Test]
    public async Task AshwalkerSpawnsWithTribalKit()
    {
        await using var pair = await PoolManager.GetServerClient();

        var server = pair.Server;
        var testMap = await pair.CreateTestMap();
        var entMan = server.ResolveDependency<IEntityManager>();

        EntityUid ashwalker = default;
        await server.WaitPost(() => ashwalker = entMan.SpawnEntity("MobAshwalker", testMap.GridCoords));
        await server.WaitRunTicks(2);

        await server.WaitAssertion(() =>
        {
            var inventory = entMan.System<InventorySystem>();

            Assert.That(entMan.HasComponent<AshwalkerTribeMemberComponent>(ashwalker), Is.True,
                "ash walker must carry the tribe member marker");

            Assert.Multiple(() =>
            {
                foreach (var (slot, prototype) in TribalKit)
                {
                    Assert.That(inventory.TryGetSlotEntity(ashwalker, slot, out var equipped), Is.True,
                        $"ash walker spawned without {slot} gear");
                    Assert.That(entMan.GetComponent<MetaDataComponent>(equipped!.Value).EntityPrototype?.ID,
                        Is.EqualTo(prototype), $"ash walker slot {slot} holds the wrong item");
                }

                // The bone spear must be IN a hand: an orphaned spear at the spawn coordinates
                // (failed inhand pickup) leaks past deletion and breaks the prototype sweep count.
                var hands = entMan.System<SharedHandsSystem>();
                var heldSpear = false;
                foreach (var held in hands.EnumerateHeld(ashwalker))
                {
                    if (entMan.GetComponent<MetaDataComponent>(held).EntityPrototype?.ID == "SpearBone")
                        heldSpear = true;
                }
                Assert.That(heldSpear, Is.True, "ash walker must hold the bone spear inhand");
            });
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task DenSpawnsAClutchOfEggs()
    {
        await using var pair = await PoolManager.GetServerClient();

        var server = pair.Server;
        var testMap = await pair.CreateTestMap();
        var entMan = server.ResolveDependency<IEntityManager>();

        await server.WaitPost(() => entMan.SpawnAtPosition("LavalandAshwalkerDenSpawner", testMap.GridCoords));
        await server.WaitRunTicks(5);

        await server.WaitAssertion(() =>
        {
            var counts = CountPrototypes(entMan);

            Assert.That(counts.GetValueOrDefault("LavalandAshwalkerEgg"), Is.EqualTo(3),
                "the ash walker den must start with a clutch of three eggs");
            Assert.That(counts.GetValueOrDefault("LavalandAshwalkerNest"), Is.EqualTo(1),
                "the den has a single nest");
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
