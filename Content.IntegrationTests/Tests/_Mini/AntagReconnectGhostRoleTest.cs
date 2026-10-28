// SPDX-FileCopyrightText: 2026 Mini Station
//
// SPDX-License-Identifier: AGPL-3.0-or-later

#nullable enable

using System.Collections.Generic;
using System.Linq;
using Content.IntegrationTests.Pair;
using Content.Server.Antag.Components;
using Content.Server.Ghost.Roles;
using Content.Server.Ghost.Roles.Components;
using Content.Server.Roles;
using Content.Shared.Mind;
using Content.Shared.Ghost;
using Content.Shared.Players;
using Content.Shared.Roles.Components;
using NUnit.Framework;
using Robust.Server.GameObjects;
using Robust.Server.Player;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Robust.UnitTesting;

namespace Content.IntegrationTests.Tests._Mini;

/// <summary>
/// Регрессия AntagMindReconnectSystem: захват ghost-роли привязывает новый разум к сущности
/// до аттача сессии, и реставрация старого антаг-разума на этом аттаче выкидывала игрока
/// в старое тело, роль списывалась впустую. Бинглы, бореры и прочие FCFS-роли.
/// </summary>
[TestFixture]
public sealed class AntagReconnectGhostRoleTest
{
    private const string GhostRoleProtoId = "AntagReconnectGhostRoleEntity";
    private const string TestMobProtoId = "AntagReconnectTestMob";

    [TestPrototypes]
    private const string Prototypes = $"""
        - type: entity
          id: {GhostRoleProtoId}
          components:
          - type: MindContainer
          - type: GhostRole
          - type: GhostTakeoverAvailable
          - type: MobState

        - type: entity
          id: {TestMobProtoId}
          components:
          - type: MindContainer
          - type: MobState

        - type: entity
          parent: BaseMindRoleAntag
          id: TestAntagMindRoleMini
        """;

    [Test]
    public async Task AntagMindDoesNotHijackGhostRoleTakeover()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings
        {
            Dirty = true,
            DummyTicker = false,
            Connected = true
        });
        var server = pair.Server;
        var mapData = await pair.CreateTestMap();

        var entMan = server.ResolveDependency<IEntityManager>();
        var sPlayerMan = server.ResolveDependency<IPlayerManager>();
        var mindSystem = entMan.System<SharedMindSystem>();
        var roleSystem = entMan.System<RoleSystem>();
        var ghostRoles = entMan.System<GhostRoleSystem>();
        var session = sPlayerMan.Sessions.Single();

        // Текущий разум игрока на теле.
        var currentMindId = session.ContentData()!.Mind!.Value;
        EntityUid currentMob = default;
        await server.WaitPost(() =>
        {
            currentMob = entMan.SpawnEntity(TestMobProtoId, mapData.GridCoords);
            mindSystem.TransferTo(currentMindId, currentMob, true);
        });
        await pair.RunTicksSync(10);
        Assert.That(session.AttachedEntity, Is.EqualTo(currentMob));

        // Осиротевший антаг-разум прошлого раунда: тело есть, userId уже снят,
        // в AssignedMinds живого AntagSelectionComponent он даёт высокий score.
        var antagMindId = default(EntityUid);
        EntityUid antagMob = default;
        await server.WaitPost(() =>
        {
            antagMob = entMan.SpawnEntity(TestMobProtoId, mapData.GridCoords);
            antagMindId = mindSystem.CreateMind(null, "TestAntag");
            mindSystem.TransferTo(antagMindId, antagMob);
            roleSystem.MindAddRoles(antagMindId, new List<EntProtoId> { "TestAntagMindRoleMini" });
            var rule = entMan.SpawnEntity(null, mapData.GridCoords);
            entMan.AddComponent<AntagSelectionComponent>(rule).AssignedMinds.Add((antagMindId, "test"));
        });
        await pair.RunTicksSync(10);

        // Захват ghost-роли: тот же код, что и у кнопки "Запросить".
        EntityUid roleMob = default;
        await server.WaitPost(() => roleMob = entMan.SpawnEntity(GhostRoleProtoId, mapData.GridCoords));
        await server.WaitPost(() => ghostRoles.GhostRoleInternalCreateMindAndTransfer(session, roleMob, roleMob));
        await pair.RunTicksSync(10);

        var newMindId = session.ContentData()!.Mind!.Value;
        var antagMind = entMan.GetComponent<MindComponent>(antagMindId);
        Assert.Multiple(() =>
        {
            // Игрок остался в сущности роли, новый разум привязан к ней.
            Assert.That(session.AttachedEntity, Is.EqualTo(roleMob));
            Assert.That(entMan.GetComponent<MindComponent>(newMindId).OwnedEntity, Is.EqualTo(roleMob));
            Assert.That(newMindId, Is.Not.EqualTo(currentMindId));

            // Реставрация не тронула антаг-разум: ни userId, ни тело.
            Assert.That(antagMind.UserId, Is.Null);
            Assert.That(antagMind.OwnedEntity, Is.EqualTo(antagMob));

            // Фоллбек-гост не должен был заспавниться.
            Assert.That(entMan.Count<GhostComponent>(), Is.Zero);
        });

        await pair.CleanReturnAsync();
    }
}
