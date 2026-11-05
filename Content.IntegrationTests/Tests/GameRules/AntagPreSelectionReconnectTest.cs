// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Collections.Generic;
using System.Linq;
using Content.IntegrationTests.Pair;
using Content.Server.Antag;
using Content.Server.Antag.Components;
using Content.Server.Database;
using Content.Server.GameTicking;
using Content.Shared.GameTicking;
using Robust.Server.Player;
using Robust.Shared.Enums;
using Robust.Shared.GameObjects;
using Robust.Shared.Network;
using Robust.Shared.Player;

namespace Content.IntegrationTests.Tests.GameRules;

// A delayed antag rule preselects players at round start and only assigns them later. If a preselected
// player reconnects in between, the stored session object is stale, which used to log
// "Unable to check playtimes" and lose the player's antag role.
[TestFixture]
[Category("GameRuleTests")]
public sealed class AntagPreSelectionReconnectTest
{
    [TestPrototypes]
    private const string Prototypes = @"
- type: antag
  id: AntagReconnectTest
  name: roles-antag-syndicate-agent-name
  antagonist: true
  setPreference: true
  objective: roles-antag-syndicate-agent-objective

- type: entity
  id: AntagReconnectTestRule
  parent: BaseGameRule
  components:
  - type: AntagSelection
    removeUponFailedSpawn: false
    definitions:
    - prefRoles: [ AntagReconnectTest ]
";

    [Test]
    public async Task PreselectedSessionIsRematchedAfterReconnect()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings
        {
            DummyTicker = false,
            Connected = true,
            InLobby = true,
        });

        var server = pair.Server;
        var entMan = server.EntMan;
        var ticker = server.System<GameTicker>();
        var antagSys = server.System<AntagSelectionSystem>();
        var playerMan = server.ResolveDependency<IPlayerManager>();
        var userDb = server.ResolveDependency<UserDbDataManager>();

        Assert.That(ticker.RunLevel, Is.EqualTo(GameRunLevel.PreRoundLobby));

        EntityUid ruleUid = default;
        await server.WaitPost(() => ruleUid = entMan.Spawn("AntagReconnectTestRule"));
        var rule = new Entity<AntagSelectionComponent>(ruleUid, entMan.GetComponent<AntagSelectionComponent>(ruleUid));
        var def = rule.Comp.Definitions.Single();

        // Pre-select the player while they are still connected, like a delayed rule does on round start.
        var oldSession = playerMan.Sessions.Single();
        await server.WaitPost(() =>
        {
            rule.Comp.PreSelectedSessions[def] = new HashSet<ICommonSession> { oldSession };
            rule.Comp.PreSelectionsComplete = true;
        });

        // Reconnect: the server gets a new session object for the same user.
        var newSession = await DisconnectReconnect(pair);
        await PoolManager.WaitUntil(server, () => userDb.IsLoadComplete(newSession));
        await pair.SetAntagPreference("AntagReconnectTest", true);

        // Assignment has to use the live session instead of the stale pre-selection object.
        await server.WaitPost(() => antagSys.AssignPreSelectedSessions(rule));

        Assert.That(rule.Comp.PreSelectedSessions[def], Has.Count.EqualTo(1));
        Assert.That(rule.Comp.PreSelectedSessions[def].Single(), Is.EqualTo(newSession));

        await server.WaitPost(() => entMan.DeleteEntity(ruleUid));
        await pair.CleanReturnAsync();
    }

    private static async Task<ICommonSession> DisconnectReconnect(TestPair pair)
    {
        var netManager = pair.Client.ResolveDependency<IClientNetManager>();
        var playerMan = pair.Server.ResolveDependency<IPlayerManager>();
        var oldSession = playerMan.Sessions.Single();
        var name = oldSession.Name;
        var userId = oldSession.UserId;

        await pair.Client.WaitAssertion(() => netManager.ClientDisconnect("Reconnect test."));
        await pair.RunTicksSync(5);
        Assert.That(oldSession.Status, Is.EqualTo(SessionStatus.Disconnected));
        Assert.That(playerMan.Sessions, Is.Empty);

        await Task.WhenAll(pair.Client.WaitIdleAsync(), pair.Client.WaitIdleAsync());
        pair.Client.SetConnectTarget(pair.Server);
        await pair.Client.WaitPost(() => netManager.ClientConnect(null!, 0, name));
        await pair.RunTicksSync(5);

        var newSession = playerMan.Sessions.Single();
        Assert.Multiple(() =>
        {
            Assert.That(newSession, Is.Not.EqualTo(oldSession));
            Assert.That(newSession.UserId, Is.EqualTo(userId));
        });

        return newSession;
    }
}
