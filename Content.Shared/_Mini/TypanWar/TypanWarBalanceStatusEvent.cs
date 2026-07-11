// SPDX-FileCopyrightText: 2026 Egorik1
// Мини-станция, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/ministation/mini-station-goob/master/LICENSE.TXT

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
