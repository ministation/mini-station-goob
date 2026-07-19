// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.Movement.Components;
using Content.Shared.Movement.Events;

namespace Content.Shared.Movement.Systems;

public abstract class SharedSpriteMovementSystem : EntitySystem
{
    private EntityQuery<MobStateComponent> _mobStateQuery;

    public override void Initialize()
    {
        base.Initialize();

        _mobStateQuery = GetEntityQuery<MobStateComponent>();
        SubscribeLocalEvent<SpriteMovementComponent, SpriteMoveEvent>(OnSpriteMoveInput);
    }

    private void OnSpriteMoveInput(Entity<SpriteMovementComponent> ent, ref SpriteMoveEvent args)
    {
        if (ent.Comp.IsMoving == args.IsMoving)
            return;

        // Dead/crit mobs keep DamageStateVisuals; don't Dirty SpriteMovement and fight those layers.
        if (_mobStateQuery.TryGetComponent(ent, out var mobState) &&
            mobState.CurrentState is MobState.Critical or MobState.Dead)
            return;

        ent.Comp.IsMoving = args.IsMoving;
        Dirty(ent);
    }
}