// SPDX-License-Identifier: AGPL-3.0-or-later

using Robust.Shared.GameStates;

namespace Content.Shared._Lavaland.Fauna;

/// <summary>
/// Marks a mob as eating raw ore out of <see cref="FoodTroughComponent"/>.
/// TG parity: gutlunches and raptors eat ore straight from food troughs.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class TroughFeederComponent : Component
{
    /// <summary>
    /// How long a single ore meal keeps the animal fed.
    /// </summary>
    [DataField]
    public TimeSpan FedDuration = TimeSpan.FromSeconds(60);

    /// <summary>
    /// Radius in which the animal can eat from a trough.
    /// </summary>
    [DataField]
    public float FeedingRadius = 2.5f;

    /// <summary>
    /// When the animal stops counting as fed. Transient.
    /// </summary>
    [ViewVariables]
    public TimeSpan FedUntil;
}
