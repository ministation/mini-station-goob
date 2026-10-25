// SPDX-FileCopyrightText: 2026 Mini Station
// Мини-станция, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/ministation/mini-station-goob/master/LICENSE.TXT

using Robust.Shared.GameStates;

namespace Content.Shared._Mini.Ninja.Abilities.Snake;

/// <summary>
/// Snake tier 5: the second chance. Lives on the suit and revives the wearer in place once.
/// Unlike the reference this port has no respawn capsule and no dojo teleport.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class NinjaSecondChanceComponent : Component
{
    /// <summary>
    /// Whether the second chance has already been used. There is no third chance.
    /// </summary>
    [DataField, AutoNetworkedField]
    public bool Used;
}

/// <summary>
/// Placed on the wearer of a suit with the second chance module, so the ability can listen to the
/// death of the body before the auto-dust module destroys it.
/// </summary>
[RegisterComponent]
public sealed partial class NinjaSecondChanceMarkerComponent : Component
{
    /// <summary>
    /// The suit that granted the second chance.
    /// </summary>
    [DataField]
    public EntityUid? Suit;
}
