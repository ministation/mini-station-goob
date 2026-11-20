// SPDX-FileCopyrightText: 2026 Space Station 14 Contributors
//
// SPDX-License-Identifier: AGPL-3.0-or-later

using Robust.Shared.Serialization;

namespace Content.Shared._Mini.ERT;

[Serializable, NetSerializable]
public enum ERTCryostorageUiKey
{
    Key
}

[Serializable, NetSerializable]
public sealed class ERTCryostorageState : BoundUserInterfaceState
{
    public List<ERTCryostorageOption> Options;
    public LocId TitleName;
    public bool HasAdminAccess;
    public bool Blocked;

    public ERTCryostorageState(
        List<ERTCryostorageOption> options,
        LocId titleName,
        bool hasAdminAccess,
        bool blocked)
    {
        Options = options;
        TitleName = titleName;
        HasAdminAccess = hasAdminAccess;
        Blocked = blocked;
    }
}

[Serializable, NetSerializable]
public sealed class ERTCryostorageSpawnMessage : BoundUserInterfaceMessage
{
    public int OptionIndex;

    public ERTCryostorageSpawnMessage(int optionIndex)
    {
        OptionIndex = optionIndex;
    }
}
