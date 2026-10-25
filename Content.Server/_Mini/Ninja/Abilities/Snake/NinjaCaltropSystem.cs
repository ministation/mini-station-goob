// SPDX-FileCopyrightText: 2026 Mini Station
// Мини-станция, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/ministation/mini-station-goob/master/LICENSE.TXT

using System.Numerics;
using Content.Server._Mini.Ninja.Abilities.Ghost;
using Content.Shared._Mini.Ninja.Abilities.Snake;
using Content.Shared._Mini.Ninja.Systems;
using Content.Shared.Coordinates.Helpers;
using Content.Shared.Maps;
using Content.Shared.Physics;
using Robust.Shared.Map;
using Robust.Shared.Physics;
using Robust.Shared.Physics.Components;

namespace Content.Server._Mini.Ninja.Abilities.Snake;

/// <summary>
/// Snake tier 4: places a row of energy caltrops behind the ninja.
/// </summary>
public sealed class NinjaCaltropSystem : EntitySystem
{
    [Dependency] private readonly NinjaSuitPowerSystem _suitPower = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly TurfSystem _turf = default!;
    [Dependency] private readonly NinjaGhostSmokeSystem _ghostSmoke = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<NinjaCaltropComponent, NinjaCaltropAbilityEvent>(OnPlaceCaltrops);
    }

    private void OnPlaceCaltrops(Entity<NinjaCaltropComponent> ent, ref NinjaCaltropAbilityEvent args)
    {
        if (args.Handled)
            return;

        if (!_suitPower.TryGetActiveSuit(args.Performer, out _, out _))
            return;

        if (Transform(ent.Owner).ParentUid != args.Performer)
            return;

        var positions = GetSpawnPositions(args.Performer, ent.Comp);
        if (positions.Count == 0)
            return;

        if (!_suitPower.TryUseCharge(args.Performer, ent.Comp.EnergyCost))
            return;

        foreach (var position in positions)
        {
            Spawn(ent.Comp.CaltropProto, position);
        }

        // Synergy with the smoke module of the ghost path: the cloud hides the traps.
        _ghostSmoke.TryAutoSmoke(ent.Owner);

        args.Handled = true;
    }

    private List<EntityCoordinates> GetSpawnPositions(EntityUid uid, NinjaCaltropComponent comp)
    {
        var xform = Transform(uid);
        var backwards = -xform.LocalRotation.ToWorldVec();
        var side = new Vector2(-backwards.Y, backwards.X);
        var positions = new List<EntityCoordinates>();

        for (var i = -comp.SideCount; i <= comp.SideCount; i++)
        {
            var position = xform.Coordinates
                .Offset(backwards * comp.SpawnDistance + side * (i * comp.SideStep))
                .SnapToGrid();

            if (!IsTileFree(position))
                continue;

            positions.Add(position);
        }

        return positions;
    }

    private bool IsTileFree(EntityCoordinates coords)
    {
        if (!_turf.TryGetTileRef(coords, out var tileRef))
            return false;

        var entities = new HashSet<EntityUid>();
        _turf.GetEntitiesInTile(coords, entities);

        foreach (var uid in entities)
        {
            if (!TryComp<PhysicsComponent>(uid, out var physics))
                continue;

            if (physics.BodyType != BodyType.Static || !physics.Hard)
                continue;

            if ((physics.CollisionLayer & (int) CollisionGroup.Impassable) == 0)
                continue;

            return false;
        }

        return true;
    }
}