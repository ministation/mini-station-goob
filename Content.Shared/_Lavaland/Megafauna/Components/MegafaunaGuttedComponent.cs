// SPDX-License-Identifier: AGPL-3.0-or-later

using Robust.Shared.GameObjects;

namespace Content.Shared._Lavaland.Megafauna.Components;

/// <summary>
///     Marker on a corpse that was devoured by megafauna: vital organs are gone,
///     the body cannot be devoured again.
/// </summary>
[RegisterComponent]
public sealed partial class MegafaunaGuttedComponent : Component;
