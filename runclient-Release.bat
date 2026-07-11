REM SPDX-FileCopyrightText: 2026 Egor Romanovich
REM
REM SPDX-License-Identifier: AGPL-3.0-or-later

@echo off
dotnet run --project Content.Goobstation.Client --configuration Release --no-build %*
