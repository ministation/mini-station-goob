// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Linq;
using System.Numerics;
using Content.Shared.Actions;
using Content.Shared.Hands.EntitySystems;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;

namespace Content.IntegrationTests.Tests._Lavaland.Megafauna;

/// <summary>
/// Guards that the hierophant club has a working shape attack: the holder gets the attack action
/// and performing it spawns hierophant blast tiles that dig through rock.
/// </summary>
[TestFixture]
public sealed class HierophantClubTest
{
    [Test]
    public async Task ClubGrantsAndFiresShapeAttack()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var testMap = await pair.CreateTestMap();
        var entMan = server.ResolveDependency<IEntityManager>();
        var centre = testMap.GridCoords.Offset(new Vector2(0.5f, 0.5f));
        var mapSys = server.System<SharedMapSystem>();

        // CreateTestMap makes a single tile grid, the blast needs a floor to drill through.
        await server.WaitPost(() =>
        {
            for (var x = -2; x <= 4; x++)
            {
                for (var y = -2; y <= 4; y++)
                    mapSys.SetTile(testMap.Grid, new EntityCoordinates(testMap.Grid, x, y), testMap.Tile.Tile);
            }
        });
        var actions = entMan.System<SharedActionsSystem>();
        var hands = entMan.System<SharedHandsSystem>();

        EntityUid human = default;
        EntityUid club = default;

        await server.WaitPost(() =>
        {
            human = entMan.SpawnEntity("MobHuman", centre);
            club = entMan.SpawnEntity("LavalandHierophantClub", centre);
        });

        await server.WaitRunTicks(3);

        await server.WaitAssertion(() =>
        {
            var grant = entMan.GetComponent<ActionGrantComponent>(club);
            Assert.That(grant.Actions.Any(a => a.Id == "ActionHierophantClubBox"), Is.True, "club should carry its shape attack action");
            Assert.That(grant.Actions.Any(a => a.Id == "ActionHierophantClubCrosses"), Is.True, "club should carry its second shape attack action");
        });

        await server.WaitPost(() =>
        {
            Assert.That(hands.TryPickupAnyHand(human, club), Is.True, "test setup failed to put the club in a hand");
        });

        await server.WaitRunTicks(3);

        EntityUid rock = default;

        await server.WaitAssertion(() =>
        {
            Assert.That(actions.TryGetActionById(human, "ActionHierophantClubBox", out _), Is.True,
                "the club attack action should be granted to the holder");
        });

        await server.WaitPost(() =>
        {
            rock = entMan.SpawnEntity("WallRock", centre.Offset(new Vector2(2, 0)));
        });

        await server.WaitPost(() =>
        {
            Assert.That(actions.TryGetActionById(human, "ActionHierophantClubBox", out var action), Is.True);

            var target = centre.Offset(new Vector2(2, 0));
            var ev = new RequestPerformActionEvent(entMan.GetNetEntity(action!.Value.Owner), entMan.GetNetCoordinates(target));
            Assert.That(actions.TryPerformAction(human, ev), Is.True, "the club attack action should be performable");
        });

        await server.WaitRunTicks(3);

        await server.WaitAssertion(() =>
        {
            Assert.That(CountPrototype(entMan, "LavalandHierophantClubBlast"), Is.GreaterThan(0),
                "the club attack should spawn hierophant blast tiles");
            Assert.That(entMan.Deleted(rock), Is.True, "the club blast should dig through the rock it lands on");
        });

        await pair.CleanReturnAsync();
    }

    private static int CountPrototype(IEntityManager entMan, string protoId)
    {
        var count = 0;
        var query = entMan.AllEntityQueryEnumerator<MetaDataComponent>();
        while (query.MoveNext(out _, out var meta))
        {
            if (meta.EntityPrototype?.ID == protoId)
                count++;
        }

        return count;
    }
}