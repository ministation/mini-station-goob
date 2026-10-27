// SPDX-FileCopyrightText: 2026 Space Station 14 Contributors
//
// SPDX-License-Identifier: AGPL-3.0-or-later

// SPDX-FileCopyrightText: 2026 Mini Station
// Мини-станция/Freaky-station, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/ministation/mini-station-goob/master/LICENSE.TXT

using System.Linq;
using Content.Server._Mini.Katorga.Components;
using Content.Shared._Mini.Katorga;
using NUnit.Framework;
using Robust.Shared.EntitySerialization;
using Robust.Shared.EntitySerialization.Systems;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;

namespace Content.IntegrationTests.Tests._Mini;

/// <summary>
///     Карты каторги (оба лагеря) грузятся как гриды (без старта раунда) и содержат
///     машинерию каторги: фракционные точки спавна заключённых и приёмники руды.
///     Ловит переименования прототипов, после которых KatorgaSystem теряет спавны
///     или приёмники.
/// </summary>
[TestFixture]
public sealed class KatorgaMapTest
{
    [TestCase("/Maps/_Mini/katorga.yml", "KatorgaFactionMiners")]
    [TestCase("/Maps/_Mini/katorga_free.yml", "KatorgaFactionFreemen")]
    public async Task KatorgaMapLoadsAsGridWithMachinery(string mapPath, string factionId)
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        var entManager = server.ResolveDependency<IEntityManager>();
        var mapLoader = entManager.System<MapLoaderSystem>();
        var mapSystem = entManager.System<SharedMapSystem>();
        var protoManager = server.ResolveDependency<IPrototypeManager>();
        var faction = protoManager.Index<KatorgaFactionPrototype>(factionId);

        await server.WaitPost(() =>
        {
            mapSystem.CreateMap(out var mapId);
            Assert.That(
                mapLoader.TryLoadGrid(mapId, new ResPath(mapPath), out var grid, DeserializationOptions.Default),
                $"Карта каторги {mapPath} не загрузилась: битые прототипы или не grid-only файл");

            var gridUid = grid!.Value.Owner;
            var factionSpawns = entManager.EntityQuery<KatorgaSpawnPointComponent>()
                .Select(comp => comp.Owner)
                .Where(uid => entManager.GetComponent<TransformComponent>(uid).GridUid == gridUid &&
                              entManager.GetComponent<KatorgaSpawnPointComponent>(uid).Faction == faction.ID)
                .ToList();
            var processors = entManager.EntityQuery<KatorgaOreProcessorComponent>()
                .Select(comp => comp.Owner)
                .Where(uid => entManager.GetComponent<TransformComponent>(uid).GridUid == gridUid)
                .ToList();

            Assert.That(factionSpawns, Is.Not.Empty,
                $"На карте {mapPath} нет точек спавна фракции {factionId}");
            Assert.That(processors, Is.Not.Empty,
                $"На карте {mapPath} нет ни одного приёмника руды");

            mapSystem.DeleteMap(mapId);
        });
        await server.WaitRunTicks(1);

        await pair.CleanReturnAsync();
    }
}
