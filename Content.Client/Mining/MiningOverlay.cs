// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Numerics;
using Content.Shared.Mining;
using Content.Shared.Mining.Components;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Client.Player;
using Robust.Client.ResourceManagement;
using Robust.Shared.Enums;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;
using Robust.Shared.Utility;

namespace Content.Client.Mining;

public sealed class MiningOverlay : Overlay
{
    [Dependency] private readonly IEntityManager _entityManager = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly IPlayerManager _player = default!;
    [Dependency] private readonly IPrototypeManager _proto = default!;
    [Dependency] private readonly IResourceCache _resourceCache = default!;
    private readonly EntityLookupSystem _lookup;
    private readonly SpriteSystem _sprite;
    private readonly TransformSystem _xform;

    private readonly EntityQuery<SpriteComponent> _spriteQuery;
    private readonly EntityQuery<TransformComponent> _xformQuery;

    public override OverlaySpace Space => OverlaySpace.WorldSpace;
    public override bool RequestScreenTexture => false;

    private readonly HashSet<Entity<MiningScannerViewableComponent>> _viewableEnts = new();

    public MiningOverlay()
    {
        IoCManager.InjectDependencies(this);

        _lookup = _entityManager.System<EntityLookupSystem>();
        _sprite = _entityManager.System<SpriteSystem>();
        _xform = _entityManager.System<TransformSystem>();

        _spriteQuery = _entityManager.GetEntityQuery<SpriteComponent>();
        _xformQuery = _entityManager.GetEntityQuery<TransformComponent>();
    }

    protected override void Draw(in OverlayDrawArgs args)
    {
        var handle = args.WorldHandle;

        if (_player.LocalEntity is not { } localEntity ||
            !_entityManager.TryGetComponent<MiningScannerViewerComponent>(localEntity, out var viewerComp))
            return;

        if (viewerComp.LastPingLocation == null)
            return;

        var scaleMatrix = Matrix3Helpers.CreateScale(Vector2.One);

        var animTime = (viewerComp.NextPingTime - _timing.CurTime).TotalSeconds;
        var alpha = animTime < viewerComp.AnimationDuration
            ? 0
            : (float) Math.Clamp((animTime - viewerComp.AnimationDuration) / viewerComp.AnimationDuration, 0f, 1f);
        var color = Color.White.WithAlpha(alpha);

        _viewableEnts.Clear();
        _lookup.GetEntitiesInRange(viewerComp.LastPingLocation.Value, viewerComp.ViewRange, _viewableEnts);
        foreach (var ore in _viewableEnts)
        {
            if (!_xformQuery.TryComp(ore, out var xform) ||
                !_spriteQuery.TryComp(ore, out var sprite))
                continue;

            if (xform.MapID != args.MapId || !sprite.Visible)
                continue;

            if (!TryGetScanTexture((ore, sprite), out var texture, out var rotation))
                continue;

            var gridRot = xform.GridUid == null ? 0 : _xformQuery.CompOrNull(xform.GridUid.Value)?.LocalRotation ?? 0;
            var rotationMatrix = Matrix3Helpers.CreateRotation(gridRot);

            var worldMatrix = Matrix3Helpers.CreateTranslation(_xform.GetWorldPosition(xform));
            var scaledWorld = Matrix3x2.Multiply(scaleMatrix, worldMatrix);
            var matty = Matrix3x2.Multiply(rotationMatrix, scaledWorld);
            handle.SetTransform(matty);

            handle.DrawTexture(texture, -(Vector2) texture.Size / 2f / EyeManager.PixelsPerMeter, rotation, modulate: color);
        }

        handle.SetTransform(Matrix3x2.Identity);
    }

    /// <summary>
    /// Текстура подсветки для камня. Обычно это готовый слой Overlay из прототипа (камень с
    /// заранее заданной рудой). У камней со случайной рудой (каторга, шахтёрские камни) такого
    /// слоя нет: руда выбирается на старте карты, поэтому берём её арт из OrePrototype и режем
    /// по родному RSI камня, чтобы силуэт совпадал с самим камнем.
    /// </summary>
    private bool TryGetScanTexture(Entity<SpriteComponent> ent, out Texture texture, out Angle rotation)
    {
        texture = default!;
        rotation = default;

        if (_sprite.LayerMapTryGet((ent.Owner, ent.Comp), MiningScannerVisualLayers.Overlay, out var idx, false))
        {
            var layer = ent.Comp[idx];
            if (layer.ActualRsi?.Path is { } rsiPath && layer.RsiState.Name is { } state)
            {
                texture = _sprite.GetFrame(new SpriteSpecifier.Rsi(rsiPath, state),
                    TimeSpan.FromSeconds(layer.AnimationTime));
                rotation = layer.Rotation;
                return true;
            }
        }

        if (!_entityManager.TryGetComponent<OreVeinComponent>(ent, out var vein)
            || vein.CurrentOre is not { } oreId
            || !_proto.TryIndex(oreId, out OrePrototype? oreProto)
            || oreProto.OreSprite is not SpriteSpecifier.Rsi oreSprite)
            return false;

        if (ent.Comp.BaseRSI != null && ent.Comp.BaseRSI.TryGetState(oreSprite.RsiState, out _))
        {
            texture = _sprite.GetFrame(new SpriteSpecifier.Rsi(ent.Comp.BaseRSI.Path, oreSprite.RsiState),
                TimeSpan.Zero);
            return true;
        }

        if (!_resourceCache.TryGetResource<RSIResource>(oreSprite.RsiPath, out var oreRsi)
            || !oreRsi.RSI.TryGetState(oreSprite.RsiState, out _))
            return false;

        texture = _sprite.GetFrame(oreSprite, TimeSpan.Zero);
        return true;
    }
}