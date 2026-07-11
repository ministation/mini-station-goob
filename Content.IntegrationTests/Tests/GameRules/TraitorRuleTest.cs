// SPDX-FileCopyrightText: 2024 Aidenkrz <aiden@djkraz.com>
// SPDX-FileCopyrightText: 2024 IProduceWidgets <107586145+IProduceWidgets@users.noreply.github.com>
// SPDX-FileCopyrightText: 2024 Tayrtahn <tayrtahn@gmail.com>
// SPDX-FileCopyrightText: 2025 Aiden <28298836+Aidenkrz@users.noreply.github.com>
// SPDX-FileCopyrightText: 2025 Ducks <97200673+TwoDucksOnnaPlane@users.noreply.github.com>
// SPDX-FileCopyrightText: 2025 gluesniffler <159397573+gluesniffler@users.noreply.github.com>
//
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Linq;
using Content.Server.Antag.Components;
using Content.Server.GameTicking;
using Content.Server.GameTicking.Rules.Components;
using Content.Server.Mind;
using Content.Server.Roles;
using Content.Shared.GameTicking;
using Content.Shared.GameTicking.Components;
using Content.Shared.Mind;
using Content.Shared.NPC.Prototypes;
using Content.Shared.NPC.Systems;
using Content.Shared.Objectives.Components;
using Robust.Shared.GameObjects;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.Tests.GameRules;

[TestFixture]
public sealed class TraitorRuleTest
{
    /// <summary>
    /// Minimal Traitor preset for integration tests. The live Traitor preset also rolls DummyNonAntag,
    /// which requires five ready players and can cancel the round on empty.yml when that threshold is not met.
    /// </summary>
    [TestPrototypes]
    private const string Prototypes = @"
- type: gamePreset
  id: TraitorIntegrationTest
  alias:
  - traitorintegrationtest
  name: traitor-title
  description: traitor-description
  showInVote: false
  rules:
  - Traitor
";

    private const string TraitorGameRuleProtoId = "Traitor";
    private const string TraitorPresetId = "TraitorIntegrationTest";
    private const string TraitorAntagRoleName = "Traitor";
    private static readonly ProtoId<NpcFactionPrototype> SyndicateFaction = "Syndicate";
    private static readonly ProtoId<NpcFactionPrototype> NanotrasenFaction = "NanoTrasen";

    [Test]
    public async Task TestTraitorObjectives()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings()
        {
            Dirty = true,
            DummyTicker = false,
            Connected = true,
            InLobby = true,
        });
        var server = pair.Server;
        var client = pair.Client;
        var entMan = server.EntMan;
        var protoMan = server.ProtoMan;
        var compFact = server.ResolveDependency<IComponentFactory>();
        var ticker = server.System<GameTicker>();
        var mindSys = server.System<MindSystem>();
        var roleSys = server.System<RoleSystem>();
        var factionSys = server.System<NpcFactionSystem>();

        var minPlayers = 1;
        var maxDifficulty = 0f;
        await server.WaitAssertion(() =>
        {
            Assert.That(protoMan.TryIndex<EntityPrototype>(TraitorGameRuleProtoId, out var gameRuleEnt),
            $"Failed to lookup traitor game rule entity prototype with ID \"{TraitorGameRuleProtoId}\"!");

            Assert.That(gameRuleEnt.TryGetComponent<GameRuleComponent>(out var gameRule, compFact),
            $"Game rule entity {TraitorGameRuleProtoId} does not have a GameRuleComponent!");

            Assert.That(gameRuleEnt.TryGetComponent<AntagRandomObjectivesComponent>(out var randomObjectives, compFact),
            $"Game rule entity {TraitorGameRuleProtoId} does not have an AntagRandomObjectivesComponent!");

            minPlayers = gameRule.MinPlayers;
            maxDifficulty = randomObjectives.MaxDifficulty;
        });

        Assert.That(ticker.RunLevel, Is.EqualTo(GameRunLevel.PreRoundLobby));
        Assert.That(client.AttachedEntity, Is.Null);
        Assert.That(ticker.PlayerGameStatuses[client.User!.Value], Is.EqualTo(PlayerGameStatus.NotReadyToPlay));

        var dummies = await pair.Server.AddDummySessions(minPlayers);
        await pair.RunTicksSync(5);

        Assert.That(pair.Player?.AttachedEntity, Is.Null);
        Assert.That(dummies.All(x => x.AttachedEntity == null));

        await pair.SetAntagPreference(TraitorAntagRoleName, true);

        TraitorRuleComponent traitorRule = null;
        await server.WaitPost(() =>
        {
            ticker.SetGamePreset(TraitorPresetId);

            ticker.ToggleReadyAll(true);
            Assert.That(ticker.PlayerGameStatuses.Values.All(x => x == PlayerGameStatus.ReadyToPlay));

            ticker.StartRound();
        });

        await server.WaitAssertion(() =>
        {
            Assert.That(ticker.RunLevel, Is.EqualTo(GameRunLevel.InRound));
            Assert.That(ticker.PlayerGameStatuses[client.User!.Value], Is.EqualTo(PlayerGameStatus.JoinedGame));
            Assert.That(pair.Player?.AttachedEntity, Is.Not.Null);
            Assert.That(entMan.EntityExists(pair.Player!.AttachedEntity!.Value));

            foreach (var rule in ticker.GetActiveGameRules())
            {
                if (entMan.TryGetComponent<TraitorRuleComponent>(rule, out traitorRule))
                    return;
            }

            Assert.Fail("Failed to find an active Traitor game rule after starting the round.");
        });

        var player = pair.Player!.AttachedEntity!.Value;
        Assert.That(entMan.EntityExists(player));

        var mind = mindSys.GetMind(player)!.Value;
        Assert.That(roleSys.MindIsAntagonist(mind));
        Assert.That(factionSys.IsMember(player, SyndicateFaction), Is.True);
        Assert.That(factionSys.IsMember(player, NanotrasenFaction), Is.False);
        Assert.That(traitorRule.TotalTraitors, Is.EqualTo(1));
        Assert.That(traitorRule.TraitorMinds[0], Is.EqualTo(mind));

        Assert.That(entMan.TryGetComponent<MindComponent>(mind, out var mindComp));
        var totalDifficulty = mindComp.Objectives.Sum(o => entMan.GetComponent<ObjectiveComponent>(o).Difficulty);
        Assert.That(totalDifficulty, Is.AtMost(maxDifficulty),
            $"MaxDifficulty exceeded! Objectives: {string.Join(", ", mindComp.Objectives.Select(o => FormatObjective(o, entMan)))}");
        Assert.That(mindComp.Objectives, Is.Not.Empty,
            $"No objectives assigned!");

        await pair.CleanReturnAsync();
    }

    private static string FormatObjective(Entity<ObjectiveComponent> entity, IEntityManager entMan)
    {
        var meta = entMan.GetComponent<MetaDataComponent>(entity);
        var objective = entMan.GetComponent<ObjectiveComponent>(entity);
        return $"{meta.EntityName} ({objective.Difficulty})";
    }
}
