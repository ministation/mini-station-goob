// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Server.Mind;
using Content.Server.Roles;
using Content.Server.WhiteDream.BloodCult.Gamerule;
using Content.Server.WhiteDream.BloodCult.Objectives;
using Content.Shared.Objectives.Systems;
using Robust.Shared.GameObjects;

namespace Content.IntegrationTests.Tests.WhiteDream.BloodCult;

/// <summary>
/// The blood cult sacrifice objective is stored as a body, not a mind. Naming it used to pass the body uid to the
/// job API, which logged "system.role: Can't resolve MindComponent on entity ...!" for every cult round.
/// </summary>
[TestFixture]
[TestOf(typeof(KillTargetCultSystem))]
public sealed class KillTargetCultObjectiveTest
{
    [Test]
    public async Task SacrificeTargetBodyDoesNotResolveMindComponentOnItself()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var entMan = server.EntMan;
        var map = await pair.CreateTestMap();

        await server.WaitPost(() =>
        {
            var minds = server.System<MindSystem>();
            var roles = server.System<RoleSystem>();
            var objectives = server.System<SharedObjectivesSystem>();

            // The sacrifice target is a player body; its mind is a separate entity.
            var target = entMan.SpawnEntity("MobReptilian", map.GridCoords);
            var targetMind = minds.CreateMind(null, "Sacrifice Target");
            minds.TransferTo(targetMind, target);

            var rule = entMan.SpawnEntity(null, map.GridCoords);
            entMan.AddComponent<BloodCultRuleComponent>(rule).OfferingTarget = target;

            // The objective can only be assigned to a mind with the blood cult role.
            var cultist = entMan.SpawnEntity("MobReptilian", map.GridCoords);
            var cultistMind = minds.CreateMind(null, "Cultist");
            minds.TransferTo(cultistMind, cultist);
            roles.MindAddRole(cultistMind.Owner, "MindRoleBloodCultist", cultistMind.Comp, true);

            // Creating the objective names it after the target, previously by reading the job off the target body.
            Assert.That(objectives.TryCreateObjective(cultistMind, "KillTargetCultObjective", out _), Is.True);
        });

        await pair.CleanReturnAsync();
    }
}
