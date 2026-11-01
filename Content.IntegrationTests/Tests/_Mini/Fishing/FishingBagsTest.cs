// SPDX-License-Identifier: AGPL-3.0-or-later
// Ported from tgstation/tgstation (code/modules/fishing, AGPL-3.0-or-later).

using System.Linq;
using Content.IntegrationTests.Tests.Interaction;
using Content.Shared._Mini.Fishing.Components;
using Content.Shared.Storage;
using Content.Shared.Tag;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.Tests._Mini.Fishing;

/// <summary>
/// Fishing bags only take fishing gear and make the minigame easier while held.
/// </summary>
[TestFixture]
public sealed class FishingBagsTest : InteractionTest
{
    private HeldFishingDifficultyComponent Held(string prototype)
    {
        var ent = ProtoMan.Index<EntityPrototype>(prototype);
        Assert.That(ent.TryGetComponent<HeldFishingDifficultyComponent>(out var held, SEntMan.ComponentFactory),
            Is.True, $"{prototype} has no held fishing difficulty component.");
        return held!;
    }

    [Test]
    public void FishingGearMakesFishingEasier()
    {
        Assert.Multiple(() =>
        {
            Assert.That(Held("FishingBag").Difficulty, Is.EqualTo(-2f));
            Assert.That(Held("FishingBagCarpskin").Difficulty, Is.EqualTo(-4f));
            Assert.That(Held("FishCatalog").Difficulty, Is.EqualTo(-7f));
            Assert.That(Held("FishAnalyzer").Difficulty, Is.EqualTo(-3f));
        });
    }

    [Test]
    public void BagsOnlyAcceptFishingGear()
    {
        string[] expected = ["Fish", "FishingLine", "FishingHook", "FishingBait", "FishingRod"];

        foreach (var id in new[] { "FishingBag", "FishingBagCarpskin" })
        {
            var ent = ProtoMan.Index<EntityPrototype>(id);
            Assert.That(ent.TryGetComponent<StorageComponent>(out var storage, SEntMan.ComponentFactory),
                Is.True, $"{id} has no storage.");
            Assert.That(storage!.Whitelist?.Tags, Is.Not.Null, $"{id} has no storage whitelist.");
            Assert.That(storage.Whitelist!.Tags!.Select(tag => tag.Id), Is.SupersetOf(expected),
                $"{id} must accept fish and fishing gear.");
        }
    }

    [Test]
    public async Task FishAndRodsCarryTheirTags()
    {
        var tags = SEntMan.System<TagSystem>();
        var fish = SEntMan.GetEntity(await Spawn("FishPerch"));
        var rod = SEntMan.GetEntity(await Spawn("FishingRod"));

        Assert.That(tags.HasTag(fish, "Fish"), Is.True);
        Assert.That(tags.HasTag(rod, "FishingRod"), Is.True);
    }
}
