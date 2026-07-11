using System.Numerics;
using Content.Shared._Mini.TypanWar;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Shared.Enums;
using Robust.Shared.GameObjects;
using Robust.Shared.Graphics;
using Robust.Shared.Graphics.RSI;
using Robust.Shared.Map.Components;
using Robust.Shared.Maths;
using Robust.Shared.Utility;
using static Robust.Client.GameObjects.SpriteComponent;

namespace Content.Client._Mini.TypanWar;

/// <summary>
/// Draws capture zone outlines, and progress bars above flags.
/// </summary>
public sealed class TypanWarCaptureZoneOverlay : Overlay
{
    private static readonly SpriteSpecifier.Rsi AreaSprite = new(new ResPath("/Textures/_Mini/Other/area.rsi"), "base");

    [Dependency] private readonly IEntityManager _ent = default!;
    [Dependency] private readonly IEyeManager _eye = default!;

    private EntityLookupSystem? _lookup;
    private SharedMapSystem? _map;
    private TransformSystem? _xform;
    private TypanWarUiSystem? _war;
    private SpriteSystem? _sprite;
    private RSI.State? _areaState;

    public override OverlaySpace Space => OverlaySpace.WorldSpaceBelowEntities | OverlaySpace.ScreenSpace;

    public TypanWarCaptureZoneOverlay()
    {
        IoCManager.InjectDependencies(this);
        ZIndex = (int) Content.Shared.DrawDepth.DrawDepth.FloorEffects;
    }

    protected override void Draw(in OverlayDrawArgs args)
    {
        _lookup ??= _ent.System<EntityLookupSystem>();
        _map ??= _ent.System<SharedMapSystem>();
        _xform ??= _ent.System<TransformSystem>();
        _war ??= _ent.System<TypanWarUiSystem>();
        _sprite ??= _ent.System<SpriteSystem>();
        _areaState ??= _sprite.GetState(AreaSprite);

        if (_war.Phase != TypanWarPhase.Active)
            return;

        var handle = args.WorldHandle;
        var query = _ent.AllEntityQueryEnumerator<TypanWarCaptureZoneComponent, TransformComponent>();

        while (query.MoveNext(out _, out var zone, out var xform))
        {
            if (!zone.Active)
                continue;

            if (args.Space == OverlaySpace.WorldSpaceBelowEntities)
                DrawZoneHighlight(handle, zone, xform);
        }

        if (args.Space != OverlaySpace.ScreenSpace || args.ViewportControl == null)
            return;

        var screenHandle = args.ScreenHandle;
        query = _ent.AllEntityQueryEnumerator<TypanWarCaptureZoneComponent, TransformComponent>();

        while (query.MoveNext(out _, out var zone, out var xform))
        {
            if (!zone.Active)
                continue;

            DrawCaptureProgress(screenHandle, zone, xform);
        }
    }

    private void DrawZoneHighlight(
        DrawingHandleWorld handle,
        TypanWarCaptureZoneComponent zone,
        TransformComponent xform)
    {
        if (xform.GridUid is not { } gridUid || !_ent.TryGetComponent(gridUid, out MapGridComponent? grid))
            return;

        var color = zone.CaptureOwner switch
        {
            TypanWarCaptureOwner.Nanotrasen => Color.FromHex("#8CB4FF").WithAlpha(0.92f),
            TypanWarCaptureOwner.Typan => Color.FromHex("#FF9898").WithAlpha(0.92f),
            _ => Color.White.WithAlpha(0.88f),
        };

        var fillColor = zone.CaptureOwner switch
        {
            TypanWarCaptureOwner.Nanotrasen => Color.FromHex("#4A7FD4").WithAlpha(0.22f),
            TypanWarCaptureOwner.Typan => Color.FromHex("#C84848").WithAlpha(0.22f),
            _ => Color.FromHex("#D8D8D8").WithAlpha(0.18f),
        };

        var centerTile = _map!.TileIndicesFor(gridUid, grid, xform.Coordinates);
        var half = zone.ZoneHalfExtents;
        var tileSize = grid.TileSize;

        var worldMatrix = _xform!.GetWorldMatrix(gridUid);
        handle.SetTransform(worldMatrix);

        for (var dx = -half.X; dx <= half.X; dx++)
        {
            for (var dy = -half.Y; dy <= half.Y; dy++)
            {
                var tile = centerTile + new Vector2i(dx, dy);
                handle.DrawRect(_lookup!.GetLocalBounds(tile, tileSize), fillColor);
            }
        }

        var minTile = centerTile - half;
        var maxTile = centerTile + half;
        var bottomLeft = _lookup!.GetLocalBounds(minTile, tileSize);
        var topRight = _lookup.GetLocalBounds(maxTile, tileSize);
        var zoneCenter = new Vector2(
            (bottomLeft.Left + topRight.Right) * 0.5f,
            (bottomLeft.Bottom + topRight.Top) * 0.5f);

        var gridRotation = _xform.GetWorldRotation(gridUid);
        var viewAngle = (gridRotation + _eye.CurrentEye.Rotation).Reduced().FlipPositive();
        var dir = Layer.GetDirection(_areaState!.RsiDirections, viewAngle);
        var texture = _areaState.GetFrame(dir, 0);

        var size = texture.Size / (float) EyeManager.PixelsPerMeter;
        var drawBox = Box2.FromDimensions(zoneCenter - size / 2f, size);
        handle.DrawTextureRectRegion(texture, drawBox, color);

        handle.SetTransform(Matrix3x2.Identity);
    }
    private void DrawCaptureProgress(DrawingHandleScreen handle, TypanWarCaptureZoneComponent zone, TransformComponent xform)
    {
        if (zone.CaptureProgress <= 0f || xform.GridUid is not { } gridUid || !_ent.TryGetComponent(gridUid, out MapGridComponent? grid))
            return;

        var fillColor = zone.CapturingOwner switch
        {
            TypanWarCaptureOwner.Nanotrasen => Color.FromHex("#4A7FD4").WithAlpha(0.9f),
            TypanWarCaptureOwner.Typan => Color.FromHex("#C84848").WithAlpha(0.9f),
            _ => Color.FromHex("#E8E8E8").WithAlpha(0.9f),
        };

        var worldMatrix = _xform!.GetWorldMatrix(gridUid);
        var centerTile = _map!.TileIndicesFor(gridUid, grid, xform.Coordinates);
        var localCenter = _lookup!.GetLocalBounds(centerTile, grid.TileSize).Center;
        localCenter += new Vector2(0f, grid.TileSize * 0.55f);
        var worldCenter = Vector2.Transform(localCenter, worldMatrix);
        var screenCenter = _eye.WorldToScreen(worldCenter);

        const float barHalfW = 36f;
        const float barHalfH = 4f;
        var back = Color.FromHex("#252530").WithAlpha(0.55f);

        var box = new UIBox2(
            screenCenter - new Vector2(barHalfW, barHalfH),
            screenCenter + new Vector2(barHalfW, barHalfH));

        handle.DrawRect(box, back);

        var fillWidth = box.Width * Math.Clamp(zone.CaptureProgress, 0f, 1f);
        if (fillWidth > 0f)
        {
            var fillBox = new UIBox2(box.Left, box.Bottom, box.Left + fillWidth, box.Top);
            handle.DrawRect(fillBox, fillColor);
        }
    }
}
