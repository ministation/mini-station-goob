// SPDX-License-Identifier: AGPL-3.0-or-later

using Robust.Shared.GameStates;

namespace Content.Shared._Lavaland.Necropolis;

/// <summary>
/// Ashwalker nest: consumes corpses, hatches ashwalker ghost-role eggs.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class AshwalkerNestComponent : Component
{
    /// <summary>
    /// Radius in which corpses are consumed, in meters.
    /// </summary>
    [DataField]
    public float ConsumeRadius = 1.5f;

    /// <summary>
    /// How many eggs can exist at the same time.
    /// </summary>
    [DataField]
    public int MaxEggs = 2;

    [ViewVariables]
    public int Meat;

    [ViewVariables]
    public int Eggs;
}
