// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Numerics;
using Content.Shared.Destructible;
using Content.Shared.Interaction;
using Robust.Shared.Map;
using Robust.Shared.Random;

namespace Content.Server._Lavaland.Ruins;

/// <summary>
/// The TG lavaland sliding puzzle: eight pillars with picture fragments stand
/// on a 3x3 patch, one slot is free. Clicking a pillar slides it into the free
/// neighbouring slot; when every fragment is in place, the puzzling chest
/// appears in the last slot. Breaking a pillar voids the reward.
/// TG parity: code/modules/mapfluff/ruins/lavalandruin_code/puzzle.dm.
/// Fragment sprites come from the nine pillar prototypes
/// LavalandPuzzlePillar0..8, one per fragment.
/// </summary>
public sealed class SlidingPuzzleSystem : EntitySystem
{
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly IRobustRandom _random = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<PuzzleRoomSpawnerComponent, MapInitEvent>(OnSpawnerMapInit);
        SubscribeLocalEvent<SlidingPuzzlePillarComponent, InteractHandEvent>(OnPillarClicked);
        SubscribeLocalEvent<SlidingPuzzlePillarComponent, DestructionEventArgs>(OnPillarDestroyed);
    }

    private void OnSpawnerMapInit(Entity<PuzzleRoomSpawnerComponent> ent, ref MapInitEvent args)
    {
        // No self-delete: the prototype sweep requires every entity to stay deletable.
        var puzzle = ent.Comp;
        puzzle.Puzzle = ent;
        puzzle.BaseCoords = Transform(ent).Coordinates;

        // Start from the solved layout and shuffle with legal slides backwards:
        // the result is always solvable, like the TG inversion-parity check.
        for (var slot = 0; slot < 9; slot++)
        {
            var cell = SlotCoords(puzzle, slot);
            Spawn(puzzle.FloorPrototype, cell);

            if (slot == puzzle.EmptySlot)
                continue;

            var pillar = Spawn($"{puzzle.PillarPrototype}{slot}", cell);
            var pillarComp = new SlidingPuzzlePillarComponent
            {
                FragmentIndex = slot,
                SlotIndex = slot,
            };
            pillarComp.Puzzle = ent;
            AddComp(pillar, pillarComp);
            puzzle.Placement[slot] = pillar;
        }

        for (var shuffle = 0; shuffle < 50; shuffle++)
        {
            SlideRandomPillar(puzzle);
        }
    }

    private void SlideRandomPillar(PuzzleRoomSpawnerComponent puzzle)
    {
        if (FindEmptySlot(puzzle) is not { } empty)
            return;

        var emptyX = empty % 3;
        var emptyY = empty / 3;
        var candidates = new List<int>();
        foreach (var (dx, dy) in new[] { (1, 0), (-1, 0), (0, 1), (0, -1) })
        {
            var x = emptyX + dx;
            var y = emptyY + dy;
            if (x < 0 || x > 2 || y < 0 || y > 2)
                continue;

            var neighbour = y * 3 + x;
            if (puzzle.Placement.ContainsKey(neighbour))
                candidates.Add(neighbour);
        }

        if (candidates.Count == 0)
            return;

        var from = _random.Pick(candidates);
        var pillar = puzzle.Placement[from];
        puzzle.Placement.Remove(from);
        puzzle.Placement[empty] = pillar;

        if (TryComp<SlidingPuzzlePillarComponent>(pillar, out var comp))
            comp.SlotIndex = empty;
    }

    private EntityCoordinates SlotCoords(PuzzleRoomSpawnerComponent puzzle, int slot)
    {
        var x = slot % 3 - 1;
        var y = 1 - slot / 3;
        return puzzle.BaseCoords.Offset(new Vector2(x, y));
    }

    private int? FindEmptySlot(PuzzleRoomSpawnerComponent puzzle)
    {
        for (var slot = 0; slot < 9; slot++)
        {
            if (!puzzle.Placement.ContainsKey(slot))
                return slot;
        }

        return null;
    }

    private void OnPillarClicked(Entity<SlidingPuzzlePillarComponent> ent, ref InteractHandEvent args)
    {
        if (args.Handled)
            return;

        var pillar = ent.Comp;
        if (pillar.Puzzle is not { } puzzleOwner || Deleted(puzzleOwner))
            return;
        var puzzle = Comp<PuzzleRoomSpawnerComponent>(puzzleOwner);
        if (puzzle.Solved || puzzle.Broken)
            return;

        if (pillar.SlotIndex is not { } from || from < 0 || from > 8)
            return;

        // Slide into an orthogonally adjacent free slot, like the TG push.
        foreach (var (dx, dy) in new[] { (1, 0), (-1, 0), (0, 1), (0, -1) })
        {
            var x = from % 3 + dx;
            var y = from / 3 + dy;
            if (x < 0 || x > 2 || y < 0 || y > 2)
                continue;

            var neighbour = y * 3 + x;
            if (puzzle.Placement.ContainsKey(neighbour))
                continue;

            args.Handled = true;

            puzzle.Placement.Remove(from);
            puzzle.Placement[neighbour] = ent;
            pillar.SlotIndex = neighbour;
            _transform.SetCoordinates(ent, SlotCoords(puzzle, neighbour));

            Validate((puzzleOwner, puzzle));
            return;
        }
    }

    private void Validate(Entity<PuzzleRoomSpawnerComponent> ent)
    {
        var puzzle = ent.Comp;

        // Every fragment must sit on its matching slot.
        foreach (var (slot, pillar) in puzzle.Placement)
        {
            if (!TryComp<SlidingPuzzlePillarComponent>(pillar, out var comp) || comp.FragmentIndex != slot)
                return;
        }

        puzzle.Solved = true;

        // TG: the pillars collapse and the picture burns into the floor.
        foreach (var (_, pillar) in puzzle.Placement)
        {
            if (!Deleted(pillar))
                QueueDel(pillar);
        }
        puzzle.Placement.Clear();

        Spawn(puzzle.ChestPrototype, SlotCoords(puzzle, puzzle.EmptySlot));
    }

    private void OnPillarDestroyed(Entity<SlidingPuzzlePillarComponent> ent, ref DestructionEventArgs args)
    {
        // TG: breaking a pillar qdels the puzzle generator and voids the reward.
        if (ent.Comp.Puzzle is not { } puzzleOwner || Deleted(puzzleOwner))
            return;

        var puzzle = Comp<PuzzleRoomSpawnerComponent>(puzzleOwner);
        if (puzzle.Solved)
            return;

        puzzle.Broken = true;
        if (ent.Comp.SlotIndex is { } slot)
            puzzle.Placement.Remove(slot);
    }
}
