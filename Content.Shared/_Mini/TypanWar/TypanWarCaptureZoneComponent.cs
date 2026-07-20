// SPDX-FileCopyrightText: 2026 Egorik1
// Мини-станция, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/ministation/mini-station-goob/master/LICENSE.TXT

using Content.Shared.Maps;
using Robust.Shared.GameStates;
using Robust.Shared.Maths;
using Robust.Shared.Prototypes;

namespace Content.Shared._Mini.TypanWar;

/// <summary>
/// Apex-style capture zone (3×3 open field with a flag in the center). Spawned when combat begins.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
[Access(typeof(SharedTypanWarCaptureZoneSystem))]
public sealed partial class TypanWarCaptureZoneComponent : Component
{
    /// <summary>Zone extent in tiles from the anchor (center tile).</summary>
    [DataField]
    public Vector2i ZoneHalfExtents = new(1, 1);

    [DataField, AutoNetworkedField]
    public TypanWarCaptureOwner CaptureOwner = TypanWarCaptureOwner.Neutral;

    [DataField, AutoNetworkedField]
    public bool Active;

    [DataField, AutoNetworkedField]
    public float CaptureProgress;

    /// <summary>Faction currently filling the capture bar (for client overlay).</summary>
    [DataField, AutoNetworkedField]
    public TypanWarCaptureOwner? CapturingOwner;

    /// <summary>Default owner when the zone is neutral (home station color hint).</summary>
    [DataField]
    public TypanWarCaptureOwner HomeFaction = TypanWarCaptureOwner.Neutral;

    [DataField]
    public float CaptureTimeSeconds = 35f;

    /// <summary>Unused legacy field (contested zones now use majority / freeze instead of accelerated decay).</summary>
    [DataField]
    public float ContestDecayMultiplier = 2f;

    [DataField]
    public float LootIntervalSeconds = 300f;

    /// <summary>Locale key suffix for announcements, e.g. nt / typan / trade.</summary>
    [DataField]
    public string ZoneLocaleKey = "trade";

    /// <summary>Letter label shown to players (A, B, C).</summary>
    [DataField, AutoNetworkedField]
    public string ZoneLabel = "";

    /// <summary>Human-readable location, e.g. station name and nearest nav beacon.</summary>
    [DataField, AutoNetworkedField]
    public string ZoneDisplayName = "";

    /// <summary>Zone C on a trade outpost grid (not a main station).</summary>
    [DataField, AutoNetworkedField]
    public bool IsTradePostZone;

    /// <summary>Trade zone currently on the Typan trade post (false = NT trade post).</summary>
    [DataField, AutoNetworkedField]
    public bool IsTypanTradePost;

    [DataField]
    public EntProtoId NtLootCrate = "CrateNtSurplusBundle";

    [DataField]
    public EntProtoId TypanLootCrate = "CrateTypanSurplusBundle";

    [DataField]
    public EntityUid? FlagEntity;

    [DataField]
    public ProtoId<ContentTileDefinition> NtFloorTile = "FloorBlue";

    [DataField]
    public ProtoId<ContentTileDefinition> TypanFloorTile = "FloorShuttleRed";

    [DataField]
    public ProtoId<ContentTileDefinition> NeutralFloorTile = "FloorSteel";
}
