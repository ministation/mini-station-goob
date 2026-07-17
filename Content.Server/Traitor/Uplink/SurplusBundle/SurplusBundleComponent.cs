// SPDX-License-Identifier: AGPL-3.0-or-later

namespace Content.Server.Traitor.Uplink.SurplusBundle;

/// <summary>
///     Fill crate with a random uplink items.
/// </summary>
[RegisterComponent]
public sealed partial class SurplusBundleComponent : Component
{
    /// <summary>
    ///     Total price of all content inside bundle.
    /// </summary>
    [DataField]
    public int TotalPrice = 20;

    /// <summary>
    ///     When set, cheaper listings are rolled more often (weight = 1 / cost^exponent).
    /// </summary>
    [DataField]
    public bool CostWeightedSelection;

    /// <summary>
    ///     Exponent for <see cref="CostWeightedSelection"/>; higher values bias harder toward cheap loot.
    /// </summary>
    [DataField]
    public float CostWeightExponent = 2f;
}