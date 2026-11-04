// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Numerics;
using Content.Server.CriminalRecords.Systems;
using Content.Server.StationRecords.Systems;
using Content.Shared.CartridgeLoader;
using Content.Shared.CriminalRecords;
using Content.Shared.Security;
using Content.Shared.Station.Components;
using Content.Shared.StationRecords;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;

namespace Content.IntegrationTests.Tests.CriminalRecords;

/// <summary>
/// A wanted list cartridge reads the criminal records of the station its PDA is currently on.
/// Stations without a StationRecordsComponent (the ash walker tribe station on Lavaland) used to
/// log "Can't resolve StationRecordsComponent" every time a criminal status changed anywhere.
/// </summary>
[TestFixture]
[TestOf(typeof(CriminalRecordsSystem))]
public sealed class WantedListCartridgeTest
{
    [Test]
    public async Task RecordslessStationDoesNotLogError()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var entMan = server.EntMan;
        var map = await pair.CreateTestMap();
        var grid = map.Grid.Owner;

        await server.WaitPost(() =>
        {
            var records = server.System<StationRecordsSystem>();
            var criminalRecords = server.System<CriminalRecordsSystem>();

            // A regular station with a crew member and their criminal record.
            var station = entMan.SpawnEntity(null, MapCoordinates.Nullspace);
            entMan.EnsureComponent<StationRecordsComponent>(station);
            var key = records.AddRecordEntry(station, new GeneralStationRecord { Name = "Test Crewmember" });
            records.AddRecordEntry(key, new CriminalRecord());

            // A records-less station, like LavalandTribeStation, owning the grid.
            var recordslessStation = entMan.SpawnEntity(null, MapCoordinates.Nullspace);
            entMan.EnsureComponent<StationMemberComponent>(grid).Station = recordslessStation;

            // A wanted list cartridge in a PDA that is on that grid.
            var pda = entMan.SpawnEntity("PassengerPDA", new EntityCoordinates(grid, Vector2.Zero));
            var cartridge = entMan.SpawnEntity("WantedListCartridge", new EntityCoordinates(grid, Vector2.Zero));
            entMan.GetComponent<CartridgeComponent>(cartridge).LoaderUid = pda;

            // A status change on the regular station reaches every wanted list cartridge.
            Assert.That(criminalRecords.TryChangeStatus(key, SecurityStatus.Wanted, "Test reason", "Test officer"), Is.True);
        });

        await pair.CleanReturnAsync();
    }
}
