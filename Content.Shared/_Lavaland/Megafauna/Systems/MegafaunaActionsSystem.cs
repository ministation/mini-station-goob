// SPDX-FileCopyrightText: 2026 Space Station 14 Contributors
//
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Numerics;
using Content.Shared._Lavaland.Megafauna.Events;
using Content.Shared._Lavaland.Movement;
using Content.Shared.Actions.Components;
using Robust.Shared.Map;
using Robust.Shared.Network;

namespace Content.Shared._Lavaland.Megafauna.Systems;

/// <summary>
/// Handles general actions that are useful for all megafauna bosses.
/// </summary>
public sealed class MegafaunaActionsSystem : EntitySystem
{
    [Dependency] private readonly SharedTransformSystem _xform = default!;
    [Dependency] private readonly INetManager _net = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<ActionsComponent, SpawnEntityActionEvent>(OnAttackAction);
        SubscribeLocalEvent<ActionsComponent, ToggleTileMovementActionEvent>(OnTileMovement);
    }

    private void OnAttackAction(Entity<ActionsComponent> ent, ref SpawnEntityActionEvent args)
    {
        if (_net.IsClient // PredictedSpawn doesn't support spawning entities without initializing them yet...
            || args.Handled)
            return;

        // A stale target can be off the grid. Falling back to the boss keeps the attack alive
        // instead of quietly dropping it, which is how a half-working boss looks inert.
        var target = args.Target;
        if (!target.IsValid(EntityManager) || _xform.GetGrid(target) == null)
            target = Transform(args.Performer).Coordinates;

        var attachTarget = EntityUid.Invalid;
        var attach = false;
        if (args.AttachToTarget
            && args.Entity is { Valid: true } candidate
            && !TerminatingOrDeleted(candidate))
        {
            attachTarget = candidate;
            attach = true;
        }

        var amount = Math.Max(1, args.Amount);
        for (var i = 0; i < amount; i++)
        {
            EntityUid spawned;
            if (args.AttachToTarget && attach)
                spawned = EntityManager.CreateEntityUninitialized(args.Spawn, new EntityCoordinates(attachTarget, Vector2.Zero));
            else if (args.SpawnAtUser)
                spawned = EntityManager.CreateEntityUninitialized(args.Spawn, Transform(args.Performer).Coordinates);
            else
                spawned = EntityManager.CreateEntityUninitialized(args.Spawn, target);

            var ev = new SpawnedByActionEvent(ent.Owner, args.Entity);
            RaiseLocalEvent(spawned, ref ev);

            // We run MapInitEvent only after SpawnedByActionEvent so all values are already set properly.
            EntityManager.InitializeEntity(spawned);
            // InitializeEntity doesn't start components: without this no ComponentStartup fires,
            // and spawned mobs get no NPC owner blackboard and no faction caches.
            EntityManager.StartEntity(spawned);
            EntityManager.RunMapInit(spawned, MetaData(spawned)); // InitializeEntity doesn't trigger MapInit event by itself....

            if (args.AttachToTarget && attach)
                _xform.SetParent(spawned, attachTarget); // It doesn't work without that for whatever reason??
        }

        args.Handled = true;
    }

    private void OnTileMovement(Entity<ActionsComponent> ent, ref ToggleTileMovementActionEvent args)
    {
        if (args.Handled)
            return;

        if (HasComp<HierophantBeatComponent>(args.Target))
            RemComp<HierophantBeatComponent>(args.Target);
        else
            EnsureComp<HierophantBeatComponent>(args.Target);

        args.Handled = true;
    }
}
