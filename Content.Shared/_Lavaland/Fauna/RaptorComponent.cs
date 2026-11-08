// SPDX-License-Identifier: AGPL-3.0-or-later

using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._Lavaland.Fauna;

/// <summary>
/// Marks the raptor family. Used for the trough breeding population cap and to
/// find the egg color a mother lays. TG parity: GLOB.raptor_population.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class RaptorComponent : Component
{
    /// <summary>
    /// Baby raptor an egg laid by this raptor hatches into. Set on adult colors.
    /// </summary>
    [DataField]
    public EntProtoId? BabyProto;

    /// <summary>
    /// Whether this raptor is old enough to breed. TG only breeds adults.
    /// </summary>
    [DataField]
    public bool CanBreed = true;
}
