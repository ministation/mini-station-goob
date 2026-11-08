// SPDX-License-Identifier: AGPL-3.0-or-later

using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._Lavaland.Fauna;

/// <summary>
/// Time-based growth for lavaland fauna. After <see cref="GrowthTime"/> the animal
/// is replaced by a randomly picked prototype from <see cref="GrowInto"/>.
/// TG parity: growth_and_differentiation — gutlunch grubs become a warrior or a
/// milk gubbuck after 3 minutes, juvenile lobstrosities grow into adults.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class FaunaGrowthComponent : Component
{
    /// <summary>
    /// Time needed to grow into the next stage.
    /// </summary>
    [DataField(required: true)]
    public TimeSpan GrowthTime = TimeSpan.FromSeconds(180);

    /// <summary>
    /// Prototypes this animal grows into, picked uniformly at random.
    /// </summary>
    [DataField(required: true)]
    public List<EntProtoId> GrowInto = [];

    /// <summary>
    /// When the animal grows. Set on map init, transient.
    /// </summary>
    [ViewVariables]
    public TimeSpan NextGrowthTime;
}
