using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Content.Shared.Maps;
using Content.Shared.Tag;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Prototypes;
using Content.Server.RPSX.DarkForces.Ratvar.Righteous.Structures.Beacon;

namespace Content.Server.RPSX.DarkForces.Ratvar.Righteous.Structures;

public sealed class RatvarBeaconConversionSystem : EntitySystem
{
    [Dependency] private readonly SharedMapSystem _map = default!;
    [Dependency] private readonly ITileDefinitionManager _tileDef = default!;
    [Dependency] private readonly TileSystem _tile = default!;
    [Dependency] private readonly TurfSystem _turf = default!;
    [Dependency] private readonly TagSystem _tag = default!;
    [Dependency] private readonly EntityLookupSystem _lookup = default!;

    [ValidatePrototypeId<EntityPrototype>]
    private const string ClockworkWallProto = "WallClock";

    [ValidatePrototypeId<EntityPrototype>]
    private const string ClockworkWindowProto = "ClockworkWindow";

    [ValidatePrototypeId<EntityPrototype>]
    private const string ClockworkWindowDiagonalProto = "ClockworkWindowDiagonal";

    [ValidatePrototypeId<EntityPrototype>]
    private const string WindowClockworkDirectionalProto = "WindowClockworkDirectional";

    [ValidatePrototypeId<EntityPrototype>]
    private const string PinionAirlockProto = "PinionAirlock";

    [ValidatePrototypeId<EntityPrototype>]
    private const string TileConvertEffect = "RatvarTileSpawnEffect";

    [ValidatePrototypeId<EntityPrototype>]
    private const string WallConvertEffect = "RatvarWallSpawnEffect";

    private const string BrassTileId = "FloorBrassFilled";

    [ValidatePrototypeId<TagPrototype>]
    private const string WallTag = "Wall";

    [ValidatePrototypeId<TagPrototype>]
    private const string AirlockTag = "Airlock";

    [ValidatePrototypeId<TagPrototype>]
    private const string WindowTag = "Window";

    private ISawmill _sawmill = default!;

    public override void Initialize()
    {
        base.Initialize();
        _sawmill = Logger.GetSawmill("ratvar.beacon");
        SubscribeLocalEvent<RatvarBeaconComponent, BeaconConversionEvent>(OnConversionTick);
        SubscribeLocalEvent<RatvarBeaconComponent, ComponentStartup>(OnBeaconStartup);
    }

    private void OnBeaconStartup(EntityUid uid, RatvarBeaconComponent comp, ComponentStartup args)
    {
        comp.CurrentConversionRadius = 0;
    }

    private void OnConversionTick(EntityUid uid, RatvarBeaconComponent component, BeaconConversionEvent args)
    {
        var xform = Transform(uid);
        if (xform.GridUid is not { } gridUid || !TryComp<MapGridComponent>(gridUid, out var grid))
            return;

        var nearbyBeacons = CountNearbyBeacons(uid);
        var totalBeacons = nearbyBeacons + 1;
        var maxRadius = 5 * totalBeacons;
        var currentRadius = component.CurrentConversionRadius;

        ConvertRing(gridUid, grid, xform.Coordinates, currentRadius);

        if (currentRadius > 0)
            ConvertStructures(uid, currentRadius, component.MaxConversionsPerTick);

        if (currentRadius < maxRadius)
            component.CurrentConversionRadius++;
    }

    private void ConvertRing(EntityUid gridUid, MapGridComponent grid, EntityCoordinates coords, int radius)
    {
        if (!_tileDef.TryGetDefinition(BrassTileId, out var brassTileDef))
            return;

        var cBrassTileDef = (ContentTileDefinition) brassTileDef;
        var center = coords.Position;
        var centerTile = new Vector2i((int)MathF.Floor(center.X), (int)MathF.Floor(center.Y));

        for (var dx = -radius; dx <= radius; dx++)
        {
            var maxDy = radius - Math.Abs(dx);
            for (var dy = -maxDy; dy <= maxDy; dy++)
            {
                if (radius > 0 && Math.Abs(dx) + Math.Abs(dy) < radius)
                    continue;

                var tilePos = new Vector2i(centerTile.X + dx, centerTile.Y + dy);
                var tileRef = _map.GetTileRef(gridUid, grid, tilePos);

                if (tileRef.Tile.TypeId == cBrassTileDef.TileId)
                    continue;

                _tile.ReplaceTile(tileRef, cBrassTileDef);
                _tile.PickVariant(cBrassTileDef);
                Spawn(TileConvertEffect, _turf.GetTileCenter(tileRef));
            }
        }
    }

    private void ConvertStructures(EntityUid beaconUid, int radius, int maxConversions)
    {
        var xform = Transform(beaconUid);
        var converted = 0;

        // Стены
        var entities = _lookup.GetEntitiesInRange<TagComponent>(xform.Coordinates, radius);
        foreach (var (entity, tagComp) in entities)
        {
            if (converted >= maxConversions)
                break;

            if (_tag.HasTag(tagComp, WallTag) && !HasComp<WallClockComponent>(entity))
            {
                var entXform = Transform(entity);
                Spawn(ClockworkWallProto, entXform.Coordinates);
                Spawn(WallConvertEffect, entXform.Coordinates);
                QueueDel(entity);
                converted++;
            }
        }

        // Окна
        entities = _lookup.GetEntitiesInRange<TagComponent>(xform.Coordinates, radius);
        foreach (var (entity, tagComp) in entities)
        {
            if (converted >= maxConversions)
                break;

            var protoId = Prototype(entity)?.ID;
            if (protoId == null)
                continue;

            var isWindow = _tag.HasTag(tagComp, WindowTag) || protoId.Contains("Window");
            if (!isWindow || HasComp<ClockworkWindowComponent>(entity))
                continue;

            var entXform = Transform(entity);
            var rotation = entXform.LocalRotation;
            EntityUid newWindow;

            if (protoId.Contains("Diagonal"))
                newWindow = Spawn(ClockworkWindowDiagonalProto, entXform.Coordinates);
            else if (protoId.Contains("Directional"))
                newWindow = Spawn(WindowClockworkDirectionalProto, entXform.Coordinates);
            else
                newWindow = Spawn(ClockworkWindowProto, entXform.Coordinates);

            Transform(newWindow).LocalRotation = rotation;
            QueueDel(entity);
            converted++;
        }

        // Двери
        entities = _lookup.GetEntitiesInRange<TagComponent>(xform.Coordinates, radius);
        foreach (var (entity, tagComp) in entities)
        {
            if (converted >= maxConversions)
                break;

            if (_tag.HasTag(tagComp, AirlockTag) && !HasComp<PinionAirlockComponent>(entity))
            {
                Spawn(PinionAirlockProto, Transform(entity).Coordinates);
                QueueDel(entity);
                converted++;
            }
        }
    }

    private int CountNearbyBeacons(EntityUid uid)
    {
        var xform = Transform(uid);
        var beacons = _lookup.GetEntitiesInRange<RatvarBeaconComponent>(xform.Coordinates, 5f);
        return beacons.Count - 1;
    }
}

public sealed class BeaconConversionEvent : EntityEventArgs { }

[RegisterComponent]
public sealed partial class WallClockComponent : Component { }

[RegisterComponent]
public sealed partial class PinionAirlockComponent : Component { }

[RegisterComponent]
public sealed partial class ClockworkWindowComponent : Component { }
