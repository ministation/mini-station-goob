// SPDX-FileCopyrightText: 2026 Mini Station
// Мини-станция, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/ministation/mini-station-goob/master/LICENSE.TXT

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
