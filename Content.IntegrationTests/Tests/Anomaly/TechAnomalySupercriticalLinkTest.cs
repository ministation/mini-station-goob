// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Collections.Generic;
using System.Linq;
using Content.Server.Anomaly;
using Content.Server.Anomaly.Effects;
using Content.Shared.Anomaly.Components;
using Content.Shared.DeviceLinking;
using Robust.Shared.GameObjects;
using Robust.Shared.Log;
using Serilog.Events;

namespace Content.IntegrationTests.Tests.Anomaly;

/// <summary>
/// Reproduces the production line
/// "system.device_link: Can't resolve "Content.Shared.DeviceLinking.DeviceLinkSourceComponent" on entity аномалия (AnomalyTech)".
/// Going supercritical removes the anomaly's device link source, but the anomaly can still update in the same tick
/// (or keep living, for a non-deleting anomaly) and used to invoke its ports on the source-less entity.
/// </summary>
[TestFixture]
[TestOf(typeof(TechAnomalySystem))]
public sealed class TechAnomalySupercriticalLinkTest
{
    // The real AnomalyTech is queue-deleted right after the event, which hides the leftover update behind the
    // deletion order. Keeping the anomaly alive makes the same source-less state observable on later ticks.
    [TestPrototypes]
    private const string Prototypes = @"
- type: entity
  id: AnomalyTechKeepAlive
  parent: AnomalyTech
  components:
  - type: Anomaly
    deleteEntity: false
";

    [Test]
    public async Task SupercriticalAnomalyDoesNotResolveMissingLinkSource()
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
            var uid = EntityUid.Invalid;
            await server.WaitAssertion(() =>
            {
                uid = entMan.SpawnEntity("AnomalyTechKeepAlive", map.GridCoords);
            });

            await server.WaitAssertion(() =>
            {
                var anomalySystem = server.System<AnomalySystem>();
                var anomaly = entMan.GetComponent<AnomalyComponent>(uid);
                Assert.That(entMan.HasComponent<DeviceLinkSourceComponent>(uid), Is.True);

                anomalySystem.DoAnomalySupercriticalEvent(uid, anomaly);

                Assert.That(entMan.EntityExists(uid), "the non-deleting anomaly must survive its supercritical event");
                Assert.That(entMan.HasComponent<DeviceLinkSourceComponent>(uid), Is.False,
                    "the supercritical event must remove the anomaly's source component");

                errors.Clear();

                // Both port invocations of TechAnomalySystem used to resolve the removed component with logging.
                anomalySystem.DoAnomalyPulse(uid, anomaly);
            });

            // The tech anomaly timer port fires every 3 seconds.
            await pair.RunSeconds(4);

            await server.WaitAssertion(() =>
            {
                var resolveErrors = errors.Snapshot()
                    .Where(message => message.Contains("Can't resolve") &&
                                      message.Contains(nameof(DeviceLinkSourceComponent)))
                    .ToArray();
                Assert.That(resolveErrors, Is.Empty,
                    $"an anomaly without a link source must not resolve it: {string.Join('\n', resolveErrors)}");
            });
        }
        finally
        {
            logMan.RootSawmill.RemoveHandler(errors);
        }

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
                _messages.Add(message.RenderMessage());
        }

        public void Clear()
        {
            lock (_messages)
                _messages.Clear();
        }

        public string[] Snapshot()
        {
            lock (_messages)
                return _messages.ToArray();
        }
    }
}
