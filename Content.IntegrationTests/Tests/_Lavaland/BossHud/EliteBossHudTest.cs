// SPDX-License-Identifier: LicenseRef-MiniStation-1.0

using Content.Server._Lavaland.BossHud;
using Content.Shared._Lavaland.Aggression;
using Content.Shared._Lavaland.BossHud;
using Robust.Shared.GameObjects;

namespace Content.IntegrationTests.Tests._Lavaland.BossHud;

[TestFixture]
[TestOf(typeof(BossHudSystem))]
public sealed class EliteBossHudTest
{
    [TestCase("LavalandEliteLegionnaire")]
    [TestCase("LavalandElitePandora")]
    [TestCase("LavalandEliteHerald")]
    [TestCase("LavalandEliteGoliathBroodmother")]
    public async Task EliteGetsBossHud(string protoId)
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var entMan = server.ResolveDependency<IEntityManager>();
        var testMap = await pair.CreateTestMap();

        EntityUid elite = default;
        EntityUid attacker = default;

        await server.WaitPost(() =>
        {
            elite = entMan.SpawnEntity(protoId, testMap.GridCoords);
            // Any entity works as an aggressor; an item cannot die or wander off mid test.
            attacker = entMan.SpawnEntity("Wrench", testMap.GridCoords);
        });

        await server.WaitRunTicks(5);

        await server.WaitAssertion(() =>
        {
            Assert.That(entMan.TryGetComponent<BossHudComponent>(elite, out var hud), Is.True,
                $"{protoId} must declare a boss HUD");

            if (hud == null)
                return;

            Assert.Multiple(() =>
            {
                Assert.That(hud.Active, Is.False, "elite HUD must stay hidden without aggressors");
                Assert.That(hud.MaxHealth, Is.EqualTo(1000f),
                    "elite HUD max health must come from the MobThresholds death threshold");
                Assert.That(hud.Health, Is.EqualTo(hud.MaxHealth), "undamaged elite must start at full health");
            });
        });

        await server.WaitPost(() =>
        {
            var aggressive = entMan.GetComponent<AggressiveComponent>(elite);
            entMan.System<AggressorsSystem>().AddAggressor((elite, aggressive), attacker);
        });

        await server.WaitRunTicks(2);

        await server.WaitAssertion(() =>
        {
            var hud = entMan.GetComponent<BossHudComponent>(elite);
            Assert.That(hud.Active, Is.True, "elite HUD must show while aggressors are present");
        });

        await pair.CleanReturnAsync();
    }
}
