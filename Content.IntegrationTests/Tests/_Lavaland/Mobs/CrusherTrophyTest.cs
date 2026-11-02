// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Server._Lavaland.Crusher;
using Content.Shared._Lavaland.ItemUpgrades.Components;
using Content.Shared.Containers.ItemSlots;
using Robust.Shared.GameObjects;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.Tests._Lavaland.Mobs;

[TestFixture]
[TestOf(typeof(CrusherTrophyComponent))]
public sealed class CrusherTrophyTest
{
    private static readonly string[] Trophies =
    {
        "LavalandDrakeTailSpike",
        "LavalandBlasterTubes",
        "LavalandMinerEye",
        "LavalandDemonClaws",
        "LavalandVortexTalisman",
        "LavalandBroodmotherTongue",
        "LavalandLegionnaireSpine",
    };

    [Test]
    public async Task EveryTrophyFitsIntoTheCrusher()
    {
        await using var pair = await PoolManager.GetServerClient();

        var server = pair.Server;
        var testMap = await pair.CreateTestMap();
        var entMan = server.ResolveDependency<IEntityManager>();
        var protoMan = server.ResolveDependency<IPrototypeManager>();

        EntityUid crusher = default;
        await server.WaitPost(() => crusher = entMan.SpawnEntity("WeaponCrusher", testMap.GridCoords));
        await server.WaitRunTicks(2);

        foreach (var trophyId in Trophies)
        {
            await server.WaitAssertion(() =>
            {
                Assert.That(protoMan.HasIndex<EntityPrototype>(trophyId), Is.True, $"{trophyId} must exist");
            });

            EntityUid trophy = default;
            await server.WaitPost(() => trophy = entMan.SpawnEntity(trophyId, testMap.GridCoords));

            await server.WaitAssertion(() =>
            {
                Assert.That(entMan.HasComponent<ItemUpgradeComponent>(trophy), Is.True,
                    $"{trophyId} must be an item upgrade");
                Assert.That(entMan.HasComponent<CrusherTrophyComponent>(trophy), Is.True,
                    $"{trophyId} must be a crusher trophy");
            });

            await server.WaitPost(() =>
            {
                var slots = entMan.System<ItemSlotsSystem>();
                Assert.That(slots.TryGetSlot(crusher, "upgrade_slot_trophy", out var slot), Is.True,
                    "the crusher must have a trophy slot");
                Assert.That(slots.TryInsert(crusher, "upgrade_slot_trophy", trophy, null), Is.True,
                    $"{trophyId} must fit into the trophy slot");
                Assert.That(slot!.Item, Is.EqualTo(trophy));
            });

            // The slot holds one trophy only, get rid of it before testing the next one.
            await server.WaitPost(() => entMan.DeleteEntity(trophy));
            await server.WaitRunTicks(1);
        }

        await pair.CleanReturnAsync();
    }
}
