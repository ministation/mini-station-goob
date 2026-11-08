// SPDX-FileCopyrightText: 2026 Egorik1
// SPDX-License-Identifier: LicenseRef-MiniStation-1.0

using Robust.Shared.Serialization;

namespace Content.Shared._Mini.TypanWar;

[Serializable, NetSerializable]
public sealed class TypanWarBalanceStatusEvent : EntityEventArgs
{
    public bool Active;
    public bool AllowNanotrasen;
    public bool AllowTypan;
    public int NtJoined;
    public int TypanJoined;

    public TypanWarBalanceStatusEvent()
    {
    }

    public TypanWarBalanceStatusEvent(
        bool active,
        bool allowNanotrasen,
        bool allowTypan,
        int ntJoined,
        int typanJoined)
    {
        Active = active;
        AllowNanotrasen = allowNanotrasen;
        AllowTypan = allowTypan;
        NtJoined = ntJoined;
        TypanJoined = typanJoined;
    }
}
