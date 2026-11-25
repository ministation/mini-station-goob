// SPDX-FileCopyrightText: 2026 Space Station 14 Contributors
//
// SPDX-License-Identifier: AGPL-3.0-or-later

using Robust.Shared.Serialization;

namespace Content.Shared._Mini.StationsControl;

[Serializable, NetSerializable]
public enum StationsControlConsoleUiKey
{
    Key,
}

[Serializable, NetSerializable]
public sealed class StationsControlConsoleState : BoundUserInterfaceState
{
    public List<string> StationsList { get; }
    public List<NetEntity> StationsNetIds { get; }
    public List<List<string>> StationAlertLevels { get; }
    public List<string> CurrentAlertLevels { get; }
    public TimeSpan? ShuttleTimeLeft { get; }

    public StationsControlConsoleState(
        List<string> stationsList,
        List<NetEntity> stationsNetIds,
        List<List<string>> stationAlertLevels,
        List<string> currentAlertLevels,
        TimeSpan? shuttleTimeLeft)
    {
        StationsList = stationsList;
        StationsNetIds = stationsNetIds;
        StationAlertLevels = stationAlertLevels;
        CurrentAlertLevels = currentAlertLevels;
        ShuttleTimeLeft = shuttleTimeLeft;
    }
}

[Serializable, NetSerializable]
public sealed class StationsControlConsoleSetAlertLevelMessage : BoundUserInterfaceMessage
{
    public NetEntity Station { get; }
    public string AlertLevel { get; }

    public StationsControlConsoleSetAlertLevelMessage(NetEntity station, string alertLevel)
    {
        Station = station;
        AlertLevel = alertLevel;
    }
}

[Serializable, NetSerializable]
public sealed class StationsControlConsoleCallEvacMessage : BoundUserInterfaceMessage
{
    public TimeSpan CountdownTime { get; }
    public StationsControlConsoleCallEvacMessage(TimeSpan countdownTime) { CountdownTime = countdownTime; }
}

[Serializable, NetSerializable]
public sealed class StationsControlConsoleRecallEvacMessage : BoundUserInterfaceMessage
{
    public StationsControlConsoleRecallEvacMessage() { }
}
