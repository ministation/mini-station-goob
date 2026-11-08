// SPDX-FileCopyrightText: 2026 Space Station 14 Contributors
//
// SPDX-License-Identifier: LicenseRef-MiniStation-1.0

// SPDX-FileCopyrightText: 2026 Casha
// SPDX-License-Identifier: LicenseRef-MiniStation-1.0

using System;
using System.Collections.Generic;
using Robust.Shared.Serialization;

namespace Content.Shared._Mini.AntagTokens;

[Serializable, NetSerializable]
public sealed class AntagTokenOpenRequestEvent : EntityEventArgs
{
}

[Serializable, NetSerializable]
public sealed class AntagTokenPurchaseRequestEvent(string roleId) : EntityEventArgs
{
    public string RoleId { get; private set; } = roleId;
}

[Serializable, NetSerializable]
public sealed class AntagTokenClearRequestEvent : EntityEventArgs
{
}

[Serializable, NetSerializable]
public sealed class AntagTokenStateEvent(AntagTokenState state) : EntityEventArgs
{
    public AntagTokenState State { get; private set; } = state;
}

[Serializable, NetSerializable]
public sealed class AntagTokenState(
    int balance,
    int monthlyEarned,
    string? activeDepositRoleId,
    List<AntagTokenRoleEntry> roles) : BoundUserInterfaceState
{
    public int Balance { get; private set; } = balance;
    public int MonthlyEarned { get; private set; } = monthlyEarned;
    public string? ActiveDepositRoleId { get; private set; } = activeDepositRoleId;
    public List<AntagTokenRoleEntry> Roles { get; private set; } = roles;
}

[Serializable, NetSerializable]
public sealed class AntagTokenRoleEntry(
    string roleId,
    int cost,
    AntagPurchaseMode mode,
    bool purchased,
    int freeUnlocks,
    bool canAfford,
    bool saturated,
    bool available,
    string? tagLocKey,
    string? statusLocKey,
    int purchaseCooldownSecondsRemaining = 0,
    bool freePurchaseAvailable = false)
{
    public string RoleId { get; private set; } = roleId;
    public int Cost { get; private set; } = cost;
    public AntagPurchaseMode Mode { get; private set; } = mode;
    public bool Purchased { get; private set; } = purchased;
    public int FreeUnlocks { get; private set; } = freeUnlocks;
    public bool CanAfford { get; private set; } = canAfford;
    public bool Saturated { get; private set; } = saturated;
    public bool Available { get; private set; } = available;
    public string? TagLocKey { get; private set; } = tagLocKey;
    public string? StatusLocKey { get; private set; } = statusLocKey;
    public int PurchaseCooldownSecondsRemaining { get; private set; } = purchaseCooldownSecondsRemaining;
    public bool FreePurchaseAvailable { get; private set; } = freePurchaseAvailable;
}
