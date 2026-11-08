// SPDX-License-Identifier: LicenseRef-MiniStation-1.0

﻿using Robust.Shared.Audio;
using Robust.Shared.GameObjects;
using Robust.Shared.Serialization.Manager.Attributes;

namespace Content.Server.RPSX.DarkForces.Ratvar.Righteous.Abilities.Enchantment.Items;

[RegisterComponent]
public sealed partial class RatvarShardComponent : Component
{
    [DataField]
    public string TileId = "FloorBrassFilled";

    [DataField]
    public float ConvertRange = 3f;
}
