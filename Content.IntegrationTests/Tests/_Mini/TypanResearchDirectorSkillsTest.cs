// SPDX-FileCopyrightText: 2026 Mini Station
// Мини-станция/Freaky-station, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/ministation/mini-station-goob/master/LICENSE.TXT

using System.Collections.Generic;
using System.Threading.Tasks;
using Content.Server._CorvaxGoob.Skills;
using Content.Server.GameTicking;
using Content.Server.Mind;
using Content.Shared._CorvaxGoob.Skills;
using Content.Shared.CCVar;
using Content.Shared.GameTicking;
using Content.Shared.Preferences;
using Content.Shared.Roles;
using Content.Shared.Roles.Jobs;
using NUnit.Framework;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.Tests._Mini;

/// <summary>
///     Научный руководитель Тайпана, зашедший в раунд на карте НИКК Тайпан, получает навык
///     управления шаттлом и сохраняет все прежние навыки своей должности.
/// </summary>
[TestFixture]
public sealed class TypanResearchDirectorSkillsTest
{
    private static readonly ProtoId<JobPrototype> ResearchDirector = "TypanResearchDirector";
    private const string TypanMap = "Typan";

    [Test]
    public async Task ResearchDirectorCanPilotShuttles()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings
        {
            DummyTicker = false,
            Connected = true,
            InLobby = true,
            Dirty = true,
        });

        var server = pair.Server;
        server.CfgMan.SetCVar(CCVars.GameMap, TypanMap);
        var ticker = server.System<GameTicker>();

        await pair.SetJobPriorities((ResearchDirector, JobPriority.High));
        ticker.ToggleReadyAll(true);
        await server.WaitPost(() => ticker.StartRound());
        await pair.RunTicksSync(10);

        await server.WaitAssertion(() =>
        {
            Assert.That(ticker.RunLevel, Is.EqualTo(GameRunLevel.InRound));

            var mob = server.PlayerMan.SessionsDict[pair.Client.User!.Value].AttachedEntity;
            Assert.That(mob, Is.Not.Null, "игрок не появился на станции");

            var mind = server.System<MindSystem>().GetMind(mob!.Value);
            Assert.That(server.System<SharedJobSystem>().MindTryGetJobId(mind, out var job));
            Assert.That(job, Is.EqualTo(ResearchDirector));

            var skills = server.System<SkillsSystem>();
            Assert.That(skills.TryGetSkills(mob.Value, out var owned));
            Assert.That(owned, Is.EquivalentTo(new HashSet<Skills>
            {
                Skills.ShuttleControl,
                Skills.Shooting,
                Skills.Butchering,
                Skills.Surgery,
                Skills.MedicalEquipment,
                Skills.AdvancedBuilding,
            }));
            Assert.That(skills.HasSkill(mob.Value, Skills.ShuttleControl));
        });

        await server.WaitPost(() => ticker.RestartRound());
        await pair.CleanReturnAsync();
    }
}
