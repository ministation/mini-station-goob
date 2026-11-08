// SPDX-License-Identifier: LicenseRef-MiniStation-1.0

using Content.Shared.Tag;
using Robust.Shared.Prototypes;

namespace Content.Server._Lavaland.Mobs;

/// <summary>
/// Lets a mob be tamed by feeding it. Mirrors TG's /datum/component/tameable: every failed
/// attempt adds <see cref="BonusTameChance"/> to the next one.
/// </summary>
[RegisterComponent]
public sealed partial class TameableMobComponent : Component
{
    /// <summary>
    /// Chance to tame on a hand feed, in percent.
    /// </summary>
    [DataField]
    public float TameChance = 10f;

    /// <summary>
    /// Added to the chance after every failed attempt.
    /// </summary>
    [DataField]
    public float BonusTameChance = 5f;

    /// <summary>
    /// Food prototypes the mob accepts, on top of <see cref="FoodTags"/>.
    /// </summary>
    [DataField]
    public HashSet<ProtoId<EntityPrototype>> Foods = new();

    /// <summary>
    /// Food tags the mob accepts, e.g. Ore for a goldgrub.
    /// </summary>
    [DataField]
    public HashSet<ProtoId<TagPrototype>> FoodTags = new();

    /// <summary>
    /// Current chance, grows with failed attempts. Initialized from <see cref="TameChance"/>
    /// on the first attempt.
    /// </summary>
    [ViewVariables]
    public float CurrentChance;
}
