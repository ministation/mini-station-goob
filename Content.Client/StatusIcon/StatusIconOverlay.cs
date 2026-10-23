// SPDX-License-Identifier: AGPL-3.0-or-later

using Robust.Shared.IoC;
using Content.Client._Mini.FootWalk;
using Content.Shared.StatusIcon;
using Content.Shared.StatusIcon.Components;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Shared.Enums;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;
using System.Numerics;

namespace Content.Client.StatusIcon;

public sealed class StatusIconOverlay : Overlay
{
    private static readonly ProtoId<ShaderPrototype> UnshadedShader = "unshaded";

    [Dependency] private readonly IEntityManager _entity = default!;
    [Dependency] private readonly IPrototypeManager _prototype = default!;
    [Dependency] private readonly IGameTiming _timing = default!;

    private readonly SpriteSystem _sprite;
    private readonly TransformSystem _transform;
    private readonly StatusIconSystem _statusIcon;
    private readonly ShaderInstance _unshadedShader;

    public override OverlaySpace Space => OverlaySpace.WorldSpaceBelowFOV;

    internal StatusIconOverlay()
    {
        IoCManager.InjectDependencies(this);

        _sprite = _entity.System<SpriteSystem>();
        _transform = _entity.System<TransformSystem>();
        _statusIcon = _entity.System<StatusIconSystem>();
        _unshadedShader = _prototype.Index(UnshadedShader).Instance();
    }

    protected override void Draw(in OverlayDrawArgs args)
    {
        var handle = args.WorldHandle;

        var eyeRot = args.Viewport.Eye?.Rotation ?? default;

        var scaleMatrix = Matrix3Helpers.CreateScale(new Vector2(1, 1));
        var rotationMatrix = Matrix3Helpers.CreateRotation(-eyeRot);

        var query = _entity.AllEntityQueryEnumerator<StatusIconComponent, SpriteComponent, TransformComponent, MetaDataComponent>();
        while (query.MoveNext(out var uid, out var comp, out var sprite, out var xform, out var meta))
        {
            if (xform.MapID != args.MapId || !sprite.Visible)
                continue;

            var bounds = comp.Bounds ?? _sprite.GetLocalBounds((uid, sprite));

            // Mini: sprites and the camera are drawn at the interpolated render position, so icons anchored
            // to the simulation position tick-stepped (shook) against a walking body.
            var worldPos = _transform.GetWorldPosition(xform);

            if (!bounds.Translated(worldPos).Intersects(args.WorldAABB))
                continue;

            // Mini: the live bounds follow the walking legs (up to 2.5 px per step), so anchors are
            // built on the rest height the FootWalk animation recorded instead.
            var stableHeight = FootWalkBounce.GetStableBoundsHeight(uid, bounds.Height);

            var icons = _statusIcon.GetStatusIcons(uid, meta);
            if (icons.Count == 0)
                continue;

            var worldMatrix = Matrix3Helpers.CreateTranslation(worldPos);
            var scaledWorld = Matrix3x2.Multiply(scaleMatrix, worldMatrix);
            var matty = Matrix3x2.Multiply(rotationMatrix, scaledWorld);
            handle.SetTransform(matty);

            var countL = 0;
            var countR = 0;
            var accOffsetL = 0;
            var accOffsetR = 0;
            icons.Sort();

            foreach (var proto in icons)
            {
                if (!_statusIcon.IsVisible((uid, meta), proto))
                    continue;

                var curTime = _timing.RealTime;
                var texture = _sprite.GetFrame(proto.Icon, curTime);

                float yOffset;
                float xOffset;

                // the icons are ordered left to right, top to bottom.
                // extra icons that don't fit are just cut off.
                if (proto.LocationPreference == StatusIconLocationPreference.Left ||
                    proto.LocationPreference == StatusIconLocationPreference.None && countL <= countR)
                {
                    // Mini: the fit check has to use the same rest height as the anchor below, otherwise
                    // the lifting legs pushed the last icon over the edge and it blinked while walking.
                    if (accOffsetL + texture.Height > stableHeight * EyeManager.PixelsPerMeter)
                        break;
                    if (proto.Layer == StatusIconLayer.Base)
                    {
                        accOffsetL += texture.Height;
                        countL++;
                    }
                    yOffset = (stableHeight + FootWalkBounce.GetAnchoredOffsetY(uid, sprite.Offset.Y)) / 2f - (float)(accOffsetL - proto.Offset) / EyeManager.PixelsPerMeter;
                    xOffset = -(bounds.Width + sprite.Offset.X) / 2f;

                }
                else
                {
                    // Mini: see the left column above - same rest height, so icons do not blink.
                    if (accOffsetR + texture.Height > stableHeight * EyeManager.PixelsPerMeter)
                        break;
                    if (proto.Layer == StatusIconLayer.Base)
                    {
                        accOffsetR += texture.Height;
                        countR++;
                    }
                    yOffset = (stableHeight + FootWalkBounce.GetAnchoredOffsetY(uid, sprite.Offset.Y)) / 2f - (float)(accOffsetR - proto.Offset) / EyeManager.PixelsPerMeter;
                    xOffset = (bounds.Width + sprite.Offset.X) / 2f - (float)texture.Width / EyeManager.PixelsPerMeter;

                }

                if (proto.IsShaded)
                    handle.UseShader(null);
                else
                    handle.UseShader(_unshadedShader);

                var position = new Vector2(xOffset, yOffset);
                handle.DrawTexture(texture, position, proto.Color); // Goobstation, added proto.color
            }

            handle.UseShader(null);
            handle.SetTransform(Matrix3x2.Identity);
        }
    }
}
