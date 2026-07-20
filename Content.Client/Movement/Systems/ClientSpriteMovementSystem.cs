// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.Movement.Components;
using Content.Shared.Movement.Systems;
using Robust.Client.GameObjects;

namespace Content.Client.Movement.Systems;

/// <summary>
/// Controls the switching of motion and standing still animation
/// </summary>
public sealed class ClientSpriteMovementSystem : SharedSpriteMovementSystem
{
    [Dependency] private readonly SpriteSystem _sprite = default!;

    private EntityQuery<SpriteComponent> _spriteQuery;
    private EntityQuery<MobStateComponent> _mobStateQuery;

    public override void Initialize()
    {
        base.Initialize();

        _spriteQuery = GetEntityQuery<SpriteComponent>();
        _mobStateQuery = GetEntityQuery<MobStateComponent>();

        SubscribeLocalEvent<SpriteMovementComponent, AfterAutoHandleStateEvent>(OnAfterAutoHandleState);
    }

    private void OnAfterAutoHandleState(Entity<SpriteMovementComponent> ent, ref AfterAutoHandleStateEvent args)
    {
        if (!_spriteQuery.TryGetComponent(ent, out var sprite))
            return;

        // SpriteMovement often shares the DamageStateVisuals base layer (e.g. mice).
        // Do not overwrite dead/crit RSI states with idle/walk frames.
        if (_mobStateQuery.TryGetComponent(ent, out var mobState) &&
            mobState.CurrentState is MobState.Critical or MobState.Dead)
            return;

        if (ent.Comp.IsMoving)
        {
            foreach (var (layer, state) in ent.Comp.MovementLayers)
            {
                _sprite.LayerSetData((ent.Owner, sprite), layer, state);
            }
        }
        else
        {
            foreach (var (layer, state) in ent.Comp.NoMovementLayers)
            {
                _sprite.LayerSetData((ent.Owner, sprite), layer, state);
            }
        }
    }
}