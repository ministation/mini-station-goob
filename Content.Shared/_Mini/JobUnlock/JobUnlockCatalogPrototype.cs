// SPDX-FileCopyrightText: 2026 Casha
// SPDX-License-Identifier: LicenseRef-MiniStation-1.0

using System.Collections.Generic;
using Content.Shared._Mini.RoleUnlock;
using Content.Shared.Roles;
using Robust.Shared.Prototypes;

namespace Content.Shared._Mini.JobUnlock;

[DataDefinition]
public sealed partial class JobUnlockListingEntry
{
    [DataField(required: true)]
    public ProtoId<JobPrototype> JobId;
}

[Prototype("jobUnlockCatalog")]
public sealed partial class JobUnlockCatalogPrototype : IPrototype
{
    public const string DefaultId = "Default";

    [IdDataField]
    public string ID { get; private set; } = default!;

    [DataField]
    public RoleUnlockPricingSettings Pricing = new();

    [DataField]
    public RoleUnlockDefaultTierData DefaultTier = new();

    [DataField]
    public List<RoleUnlockTierData> Tiers = new();
}
