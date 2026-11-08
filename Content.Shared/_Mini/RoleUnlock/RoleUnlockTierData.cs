// SPDX-FileCopyrightText: 2026 Casha
// SPDX-License-Identifier: LicenseRef-MiniStation-1.0

using System.Collections.Generic;
using Content.Shared.Roles;
using Robust.Shared.Prototypes;

namespace Content.Shared._Mini.RoleUnlock;

[DataDefinition]
public sealed partial class RoleUnlockTierData
{
    [DataField]
    public List<ProtoId<JobPrototype>>? Jobs;

    [DataField]
    public List<ProtoId<AntagPrototype>>? Antags;

    [DataField(required: true)]
    public int MaxCost;

    [DataField]
    public bool Command;

    /// <summary>
    /// For jobs not listed explicitly: minimum display weight (inclusive).
    /// </summary>
    [DataField]
    public int? MinWeight;
}

[DataDefinition]
public sealed partial class RoleUnlockDefaultTierData
{
    [DataField]
    public int MaxCost = 10;

    [DataField]
    public bool Command;
}

[DataDefinition]
public sealed partial class RoleUnlockPricingSettings
{
    [DataField]
    public float HoursPerCoinStep = 4f;

    [DataField]
    public int MinCost = 1;

    [DataField]
    public int RegularCoinsPerStep = 2;

    [DataField]
    public int CommandCoinsPerStep = 4;
}
