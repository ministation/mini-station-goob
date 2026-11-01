// SPDX-FileCopyrightText: 2026 Goob Station Contributors
//
// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Goobstation.Server.StationEvents.Components;
using Content.Shared.Roles;
using Robust.Shared.Prototypes;

namespace Content.Goobstation.Server.StationEvents.GameDirector;

public readonly record struct RankedEvent(PossibleEvent PossibleEvent, float Score);

public sealed class PlayerCount
{
    public int Players;
    public int Ghosts;
    public int InGame;
    public Dictionary<ProtoId<DepartmentPrototype>, int> Departments = new();

    public int Staff(ProtoId<DepartmentPrototype> department)
    {
        return Departments.GetValueOrDefault(department);
    }
}
