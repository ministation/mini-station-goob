// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared._CorvaxGoob.Skills;
using Content.Shared.DoAfter;
using Content.Shared.Mobs.Components;
using Robust.Shared.GameObjects;
using SkillTypes = Content.Shared._CorvaxGoob.Skills.Skills;

namespace Content.IntegrationTests.Tests._CorvaxGoob.Skills;

/// <summary>
/// A mob without a <see cref="DoAfterComponent"/> (e.g. a guardian) cannot run a training do-after,
/// so the training system must refuse to start a session with it as the teacher instead of logging
/// "Attempting to start a doAfter with invalid user".
/// </summary>
[TestFixture]
public sealed class SkillTrainingDoAfterTest
{
    private const string NoDoAfterTeacherProtoId = "SkillTrainingNoDoAfterTeacherTest";
    private const string TeacherProtoId = "SkillTrainingTeacherTest";
    private const string StudentProtoId = "SkillTrainingStudentTest";

    [TestPrototypes]
    private const string Prototypes = $"""
        - type: entity
          id: {NoDoAfterTeacherProtoId}
          components:
          - type: MobState
          - type: MobThresholds
            thresholds:
              0: Alive
          - type: EntitySkills
            skills: [Surgery]

        - type: entity
          id: {TeacherProtoId}
          components:
          - type: MobState
          - type: MobThresholds
            thresholds:
              0: Alive
          - type: DoAfter
          - type: EntitySkills
            skills: [Surgery]

        - type: entity
          id: {StudentProtoId}
          components:
          - type: MobState
          - type: MobThresholds
            thresholds:
              0: Alive
        """;

    [Test]
    public async Task TeacherWithDoAfterStartsTraining()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings
        {
            Dirty = true,
        });
        var server = pair.Server;
        var entMan = server.ResolveDependency<IEntityManager>();
        var mapData = await pair.CreateTestMap();

        await server.WaitPost(() =>
        {
            var teacher = entMan.SpawnEntity(TeacherProtoId, mapData.GridCoords);
            var student = entMan.SpawnEntity(StudentProtoId, mapData.GridCoords);

            StartTrainingFromUi(entMan, teacher, student);

            Assert.That(entMan.TryGetComponent<SkillTrainingTeacherComponent>(teacher, out var teacherComp), Is.True,
                "the training session should start for a teacher that can run a do-after");
            Assert.That(teacherComp!.ActiveStudent, Is.EqualTo(entMan.GetNetEntity(student)));
            Assert.That(entMan.HasComponent<SkillTrainingStudentComponent>(student), Is.True);
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task TeacherWithoutDoAfterCannotStartTraining()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings
        {
            Dirty = true,
        });
        var server = pair.Server;
        var entMan = server.ResolveDependency<IEntityManager>();
        var mapData = await pair.CreateTestMap();

        await server.WaitPost(() =>
        {
            var teacher = entMan.SpawnEntity(NoDoAfterTeacherProtoId, mapData.GridCoords);
            var student = entMan.SpawnEntity(StudentProtoId, mapData.GridCoords);

            Assert.That(entMan.HasComponent<DoAfterComponent>(teacher), Is.False,
                "the test teacher must not have a DoAfterComponent");
            Assert.That(entMan.TryGetComponent<MobStateComponent>(teacher, out _), Is.True);

            StartTrainingFromUi(entMan, teacher, student);

            Assert.That(entMan.HasComponent<SkillTrainingTeacherComponent>(teacher), Is.False,
                "no training session may start when the teacher cannot run a do-after");
            Assert.That(entMan.HasComponent<SkillTrainingStudentComponent>(student), Is.False);
        });

        await pair.CleanReturnAsync();
    }

    private static void StartTrainingFromUi(IEntityManager entMan, EntityUid teacher, EntityUid student)
    {
        var session = entMan.EnsureComponent<SkillTrainingUiComponent>(student);
        session.Teacher = teacher;

        entMan.EventBus.RaiseLocalEvent(student, new SkillTrainingSelectSkillMessage(SkillTypes.Surgery)
        {
            Actor = teacher,
        });
    }
}
