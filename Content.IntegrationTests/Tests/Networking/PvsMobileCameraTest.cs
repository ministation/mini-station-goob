// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Collections.Generic;
using System.Numerics;
using Content.Goobstation.Common.SurveillanceCamera;
using Content.Server.SurveillanceCamera;
using Content.Shared.DeviceNetwork;
using Content.Shared.DeviceNetwork.Events;
using NUnit.Framework;
using Robust.Shared.GameObjects;
using Robust.Shared.Log;
using Robust.Shared.Map;
using Serilog.Events;

namespace Content.IntegrationTests.Tests.Networking;

/// <summary>
/// Opening a camera monitor must not add a PVS override for a mobile camera that was already
/// deleted. Its stale NetEntity resolves to EntityUid.Invalid, and system.pvs then logs
/// "Attempted to add non-existent entity 0 to PVS override for session ..." on every tick.
/// </summary>
[TestFixture]
public sealed class PvsMobileCameraTest
{
    private sealed class ErrorLogCapture : ILogHandler
    {
        private readonly List<string> _messages = new();

        public void Log(string sawmillName, LogEvent message)
        {
            if (message.Level < LogEventLevel.Error)
                return;

            lock (_messages)
                _messages.Add(message.RenderMessage());
        }

        public string[] Snapshot()
        {
            lock (_messages)
                return _messages.ToArray();
        }
    }

    [Test]
    public async Task DeletedMobileCameraIsNotAddedToPvsOverride()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings
        {
            Connected = true,
            EnableNetPvs = true,
            Destructive = true,
        });

        var server = pair.Server;
        var entMan = server.EntMan;
        var logMan = server.ResolveDependency<ILogManager>();
        var map = await pair.CreateTestMap();

        var errorLogs = new ErrorLogCapture();
        logMan.GetSawmill("system.pvs").AddHandler(errorLogs);
        var hasMobileCamera = false;

        try
        {
            await server.WaitPost(() =>
            {
                var monitor = entMan.SpawnAtPosition("ComputerSurveillanceCameraMonitor", map.GridCoords);
                var player = entMan.SpawnAtPosition("MobHuman", map.GridCoords);
                server.PlayerMan.SetAttachedEntity(pair.Player, player, true);

                var camera = entMan.SpawnAtPosition("Crowbar", map.GridCoords);
                var netCamera = entMan.GetNetEntity(camera);

                // Mimics the payload a mobile camera broadcasts, but the camera gets deleted
                // before anyone opens the monitor, as happens when a borg dies or disconnects.
                var payload = new NetworkPayload
                {
                    { DeviceNetworkConstants.Command, SurveillanceCameraSystem.CameraDataMessage },
                    { SurveillanceCameraSystem.CameraNameData, "Deleted camera" },
                    { SurveillanceCameraSystem.CameraSubnetData, "TestSubnet" },
                    { SurveillanceCameraSystem.CameraAddressData, "TestAddress" },
                    { SurveillanceCameraSystem.CameraNetEntity, (netCamera, new NetCoordinates(netCamera, Vector2.Zero)) },
                    { SurveillanceCameraSystem.CameraMobile, true },
                };

                entMan.EventBus.RaiseLocalEvent(monitor,
                    new DeviceNetworkPacketEvent(0, null, 0, "TestSender", camera, payload));
                entMan.DeleteEntity(camera);

                hasMobileCamera = entMan.HasComponent<HasMobileCamerasSurveillanceCameraMonitorComponent>(monitor);
                entMan.System<SurveillanceCameraMonitorSystem>().AfterOpenUserInterface(monitor, player);
            });

            Assert.That(hasMobileCamera, Is.True, "the monitor must know the deleted mobile camera");

            await pair.RunTicksSync(10);

            Assert.That(errorLogs.Snapshot(), Is.Empty,
                "a deleted mobile camera must not be added to a session's PVS overrides");

            await pair.CleanReturnAsync();
        }
        finally
        {
            logMan.GetSawmill("system.pvs").RemoveHandler(errorLogs);
        }
    }
}
