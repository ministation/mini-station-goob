// SPDX-FileCopyrightText: 2026 Space Station 14 Contributors
//
// SPDX-License-Identifier: AGPL-3.0-or-later

// SPDX-FileCopyrightText: 2026 Mini Station
// Мини-станция/Freaky-station, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/ministation/mini-station-goob/master/LICENSE.TXT

using Content.Shared._Mini.Katorga;
using Robust.Shared.Prototypes;

namespace Content.Server._Mini.Katorga.Components;

/// <summary>
///     Фракция, к которой приписан заключённый каторги.
/// </summary>
[RegisterComponent]
public sealed partial class KatorgaFactionMemberComponent : Component
{
    [DataField]
    public ProtoId<KatorgaFactionPrototype> Faction = default!;
}
