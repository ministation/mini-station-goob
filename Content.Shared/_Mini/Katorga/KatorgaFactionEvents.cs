// SPDX-FileCopyrightText: 2026 Space Station 14 Contributors
//
// SPDX-License-Identifier: AGPL-3.0-or-later

// SPDX-FileCopyrightText: 2026 Mini Station
// Мини-станция/Freaky-station, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/ministation/mini-station-goob/master/LICENSE.TXT

using Robust.Shared.Serialization;

namespace Content.Shared._Mini.Katorga;

/// <summary>Вариант фракции для окна позднего подключения: строки уже локализованы сервером.</summary>
[Serializable, NetSerializable]
public sealed class KatorgaFactionChoice
{
    public string Id = "";
    public string Name = "";
    public string Feature = "";
}

/// <summary>
///     Сервер сообщает клиенту варианты фракций, остаток времени на выбор и уже выбранную фракцию.
///     Нужен окну позднего подключения: выбор фракции каторги идёт там, а не отдельным окном в лобби.
/// </summary>
[Serializable, NetSerializable]
public sealed class KatorgaFactionStateEvent : EntityEventArgs
{
    public List<KatorgaFactionChoice> Options = new();
    public int SecondsRemaining;
    public string? Selected;
}

/// <summary>Окно позднего подключения открылось: клиент просит актуальное состояние выбора.</summary>
[Serializable, NetSerializable]
public sealed class KatorgaFactionStateRequestEvent : EntityEventArgs;

/// <summary>Клиент выбрал фракцию в окне позднего подключения.</summary>
[Serializable, NetSerializable]
public sealed class KatorgaFactionSelectEvent : EntityEventArgs
{
    public string FactionId = "";

    public KatorgaFactionSelectEvent()
    {
    }

    public KatorgaFactionSelectEvent(string factionId)
    {
        FactionId = factionId;
    }
}
