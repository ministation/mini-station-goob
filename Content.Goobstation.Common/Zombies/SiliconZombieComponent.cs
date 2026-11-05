// SPDX-FileCopyrightText: 2026 Goob Station Contributors
//
// SPDX-License-Identifier: AGPL-3.0-or-later

using Robust.Shared.Audio;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Goobstation.Common.Zombies;

[RegisterComponent, NetworkedComponent]
public sealed partial class SiliconZombieComponent : Component
{
    [DataField]
    public EntProtoId SparkEffect = "EffectSparks";

    [DataField]
    public SoundSpecifier SparkSound = new SoundCollectionSpecifier("sparks");
}
