// SPDX-FileCopyrightText: 2026 Goob Station Contributors
//
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Numerics;
using Content.Goobstation.Shared.Nullspace;

namespace Content.Goobstation.Client.Nullspace;

public sealed class NullspaceClownTrack
{
    public EntityUid? Uid;
    public Vector2 Position;
    public float Phase;
    public NullspaceClownState State;
    public EntityUid? Target;
}
