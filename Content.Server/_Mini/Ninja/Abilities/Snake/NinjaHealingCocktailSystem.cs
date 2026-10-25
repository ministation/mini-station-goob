// SPDX-FileCopyrightText: 2026 Mini Station
// Мини-станция, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/ministation/mini-station-goob/master/LICENSE.TXT

using Content.Shared._Mini.Ninja.Abilities.Snake;
using Content.Shared._Mini.Ninja.Systems;
using Content.Shared.Body.Systems;
using Content.Shared.Chemistry.Components;
using Content.Shared.Popups;

namespace Content.Server._Mini.Ninja.Abilities.Snake;

/// <summary>
/// Snake tier 2: injects the healing cocktail reagent into the bloodstream of the wearer.
/// There is no entity effect action for this in the port, so the injection is done directly.
/// </summary>
public sealed class NinjaHealingCocktailSystem : EntitySystem
{
    [Dependency] private readonly NinjaSuitPowerSystem _suitPower = default!;
    [Dependency] private readonly SharedBloodstreamSystem _bloodstream = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<NinjaHealingCocktailComponent, NinjaHealingCocktailEvent>(OnUse);
    }

    private void OnUse(Entity<NinjaHealingCocktailComponent> ent, ref NinjaHealingCocktailEvent args)
    {
        if (args.Handled)
            return;

        if (!_suitPower.TryGetActiveSuit(args.Performer, out var suit, out _) || suit.Value != ent.Owner)
            return;

        if (Transform(ent.Owner).ParentUid != args.Performer)
            return;

        var solution = new Solution();
        solution.AddReagent(ent.Comp.Reagent, ent.Comp.Quantity);

        if (!_bloodstream.TryAddToBloodstream(args.Performer, solution))
            return;

        if (ent.Comp.Popup is { } popup)
            _popup.PopupEntity(Loc.GetString(popup), args.Performer, args.Performer, PopupType.Medium);

        args.Handled = true;
    }
}
