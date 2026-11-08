// SPDX-FileCopyrightText: 2026 Mini Station
// SPDX-License-Identifier: LicenseRef-MiniStation-1.0

using Robust.Shared.GameStates;

namespace Content.Shared._Mini.Ninja.Appearance;

/// <summary>
/// Personalisation of the ninja suit, chosen in the Spider OS before the suit is activated.
/// Lives on the suit and is networked so the client can recolour all worn suit parts.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState(raiseAfterAutoHandleState: true)]
public sealed partial class NinjaAppearanceComponent : Component
{
    [DataField, AutoNetworkedField]
    public NinjaColorway Colorway = NinjaColorway.Green;

    [DataField, AutoNetworkedField]
    public NinjaStyle Style = NinjaStyle.Old;

    [DataField, AutoNetworkedField]
    public bool ScarfShown;

    [DataField, AutoNetworkedField]
    public bool HelmetShown = true;
}
