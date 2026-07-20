// SPDX-License-Identifier: MIT

using Robust.Shared.GameStates;

namespace Content.Shared.Botany.Components;

[NetworkedComponent]
public abstract partial class SharedProduceComponent : Component
{
    /// <summary>
    ///     Seed prototype used when this produce has its seeds extracted / for guidebook sources.
    /// </summary>
    [DataField("seedId")]
    public string? SeedId;
}
