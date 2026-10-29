// SPDX-License-Identifier: AGPL-3.0-or-later
// Ported from tgstation/tgstation (code/modules/fishing, AGPL-3.0-or-later).

using System.Numerics;
using Content.Shared._Mini.Fishing;
using Content.Shared._Mini.Fishing.Components;
using Content.Shared.Hands.Components;
using Content.Shared.Hands.EntitySystems;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Client.Player;
using Robust.Client.ResourceManagement;
using Robust.Shared.Enums;
using Robust.Shared.GameObjects;
using Robust.Shared.Maths;
using Robust.Shared.Timing;

namespace Content.Client._Mini.Fishing.Overlays;

/// <summary>
/// Draws the fishing line from the rod to the float and the minigame panel right next to the float.
/// </summary>
public sealed partial class FishingOverlay : Overlay
{
    [Dependency] private IEntityManager _ent = default!;
    [Dependency] private IPlayerManager _player = default!;
    [Dependency] private IResourceCache _cache = default!;
    [Dependency] private IGameTiming _timing = default!;

    private readonly TransformSystem _transform;
    private readonly RSI _hud;

    // World space uses meters, one tile is one meter, and sprites are drawn at 32 px per meter.
    // All UI pixel sizes below are converted into meters with HudScale.
    private const float Pixel = 1f / 32f;
    private const float HudScale = Pixel;
    private const float TileCenter = 0.5f;

    private const float Area = 1000f;

    // Same pixel geometry as tg's fishing HUD: 76 px of travel for fish and bait,
    // 80 px tall completion bar, 27 px tall bait at the base bait height.
    private const float SliderHeight = 76f;
    private const float CompletionBarHeight = 80f;
    private const float BaitBaseHeight = 360f;
    private const float BaitBasePixelHeight = 27f;
    private const float PlayfieldWidth = 24f;
    private const float PanelPadding = 4f;

    private static readonly float[] LineOffsets = { -0.7f, 0f, 0.7f };

    private static readonly Color LineColor = Color.FromHex("#f4ecd2");
    private static readonly Color LineShadow = Color.FromHex("#241f13dd");
    private static readonly Color PanelColor = Color.FromHex("#121212dd");
    private static readonly Color PanelBorder = Color.FromHex("#a89d82");
    private static readonly Color BaitColor = Color.FromHex("#7bc96f44");
    private static readonly Color BaitEdgeColor = Color.FromHex("#9be38d");
    private static readonly Color BiteColor = Color.FromHex("#ff4b3a");

    public override OverlaySpace Space => OverlaySpace.WorldSpace;

    public FishingOverlay()
    {
        IoCManager.InjectDependencies(this);
        _transform = _ent.EntitySysManager.GetEntitySystem<TransformSystem>();
        _hud = _cache.GetResource<RSIResource>("/Textures/_Goobstation/Interface/Fishing/fishing_hud.rsi").RSI;
    }

    protected override void Draw(in OverlayDrawArgs args)
    {
        var handle = args.WorldHandle;
        var eyeRot = args.Viewport.Eye?.Rotation ?? Angle.Zero;
        var bounds = args.WorldAABB.Enlarged(0.25f);

        FishingChallengeComponent? localChallenge = null;
        var localPlayer = EntityUid.Invalid;
        if (_player.LocalEntity is { } local && _ent.TryGetComponent<FishingChallengeComponent>(local, out var challenge))
        {
            localChallenge = challenge;
            localPlayer = local;
        }

        EntityUid? localLure = localChallenge?.Lure;
        if (localLure == null &&
            localChallenge?.Rod is { } localRod &&
            _ent.TryGetComponent<FishingRodComponent>(localRod, out var localRodComp))
        {
            localLure = localRodComp.FishingLure;
        }

        var localPlayerPos = localPlayer != EntityUid.Invalid
            ? _transform.GetWorldPosition(localPlayer) + new Vector2(TileCenter, TileCenter)
            : Vector2.Zero;

        var panelDrawn = false;

        var query = _ent.EntityQueryEnumerator<FishingLureComponent, TransformComponent>();
        while (query.MoveNext(out var lureUid, out var lure, out var lureXform))
        {
            if (lureXform.MapID != args.MapId)
                continue;

            var lurePos = _transform.GetWorldPosition(lureXform) + new Vector2(TileCenter, TileCenter);

            Vector2? lineStart = null;
            if (lure.Rod is { } rod &&
                _ent.TryGetComponent<TransformComponent>(rod, out var rodXform) &&
                rodXform.MapID == args.MapId)
            {
                lineStart = _transform.GetWorldPosition(rodXform) + new Vector2(TileCenter, TileCenter);
            }

            // The local fisherman always gets a line from the tip of their rod, even if the
            // lure's Rod field has not arrived on this client yet. The offset follows the
            // player's rotation and the hand holding the rod, so turning around keeps the line attached.
            if (localLure == lureUid && localPlayer != EntityUid.Invalid)
            {
                var playerCenter = _transform.GetWorldPosition(localPlayer) + new Vector2(TileCenter, TileCenter);
                var rodHand = localChallenge?.Rod is { } rodUid ? GetRodHandSign(localPlayer, rodUid) : 1;
                var playerRot = _transform.GetWorldRotation(localPlayer);
                lineStart = playerCenter + playerRot.RotateVec(new Vector2(0.8f * rodHand, 0.6f));
            }

            if (lineStart is { } start && (bounds.Contains(start) || bounds.Contains(lurePos)))
                DrawLine(handle, start, lurePos, eyeRot);

            if (localChallenge == null || localLure != lureUid)
                continue;

            switch (localChallenge.Phase)
            {
                case FishingPhase.Biting:
                    DrawBiteMarker(handle, lurePos, eyeRot);
                    break;
                case FishingPhase.Minigame:
                    DrawMinigame(handle, lurePos, localChallenge, eyeRot);
                    panelDrawn = true;
                    break;
            }
        }

        // The panel must be visible even if the lure briefly fell out of the client's view.
        if (!panelDrawn &&
            localChallenge is { Phase: FishingPhase.Minigame } &&
            localPlayer != EntityUid.Invalid)
        {
            DrawMinigame(handle, localPlayerPos, localChallenge, eyeRot);
        }
    }

    private int GetRodHandSign(EntityUid player, EntityUid rod)
    {
        if (!_ent.TryGetComponent<HandsComponent>(player, out var hands))
            return 1;

        var handsSystem = _ent.System<SharedHandsSystem>();
        foreach (var handId in hands.SortedHands)
        {
            if (!handsSystem.TryGetHeldItem((player, hands), handId, out var held) || held != rod)
                continue;

            if (!handsSystem.TryGetHand((player, hands), handId, out var hand))
                continue;

            return hand.Value.Location == HandLocation.Right ? 1 : -1;
        }

        return 1;
    }

    private static void SetAnchorTransform(DrawingHandleWorld handle, Vector2 anchor, Angle eyeRot)
    {
        // The world handle applies the camera rotation; undo it so the UI and the rope stay upright.
        var matrix = Matrix3x2.Multiply(Matrix3Helpers.CreateRotation(-eyeRot), Matrix3x2.CreateTranslation(anchor));
        handle.SetTransform(matrix);
    }

    private static Vector2 ToLocal(Vector2 world, Vector2 anchor, Angle eyeRot)
    {
        return Vector2.Transform(world - anchor, Matrix3Helpers.CreateRotation(eyeRot));
    }

    private static void DrawLine(DrawingHandleWorld handle, Vector2 start, Vector2 end, Angle eyeRot)
    {
        var distance = (end - start).Length();
        if (distance < 0.02f || distance > 20f)
            return;

        SetAnchorTransform(handle, start, eyeRot);
        var localEnd = ToLocal(end, start, eyeRot);

        // A little sag makes the line read as a rope and not as a UI artefact.
        var control = localEnd * 0.5f - new Vector2(0f, MathF.Min(distance * 0.06f, 0.15f));

        var previous = Vector2.Zero;
        const int steps = 10;
        for (var i = 1; i <= steps; i++)
        {
            var t = i / (float) steps;
            var point = Quadratic(Vector2.Zero, control, localEnd, t);
            var delta = point - previous;
            var length = delta.Length();
            if (length > 0.0001f)
            {
                var perpendicular = new Vector2(-delta.Y, delta.X) / length;
                foreach (var offset in LineOffsets)
                {
                    var shift = perpendicular * (offset * HudScale);
                    handle.DrawLine(previous + shift, point + shift, MathF.Abs(offset) >= 0.7f ? LineShadow : LineColor);
                }
            }

            previous = point;
        }
    }

    private void DrawBiteMarker(DrawingHandleWorld handle, Vector2 anchor, Angle eyeRot)
    {
        SetAnchorTransform(handle, anchor, eyeRot);

        var time = (float) _timing.RealTime.TotalSeconds;
        var bounce = MathF.Abs(MathF.Sin(time * 8f));
        var color = BiteColor.WithAlpha(0.65f + 0.35f * bounce);
        // Sit the mark right above the bobber, not next to it.
        var origin = new Vector2(-3f * HudScale, (14f + bounce * 3f) * HudScale);

        var shadow = 1f * HudScale;
        handle.DrawRect(Box2.FromDimensions(origin - new Vector2(shadow, shadow), new Vector2(8f, 18f) * HudScale), LineShadow);
        handle.DrawRect(Box2.FromDimensions(origin + new Vector2(0f, 20f * HudScale), new Vector2(6f, 6f) * HudScale), LineShadow);
        handle.DrawRect(Box2.FromDimensions(origin, new Vector2(6f, 16f) * HudScale), color);
        handle.DrawRect(Box2.FromDimensions(origin + new Vector2(0f, 19f * HudScale), new Vector2(6f, 6f) * HudScale), color);
    }

    private void DrawMinigame(DrawingHandleWorld handle, Vector2 anchor, FishingChallengeComponent challenge, Angle eyeRot)
    {
        SetAnchorTransform(handle, anchor, eyeRot);

        var barWidth = 32f * HudScale;
        var barHeight = 96f * HudScale;
        var bar = Box2.FromDimensions(
            new Vector2(20f * HudScale, -barHeight * 0.5f),
            new Vector2(barWidth, barHeight));
        var fieldHeight = SliderHeight * HudScale;

        // A dark panel behind the tg sprites makes this read as an interface and not as a wooden
        // object lying above the water.
        var padding = PanelPadding * HudScale;
        var panel = new Box2(
            bar.Left - padding,
            bar.Bottom - padding,
            bar.Right + padding,
            bar.Top + padding);
        handle.DrawRect(panel, PanelColor);
        handle.DrawRect(panel, PanelBorder, filled: false);
        handle.DrawLine(Vector2.Zero, new Vector2(panel.Left, 0f), LineShadow);

        if (TryTex(challenge.Background) is { } background)
            handle.DrawTextureRect(background, bar);

        // Bait zone.
        var baitBottom = bar.Bottom + Math.Clamp(challenge.BaitPosition / Area, 0f, 1f) * fieldHeight;
        var baitHeight = Math.Clamp(BaitBasePixelHeight * challenge.BaitHeight / BaitBaseHeight, 6f, 40f) * HudScale;
        var baitRect = Box2.FromDimensions(
            new Vector2(bar.Left, baitBottom - baitHeight),
            new Vector2(PlayfieldWidth * HudScale, baitHeight));
        handle.DrawRect(baitRect, BaitColor);
        handle.DrawRect(new Box2(baitRect.Left, baitRect.Bottom, baitRect.Right, baitRect.Bottom + HudScale), BaitEdgeColor);

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
        var completionColor = new Color(1f - completion, 0.35f + completion * 0.55f, 0.2f + completion * 0.2f, 0.95f);
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
