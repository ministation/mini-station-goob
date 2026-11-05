// SPDX-License-Identifier: AGPL-3.0-or-later

#nullable enable annotations

using System.Linq;
using Content.Goobstation.Server.Achievements;
using Content.Goobstation.Server.Achievements.Conditions;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.Tests._Goobstation.Achievements;

/// <summary>
/// Guards the lavaland boss achievements: every boss that has a kill achievement must be killable
/// by <see cref="BossKillCondition"/> (Aggressive component), and the BossKiller meta achievement
/// must count exactly as many bosses as the category has.
/// </summary>
[TestFixture]
public sealed class LavalandBossAchievementTest
{
    private static readonly (string Boss, string Achievement)[] BossAchievements =
    {
        ("MobHierophant", "HierophantKiller"),
        ("LavalandBossColossus", "ColossusKiller"),
        ("LavalandBossAshDrake", "DrakeKiller"),
        ("LavalandBossBubblegum", "BubblegumKiller"),
        ("LavalandBossMegaLegion", "LegionKiller"),
        ("LavalandBossWendigo", "WendigoKiller"),
        ("LavalandBossDemonicFrostMiner", "FrostMinerKiller"),
    };

    [Test]
    public async Task EveryBossKillAchievementIsWired()
    {
        await using var pair = await PoolManager.GetServerClient();

        var server = pair.Server;
        var protoMan = server.ResolveDependency<IPrototypeManager>();

        await server.WaitAssertion(() =>
        {
            foreach (var (boss, achievementId) in BossAchievements)
            {
                Assert.That(protoMan.HasIndex<AchievementPrototype>(achievementId), Is.True,
                    achievementId + " achievement prototype is missing");

                var achievement = protoMan.Index<AchievementPrototype>(achievementId);
                Assert.That(achievement.Category.Id, Is.EqualTo("LavalandBosses"),
                    achievementId + " must stay in the LavalandBosses category");
                Assert.That(achievement.Icon, Is.Not.Null,
                    achievementId + " needs an icon");

                Assert.That(achievement.Condition, Is.InstanceOf<BossKillCondition>(),
                    achievementId + " must use BossKillCondition");
                var condition = (BossKillCondition) achievement.Condition;
                Assert.That(condition.Prototypes, Is.Not.Null, achievementId + " has no boss prototypes");
                var prototypes = condition.Prototypes!.Select(p => p.Id).ToArray();
                Assert.That(prototypes, Does.Contain(boss),
                    achievementId + " does not listen for " + boss);

                Assert.That(protoMan.HasIndex<EntityPrototype>(boss), Is.True, boss + " prototype is missing");
                var bossPrototype = protoMan.Index<EntityPrototype>(boss);
                Assert.That(bossPrototype.Components.ContainsKey("Aggressive"), Is.True,
                    boss + " needs AggressiveComponent or BossKillCondition never fires");
            }

            var bossAchievementCount = protoMan.EnumeratePrototypes<AchievementPrototype>()
                .Count(p => p.Category.Id == "LavalandBosses" && p.ID != "BossKiller");

            Assert.That(protoMan.HasIndex<AchievementPrototype>("BossKiller"), Is.True, "BossKiller is missing");
            var bossKiller = protoMan.Index<AchievementPrototype>("BossKiller");
            Assert.That(bossKiller.Condition, Is.InstanceOf<AchievementCountCondition>(),
                "BossKiller must use AchievementCountCondition");
            var countCondition = (AchievementCountCondition) bossKiller.Condition;
            Assert.That(countCondition.Count, Is.EqualTo(bossAchievementCount),
                "BossKiller must require all LavalandBosses achievements");
        });

        await pair.CleanReturnAsync();
    }
}
