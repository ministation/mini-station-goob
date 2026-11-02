// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared._Lavaland.Weapons.Lava;
using Content.Shared._Shitmed.Body.Components;
using Content.Shared.Humanoid;
using Content.Shared.Humanoid.Prototypes;
using Robust.Shared.GameObjects;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.Tests._Lavaland.Mobs;

[TestFixture]
[TestOf(typeof(HumanoidAppearanceComponent))]
public sealed class MobAshwalkerTest
{
    [Test]
    public async Task AshwalkerHasOwnSpeciesAndBreathesLavaland()
    {
        await using var pair = await PoolManager.GetServerClient();

        var server = pair.Server;
        var testMap = await pair.CreateTestMap();
        var entMan = server.ResolveDependency<IEntityManager>();
        var protoMan = server.ResolveDependency<IPrototypeManager>();

        EntityUid ashwalker = default;
        await server.WaitPost(() =>
        {
            ashwalker = entMan.SpawnEntity("MobAshwalker", testMap.GridCoords);
        });

        await server.WaitRunTicks(5);

        await server.WaitAssertion(() =>
        {
            // Ashwalkers must not be a plain Unathi copy: they have their own species.
            Assert.That(protoMan.HasIndex<SpeciesPrototype>("Ashwalker"), Is.True,
                "Ashwalker species prototype must exist");
            Assert.That(entMan.TryGetComponent<HumanoidAppearanceComponent>(ashwalker, out var humanoid), Is.True);
            Assert.That(protoMan.Index(humanoid!.Species).ID, Is.EqualTo("Ashwalker"),
                "ashwalker must use the Ashwalker species, not Reptilian");

            // And they must survive Lavaland air without internals.
            Assert.That(entMan.HasComponent<BreathingImmunityComponent>(ashwalker), Is.True,
                "ashwalker must not need to breathe");
            Assert.That(entMan.HasComponent<LavaImmunityComponent>(ashwalker), Is.True,
                "ashwalker must be immune to lava");
        });

        await pair.CleanReturnAsync();
    }
}
