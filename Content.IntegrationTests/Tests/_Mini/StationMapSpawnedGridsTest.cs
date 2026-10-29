// SPDX-FileCopyrightText: 2026 Space Station 14 Contributors
//
// SPDX-License-Identifier: AGPL-3.0-or-later

// SPDX-FileCopyrightText: 2026 Mini Station
// Мини-станция/Freaky-station, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/ministation/mini-station-goob/master/LICENSE.TXT

using System.Collections.Generic;
using System.IO;
using Content.Shared.Maps;
using NUnit.Framework;
using Robust.Shared.ContentPack;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.Tests._Mini;

/// <summary>
///     Карта станции не должна содержать гриды, которые станция спавнит сама (карго-шаттл,
///     шаттл прибытия, торговая станция, шахтёрский шаттл). Если карту сохранили из живого
///     раунда, эти гриды записываются в файл карты, и в раунде появляется второй шаттл.
/// </summary>
[TestFixture]
public sealed class StationMapSpawnedGridsTest
{
    /// <summary>
    ///     Компоненты, которые вешает только код спавна шаттлов. В файле карты их быть не должно.
    /// </summary>
    private static readonly string[] RuntimeGridMarkers =
    {
        "- type: CargoShuttle",
        "- type: PreventPilot",
        "- type: TradeStation",
        "- type: SalvageShuttle",
    };

    [Test]
    public async Task StationMapsHaveNoRuntimeSpawnedShuttles()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        var protoManager = server.ResolveDependency<IPrototypeManager>();
        var resMan = server.ResolveDependency<IResourceManager>();

        var failed = new List<string>();

        await server.WaitPost(() =>
        {
            foreach (var map in protoManager.EnumeratePrototypes<GameMapPrototype>())
            {
                if (!resMan.TryContentFileRead(map.MapPath.ToRootedPath(), out var file))
                {
                    failed.Add($"{map.ID}: не удалось прочитать {map.MapPath}");
                    continue;
                }

                using var reader = new StreamReader(file);
                var text = reader.ReadToEnd();

                foreach (var marker in RuntimeGridMarkers)
                {
                    if (text.Contains(marker))
                        failed.Add($"{map.ID} ({map.MapPath}): в карте лежит грид от кода ({marker}), в раунде будет дубль");
                }
            }
        });

        Assert.That(failed, Is.Empty, string.Join("\n", failed));
    }
}
