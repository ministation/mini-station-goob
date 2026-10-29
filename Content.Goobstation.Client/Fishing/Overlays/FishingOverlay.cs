// SPDX-License-Identifier: AGPL-3.0-or-later
// Ported from tgstation/tgstation (code/modules/fishing, AGPL-3.0-or-later).

using System.Numerics;
using Content.Goobstation.Shared.Fishing;
using Content.Goobstation.Shared.Fishing.Components;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Client.Player;
using Robust.Client.ResourceManagement;
using Robust.Shared.Enums;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Maths;
using Robust.Shared.Timing;

namespace Content.Goobstation.Client.Fishing.Overlays;

/// <summary>
/// Draws the fishing line from the rod to the float and the minigame bar right next to the float.
/// </summary>
public sealed partial class FishingOverlay : Overlay
{
    [Dependency] private IEntityManager _ent = default!;
    [Dependency] private IPlayerManager _player = default!;
    [Dependency] private IResourceCache _cache = default!;
    [Dependency] private IGameTiming _timing = default!;

    private readonly TransformSystem _transform;

    private const float HudScale = 0.7f;
    private const float Area = 1000f;

    // Same pixel geometry as tg's fishing HUD: 76 px of travel for fish and bait,
    // 80 px tall completion bar, 27 px tall bait at the base bait height.
    private const float SliderHeight = 76f;
    private const float CompletionBarHeight = 80f;
    private const float BaitBaseHeight = 360f;
    private const float BaitBasePixelHeight = 27f;
    private const float PlayfieldWidth = 24f;
    private const float TileCenter = 16f;

    private static readonly Color LineColor = Color.FromHex("#e8e1c8dd");
    private static readonly Color LineShadow = Color.FromHex("#8a806acc");
    private static readonly Color BaitColor = Color.FromHex("#7bc96f33");
    private static readonly Color BaitEdgeColor = Color.FromHex("#7bc96faa");
    private static readonly Color BiteColor = Color.FromHex("#e04a3a");

    private readonly RSI _hud;

    public override OverlaySpace Space => OverlaySpace.WorldSpaceBelowFOV;

    public FishingOverlay()
    {
        IoCManager.InjectDependencies(this);
        _transform = _ent.EntitySysManager.GetEntitySystem<TransformSystem>();
        _hud = _cache.GetResource<RSIResource>("/Textures/_Goobstation/Interface/Fishing/fishing_hud.rsi").RSI;
    }

    protected override void Draw(in OverlayDrawArgs args)
    {
        var handle = args.WorldHandle;
        var bounds = args.WorldAABB.Enlarged(8f);

        FishingChallengeComponent? localChallenge = null;
        EntityUid? localLure = null;
        if (_player.LocalEntity is { } local && _ent.TryGetComponent<FishingChallengeComponent>(local, out var challenge))
        {
            localChallenge = challenge;
            localLure = challenge.Lure;
        }

        var query = _ent.EntityQueryEnumerator<FishingLureComponent, TransformComponent>();
        while (query.MoveNext(out var lureUid, out var lure, out var lureXform))
        {
            if (lureXform.MapID != args.MapId)
                continue;

            var lurePos = _transform.GetWorldPosition(lureXform);

            if (lure.Rod is { } rod &&
                _ent.TryGetComponent<TransformComponent>(rod, out var rodXform) &&
                rodXform.MapID == args.MapId)
            {
                var rodPos = _transform.GetWorldPosition(rodXform);
                if (bounds.Contains(lurePos) || bounds.Contains(rodPos))
                {
                    DrawLine(
                        handle,
                        rodPos + new Vector2(TileCenter, TileCenter),
                        lurePos + new Vector2(TileCenter, TileCenter));
                }
            }
            else if (!bounds.Contains(lurePos))
            {
                continue;
            }

            if (localChallenge == null || localLure != lureUid)
                continue;

            switch (localChallenge.Phase)
            {
                case FishingPhase.Biting:
                    DrawBiteMarker(handle, lurePos);
                    break;
                case FishingPhase.Minigame:
                    DrawMinigame(handle, lurePos, localChallenge);
                    break;
            }
        }
    }

    private static void DrawLine(DrawingHandleWorld handle, Vector2 start, Vector2 end)
    {
        var distance = (end - start).Length();
        if (distance < 0.5f)
            return;

        // A little sag makes the line read as a line and not as a UI artefact.
        var control = (start + end) * 0.5f - new Vector2(0f, MathF.Min(distance * 0.06f, 1.4f));

        var previous = start;
        const int steps = 10;
        for (var i = 1; i <= steps; i++)
        {
            var t = i / (float) steps;
            var point = Quadratic(start, control, end, t);
            var delta = point - previous;
            var length = delta.Length();
            if (length > 0.001f)
            {
                var perpendicular = new Vector2(-delta.Y, delta.X) / length * 0.65f;
                handle.DrawLine(previous - perpendicular, point - perpendicular, LineShadow);
                handle.DrawLine(previous + perpendicular, point + perpendicular, LineShadow);
                handle.DrawLine(previous, point, LineColor);
            }

            previous = point;
        }
    }

    private void DrawBiteMarker(DrawingHandleWorld handle, Vector2 lurePos)
    {
        var time = (float) _timing.RealTime.TotalSeconds;
        var bounce = MathF.Abs(MathF.Sin(time * 8f));
        var color = BiteColor.WithAlpha(0.55f + 0.45f * bounce);
        var origin = lurePos + new Vector2(18f, 28f + bounce * 4f);

        handle.DrawRect(Box2.FromDimensions(origin, new Vector2(5f, 14f)), color);
        handle.DrawRect(Box2.FromDimensions(origin + new Vector2(0f, 18f), new Vector2(5f, 5f)), color);
    }

    private void DrawMinigame(DrawingHandleWorld handle, Vector2 lurePos, FishingChallengeComponent challenge)
    {
        var barWidth = 32f * HudScale;
        var barHeight = 96f * HudScale;
        var bar = Box2.FromDimensions(
            lurePos + new Vector2(20f, TileCenter - barHeight * 0.5f),
            new Vector2(barWidth, barHeight));
        var fieldHeight = SliderHeight * HudScale;

        if (TryTex(challenge.Background) is { } background)
            handle.DrawTextureRect(background, bar);

        // Bait zone.
        var baitBottom = bar.Bottom + Math.Clamp(challenge.BaitPosition / Area, 0f, 1f) * fieldHeight;
        var baitHeight = Math.Clamp(BaitBasePixelHeight * challenge.BaitHeight / BaitBaseHeight, 6f, 40f) * HudScale;
        var baitRect = Box2.FromDimensions(
            new Vector2(bar.Left, baitBottom - baitHeight),
            new Vector2(PlayfieldWidth * HudScale, baitHeight));
        handle.DrawRect(baitRect, BaitColor);
        handle.DrawRect(new Box2(baitRect.Left, baitRect.Bottom, baitRect.Right, baitRect.Bottom + 1f), BaitEdgeColor);

        // Fish sprite, its position inside the 32x96 hud texture is shifted by the minigame position.
        if (TryTex(challenge.FishIcon) is { } fishIcon)
        {
            var fishOffset = Math.Clamp(challenge.FishPosition / Area, 0f, 1f) * fieldHeight;
            var fishRect = new Box2(bar.Left, bar.Bottom - fishOffset, bar.Right, bar.Top - fishOffset);
            DrawClipped(handle, fishIcon, fishRect, bar);
        }

        // Completion column on the right. It is 80 px tall in tg, not the full 96 px sprite.
        var completion = Math.Clamp(challenge.Completion / 100f, 0f, 1f);
        var completionHeight = CompletionBarHeight * HudScale * completion;
        var completionColor = new Color(1f - completion, 0.35f + completion * 0.55f, 0.2f + completion * 0.2f, 0.9f);
        var completionRect = new Box2(
            bar.Right - 8f * HudScale,
            bar.Bottom,
            bar.Right - 2f * HudScale,
            bar.Bottom + completionHeight);
        handle.DrawRect(completionRect, completionColor);

        if (TryTex(challenge.FrameState) is { } frame)
            handle.DrawTextureRect(frame, bar);
    }

    private static Vector2 Quadratic(Vector2 start, Vector2 control, Vector2 end, float t)
    {
        var inverse = 1f - t;
        return inverse * inverse * start + 2f * inverse * t * control + t * t * end;
    }

    private static void DrawClipped(DrawingHandleWorld handle, Texture texture, Box2 dest, Box2 clip)
    {
        var intersection = dest.Intersect(clip);
        if (intersection.Width <= 0f || intersection.Height <= 0f)
            return;

        var subRegion = new UIBox2(
            (intersection.Left - dest.Left) / dest.Width * texture.Width,
            (dest.Top - intersection.Top) / dest.Height * texture.Height,
            (intersection.Right - dest.Left) / dest.Width * texture.Width,
            (dest.Top - intersection.Bottom) / dest.Height * texture.Height);

        handle.DrawTextureRectRegion(texture, intersection, subRegion: subRegion);
    }

    private Texture? TryTex(string stateName)
    {
        return _hud.TryGetState(stateName, out var state) ? state.Frame0 : null;
    }
}
