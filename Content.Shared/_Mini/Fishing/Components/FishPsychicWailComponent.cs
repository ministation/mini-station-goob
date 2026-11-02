// SPDX-License-Identifier: AGPL-3.0-or-later
// Ported from tgstation/tgstation (code/modules/fishing, AGPL-3.0-or-later).

namespace Content.Shared._Mini.Fishing.Components;

/// <summary>
/// A fish that wails when it dies, killing nearby fish and stunning everyone else.
/// </summary>
[RegisterComponent]
public sealed partial class FishPsychicWailComponent : Component
{
    [DataField]
    public float Range = 7f;

    [DataField]
    public TimeSpan KnockdownTime = TimeSpan.FromSeconds(1.5);

    [DataField]
    public TimeSpan EffectTime = TimeSpan.FromSeconds(10);
}
