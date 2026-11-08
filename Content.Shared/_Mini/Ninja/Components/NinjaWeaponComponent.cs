// SPDX-FileCopyrightText: 2026 Mini Station
// SPDX-License-Identifier: LicenseRef-MiniStation-1.0

namespace Content.Shared._Mini.Ninja.Components;

/// <summary>
/// Marker for weapons that a space ninja is allowed to use (suit-integrated emitters, johyo, energy net).
/// The base ninja code forbids firing regular guns.
/// </summary>
[RegisterComponent]
public sealed partial class NinjaWeaponComponent : Component
{
}
