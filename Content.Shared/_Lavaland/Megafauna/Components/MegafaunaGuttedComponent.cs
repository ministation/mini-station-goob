// SPDX-License-Identifier: LicenseRef-MiniStation-1.0

using Robust.Shared.GameObjects;

namespace Content.Shared._Lavaland.Megafauna.Components;

/// <summary>
///     Marker on a corpse that was devoured by megafauna: vital organs are gone,
///     the body cannot be devoured again.
/// </summary>
[RegisterComponent]
public sealed partial class MegafaunaGuttedComponent : Component;
