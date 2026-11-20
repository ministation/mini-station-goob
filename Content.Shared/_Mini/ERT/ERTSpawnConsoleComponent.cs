// SPDX-FileCopyrightText: 2026 Space Station 14 Contributors
//
// SPDX-License-Identifier: AGPL-3.0-or-later

using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared._Mini.ERT;

[RegisterComponent, NetworkedComponent]
public sealed partial class ERTSpawnConsoleComponent : Component
{
    [DataField]
    public bool AdminAccess = false;

    [DataField]
    public int Balance;

    [DataField]
    public LocId BalanceName;

    [DataField]
    public float Delay;

    [DataField]
    public List<ERTSpawnOption> Options = new();

    [DataField]
    public LocId TitleName;
}

[DataDefinition, Serializable, NetSerializable]
public sealed partial class ERTSpawnOption
{
    [DataField]
    public LocId Name;

    [DataField]
    public LocId Desc;

    [DataField]
    public EntProtoId Rule;

    [DataField]
    public int Price;

    [DataField]
    public bool RequiresAdminAllow = false;
}