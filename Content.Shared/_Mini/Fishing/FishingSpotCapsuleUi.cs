// SPDX-License-Identifier: AGPL-3.0-or-later
// Ported from tgstation/tgstation (code/modules/fishing/fishing_equipment.dm, AGPL-3.0-or-later).

using Robust.Shared.Serialization;
using Robust.Shared.Utility;

namespace Content.Shared._Mini.Fishing;

[Serializable, NetSerializable]
public enum FishingSpotCapsuleUiKey : byte
{
    Key,
}

[Serializable, NetSerializable]
public sealed class FishingSpotCapsuleOptionState
{
    public string SourceId;
    public string Name;
    public string Description;
    public SpriteSpecifier Icon;

    public FishingSpotCapsuleOptionState(string sourceId, string name, string description, SpriteSpecifier icon)
    {
        SourceId = sourceId;
        Name = name;
        Description = description;
        Icon = icon;
    }
}

[Serializable, NetSerializable]
public sealed class FishingSpotCapsuleBoundUserInterfaceState : BoundUserInterfaceState
{
    public List<FishingSpotCapsuleOptionState> Options;

    public FishingSpotCapsuleBoundUserInterfaceState(List<FishingSpotCapsuleOptionState> options)
    {
        Options = options;
    }
}

/// <summary>
/// Sent with the chosen fishing source id, or null to keep the current selection.
/// </summary>
[Serializable, NetSerializable]
public sealed class FishingSpotCapsuleSelectMessage : BoundUserInterfaceMessage
{
    public string? SourceId;

    public FishingSpotCapsuleSelectMessage(string? sourceId)
    {
        SourceId = sourceId;
    }
}
