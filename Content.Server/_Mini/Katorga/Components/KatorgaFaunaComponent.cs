// SPDX-FileCopyrightText: 2026 Space Station 14 Contributors
//
// SPDX-License-Identifier: AGPL-3.0-or-later

// SPDX-FileCopyrightText: 2026 Mini Station
// Мини-станция/Freaky-station, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/ministation/mini-station-goob/master/LICENSE.TXT

using Content.Shared._Mini.Katorga;
using Robust.Shared.Prototypes;

namespace Content.Server._Mini.Katorga.Components;

/// <summary>
///     Моб фауны каторги: спавнится системой популяции вокруг лагерей,
///     убийство сокращает срок заключённому на RewardMinutes минут.
/// </summary>
[RegisterComponent]
public sealed partial class KatorgaFaunaComponent : Component
{
    /// <summary>Прототип моба: нужен для подсчёта популяции по видам.</summary>
    [DataField]
    public EntProtoId Prototype = default!;

    [DataField]
    public int RewardMinutes = 1;
}

/// <summary>
///     Популяция фауны на карте каторги: хранит настройки спавнера и список баз,
///     вокруг которых живут мобы. Вешается KatorgaSystem при загрузке карты.
/// </summary>
[RegisterComponent]
public sealed partial class KatorgaFaunaPopulationComponent : Component
{
    [DataField]
    public ProtoId<KatorgaFaunaSpawnerPrototype> Spawner = "KatorgaFauna";

    /// <summary>Сколько мобов осталось засеять при старте раунда.</summary>
    public int InitialRemaining;

    /// <summary>Базы каторги (гриды), вокруг которых спавнится фауна.</summary>
    public List<EntityUid> Bases = new();

    /// <summary>Время следующего захода спавна.</summary>
    public TimeSpan NextSpawnTime;
}
