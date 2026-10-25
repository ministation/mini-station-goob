// SPDX-FileCopyrightText: 2026 Mini Station
// Мини-станция, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/ministation/mini-station-goob/master/LICENSE.TXT

using Content.Shared._Mini.Ninja.Abilities.Steel;
using Content.Shared._Mini.Ninja.Systems;
using Content.Shared.Body.Systems;
using Content.Shared.Chemistry.Components;
using Content.Shared.Popups;

namespace Content.Server._Mini.Ninja.Abilities.Steel;

/// <summary>
/// Steel tier 2: the adrenaline surge. Injects Velaxin and a bit of Radium into the bloodstream of the
/// suit wearer. The action carries the charges, so the suit component only stores the reagent amounts.
/// </summary>
public sealed class NinjaAdrenalSystem : EntitySystem
{
    [Dependency] private readonly NinjaSuitPowerSystem _suitPower = default!;
    [Dependency] private readonly SharedBloodstreamSystem _bloodstream = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<NinjaAdrenalComponent, NinjaAdrenalEvent>(OnAdrenal);
    }

    private void OnAdrenal(Entity<NinjaAdrenalComponent> ent, ref NinjaAdrenalEvent args)
    {
        if (args.Handled)
            return;

        var wearer = args.Performer;

        // Only the current wearer of the suit the module is installed in can use it.
        if (Transform(ent.Owner).ParentUid != wearer)
            return;

        if (!_suitPower.TryGetActiveSuit(wearer, out _, out _))
            return;

        args.Handled = true;

        var solution = new Solution();
        solution.AddReagent(ent.Comp.VelaxinReagent, ent.Comp.VelaxinAmount);
        solution.AddReagent(ent.Comp.RadiumReagent, ent.Comp.RadiumAmount);

        if (!_bloodstream.TryAddToBloodstream(wearer, solution))
            return;

        _popup.PopupEntity(Loc.GetString("ninja-adrenal-injected"), wearer, wearer, PopupType.Medium);
    }
}
