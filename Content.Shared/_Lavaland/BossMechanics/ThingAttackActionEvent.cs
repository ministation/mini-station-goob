// SPDX-License-Identifier: LicenseRef-MiniStation-1.0

using Content.Shared.Actions;

namespace Content.Shared._Lavaland.BossMechanics;

/// <summary>
/// Attack performed by The Thing. Everything is driven by a single event and one
/// system so the boss does not need an action prototype per special case.
/// </summary>
public enum ThingAttackKind : byte
{
    /// <summary>
    /// Spikes in a radius 2 box around the boss.
    /// </summary>
    Decimate,

    /// <summary>
    /// Lunge at the target, twice in the last phase.
    /// </summary>
    Charge,

    /// <summary>
    /// Short range shriek that makes everyone around drop their items.
    /// </summary>
    Shriek,

    /// <summary>
    /// Spikes in the four cardinal lines around the boss.
    /// </summary>
    CardinalTendrils,

    /// <summary>
    /// Spikes in a box around the target, bigger in the last phase.
    /// </summary>
    BigTendrils,

    /// <summary>
    /// Acid puddles around the boss, last phase only.
    /// </summary>
    AcidSpit,
}

public sealed partial class ThingAttackActionEvent : WorldTargetActionEvent
{
    [DataField(required: true)]
    public ThingAttackKind Attack;
}
