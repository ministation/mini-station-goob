// SPDX-FileCopyrightText: 2026 Space Station 14 Contributors
//
// SPDX-License-Identifier: AGPL-3.0-or-later

// SPDX-FileCopyrightText: 2026 Mini Station
// Мини-станция/Freaky-station, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/ministation/mini-station-goob/master/LICENSE.TXT

#nullable enable

using System.Linq;
using Content.Server.Database;
using Robust.Server.Player;

namespace Content.IntegrationTests.Tests._Mini;

/// <summary>
/// <see cref="IServerDbManager.SetPlayerAntagTokenAmount"/> пишет строку одним upsert-запросом
/// (INSERT ... ON CONFLICT DO UPDATE): вставка больше не конфликтует с параллельным писателем
/// и не логирует 23505 на unique-индексе player_antag_token.
/// </summary>
[TestFixture]
public sealed class AntagTokenDbTest
{
    [Test]
    public async Task SetPlayerAntagTokenAmountUpsertsAndDeletes()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Connected = true });
        var db = pair.Server.ResolveDependency<IServerDbManager>();
        var session = pair.Server.ResolveDependency<IPlayerManager>().Sessions.Single();
        var tokenId = $"test-upsert-{Guid.NewGuid()}";

        await db.SetPlayerAntagTokenAmount(session.UserId.UserId, tokenId, 3);
        var rows = await db.GetPlayerAntagTokens(session.UserId.UserId, default);
        Assert.That(rows.Single(t => t.TokenId == tokenId).Amount, Is.EqualTo(3),
            "upsert должен вставить новую строку");

        await db.SetPlayerAntagTokenAmount(session.UserId.UserId, tokenId, 5);
        rows = await db.GetPlayerAntagTokens(session.UserId.UserId, default);
        Assert.That(rows.Single(t => t.TokenId == tokenId).Amount, Is.EqualTo(5),
            "повторная запись должна обновить существующую строку, а не падать на unique-индексе");

        await db.SetPlayerAntagTokenAmount(session.UserId.UserId, tokenId, 0);
        rows = await db.GetPlayerAntagTokens(session.UserId.UserId, default);
        Assert.That(rows.Any(t => t.TokenId == tokenId), Is.False,
            "нулевое количество должно удалять строку");

        await pair.CleanReturnAsync();
    }
}
