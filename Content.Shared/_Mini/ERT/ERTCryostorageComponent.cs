// SPDX-FileCopyrightText: 2026 Space Station 14 Contributors
//
// SPDX-License-Identifier: AGPL-3.0-or-later

using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;
using Robust.Shared.Utility;

namespace Content.Shared._Mini.ERT;

[RegisterComponent, NetworkedComponent]
public sealed partial class ERTCryostorageComponent : Component
{
    [DataField]
    public bool AdminAccess = false;

    [DataField]
    public bool Blocked = false;

    [DataField]
    public string ContainerId;

    [DataField]
    public List<ERTCryostorageOption> Options = new();

    [DataField]
    public LocId TitleName;

    [DataField]
    public bool UseOnce = true;
}

[DataDefinition, Serializable, NetSerializable]
public sealed partial class ERTCryostorageOption
{
    [DataField]
    public LocId Name;

    [DataField]
    public SpriteSpecifier Sprite;

    [DataField]
    public string Settings = string.Empty;

    [DataField]
    public bool RequiresAdminAllow = false;
}