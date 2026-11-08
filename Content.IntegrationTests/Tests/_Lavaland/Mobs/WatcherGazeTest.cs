// SPDX-License-Identifier: AGPL-3.0-or-later

#nullable enable annotations

using System.Numerics;
using Content.Shared._Lavaland.Mobs;
using Content.Shared.Atmos.Components;
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
    // 1.6 s warning delay plus a few ticks of margin at 30 tps.
    private const int Ticks = 90;

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
            var tile = testMap.Tile.Tile;
            for (var x = -4; x <= 4; x++)
            {
                for (var y = -4; y <= 4; y++)
                    mapSys.SetTile(testMap.Grid, new Vector2i(x, y), tile);
            }

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
