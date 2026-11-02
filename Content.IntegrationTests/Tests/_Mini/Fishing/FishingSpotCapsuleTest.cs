// SPDX-License-Identifier: AGPL-3.0-or-later
// Ported from tgstation/tgstation (code/modules/fishing, AGPL-3.0-or-later).

#nullable enable

using System.Collections.Generic;
using Content.IntegrationTests.Tests.Interaction;
using Content.Shared._Mini.Fishing;
using Content.Shared._Mini.Fishing.Components;
using Content.Shared._Mini.Fishing.Prototypes;
using Content.Shared.Emag.Systems;
using Content.Shared.Interaction;
using Robust.Shared.GameObjects;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.Tests._Mini.Fishing;

[TestFixture]
public sealed class FishingSpotCapsuleTest : InteractionTest
{
    private async Task<List<ProtoId<FishingSourcePrototype>>> AnchoredSpotSources()
    {
        var found = new List<ProtoId<FishingSourcePrototype>>();
        await Server.WaitPost(() =>
        {
            var spotQuery = SEntMan.EntityQueryEnumerator<FishingSourceComponent, TransformComponent>();
            while (spotQuery.MoveNext(out var uid, out var source, out var xform))
            {
                // BaseFishingSpot entities are anchored; machine sources (toilets etc.) are not.
                if (!xform.Anchored)
                    continue;

                found.Add(source.Source);
            }
        });
        return found;
    }

    [Test]
    public async Task CapsuleDeploysSelectedSpot()
    {
        var capsule = SEntMan.GetEntity(await Spawn("FishingSpotCapsule"));

        await Server.WaitPost(() =>
        {
            SEntMan.System<SharedInteractionSystem>().UseInHandInteraction(SPlayer, capsule);
        });

        await RunTicks(5);

        Assert.That(SEntMan.EntityExists(capsule), Is.False, "capsule is consumed by the deploy");

        var sources = await AnchoredSpotSources();
        Assert.That(sources, Does.Contain(new ProtoId<FishingSourcePrototype>("FishingSourceRiver")));
    }

    [Test]
    public async Task UnsafeSpotsNeedEmag()
    {
        var capsule = SEntMan.GetEntity(await Spawn("FishingSpotCapsule"));

        // Forged selection of an unsafe spot is rejected while the safeties are on.
        await Server.WaitPost(() =>
        {
            SEntMan.EventBus.RaiseLocalEvent(capsule, new FishingSpotCapsuleSelectMessage("FishingSourceLava"));
        });

        await RunTicks(5);

        await Server.WaitPost(() =>
        {
            Assert.That(SEntMan.GetComponent<FishingSpotCapsuleComponent>(capsule).SelectedSource,
                Is.EqualTo(new ProtoId<FishingSourcePrototype>("FishingSourceRiver")));
        });

        // Emagging unlocks the lava spot.
        await Server.WaitPost(() =>
        {
            var emagEv = new GotEmaggedEvent(SPlayer, EmagType.Access); SEntMan.EventBus.RaiseLocalEvent(capsule, ref emagEv);
        });

        await RunTicks(5);

        await Server.WaitPost(() =>
        {
            Assert.That(SEntMan.GetComponent<FishingSpotCapsuleComponent>(capsule).Emagged, Is.True);
            SEntMan.EventBus.RaiseLocalEvent(capsule, new FishingSpotCapsuleSelectMessage("FishingSourceLava"));
        });

        await RunTicks(5);

        await Server.WaitPost(() =>
        {
            var comp = SEntMan.GetComponent<FishingSpotCapsuleComponent>(capsule);
            Assert.That(comp.SelectedSource, Is.EqualTo(new ProtoId<FishingSourcePrototype>("FishingSourceLava")));
            SEntMan.System<SharedInteractionSystem>().UseInHandInteraction(SPlayer, capsule);
        });

        await RunTicks(5);

        Assert.That(SEntMan.EntityExists(capsule), Is.False);

        var sources = await AnchoredSpotSources();
        Assert.That(sources, Does.Contain(new ProtoId<FishingSourcePrototype>("FishingSourceLava")));
        Assert.That(sources, Does.Not.Contain(new ProtoId<FishingSourcePrototype>("FishingSourceRiver")));
    }
}
