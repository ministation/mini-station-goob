// SPDX-License-Identifier: LicenseRef-MiniStation-1.0

﻿using Robust.Shared.Serialization;

namespace Content.Shared.RPSX.DarkForces.Ratvar.Righteous.Abilities.Structures;

[Serializable, NetSerializable]
public enum RatvarAltarVisuals : byte
{
    State
}

[Serializable, NetSerializable]
public enum RatvarAltarState : byte
{
    UnAnchored,
    AnchoredIdle,
    Working
}
