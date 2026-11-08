// SPDX-License-Identifier: LicenseRef-MiniStation-1.0

using Content.Shared.Actions;
using Robust.Shared.GameStates;

namespace Content.Shared._Lavaland.Weapons.Voice;

/// <summary>
/// Item that grants the Voice of God action.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class VocalCordsComponent : Component
{
}

/// <summary>
/// Word of command: forces the target to its knees.
/// </summary>
public sealed partial class VoiceOfGodActionEvent : EntityTargetActionEvent
{
}
