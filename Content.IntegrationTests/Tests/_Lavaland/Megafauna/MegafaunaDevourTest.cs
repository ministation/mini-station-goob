// SPDX-License-Identifier: LicenseRef-MiniStation-1.0

using System.Collections.Generic;
using System.Linq;
using Content.Shared._Lavaland.Megafauna.Components;
using Content.Shared._Shitmed.Body.Organ;
using Content.Shared.Body.Components;
using Content.Shared.Body.Systems;
using Content.Shared.Damage.Components;
using Content.Shared.Damage;
using Content.Shared.Damage.Systems;
using Content.Goobstation.Maths.FixedPoint;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Robust.Shared.GameObjects;
using Robust.Shared.Log;
using Robust.Shared.Map;
using Serilog.Events;

namespace Content.IntegrationTests.Tests._Lavaland.Megafauna;

/// <summary>
/// Megafauna devour (TG _megafauna.dm): an adjacent corpse is eaten once — the eater
/// heals, the corpse is marked gutted and loses its vital organs; a distant corpse
/// is left alone.
/// </summary>
[TestFixture]
public sealed class MegafaunaDevourTest
{
    [Test]
    public async Task AdjacentCorpseIsDevoured()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var entMan = server.ResolveDependency<IEntityManager>();
        var mapSys = server.System<SharedMapSystem>();
        var mobState = server.System<MobStateSystem>();
        var damageable = server.System<DamageableSystem>();
        var body = server.System<SharedBodySystem>();

        EntityUid eater = default;
        EntityUid corpse = default;
        FixedPoint2 damageBefore = default;

        await server.WaitPost(() =>
        {
            mapSys.CreateMap(out var map);

            // The eater is a wounded human: devour healing must reduce its damage.
            eater = entMan.SpawnEntity("MobHuman", new MapCoordinates(0, 0, map));
            entMan.AddComponent<MegafaunaDevourComponent>(eater);
            var bite = new DamageSpecifier();
            bite.DamageDict["Blunt"] = 40;
            damageable.TryChangeDamage(eater, bite, true);
            damageBefore = entMan.GetComponent<DamageableComponent>(eater).Damage.GetTotal();

            corpse = entMan.SpawnEntity("MobHuman", new MapCoordinates(0.5f, 0, map));
            Kill(mobState, corpse);
        });

        await server.WaitRunTicks(5);

        await server.WaitAssertion(() =>
        {
            Assert.That(mobState.IsDead(corpse), Is.True, "victim should be dead");
            Assert.That(entMan.HasComponent<MegafaunaGuttedComponent>(corpse), Is.True,
                "corpse should be marked gutted");
            Assert.That(body.TryGetBodyOrganEntityComps<HeartComponent>(corpse, out _), Is.False,
                "heart should be removed");
            Assert.That(body.TryGetBodyOrganEntityComps<LungComponent>(corpse, out _), Is.False,
                "lungs should be removed");

            var damageAfter = entMan.GetComponent<DamageableComponent>(eater).Damage.GetTotal();
            Assert.That(damageAfter, Is.LessThan(damageBefore),
                $"eater should heal after devouring (before {damageBefore}, after {damageAfter})");
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task DistanceCorpseIsNotDevoured()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var entMan = server.ResolveDependency<IEntityManager>();
        var mapSys = server.System<SharedMapSystem>();
        var mobState = server.System<MobStateSystem>();
        var damageable = server.System<DamageableSystem>();

        EntityUid eater = default;
        EntityUid corpse = default;

        await server.WaitPost(() =>
        {
            mapSys.CreateMap(out var map);

            eater = entMan.SpawnEntity("MobHuman", new MapCoordinates(0, 0, map));
            entMan.AddComponent<MegafaunaDevourComponent>(eater);

            corpse = entMan.SpawnEntity("MobHuman", new MapCoordinates(50, 0, map));
            Kill(mobState, corpse);
        });

        await server.WaitRunTicks(5);

        await server.WaitAssertion(() =>
        {
            var dmg = entMan.GetComponent<DamageableComponent>(corpse).Damage.GetTotal();
            Assert.That(mobState.IsDead(corpse), Is.True, $"distant corpse not dead: damage {dmg}");
            Assert.That(entMan.HasComponent<MegafaunaGuttedComponent>(corpse), Is.False,
                "a distant corpse must not be devoured");
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task BodylessCorpseIsDevouredWithoutResolveError()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var entMan = server.ResolveDependency<IEntityManager>();
        var logMan = server.ResolveDependency<ILogManager>();
        var mapSys = server.System<SharedMapSystem>();
        var mobState = server.System<MobStateSystem>();

        EntityUid eater = default;
        EntityUid corpse = default;

        var errors = new ErrorLogCapture();
        logMan.RootSawmill.AddHandler(errors);

        try
        {
            await server.WaitPost(() =>
            {
                mapSys.CreateMap(out var map);

                eater = entMan.SpawnEntity("MobHuman", new MapCoordinates(0, 0, map));
                entMan.AddComponent<MegafaunaDevourComponent>(eater);

                // A simple mob: devourable because it is dead and has mob thresholds, but it has
                // no BodyComponent, so it has no organs to remove.
                corpse = entMan.SpawnEntity("LavalandMobRaptorBabyGreen", new MapCoordinates(0.5f, 0, map));
                Assert.That(entMan.HasComponent<BodyComponent>(corpse), Is.False,
                    "the raptor hatchling must have no BodyComponent for this test to reproduce the bug");
                Kill(mobState, corpse);
            });

            await server.WaitRunTicks(5);

            await server.WaitAssertion(() =>
            {
                Assert.That(mobState.IsDead(corpse), Is.True, "victim should be dead");
                Assert.That(entMan.HasComponent<MegafaunaGuttedComponent>(corpse), Is.True,
                    "bodyless corpse should be marked gutted");

                var resolveErrors = errors.Snapshot()
                    .Where(message => message.Contains(nameof(BodyComponent)))
                    .ToArray();
                Assert.That(resolveErrors, Is.Empty,
                    $"the devour tried to get organs from a bodyless mob: {string.Join('\n', resolveErrors)}");
            });
        }
        finally
        {
            logMan.RootSawmill.RemoveHandler(errors);
        }

        await pair.CleanReturnAsync();
    }

    /// <summary>
    /// Kills the victim directly: damage application caps at the Critical threshold
    /// in this fork, so the state is changed the way the game does it on lethal hits.
    /// </summary>
    private static void Kill(MobStateSystem mobState, EntityUid victim)
    {
        mobState.ChangeMobState(victim, Content.Shared.Mobs.MobState.Dead);
    }

    private sealed class ErrorLogCapture : ILogHandler
    {
        public readonly List<string> Messages = [];

        public void Log(string sawmillName, LogEvent message)
        {
            if (message.Level < LogEventLevel.Error)
                return;

            lock (Messages)
                Messages.Add(message.RenderMessage());
        }

        public string[] Snapshot()
        {
            lock (Messages)
                return Messages.ToArray();
        }
    }
}
