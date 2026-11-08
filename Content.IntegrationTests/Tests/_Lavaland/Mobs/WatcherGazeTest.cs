// SPDX-License-Identifier: AGPL-3.0-or-later

#nullable enable annotations

using System.Numerics;
using Content.Server.Atmos.EntitySystems;
using Content.Shared._Lavaland.Mobs;
using Content.Shared.Atmos;
using Content.Shared.Atmos.Components;
using Content.Shared.Atmos;
using Content.Server.Atmos.Components;
using Content.Server.Atmos.EntitySystems;
using Content.Shared.Flash.Components;
using Content.Shared.StatusEffectNew;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Maths;

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
}
