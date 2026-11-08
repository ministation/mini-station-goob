// SPDX-License-Identifier: LicenseRef-MiniStation-1.0
// Mechanics reference: tgstation/tgstation (code/modules/fishing).

using Robust.Shared.GameStates;

namespace Content.Shared._Mini.Fishing.Components;

/// <summary>
/// The fish encyclopedia. The catalog window is built on the client from prototypes,
/// so this component only marks the item.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class FishCatalogComponent : Component;
