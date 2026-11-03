// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Linq;
using Content.Goobstation.Server.Achievements;
using Content.Goobstation.Server.Achievements.Conditions;
using NUnit.Framework;
using Robust.Shared.Prototypes;
using System.Threading.Tasks;

namespace Content.IntegrationTests.Tests._Lavaland.Achievements;

/// <summary>
/// Guards the invariant behind "BossKiller": it unlocks only when every per-boss
/// achievement is unlocked, and those achievements all live in one category.
/// Moving a boss achievement out of the category would make BossKiller unreachable.
/// </summary>
[TestFixture]
public sealed class BossKillerAchievementTest
{
    [Test]
    public async Task BossKillerCountsEveryBossAchievement()
    {
        await using var pair = await PoolManager.GetServerClient();
        var protoMan = pair.Server.ResolveDependency<IPrototypeManager>();

        await pair.Server.WaitAssertion(() =>
        {
            var bossAchievements = protoMan.EnumeratePrototypes<AchievementPrototype>()
                .Where(achievement => achievement.ID != "BossKiller" && achievement.Category.Id == "LavalandBosses")
                .ToList();

            Assert.That(bossAchievements, Has.Count.EqualTo(5),
                "each megafauna must keep its own achievement in the LavalandBosses category");

            var killer = protoMan.Index<AchievementPrototype>("BossKiller");
            Assert.That(killer.Condition, Is.InstanceOf<AchievementCountCondition>(),
                "BossKiller must count unlocked achievements, not raw kill events");

            var condition = (AchievementCountCondition) killer.Condition;
            Assert.Multiple(() =>
            {
                Assert.That(condition.Category?.Id, Is.EqualTo("LavalandBosses"),
                    "BossKiller must count the boss achievements of its own category");
                Assert.That(condition.Count, Is.EqualTo(bossAchievements.Count),
                    "BossKiller must require every boss achievement");
            });
        });

        await pair.CleanReturnAsync();
    }
}
