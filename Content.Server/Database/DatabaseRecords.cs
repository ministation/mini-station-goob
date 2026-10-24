// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Collections.Immutable;
using System.Net;
using Content.Shared.Database;
using Robust.Shared.Network;

namespace Content.Server.Database;

// This file contains copies of records returned from the database.
// We can't return the raw EF Core entities as they are often unsuited.
// (e.g. datetime handling of Microsoft.Data.Sqlite)

public interface IAdminRemarksRecord
{
    public int Id { get; }

    public ImmutableArray<RoundRecord> Rounds { get; }

    public ImmutableArray<PlayerRecord> Players { get; }
    public TimeSpan PlaytimeAtNote { get; }

    public string Message { get; }

    public PlayerRecord? CreatedBy { get; }

    public DateTimeOffset CreatedAt { get; }

    public PlayerRecord? LastEditedBy { get; }

    public DateTimeOffset? LastEditedAt { get; }
    public DateTimeOffset? ExpirationTime { get; }

    public bool Deleted { get; }
}

public sealed record BanNoteRecord(
    int Id,
    BanType Type,
    ImmutableArray<RoundRecord> Rounds,
    ImmutableArray<PlayerRecord> Players,
    TimeSpan PlaytimeAtNote,
    string Message,
    NoteSeverity Severity,
    PlayerRecord? CreatedBy,
    DateTimeOffset CreatedAt,
    PlayerRecord? LastEditedBy,
    DateTimeOffset? LastEditedAt,
    DateTimeOffset? ExpirationTime,
    bool Deleted,
    PlayerRecord? UnbanningAdmin,
    DateTime? UnbanTime,
    ImmutableArray<BanRoleDef> Roles) : IAdminRemarksRecord;

public sealed record AdminNoteRecord(
    int Id,
    RoundRecord? Round,
    PlayerRecord? Player,
    TimeSpan PlaytimeAtNote,
    string Message,
    NoteSeverity Severity,
    PlayerRecord? CreatedBy,
    DateTimeOffset CreatedAt,
    PlayerRecord? LastEditedBy,
    DateTimeOffset? LastEditedAt,
    DateTimeOffset? ExpirationTime,
    bool Deleted,
    PlayerRecord? DeletedBy,
    DateTimeOffset? DeletedAt,
    bool Secret) : IAdminRemarksRecord
{
    ImmutableArray<RoundRecord> IAdminRemarksRecord.Rounds => Round != null ? [Round] : [];
    ImmutableArray<PlayerRecord> IAdminRemarksRecord.Players => Player != null ? [Player] : [];
}

public sealed record AdminWatchlistRecord(
    int Id,
    RoundRecord? Round,
    PlayerRecord? Player,
    TimeSpan PlaytimeAtNote,
    string Message,
    PlayerRecord? CreatedBy,
    DateTimeOffset CreatedAt,
    PlayerRecord? LastEditedBy,
    DateTimeOffset? LastEditedAt,
    DateTimeOffset? ExpirationTime,
    bool Deleted,
    PlayerRecord? DeletedBy,
    DateTimeOffset? DeletedAt) : IAdminRemarksRecord
{
    ImmutableArray<RoundRecord> IAdminRemarksRecord.Rounds => Round != null ? [Round] : [];
    ImmutableArray<PlayerRecord> IAdminRemarksRecord.Players => Player != null ? [Player] : [];
}

public sealed record AdminMessageRecord(
    int Id,
    RoundRecord? Round,
    PlayerRecord? Player,
    TimeSpan PlaytimeAtNote,
    string Message,
    PlayerRecord? CreatedBy,
    DateTimeOffset CreatedAt,
    PlayerRecord? LastEditedBy,
    DateTimeOffset? LastEditedAt,
    DateTimeOffset? ExpirationTime,
    bool Deleted,
    PlayerRecord? DeletedBy,
    DateTimeOffset? DeletedAt,
    bool Seen,
    bool Dismissed) : IAdminRemarksRecord
{
    ImmutableArray<RoundRecord> IAdminRemarksRecord.Rounds => Round != null ? [Round] : [];
    ImmutableArray<PlayerRecord> IAdminRemarksRecord.Players => Player != null ? [Player] : [];
}

public sealed record PlayerRecord(
    NetUserId UserId,
    DateTimeOffset FirstSeenTime,
    string LastSeenUserName,
    DateTimeOffset LastSeenTime,
    IPAddress? LastSeenAddress,
    ImmutableTypedHwid? HWId);

/// <summary>
///     Mini: активный серверный бан, выданный при связке аккаунтов (см. <see cref="PlayerLastConnection"/>).
/// </summary>
public sealed record LinkedBanInfo(
    int BanId,
    string Reason,
    NoteSeverity Severity,
    DateTimeOffset? ExpirationTime);

/// <summary>
///     Mini: последняя известная информация о подключении игрока и его активный серверный бан (если есть).
///     Используется системой связки банов для поиска твинков забаненных игроков.
/// </summary>
public sealed record PlayerLastConnection(
    NetUserId UserId,
    string LastSeenUserName,
    DateTimeOffset LastSeenTime,
    IPAddress? LastSeenAddress,
    ImmutableTypedHwid? LastSeenHWId,
    LinkedBanInfo? ActiveBan)
{
    /// <summary>
    ///     Проверяет, совпадает ли последний IP/HWID этой записи с переданными данными подключения.
    /// </summary>
    public bool MatchesConnection(
        IPAddress? address,
        ImmutableArray<byte>? hwId,
        ImmutableArray<ImmutableArray<byte>>? modernHwid,
        bool matchIp,
        bool matchHwid)
    {
        return MatchesConnection(LastSeenAddress, LastSeenHWId, address, hwId, modernHwid, matchIp, matchHwid);
    }

    /// <summary>
    ///     Проверяет, совпадает ли последний известный IP/HWID игрока с данными подключения.
    ///     Совпадение засчитывается по любому из modern HWID подключения.
    /// </summary>
    public static bool MatchesConnection(
        IPAddress? lastAddress,
        ImmutableTypedHwid? lastHwid,
        IPAddress? address,
        ImmutableArray<byte>? hwId,
        ImmutableArray<ImmutableArray<byte>>? modernHwid,
        bool matchIp,
        bool matchHwid)
    {
        if (matchHwid && lastHwid is { } last)
        {
            if (last.Type == HwidType.Legacy
                && hwId is { Length: > 0 }
                && hwId.Value.AsSpan().SequenceEqual(last.Hwid.AsSpan()))
            {
                return true;
            }

            if (last.Type == HwidType.Modern && modernHwid is { Length: > 0 })
            {
                foreach (var hwid in modernHwid.Value)
                {
                    if (hwid.AsSpan().SequenceEqual(last.Hwid.AsSpan()))
                        return true;
                }
            }
        }

        return matchIp
            && address != null
            && lastAddress != null
            && lastAddress.Equals(address);
    }
}

public sealed record RoundRecord(int Id, DateTimeOffset? StartDate, ServerRecord Server);

public sealed record ServerRecord(int Id, string Name);
