// SPDX-FileCopyrightText: 2026 Space Station 14 Contributors
//
// SPDX-License-Identifier: LicenseRef-MiniStation-1.0

// SPDX-FileCopyrightText: 2026 Mini Station
// SPDX-License-Identifier: LicenseRef-MiniStation-1.0

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
