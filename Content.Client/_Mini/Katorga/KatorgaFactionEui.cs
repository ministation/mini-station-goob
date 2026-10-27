// SPDX-FileCopyrightText: 2026 Space Station 14 Contributors
//
// SPDX-License-Identifier: AGPL-3.0-or-later

// SPDX-FileCopyrightText: 2026 Mini Station
// Мини-станция/Freaky-station, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/ministation/mini-station-goob/master/LICENSE.TXT

using Content.Client.Eui;
using Content.Shared._Mini.Katorga;
using Content.Shared.Eui;
using JetBrains.Annotations;

namespace Content.Client._Mini.Katorga;

/// <summary>
///     Клиент окна выбора фракции каторги. Имя класса должно совпадать с серверным:
///     EuiManager находит клиентский класс по имени из OpenType.
/// </summary>
[UsedImplicitly]
public sealed class KatorgaFactionEui : BaseEui
{
    private readonly KatorgaFactionWindow _window;

    public KatorgaFactionEui()
    {
        _window = new KatorgaFactionWindow();
        _window.OnSelect += factionId =>
            SendMessage(new KatorgaFactionSelectMessage { FactionId = factionId });
        _window.OnClose += () => SendMessage(new CloseEuiMessage());
    }

    public override void HandleState(EuiStateBase state)
    {
        if (state is KatorgaFactionEuiState factionState)
            _window.Update(factionState);
    }

    public override void Opened()
    {
        _window.OpenCentered();
    }

    public override void Closed()
    {
        _window.Close();
    }
}
