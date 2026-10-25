// SPDX-FileCopyrightText: 2026 Mini Station
// Мини-станция, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/ministation/mini-station-goob/master/LICENSE.TXT

using System.Diagnostics.CodeAnalysis;
using Content.Shared._Mini.Ninja.Components;
using Content.Shared.Ninja.Components;
using Content.Shared.Ninja.Systems;
using Content.Shared.Popups;
using Content.Shared.Power.EntitySystems;
using Content.Shared.PowerCell;

namespace Content.Shared._Mini.Ninja.Systems;

/// <summary>
/// Shared helpers for everything that consumes the ninja suit power.
/// Abilities should call <see cref="TryUseCharge"/> and use <see cref="TryGetActiveSuit"/> to check that the suit is on.
/// </summary>
public sealed partial class NinjaSuitPowerSystem : EntitySystem
{
    [Dependency] private readonly SharedSpaceNinjaSystem _ninja = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly PowerCellSystem _powerCell = default!;
    [Dependency] private readonly SharedBatterySystem _battery = default!;

    public bool TryGetSuit(EntityUid wearer, [NotNullWhen(true)] out EntityUid? suit, [NotNullWhen(true)] out SpaceNinjaComponent? ninja)
    {
        suit = null;
        ninja = null;

        if (!TryComp<SpaceNinjaComponent>(wearer, out ninja) || ninja.Suit is not { } suitUid)
            return false;

        if (TerminatingOrDeleted(suitUid))
            return false;

        suit = suitUid;
        return true;
    }

    /// <summary>
    /// Suit of the wearer, if it is currently activated.
    /// </summary>
    public bool TryGetActiveSuit(EntityUid wearer, [NotNullWhen(true)] out EntityUid? suit, [NotNullWhen(true)] out NinjaOsComponent? os)
    {
        suit = null;
        os = null;

        if (!TryGetSuit(wearer, out var suitUid, out _))
            return false;

        if (!TryComp<NinjaOsComponent>(suitUid, out os) || !os.SuitActivated)
            return false;

        suit = suitUid;
        return true;
    }

    public bool IsSuitActive(EntityUid wearer)
    {
        return TryGetActiveSuit(wearer, out _, out _);
    }

    /// <summary>
    /// Try to consume some charge from the suit power cell, showing the standard "no power" popup on failure.
    /// </summary>
    public bool TryUseCharge(EntityUid wearer, float charge)
    {
        if (_ninja.TryUseCharge(wearer, charge))
            return true;

        PopupNoPower(wearer);
        return false;
    }

    public bool HasCharge(EntityUid wearer, float charge)
    {
        if (!TryGetSuit(wearer, out var suit, out _))
            return false;

        return _powerCell.TryGetBatteryFromSlot(suit.Value, out var battery) &&
               _battery.GetCharge(battery.Value.AsNullable()) >= charge;
    }

    public float GetCharge(EntityUid wearer)
    {
        if (!TryGetSuit(wearer, out var suit, out _))
            return 0f;

        return _powerCell.TryGetBatteryFromSlot(suit.Value, out var battery) ? _battery.GetCharge(battery.Value.AsNullable()) : 0f;
    }

    public void PopupNoPower(EntityUid wearer)
    {
        _popup.PopupEntity(Loc.GetString("ninja-no-power"), wearer, wearer);
    }
}
