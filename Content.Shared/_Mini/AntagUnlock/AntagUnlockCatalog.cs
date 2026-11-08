// SPDX-FileCopyrightText: 2026 Casha
// SPDX-License-Identifier: LicenseRef-MiniStation-1.0

namespace Content.Shared._Mini.AntagUnlock;

public static class AntagUnlockCatalog
{
    public const string EntryPrefix = "antag-unlock:";

    public static string GetEntryId(string antagId) => $"{EntryPrefix}{antagId}";
}
