// SPDX-License-Identifier: LicenseRef-MiniStation-1.0

﻿using Robust.Shared.GameStates;

namespace Content.Shared._Lavaland.Megafauna.Components;

/// <summary>
/// Makes megafauna immune to damage without an origin and any damage while AI is inactive.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class MegafaunaGodmodeComponent : Component;
