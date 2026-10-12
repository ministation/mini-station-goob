// Мини-станция, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/ministation/mini-station-goob/master/LICENSE.TXT

using Content.Shared._Mini.FootWalk;
using Robust.Shared.GameObjects;
using Robust.Shared.IoC;

namespace Content.Client._Mini.FootWalk;

/// <summary>
/// Mini: shared access to the FootWalk whole-body bounce for everything that hangs above the head
/// (status icons, health bar, do-after bar, speech bubble).
/// </summary>
/// <remarks>
/// The bounce lives inside <c>SpriteComponent.Offset</c> and lifts the drawn body by up to
/// <see cref="FootWalkAnimationComponent.BodyBounceAmplitude"/> (1.25 px by default). The body is
/// pixel-snapped when drawn, so an anchor that ignores the bounce visibly shimmers against the body
/// on every step. <see cref="HudFollowsBody"/> decides the fix: true glues the overlay to the body
/// (no relative motion), false keeps it still on screen (the bounce is cancelled out instead).
/// It stays true: nothing toggles it since the client cvar was dropped.
/// </remarks>
public static class FootWalkBounce
{
    /// <summary>
    /// Whether overlays above the head follow the body bounce. Glued to the body by default: an
    /// overlay that stays still while the body hops shimmers against it on every step.
    /// </summary>
    public static bool HudFollowsBody = true;

    /// <summary>Current bounce lift in world units, or 0 when the entity does not walk-animate.</summary>
    public static float Get(EntityUid uid)
    {
        return IoCManager.Resolve<IEntityManager>()
            .TryGetComponent<FootWalkAnimationComponent>(uid, out var walk)
            ? walk.CurrentBounceY
            : 0f;
    }

    /// <summary>
    /// Vertical sprite anchor for an overlay that must not shimmer against the walking body:
    /// the sprite offset minus the current bounce, unless the HUD is glued to the body.
    /// </summary>
    public static float GetAnchoredOffsetY(EntityUid uid, float spriteOffsetY)
    {
        return HudFollowsBody ? spriteOffsetY : spriteOffsetY - Get(uid);
    }

    /// <summary>
    /// Sprite height for anchors above the head. The walk animation lifts the leg layers every step
    /// and <c>GetLocalBounds()</c> includes those offsets, so the live bounds height oscillates by up
    /// to <see cref="FootWalkAnimationComponent.Amplitude"/> — the HUD would bob with it. While the
    /// animation runs we return the rest height the animation recorded instead.
    /// </summary>
    public static float GetStableBoundsHeight(EntityUid uid, float liveHeight)
    {
        var walk = IoCManager.Resolve<IEntityManager>()
            .TryGetComponent<FootWalkAnimationComponent>(uid, out var comp) ? comp : null;

        return walk is { RestBoundsHeight: > 0f } ? walk.RestBoundsHeight : liveHeight;
    }
}
