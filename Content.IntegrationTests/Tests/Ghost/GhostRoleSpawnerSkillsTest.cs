// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Linq;
using Content.Server.Ghost.Roles.Components;
using Content.Shared._CorvaxGoob.Skills;
using Robust.Server.Player;
using Robust.Shared.GameObjects;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.Tests.Ghost;

/// <summary>
/// Reinforcement dolls are spawned by a <see cref="GhostRoleMobSpawnerComponent"/> instead of a
/// takeover ghost role, so the skills declared on the radio ghost role must be granted here too.
/// Ghost roles without a declared list default to all skills.
/// </summary>
[TestFixture]
public sealed class GhostRoleSpawnerSkillsTest
{
    private const string SpawnerProtoId = "GhostRoleSkillsSpawnerTest";
    private const string MobProtoId = "GhostRoleSkillsMobTest";
    private const string DefaultSpawnerProtoId = "GhostRoleDefaultSkillsSpawnerTest";
    private const string DefaultMobProtoId = "GhostRoleDefaultSkillsMobTest";

    [TestPrototypes]
    private const string Prototypes = $"""
        - type: entity
          id: {MobProtoId}
          components:
          - type: MobState
          - type: MobThresholds
            thresholds:
              0: Alive

        - type: entity
          id: {SpawnerProtoId}
          components:
          - type: GhostRole
            makeSentient: false
            skills:
            - All
          - type: GhostRoleMobSpawner
            prototype: {MobProtoId}

        - type: entity
          id: {DefaultMobProtoId}
          components:
          - type: MobState
          - type: MobThresholds
            thresholds:
              0: Alive

        - type: entity
          id: {DefaultSpawnerProtoId}
          components:
          - type: GhostRole
            makeSentient: false
          - type: GhostRoleMobSpawner
            prototype: {DefaultMobProtoId}
        """;

    [Test]
    public async Task SpawnedGhostRoleGetsDeclaredSkills()
    {
        await AssertSpawnerGrantsAllSkills(SpawnerProtoId);
    }

    [Test]
    public async Task SpawnedGhostRoleWithoutDeclaredSkillsGetsAllSkills()
    {
        await AssertSpawnerGrantsAllSkills(DefaultSpawnerProtoId);
    }

    private static async Task AssertSpawnerGrantsAllSkills(string spawnerProtoId)
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings
        {
            Connected = true,
            DummyTicker = false,
            Dirty = true
        });
        var server = pair.Server;
        var entMan = server.ResolveDependency<IEntityManager>();
        var playerMan = server.ResolveDependency<IPlayerManager>();
        var mapData = await pair.CreateTestMap();
        var session = playerMan.Sessions.Single();

        EntityUid mob = default;
        await server.WaitPost(() =>
        {
            var spawner = entMan.SpawnEntity(spawnerProtoId, mapData.GridCoords);
            var ev = new TakeGhostRoleEvent(session);
            entMan.EventBus.RaiseLocalEvent(spawner, ref ev);

            Assert.That(ev.TookRole, Is.True, "the spawner ghost role should be taken");
            Assert.That(session.AttachedEntity, Is.Not.Null, "the player should control the spawned mob");
            mob = session.AttachedEntity!.Value;
        });

        await server.WaitRunTicks(5);

        await server.WaitAssertion(() =>
        {
            Assert.That(entMan.TryGetComponent<EntitySkillsComponent>(mob, out var skills), Is.True,
                "the spawned reinforcement mob has no skills component");
            Assert.That(skills!.Skills, Does.Contain(Skills.All));
        });

        await pair.CleanReturnAsync();
    }
}
