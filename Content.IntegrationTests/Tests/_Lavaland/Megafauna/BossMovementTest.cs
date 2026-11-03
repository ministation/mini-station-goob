// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Content.Server.Gravity;
using Content.Server.NPC.Components;
using Content.Shared._Lavaland.Aggression;
using Content.Shared._Lavaland.Megafauna.Components;
using Content.Shared._Mini.MiniCCVars;
using Content.Shared.Damage;
using Content.Shared.Damage.Components;
using Content.Shared.Damage.Systems;
using Content.Shared.Gravity;
using Content.Shared.Movement.Components;
using Content.Shared.NPC;
using Content.Shared.NPC.Components;
using Content.Shared.NPC.Systems;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Physics.Components;

namespace Content.IntegrationTests.Tests._Lavaland.Megafauna;

/// <summary>
/// Guards that lavaland bosses actually walk at whoever they are fighting.
/// They keep the megafauna special AI, but without walking they only blink around or cast and stand still.
/// </summary>
[TestFixture]
public sealed class BossMovementTest
{
    private static readonly string[] Bosses =
    {
        "LavalandBossAshDrake",
        "LavalandBossColossus",
        "LavalandBossMegaLegion",
    };

    [Test]
    public async Task BossesChaseTheAggressor()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var testMap = await pair.CreateTestMap();
        var entMan = server.ResolveDependency<IEntityManager>();
        var mapSys = server.System<SharedMapSystem>();

        // Mini puts every NPC to sleep when no player is within 20 tiles. Integration tests run
        // without a client, so the bosses would be slept on the first tick and never plan a move.
        server.CfgMan.SetCVar(MiniCCVars.NPCDisableWithoutPlayers, false);

        // CreateTestMap makes a single tile grid. Mobs need a floor to path on and gravity,
        // otherwise the boss has nowhere to walk and the movement test is meaningless.
        await server.WaitPost(() =>
        {
            for (var x = -2; x <= 10; x++)
            {
                for (var y = -4; y <= 4; y++)
                    mapSys.SetTile(testMap.Grid, new EntityCoordinates(testMap.Grid, x, y), testMap.Tile.Tile);
            }

            var gravity = entMan.EnsureComponent<GravityComponent>(testMap.Grid.Owner);
            entMan.System<GravitySystem>().EnableGravity(testMap.Grid.Owner, gravity);
        });

        var failures = new List<string>();

        foreach (var proto in Bosses)
        {
            EntityUid boss = default;
            EntityUid target = default;
            var startDistance = 0f;

            await server.WaitPost(() =>
            {
                boss = entMan.SpawnEntity(proto, testMap.GridCoords);
                target = entMan.SpawnEntity("MobHuman", testMap.GridCoords.Offset(new Vector2(4f, 0f)));

                // Aggression is what the chase system follows; the faction add keeps the NPC
                // bookkeeping consistent with a real player target.
                entMan.EnsureComponent<NpcFactionMemberComponent>(target);
                entMan.System<NpcFactionSystem>().AddFaction(target, "NanoTrasen");

                // Keep the target alive for the whole window. A dead target drops the boss
                // back to idle wandering, which makes the final distance meaningless.
                entMan.EnsureComponent<GodmodeComponent>(target);
            });

            await server.WaitRunTicks(10);

            await server.WaitPost(() =>
            {
                var aggressive = entMan.GetComponent<AggressiveComponent>(boss);
                entMan.System<AggressorsSystem>().AddAggressor((boss, aggressive), target);

                // Measure after the warmup so idle wandering cannot make the threshold unfair.
                startDistance = Distance(entMan, boss, target);
            });

            await server.WaitRunTicks(120);

            await server.WaitPost(() =>
            {
                var distance = Distance(entMan, boss, target);
                if (distance >= startDistance - 1f)
                {
                    var xform = entMan.GetComponent<TransformComponent>(boss);
                    var mover = entMan.GetComponent<InputMoverComponent>(boss);
                    var body = entMan.GetComponent<PhysicsComponent>(boss);
                    var aiActive = entMan.GetComponent<MegafaunaAiComponent>(boss).Active;

                    failures.Add(proto + " did not walk at the target: started at " + startDistance.ToString("0.0")
                        + ", ended at " + distance.ToString("0.0")
                        + " (anchored=" + xform.Anchored
                        + ", canMove=" + mover.CanMove
                        + ", bodyStatus=" + body.BodyStatus
                        + ", megafaunaAi=" + aiActive
                        + ", velocity=" + body.LinearVelocity + ")");
                }
            });

            await server.WaitPost(() =>
            {
                entMan.DeleteEntity(target);
                entMan.DeleteEntity(boss);
            });

            await server.WaitRunTicks(5);
        }

        Assert.That(failures, Is.Empty, string.Join("; ", failures));
        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task LegionShardsKeepTheBossHud()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var testMap = await pair.CreateTestMap();
        var entMan = server.ResolveDependency<IEntityManager>();

        EntityUid boss = default;
        await server.WaitPost(() =>
        {
            boss = entMan.SpawnEntity("LavalandBossMegaLegion", testMap.GridCoords);
        });

        await server.WaitRunTicks(5);

        await server.WaitPost(() =>
        {
            entMan.System<DamageableSystem>().TryChangeDamage(
                boss,
                new DamageSpecifier { DamageDict = { { "Blunt", 2000f } } },
                true);
        });

        await server.WaitRunTicks(10);

        await server.WaitAssertion(() =>
        {
            Assert.That(entMan.Deleted(boss), Is.True, "the legion boss should die and delete itself");

            var shards = 0;
            var wrongStage = new List<string>();
            var query = entMan.EntityQueryEnumerator<MegafaunaStageComponent>();
            while (query.MoveNext(out var uid, out var stage))
            {
                if (entMan.Deleted(uid))
                    continue;

                shards++;
                if (stage.Stage != 1 || stage.MaxStage != 1)
                {
                    var protoId = entMan.GetComponent<MetaDataComponent>(uid).EntityPrototype?.ID;
                    wrongStage.Add(protoId + " is at stage " + stage.Stage + " of " + stage.MaxStage);
                }
            }

            Assert.That(shards, Is.GreaterThanOrEqualTo(3), "the legion boss did not split into shards");
            Assert.That(wrongStage, Is.Empty, "split shards should keep the HUD without a stage counter: " + string.Join("; ", wrongStage));
        });

        await pair.CleanReturnAsync();
    }

    private static float Distance(IEntityManager entMan, EntityUid a, EntityUid b)
    {
        var aPos = entMan.GetComponent<TransformComponent>(a).Coordinates.Position;
        var bPos = entMan.GetComponent<TransformComponent>(b).Coordinates.Position;
        return (aPos - bPos).Length();
    }
}
