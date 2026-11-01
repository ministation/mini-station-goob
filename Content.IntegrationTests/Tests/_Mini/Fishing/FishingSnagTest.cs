// SPDX-License-Identifier: AGPL-3.0-or-later
// Ported from tgstation/tgstation (code/modules/fishing, AGPL-3.0-or-later).

using System.Numerics;
using Content.IntegrationTests.Tests.Interaction;
using Content.Shared._Mini.Fishing;
using Content.Shared._Mini.Fishing.Components;
using Content.Shared.Interaction;
using Content.Shared.Interaction.Events;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;

namespace Content.IntegrationTests.Tests._Mini.Fishing;

/// <summary>
/// tg lets the hook grab things that are not fish sources: items and, with the right hook, creatures.
/// Telescopic rods also have to be extended before casting.
/// </summary>
[TestFixture]
public sealed class FishingSnagTest : InteractionTest
{
    private const string RodPrototype = "FishingRod";
    private const string RescueRodPrototype = "FishingRodRescue";
    private const string TelescopicRodPrototype = "FishingRodTelescopic";
    private const string WaterPrototype = "FloorWaterEntity";

    private void RaiseAfterInteract(EntityUid rod, EntityUid target)
    {
        SEntMan.EventBus.RaiseLocalEvent(rod,
            new AfterInteractEvent(SPlayer, rod, target, Xform(target).Coordinates, true));
    }

    private float DistanceToPlayer(EntityUid uid)
    {
        return (Transform.GetWorldPosition(Xform(uid)) - Transform.GetWorldPosition(Xform(SPlayer))).Length();
    }

    [Test]
    public async Task RodHooksItemsAndPullsThemIn()
    {
        var rod = SEntMan.GetEntity(await PlaceInHands(RodPrototype));
        var item = SEntMan.GetEntity(await Spawn("Crowbar"));

        await Server.WaitPost(() =>
        {
            Transform.SetCoordinates(item, Xform(SPlayer).Coordinates.Offset(new Vector2(1.5f, 0f)));
            RaiseAfterInteract(rod, item);

            var rodComp = SEntMan.GetComponent<FishingRodComponent>(rod);
            Assert.That(rodComp.FishingLure, Is.Not.Null, "An item within cast range must be hooked.");
            var lure = SEntMan.GetComponent<FishingLureComponent>(rodComp.FishingLure!.Value);
            Assert.Multiple(() =>
            {
                Assert.That(lure.Snagged, Is.True, "The lure must be marked as snagged.");
                Assert.That(lure.SourceEntity, Is.EqualTo(item), "The hooked item must be the lure target.");
            });

            SEntMan.EventBus.RaiseLocalEvent(rod, new UseInHandEvent(SPlayer));
            Assert.That(SEntMan.GetComponent<FishingRodComponent>(rod).FishingLure, Is.Null,
                "Reeling must free the hook.");
        });

        // The test player has a single hand and it holds the rod, so the item lands next to them.
        Assert.That(DistanceToPlayer(item), Is.LessThan(1.5f), "The hooked item must be pulled to the fisher.");
    }

    [Test]
    public async Task OnlyRescueHooksGrabCreatures()
    {
        var rod = SEntMan.GetEntity(await PlaceInHands(RodPrototype));
        var mob = SEntMan.GetEntity(await Spawn("MobMouse"));

        await Server.WaitPost(() =>
        {
            Transform.SetCoordinates(mob, Xform(SPlayer).Coordinates.Offset(new Vector2(1.5f, 0f)));
            RaiseAfterInteract(rod, mob);
            Assert.That(SEntMan.GetComponent<FishingRodComponent>(rod).FishingLure, Is.Null,
                "A regular hook must not grab a creature.");
        });

        var rescueRod = SEntMan.GetEntity(await PlaceInHands(RescueRodPrototype));
        await Server.WaitPost(() =>
        {
            RaiseAfterInteract(rescueRod, mob);
            Assert.That(SEntMan.GetComponent<FishingRodComponent>(rescueRod).FishingLure, Is.Not.Null,
                "The rescue hook must be able to grab a creature.");

            SEntMan.EventBus.RaiseLocalEvent(rescueRod, new UseInHandEvent(SPlayer));
            Assert.That(SEntMan.GetComponent<FishingRodComponent>(rescueRod).FishingLure, Is.Null,
                "Reeling the rescue hook must free it.");
        });

        Assert.That(DistanceToPlayer(mob), Is.LessThan(1.5f), "The rescue hook must pull the creature to the fisher.");
    }

    [Test]
    public async Task TelescopicRodMustBeExtended()
    {
        var rod = SEntMan.GetEntity(await PlaceInHands(TelescopicRodPrototype));
        await SpawnTarget(WaterPrototype);

        await Server.WaitPost(() =>
        {
            RaiseAfterInteract(rod, STarget!.Value);
            Assert.Multiple(() =>
            {
                Assert.That(SEntMan.GetComponent<FishingRodComponent>(rod).Extended, Is.False);
                Assert.That(SEntMan.GetComponent<FishingRodComponent>(rod).FishingLure, Is.Null,
                    "A collapsed rod must not cast.");
                Assert.That(SEntMan.HasComponent<FishingChallengeComponent>(SPlayer), Is.False);
            });

            SEntMan.EventBus.RaiseLocalEvent(rod, new UseInHandEvent(SPlayer));
            Assert.That(SEntMan.GetComponent<FishingRodComponent>(rod).Extended, Is.True);

            RaiseAfterInteract(rod, STarget.Value);
            Assert.That(SEntMan.GetComponent<FishingRodComponent>(rod).FishingLure, Is.Not.Null,
                "An extended rod must cast normally.");
            Assert.That(SEntMan.HasComponent<FishingChallengeComponent>(SPlayer), Is.True);
        });
    }
}
