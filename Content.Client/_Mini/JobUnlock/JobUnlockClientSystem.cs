// SPDX-FileCopyrightText: 2026 Casha
// SPDX-License-Identifier: LicenseRef-MiniStation-1.0

using Content.Shared._Mini.JobUnlock;
using Content.Shared.Roles;
using Robust.Shared.Prototypes;

namespace Content.Client._Mini.JobUnlock;

public sealed class JobUnlockClientSystem : EntitySystem
{
    public void RequestUnlock(ProtoId<JobPrototype> jobId)
    {
        RaiseNetworkEvent(new JobUnlockPurchaseRequestEvent(jobId));
    }
}
