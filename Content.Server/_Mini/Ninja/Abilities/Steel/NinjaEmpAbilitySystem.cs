// SPDX-FileCopyrightText: 2026 Mini Station
// Мини-станция, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/ministation/mini-station-goob/master/LICENSE.TXT

using Content.Server.Emp;
using Content.Shared._Mini.Ninja.Abilities.Steel;
using Content.Shared._Mini.Ninja.Systems;

namespace Content.Server._Mini.Ninja.Abilities.Steel;

/// <summary>
/// Steel tier 3: the electromagnetic pulse. Fully server side, the client only shows the granted action.
/// </summary>
public sealed class NinjaEmpAbilitySystem : EntitySystem
{
    [Dependency] private readonly NinjaSuitPowerSystem _suitPower = default!;
    [Dependency] private readonly EmpSystem _emp = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<NinjaEmpAbilityComponent, NinjaEmpAbilityEvent>(OnEmp);
    }

    private void OnEmp(Entity<NinjaEmpAbilityComponent> ent, ref NinjaEmpAbilityEvent args)
    {
        if (args.Handled)
            return;

        if (Transform(ent.Owner).ParentUid != args.Performer)
            return;

        if (!_suitPower.TryGetActiveSuit(args.Performer, out var suit, out _) || suit != ent.Owner)
            return;

        if (!_suitPower.TryUseCharge(args.Performer, ent.Comp.Charge))
            return;

        args.Handled = true;

        // Centered on the suit, so on the ninja. The suit itself has full EMP resistance.
        _emp.EmpPulse(
            Transform(ent.Owner).Coordinates,
            ent.Comp.EmpRange,
            ent.Comp.EmpEnergyConsumption,
            ent.Comp.EmpDuration,
            args.Performer);
    }
}
