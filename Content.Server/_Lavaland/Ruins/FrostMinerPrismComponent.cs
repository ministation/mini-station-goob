// SPDX-License-Identifier: AGPL-3.0-or-later

namespace Content.Server._Lavaland.Ruins;

/// <summary>
/// TG parity: the frost miner arena prisms react to their master. They turn violet
/// while the miner is enraged and dim to gray when it dies.
/// </summary>
[RegisterComponent]
public sealed partial class FrostMinerPrismComponent : Component
{
    /// <summary>
    /// Sprite layer the tint is applied to, same shortcut as the miner's own enrage sprite swap.
    /// </summary>
    [DataField]
    public string Layer = "enum.DamageStateVisualLayers.Base";

    /// <summary>
    /// Sprite state kept on the layer while tinting.
    /// </summary>
    [DataField]
    public string State = "ice_crystal";

    /// <summary>
    /// Sprite tint applied on the boss enrage.
    /// </summary>
    [DataField]
    public Color EnragedColor = Color.FromHex("#9966FF");

    /// <summary>
    /// Sprite tint applied on the boss death.
    /// </summary>
    [DataField]
    public Color DeadColor = Color.FromHex("#828282");

    /// <summary>
    /// Point light color applied on the boss enrage.
    /// </summary>
    [DataField]
    public Color EnragedLightColor = Color.FromHex("#9966FF");

    /// <summary>
    /// Point light color applied on the boss death.
    /// </summary>
    [DataField]
    public Color? DeadLightColor = Color.FromHex("#5a5a5a");
}
