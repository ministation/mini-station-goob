// SPDX-License-Identifier: LicenseRef-MiniStation-1.0

#nullable enable annotations

using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Content.Server.Atmos.EntitySystems;
using Content.Server.NPC.HTN;
using Content.Shared._Lavaland.Mobs;
using Content.Shared.Atmos;
using Content.Shared.Atmos.Components;
using Content.Shared.Damage.Components;
using Content.Shared.Flash.Components;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.Projectiles;
using Content.Shared.StatusEffectNew;
using Robust.Shared.GameObjects;
using Robust.Shared.Log;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Maths;
using Serilog.Events;

namespace Content.IntegrationTests.Tests._Lavaland.Mobs;

/// <summary>
/// End-to-end check of the watcher gaze: it must fire after the warning delay and only affect the entities
/// that are looking at the watcher. A prototype-only audit cannot see a gaze that never lands.
/// </summary>
[TestFixture]
public sealed class WatcherGazeTest
{
    // The gaze lands 1.6 s of REAL time after the warning (Timer.Spawn), and the flash
    // plus burning stacks expire seconds later. The window is tight: past ~120 ticks the
    // effects burn out before the assertion, but 90 ticks can miss the timer itself.
    private const int Ticks = 120;

    [Test]
    public async Task GazeOnlyFlashesEntitiesLookingAtTheWatcher()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var entMan = server.ResolveDependency<IEntityManager>();
        var mapSys = server.System<SharedMapSystem>();
        var status = server.System<StatusEffectsSystem>();
        var transform = server.System<SharedTransformSystem>();

        var testMap = await pair.CreateTestMap();
        EntityUid watcher = default;
        EntityUid looking = default;
        EntityUid turnedAway = default;

        await server.WaitPost(() =>
        {
            var tile = testMap.Tile.Tile;
            for (var x = -4; x <= 4; x++)
            {
                for (var y = -4; y <= 4; y++)
                    mapSys.SetTile(testMap.Grid, new Vector2i(x, y), tile);
            }

            watcher = entMan.SpawnEntity("MobWatcherLavaland", testMap.GridCoords);
            looking = entMan.SpawnEntity("MobHuman", testMap.GridCoords.Offset(new Vector2(2f, 0f)));
            turnedAway = entMan.SpawnEntity("MobHuman", testMap.GridCoords.Offset(new Vector2(-2f, 0f)));

            // Both face west: the watcher is straight ahead for one of them and behind the other.
            transform.SetWorldRotation(looking, Angle.FromDegrees(180));
            transform.SetWorldRotation(turnedAway, Angle.FromDegrees(180));
        });

        await server.WaitRunTicks(1);
        await server.WaitPost(() =>
        {
            var ev = new WatcherGazeActionEvent
            {
                Performer = watcher,
                Target = looking,
            };
            entMan.EventBus.RaiseLocalEvent(watcher, ev, broadcast: true);
        });

        await server.WaitRunTicks(Ticks);

        await server.WaitAssertion(() =>
        {
            Assert.That(entMan.HasComponent<FlashedComponent>(looking), Is.True,
                "the entity looking at the watcher was not flashed by the gaze");
            Assert.That(status.HasStatusEffect(looking, "StatusEffectDrunk"), Is.True,
                "the confusion stand-in was not applied to the flashed entity");
            Assert.That(entMan.HasComponent<FlashedComponent>(turnedAway), Is.False,
                "the entity not looking at the watcher was flashed by the gaze");
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task FireVariantIgnitesTheViewer()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var entMan = server.ResolveDependency<IEntityManager>();
        var mapSys = server.System<SharedMapSystem>();
        var transform = server.System<SharedTransformSystem>();

        var testMap = await pair.CreateTestMap();
        EntityUid watcher = default;
        EntityUid victim = default;

        await server.WaitPost(() =>
        {
            // Burning needs oxygen: give the map a breathable atmosphere, space does not burn.
            var air = new GasMixture(2500f);
            air.AdjustMoles(Content.Shared.Atmos.Gas.Oxygen, 21.2f);
            air.AdjustMoles(Content.Shared.Atmos.Gas.Nitrogen, 79f);
            server.System<AtmosphereSystem>().SetMapAtmosphere(testMap.MapUid, false, air);

            var tile = testMap.Tile.Tile;
            for (var x = -4; x <= 4; x++)
            {
                for (var y = -4; y <= 4; y++)
                    mapSys.SetTile(testMap.Grid, new Vector2i(x, y), tile);
            }

            // FlammableSystem extinguishes fires in vacuum, so give the map a station-like atmosphere.
            var atmos = server.System<AtmosphereSystem>();
            var moles = new float[Atmospherics.AdjustedNumberOfGases];
            moles[(int)Gas.Oxygen] = 21.824779f;
            moles[(int)Gas.Nitrogen] = 82.10312f;
            atmos.SetMapAtmosphere(testMap.MapUid, false, new GasMixture(moles, Atmospherics.T20C));

            watcher = entMan.SpawnEntity("MobWatcherMagmawing", testMap.GridCoords);
            victim = entMan.SpawnEntity("MobHuman", testMap.GridCoords.Offset(new Vector2(2f, 0f)));
            transform.SetWorldRotation(victim, Angle.FromDegrees(180));
        });

        await server.WaitRunTicks(1);
        await server.WaitPost(() =>
        {
            var ev = new WatcherGazeActionEvent
            {
                Performer = watcher,
                Target = victim,
            };
            entMan.EventBus.RaiseLocalEvent(watcher, ev, broadcast: true);
        });

        await server.WaitRunTicks(Ticks);

        await server.WaitAssertion(() =>
        {
            Assert.That(entMan.TryGetComponent<FlammableComponent>(victim, out var flammable), Is.True,
                "the fire gaze victim has no FlammableComponent");
            Assert.That(flammable!.FireStacks, Is.GreaterThan(0f),
                "the fire gaze did not set the victim on fire");
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task IceVariantThrowsTheViewerAway()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var entMan = server.ResolveDependency<IEntityManager>();
        var mapSys = server.System<SharedMapSystem>();
        var transform = server.System<SharedTransformSystem>();

        var testMap = await pair.CreateTestMap();
        EntityUid watcher = default;
        EntityUid victim = default;
        var start = Vector2.Zero;

        await server.WaitPost(() =>
        {
            var tile = testMap.Tile.Tile;
            for (var x = -10; x <= 10; x++)
            {
                for (var y = -10; y <= 10; y++)
                    mapSys.SetTile(testMap.Grid, new Vector2i(x, y), tile);
            }

            watcher = entMan.SpawnEntity("MobWatcherIcewing", testMap.GridCoords);
            victim = entMan.SpawnEntity("MobHuman", testMap.GridCoords.Offset(new Vector2(2f, 0f)));
            transform.SetWorldRotation(victim, Angle.FromDegrees(180));
            start = transform.GetWorldPosition(victim);
        });

        await server.WaitRunTicks(1);
        await server.WaitPost(() =>
        {
            var ev = new WatcherGazeActionEvent
            {
                Performer = watcher,
                Target = victim,
            };
            entMan.EventBus.RaiseLocalEvent(watcher, ev, broadcast: true);
        });

        await server.WaitRunTicks(Ticks);

        await server.WaitAssertion(() =>
        {
            var moved = transform.GetWorldPosition(victim) - start;
            Assert.That(moved.X, Is.GreaterThan(0.5f),
                "the ice gaze did not throw the victim away from the watcher");
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task FireVariantOnNonFlammableMobDoesNotLogResolveError()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var entMan = server.ResolveDependency<IEntityManager>();
        var logMan = server.ResolveDependency<ILogManager>();
        var mapSys = server.System<SharedMapSystem>();
        var transform = server.System<SharedTransformSystem>();

        var testMap = await pair.CreateTestMap();
        EntityUid watcher = default;
        EntityUid victim = default;

        var errors = new ErrorLogCapture();
        logMan.RootSawmill.AddHandler(errors);

        try
        {
            await server.WaitPost(() =>
            {
                var tile = testMap.Tile.Tile;
                for (var x = -4; x <= 4; x++)
                {
                    for (var y = -4; y <= 4; y++)
                        mapSys.SetTile(testMap.Grid, new Vector2i(x, y), tile);
                }

                watcher = entMan.SpawnEntity("MobWatcherMagmawing", testMap.GridCoords);
                victim = entMan.SpawnEntity("MobBasilisk", testMap.GridCoords.Offset(new Vector2(2f, 0f)));
                transform.SetWorldRotation(victim, Angle.FromDegrees(180));
            });

            await server.WaitRunTicks(1);
            await server.WaitPost(() =>
            {
                // The gaze targets every living thing in range looking at the watcher, not just players.
                var ev = new WatcherGazeActionEvent
                {
                    Performer = watcher,
                    Target = victim,
                };
                entMan.EventBus.RaiseLocalEvent(watcher, ev, broadcast: true);
            });

            await server.WaitRunTicks(Ticks);

            await server.WaitAssertion(() =>
            {
                Assert.That(entMan.HasComponent<FlammableComponent>(victim), Is.False,
                    $"{entMan.GetComponent<MetaDataComponent>(victim).EntityPrototype?.ID} must be non-flammable for this test to reproduce the bug");

                var resolveErrors = errors.Snapshot()
                    .Where(message => message.Contains(nameof(FlammableComponent)))
                    .ToArray();
                Assert.That(resolveErrors, Is.Empty,
                    $"the fire gaze tried to set fire stacks on a non-flammable mob: {string.Join('\n', resolveErrors)}");
            });
        }
        finally
        {
            logMan.RootSawmill.RemoveHandler(errors);
        }

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task MagmawingGazeDoesNotBurnFullHpInOneLook()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var entMan = server.ResolveDependency<IEntityManager>();
        var mapSys = server.System<SharedMapSystem>();
        var status = server.System<StatusEffectsSystem>();
        var transform = server.System<SharedTransformSystem>();

        var testMap = await pair.CreateTestMap();
        EntityUid watcher = default;
        EntityUid victim = default;

        await server.WaitPost(() =>
        {
            var tile = testMap.Tile.Tile;
            for (var x = -6; x <= 6; x++)
            {
                for (var y = -6; y <= 6; y++)
                    mapSys.SetTile(testMap.Grid, new Vector2i(x, y), tile);
            }

            // Burning needs oxygen, a fire in vacuum is put out immediately.
            var atmos = server.System<AtmosphereSystem>();
            var moles = new float[Atmospherics.AdjustedNumberOfGases];
            moles[(int)Gas.Oxygen] = 21.824779f;
            moles[(int)Gas.Nitrogen] = 82.10312f;
            atmos.SetMapAtmosphere(testMap.MapUid, false, new GasMixture(moles, Atmospherics.T20C));

            watcher = entMan.SpawnEntity("MobWatcherMagmawing", testMap.GridCoords);

            // The watcher never shoots here: removing the HTN leaves only the gaze to be measured.
            entMan.RemoveComponent<HTNComponent>(watcher);

            victim = entMan.SpawnEntity("MobHuman", testMap.GridCoords.Offset(new Vector2(2f, 0f)));
            transform.SetWorldRotation(victim, Angle.FromDegrees(180));
        });

        await server.WaitRunTicks(1);
        await server.WaitPost(() =>
        {
            var ev = new WatcherGazeActionEvent
            {
                Performer = watcher,
                Target = victim,
            };
            entMan.EventBus.RaiseLocalEvent(watcher, ev, broadcast: true);
        });

        // 1.6 s warning, then the gaze lands.
        await server.WaitRunTicks(52);

        await server.WaitAssertion(() =>
        {
            Assert.That(entMan.TryGetComponent<FlammableComponent>(victim, out var flammable), Is.True,
                "the fire gaze victim has no FlammableComponent");
            Assert.That(flammable!.OnFire, Is.True,
                "the searing glare did not ignite the viewer");
            Assert.That(flammable.FireStacks, Is.EqualTo(2f).Within(0.25f),
                "the glare fire stacks drifted away from the tuned value");
            Assert.That(status.HasStatusEffect(victim, "StatusEffectStunned"), Is.True,
                "the searing glare did not paralyze the viewer");
        });

        // The burn is the slow part. A full health mob has to survive a single glare, so ten
        // seconds of burning must stay under one health bar.
        await server.WaitRunTicks(300);

        await server.WaitAssertion(() =>
        {
            var damageable = entMan.GetComponent<DamageableComponent>(victim);
            var mobState = entMan.GetComponent<MobStateComponent>(victim);
            Assert.That(mobState.CurrentState, Is.EqualTo(MobState.Alive),
                "one searing glare killed a full health mob");
            Assert.That(damageable.TotalDamage.Float(), Is.LessThan(100f),
                "one searing glare burned a whole health bar");
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task MagmawingBoltDamageMatchesTg()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var entMan = server.ResolveDependency<IEntityManager>();

        var testMap = await pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var bolt = entMan.SpawnEntity("WatcherBoltMagmawing", testMap.GridCoords);
            Assert.That(entMan.TryGetComponent<ProjectileComponent>(bolt, out var projectile), Is.True,
                "the magmawing bolt has no ProjectileComponent");

            // tg watcher/magma_wing: 5 burn, the only damage type on the bolt.
            Assert.That(projectile!.Damage.GetTotal().Float(), Is.EqualTo(5f),
                "the magmawing bolt damage drifted away from tg");
            Assert.That(projectile.Damage.DamageDict.TryGetValue("Heat", out var heat), Is.True,
                "the magmawing bolt lost its Heat damage type");
            Assert.That(heat.Float(), Is.EqualTo(5f),
                "the magmawing bolt Heat damage drifted away from tg");
        });

        await pair.CleanReturnAsync();
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
