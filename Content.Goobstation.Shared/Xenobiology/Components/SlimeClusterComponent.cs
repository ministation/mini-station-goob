// SPDX-FileCopyrightText: 2025 GoobBot <uristmchands@proton.me>
//
// SPDX-License-Identifier: AGPL-3.0-or-later

using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Goobstation.Shared.Xenobiology.Components;

/// <summary>
/// A xenobiology slime formed from many slimes of the same breed merged together.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class SlimeClusterComponent : Component
{
    [DataField, AutoNetworkedField]
    public int Count = 1;

    [DataField]
    public EntProtoId PeelPrototype = "MobSlimeXenobioBaby";

    [DataField]
    public EntProtoId MergeEffectPrototype = "XenoSlimeClusterMergeEffect";

    [DataField]
    public TimeSpan MergeDelay = TimeSpan.FromSeconds(1.5);

    [DataField]
    public TimeSpan PeelDelay = TimeSpan.FromSeconds(2);

    [DataField]
    public float MergedWalkSpeedModifier = 0.5f;

    [DataField]
    public float MergedSprintSpeedModifier = 0.375f;
}
