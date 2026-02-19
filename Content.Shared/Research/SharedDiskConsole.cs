// SPDX-FileCopyrightText: 2022 Nemanja <98561806+EmoGarbage404@users.noreply.github.com>
// SPDX-FileCopyrightText: 2025 Aiden <28298836+Aidenkrz@users.noreply.github.com>
//
// SPDX-License-Identifier: MIT

using Robust.Shared.Serialization;

namespace Content.Shared.Research;

[Serializable, NetSerializable]
public enum DiskConsoleUiKey : byte
{
    Key
}

[Serializable, NetSerializable]
public sealed class DiskConsoleBoundUserInterfaceState : BoundUserInterfaceState
{
    public bool CanPrint;
    public bool AutoPrint;
    public bool AutoFeedAdjacentConverter;
    public int PointCost;
    public int ServerPoints;

    public DiskConsoleBoundUserInterfaceState(
        int serverPoints,
        int pointCost,
        bool canPrint,
        bool autoPrint,
        bool autoFeedAdjacentConverter)
    {
        CanPrint = canPrint;
        AutoPrint = autoPrint;
        AutoFeedAdjacentConverter = autoFeedAdjacentConverter;
        PointCost = pointCost;
        ServerPoints = serverPoints;
    }
}

[Serializable, NetSerializable]
public sealed class DiskConsolePrintDiskMessage : BoundUserInterfaceMessage
{

}

[Serializable, NetSerializable]
public sealed class DiskConsoleSetAutoPrintMessage : BoundUserInterfaceMessage
{
    public bool AutoPrint;

    public DiskConsoleSetAutoPrintMessage(bool autoPrint)
    {
        AutoPrint = autoPrint;
    }
}

[Serializable, NetSerializable]
public sealed class DiskConsoleSetAutoFeedAdjacentConverterMessage : BoundUserInterfaceMessage
{
    public bool AutoFeedAdjacentConverter;

    public DiskConsoleSetAutoFeedAdjacentConverterMessage(bool autoFeedAdjacentConverter)
    {
        AutoFeedAdjacentConverter = autoFeedAdjacentConverter;
    }
}
