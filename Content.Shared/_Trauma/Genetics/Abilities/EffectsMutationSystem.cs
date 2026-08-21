// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared.EntityEffects;
using Content.Shared._Trauma.Genetics.Mutations;

namespace Content.Shared._Trauma.Genetics.Abilities;

/// <summary>
/// Handles running effects for <see cref="EffectsMutationComponent"/>.
/// </summary>
public sealed partial class EffectsMutationSystem : EntitySystem
{
    [Dependency] private SharedEntityEffectsSystem _effects = default!;

    [SubscribeLocalEvent]
    private void OnAdded(Entity<EffectsMutationComponent> ent, ref MutationAddedEvent args)
    {
        if (args.Automatic && ent.Comp.IgnoreAutomatic)
            return;

        _effects.ApplyEffects(args.Target, ent.Comp.Added, user: args.User);
    }

    [SubscribeLocalEvent]
    private void OnRemoved(Entity<EffectsMutationComponent> ent, ref MutationRemovedEvent args)
    {
        if (args.Automatic && ent.Comp.IgnoreAutomatic)
            return;

        _effects.ApplyEffects(args.Target, ent.Comp.Removed, user: args.User);
    }
}
