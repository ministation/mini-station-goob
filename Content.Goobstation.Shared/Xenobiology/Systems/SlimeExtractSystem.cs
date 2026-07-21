// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Goobstation.Shared.Xenobiology.Components;
using Content.Shared.Chemistry;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.Chemistry.Reaction;
using Content.Shared.EntityEffects;
using Content.Shared.Examine;

namespace Content.Goobstation.Shared.Xenobiology.Systems;

// This handles slime extracts.
public sealed partial class SlimeExtractSystem : EntitySystem
{
    [Dependency] private readonly SharedSolutionContainerSystem _solution = default!;

    public override void Initialize()
    {
        base.Initialize();

        // Clear leftover trigger reagents before extract effects add products into the same solution.
        SubscribeLocalEvent<SlimeExtractComponent, ReactionEntityEvent>(OnReaction, before: [typeof(SharedEntityEffectsSystem)]);
        SubscribeLocalEvent<SlimeExtractComponent, ExaminedEvent>(OnExamined);
    }

    private void OnReaction(Entity<SlimeExtractComponent> ent, ref ReactionEntityEvent args)
    {
        if (_solution.TryGetInjectableSolution(ent.Owner, out var soln, out _))
            _solution.RemoveAllSolution(soln.Value);
    }

    private void OnExamined(Entity<SlimeExtractComponent> ent, ref ExaminedEvent args)
    {
        if (!HasComp<ReactiveComponent>(ent))
            args.PushMarkup(Loc.GetString("xeno-extract-reaction-unreactive"));
    }
}
