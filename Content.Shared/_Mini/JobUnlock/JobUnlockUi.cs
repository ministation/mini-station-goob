// SPDX-FileCopyrightText: 2026 Casha
// SPDX-License-Identifier: LicenseRef-MiniStation-1.0

using Robust.Shared.Serialization;

namespace Content.Shared._Mini.JobUnlock;

[Serializable, NetSerializable]
public sealed class JobUnlockPurchaseRequestEvent(string jobId) : EntityEventArgs
{
    public string JobId { get; private set; } = jobId;
}
