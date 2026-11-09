// SPDX-License-Identifier: LicenseRef-MiniStation-1.0

using Robust.Shared.GameStates;
using Robust.Shared.Localization;

namespace Content.Shared._Lavaland.Weapons.CrusherSkin;

/// <summary>
/// Cosmetic crusher reskin: swaps the item and in-hand sprites between the existing crusher sprite sets.
/// Stats and upgrades are not touched.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState(raiseAfterAutoHandleState: true)]
public sealed partial class CrusherSkinComponent : Component
{
    /// <summary>
    /// Available looks, in cycle order.
    /// </summary>
    [DataField(required: true)]
    public List<CrusherSkin> Skins = new();

    /// <summary>
    /// Look the item currently shows.
    /// </summary>
    [DataField, AutoNetworkedField]
    public int Index;
}

[DataDefinition]
public sealed partial class CrusherSkin
{
    /// <summary>
    /// RSI of the item sprite, upgrade overlays included.
    /// </summary>
    [DataField(required: true)]
    public string Sprite = string.Empty;

    /// <summary>
    /// RSI of the in-hand layers.
    /// </summary>
    [DataField(required: true)]
    public string Inhand = string.Empty;

    [DataField(required: true)]
    public LocId Name;
}
