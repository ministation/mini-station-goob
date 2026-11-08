// SPDX-FileCopyrightText: 2026 Casha
// SPDX-License-Identifier: LicenseRef-MiniStation-1.0

using Robust.Shared.Serialization;

namespace Content.Shared._Mini.AntagUnlock;

[Serializable, NetSerializable]
public sealed class AntagUnlockPurchaseRequestEvent(string antagId) : EntityEventArgs
{
    public string AntagId { get; private set; } = antagId;
}
