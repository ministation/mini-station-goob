// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared.EntityEffects;
using Content.Shared.Random.Helpers;
using Content.Shared._Trauma.Genetics.Mutations;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Shared._Trauma.Genetics.EntityEffects;

/// <summary>
/// Adds a random negative mutation to the target entity.
/// Does nothing for mutations which are already present or conflict with existing ones.
/// </summary>
public sealed partial class RandomNegativeMutation : EntityEffectBase<RandomNegativeMutation>;

public sealed partial class RandomNegativeMutationEffectSystem : EntityEffectSystem<MutatableComponent, RandomNegativeMutation>
{
    [Dependency] private IRobustRandom _random = default!;
    [Dependency] private MutationSystem _mutation = default!;

    protected override void Effect(Entity<MutatableComponent> ent, ref EntityEffectEvent<RandomNegativeMutation> args)
    {
        if (_mutation.NegativeMutations.Count == 0)
            return;

        var mutation = _random.Pick(_mutation.NegativeMutations);
        _mutation.AddMutation(ent.AsNullable(), mutation, user: args.User,
            automatic: false, predicted: false);
    }
}
