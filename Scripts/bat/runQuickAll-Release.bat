REM SPDX-FileCopyrightText: 2026 Egor Romanovich
REM
REM SPDX-License-Identifier: AGPL-3.0-or-later

@echo off
cd ../../

call dotnet build -c Release
if errorlevel 1 exit /b 1

start "SS14 Server (Release)" dotnet run --project Content.Goobstation.Server -c Release --no-build
timeout /t 8 /nobreak >nul
start "SS14 Client (Release)" dotnet run --project Content.Goobstation.Client -c Release --no-build

exit
