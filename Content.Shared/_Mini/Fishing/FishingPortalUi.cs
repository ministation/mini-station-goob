// SPDX-License-Identifier: AGPL-3.0-or-later
// Ported from tgstation/tgstation (code/modules/fishing, AGPL-3.0-or-later).

using Robust.Shared.Serialization;
using Robust.Shared.Utility;

namespace Content.Shared._Mini.Fishing;

[Serializable, NetSerializable]
public enum FishingPortalUiKey : byte
{
    Key,
}

[Serializable, NetSerializable]
public sealed class FishingPortalOption
{
    public string SourceId;
    public string Name;
    public SpriteSpecifier Icon;

    public FishingPortalOption(string sourceId, string name, SpriteSpecifier icon)
    {
        SourceId = sourceId;
        Name = name;
        Icon = icon;
    }
}

[Serializable, NetSerializable]
public sealed class FishingPortalBoundUserInterfaceState : BoundUserInterfaceState
{
    public List<FishingPortalOption> Options;

    public FishingPortalBoundUserInterfaceState(List<FishingPortalOption> options)
    {
        Options = options;
    }
}

/// <summary>
/// Sent with the chosen fishing source id, or null to turn the portal off.
/// </summary>
[Serializable, NetSerializable]
public sealed class FishingPortalSelectMessage : BoundUserInterfaceMessage
{
    public string? SourceId;

    public FishingPortalSelectMessage(string? sourceId)
    {
        SourceId = sourceId;
    }
}
