// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Collections.Generic;
using Content.Goobstation.Server.Possession;
using Content.Shared.Mind;
using Robust.Server.Containers;
using Robust.Shared.Containers;
using Robust.Shared.GameObjects;
using Robust.Shared.Log;
using Robust.Shared.Map;
using Serilog.Events;

namespace Content.IntegrationTests.Tests._Goobstation.Possession;

/// <summary>
/// Removing a <see cref="PossessedComponent"/> must not throw when the possessor's original body was
/// already deleted, which happens during the end-of-round entity flush: entities are deleted one by
/// one in no particular order, so the possessor's body can be gone before the possessed entity.
/// </summary>
[TestFixture]
[TestOf(typeof(PossessedComponent))]
public sealed class PossessionRemovalTest
{
    [Test]
    public async Task RemovalAfterPossessorBodyDeletedDoesNotThrow()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var entMan = server.ResolveDependency<IEntityManager>();
        var logMan = server.ResolveDependency<ILogManager>();
        var mind = server.System<SharedMindSystem>();
        var container = server.System<ContainerSystem>();

        var errors = new ErrorLogCapture();
        logMan.RootSawmill.AddHandler(errors);

        EntityUid target = default;
        EntityUid possessorBody = default;
        EntityUid possessorMind = default;

        try
        {
            await server.WaitPost(() =>
            {
                target = entMan.SpawnEntity(null, MapCoordinates.Nullspace);
                possessorBody = entMan.SpawnEntity(null, MapCoordinates.Nullspace);
                possessorMind = mind.CreateMind(null, "Test possessor").Owner;
            });

            await server.WaitAssertion(() =>
            {
                var possessed = entMan.AddComponent<PossessedComponent>(target);
                possessed.HideActions = false;
                possessed.PolymorphEntity = false;
                possessed.OriginalEntity = target;
                possessed.PossessorOriginalEntity = possessorBody;
                possessed.PossessorMindId = possessorMind;
                possessed.PossessedContainer = container.EnsureContainer<Container>(target, "PossessedContainer");

                // Recreate the end-of-round order: the possessor's body is already deleted when the
                // possessed component is removed. This used to throw in MindSystem.TransferTo.
                entMan.DeleteEntity(possessorBody);
                Assert.That(entMan.EntityExists(possessorBody), Is.False);

                entMan.RemoveComponent<PossessedComponent>(target);
            });
        }
        finally
        {
            logMan.RootSawmill.RemoveHandler(errors);
        }

        var logged = errors.Snapshot();
        Assert.That(logged, Is.Empty,
            "removing the possession logged errors:\n" + string.Join('\n', logged));

        await pair.CleanReturnAsync();
    }

    private sealed class ErrorLogCapture : ILogHandler
    {
        private readonly List<string> _messages = [];

        public void Log(string sawmillName, LogEvent message)
        {
            if (message.Level < LogEventLevel.Error)
                return;

            lock (_messages)
                _messages.Add(sawmillName + ": " + message.RenderMessage());
        }

        public string[] Snapshot()
        {
            lock (_messages)
                return _messages.ToArray();
        }
    }
}
