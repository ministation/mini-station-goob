// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared.StatusEffectNew;

using Content.Shared._Trauma.Genetics.Mutations;

namespace Content.Shared._Trauma.Genetics.Abilities;

public sealed partial class StatusEffectsMutationSystem : EntitySystem
{
    [Dependency] private StatusEffectsSystem _status = default!;

    [SubscribeLocalEvent]
    private void OnAdded(Entity<StatusEffectsMutationComponent> ent, ref MutationAddedEvent args)
    {
        foreach (var effect in ent.Comp.StatusEffects)
            _status.TryAddStatusEffect(args.Target, effect, out _);
    }

    [SubscribeLocalEvent]
    private void OnRemoved(Entity<StatusEffectsMutationComponent> ent, ref MutationRemovedEvent args)
    {
        foreach (var effect in ent.Comp.StatusEffects)
            _status.TryRemoveStatusEffect(args.Target, effect);
    }
}
