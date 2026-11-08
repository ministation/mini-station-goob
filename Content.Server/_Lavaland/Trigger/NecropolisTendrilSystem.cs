// SPDX-License-Identifier: LicenseRef-MiniStation-1.0

using Content.Server.Destructible;
using Content.Shared.Actions;
using Content.Shared.Mobs.Components;
using Content.Shared.Weapons.Melee.Events;
using Robust.Shared.Map.Components;

namespace Content.Server._Lavaland.Trigger;

public sealed class NecropolisTendrilSystem : EntitySystem
{
    [Dependency] private readonly SharedActionsSystem _actions = default!;
    [Dependency] private readonly SharedMapSystem _map = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<NecropolisTendrilComponent, AttackedEvent>(OnAttacked);
        SubscribeLocalEvent<NecropolisTendrilComponent, DamageThresholdReached>(OnThresholdReached);
    }

    private void OnAttacked(Entity<NecropolisTendrilComponent> ent, ref AttackedEvent args)
    {
        if (args.User == ent.Owner || !HasComp<MobStateComponent>(args.User))
            return;

        if (!_actions.TryGetActionById(ent.Owner, ent.Comp.RetaliateAction, out var action))
            return;

        var ev = new RequestPerformActionEvent(GetNetEntity(action.Value.Owner), GetNetCoordinates(Transform(ent).Coordinates));
        _actions.TryPerformAction(ent.Owner, ev);
    }

    private void OnThresholdReached(Entity<NecropolisTendrilComponent> ent, ref DamageThresholdReached args)
    {
        var xform = Transform(ent);
        if (xform.GridUid is not { } gridUid || !TryComp<MapGridComponent>(gridUid, out var grid))
            return;

        var centre = _map.TileIndicesFor(gridUid, grid, xform.Coordinates);
        var radius = ent.Comp.InfectionRadius;
        var rocks = new List<EntityUid>();

        for (var x = -radius; x <= radius; x++)
        {
            for (var y = -radius; y <= radius; y++)
            {
                // The tendril leaves a chasm in its own 3x3, only the ring around it is infected.
                if (Math.Abs(x) <= 1 && Math.Abs(y) <= 1)
                    continue;

                var tile = new Vector2i(centre.X + x, centre.Y + y);
                foreach (var uid in _map.GetAnchoredEntities(gridUid, grid, tile))
                {
                    if (IsRock(uid))
                        rocks.Add(uid);
                }
            }
        }

        foreach (var rock in rocks)
        {
            var coords = Transform(rock).Coordinates;
            QueueDel(rock);
            Spawn(ent.Comp.InfectionWall, coords);
        }
    }

    private bool IsRock(EntityUid uid)
    {
        var id = MetaData(uid).EntityPrototype?.ID;
        // shortcut: rock is matched by prototype id, switch to a tag if rock ids stop using the WallRock prefix
        return id != null && id.StartsWith("WallRock", StringComparison.Ordinal);
    }
}
