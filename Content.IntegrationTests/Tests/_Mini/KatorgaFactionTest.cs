// SPDX-FileCopyrightText: 2026 Space Station 14 Contributors
//
// SPDX-License-Identifier: AGPL-3.0-or-later

// SPDX-FileCopyrightText: 2026 Mini Station
// Мини-станция/Freaky-station, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/ministation/mini-station-goob/master/LICENSE.TXT

using System.Linq;
using System.Threading.Tasks;
using Content.Client._Mini.Katorga;
using Content.Server._Mini.Katorga;
using Content.Server.GameTicking;
using Content.Shared._Mini.Katorga;
using Content.Shared._Mini.MiniCCVars;
using Content.Shared.StatusIcon;
using NUnit.Framework;
using Robust.Server.Player;
using Robust.Shared.Configuration;
using Robust.Shared.GameObjects;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.Tests._Mini;

/// <summary>
///     Выбор фракции каторги идёт через окно позднего подключения: клиент просит состояние,
///     сервер запускает отсчёт и присылает варианты, выбор клиента доходит до сервера.
///     Заодно проверяет, что у каждой фракции есть иконка метки.
/// </summary>
[TestFixture]
public sealed class KatorgaFactionTest
{
    [Test]
    public async Task FactionChoiceWorksThroughLateJoinEvents()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings
        {
            DummyTicker = false,
            Connected = true,
            InLobby = true,
            Destructive = true,
        });

        var server = pair.Server;
        var serverEnt = server.ResolveDependency<IEntityManager>();
        var playerMan = server.ResolveDependency<IPlayerManager>();
        var prototypes = server.ResolveDependency<IPrototypeManager>();
        var cfg = server.ResolveDependency<IConfigurationManager>();
        var ticker = serverEnt.System<GameTicker>();
        var factions = serverEnt.System<KatorgaFactionSystem>();

        // Каторга включается до старта раунда: на RoundStarting грузится карта и её точки спавна,
        // без них фракции не из чего выбирать. Игрок не готов и остаётся в лобби.
        cfg.SetCVar(MiniCCVars.KatorgaEnabled, true);
        await server.WaitPost(() => ticker.StartRound());
        await pair.RunTicksSync(10);

        var player = playerMan.Sessions.Single();
        await pair.RunTicksSync(2);

        foreach (var faction in prototypes.EnumeratePrototypes<KatorgaFactionPrototype>())
        {
            Assert.That(prototypes.HasIndex<FactionIconPrototype>(faction.Icon), Is.True,
                $"у фракции {faction.ID} должна быть иконка метки");
        }

        var clientEnt = pair.Client.ResolveDependency<IEntityManager>();
        var clientUi = clientEnt.System<KatorgaUiSystem>();

        // Пока окно позднего подключения не открывали, выбирать нечего.
        Assert.That(factions.TrySelectFaction(player, "KatorgaFactionMiners"), Is.False,
            "без открытого late join выбор фракции недоступен");

        await server.WaitPost(() => factions.MakeEligible(player));
        await pair.RunTicksSync(2);

        // Окно late join открылось: клиент просит состояние, сервер запускает отсчёт.
        await pair.Client.WaitPost(() => clientUi.RequestFactionState());
        await pair.RunTicksSync(5);

        Assert.That(clientUi.FactionState, Is.Not.Null, "клиент должен получить варианты фракций");
        Assert.Multiple(() =>
        {
            Assert.That(clientUi.FactionState!.Options.Select(option => option.Id),
                Does.Contain("KatorgaFactionMiners"));
            Assert.That(clientUi.FactionState.Selected, Is.Null);
            Assert.That(clientUi.FactionSecondsRemaining, Is.GreaterThan(0),
                "после открытия late join должен идти отсчёт на выбор");
        });

        await pair.Client.WaitPost(() => clientUi.RequestFactionSelect("KatorgaFactionFreemen"));
        await pair.RunTicksSync(5);

        Assert.Multiple(() =>
        {
            Assert.That(factions.GetFaction(player.UserId)?.Id, Is.EqualTo("KatorgaFactionFreemen"),
                "выбор клиента должен дойти до сервера");
            Assert.That(clientUi.FactionState?.Selected, Is.EqualTo("KatorgaFactionFreemen"),
                "клиент должен увидеть, что фракция выбрана");
        });

        await pair.CleanReturnAsync();
    }
}
