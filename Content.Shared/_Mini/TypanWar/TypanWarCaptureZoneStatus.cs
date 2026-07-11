// SPDX-FileCopyrightText: 2026 Egorik1
// Мини-станция, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/ministation/mini-station-goob/master/LICENSE.TXT

using Robust.Shared.Map;
using Robust.Shared.Serialization;

namespace Content.Shared._Mini.TypanWar;

[Serializable, NetSerializable]
public readonly struct TypanWarCaptureZoneStatus
{
    public readonly string ZoneLabel;
    public readonly string ZoneDisplayName;
    public readonly string ZoneLocaleKey;
    public readonly TypanWarCaptureOwner Owner;
    public readonly TypanWarCaptureOwner HomeFaction;
    public readonly float CaptureProgress;
    public readonly bool Active;
    public readonly float WorldX;
    public readonly float WorldY;

    public TypanWarCaptureZoneStatus(
        string zoneLabel,
        string zoneDisplayName,
        string zoneLocaleKey,
        TypanWarCaptureOwner owner,
        TypanWarCaptureOwner homeFaction,
        float captureProgress,
        bool active,
        float worldX = 0,
        float worldY = 0)
    {
        ZoneLabel = zoneLabel;
        ZoneDisplayName = zoneDisplayName;
        ZoneLocaleKey = zoneLocaleKey;
        Owner = owner;
        HomeFaction = homeFaction;
        CaptureProgress = captureProgress;
        Active = active;
        WorldX = worldX;
        WorldY = worldY;
    }
}
