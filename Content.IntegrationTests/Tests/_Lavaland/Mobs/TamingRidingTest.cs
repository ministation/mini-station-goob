// SPDX-License-Identifier: AGPL-3.0-or-later

using System;
using System.Numerics;
using Content.Goobstation.Shared.Vehicles;
using Content.Server._Lavaland.Mobs;
using Content.Server.NPC.HTN;
using Content.Shared.Buckle;
using Content.Shared.Buckle.Components;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Interaction;
using Content.Shared.Movement.Components;
using Content.Shared.NPC.Systems;
using Robust.Shared.GameObjects;

namespace Content.IntegrationTests.Tests._Lavaland.Mobs;

/// <summary>
/// TG parity for lavaland taming and riding: feeding an ash flora produce tames a goliath,
/// the goliath saddle takes 5.5 seconds to affix, and a rider then drives the mob through the
/// fork's VehicleComponent (the AI is parked and the mob speeds up while mounted).
/// </summary>
[TestFixture]
[TestOf(typeof(LavalandTamingSystem))]
public sealed class TamingRidingTest
{
    [Test]
    public async Task TameSaddleAndRideGoliath()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var entMan = server.ResolveDependency<IEntityManager>();
        var hands = server.System<SharedHandsSystem>();
        var buckleSystem = server.System<SharedBuckleSystem>();
        var factions = server.System<NpcFactionSystem>();

        var map = await pair.CreateTestMap();
        var coords = map.GridCoords;

        EntityUid goliath = default;
        EntityUid tamer = default;

        // Feed it in the tick it spawns: a hostile goliath that gets a tick to plan attacks the tamer,
        // which interrupts the saddle do-after below.
        await server.WaitPost(() =>
        {
            tamer = entMan.SpawnEntity("MobHuman", coords);
            // 1.5 m keeps the mobs out of collision range so physics cannot jitter the
            // saddle do-after past its 0.3 m break-on-move threshold.
            goliath = entMan.SpawnEntity("MobLavalandGoliath", coords.Offset(new Vector2(1.5f, 0f)));

            // Feed it once with a guaranteed roll: the goliath should become tamed and friendly.
            var tame = entMan.GetComponent<TameableMobComponent>(goliath);
            tame.CurrentChance = 100f;

            var food = entMan.SpawnEntity("FoodAshFireBlossom", coords);
            Assert.That(hands.TryPickupAnyHand(tamer, food), Is.True);

            entMan.EventBus.RaiseLocalEvent(goliath, new InteractUsingEvent(tamer, food, goliath, coords));
        });

        // The eaten food is deleted at the end of its tick. Do not pick up the saddle before that:
        // while the food still stands in the active hand it becomes the saddle do-after's initial
        // item, and its deletion then breaks the do-after.
        await server.WaitRunTicks(1);

        await server.WaitAssertion(() =>
        {
            Assert.That(entMan.HasComponent<TamedMobComponent>(goliath), "feeding ash flora must tame the goliath");
            Assert.That(entMan.HasComponent<TameableMobComponent>(goliath), Is.False);
            Assert.That(factions.IsIgnored(goliath, tamer), "a tamed goliath must ignore its tamer");
        });

        // Affix the saddle. TG wants 5.5 s, the test shortens it to keep the run fast.
        await server.WaitPost(() =>
        {
            // Park the AI so a wandering goliath cannot outrun the saddle do-after.
            server.System<HTNSystem>().SetHTNEnabled(goliath, false);

            var mount = entMan.GetComponent<MountableMobComponent>(goliath);
            mount.SaddleDelay = TimeSpan.FromMilliseconds(100);

            var saddle = entMan.SpawnEntity("LavalandGoliathSaddle", coords);
            Assert.That(hands.TryPickupAnyHand(tamer, saddle), Is.True);

            entMan.EventBus.RaiseLocalEvent(goliath, new InteractUsingEvent(tamer, saddle, goliath, coords));
        });

        await server.WaitRunTicks(20);

        await server.WaitAssertion(() =>
        {
            Assert.That(entMan.GetComponent<MountableMobComponent>(goliath).Saddled, "the do-after must saddle the goliath");
            Assert.That(entMan.HasComponent<StrapComponent>(goliath), Is.True);
            Assert.That(entMan.TryGetComponent<VehicleComponent>(goliath, out var vehicle), Is.True);

            var buckle = entMan.GetComponent<BuckleComponent>(tamer);
            Assert.That(buckleSystem.TryBuckle(tamer, tamer, goliath, buckle), "a saddled goliath must accept a rider");
        });

        await server.WaitRunTicks(5);

        await server.WaitAssertion(() =>
        {
            var vehicle = entMan.GetComponent<VehicleComponent>(goliath);
            Assert.That(vehicle.Driver, Is.EqualTo(tamer), "the rider must become the vehicle driver");
            Assert.That(entMan.HasComponent<RelayInputMoverComponent>(tamer), Is.True);

            Assert.That(entMan.GetComponent<HTNComponent>(goliath).Enabled, Is.False,
                "the AI must be parked while a rider drives the mob");
            Assert.That(entMan.GetComponent<MovementSpeedModifierComponent>(goliath).CurrentWalkSpeed,
                Is.EqualTo(4.5f).Within(0.01f), "a ridden goliath must run at riding speed");
        });

        // Let the unbuckle cooldown expire, then dismount and check that the mob is restored.
        await server.WaitRunTicks(15);

        await server.WaitAssertion(() =>
        {
            Assert.That(buckleSystem.TryUnbuckle(tamer, tamer), Is.True);
        });

        await server.WaitRunTicks(5);

        await server.WaitAssertion(() =>
        {
            Assert.That(entMan.GetComponent<VehicleComponent>(goliath).Driver, Is.Null);
            Assert.That(entMan.GetComponent<HTNComponent>(goliath).Enabled, Is.True,
                "the AI must come back after the rider dismounts");
            Assert.That(entMan.GetComponent<MovementSpeedModifierComponent>(goliath).CurrentWalkSpeed,
                Is.EqualTo(1.5f).Within(0.01f), "the riding speed boost must be reverted");
        });

        await pair.CleanReturnAsync();
    }
}
