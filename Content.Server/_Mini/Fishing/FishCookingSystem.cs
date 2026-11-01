// SPDX-License-Identifier: AGPL-3.0-or-later
// Ported from tgstation/tgstation (code/modules/fishing, AGPL-3.0-or-later).

using Content.Shared._Mini.Fishing.Components;
using Content.Shared._Mini.Fishing.Systems;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.Chemistry.Reagent;
using Content.Goobstation.Maths.FixedPoint;
using Content.Shared.Nutrition.Components;
using Content.Shared.Placeable;
using Content.Shared.Temperature;
using Content.Shared.Temperature.Components;
using Robust.Shared.Prototypes;

namespace Content.Server._Mini.Fishing;

/// <summary>
/// Cooks fish placed on a working grill, like tg COMSIG_ITEM_BARBEQUE_GRILLED:
/// kills the fish, removes blood, doubles protein and swaps the taste for cooked fish.
/// </summary>
public sealed class FishCookingSystem : EntitySystem
{
    private static readonly ProtoId<ReagentPrototype> Protein = "Protein";
    private static readonly ProtoId<ReagentPrototype> Blood = "Blood";

    // tg FISH_SAFE_COOKING_DURATION.
    private const float SafeCookingTime = 10f;

    [Dependency] private readonly FishSystem _fish = default!;
    [Dependency] private readonly SharedSolutionContainerSystem _solutions = default!;

    private float _timer;

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        _timer += frameTime;
        if (_timer < 1f)
            return;

        var elapsed = _timer;
        _timer = 0f;

        var query = EntityQueryEnumerator<EntityHeaterComponent, ItemPlacerComponent>();
        while (query.MoveNext(out _, out var heater, out var placer))
        {
            if (heater.Setting == EntityHeaterSetting.Off)
                continue;

            var speed = heater.Setting == EntityHeaterSetting.Low ? 0.5f : 1f;
            foreach (var placed in placer.PlacedEntities)
            {
                if (!TryComp<FishComponent>(placed, out var fish) || fish.Cooked)
                    continue;

                fish.CookTime += elapsed * speed;
                if (fish.CookTime >= SafeCookingTime)
                    CookFish(placed, fish);
            }
        }
    }

    /// <summary>
    /// Applies the cooked state once. Public so tests can cook without a grill.
    /// </summary>
    public void CookFish(EntityUid uid, FishComponent fish)
    {
        if (fish.Cooked)
            return;

        fish.Cooked = true;
        fish.CookTime = SafeCookingTime;
        Dirty(uid, fish);
        _fish.SetAlive((uid, fish), false);

        if (_solutions.TryGetSolution(uid, "food", out var solutionEntity, out var solution))
        {
            var blood = solution.GetReagentQuantity(new ReagentId(Blood.Id, null));
            if (blood > FixedPoint2.Zero)
                solution.RemoveReagent(Blood, blood);

            var protein = solution.GetReagentQuantity(new ReagentId(Protein.Id, null));
            if (protein > FixedPoint2.Zero)
            {
                // tg adjust_reagents_capacity: make room for the extra protein the blood used to take.
                solution.MaxVolume += protein;
                solution.AddReagent(Protein, protein);
            }

            _solutions.UpdateChemicals(solutionEntity.Value);
        }

        var flavor = EnsureComp<FlavorProfileComponent>(uid);
        flavor.Flavors.Clear();
        flavor.Flavors.Add("cooked-fish");
        Dirty(uid, flavor);
    }
}
