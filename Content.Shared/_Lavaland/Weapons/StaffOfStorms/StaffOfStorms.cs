// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared.Actions;
using Content.Shared.Weather;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._Lavaland.Weapons.StaffOfStorms;

/// <summary>
/// Staff of storms from TG lavaland: calls or disperses an ash storm on the planet.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class StaffOfStormsComponent : Component
{
    /// <summary>
    /// Which storm the staff summons.
    /// </summary>
    [DataField]
    public ProtoId<WeatherPrototype> Storm = "AshfallHeavy";

    /// <summary>
    /// How long the summoned storm lasts, in seconds.
    /// </summary>
    [DataField]
    public float StormDuration = 90f;
}

public sealed partial class StaffOfStormsActionEvent : InstantActionEvent
{
}
