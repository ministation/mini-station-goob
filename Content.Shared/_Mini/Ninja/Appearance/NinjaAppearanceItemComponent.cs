// SPDX-FileCopyrightText: 2026 Mini Station
// SPDX-License-Identifier: LicenseRef-MiniStation-1.0

using Robust.Shared.GameStates;

namespace Content.Shared._Mini.Ninja.Appearance;

/// <summary>
/// Marker for the suit parts that follow the suit colour/style, and for the helmet and scarf which can be shown or hidden.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState(raiseAfterAutoHandleState: true)]
public sealed partial class NinjaAppearanceItemComponent : Component
{
    [DataField]
    public NinjaAppearanceItemType ItemType = NinjaAppearanceItemType.Color;

    /// <summary>
    /// Colour/style remembered by the item while it is not worn together with the suit.
    /// </summary>
    [DataField, AutoNetworkedField]
    public NinjaColorway? FrozenColor;

    [DataField, AutoNetworkedField]
    public NinjaStyle? FrozenStyle;
}
