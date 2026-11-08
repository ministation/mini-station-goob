// SPDX-FileCopyrightText: 2026 Egorik1
// SPDX-License-Identifier: LicenseRef-MiniStation-1.0

using Robust.Shared.GameStates;

namespace Content.Shared._Mini.TypanWar;

[RegisterComponent, NetworkedComponent]
public sealed partial class TypanWarMinimapComponent : Component
{
    public EntityUid? ActionEntity;
}
