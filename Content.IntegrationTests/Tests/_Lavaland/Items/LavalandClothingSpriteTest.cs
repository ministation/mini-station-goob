// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared.Clothing.Components;
using Robust.Client.GameObjects;
using Robust.Shared.GameObjects;

namespace Content.IntegrationTests.Tests._Lavaland.Items;

/// <summary>
/// The TG lavaland clothing sprites keep their TG state names (e.g. "hostile_env-equipped-OUTERCLOTHING"),
/// so the prototypes must point at them with equippedState. Otherwise the worn sprite silently disappears.
/// </summary>
[TestFixture]
public sealed class LavalandClothingSpriteTest
{
    private static readonly string[] Hardsuits =
    {
        "ClothingOuterHardsuitHECK",
        "ClothingHeadHelmetHECK",
    };

    [Test]
    public async Task WornSpritesUseExistingStates()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Connected = true });
        var client = pair.Client;
        var entMan = client.ResolveDependency<IEntityManager>();

        await client.WaitAssertion(() =>
        {
            foreach (var id in Hardsuits)
            {
                var uid = entMan.Spawn(id);

                var clothing = entMan.GetComponent<ClothingComponent>(uid);
                Assert.That(clothing.EquippedState, Is.Not.Null.And.Not.Empty,
                    $"{id} must set equippedState to its TG-prefixed worn sprite");

                var sprite = entMan.GetComponent<SpriteComponent>(uid);
                var rsi = sprite.BaseRSI;
                Assert.That(rsi, Is.Not.Null, $"{id} has no base sprite RSI");
                Assert.That(rsi!.TryGetState(clothing.EquippedState!, out _), Is.True,
                    $"{id} equippedState {clothing.EquippedState} is missing from {rsi.Path}");

                entMan.DeleteEntity(uid);
            }
        });

        await pair.CleanReturnAsync();
    }
}
