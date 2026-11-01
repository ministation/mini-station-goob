// SPDX-License-Identifier: AGPL-3.0-or-later
// Ported from tgstation/tgstation (code/modules/fishing, AGPL-3.0-or-later).

using Content.Shared._Mini.Fishing.Components;

namespace Content.Shared._Mini.Fishing;

/// <summary>
/// Band of the bait zone the fish is currently sitting in.
/// </summary>
public enum FishingBaitZone : byte
{
    Red,
    Yellow,
    Green,
}

/// <summary>
/// The bait zone of the minigame is split into five vertical bands: green in the middle, yellow
/// around it and red at the edges. The closer the fish is to the middle, the faster the catch
/// progresses, so a fish held in the green band is landed in half the time.
/// </summary>
public static class FishingBaitZones
{
    /// <summary>Distance from the center, in halves of the bait height, that is still green.</summary>
    public const float GreenDistance = 1f / 3f;

    /// <summary>Distance from the center, in halves of the bait height, that is still yellow.</summary>
    public const float YellowDistance = 2f / 3f;

    public const float GreenCompletionMultiplier = 2f;
    public const float RedCompletionMultiplier = 0.5f;

    /// <summary>
    /// How far the center of the fish is from the center of the bait zone, in halves of the bait
    /// height, clamped to the edge of the zone.
    /// </summary>
    public static float GetCenterDistance(FishingChallengeComponent challenge)
    {
        var baitCenter = challenge.BaitPosition + challenge.BaitHeight * 0.5f;
        var fishCenter = challenge.FishPosition + challenge.FishHeight * 0.5f;
        var half = MathF.Max(challenge.BaitHeight * 0.5f, 1f);

        return Math.Clamp(MathF.Abs(fishCenter - baitCenter) / half, 0f, 1f);
    }

    public static FishingBaitZone GetZone(float distance)
    {
        if (distance <= GreenDistance)
            return FishingBaitZone.Green;

        return distance <= YellowDistance ? FishingBaitZone.Yellow : FishingBaitZone.Red;
    }

    /// <summary>
    /// Completion gain multiplier, highest in the middle of the zone and lowest at its edges.
    /// </summary>
    public static float GetCompletionMultiplier(float distance)
    {
        return GreenCompletionMultiplier +
               (RedCompletionMultiplier - GreenCompletionMultiplier) * Math.Clamp(distance, 0f, 1f);
    }
}
