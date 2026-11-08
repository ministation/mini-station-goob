// SPDX-FileCopyrightText: 2026 Casha
// SPDX-License-Identifier: LicenseRef-MiniStation-1.0

namespace Content.Shared._Mini.GhostRolePurchase;

public static class GhostRolePriceCatalog
{
    public static readonly Dictionary<string, int> Prices = new()
    {
        { "Thief", 1 },
        { "Agent", 2 },
        { "Revenant", 2 },
        { "Ninja", 3 },
        { "Dragon", 3 },
        { "Abductor", 3 },
        { "InitialInfected", 3 },
        { "Devil", 3 },
        { "SpaceCultist", 4 },
        { "RevolutionaryHead", 5 },
        { "Yao", 5 },
        { "Heretic", 6 },
        { "Shadowling", 6 },
        { "Wizard", 8 },
        { "Changeling", 8 },
        { "Blob", 10 },
        { "SlaughterDemon", 10 },
        { "Slasher", 5 },
        { "Xenomorph", 5 },
        { "Bingle", 5 },
    };

    public static bool TryGetPrice(string roleId, out int price)
    {
        return Prices.TryGetValue(roleId, out price);
    }

    public static int GetPrice(string roleId)
    {
        if (!Prices.TryGetValue(roleId, out var price))
        {
            throw new KeyNotFoundException($"Ghost role '{roleId}' not found in price catalog.");
        }

        return price;
    }
}
