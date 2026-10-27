// SPDX-FileCopyrightText: 2026 Space Station 14 Contributors
//
// SPDX-License-Identifier: AGPL-3.0-or-later

// SPDX-FileCopyrightText: 2026 Mini Station
// Мини-станция/Freaky-station, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/ministation/mini-station-goob/master/LICENSE.TXT

using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._Mini.Katorga.Components;

/// <summary>
///     Фракция, к которой приписан заключённый каторги. Синхронизируется клиенту:
///     по ней рисуется метка фракции рядом с заключённым, как на войне станций.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class KatorgaFactionMemberComponent : Component
{
    [DataField, AutoNetworkedField]
    public ProtoId<KatorgaFactionPrototype> Faction = default!;
}
