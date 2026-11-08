// SPDX-FileCopyrightText: 2026 Egorik1
// SPDX-License-Identifier: LicenseRef-MiniStation-1.0

using Robust.Shared.GameStates;

namespace Content.Shared._Mini.TypanWar;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class TypanWarFactionComponent : Component
{
    [DataField, AutoNetworkedField]
    public TypanWarSide Side;
}
