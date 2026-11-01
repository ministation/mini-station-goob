// SPDX-License-Identifier: AGPL-3.0-or-later
// Ported from tgstation/tgstation (code/modules/fishing, AGPL-3.0-or-later).

using Content.IntegrationTests.Tests.Interaction;
using Content.Shared._Mini.Fishing.Components;
using Content.Shared._Mini.Fishing.Systems;
using Content.Shared.Chasm;
using Robust.Shared.Containers;

namespace Content.IntegrationTests.Tests._Mini.Fishing;

[TestFixture]
public sealed class ChasmDetritusTest : InteractionTest
{
    [Test]
    public async Task FallenItemIsFishedBackOut()
    {
        var chasm = SEntMan.GetEntity(await Spawn("FloorChasmEntity"));
        var item = SEntMan.GetEntity(await Spawn("Crowbar"));
        var rod = SEntMan.GetEntity(await Spawn("FishingRod"));

        Assert.That(SEntMan.HasComponent<FishingSourceComponent>(chasm), Is.True,
            "A chasm must be a fishing source.");

        await Server.WaitPost(() =>
        {
            var chasmSystem = SEntMan.System<ChasmSystem>();
            chasmSystem.StartFalling(chasm, SEntMan.GetComponent<ChasmComponent>(chasm), item, playSound: false);

            // Test hook: always hand over a stored item instead of rolling the loot table.
            SEntMan.EnsureComponent<ChasmStorageComponent>(chasm).StoredChance = 1f;
        });

        await RunSeconds(2f);

        var containerSystem = SEntMan.System<SharedContainerSystem>();
        Assert.That(containerSystem.TryGetContainingContainer(item, out var container), Is.True,
            "The fallen item must be kept in the chasm.");
        Assert.That(container!.ID, Is.EqualTo(ChasmStorageComponent.ContainerId));

        await Server.WaitPost(() =>
        {
            var sources = SEntMan.System<FishingSourceSystem>();
            Assert.That(sources.TryRollReward(chasm, "FishingSourceChasm", SPlayer, rod, out _, out var existing), Is.True);
            Assert.That(existing, Is.EqualTo(item), "Fishing in a chasm must return a stored item.");
        });
    }
}
