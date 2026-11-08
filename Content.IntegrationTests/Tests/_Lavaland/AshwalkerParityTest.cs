// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Linq;
using System.Numerics;
using Content.Server._EinsteinEngines.Language;
using Content.Server._Lavaland.Ashwalker;
using Content.Server.Ghost.Roles.Components;
using Content.Shared._EinsteinEngines.Language.Components;
using Content.Shared._Lavaland.Ashwalker;
using Robust.Server.Player;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;

namespace Content.IntegrationTests.Tests._Lavaland;

/// <summary>
/// TG ash walker parity: a player may hatch from the tribe's eggs only once (a sacrifice can
/// clear that), and ash walkers lose the common language.
/// </summary>
[TestFixture]
public sealed class AshwalkerParityTest
{
    [Test]
    public async Task AshwalkerSpeaksOnlyDraconic()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var entMan = server.ResolveDependency<IEntityManager>();
        var mapData = await pair.CreateTestMap();

        EntityUid mob = default;
        await server.WaitPost(() => mob = entMan.SpawnEntity("MobAshwalkerPlayable", mapData.GridCoords));
        await server.WaitRunTicks(3);

        await server.WaitAssertion(() =>
        {
            // TG remove_language(common): only Draconic is left, both spoken and understood.
            var knowledge = entMan.GetComponent<LanguageKnowledgeComponent>(mob);
            Assert.That(knowledge.SpokenLanguages.Select(l => l.Id), Is.EquivalentTo(new[] { "Draconic" }));
            Assert.That(knowledge.UnderstoodLanguages.Select(l => l.Id), Is.EquivalentTo(new[] { "Draconic" }));

            var speaker = entMan.GetComponent<LanguageSpeakerComponent>(mob);
            Assert.That(speaker.SpokenLanguages.Select(l => l.Id), Is.EquivalentTo(new[] { "Draconic" }),
                "ash walkers must not speak the common language");
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task EggHatchIsLimitedToOnePerCkey()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings
        {
            Connected = true,
            DummyTicker = false,
            Dirty = true
        });
        var server = pair.Server;
        var entMan = server.ResolveDependency<IEntityManager>();
        var playerMan = server.ResolveDependency<IPlayerManager>();
        var tribeSystem = server.System<AshwalkerTribeSystem>();
        var mapData = await pair.CreateTestMap();
        var session = playerMan.Sessions.Single();

        EntityUid nest = default;
        EntityUid egg = default;
        await server.WaitPost(() =>
        {
            nest = entMan.SpawnEntity("LavalandAshwalkerNest", mapData.GridCoords);
            tribeSystem.EnsureTribe(nest).SpawnedCkeys.Add(session.UserId);
            egg = entMan.SpawnEntity("LavalandAshwalkerEgg", mapData.GridCoords.Offset(new Vector2(1f, 0f)));
        });
        await server.WaitRunTicks(2);

        await server.WaitAssertion(() =>
        {
            Assert.That(entMan.GetComponent<AshwalkerEggComponent>(egg).Nest, Is.EqualTo(nest),
                "the egg must join the closest nest");
        });

        // The ckey already hatched once: the request is refused and the egg stays for others.
        await server.WaitPost(() =>
        {
            var ev = new TakeGhostRoleEvent(session);
            entMan.EventBus.RaiseLocalEvent(egg, ref ev);
            Assert.That(ev.TookRole, Is.False, "a ckey that already hatched must not take another egg");
            Assert.That(entMan.Deleted(egg), Is.False);
        });

        // Clearing the entry is what the 40% sacrifice roll does; now the same player may hatch.
        await server.WaitPost(() => tribeSystem.EnsureTribe(nest).SpawnedCkeys.Clear());
        await server.WaitPost(() =>
        {
            var ev = new TakeGhostRoleEvent(session);
            entMan.EventBus.RaiseLocalEvent(egg, ref ev);
            Assert.That(ev.TookRole, Is.True, "a cleared ckey should be able to hatch");
        });

        await server.WaitRunTicks(3);

        await server.WaitAssertion(() =>
        {
            Assert.That(entMan.Deleted(egg), Is.True);
            Assert.That(session.AttachedEntity, Is.Not.Null);
            Assert.That(entMan.HasComponent<AshwalkerTribeMemberComponent>(session.AttachedEntity!.Value), Is.True,
                "the player must end up in an ash walker body");
            Assert.That(tribeSystem.EnsureTribe(nest).SpawnedCkeys, Does.Contain(session.UserId));
        });

        await pair.CleanReturnAsync();
    }
}
