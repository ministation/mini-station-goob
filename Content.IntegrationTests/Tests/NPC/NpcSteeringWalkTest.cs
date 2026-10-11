// SPDX-FileCopyrightText: 2026 Mini Station
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Numerics;
using System.Text;
using Content.IntegrationTests.Tests.Movement;
using Content.Server.NPC.Components;
using Content.Server.NPC.Systems;
using Robust.Shared.GameObjects;
using Robust.Shared.Physics.Components;
using Content.Shared.Movement.Components;
using Robust.Shared.Timing;

namespace Content.IntegrationTests.Tests.NPC;

/// <summary>
/// Mini: an NPC steered by NPCSteeringSystem must physically walk to its
/// target. Bots standing still were reported on prod.
/// </summary>
public sealed class NpcSteeringWalkTest : MovementTest
{
    [Test]
    public async Task SteererWalksToTarget()
    {
        var start = SEntMan.GetCoordinates(PlayerCoords).Offset(new Vector2(-2, 0));
        var target = SEntMan.GetCoordinates(PlayerCoords).Offset(new Vector2(2, 0));

        var npc = await SpawnEntity("MobHuman", start);

        var steering = Server.System<NPCSteeringSystem>();
        await Server.WaitPost(() =>
        {
            steering.Register(npc, target);
        });

        var log = new StringBuilder();
        for (var i = 0; i < 7; i++)
        {
            await RunTicks(60);
            await Server.WaitPost(() =>
            {
                var xform = SEntMan.GetComponent<TransformComponent>(npc);
                var steer = SEntMan.GetComponentOrNull<NPCSteeringComponent>(npc);
                var body = SEntMan.GetComponentOrNull<PhysicsComponent>(npc);
                var status = steer != null ? steer.Status.ToString() : "noComp";
                var vel = body != null ? body.LinearVelocity.ToString() : "-";
                var pathfind = steer != null ? steer.Pathfind.ToString() : "-";
                var pathLen = steer != null ? steer.CurrentPath.Count.ToString() : "-";
                var mover = SEntMan.GetComponentOrNull<InputMoverComponent>(npc);
                var lastTick = mover != null ? mover.LastInputTick.Value.ToString() : "-";
                
                log.AppendLine("t=" + ((i + 1) * 60) + " pos=" + xform.Coordinates + " status=" + status + " vel=" + vel
                    + " pathfinding=" + pathfind + " pathLen=" + pathLen + " lastInputTick=" + lastTick);
            });
        }

        await Server.WaitPost(() =>
        {
            var pos = SEntMan.GetComponent<TransformComponent>(npc).Coordinates.Position;
            Assert.That(pos.X, Is.GreaterThan(start.Position.X + 1f),
                "NPC did not walk to the target, pos=" + pos + System.Environment.NewLine + log);
        });

        await Pair.CleanReturnAsync();
    }
}
