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
/// <see cref="FootWalkAnimationComponent.BodyBounceAmplitude"/> (1.25 px by default) on every step.
/// It is body animation, not a change of where the character actually stands, so nothing above the
/// head rides it: an overlay glued to the bounce hopped up and down with the feet ("худы трясутся
/// при ходьбе"), while the rest offset keeps it steady at the character's own height.
/// </remarks>
public static class FootWalkBounce
{
    /// <summary>Current bounce lift in world units, or 0 when the entity does not walk-animate.</summary>
    public static float Get(EntityUid uid)
    {
        return IoCManager.Resolve<IEntityManager>()
            .TryGetComponent<FootWalkAnimationComponent>(uid, out var walk)
            ? walk.CurrentBounceY
            : 0f;
    }

    /// <summary>
    /// Vertical sprite anchor for an overlay above the head: the sprite offset the body has at rest,
    /// i.e. with the cosmetic bounce taken back out so the overlay does not hop along with it.
    /// </summary>
    public static float GetAnchoredOffsetY(EntityUid uid, float spriteOffsetY)
    {
        return spriteOffsetY - Get(uid);
    }

    /// <summary>
    /// Sprite height for anchors above the head. The walk animation lifts the leg layers every step
    /// and <c>GetLocalBounds()</c> includes those offsets, so the live bounds height oscillates by up
    /// to <see cref="FootWalkAnimationComponent.Amplitude"/> - the HUD would bob with it. While the
    /// animation runs we return the rest height the animation recorded instead.
    /// </summary>
    public static float GetStableBoundsHeight(EntityUid uid, float liveHeight)
    {
        var walk = IoCManager.Resolve<IEntityManager>()
            .TryGetComponent<FootWalkAnimationComponent>(uid, out var comp) ? comp : null;

        return walk is { RestBoundsHeight: > 0f } ? walk.RestBoundsHeight : liveHeight;
    }
}
