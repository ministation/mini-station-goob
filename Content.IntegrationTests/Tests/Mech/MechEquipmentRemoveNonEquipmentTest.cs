// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Collections.Generic;
using System.Linq;
using Content.Server.Mech.Systems;
using Content.Shared.Mech;
using Content.Shared.Mech.Components;
using Content.Shared.Mech.Equipment.Components;
using Robust.Shared.Containers;
using Robust.Shared.GameObjects;
using Robust.Shared.Log;
using Serilog.Events;

namespace Content.IntegrationTests.Tests.Mech;

/// <summary>
/// The equipment container can hold an entity without a <see cref="MechEquipmentComponent"/>, for
/// example when an entity was stuck inside a mech. Removing it used to resolve the component with
/// logging, which wrote "Can't resolve MechEquipmentComponent" to the server log for every removal
/// request. That case is expected, so it must be handled silently.
/// </summary>
[TestFixture]
[TestOf(typeof(MechComponent))]
public sealed class MechEquipmentRemoveNonEquipmentTest
{
    private sealed class ErrorLogCapture : ILogHandler
    {
        public readonly List<string> Messages = [];

        public void Log(string sawmillName, LogEvent message)
        {
            if (message.Level < LogEventLevel.Error)
                return;

            lock (Messages)
                Messages.Add(message.RenderMessage());
        }

        public void Clear()
        {
            lock (Messages)
                Messages.Clear();
        }

        public string[] Snapshot()
        {
            lock (Messages)
                return Messages.ToArray();
        }
    }

    [Test]
    public async Task NonEquipmentInEquipmentContainerDoesNotLogError()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Dirty = true });
        var server = pair.Server;
        var entMan = server.ResolveDependency<IEntityManager>();
        var logMan = server.ResolveDependency<ILogManager>();
        var map = await pair.CreateTestMap();

        var errors = new ErrorLogCapture();
        logMan.RootSawmill.AddHandler(errors);

        try
        {
            await server.WaitPost(() =>
            {
                var mech = entMan.SpawnEntity("MechRipleyBattery", map.GridCoords);
                var human = entMan.SpawnEntity("MobHuman", map.GridCoords);
                var mechComponent = entMan.GetComponent<MechComponent>(mech);
                var containers = entMan.System<SharedContainerSystem>();

                Assert.That(entMan.HasComponent<MechEquipmentComponent>(human), Is.False);
                Assert.That(containers.Insert(human, mechComponent.EquipmentContainer), Is.True);

                errors.Clear();

                // The mech UI removal request from the production log names the non-equipment
                // entity that is contained in the equipment container.
                var remove = new MechEquipmentRemoveMessage(entMan.GetNetEntity(human));
                entMan.EventBus.RaiseLocalEvent(mech, remove, true);

                Assert.That(mechComponent.EquipmentContainer.ContainedEntities, Does.Contain(human),
                    "a non-forced removal of a non-equipment entity must be ignored");

                var resolveErrors = FindResolveErrors(errors);
                Assert.That(resolveErrors, Is.Empty,
                    $"a removal request for a non-equipment entity must not resolve it: {string.Join('\n', resolveErrors)}");

                // A broken mech is supposed to dump non-equipment entities out of its container.
                errors.Clear();
                server.System<MechSystem>().BreakMech(mech);

                Assert.That(mechComponent.EquipmentContainer.ContainedEntities, Does.Not.Contain(human),
                    "a broken mech must eject a stuck non-equipment entity");

                resolveErrors = FindResolveErrors(errors);
                Assert.That(resolveErrors, Is.Empty,
                    $"breaking a mech with a non-equipment entity inside must not resolve it: {string.Join('\n', resolveErrors)}");
            });
        }
        finally
        {
            logMan.RootSawmill.RemoveHandler(errors);
        }

        await pair.CleanReturnAsync();
    }

    private static string[] FindResolveErrors(ErrorLogCapture errors)
    {
        return errors.Snapshot()
            .Where(message => message.Contains("Can't resolve") && message.Contains(nameof(MechEquipmentComponent)))
            .ToArray();
    }
}
