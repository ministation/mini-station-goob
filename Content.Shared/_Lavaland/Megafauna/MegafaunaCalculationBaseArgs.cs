// SPDX-FileCopyrightText: 2026 Space Station 14 Contributors
//
// SPDX-License-Identifier: LicenseRef-MiniStation-1.0

using Robust.Shared.Prototypes;
using Robust.Shared.Random;

namespace Content.Shared._Lavaland.Megafauna;

/// <summary>
/// Arguments that are used for Megafauna Actions and Conditions.
/// </summary>
public record struct MegafaunaCalculationBaseArgs(
    EntityUid Entity,
    IEntityManager EntityManager,
    IPrototypeManager PrototypeMan,
    ISawmill Logger,
    IRobustRandom Random);
