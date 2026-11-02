// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared._Lavaland.Aggression;
using Content.Shared._Lavaland.Megafauna.Components;
using Content.Shared._Lavaland.Megafauna.Systems;
using Robust.Shared.GameObjects;

namespace Content.IntegrationTests.Tests._Lavaland.Mobs;

[TestFixture]
[TestOf(typeof(MegafaunaAiComponent))]
public sealed class TendrilAiTest
{
    [Test]
    public async Task TendrilStartsAiWhenAggroed()
    {
        await using var pair = await PoolManager.GetServerClient();

        var server = pair.Server;
        var testMap = await pair.CreateTestMap();
        var entMan = server.ResolveDependency<IEntityManager>();
        var entSysMan = server.ResolveDependency<IEntitySystemManager>();

        EntityUid tendril = default;
        MegafaunaAiComponent ai = null;

        await server.WaitPost(() =>
        {
            tendril = entMan.SpawnAtPosition("LavalandTendrilGoliath", testMap.GridCoords);
            ai = entMan.GetComponent<MegafaunaAiComponent>(tendril);
        });

        await server.WaitRunTicks(5);

        await server.WaitAssertion(() =>
        {
            Assert.That(ai.Active, Is.False, "tendril should be dormant until provoked");
        });

        // In production a player damaging the tendril adds them as an aggressor
        // through AggressorsSystem; here we invoke the same entry point directly
        // (pooled tests have no in-game player session to attach).
        await server.WaitPost(() =>
        {
            var human = entMan.SpawnEntity("MobHuman", testMap.GridCoords);
            var aggressive = entMan.GetComponent<AggressiveComponent>(tendril);
            entSysMan.GetEntitySystem<AggressorsSystem>()
                .AddAggressor((tendril, aggressive), human);
        });

        await server.WaitRunTicks(5);

        await server.WaitAssertion(() =>
        {
            Assert.That(ai.Active, Is.True, "provoked tendril should start its combat AI");
            Assert.That(ai.Schedule, Is.Not.Empty);
        });

        await pair.CleanReturnAsync();
    }
}
