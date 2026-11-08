// SPDX-FileCopyrightText: 2026 Egorik1
// SPDX-License-Identifier: LicenseRef-MiniStation-1.0

using Robust.Shared.GameStates;

namespace Content.Shared._Mini.TypanWar;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class TypanWarFriendlyFireComponent : Component
{
    [DataField, AutoNetworkedField]
    public bool Enabled = true;

    [DataField, AutoNetworkedField]
    public EntityUid? ActionEntity;
}
