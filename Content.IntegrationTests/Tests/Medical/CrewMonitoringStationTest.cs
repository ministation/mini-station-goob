// SPDX-License-Identifier: MIT

using System.Collections.Generic;
using System.Numerics;
using Content.Server.DeviceNetwork.Components;
using Content.Server.DeviceNetwork.Systems;
using Content.Server.Medical.CrewMonitoring;
using Content.Shared.DeviceNetwork;
using Content.Shared.DeviceNetwork.Components;
using Content.Shared.Medical.SuitSensor;
using Content.Shared.Station.Components;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;

namespace Content.IntegrationTests.Tests.Medical;

/// <summary>
/// Ensures that crew monitoring receivers (the station AI and the crew monitoring console) only accept
/// broadcasts from servers of their own station. Maps without a mapped crew monitoring server used to see
/// CentComm suit sensors because those receivers accepted every crew monitoring broadcast on the device net.
/// </summary>
[TestFixture]
[TestOf(typeof(CrewMonitoringConsoleComponent))]
[TestOf(typeof(StationLimitedNetworkSystem))]
public sealed class CrewMonitoringStationTest
{
    [TestPrototypes]
    private const string Prototypes = @"
- type: entity
  name: CrewMonitoringServerDummy
  id: CrewMonitoringServerDummy
  components:
  - type: DeviceNetwork
    deviceNetId: Wireless
    transmitFrequencyId: CrewMonitor
  - type: WirelessNetworkConnection
    range: 500
  - type: StationLimitedNetwork
";

    private static NetworkPayload MakePayload(string sensorName)
    {
        var sensor = new SuitSensorStatus(NetEntity.Invalid, NetEntity.Invalid, sensorName, "Central Command Official", "JobIconCentComm", new List<string>());
        return new NetworkPayload
        {
            [DeviceNetworkConstants.Command] = DeviceNetworkConstants.CmdUpdatedState,
            [SuitSensorConstants.NET_STATUS_COLLECTION] = new Dictionary<string, SuitSensorStatus>
            {
                [sensorName] = sensor,
            },
        };
    }

    private static void Broadcast(DeviceNetworkSystem deviceNet, EntityUid sender, NetworkPayload payload, IEntityManager entMan)
    {
        Assert.That(deviceNet.QueuePacket(sender, null, payload, device: entMan.GetComponent<DeviceNetworkComponent>(sender)), Is.True);
    }

    [Test]
    public async Task ReceiversIgnoreForeignStationBroadcasts()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var entMan = server.EntMan;
        var mapSys = server.System<SharedMapSystem>();
        var deviceNet = server.System<DeviceNetworkSystem>();

        var testMap = await pair.CreateTestMap();

        EntityUid stationA = default;
        EntityUid stationB = default;
        EntityUid receiverConsole = default;
        EntityUid receiverAi = default;
        EntityUid serverB = default;
        EntityUid serverA = default;
        EntityUid gridB = default;

        await server.WaitPost(() =>
        {
            var gridA = testMap.Grid.Owner;

            // Two stations on the same map. Station A is the station without a mapped crew monitoring server,
            // station B plays the part of CentComm which does have a server.
            stationA = entMan.SpawnEntity(null, MapCoordinates.Nullspace);
            stationB = entMan.SpawnEntity(null, MapCoordinates.Nullspace);
            gridB = mapSys.CreateGridEntity(testMap.MapId).Owner;

            entMan.EnsureComponent<StationMemberComponent>(gridA).Station = stationA;
            entMan.EnsureComponent<StationMemberComponent>(gridB).Station = stationB;

            // Crew monitoring receivers of station A.
            receiverConsole = entMan.SpawnEntity("ComputerCrewMonitoring", new EntityCoordinates(gridA, Vector2.Zero));
            receiverAi = entMan.SpawnEntity("AiHeld", new EntityCoordinates(gridA, new Vector2(1, 0)));

            // A crew monitoring server of the other station, right next to the receivers.
            serverB = entMan.SpawnEntity("CrewMonitoringServerDummy", new EntityCoordinates(gridB, Vector2.Zero));
            deviceNet.ConnectDevice(serverB);
        });

        // The synthetic stations are not created by the game map loader, so pin the station ids
        // explicitly; this is the same state the loader gives these devices in a real round.
        await server.WaitAssertion(() =>
        {
            Assert.That(entMan.GetComponent<DeviceNetworkComponent>(serverB).Address, Is.Not.Empty);
            entMan.GetComponent<StationLimitedNetworkComponent>(receiverConsole).StationId = stationA;
            entMan.GetComponent<StationLimitedNetworkComponent>(receiverAi).StationId = stationA;
            entMan.GetComponent<StationLimitedNetworkComponent>(serverB).StationId = stationB;
            Assert.That(entMan.GetComponent<CrewMonitoringConsoleComponent>(receiverConsole).ConnectedSensors, Is.Empty);
            Assert.That(entMan.GetComponent<CrewMonitoringConsoleComponent>(receiverAi).ConnectedSensors, Is.Empty);
        });

        // The other station broadcasts its suit sensors.
        await server.WaitAssertion(() => Broadcast(deviceNet, serverB, MakePayload("CentComm Sensor"), entMan));
        await server.WaitRunTicks(2);
        await server.WaitIdleAsync();

        // The receivers of station A must not expose foreign suit sensors.
        await server.WaitAssertion(() =>
        {
            Assert.That(entMan.GetComponent<CrewMonitoringConsoleComponent>(receiverConsole).ConnectedSensors, Is.Empty);
            Assert.That(entMan.GetComponent<CrewMonitoringConsoleComponent>(receiverAi).ConnectedSensors, Is.Empty);
        });

        // Positive control: a server of the same station still reaches the receivers.
        await server.WaitPost(() =>
        {
            serverA = entMan.SpawnEntity("CrewMonitoringServerDummy", new EntityCoordinates(testMap.Grid.Owner, new Vector2(2, 0)));
            entMan.GetComponent<StationLimitedNetworkComponent>(serverA).StationId = stationA;
            deviceNet.ConnectDevice(serverA);
        });

        await server.WaitAssertion(() => Broadcast(deviceNet, serverA, MakePayload("Station Sensor"), entMan));
        await server.WaitRunTicks(2);
        await server.WaitIdleAsync();

        await server.WaitAssertion(() =>
        {
            var consoleSensors = entMan.GetComponent<CrewMonitoringConsoleComponent>(receiverConsole).ConnectedSensors;
            Assert.That(consoleSensors, Has.Count.EqualTo(1));
            Assert.That(consoleSensors.ContainsKey("Station Sensor"), Is.True);

            var aiSensors = entMan.GetComponent<CrewMonitoringConsoleComponent>(receiverAi).ConnectedSensors;
            Assert.That(aiSensors, Has.Count.EqualTo(1));
            Assert.That(aiSensors.ContainsKey("Station Sensor"), Is.True);
        });

        await pair.CleanReturnAsync();
    }
}
