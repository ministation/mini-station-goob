// SPDX-FileCopyrightText: 2026 Egorik1
// SPDX-License-Identifier: LicenseRef-MiniStation-1.0

using Robust.Shared.Serialization;

namespace Content.Shared._Mini.TypanWar;

[Serializable, NetSerializable]
public sealed class TypanWarStatusEvent : EntityEventArgs
{
    public TypanWarPhase Phase;
    public int NtAlive;
    public int TypanAlive;
    public float NtCapturePoints;
    public float TypanCapturePoints;
    public int CapturePointsToWin;
    public float TimeRemainingSeconds;

    public TypanWarWinner Winner;

    public TypanWarCaptureZoneStatus[] CaptureZones;
    public TypanWarAllyBlip[] AllyBlips;
    public TypanWarMinimapGrid[] MinimapGrids;

    /// <summary>
    /// When false, clients keep their previous minimap payload (zones/allies/grids).
    /// Periodic HUD broadcasts omit minimap data to avoid flooding every client each second.
    /// </summary>
    public bool IncludeMinimapData;

    public TypanWarStatusEvent(
        TypanWarPhase phase,
        int ntAlive,
        int typanAlive,
        float ntCapturePoints,
        float typanCapturePoints,
        int capturePointsToWin,
        float timeRemainingSeconds,
        TypanWarWinner winner = TypanWarWinner.None,
        TypanWarCaptureZoneStatus[]? captureZones = null,
        TypanWarAllyBlip[]? allyBlips = null,
        TypanWarMinimapGrid[]? minimapGrids = null,
        bool includeMinimapData = true)
    {
        Phase = phase;
        NtAlive = ntAlive;
        TypanAlive = typanAlive;
        NtCapturePoints = ntCapturePoints;
        TypanCapturePoints = typanCapturePoints;
        CapturePointsToWin = capturePointsToWin;
        TimeRemainingSeconds = timeRemainingSeconds;
        Winner = winner;
        IncludeMinimapData = includeMinimapData;
        CaptureZones = includeMinimapData
            ? captureZones ?? Array.Empty<TypanWarCaptureZoneStatus>()
            : Array.Empty<TypanWarCaptureZoneStatus>();
        AllyBlips = includeMinimapData
            ? allyBlips ?? Array.Empty<TypanWarAllyBlip>()
            : Array.Empty<TypanWarAllyBlip>();
        MinimapGrids = includeMinimapData
            ? minimapGrids ?? Array.Empty<TypanWarMinimapGrid>()
            : Array.Empty<TypanWarMinimapGrid>();
    }
}
