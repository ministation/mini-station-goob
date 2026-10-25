// SPDX-FileCopyrightText: 2026 Mini Station
// Мини-станция, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/ministation/mini-station-goob/master/LICENSE.TXT

using Content.Shared._Mini.Ninja;
using Robust.Shared.Localization;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;

namespace Content.Shared._Mini.Ninja.Prototypes;

/// <summary>
/// A single module that a ninja can install into the suit through the Spider OS.
/// Modules are grouped by path and tier, exactly one module per tier can be installed.
/// </summary>
[Prototype("ninjaModule")]
public sealed partial class NinjaModulePrototype : IPrototype
{
    [IdDataField]
    public string ID { get; private set; } = default!;

    /// <summary>
    /// Which path this module belongs to.
    /// </summary>
    [DataField(required: true)]
    public NinjaPath Path;

    /// <summary>
    /// Tier of the module, from 1 to <see cref="NinjaOsPrototype.MaxTier"/>.
    /// A suit can only install one module per tier.
    /// </summary>
    [DataField(required: true)]
    public int Tier;

    [DataField]
    public LocId Name = string.Empty;

    [DataField]
    public LocId Description = string.Empty;

    [DataField]
    public SpriteSpecifier Icon = new SpriteSpecifier.Rsi(new ResPath("/Textures/_Mini/Ninja/Actions/ninja_actions.rsi"), "nullaction");

    /// <summary>
    /// Whether the module survives the second chance (respawn) of the ninja.
    /// </summary>
    [DataField]
    public bool TransferOnSecondChance = true;

    /// <summary>
    /// Components that are added to the suit when the module is installed.
    /// </summary>
    [DataField]
    public ComponentRegistry Components = new();

    /// <summary>
    /// Actions that are added to the suit action container and granted to the wearer.
    /// </summary>
    [DataField]
    public List<EntProtoId>? Actions;
}
