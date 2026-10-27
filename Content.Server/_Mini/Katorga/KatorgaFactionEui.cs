// SPDX-FileCopyrightText: 2026 Space Station 14 Contributors
//
// SPDX-License-Identifier: AGPL-3.0-or-later

// SPDX-FileCopyrightText: 2026 Mini Station
// Мини-станция/Freaky-station, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/ministation/mini-station-goob/master/LICENSE.TXT

using Content.Server.EUI;
using Content.Shared._Mini.Katorga;
using Content.Shared.Eui;
using Robust.Shared.Player;
using Robust.Server.Player;

namespace Content.Server._Mini.Katorga;

/// <summary>
///     Серверная сторона окна выбора фракции каторги.
/// </summary>
public sealed class KatorgaFactionEui : BaseEui
{
    private readonly KatorgaFactionSystem _system;
    private readonly ICommonSession _session;

    public KatorgaFactionEui(KatorgaFactionSystem system, ICommonSession session)
    {
        _system = system;
        _session = session;
    }

    public override EuiStateBase GetNewState()
    {
        return _system.GetEuiState(_session);
    }

    public override void HandleMessage(EuiMessageBase msg)
    {
        base.HandleMessage(msg);

        if (msg is KatorgaFactionSelectMessage select)
            _system.TrySelectFaction(_session, select.FactionId);
    }

    public override void Closed()
    {
        base.Closed();
        _system.OnEuiClosed(_session, this);
    }
}
