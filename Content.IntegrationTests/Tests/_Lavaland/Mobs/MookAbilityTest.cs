// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Numerics;
using Content.Server._Lavaland.Ruins;
using Content.Shared._Lavaland.Mobs;
using Content.Shared.Damage;
using Content.Shared.Damage.Systems;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;

namespace Content.IntegrationTests.Tests._Lavaland.Mobs;

/// <summary>
/// TG mook abilities: a provoked mook winds up for a leap at its attacker,
/// and a village mook that strays too far jumps back home.
/// </summary>
[TestFixture]
public sealed class MookAbilityTest
{
    [Test]
    public async Task ProvokedMookStartsLeapWindup()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var entMan = server.ResolveDependency<IEntityManager>();
        var mapSys = server.System<SharedMapSystem>();
        var damageable = server.System<DamageableSystem>();
        var appearance = server.System<SharedAppearanceSystem>();

        EntityUid mook = default;
        EntityUid human = default;

        await server.WaitPost(() =>
        {
            mapSys.CreateMap(out var map);
            var coords = new MapCoordinates(0, 0, map);
            mook = entMan.SpawnEntity("MobLavalandMookWorker", coords);
            human = entMan.SpawnEntity("MobHuman", coords.Offset(new Vector2(3, 0)));
        });

        await server.WaitRunTicks(2);

        // An unprovoked mook never leaps.
        await server.WaitRunTicks(60);
        await server.WaitPost(() =>
        {
            Assert.That(
                appearance.TryGetData(mook, MookVisuals.Leaping, out bool leaping) && leaping,
                Is.False, "an unprovoked mook must not leap");
        });

        // Hit the mook: retaliation + the TG leap wind-up.
        await server.WaitPost(() =>
        {
            var hit = new DamageSpecifier();
            hit.DamageDict["Blunt"] = 5;
            damageable.TryChangeDamage(mook, hit, origin: human);
        });

        await server.WaitRunTicks(30);

        await server.WaitPost(() =>
        {
            Assert.That(
                appearance.TryGetData(mook, MookVisuals.Leaping, out bool leaping) && leaping,
                Is.True, "a provoked mook should wind up its leap");
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task StrayedVillageMookJumpsHome()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var entMan = server.ResolveDependency<IEntityManager>();
        var mapSys = server.System<SharedMapSystem>();
        var xformSys = server.System<SharedTransformSystem>();

        EntityUid mook = default;
        MapCoordinates village = default;

        await server.WaitPost(() =>
        {
            mapSys.CreateMap(out var map);
            village = new MapCoordinates(0, 0, map);

            // A bare village marker: the component without the village builder.
            var marker = entMan.SpawnEntity(null, village);
            entMan.AddComponent<MookVillageSpawnerComponent>(marker);

            mook = entMan.SpawnEntity("MobLavalandMookWorker", new MapCoordinates(30, 0, map));
        });

        await server.WaitRunTicks(240);

        var distance = 0f;
        await server.WaitPost(() =>
        {
            var mookPos = xformSys.GetMapCoordinates(mook);
            distance = (mookPos.Position - village.Position).Length();
        });

        Assert.That(distance, Is.LessThan(10f), "a mook far from its village should jump back home");

        await pair.CleanReturnAsync();
    }
}
