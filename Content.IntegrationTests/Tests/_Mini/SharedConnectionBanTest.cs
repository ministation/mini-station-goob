// SPDX-FileCopyrightText: 2026 Mini Station
// Мини-станция/Freaky-station, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/ministation/mini-station-goob/master/LICENSE.TXT

#nullable enable annotations

using System;
using System.Collections.Immutable;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using Content.Server.Database;
using Content.Shared.Database;
using NUnit.Framework;
using Robust.Shared.Network;
using Robust.UnitTesting;

namespace Content.IntegrationTests.Tests._Mini;

/// <summary>
/// Проверка системы связки банов: активный серверный бан без привязки HWID/IP (как из веб-панели
/// или старый бан) должен находиться по последнему известному HWID забаненного аккаунта, а сам
/// подключающийся аккаунт не должен матчится своими собственными банами.
/// </summary>
[TestFixture]
public sealed class SharedConnectionBanTest
{
    private static readonly IPAddress TestAddress = IPAddress.Parse("203.0.113.10");

    private static ImmutableArray<byte> HwidBytes(byte seed)
    {
        // Явный ImmutableArray.Create: коллекционные выражения для ImmutableArray капризны в sandbox.
        return ImmutableArray.Create(new byte[] { (byte) (seed + 1), 0xAA, 0xBB, 0xCC, 0xDD, 0xEE, 0xFF, 0x01 });
    }

    [Test]
    public async Task BanWithoutHwidBlocksAltByLastSeenHwid()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings
        {
            Connected = true,
            DummyTicker = true,
            // Тест пишет баны и записи player в БД пары, поэтому она не должна переиспользоваться.
            Destructive = true,
        });
        var sDb = pair.Server.ResolveDependency<IServerDbManager>();

        var mainId = new NetUserId(Guid.NewGuid());
        var altId = new NetUserId(Guid.NewGuid());
        var modernHwid = new ImmutableTypedHwid(HwidBytes(0), HwidType.Modern);

        // Основной аккаунт забанен userId-баном без привязки HWID/IP.
        var ban = await sDb.AddBanAsync(new BanDef(
            null,
            BanType.Server,
            ImmutableArray.Create(mainId),
            [],
            [],
            DateTimeOffset.UtcNow,
            null,
            [],
            TimeSpan.Zero,
            "test linked ban",
            NoteSeverity.High,
            null,
            null));
        Assert.That(ban.Id, Is.Not.Null);

        // Основной аккаунт заходил с этой машины — modern HWID записан в player.
        await sDb.UpdatePlayerRecordAsync(mainId, "TestMain", TestAddress, modernHwid);

        // Твинк (другой userId) подключается с того же HWID: бан должен найтись по связке.
        var linked = await sDb.GetSharedConnectionBanAsync(
            altId, TestAddress, null, ImmutableArray.Create(modernHwid.Hwid), false, true);
        Assert.That(linked, Is.Not.Null);
        Assert.That(linked!.Id, Is.EqualTo(ban.Id));

        // Собственные баны подключающегося аккаунта через связку не возвращаются.
        var own = await sDb.GetSharedConnectionBanAsync(
            mainId, TestAddress, null, ImmutableArray.Create(modernHwid.Hwid), false, true);
        Assert.That(own, Is.Null);

        // Данные для findalts: запись основного аккаунта видна вместе с активным баном.
        var players = await sDb.GetPlayersWithLastConnectionAsync();
        var mainRecord = players.Single(p => p.UserId == mainId);
        Assert.That(mainRecord.LastSeenHWId, Is.Not.Null);
        Assert.That(mainRecord.LastSeenHWId!.Type, Is.EqualTo(HwidType.Modern));
        Assert.That(mainRecord.ActiveBan, Is.Not.Null);
        Assert.That(mainRecord.ActiveBan!.BanId, Is.EqualTo(ban.Id));
        Assert.That(mainRecord.ActiveBan.Reason, Is.EqualTo("test linked ban"));

        // Другой HWID не матчится.
        var unrelated = await sDb.GetSharedConnectionBanAsync(
            altId, TestAddress, null, ImmutableArray.Create(HwidBytes(9)), false, true);
        Assert.That(unrelated, Is.Null);
    }
}
