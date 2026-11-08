// SPDX-License-Identifier: LicenseRef-MiniStation-1.0

using System.Numerics;
using Content.Shared._Lavaland.Elites.Components;
using Content.Shared._Lavaland.Elites.Events;
using Content.Shared.Actions.Components;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Robust.Shared.Network;

namespace Content.Server._Lavaland.Elites;

/// <summary>
/// Elite kit actions: broodmother children and tentacle patches, legionnaire turrets.
/// </summary>
public sealed class EliteAbilitiesSystem : EntitySystem
{
    [Dependency] private readonly INetManager _net = default!;
    [Dependency] private readonly SharedTransformSystem _xform = default!;
    [Dependency] private readonly MobStateSystem _mobState = default!;

    private static readonly Vector2[] PatchOffsets =
    {
        new(0, 0),
        new(1, 0),
        new(-1, 0),
        new(0, 1),
        new(0, -1),
    };

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<ActionsComponent, SpawnBroodActionEvent>(OnSpawnBrood);
        SubscribeLocalEvent<ActionsComponent, SpawnTentaclePatchActionEvent>(OnSpawnTentaclePatch);
        SubscribeLocalEvent<ActionsComponent, SpawnTurretActionEvent>(OnSpawnTurret);
    }

    private void OnSpawnBrood(Entity<ActionsComponent> ent, ref SpawnBroodActionEvent args)
    {
        if (args.Handled || _net.IsClient)
            return;

        args.Handled = true;

        var alive = 0;
        var query = EntityQueryEnumerator<EliteChildComponent, MobStateComponent>();
        while (query.MoveNext(out var uid, out var child, out var mobState))
        {
            if (child.Parent == ent.Owner && _mobState.IsAlive(uid, mobState))
                alive++;
        }

        var count = Math.Min(args.Count, args.MaxChildren - alive);
        for (var i = 0; i < count; i++)
        {
            var child = Spawn(args.Child, Transform(ent.Owner).Coordinates);
            EnsureComp<EliteChildComponent>(child).Parent = ent.Owner;
        }
    }

    private void OnSpawnTentaclePatch(Entity<ActionsComponent> ent, ref SpawnTentaclePatchActionEvent args)
    {
        if (args.Handled || _net.IsClient || _xform.GetGrid(args.Target) == null)
            return;

        args.Handled = true;

        foreach (var offset in PatchOffsets)
            Spawn(args.Tentacle, args.Target.Offset(offset));
    }

    private void OnSpawnTurret(Entity<ActionsComponent> ent, ref SpawnTurretActionEvent args)
    {
        if (args.Handled || _net.IsClient || _xform.GetGrid(args.Target) == null)
            return;

        args.Handled = true;

        var alive = 0;
        var query = EntityQueryEnumerator<LegionTurretComponent>();
        while (query.MoveNext(out var uid, out var turret))
        {
            if (turret.PlacedBy == ent.Owner && !TerminatingOrDeleted(uid))
                alive++;
        }

        if (alive >= args.MaxTurrets)
            return;

        var spawned = Spawn(args.Turret, args.Target);
        EnsureComp<LegionTurretComponent>(spawned).PlacedBy = ent.Owner;
    }
}
