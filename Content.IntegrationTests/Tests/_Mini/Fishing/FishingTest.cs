// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Numerics;
using Content.Shared._Mini.Fishing;
using Content.Shared._Mini.Fishing.Components;
using Content.IntegrationTests.Tests.Interaction;
using Robust.Shared.GameObjects;

namespace Content.IntegrationTests.Tests._Mini.Fishing;

[TestFixture]
public sealed class FishingTest : InteractionTest
{
    /// <summary>
    /// Casting at water starts a challenge, the bite phase rolls loot, and a finished minigame gives a fish.
    /// </summary>
    [Test]
    public async Task CastFightCatchTest()
    {
        await SpawnTarget("FloorWaterEntity");
        await PlaceInHands("FishingRod");
        await Interact();
        await RunTicks(5);

        var player = ToServer(Player);
        Assert.That(SEntMan.TryGetComponent(player, out FishingChallengeComponent? challenge),
            "Casting a rod at water did not start a fishing challenge.");

        await Server.WaitPost(() => challenge!.NextPhaseTime = STiming.CurTime - TimeSpan.FromSeconds(1));
        await RunTicks(10);

        Assert.That(challenge!.Phase, Is.EqualTo(FishingPhase.Biting),
            "Fishing challenge did not enter the biting phase.");

        // Make the roll deterministic: water may roll a dud.
        await Server.WaitPost(() =>
        {
            challenge.Reward = "FishGoldfish";
            challenge.RewardEntity = null;
        });

        // Skip the click window and go straight to the minigame with the bait on the fish.
        await Server.WaitPost(() =>
        {
            challenge.Phase = FishingPhase.Minigame;
            challenge.Completion = 99f;
            challenge.CompletionGain = 100f;
            challenge.BaitHeight = 300f;
            challenge.FishHeight = 50f;
            challenge.FishPosition = 500f;
            challenge.BaitPosition = 500f;
            challenge.BaitVelocity = 0f;
            challenge.ReelDirection = 0;
        });

        await RunTicks(20);

        var fishCount = 0;
        await Server.WaitPost(() =>
        {
            var query = SEntMan.EntityQueryEnumerator<FishComponent>();
            while (query.MoveNext(out _))
                fishCount++;
        });

        Assert.That(fishCount, Is.GreaterThan(0), "Finishing the minigame did not spawn any fish.");
    }

    /// <summary>
    /// Walking away from the float must snap the line and stop the challenge.
    /// </summary>
    [Test]
    public async Task LineSnapsWhenWalkingAwayTest()
    {
        await SpawnTarget("FloorWaterEntity");
        await PlaceInHands("FishingRod");
        await Interact();
        await RunTicks(5);

        var player = ToServer(Player);
        Assert.That(SEntMan.TryGetComponent(player, out FishingChallengeComponent? challenge),
            "Casting a rod at water did not start a fishing challenge.");

        var lure = challenge!.Lure;
        Assert.That(lure, Is.Not.Null, "Casting a rod did not spawn a lure.");

        await Server.WaitPost(() =>
        {
            var mapCoords = Transform.GetMapCoordinates(player);
            Transform.SetWorldPosition(player, mapCoords.Position + new Vector2(30f, 0f));
        });

        await RunTicks(5);

        Assert.That(SEntMan.HasComponent<FishingChallengeComponent>(player), Is.False,
            "Walking far away from the float did not stop the fishing challenge.");

        await Server.WaitPost(() =>
        {
            var rod = SEntMan.EntityQueryEnumerator<FishingRodComponent>();
            while (rod.MoveNext(out _, out var rodComp))
                Assert.That(rodComp.FishingLure, Is.Null, "The rod still references the lure after the line snapped.");

            var lures = SEntMan.EntityQueryEnumerator<FishingLureComponent>();
            Assert.That(lures.MoveNext(out _, out _), Is.False, "The lure was not deleted after the line snapped.");
        });
    }

    /// <summary>
    /// Vending machines are dynamic fishing sources: the catch must come from the machine inventory.
    /// </summary>
    [Test]
    public async Task VendingDynamicSourceTest()
    {
        await SpawnTarget("VendingMachineCola");
        await PlaceInHands("FishingRod");
        await Interact();
        await RunTicks(5);

        var player = ToServer(Player);
        Assert.That(SEntMan.TryGetComponent(player, out FishingChallengeComponent? challenge),
            "Casting a rod at a vending machine did not start a fishing challenge.");

        await Server.WaitPost(() => challenge!.NextPhaseTime = STiming.CurTime - TimeSpan.FromSeconds(1));
        await RunTicks(10);

        Assert.That(challenge!.Phase, Is.EqualTo(FishingPhase.Biting),
            "Vending machine fishing challenge did not enter the biting phase.");
        Assert.That(challenge.Reward, Is.Not.Null,
            "Dynamic vending source did not roll any product.");

        var rewardId = challenge.Reward?.Id;
        Assert.That(rewardId, Is.Not.Null, "Dynamic vending source did not roll a concrete product id.");

        await Server.WaitPost(() =>
        {
            challenge.Phase = FishingPhase.Minigame;
            challenge.Completion = 99f;
            challenge.CompletionGain = 100f;
            challenge.BaitHeight = 300f;
            challenge.FishHeight = 50f;
            challenge.FishPosition = 500f;
            challenge.BaitPosition = 500f;
            challenge.BaitVelocity = 0f;
            challenge.ReelDirection = 0;
        });

        await RunTicks(20);

        var productCount = 0;
        await Server.WaitPost(() =>
        {
            var q = SEntMan.EntityQueryEnumerator<MetaDataComponent>();
            while (q.MoveNext(out _, out var meta))
            {
                if (meta.EntityPrototype?.ID == rewardId)
                    productCount++;
            }
        });

        Assert.That(productCount, Is.GreaterThan(0),
            "Finishing the minigame on a vending machine did not spawn the rolled product.");
    }
}