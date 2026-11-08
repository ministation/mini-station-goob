// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared.Mobs;

namespace Content.Server._Lavaland.BossMechanics;

/// <summary>
/// Raised broadcast when the demonic frost miner enrages, so the arena prisms
/// can turn violet (TG: blue to violet prism reaction).
/// </summary>
[ByRefEvent]
public readonly record struct FrostMinerEnragedEvent;

/// <summary>
/// Raised broadcast when the demonic frost miner dies, so the arena prisms
/// can dim to gray (TG: prisms turn gray on death).
/// </summary>
[ByRefEvent]
public readonly record struct FrostMinerDiedEvent(EntityUid Boss, MobState State);
