// SPDX-License-Identifier: AGPL-3.0-or-later
// Ported from tgstation/tgstation (code/modules/fishing, AGPL-3.0-or-later).

using Content.Goobstation.Maths.FixedPoint;
using Content.Shared._Mini.Fishing.Components;
using Content.Shared._Mini.Fishing.Events;
using Content.Shared._Mini.Fishing.Prototypes;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.Chemistry.Reagent;
using Content.Shared.Nutrition.Components;
using Robust.Shared.Network;
using Robust.Shared.Prototypes;

namespace Content.Shared._Mini.Fishing.Systems;

/// <summary>
/// Makes fish edible: reagents scale with the weight of the fish, seafood adds salt and
/// species override their taste and reagents like in tg.
/// </summary>
public sealed class FishEdibleSystem : EntitySystem
{
    // tg FISH_WEIGHT_BITE_DIVISOR = FISH_GRIND_RESULTS_WEIGHT_DIVISOR * FISH_WEIGHT_GRIND_TO_BITE_MULT.
    private const float WeightPerBite = 200f;

    private static readonly ProtoId<ReagentPrototype> Protein = "Protein";
    private static readonly ProtoId<ReagentPrototype> Blood = "Blood";
    private static readonly ProtoId<ReagentPrototype> Salt = "TableSalt";

    [Dependency] private readonly INetManager _net = default!;
    [Dependency] private readonly IPrototypeManager _proto = default!;
    [Dependency] private readonly SharedSolutionContainerSystem _solutions = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<FishInitializedEvent>(OnFishInitialized);
    }

    private void OnFishInitialized(FishInitializedEvent args)
    {
        if (!_net.IsServer || !TryComp<FishComponent>(args.Fish, out var fish))
            return;

        TryMakeEdible(args.Fish, fish);
    }

    private void TryMakeEdible(EntityUid uid, FishComponent fish)
    {
        FishPrototype? proto = null;
        if (fish.Fish is { } protoId)
            _proto.TryIndex(protoId, out proto);

        if (proto is { Edible: false })
            return;

        // shortcut: this fork has no grill or fryer, so only the raw taste and reagents are ported.
        // shortcut: tg makes raw fish infectious with floor diseases, this fork has no such component.
        var bites = MathF.Max(1f, fish.Weight / WeightPerBite);

        var reagents = new Dictionary<ProtoId<ReagentPrototype>, float>();
        if (proto is not { ReplaceEdibleReagents: true })
        {
            reagents[Protein] = 2f;
            reagents[Blood] = 1f;
        }

        if (proto != null)
        {
            foreach (var (reagent, amount) in proto.EdibleReagents)
                reagents[reagent] = reagents.GetValueOrDefault(reagent) + amount;

            if (proto.Fluid == AquariumFluid.Saltwater)
                reagents[Salt] = reagents.GetValueOrDefault(Salt) + 0.4f;
        }
        else
        {
            reagents[Protein] = 2f;
            reagents[Blood] = 1f;
        }

        var perBite = 0f;
        foreach (var amount in reagents.Values)
            perBite += amount;

        if (perBite <= 0f)
            return;

        // tg leaves a little room for condiments.
        var maxVolume = FixedPoint2.New(perBite * bites * 1.25f);
        _solutions.EnsureSolution(uid, "food", out var solution, maxVol: maxVolume);
        if (solution == null)
            return;

        foreach (var (reagent, amount) in reagents)
            solution.AddReagent(reagent, FixedPoint2.New(amount * bites));

        var edible = EnsureComp<EdibleComponent>(uid);
        edible.Solution = "food";
        edible.DestroyOnEmpty = true;
        edible.Delay = TimeSpan.FromSeconds(1.5);
        edible.TransferAmount = maxVolume / FixedPoint2.New(bites);
        Dirty(uid, edible);

        if (proto is { Taste.Count: > 0 })
        {
            var flavor = EnsureComp<FlavorProfileComponent>(uid);
            flavor.Flavors.Clear();
            flavor.Flavors.UnionWith(proto.Taste);
            Dirty(uid, flavor);
        }
    }
}
