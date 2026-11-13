// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Linq;
using System.Numerics;
using Content.Server.Gravity;
using Content.Server.Polymorph.Components;
using Content.Shared.Actions;
using Content.Shared.Actions.Components;
using Content.Shared.Body.Organ;
using Content.Shared.Body.Systems;
using Content.Shared.Gravity;
using Content.Shared._Trauma.Genetics.Abilities;
using Content.Shared._Trauma.Genetics.Mutations;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Maths;

namespace Content.IntegrationTests.Tests._Trauma;

/// <summary>
/// Checks that the tongue spike mutation removes the tongue, throws a spike and gives the tongue back after landing.
/// </summary>
[TestFixture]
public sealed class TongueSpikeTest
{
    [Test]
    public async Task TestTongueSpikeReturnsTongue()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Connected = false });
        var server = pair.Server;
        var entMan = server.EntMan;
        var map = await pair.CreateTestMap();
        var body = server.System<SharedBodySystem>();

        EntityUid human = default;
        await server.WaitPost(() =>
        {
            // the test map is a single tile, extend it so the spike can land on the grid
            var mapSys = server.System<SharedMapSystem>();
            for (var x = 1; x <= 5; x++)
                mapSys.SetTile(map.Grid, new Vector2i(x, 0), map.Tile.Tile);

            var gravity = entMan.EnsureComponent<GravityComponent>(map.Grid);
            server.System<GravitySystem>().EnableGravity(map.Grid, gravity);

            human = entMan.SpawnAtPosition("MobHuman", map.GridCoords);
        });
        await pair.RunTicksSync(10);

        EntityUid actionEnt = default;
        EntityUid tongue = default;
        await server.WaitAssertion(() =>
        {
            var mutation = server.System<MutationSystem>();
            Assert.That(mutation.AddMutation(human, "MutationTongueSpike"), "Failed to add MutationTongueSpike");

            var query = entMan.AllEntityQueryEnumerator<ActionMutationComponent>();
            while (query.MoveNext(out var uid, out var comp))
            {
                if (comp.Action.Id != "ActionMutationTongueSpike")
                    continue;

                actionEnt = comp.ActionEntity ?? default;
                break;
            }

            Assert.That(actionEnt, Is.Not.EqualTo(default(EntityUid)), "Tongue spike action was not granted");
            Assert.That(entMan.HasComponent<ShootOrganActionComponent>(actionEnt), "Action has no ShootOrganActionComponent");
            // remember the tongue: it is the same entity that comes back after the polymorph
            tongue = body.GetBodyOrgans(human).FirstOrDefault(o => o.Component.SlotId == "tongue").Id;
            Assert.That(tongue, Is.Not.EqualTo(default(EntityUid)), "Human has no tongue before shooting");
        });

        EntityUid spike = default;
        await server.WaitAssertion(() =>
        {
            var actions = server.System<SharedActionsSystem>();
            var action = actions.GetAction(actionEnt);
            Assert.That(action, Is.Not.Null, "Tongue spike action component is missing");

            var target = server.Transform(human).Coordinates.Offset(new Vector2(2, 0));
            var ev = new ShootOrganActionEvent { Target = target };
            actions.PerformAction(new Entity<ActionsComponent?>(human, entMan.GetComponent<ActionsComponent>(human)),
                action!.Value, ev, predicted: false);
            Assert.That(ev.Handled, "ShootOrganActionEvent was not handled by the server");

            Assert.That(body.GetBodyOrgans(human).Any(o => o.Component.SlotId == "tongue"), Is.False,
                "Tongue is still in the body after shooting");

            var query = entMan.AllEntityQueryEnumerator<PolymorphedEntityComponent>();
            while (query.MoveNext(out var uid, out var poly))
            {
                if (poly.Parent == tongue)
                    spike = uid;
            }

            Assert.That(spike, Is.Not.EqualTo(default(EntityUid)), "No tongue spike projectile was created");
            Assert.That(entMan.GetComponent<MetaDataComponent>(spike).EntityPrototype?.ID, Is.EqualTo("TongueSpike"),
                "The projectile is not a TongueSpike");
        });

        // Let the spike fly, land and revert.
        await pair.RunTicksSync(180);

        await server.WaitAssertion(() =>
        {
            Assert.That(entMan.EntityExists(spike), Is.False, "Tongue spike was not reverted after landing");

            // the pair is pooled and reused, so unrelated organs from other tests can exist
            Assert.That(entMan.EntityExists(tongue), "Tongue was not returned after the spike landed");
            Assert.That(server.Transform(tongue).MapID, Is.EqualTo(map.MapId), "Returned tongue is not on the test map");
            Assert.That(body.GetBodyOrgans(human).Any(o => o.Id == tongue),
                "Tongue was not put back into its body");
        });

        await server.WaitPost(() => entMan.DeleteEntity(map.MapUid));
        await pair.CleanReturnAsync();
    }
}
