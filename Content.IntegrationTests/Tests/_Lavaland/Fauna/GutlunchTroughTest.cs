// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Linq;
using System.Numerics;
using Content.Server._Lavaland.Fauna;
using Content.Shared._Lavaland.Fauna;
using Content.Shared.Interaction;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Timing;

namespace Content.IntegrationTests.Tests._Lavaland.Fauna;

/// <summary>
/// TG gutlunch parity: ore troughs and the feeding loop, grubs growing into a
/// gunther or a gubbuck, and trough breeding. Milking itself reuses the
/// wizden cow udder and is covered by its own tests.
/// </summary>
[TestFixture]
public sealed class GutlunchTroughTest
{
    [Test]
    public async Task TroughFeeding()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var entMan = server.ResolveDependency<IEntityManager>();
        var mapSys = server.System<SharedMapSystem>();
        var timing = server.ResolveDependency<IGameTiming>();

        EntityUid trough = default;
        EntityUid gubbuck = default;
        EntityUid user = default;
        MapCoordinates coords = default;

        await server.WaitPost(() =>
        {
            mapSys.CreateMap(out var map);
            coords = new MapCoordinates(0, 0, map);
        });

        await server.WaitRunTicks(2);

        await server.WaitPost(() =>
        {
            trough = entMan.SpawnEntity("LavalandFoodTrough", coords);
            user = entMan.SpawnEntity("MobHuman", coords.Offset(new Vector2(5, 0)));
        });

        await server.WaitRunTicks(2);

        // Filling the trough with one ore piece.
        await server.WaitPost(() =>
        {
            var ore = entMan.SpawnEntity("GoldOre", coords.Offset(new Vector2(1, 0)));
            entMan.EventBus.RaiseLocalEvent(
                trough, new InteractUsingEvent(user, ore, trough, entMan.GetComponent<TransformComponent>(trough).Coordinates));
        });

        await server.WaitRunTicks(1);
        Assert.That(entMan.GetComponent<FoodTroughComponent>(trough).OreCount, Is.EqualTo(1),
            "the trough should accept one ore piece");

        // A nearby gubbuck eats from the trough on the next feeder tick.
        await server.WaitPost(() =>
        {
            gubbuck = entMan.SpawnEntity("MobLavalandGutlunchMilk", coords.Offset(new Vector2(1, 0)));
        });

        await server.WaitRunTicks(180);

        Assert.That(entMan.GetComponent<FoodTroughComponent>(trough).OreCount, Is.EqualTo(0),
            "the gutlunch should have eaten the ore");
        Assert.That(entMan.GetComponent<TroughFeederComponent>(gubbuck).FedUntil,
            Is.GreaterThan(timing.CurTime),
            "the gubbuck should count as fed");

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task GrubGrowsIntoAdult()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var entMan = server.ResolveDependency<IEntityManager>();
        var mapSys = server.System<SharedMapSystem>();
        var timing = server.ResolveDependency<IGameTiming>();

        EntityUid grub = default;
        MapCoordinates coords = default;
        MapId mapId = default;

        await server.WaitPost(() =>
        {
            mapSys.CreateMap(out mapId);
            coords = new MapCoordinates(0, 0, mapId);
        });

        await server.WaitRunTicks(2);

        await server.WaitPost(() =>
        {
            grub = entMan.SpawnEntity("MobLavalandGutlunchGrub", coords);
        });

        await server.WaitRunTicks(2);

        await server.WaitPost(() =>
        {
            entMan.GetComponent<FaunaGrowthComponent>(grub).NextGrowthTime = timing.CurTime;
        });

        await server.WaitRunTicks(5);

        Assert.That(entMan.EntityExists(grub), Is.False, "the grub should be replaced by the grown adult");

        // Other tests may leave gutlunches on other maps — only count ours.
        var adults = 0;
        EntityUid grown = default;
        var query = entMan.EntityQueryEnumerator<GutlunchComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out _, out var tr))
        {
            if (tr.MapID != mapId)
                continue;
            adults++;
            grown = uid;
        }

        Assert.That(adults, Is.EqualTo(1), "exactly one grown gutlunch should replace the grub");
        var protoId = entMan.GetComponent<MetaDataComponent>(grown).EntityPrototype!.ID;
        Assert.That(
            protoId,
            Is.AnyOf("MobLavalandGutlunchWarrior", "MobLavalandGutlunchMilk"),
            "a grub grows into a gunther or a gubbuck");

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task TroughBreedingSpawnsGrub()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var entMan = server.ResolveDependency<IEntityManager>();
        var mapSys = server.System<SharedMapSystem>();
        var timing = server.ResolveDependency<IGameTiming>();

        EntityUid trough = default;
        MapCoordinates coords = default;
        MapId mapId = default;

        await server.WaitPost(() =>
        {
            mapSys.CreateMap(out mapId);
            coords = new MapCoordinates(0, 0, mapId);
            trough = entMan.SpawnEntity("LavalandFoodTrough", coords);
            entMan.SpawnEntity("MobLavalandGutlunchMilk", coords.Offset(new Vector2(1, 0)));
            entMan.SpawnEntity("MobLavalandGutlunchWarrior", coords.Offset(new Vector2(-1, 0)));
        });

        await server.WaitRunTicks(2);

        // Stock the trough: the parents eat the ore, and fed parents breed.
        await server.WaitPost(() =>
        {
            entMan.GetComponent<FoodTroughComponent>(trough).OreCount = 4;
        });

        await server.WaitRunTicks(360);

        // Other tests may leave gutlunches around — the trough cooldown proves
        // a breed happened, and the grub shows up on our map.
        Assert.That(entMan.GetComponent<FoodTroughComponent>(trough).NextBreed,
            Is.GreaterThan(timing.CurTime), "the trough should start its breeding cooldown");

        var grubsOnMap = 0;
        var query = entMan.EntityQueryEnumerator<FaunaGrowthComponent, TransformComponent>();
        while (query.MoveNext(out _, out _, out var tr))
        {
            if (tr.MapID == mapId)
                grubsOnMap++;
        }

        Assert.That(grubsOnMap, Is.GreaterThanOrEqualTo(1),
            "a mother and a partner at an ore trough should produce one grub");

        await pair.CleanReturnAsync();
    }
}
