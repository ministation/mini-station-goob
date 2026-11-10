// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Linq;
using System.Numerics;
using Content.Server._Lavaland.Fauna;
using Content.Server._Lavaland.Mobs;
using Content.Shared._Lavaland.Fauna;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Timing;

namespace Content.IntegrationTests.Tests._Lavaland.Fauna;

/// <summary>
/// TG raptor parity: eggs hatch into hatchlings, hatchlings grow into
/// younglings, and fed adult raptors lay eggs at food troughs.
/// </summary>
[TestFixture]
public sealed class RaptorParityTest
{
    [Test]
    public async Task EggHatchesIntoHatchling()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var entMan = server.ResolveDependency<IEntityManager>();
        var mapSys = server.System<SharedMapSystem>();
        var timing = server.ResolveDependency<IGameTiming>();

        EntityUid egg = default;
        MapId mapId = default;

        await server.WaitPost(() =>
        {
            mapSys.CreateMap(out mapId);
            egg = entMan.SpawnEntity("LavalandRaptorEgg", new MapCoordinates(0, 0, mapId));
        });

        await server.WaitRunTicks(2);

        // A laid egg knows its color and is one second away from hatching.
        await server.WaitPost(() =>
        {
            var comp = entMan.GetComponent<RaptorEggComponent>(egg);
            comp.BabyProto = "LavalandMobRaptorBabyRed";
            comp.Progress = comp.HatchSeconds - 1f;
        });

        await server.WaitRunTicks(120);

        Assert.That(entMan.EntityExists(egg), Is.False, "the egg should hatch");

        var hatchlings = 0;
        var query = entMan.EntityQueryEnumerator<RaptorComponent, TransformComponent>();
        while (query.MoveNext(out _, out _, out var tr))
        {
            if (tr.MapID == mapId)
                hatchlings++;
        }

        Assert.That(hatchlings, Is.EqualTo(1), "hatching should spawn one raptor hatchling");
        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task BabyGrowsIntoYoungling()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var entMan = server.ResolveDependency<IEntityManager>();
        var mapSys = server.System<SharedMapSystem>();
        var timing = server.ResolveDependency<IGameTiming>();

        EntityUid baby = default;
        MapId mapId = default;

        await server.WaitPost(() =>
        {
            mapSys.CreateMap(out mapId);
            baby = entMan.SpawnEntity("LavalandMobRaptorBabyRed", new MapCoordinates(0, 0, mapId));
        });

        await server.WaitRunTicks(2);

        await server.WaitPost(() =>
        {
            entMan.GetComponent<FaunaGrowthComponent>(baby).NextGrowthTime = timing.CurTime;
        });

        await server.WaitRunTicks(5);

        Assert.That(entMan.EntityExists(baby), Is.False, "the hatchling should grow into a youngling");

        var grown = 0;
        var query = entMan.EntityQueryEnumerator<RaptorComponent, TransformComponent>();
        while (query.MoveNext(out _, out _, out var tr))
        {
            if (tr.MapID == mapId)
                grown++;
        }

        Assert.That(grown, Is.EqualTo(1), "exactly one youngling should replace the hatchling");
        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task TamingSurvivesGrowth()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var entMan = server.ResolveDependency<IEntityManager>();
        var mapSys = server.System<SharedMapSystem>();
        var timing = server.ResolveDependency<IGameTiming>();

        EntityUid baby = default;
        EntityUid tamer = default;
        MapId mapId = default;

        await server.WaitPost(() =>
        {
            mapSys.CreateMap(out mapId);
            var coords = new MapCoordinates(0, 0, mapId);
            tamer = entMan.SpawnEntity("MobHuman", coords);
            baby = entMan.SpawnEntity("LavalandMobRaptorBabyRed", coords.Offset(new Vector2(1.5f, 0)));

            entMan.AddComponent<TamedMobComponent>(baby).Tamer = tamer;
            entMan.GetComponent<FaunaGrowthComponent>(baby).NextGrowthTime = timing.CurTime;
        });

        await server.WaitRunTicks(5);

        await server.WaitAssertion(() =>
        {
            Assert.That(entMan.EntityExists(baby), Is.False, "the tamed hatchling should grow into a youngling");

            var grown = EntityUid.Invalid;
            var query = entMan.EntityQueryEnumerator<RaptorComponent, TransformComponent>();
            while (query.MoveNext(out var uid, out _, out var xform))
            {
                if (xform.MapID == mapId)
                    grown = uid;
            }

            Assert.That(grown, Is.Not.EqualTo(EntityUid.Invalid), "growth should spawn one youngling");
            Assert.That(entMan.GetComponent<TamedMobComponent>(grown).Tamer, Is.EqualTo(tamer),
                "growing up must keep the raptor tamed by the same player");
            Assert.That(entMan.HasComponent<TameableMobComponent>(grown), Is.False,
                "a grown tamed raptor must not be tameable with food again");
            Assert.That(entMan.GetComponent<MountableMobComponent>(grown).Saddled, Is.True,
                "a tamed raptor must stay rideable without a saddle after growing");
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task FedRaptorsLayEggAtTrough()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var entMan = server.ResolveDependency<IEntityManager>();
        var mapSys = server.System<SharedMapSystem>();
        var timing = server.ResolveDependency<IGameTiming>();

        EntityUid trough = default;
        MapId mapId = default;

        await server.WaitPost(() =>
        {
            mapSys.CreateMap(out mapId);
            var coords = new MapCoordinates(0, 0, mapId);
            trough = entMan.SpawnEntity("LavalandFoodTrough", coords);
            entMan.SpawnEntity("LavalandMobRaptorRed", coords.Offset(new Vector2(1, 0)));
            entMan.SpawnEntity("LavalandMobRaptorWhite", coords.Offset(new Vector2(-1, 0)));
        });

        await server.WaitRunTicks(2);

        // Stock the trough: the raptors eat, and fed raptors lay an egg.
        await server.WaitPost(() =>
        {
            entMan.GetComponent<FoodTroughComponent>(trough).OreCount = 4;
        });

        await server.WaitRunTicks(360);

        Assert.That(entMan.GetComponent<FoodTroughComponent>(trough).NextRaptorBreed,
            Is.GreaterThan(timing.CurTime), "the trough should start its raptor breeding cooldown");

        var eggs = 0;
        EntityUid eggProto = default;
        var query = entMan.EntityQueryEnumerator<RaptorEggComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var eggComp, out _))
        {
            eggs++;
            eggProto = uid;
        }

        Assert.That(eggs, Is.EqualTo(1), "two fed adult raptors at a trough should lay one egg");
        Assert.That(entMan.GetComponent<RaptorEggComponent>(eggProto).BabyProto?.Id,
            Is.AnyOf("LavalandMobRaptorBabyRed", "LavalandMobRaptorBabyWhite"),
            "the egg inherits the color of its parents");

        await pair.CleanReturnAsync();
    }
}
