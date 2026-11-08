// SPDX-License-Identifier: AGPL-3.0-or-later

namespace Content.Server._Lavaland.Mobs;

/// <summary>
/// TG parity: heals brute damage after a period without taking damage.
/// Used by the necropolis tendril (tg regenerator component).
/// </summary>
[RegisterComponent]
public sealed partial class RegeneratorComponent : Component
{
    /// <summary>
    /// How long the entity has to go undamaged before regeneration starts.
    /// </summary>
    [DataField]
    public TimeSpan RegenerationDelay = TimeSpan.FromSeconds(30);

    /// <summary>
    /// Brute damage healed per second once regeneration is running.
    /// </summary>
    [DataField]
    public float BrutePerSecond = 20f;

    [ViewVariables]
    public TimeSpan LastDamageAt;
}
