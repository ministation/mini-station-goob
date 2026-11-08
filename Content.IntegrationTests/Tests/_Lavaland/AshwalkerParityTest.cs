// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Linq;
using System.Numerics;
using Content.Server._EinsteinEngines.Language;
using Content.Server._Lavaland.Ashwalker;
using Content.Server.Ghost.Roles;
using Content.Server.Ghost.Roles.Components;
using Content.Shared._EinsteinEngines.Language.Components;
using Content.Shared._Lavaland.Ashwalker;
using Content.Shared.Mind;
using Content.Shared.Mind.Components;
using Content.Shared.Players;
using Robust.Server.Player;
using Robust.Shared.Console;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;

namespace Content.IntegrationTests.Tests._Lavaland;

/// <summary>
/// TG ash walker parity: an egg hatches a walker for a ckey only once until a sacrifice clears
/// the limit, and ash walkers lose the common language.
/// </summary>
[TestFixture]
public sealed class AshwalkerParityTest
{
    private const string TestMobProtoId = "AshwalkerParityTestMob";

    [TestPrototypes]
    private const string Prototypes = $"""
        - type: entity
          id: {TestMobProtoId}
          components:
          - type: MobState
        """;

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
    public async Task EggHatchesOneAshwalkerPerCkey()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings
        {
            Dirty = true,
            DummyTicker = false,
            Connected = true
        });
        var server = pair.Server;
        var client = pair.Client;
        var entMan = server.ResolveDependency<IEntityManager>();
        var playerMan = server.ResolveDependency<IPlayerManager>();
        var conHost = client.ResolveDependency<IConsoleHost>();
        var mindSystem = entMan.System<SharedMindSystem>();
        var ghostRoles = entMan.System<GhostRoleSystem>();
        var tribeSystem = entMan.System<AshwalkerTribeSystem>();
        var mapData = await pair.CreateTestMap();
        var session = playerMan.Sessions.Single();

        // Give the player a body and ghost out, like GhostRoleTests does.
        await server.WaitPost(() =>
        {
            var body = entMan.SpawnEntity(TestMobProtoId, mapData.GridCoords);
            mindSystem.TransferTo(session.ContentData()!.Mind!.Value, body, true);
        });
        await pair.RunTicksSync(5);
        conHost.ExecuteCommand("ghost");
        await pair.RunTicksSync(5);

        EntityUid nest = default;
        EntityUid egg = default;
        await server.WaitPost(() =>
        {
            nest = entMan.SpawnEntity("LavalandAshwalkerNest", mapData.GridCoords);
            tribeSystem.EnsureTribe(nest);
            egg = entMan.SpawnEntity("LavalandAshwalkerEgg", mapData.GridCoords.Offset(new Vector2(1f, 0f)));
        });
        await pair.RunTicksSync(3);

        var tribe = entMan.GetComponent<AshwalkerTribeComponent>(nest);

        // First hatch goes through the real ghost role path.
        await server.WaitPost(() => ghostRoles.Takeover(session, entMan.GetComponent<GhostRoleComponent>(egg).Identifier));
        await pair.RunTicksSync(5);

        await server.WaitAssertion(() =>
        {
            Assert.That(entMan.Deleted(egg), Is.True, "the egg must be consumed by the hatch");
            Assert.That(session.AttachedEntity, Is.Not.Null);
            var born = session.AttachedEntity!.Value;
            Assert.That(entMan.GetComponent<MetaDataComponent>(born).EntityPrototype?.ID, Is.EqualTo("MobAshwalker"),
                "the egg must hatch the ghost role ash walker body");
            Assert.That(entMan.HasComponent<AshwalkerTribeMemberComponent>(born), Is.True);
            Assert.That(entMan.HasComponent<MindContainerComponent>(born), Is.True);
            Assert.That(tribe.SpawnedCkeys, Does.Contain(session.UserId),
                "the hatched ckey must be recorded by the tribe");
        });

        // Ghost out and offer a second egg to the same ckey: it must be refused and left intact.
        conHost.ExecuteCommand("ghost");
        await pair.RunTicksSync(5);

        EntityUid secondEgg = default;
        await server.WaitPost(() =>
            secondEgg = entMan.SpawnEntity("LavalandAshwalkerEgg", mapData.GridCoords.Offset(new Vector2(1f, 0f))));
        await pair.RunTicksSync(3);

        await server.WaitPost(() => ghostRoles.Takeover(session, entMan.GetComponent<GhostRoleComponent>(secondEgg).Identifier));
        await pair.RunTicksSync(3);

        await server.WaitAssertion(() =>
        {
            Assert.That(entMan.Deleted(secondEgg), Is.False, "a ckey that already hatched must not consume the egg");
            Assert.That(tribe.SpawnedCkeys.Count, Is.EqualTo(1));
        });

        // Clearing the entry is what the 40% sacrifice roll does: the same ckey may hatch again.
        await server.WaitPost(() => tribe.SpawnedCkeys.Clear());
        await server.WaitPost(() => ghostRoles.Takeover(session, entMan.GetComponent<GhostRoleComponent>(secondEgg).Identifier));
        await pair.RunTicksSync(5);

        await server.WaitAssertion(() =>
        {
            Assert.That(entMan.Deleted(secondEgg), Is.True);
            Assert.That(session.AttachedEntity, Is.Not.Null);
            Assert.That(entMan.GetComponent<MetaDataComponent>(session.AttachedEntity!.Value).EntityPrototype?.ID,
                Is.EqualTo("MobAshwalker"));
            Assert.That(tribe.SpawnedCkeys, Does.Contain(session.UserId));
        });

        await pair.CleanReturnAsync();
    }
}
