// SPDX-License-Identifier: AGPL-3.0-or-later
// Ported from tgstation/tgstation (code/modules/fishing, AGPL-3.0-or-later).

using Content.IntegrationTests.Tests.Interaction;
using Content.Server.Storage.EntitySystems;
using Content.Shared._Mini.Fishing.Components;
using Content.Shared.Mobs.Components;

namespace Content.IntegrationTests.Tests._Mini.Fishing;

/// <summary>
/// Some fish grow into mobs and only do so when they are not stashed in a container.
/// </summary>
[TestFixture]
public sealed class FishGrowthTest : InteractionTest
{
    [Test]
    public async Task TadpoleGrowsIntoFrog()
    {
        var mobs = SEntMan.Count<MobStateComponent>();
        var tadpole = SEntMan.GetEntity(await Spawn("FishTadpole"));
        SEntMan.GetComponent<FishGrowthComponent>(tadpole).GrowAt = TimeSpan.Zero;

        await RunTicks(5);

        Assert.That(SEntMan.EntityExists(tadpole), Is.False, "The grown fish must be replaced.");
        Assert.That(SEntMan.Count<MobStateComponent>(), Is.EqualTo(mobs + 1), "A mob must be spawned in its place.");
    }

    [Test]
    public async Task FishDoesNotGrowWhileInABag()
    {
        var mobs = SEntMan.Count<MobStateComponent>();
        var bag = SEntMan.GetEntity(await Spawn("FishingBag"));
        var tadpole = SEntMan.GetEntity(await Spawn("FishTadpole"));
        SEntMan.GetComponent<FishGrowthComponent>(tadpole).GrowAt = TimeSpan.Zero;

        Assert.That(SEntMan.System<StorageSystem>().Insert(bag, tadpole, out _, playSound: false), Is.True,
            "The bag must accept the fish.");

        await RunTicks(5);

        Assert.That(SEntMan.EntityExists(tadpole), Is.True, "A fish stashed in a bag must not grow.");
        Assert.That(SEntMan.Count<MobStateComponent>(), Is.EqualTo(mobs), "No mob may be spawned inside a bag.");
    }
}
