// SPDX-FileCopyrightText: 2026 Mini Station
// Мини-станция, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/ministation/mini-station-goob/master/LICENSE.TXT

namespace Content.Shared._Mini.Ninja.Components;

/// <summary>
/// Marker for weapons that a space ninja is allowed to use (suit-integrated emitters, johyo, energy net).
/// The base ninja code forbids firing regular guns.
/// </summary>
[RegisterComponent]
public sealed partial class NinjaWeaponComponent : Component
{
}
