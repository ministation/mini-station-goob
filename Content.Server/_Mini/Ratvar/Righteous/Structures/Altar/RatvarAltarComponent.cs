using System;
using Robust.Shared.GameObjects;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization.Manager.Attributes;
using Robust.Shared.Serialization.TypeSerializers.Implementations;
using Robust.Shared.ViewVariables;

namespace Content.Server.RPSX.DarkForces.Ratvar.Righteous.Structures.Altar;

[RegisterComponent]
public sealed partial class RatvarAltarComponent : Component
{
    [DataField(customTypeSerializer: typeof(TimespanSerializer))]
    public TimeSpan ActivateTime = TimeSpan.Zero;

    [ViewVariables]
    public EntityUid BuckledEntity = EntityUid.Invalid;

    [DataField]
    public EntProtoId SoulVessel = "RatvarSoulVessel";

    [DataField("activeType")]
    public AltarActiveType Type = AltarActiveType.Idle;
}

public enum AltarActiveType
{
    Idle,
    Convert,
    Die
}
