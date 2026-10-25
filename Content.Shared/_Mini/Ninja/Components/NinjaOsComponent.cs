// SPDX-FileCopyrightText: 2026 Mini Station
// Мини-станция, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/ministation/mini-station-goob/master/LICENSE.TXT

using Content.Shared._Mini.Ninja.Appearance;
using Content.Shared._Mini.Ninja.Prototypes;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._Mini.Ninja.Components;

/// <summary>
/// Spider OS of a ninja suit. Stores the selected modules and the suit power state.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class NinjaOsComponent : Component
{
    [DataField, AutoNetworkedField]
    public ProtoId<NinjaOsPrototype> Os = "NinjaOs";

    /// <summary>
    /// Installed module per tier. Values are the paths of the selected modules.
    /// </summary>
    [DataField, AutoNetworkedField]
    public Dictionary<int, NinjaPath> SelectedModules = new();

    /// <summary>
    /// Tiers whose module was picked and can no longer be changed.
    /// </summary>
    [DataField, AutoNetworkedField]
    public HashSet<int> LockedTiers = new();

    /// <summary>
    /// Tiers whose module components/actions have already been installed into the suit.
    /// </summary>
    [DataField, AutoNetworkedField]
    public HashSet<int> ActivatedTiers = new();

    [DataField, AutoNetworkedField]
    public bool SuitActivated;

    [DataField]
    public ProtoId<NinjaOsBootPrototype> ActivationBoot = "NinjaOsActivationBoot";

    [DataField]
    public ProtoId<NinjaOsBootPrototype> DeactivationBoot = "NinjaOsDeactivationBoot";

    /// <summary>
    /// Appearance selected in the Spider OS, applied when the suit is activated.
    /// </summary>
    [DataField, AutoNetworkedField]
    public NinjaColorway PendingColorway = NinjaColorway.Green;

    [DataField, AutoNetworkedField]
    public NinjaStyle PendingStyle = NinjaStyle.Old;

    [DataField, AutoNetworkedField]
    public bool PendingHelmet = true;

    /// <summary>
    /// Suit power drain per second while the suit is activated.
    /// </summary>
    [DataField]
    public float IdleDraw = 2f;
}

/// <summary>
/// Marker component for the hardware items of the ninja suit (suit, helmet, mask, visor, gloves, boots).
/// Used to check that the whole set is worn before the suit can be activated and to lock it in place.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class NinjaSuitItemComponent : Component
{
}