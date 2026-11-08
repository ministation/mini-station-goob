// SPDX-FileCopyrightText: 2026 Mini Station
// SPDX-License-Identifier: LicenseRef-MiniStation-1.0

using Robust.Shared.Prototypes;

namespace Content.Client._Mini.Ninja.Abilities.Ghost;

/// <summary>
/// Client only marker of an item worn by a disguised ninja. Its equipment visuals are replaced by the layers of
/// the scanned clothing prototype, or removed entirely when the scanned crew member had nothing in that slot.
/// </summary>
[RegisterComponent]
public sealed partial class NinjaGhostDisguiseItemComponent : Component
{
    /// <summary>
    /// Wearer the replacement layers were prepared for.
    /// </summary>
    [ViewVariables]
    public EntityUid Wearer;

    /// <summary>
    /// Inventory slot the replacement layers were prepared for.
    /// </summary>
    [ViewVariables]
    public string Slot = string.Empty;

    /// <summary>
    /// Prototype drawn instead of the real item, null hides the item.
    /// </summary>
    [ViewVariables]
    public EntProtoId? Prototype;

    /// <summary>
    /// Equipment layers of <see cref="Prototype"/> for <see cref="Slot"/>.
    /// </summary>
    [ViewVariables]
    public List<(string Key, PrototypeLayerData Layer)> Layers = new();
}
