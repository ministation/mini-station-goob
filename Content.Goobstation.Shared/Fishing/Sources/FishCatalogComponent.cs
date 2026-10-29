// SPDX-License-Identifier: AGPL-3.0-or-later
// Ported from tgstation/tgstation (code/modules/fishing, AGPL-3.0-or-later).

using Robust.Shared.GameStates;

namespace Content.Goobstation.Shared.Fishing.Components;

/// <summary>
/// The fish encyclopedia. The catalog window is built on the client from prototypes,
/// so this component only marks the item.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class FishCatalogComponent : Component;
