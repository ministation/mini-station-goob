#nullable enable

using System.Linq;
using System.Numerics;
using Content.IntegrationTests.Pair;
using Content.Server.Mind;
using Content.Server._Mini.AntagTokens;
using NUnit.Framework;
using Robust.Server.GameObjects;
using Robust.Server.Player;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Player;
using Robust.UnitTesting;

namespace Content.IntegrationTests.Tests._Mini;

/// <summary>
/// Регрессия на покупку ghost-ролей за токены (ревенант): auto-join должен выдавать роль,
/// только когда покупатель - призрак и реально оказался в купленной сущности.
/// Иначе монетки возвращаются сразу, а недоставленная роль удаляется.
/// </summary>
[TestFixture]
[TestOf(typeof(AntagTokenSystem))]
public sealed class AntagTokenGhostAutoJoinTest
{
    private const string RevenantProto = "MobRevenantToken";
    private const string RevenantRoleId = "revenant";
    private const int RevenantCost = 3;

    /// <summary>
    /// Призрак купил ревенанта: роль выдаётся, монетки не меняются, метка раунда ставится.
    /// </summary>
    [Test]
    public async Task EligibleGhostReceivesRevenant()
    {
        var (pair, server, session) = await Setup("MobObserver");
        var sEntMan = server.ResolveDependency<IEntityManager>();
        var tokens = sEntMan.System<AntagTokenSystem>();

        EntityUid? revenant = null;
        await server.WaitPost(() =>
        {
            tokens.TestSetupPendingGhostAuto(session, RevenantRoleId, 0);
            revenant = SpawnOnTestMap(server, RevenantProto);
        });
        await server.WaitRunTicks(5);

        var state = tokens.TestGetState(session.UserId);
        Assert.That(state, Is.Not.Null, "token state must be loaded");
        Assert.That(state!.PendingGhostAutoRoleId, Is.Null, "pending must be cleared after delivery");
        Assert.That(state.GhostAntagConsumedMark, Is.True, "round mark must be set after delivery");
        Assert.That(state.Balance, Is.EqualTo(0), "balance must not change on successful delivery");
        Assert.That(session.AttachedEntity, Is.Not.Null, "player must be attached to the purchased revenant");
        Assert.That(
            sEntMan.GetComponent<MetaDataComponent>(session.AttachedEntity!.Value).EntityPrototype?.ID,
            Is.EqualTo(RevenantProto),
            "player must control the purchased revenant entity");
        Assert.That(sEntMan.EntityExists(revenant), Is.True, "delivered revenant must stay alive");

        await pair.CleanReturnAsync();
    }

    /// <summary>
    /// Покупатель не призрок (вернулся в тело): монетки возвращаются сразу, а купленная
    /// сущность удаляется и не остаётся бесплатной раздачей другим призракам.
    /// </summary>
    [Test]
    public async Task IneligibleBuyerGetsRefundAndRoleRemoved()
    {
        var (pair, server, session) = await Setup("MobHuman");
        var sEntMan = server.ResolveDependency<IEntityManager>();
        var tokens = sEntMan.System<AntagTokenSystem>();

        EntityUid? revenant = null;
        await server.WaitPost(() =>
        {
            tokens.TestSetupPendingGhostAuto(session, RevenantRoleId, 0);
            revenant = SpawnOnTestMap(server, RevenantProto);
        });
        await server.WaitRunTicks(5);

        var state = tokens.TestGetState(session.UserId);
        Assert.That(state, Is.Not.Null, "token state must be loaded");
        Assert.That(state!.Balance, Is.EqualTo(RevenantCost), "coins must be refunded immediately");
        Assert.That(state.PendingGhostAutoRoleId, Is.Null, "pending must be cleared after refund");
        Assert.That(state.GhostAntagConsumedMark, Is.False, "round mark must not be set on failed delivery");
        Assert.That(sEntMan.EntityExists(revenant), Is.False, "undelivered paid role must be deleted");

        await pair.CleanReturnAsync();
    }

    /// <summary>
    /// Подключает сессию выбранной сущностью (призрак или тело) на тестовой карте.
    /// Пара живёт до конца теста, поэтому возвращается вместе с результатом.
    /// </summary>
    private static async Task<(TestPair Pair, RobustIntegrationTest.ServerIntegrationInstance Server, ICommonSession Session)>
        Setup(string playerProto)
    {
        var pair = await PoolManager.GetServerClient(new PoolSettings
        {
            DummyTicker = false,
            Connected = true,
        });
        var server = pair.Server;
        var sEntMan = server.ResolveDependency<IServerEntityManager>();
        var playerMan = server.ResolveDependency<IPlayerManager>();
        var mindSys = sEntMan.System<MindSystem>();

        await server.WaitIdleAsync();
        await pair.CreateTestMap();

        Assert.That(pair.Client.Session, Is.Not.Null, "no player session");
        var clientSession = pair.Client.Session!;
        var session = playerMan.GetSessionById(clientSession.UserId);

        await server.WaitPost(() =>
        {
            var mob = sEntMan.SpawnEntity(playerProto, pair.TestMap!.GridCoords);
            var mind = mindSys.CreateMind(session.UserId, "TokenTestPlayer");
            mindSys.TransferTo(mind, mob, mind: mind.Comp);
            playerMan.SetAttachedEntity(session, mob);
        });
        await server.WaitRunTicks(5);

        return (pair, server, session);
    }

    private static EntityUid SpawnOnTestMap(RobustIntegrationTest.ServerIntegrationInstance server, string proto)
    {
        var sEntMan = server.ResolveDependency<IServerEntityManager>();
        var mapSys = sEntMan.System<SharedMapSystem>();

        mapSys.CreateMap(out var mapId);
        return sEntMan.SpawnEntity(proto, new MapCoordinates(new Vector2(0.5f, 0.5f), mapId));
    }
}
