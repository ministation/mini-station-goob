// SPDX-FileCopyrightText: 2026 Egorik1
// Мини-станция, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/ministation/mini-station-goob/master/LICENSE.TXT

using Robust.Shared.GameStates;

namespace Content.Shared._Mini.TypanWar;

[RegisterComponent, NetworkedComponent]
public sealed partial class TypanWarMinimapComponent : Component
{
    public EntityUid? ActionEntity;
}
