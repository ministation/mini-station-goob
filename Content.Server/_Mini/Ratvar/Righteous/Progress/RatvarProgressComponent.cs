using System;
using Robust.Shared.GameObjects;
using Robust.Shared.ViewVariables;

namespace Content.Server.RPSX.DarkForces.Ratvar.Righteous.Progress;

[RegisterComponent]
public sealed partial class RatvarProgressComponent : Component
{
    [DataField]
    public int CurrentPower;

    [ViewVariables]
    public EntityUid RatvarBeaconsObjective = EntityUid.Invalid;

    [ViewVariables]
    public EntityUid RatvarConvertObjective = EntityUid.Invalid;

    [ViewVariables]
    public EntityUid RatvarPowerObjective = EntityUid.Invalid;

    [ViewVariables]
    public EntityUid RatvarSummonObjective = EntityUid.Invalid;

    [ViewVariables]
    public TimeSpan NextObjectivesCheckTick;

    [DataField]
    public TimeSpan ObjectivesCheckPeriod = TimeSpan.FromSeconds(30);
}
