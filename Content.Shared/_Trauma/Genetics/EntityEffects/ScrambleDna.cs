// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared.EntityEffects;
using Content.Shared.Forensics.Systems;
using Content.Shared._Trauma.Genetics.Mutations;

namespace Content.Shared._Trauma.Genetics.EntityEffects;

public sealed partial class ScrambleDna : EntityEffectBase<ScrambleDna>
{
    public override string? EntityEffectGuidebookText(IPrototypeManager prototype, IEntitySystemManager entSys)
        => Loc.GetString("entity-effect-guidebook-scramble-dna", ("chance", Probability));
}

public sealed partial class ScrambleDnaEntityEffectSystem : EntityEffectSystem<MutatableComponent, ScrambleDna>
{
    [Dependency] private MutationSystem _mutation = default!;
    [Dependency] private SharedForensicsSystem _forensics = default!;

    protected override void Effect(Entity<MutatableComponent> ent, ref EntityEffectEvent<ScrambleDna> args)
    {
        _forensics.RandomizeDNA(ent.Owner);
        _mutation.Scramble(ent);
    }
}
