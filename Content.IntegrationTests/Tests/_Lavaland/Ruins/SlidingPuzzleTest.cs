// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Linq;
using Content.Server._Lavaland.Ruins;
using Content.Shared.Damage;
using Content.Shared.Damage.Systems;
using Content.Shared.Interaction;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;

namespace Content.IntegrationTests.Tests._Lavaland.Ruins;

/// <summary>
/// TG sliding puzzle parity: the room spawns eight fragment pillars and one
/// free slot, breaking a pillar voids the reward, and sliding every fragment
/// home spawns the puzzling chest.
/// </summary>
[TestFixture]
public sealed class SlidingPuzzleTest
{
    [Test]
    public async Task RoomSpawnsSolvableLayout()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var entMan = server.ResolveDependency<IEntityManager>();
        var mapSys = server.System<SharedMapSystem>();

        EntityUid spawner = default;

        await server.WaitPost(() =>
        {
            mapSys.CreateMap(out var map);
            spawner = entMan.SpawnEntity(null, new MapCoordinates(0, 0, map));
            entMan.AddComponent<PuzzleRoomSpawnerComponent>(spawner);
        });

        await server.WaitRunTicks(2);

        var puzzle = entMan.GetComponent<PuzzleRoomSpawnerComponent>(spawner);
        Assert.That(puzzle.Placement.Count, Is.EqualTo(8), "eight pillars should be placed");
        Assert.That(puzzle.Placement.Values.Distinct().Count(), Is.EqualTo(8), "pillars should be distinct");
        Assert.That(puzzle.Solved, Is.False);

        // Solvable by construction: fragments are distinct and the empty slot exists.
        var emptySlots = Enumerable.Range(0, 9).Where(s => !puzzle.Placement.ContainsKey(s)).ToList();
        Assert.That(emptySlots.Count, Is.EqualTo(1), "exactly one slot must stay free");

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task BreakingPillarVoidsReward()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var entMan = server.ResolveDependency<IEntityManager>();
        var mapSys = server.System<SharedMapSystem>();
        var damageable = server.System<DamageableSystem>();

        EntityUid spawner = default;

        await server.WaitPost(() =>
        {
            mapSys.CreateMap(out var map);
            spawner = entMan.SpawnEntity(null, new MapCoordinates(0, 0, map));
            entMan.AddComponent<PuzzleRoomSpawnerComponent>(spawner);
        });

        await server.WaitRunTicks(2);

        var puzzle = entMan.GetComponent<PuzzleRoomSpawnerComponent>(spawner);
        var victim = puzzle.Placement.Values.First();

        await server.WaitPost(() =>
        {
            var hit = new DamageSpecifier();
            hit.DamageDict["Blunt"] = 200;
            damageable.TryChangeDamage(victim, hit);
        });

        await server.WaitRunTicks(5);

        Assert.That(puzzle.Broken, Is.True, "destroying a pillar must void the puzzle");
        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task SolvingSpawnsChest()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var entMan = server.ResolveDependency<IEntityManager>();
        var mapSys = server.System<SharedMapSystem>();

        EntityUid spawner = default;
        EntityUid human = default;

        await server.WaitPost(() =>
        {
            mapSys.CreateMap(out var map);
            var coords = new MapCoordinates(0, 0, map);
            spawner = entMan.SpawnEntity(null, coords);
            entMan.AddComponent<PuzzleRoomSpawnerComponent>(spawner);
            human = entMan.SpawnEntity("MobHuman", coords.Offset(new System.Numerics.Vector2(5, 0)));
        });

        await server.WaitRunTicks(2);

        var puzzle = entMan.GetComponent<PuzzleRoomSpawnerComponent>(spawner);

        // Restore the solved layout: every fragment back on its own slot, so
        // slot 8 (the default EmptySlot) becomes the free one wherever the
        // shuffle actually left it.
        foreach (var (_, pillar) in puzzle.Placement.ToArray())
        {
            if (!entMan.TryGetComponent<SlidingPuzzlePillarComponent>(pillar, out var comp))
                continue;
            comp.SlotIndex = comp.FragmentIndex;
            puzzle.Placement[comp.FragmentIndex] = pillar;
        }
        for (var s = 0; s < 9; s++)
        {
            if (!puzzle.Placement.ContainsKey(s) || !entMan.TryGetComponent<SlidingPuzzlePillarComponent>(puzzle.Placement[s], out var c) || c.FragmentIndex != s)
                puzzle.Placement.Remove(s);
        }

        // Slot 8 is free by default and the pillar with fragment 5 is its neighbour.
        Assert.That(puzzle.Placement.ContainsKey(5), Is.True, "fragment 5 should be placed");
        var mover = puzzle.Placement[5];

        // First click slides fragment 5 into the free slot (5 becomes free).
        await server.WaitPost(() =>
        {
            entMan.EventBus.RaiseLocalEvent(mover, new InteractHandEvent(human, mover));
        });

        await server.WaitRunTicks(2);

        var moverComp = entMan.GetComponent<SlidingPuzzlePillarComponent>(mover);
        Assert.That(moverComp.SlotIndex, Is.Not.EqualTo(5),
            $"the first click must slide fragment 5 out of slot 5 (placement keys: {string.Join(",", puzzle.Placement.Keys.OrderBy(k => k))}, empty after shuffle was not 8)");

        Assert.That(puzzle.Solved, Is.False, "one slide must not solve the puzzle");

        // Second click slides it back: every fragment sits on its slot now.
        await server.WaitPost(() =>
        {
            entMan.EventBus.RaiseLocalEvent(mover, new InteractHandEvent(human, mover));
        });

        await server.WaitRunTicks(3);

        Assert.That(puzzle.Solved, Is.True, "sliding every fragment home must solve the puzzle");
        Assert.That(entMan.EntityExists(mover), Is.False, "the pillars should collapse on solve");

        var chests = 0;
        var query = entMan.EntityQueryEnumerator<MetaDataComponent>();
        while (query.MoveNext(out var meta))
        {
            if (meta.EntityPrototype?.ID == "LavalandPuzzleCrate")
                chests++;
        }

        Assert.That(chests, Is.EqualTo(1), "solving must spawn exactly one puzzling chest");

        await pair.CleanReturnAsync();
    }
}
