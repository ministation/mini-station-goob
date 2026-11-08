// SPDX-FileCopyrightText: 2026 Casha
// SPDX-License-Identifier: LicenseRef-MiniStation-1.0

namespace Content.Shared._Mini.JobUnlock;

public static class JobUnlockCatalog
{
    public const string EntryPrefix = "job-unlock:";

    public static string GetEntryId(string jobId) => $"{EntryPrefix}{jobId}";
}
