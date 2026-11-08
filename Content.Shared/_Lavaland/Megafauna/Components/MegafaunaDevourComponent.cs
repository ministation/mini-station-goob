// SPDX-License-Identifier: LicenseRef-MiniStation-1.0

using Robust.Shared.GameObjects;

namespace Content.Shared._Lavaland.Megafauna.Components;

/// <summary>
///     Megafauna eats adjacent fresh corpses (TG _megafauna.dm devour): restores health,
///     rips out vital organs and marks the body as gutted. Corpses already gutted are
///     ignored, so a body can only be devoured once.
/// </summary>
[RegisterComponent]
public sealed partial class MegafaunaDevourComponent : Component
{
    /// <summary>
    ///     Part of the victim's max health restored to the megafauna.
    /// </summary>
    [DataField]
    public float HealFraction = 0.5f;

    /// <summary>
    ///     Maximum distance between the megafauna and the corpse.
    /// </summary>
    [DataField]
    public float Range = 2.2f;
}
