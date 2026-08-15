// SPDX-License-Identifier: AGPL-3.0-or-later

using Robust.Shared.GameStates;

namespace Content.Goobstation.Shared.Whitelist.Components;

/// <summary>
/// Whitelist component for stock parts to avoid tag redefinition and collisions
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class StockPartComponent : Component;