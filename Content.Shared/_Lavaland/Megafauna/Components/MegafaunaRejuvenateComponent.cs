// SPDX-License-Identifier: LicenseRef-MiniStation-1.0

﻿using Robust.Shared.GameStates;

namespace Content.Shared._Lavaland.Megafauna.Components;

/// <summary>
/// Fully heals megafauna after its shutdown.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class MegafaunaRejuvenateComponent : Component;
