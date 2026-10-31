// SPDX-License-Identifier: AGPL-3.0-or-later

#nullable enable

using Content.IntegrationTests.Tests.Interaction;
using Content.Shared._Mini.Fishing.Components;
using Robust.Shared.GameObjects;

namespace Content.IntegrationTests.Tests._Mini.Fishing;

[TestFixture]
public sealed class FishingTreasureChestTest : InteractionTest
{
    /// <summary>
    /// A chest opens for a click, shows a prize, and hands it over on the second click.
    /// </summary>
    [Test]
    public async Task ChestPresentsAndGivesPrize()
    {
        await SpawnTarget("FishingTreasureChest");

        var chest = STarget!.Value;
        await Server.WaitPost(() =>
        {
            var comp = SEntMan.GetComponent<FishingTreasureChestComponent>(chest);
            comp.ChoosingTime = 0.01f;
        });

        await Interact();
        await RunTicks(10);

        await Server.WaitPost(() =>
        {
            var comp = SEntMan.GetComponent<FishingTreasureChestComponent>(chest);
            Assert.That(comp.State, Is.EqualTo(FishingTreasureChestState.Open),
                "The chest did not finish choosing a prize.");
            Assert.That(comp.Prize, Is.Not.Null, "The open chest shows no prize.");
        });

        var prizeCount = 0;
        await Server.WaitPost(() =>
        {
            var query = SEntMan.EntityQueryEnumerator<FishingTreasureChestPrizeComponent>();
            while (query.MoveNext(out _))
                prizeCount++;
        });
        Assert.That(prizeCount, Is.EqualTo(1), "The chest did not spawn its prize display entity.");

        await Interact();
        await RunTicks(5);

        await Server.WaitPost(() =>
        {
            var comp = SEntMan.GetComponent<FishingTreasureChestComponent>(chest);
            Assert.That(comp.UsesLeft, Is.EqualTo(17), "Taking the prize did not use up a pull.");
            Assert.That(comp.State, Is.EqualTo(FishingTreasureChestState.Closed),
                "The chest did not close after the prize was taken.");
        });

        prizeCount = 0;
        await Server.WaitPost(() =>
        {
            var query = SEntMan.EntityQueryEnumerator<FishingTreasureChestPrizeComponent>();
            while (query.MoveNext(out _))
                prizeCount++;
        });
        Assert.That(prizeCount, Is.Zero, "The prize display entity was left behind after the pull.");
    }
}
