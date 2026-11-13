// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Server.Body.Components;
using Content.Shared._Trauma.Genetics.YamlCompat;
using Content.Shared.EntityEffects;

namespace Content.Server._Trauma.Genetics.YamlCompat;

/// <summary>
/// MetabolizerComponent lives in Content.Server, so these two adapters cannot be shared like the
/// rest of the genetics YAML compat effects. The effect data itself is defined in Content.Shared.
/// </summary>
public sealed class AddMetabolizerTypeEffectSystem : EntityEffectSystem<MetabolizerComponent, AddMetabolizerType>
{
    protected override void Effect(Entity<MetabolizerComponent> ent, ref EntityEffectEvent<AddMetabolizerType> args)
    {
        ent.Comp.MetabolizerTypes ??= new();
        ent.Comp.MetabolizerTypes.Add(args.Effect.Type);
    }
}

public sealed class RemoveMetabolizerTypeEffectSystem : EntityEffectSystem<MetabolizerComponent, RemoveMetabolizerType>
{
    protected override void Effect(Entity<MetabolizerComponent> ent, ref EntityEffectEvent<RemoveMetabolizerType> args)
    {
        ent.Comp.MetabolizerTypes?.Remove(args.Effect.Type);
    }
}
