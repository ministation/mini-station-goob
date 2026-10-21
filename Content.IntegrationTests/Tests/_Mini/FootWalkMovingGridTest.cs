// Мини-станция, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/ministation/mini-station-goob/master/LICENSE.TXT

#nullable enable

using System.Numerics;
using Content.Client._Mini.FootWalk;
using Content.IntegrationTests.Pair;
using Content.Shared._Mini.FootWalk;
using Content.Shared.Gravity;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Physics;
using Robust.Shared.Physics.Components;
using Robust.Shared.Physics.Systems;

namespace Content.IntegrationTests.Tests._Mini;

/// <summary>
/// Регрессия на "ходьбу на месте" у персонажей, стоящих на движущемся шаттле.
/// </summary>
/// <remarks>
/// В движке LinearVelocity тела уже относительна родителя (грида/шаттла): у стоящего
/// на движущемся шаттле моба она равна нулю, а карту он проезжает со скоростью шаттла.
/// FootWalkAnimationSystem раньше вычитал из неё скорость грида и получал ложную
/// скорость ~скорости шаттла, из-за чего включался боб ходьбы.
/// </remarks>
[TestFixture]
[TestOf(typeof(FootWalkAnimationSystem))]
public sealed class FootWalkMovingGridTest
{
    private static readonly Vector2 ShuttleVelocity = new(5f, 0f);

    private const float VelocityTolerance = 0.35f;

    /// <summary>
    /// Эмпирическая фиксация семантики: на сервере и на клиенте у моба, стоящего на
    /// движущемся гриде, LinearVelocity родитель-относительная (0), а скорость по карте
    /// равна скорости грида.
    /// </summary>
    [Test]
    public async Task MovingGridVelocityIsParentRelative()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings
        {
            Connected = true,
            DummyTicker = true,
        });

        var (mob, grid) = await CreateShuttle(pair);

        var sEntMan = pair.Server.ResolveDependency<IEntityManager>();
        var cEntMan = pair.Client.ResolveDependency<IEntityManager>();
        var sPhysics = sEntMan.System<SharedPhysicsSystem>();
        var cPhysics = cEntMan.System<SharedPhysicsSystem>();

        Vector2 sMobVelocity = default;
        Vector2 sMobMapVelocity = default;
        Vector2 sGridVelocity = default;
        Vector2 sGridMapVelocity = default;
        bool sWeightless = true;

        await pair.Server.WaitAssertion(() =>
        {
            var mobBody = sEntMan.GetComponent<PhysicsComponent>(mob);
            var gridBody = sEntMan.GetComponent<PhysicsComponent>(grid);
            sMobVelocity = mobBody.LinearVelocity;
            sGridVelocity = gridBody.LinearVelocity;
            sMobMapVelocity = sPhysics.GetMapLinearVelocity(mob, mobBody);
            sGridMapVelocity = sPhysics.GetMapLinearVelocity(grid, gridBody);
            sWeightless = sEntMan.System<SharedGravitySystem>().IsWeightless(mob);
        });

        Vector2 cMobVelocity = default;
        Vector2 cMobMapVelocity = default;
        Vector2 cGridVelocity = default;
        Vector2 cGridMapVelocity = default;
        bool cWeightless = true;
        var cMob = pair.ToClientUid(mob);
        var cGrid = pair.ToClientUid(grid);

        await pair.Client.WaitAssertion(() =>
        {
            var mobBody = cEntMan.GetComponent<PhysicsComponent>(cMob);
            var gridBody = cEntMan.GetComponent<PhysicsComponent>(cGrid);
            cMobVelocity = mobBody.LinearVelocity;
            cGridVelocity = gridBody.LinearVelocity;
            cMobMapVelocity = cPhysics.GetMapLinearVelocity(cMob, mobBody);
            cGridMapVelocity = cPhysics.GetMapLinearVelocity(cGrid, gridBody);
            cWeightless = cEntMan.System<SharedGravitySystem>().IsWeightless(cMob);
        });

        await TestContext.Out.WriteLineAsync(
            "server: mob.LinearVelocity=" + sMobVelocity +
            ", mob map velocity=" + sMobMapVelocity +
            ", grid.LinearVelocity=" + sGridVelocity +
            ", grid map velocity=" + sGridMapVelocity +
            ", weightless=" + sWeightless);
        await TestContext.Out.WriteLineAsync(
            "client: mob.LinearVelocity=" + cMobVelocity +
            ", mob map velocity=" + cMobMapVelocity +
            ", grid.LinearVelocity=" + cGridVelocity +
            ", grid map velocity=" + cGridMapVelocity +
            ", weightless=" + cWeightless);

        Assert.Multiple(() =>
        {
            Assert.That(sWeightless, Is.False, "Моб на гриде с гравитацией не должен быть невесомым");
            Assert.That(cWeightless, Is.False, "Клиент не увидел гравитацию грида");

            Assert.That(sGridVelocity.Length(), Is.GreaterThan(1f), "Серверный грид не двигается");
            Assert.That(cGridVelocity.Length(), Is.GreaterThan(1f), "Клиент не увидел движущийся грид");

            AssertClose(sMobVelocity, Vector2.Zero,
                "LinearVelocity моба на движущемся гриде должна быть родитель-относительной (0)");
            AssertClose(sMobMapVelocity, sGridVelocity,
                "Скорость моба по карте должна совпадать со скоростью грида");
            AssertClose(sGridMapVelocity, sGridVelocity,
                "Скорость грида по карте должна совпадать с его LinearVelocity");

            AssertClose(cMobVelocity, Vector2.Zero,
                "Клиентская LinearVelocity моба на движущемся гриде должна быть 0");
            AssertClose(cMobMapVelocity, cGridVelocity,
                "Клиентская скорость моба по карте должна совпадать со скоростью грида");
            AssertClose(cGridMapVelocity, cGridVelocity,
                "Клиентская скорость грида по карте должна совпадать с его LinearVelocity");
        });

        await pair.CleanReturnAsync();
    }

    /// <summary>
    /// Постоянный регрессионный тест: стоящий на движущемся гриде персонаж не должен
    /// переходить в WasAnimating и крутить фазу анимации. Положительный контроль рядом
    /// проверяет, что система при этом вообще способна анимировать реальное движение
    /// относительно грида, иначе тест был бы пустым.
    /// </summary>
    [Test]
    public async Task StandingOnMovingGridDoesNotAnimateFootWalk()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings
        {
            Connected = true,
            DummyTicker = true,
        });

        var (mob, grid) = await CreateShuttle(pair);

        var cEntMan = pair.Client.ResolveDependency<IEntityManager>();
        var cMob = pair.ToClientUid(mob);
        var cGrid = pair.ToClientUid(grid);
        var cPhysics = cEntMan.System<SharedPhysicsSystem>();
        var walkSystem = cEntMan.System<FootWalkAnimationSystem>();

        Vector2 cMobVelocity = default;
        Vector2 cGridVelocity = default;
        bool wasAnimating = true;
        float phase = -1f;

        await pair.Client.WaitAssertion(() =>
        {
            var mobBody = cEntMan.GetComponent<PhysicsComponent>(cMob);
            var gridBody = cEntMan.GetComponent<PhysicsComponent>(cGrid);
            cMobVelocity = mobBody.LinearVelocity;
            cGridVelocity = gridBody.LinearVelocity;

            var walk = cEntMan.GetComponent<FootWalkAnimationComponent>(cMob);
            wasAnimating = walk.WasAnimating;
            phase = walk.Phase;
        });

        await TestContext.Out.WriteLineAsync(
            "client before FrameUpdate: mob.LinearVelocity=" + cMobVelocity +
            ", grid.LinearVelocity=" + cGridVelocity +
            ", WasAnimating=" + wasAnimating +
            ", Phase=" + phase);

        Assert.Multiple(() =>
        {
            Assert.That(cGridVelocity.Length(), Is.GreaterThan(1f), "Клиент не увидел движущийся грид");
            AssertClose(cMobVelocity, Vector2.Zero, "У стоящего на шаттле моба должна быть нулевая скорость");
            Assert.That(wasAnimating, Is.False,
                "Стоящий на движущемся шаттле персонаж не должен играть анимацию ходьбы");
            Assert.That(phase, Is.Zero, "Фаза анимации ходьбы не должна накручиваться на движущемся шаттле");
        });

        // Положительный контроль: реальное движение относительно грида обязано включать боб.
        await pair.Client.WaitPost(() =>
        {
            cPhysics.SetLinearVelocity(cMob, new Vector2(2.5f, 0f), body: cEntMan.GetComponent<PhysicsComponent>(cMob));
            walkSystem.FrameUpdate(1f / 60f);
        });

        await pair.Client.WaitAssertion(() =>
        {
            wasAnimating = cEntMan.GetComponent<FootWalkAnimationComponent>(cMob).WasAnimating;
        });

        await TestContext.Out.WriteLineAsync("client after walking FrameUpdate: WasAnimating=" + wasAnimating);

        Assert.That(wasAnimating, Is.True,
            "Положительный контроль: движение относительно грида должно включать анимацию ходьбы");

        await pair.CleanReturnAsync();
    }

    /// <summary>
    /// Создаёт тестовый грид с гравитацией, ставит на него MobHuman и разгоняет грид
    /// до <see cref="ShuttleVelocity"/>, ежесекундно (по тикам) поддерживая скорость:
    /// у динамических тел есть демпфирование.
    /// </summary>
    private static async Task<(EntityUid Mob, EntityUid Grid)> CreateShuttle(TestPair pair)
    {
        var map = await pair.CreateTestMap();
        var grid = map.Grid.Owner;
        var sEntMan = pair.Server.ResolveDependency<IEntityManager>();
        var sPhysics = sEntMan.System<SharedPhysicsSystem>();

        EntityUid mob = default;

        await pair.Server.WaitPost(() =>
        {
            var gridBody = sEntMan.GetComponent<PhysicsComponent>(grid);
            sPhysics.SetBodyType(grid, BodyType.Dynamic, body: gridBody);

            var gravity = sEntMan.GetComponent<GravityComponent>(grid);
            gravity.Inherent = true;
            gravity.Enabled = true;
            sEntMan.Dirty(grid, gravity);

            mob = sEntMan.SpawnEntity("MobHuman", new EntityCoordinates(grid, 0.5f, 0.5f));

            // Пересчитать невесомость сразу, не дожидаясь события смены гравитации.
            var gravitySystem = sEntMan.System<SharedGravitySystem>();
            if (sEntMan.TryGetComponent<GravityAffectedComponent>(mob, out var affected))
                gravitySystem.RefreshWeightless((mob, affected));
        });

        // Грид должен реально двигаться, а не быть статиком: держим скорость на тиках.
        for (var i = 0; i < 10; i++)
        {
            await pair.Server.WaitPost(() =>
            {
                sPhysics.SetLinearVelocity(grid, ShuttleVelocity,
                    body: sEntMan.GetComponent<PhysicsComponent>(grid));
            });

            await pair.RunTicksSync(2);
        }

        return (mob, grid);
    }

    private static void AssertClose(Vector2 actual, Vector2 expected, string message)
    {
        Assert.That(Vector2.Distance(actual, expected), Is.LessThanOrEqualTo(VelocityTolerance),
            message + ": ожидалось " + expected + ", получено " + actual);
    }
}
