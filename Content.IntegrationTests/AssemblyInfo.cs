// SPDX-License-Identifier: AGPL-3.0-or-later

[assembly: Parallelizable(ParallelScope.Children)]

// I don't know why this parallelism limit was originally put here.
// I *do* know that I tried removing it, and ran into the following .NET runtime problem:
// https://github.com/dotnet/runtime/issues/107197
// So we can't really parallelize integration tests harder either until the runtime fixes that,
// *or* we fix serv3 to not spam expression trees.
// Mini: 3 полосы проверены (PR #725) и ВРЕДНЫ: main-шард 408с против 346с
// при двух (6 процессов на 4 ядрах раннера, контеншн). Ускорение сделано
// ребалансом шардов (main-heavy), лимит оставлен на 2.
[assembly: LevelOfParallelism(2)]
