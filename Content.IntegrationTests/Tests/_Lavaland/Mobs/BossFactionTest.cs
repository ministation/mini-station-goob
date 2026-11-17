// SPDX-License-Identifier: LicenseRef-MiniStation-1.0

using System.Linq;
using System.Numerics;
using Content.Shared._Lavaland.Aggression;
using Content.Shared.NPC.Components;
using Content.Shared.NPC.Prototypes;
using Content.Shared.NPC.Systems;
using Robust.Shared.GameObjects;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.Tests._Lavaland.Mobs;

/// <summary>
/// Lavaland mining mobs and the fork's bosses must not treat each other as enemies.
/// TG keeps all of them in one mining faction; the fork has two (ported fauna uses
/// SimpleHostile, upstream watchers/goliaths/legions use Lavafauna), so bosses join both.
/// </summary>
[TestFixture]
public sealed class BossFactionTest
{
    private static readonly ProtoId<NpcFactionPrototype> SimpleHostile = "SimpleHostile";
    private static readonly ProtoId<NpcFactionPrototype> Lavafauna = "Lavafauna";
    private static readonly ProtoId<NpcFactionPrototype> Nanotrasen = "NanoTrasen";

    private static readonly string[] BossPrototypes =
    {
        "LavalandBossAshDrake",
        "LavalandBossBloodDrunk",
        "LavalandBossBubblegum",
        "LavalandBossColossus",
        "LavalandBossDemonicFrostMiner",
        "MobHierophant",
        "LavalandBossMegaLegion",
        "LavalandBossThing",
        "LavalandBossWendigo",
    };

    private static readonly string[] BossVariantPrototypes =
    {
        "LavalandBossBloodDrunkGuidance",
        "LavalandBossBloodDrunkHunter",
        "LavalandBossBubblegumBitrunning",
        "LavalandBossLegionMedium",
        "LavalandBossLegionMediumRight",
        "LavalandBossLegionMediumEye",
        "LavalandBossLegionSmall",
        "MobLavalandBubblegumSlaughterling",
    };

    private static readonly string[] ElitePrototypes =
    {
        "LavalandEliteLegionnaire",
        "LavalandElitePandora",
        "LavalandEliteHerald",
        "LavalandEliteGoliathBroodmother",
        "MobLavalandGoliathChild",
        "LavalandLegionBoneTurret",
    };

    [Test]
    public async Task FaunaDoesNotTargetBosses()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var entMan = server.ResolveDependency<IEntityManager>();
        var factions = server.System<NpcFactionSystem>();
        var testMap = await pair.CreateTestMap();

        foreach (var bossId in BossPrototypes)
        {
            EntityUid boss = default;
            EntityUid portedFauna = default;
            EntityUid upstreamFauna = default;
            EntityUid player = default;

            await server.WaitPost(() =>
            {
                boss = entMan.SpawnEntity(bossId, testMap.GridCoords);
                // Ported TG fauna: SimpleHostile. Upstream lavaland fauna: Lavafauna.
                portedFauna = entMan.SpawnEntity("MobLavalandLobstrosity", testMap.GridCoords.Offset(new Vector2(0.7f, 0)));
                upstreamFauna = entMan.SpawnEntity("MobWatcherLavaland", testMap.GridCoords.Offset(new Vector2(-0.7f, 0)));
                player = entMan.SpawnEntity("MobHuman", testMap.GridCoords.Offset(new Vector2(1.4f, 0)));
            });

            await server.WaitRunTicks(1);

            await server.WaitAssertion(() =>
            {
                var faction = entMan.GetComponent<NpcFactionMemberComponent>(boss);
                Assert.That(faction.Factions, Does.Contain(SimpleHostile),
                    $"{bossId} must be in the ported fauna faction");
                Assert.That(faction.Factions, Does.Contain(Lavafauna),
                    $"{bossId} must be in the upstream fauna faction");
                Assert.That(faction.HostileFactions, Does.Contain(Nanotrasen),
                    $"{bossId} must stay hostile to NanoTrasen");

                Assert.That(factions.IsEntityFriendly(portedFauna, boss), Is.True,
                    $"{bossId}: ported fauna must not see the boss as an enemy");
                Assert.That(factions.IsEntityFriendly(upstreamFauna, boss), Is.True,
                    $"{bossId}: upstream fauna must not see the boss as an enemy");
                Assert.That(factions.IsEntityFriendly(boss, portedFauna), Is.True,
                    $"{bossId} must not see ported fauna as an enemy");
                Assert.That(factions.IsEntityFriendly(boss, upstreamFauna), Is.True,
                    $"{bossId} must not see upstream fauna as an enemy");

                // This is the exact query the HTN melee combat uses to pick a target.
                Assert.That(factions.GetNearbyHostiles(portedFauna, 3f), Does.Not.Contain(boss),
                    $"{bossId} must not be picked as a target by ported fauna");
                Assert.That(factions.GetNearbyHostiles(upstreamFauna, 3f), Does.Not.Contain(boss),
                    $"{bossId} must not be picked as a target by upstream fauna");

                // Bosses fight players through Aggressive, not through their faction, so this must stay.
                Assert.That(factions.IsEntityFriendly(boss, player), Is.False,
                    $"{bossId} must not be friendly to players");
                Assert.That(entMan.HasComponent<AggressiveComponent>(boss), Is.True,
                    $"{bossId} must keep the aggression component that drives attacks on players");
            });

            await server.WaitPost(() =>
            {
                entMan.DeleteEntity(boss);
                entMan.DeleteEntity(portedFauna);
                entMan.DeleteEntity(upstreamFauna);
                entMan.DeleteEntity(player);
            });
        }

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task EveryBossAndEliteJoinsBothMiningFactions()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var protoMan = server.ResolveDependency<IPrototypeManager>();

        foreach (var id in BossPrototypes.Concat(BossVariantPrototypes).Concat(ElitePrototypes))
        {
            var prototype = protoMan.Index<EntityPrototype>(id);
            var faction = prototype.Components.Values
                .Select(entry => entry.Component)
                .OfType<NpcFactionMemberComponent>()
                .FirstOrDefault();

            Assert.That(faction, Is.Not.Null, $"{id} must have a faction");
            Assert.That(faction!.Factions, Does.Contain(SimpleHostile),
                $"{id} must be in the ported fauna faction");
            Assert.That(faction.Factions, Does.Contain(Lavafauna),
                $"{id} must be in the upstream fauna faction");
        }

        await pair.CleanReturnAsync();
    }
}
