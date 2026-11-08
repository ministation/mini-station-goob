// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Server._Lavaland.Mobs;
using Content.Server.Atmos.EntitySystems;
using Content.Shared._Lavaland.Mobs;
using Content.Shared.Atmos;
using Content.Shared.Containers.ItemSlots;
using Content.Shared.Damage;
using Content.Shared.Damage.Components;
using Content.Shared.Damage.Systems;
using System.Collections.Generic;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Content.Shared.Weapons.Melee.Events;
using Robust.Shared.Containers;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;

namespace Content.IntegrationTests.Tests._Lavaland.Megafauna;

/// <summary>
/// TG megafauna legion infest: a legion that hits an unconscious human swallows the
/// body into itself; when the shell legion dies, the body is released — dead but
/// fresh, so it can be revived.
/// </summary>
[TestFixture]
public sealed class LegionInfestTest
{
    [Test]
    public async Task UnconsciousHumanIsSwallowedAndReleased()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var entMan = server.ResolveDependency<IEntityManager>();
        var mapSys = server.System<SharedMapSystem>();
        var mobState = server.System<MobStateSystem>();
        var damageable = server.System<DamageableSystem>();
        var containerSys = server.System<SharedContainerSystem>();
        var atmos = server.System<AtmosphereSystem>();

        EntityUid legion = default;
        EntityUid victim = default;

        // Bring the victim to critical (unconscious, alive) like a real fight would.
        await server.WaitPost(() =>
        {
            var mapUid = mapSys.CreateMap(out var map);
            var coords = new MapCoordinates(0, 0, map);

            // Lavaland has air. In vacuum the swallowed body takes barotrauma damage, which
            // re-runs the mob thresholds and revives the forced-dead body inside the shell.
            var moles = new float[Atmospherics.AdjustedNumberOfGases];
            moles[(int) Gas.Oxygen] = 21.824779f;
            moles[(int) Gas.Nitrogen] = 82.10312f;
            atmos.SetMapAtmosphere(mapUid, false, new GasMixture(moles, Atmospherics.T20C));

            legion = entMan.SpawnEntity(null, coords);
            entMan.AddComponent<LegionInfestComponent>(legion);
            entMan.AddComponent<MobStateComponent>(legion);

            victim = entMan.SpawnEntity("MobHuman", coords);
            var hit = new DamageSpecifier();
            hit.DamageDict["Blunt"] = 120;
            damageable.TryChangeDamage(victim, hit, true);
        });

        await server.WaitRunTicks(2);

        // The legion's melee hit swallows the crit human.
        await server.WaitPost(() =>
        {
            var hitEvent = new MeleeHitEvent(
                new List<EntityUid> { victim }, legion, legion, new DamageSpecifier(), null,
                entMan.GetComponent<TransformComponent>(legion).Coordinates);
            entMan.EventBus.RaiseLocalEvent(legion, hitEvent);
        });

        await server.WaitRunTicks(3);

        await server.WaitAssertion(() =>
        {
            Assert.That(entMan.HasComponent<LegionShellComponent>(legion), Is.True,
                "legion should hold a shell");
            Assert.That(containerSys.TryGetContainer(legion, LegionInfestSystem.ShellContainerId, out var container),
                Is.True, "shell container should exist");
            Assert.That(container.ContainedEntities, Does.Contain(victim),
                "victim should be inside the legion");
            Assert.That(mobState.IsDead(victim), Is.True, "victim should be dead");
        });

        // The shell legion dying releases the body.
        await server.WaitPost(() =>
        {
            mobState.ChangeMobState(legion, MobState.Dead);
        });

        await server.WaitRunTicks(3);

        await server.WaitAssertion(() =>
        {
            Assert.That(containerSys.TryGetContainer(legion, LegionInfestSystem.ShellContainerId, out var container),
                Is.True);
            if (container is not null)
                Assert.That(container.ContainedEntities, Does.Not.Contain(victim),
                    "victim should be released from the shell container");
            var xform = entMan.GetComponent<TransformComponent>(victim);
            Assert.That(xform.MapID, Is.Not.EqualTo(MapId.Nullspace), "body should be back in the world");
            Assert.That(mobState.IsDead(victim), Is.True, "body stays dead until revived by medics");
            // TG fully heals the shell; here the body keeps the damage it died with,
            // which still stays far below the death threshold, so it is defib-able.
            var damage = entMan.GetComponent<DamageableComponent>(victim).Damage.GetTotal();
            Assert.That(damage, Is.LessThan(Content.Goobstation.Maths.FixedPoint.FixedPoint2.New(200)),
                "released body must remain revivable");
        });

        await pair.CleanReturnAsync();
    }
}
