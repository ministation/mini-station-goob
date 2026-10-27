// SPDX-FileCopyrightText: 2026 Space Station 14 Contributors
//
// SPDX-License-Identifier: AGPL-3.0-or-later

// SPDX-FileCopyrightText: 2026 Mini Station
// Мини-станция/Freaky-station, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/ministation/mini-station-goob/master/LICENSE.TXT

using Content.Shared.Eui;
using Robust.Shared.Serialization;

namespace Content.Shared._Mini.Katorga;

/// <summary>
///     EUI-окно выбора фракции каторги: открывается в лобби временно забаненному,
///     пока он не выбрал фракцию (или пока не истёк таймер автоназначения).
/// </summary>
[Serializable, NetSerializable]
public sealed class KatorgaFactionEuiState : EuiStateBase
{
    public List<KatorgaFactionOption> Options = new();

    /// <summary>Сколько секунд осталось до автоназначения фракции.</summary>
    public int SecondsRemaining;
}

[Serializable, NetSerializable]
public sealed record KatorgaFactionOption(string Id, string Name, string Feature, Color Color);

[Serializable, NetSerializable]
public sealed class KatorgaFactionSelectMessage : EuiMessageBase
{
    public string FactionId = default!;
}
