// SPDX-FileCopyrightText: 2026 Mini Station
// Мини-станция, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/ministation/mini-station-goob/master/LICENSE.TXT

using Content.Shared.Actions;
using Robust.Shared.GameStates;
using Content.Shared.Tag;
using Robust.Shared.Prototypes;

namespace Content.Shared._Mini.Ninja.Base.AutoDust;

/// <summary>
/// Auto-dust module of the ninja suit: vaporizes the ninja body after death (or critical state)
/// so that the equipment can not be recovered.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class NinjaAutoDustComponent : Component
{
    [DataField, AutoNetworkedField]
    public NinjaDustMode Mode = NinjaDustMode.Off;

    [DataField]
    public EntProtoId Action = "ActionNinjaAutoDust";

    [DataField, AutoNetworkedField]
    public EntityUid? ActionEntity;

    /// <summary>
    /// Remove the worn equipment when the body turns to dust.
    /// </summary>
    [DataField]
    public bool DeleteItems = true;

    [DataField]
    public EntProtoId DustEffect = "Acidifier";

    /// <summary>
    /// Items with these tags are dropped instead of being deleted.
    /// </summary>
    [DataField]
    public List<ProtoId<TagPrototype>> HighRiskTags = new()
    {
        "HighRiskItem",
    };
}

public enum NinjaDustMode : byte
{
    Off,
    Crit,
    Dead,
}

/// <summary>
/// Marker placed on the wearer of a suit with the auto-dust module.
/// </summary>
[RegisterComponent]
public sealed partial class NinjaAutoDustMarkerComponent : Component
{
    [DataField]
    public EntityUid? Suit;
}

public sealed partial class NinjaToggleAutoDustEvent : InstantActionEvent;

/// <summary>
/// Raised on the body right before auto-dust destroys it. Other modules (second chance) may cancel it.
/// </summary>
[ByRefEvent]
public record struct NinjaAutoDustAttemptEvent(EntityUid Body, EntityUid Suit, NinjaAutoDustComponent Dust)
{
    public bool Cancelled;
}
