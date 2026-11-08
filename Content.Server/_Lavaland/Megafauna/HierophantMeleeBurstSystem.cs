// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared._Lavaland.Megafauna.Events;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Content.Shared.Weapons.Melee.Events;

namespace Content.Server._Lavaland.Megafauna;

/// <summary>
/// TG parity for <see cref="HierophantMeleeBurstComponent"/>: every melee hit on a living
/// target is followed by a burst at the hierophant's own position.
/// </summary>
public sealed class HierophantMeleeBurstSystem : EntitySystem
{
    [Dependency] private readonly SharedTransformSystem _xform = default!;
    [Dependency] private readonly MobStateSystem _mobState = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<HierophantMeleeBurstComponent, MeleeHitEvent>(OnMeleeHit);
    }

    private void OnMeleeHit(Entity<HierophantMeleeBurstComponent> ent, ref MeleeHitEvent args)
    {
        // The event is broadcast from the weapon, so it reaches every entity with this component.
        if (args.User != ent.Owner)
            return;

        var hitLiving = false;
        foreach (var target in args.HitEntities)
        {
            if (HasComp<MobStateComponent>(target) && !_mobState.IsDead(target))
                hitLiving = true;
        }

        if (!hitLiving)
            return;

        // Spawn through the action event so AngerShapeSpawner can set the ring counter and
        // period: a plain Spawn() leaves MaxCounter at 1, i.e. a single static ring.
        var spawned = EntityManager.CreateEntityUninitialized(ent.Comp.Shape, _xform.GetMapCoordinates(ent));
        var ev = new SpawnedByActionEvent(ent.Owner, null);
        RaiseLocalEvent(spawned, ref ev);
        EntityManager.InitializeEntity(spawned);
        EntityManager.StartEntity(spawned);
        EntityManager.RunMapInit(spawned, MetaData(spawned));
    }
}
