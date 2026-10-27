// SPDX-FileCopyrightText: 2026 Space Station 14 Contributors
//
// SPDX-License-Identifier: AGPL-3.0-or-later

// SPDX-FileCopyrightText: 2026 Mini Station
// Мини-станция/Freaky-station, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/ministation/mini-station-goob/master/LICENSE.TXT

using Robust.Shared.Prototypes;

namespace Content.Shared._Mini.Katorga;

/// <summary>
///     Таблица фауны каторги: какие мобы и в каком количестве живут вокруг лагерей.
///     Убийство моба сокращает срок заключённому на RewardMinutes минут.
/// </summary>
[Prototype]
public sealed partial class KatorgaFaunaSpawnerPrototype : IPrototype
{
    [IdDataField]
    public string ID { get; private set; } = default!;

    [DataField(required: true)]
    public List<KatorgaFaunaSpawnEntry> Spawns = new();

    /// <summary>Сколько мобов засеять сразу после загрузки карты.</summary>
    [DataField]
    public int InitialCount = 14;

    /// <summary>Размер начальной партии за один заход спавна.</summary>
    [DataField]
    public int InitialBatchSize = 4;

    /// <summary>Целевая популяция: выше неё новые мобы не спавнятся.</summary>
    [DataField]
    public int SoftCap = 80;

    /// <summary>Жёсткий предел: мобы не спавнятся вообще.</summary>
    [DataField]
    public int HardCap = 110;

    /// <summary>Интервал пополнения популяции.</summary>
    [DataField]
    public TimeSpan SpawnInterval = TimeSpan.FromSeconds(75);

    [DataField]
    public int SpawnBatchMin = 2;

    [DataField]
    public int SpawnBatchMax = 4;

    /// <summary>Минимальная дистанция спавна от игрока.</summary>
    [DataField]
    public float MinPlayerDistance = 18f;

    /// <summary>Минимальная дистанция спавна от центра базы.</summary>
    [DataField]
    public float MinBaseDistance = 20f;

    /// <summary>Максимальная дистанция спавна от центра базы.</summary>
    [DataField]
    public float MaxBaseDistance = 100f;
}

[DataDefinition]
public sealed partial class KatorgaFaunaSpawnEntry
{
    [DataField(required: true)]
    public EntProtoId Prototype = default!;

    [DataField]
    public int Weight = 1;

    /// <summary>Максимум живых мобов этого вида одновременно.</summary>
    [DataField]
    public int MaxCount = 20;

    /// <summary>Сколько минут срока даёт убийство моба.</summary>
    [DataField]
    public int RewardMinutes = 1;
}
