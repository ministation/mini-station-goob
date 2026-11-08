// SPDX-FileCopyrightText: 2026 Casha
// SPDX-License-Identifier: LicenseRef-MiniStation-1.0

using Content.Shared._Mini.AntagUnlock;
using Content.Shared.Roles;
using Robust.Shared.Prototypes;

namespace Content.Client._Mini.AntagUnlock;

public sealed class AntagUnlockClientSystem : EntitySystem
{
    public void RequestUnlock(ProtoId<AntagPrototype> antagId)
    {
        RaiseNetworkEvent(new AntagUnlockPurchaseRequestEvent(antagId));
    }
}
