// SPDX-FileCopyrightText: 2026 Space Station 14 Contributors
//
// SPDX-License-Identifier: AGPL-3.0-or-later

// SPDX-FileCopyrightText: 2026 Mini Station
// Мини-станция/Freaky-station, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/ministation/mini-station-goob/master/LICENSE.TXT

using System.Linq;
using Content.Server._Mini.Katorga.Components;
using NUnit.Framework;
using Robust.Shared.EntitySerialization;
using Robust.Shared.EntitySerialization.Systems;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Utility;

namespace Content.IntegrationTests.Tests._Mini;

/// <summary>
///     Карта каторги грузится как грид (без старта раунда) и содержит машинерию каторги:
///     точки спавна заключённых и приёмники руды. Ловит переименования прототипов,
///     после которых KatorgaSystem теряет спавны или приёмники.
/// </summary>
[TestFixture]
public sealed class KatorgaMapTest
{
    private const string MapPath = "/Maps/_Mini/katorga.yml";

    [Test]
    public async Task KatorgaMapLoadsAsGridWithMachinery()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        var entManager = server.ResolveDependency<IEntityManager>();
        var mapLoader = entManager.System<MapLoaderSystem>();
        var mapSystem = entManager.System<SharedMapSystem>();

        await server.WaitPost(() =>
        {
            mapSystem.CreateMap(out var mapId);
            Assert.That(
                mapLoader.TryLoadGrid(mapId, new ResPath(MapPath), out var grid, DeserializationOptions.Default),
                $"Карта каторги {MapPath} не загрузилась: битые прототипы или не grid-only файл");

            var gridUid = grid!.Value.Owner;
            var spawns = entManager.EntityQuery<KatorgaSpawnPointComponent>()
                .Select(comp => comp.Owner)
                .Where(uid => entManager.GetComponent<TransformComponent>(uid).GridUid == gridUid)
                .ToList();
            var processors = entManager.EntityQuery<KatorgaOreProcessorComponent>()
                .Select(comp => comp.Owner)
                .Where(uid => entManager.GetComponent<TransformComponent>(uid).GridUid == gridUid)
                .ToList();

            Assert.That(spawns, Is.Not.Empty, "На карте каторги нет ни одной точки спавна заключённых");
            Assert.That(processors, Is.Not.Empty, "На карте каторги нет ни одного приёмника руды");

            mapSystem.DeleteMap(mapId);
        });
        await server.WaitRunTicks(1);

        await pair.CleanReturnAsync();
    }
}
