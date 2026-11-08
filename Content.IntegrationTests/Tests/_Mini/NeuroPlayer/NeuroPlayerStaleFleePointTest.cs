// SPDX-License-Identifier: LicenseRef-MiniStation-1.0

using System.Numerics;
using Content.Server._Mini.NeuroPlayer;
using Content.Server.NPC.HTN;
using Content.Shared.Damage;
using Content.Shared.Damage.Components;
using Content.Shared.Damage.Systems;
using Content.Shared._Mini.MiniCCVars;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Timing;

namespace Content.IntegrationTests.Tests._Mini.NeuroPlayer;

/// <summary>
/// Reproduces the production line
/// "system.transform: Attempted to convert coordinates with invalid entity: EntId=...".
/// A fleeing bot kept a NeuroPoint on an entity that was deleted mid-round, and the reroute check
/// converted that stale point on the next hit.
/// </summary>
[TestFixture]
[TestOf(typeof(NeuroPlayerSystem))]
public sealed class NeuroPlayerStaleFleePointTest
{
    private const string BotProtoId = "NeuroPlayerStaleFleePointBot";

    [TestPrototypes]
    private const string Prototypes = $@"
- type: entity
  id: {BotProtoId}
  components:
  - type: Damageable
  - type: MobState
  - type: MobThresholds
    thresholds:
      0: Alive
";

    [Test]
    public async Task StaleFleePointDoesNotLogError()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings
        {
            Dirty = true,
        });
        var server = pair.Server;
        var entMan = server.EntMan;
        var cfg = server.CfgMan;
        var timing = server.ResolveDependency<IGameTiming>();
        var map = await pair.CreateTestMap();

        EntityUid bot = default;
        EntityUid staleEntity = default;

        await server.WaitPost(() =>
        {
            cfg.SetCVar(MiniCCVars.NeuroPlayerEnabled, true);

            bot = entMan.SpawnEntity(BotProtoId, map.GridCoords);
            // The brain components are server-only, so they cannot come from the shared test prototype.
            var comp = entMan.AddComponent<NeuroPlayerComponent>(bot);
            var htn = entMan.AddComponent<HTNComponent>(bot);
            var damageable = entMan.GetComponent<DamageableComponent>(bot);

            // The point the bot keeps from an earlier flee, on an entity that no longer exists.
            staleEntity = entMan.SpawnEntity(null, map.GridCoords);
            htn.Blackboard.SetValue("NeuroPoint", new EntityCoordinates(staleEntity, new Vector2(-6.70f, 16.71f)));
            entMan.DeleteEntity(staleEntity);

            comp.DangerUntil = timing.CurTime + TimeSpan.FromSeconds(20);
            comp.NextCry = timing.CurTime + TimeSpan.FromHours(1);

            var damage = new DamageSpecifier { DamageDict = { ["Blunt"] = 15 } };
            entMan.EventBus.RaiseLocalEvent(bot, new DamageChangedEvent(damageable, damage, true, null));
        });

        await server.WaitAssertion(() =>
        {
            var htn = entMan.GetComponent<HTNComponent>(bot);
            Assert.That(htn.Blackboard.TryGetValue<EntityCoordinates>("NeuroPoint", out var point, entMan), Is.True);

            Assert.That(point.EntityId, Is.Not.EqualTo(staleEntity),
                "the deleted flee point should be replaced by a resolvable one");
            Assert.That(point.IsValid(entMan), Is.True);
        });

        await pair.CleanReturnAsync();
    }
}
