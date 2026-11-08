// SPDX-FileCopyrightText: 2026 Casha
// SPDX-License-Identifier: LicenseRef-MiniStation-1.0

using Robust.Shared.Network;
using Robust.Shared.Serialization;

namespace Content.Shared._Mini.GhostRolePurchase;

[Serializable, NetSerializable]
public sealed class GhostRolePurchasedEvent : EntityEventArgs
{
    public NetUserId UserId { get; }
    public string RoleId { get; }

    public GhostRolePurchasedEvent(NetUserId userId, string roleId)
    {
        UserId = userId;
        RoleId = roleId;
    }
}

[Serializable, NetSerializable]
public sealed class GhostRoleTicketUpdateEvent : EntityEventArgs
{
    public int Tickets { get; }

    public GhostRoleTicketUpdateEvent(int tickets)
    {
        Tickets = tickets;
    }
}

[Serializable, NetSerializable]
public sealed class GhostRolePurchaseTimerUpdateEvent : EntityEventArgs
{
    public TimeSpan TimerEndTime { get; }

    public GhostRolePurchaseTimerUpdateEvent(TimeSpan timerEndTime)
    {
        TimerEndTime = timerEndTime;
    }
}

[Serializable, NetSerializable]
public sealed class GhostRolePurchaseRequestEvent : EntityEventArgs
{
    public string RoleId { get; }

    public GhostRolePurchaseRequestEvent(string roleId)
    {
        RoleId = roleId;
    }
}
