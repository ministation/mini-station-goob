// SPDX-License-Identifier: LicenseRef-MiniStation-1.0

namespace Content.Shared._Lavaland.BossMechanics;

/// <summary>
/// Marks the AI-uplink brain organ. While it sits in a robotic body's brain slot the
/// station AI can take the body over with its regular remote control verb.
/// </summary>
[RegisterComponent]
public sealed partial class AiUplinkBrainComponent : Component;

/// <summary>
/// Marker put on a body that currently has an <see cref="AiUplinkBrainComponent"/> inside.
/// </summary>
[RegisterComponent]
public sealed partial class AiUplinkShellComponent : Component;
