# SPDX-FileCopyrightText: 2021 DrSmugleaf <DrSmugleaf@users.noreply.github.com>
# SPDX-FileCopyrightText: 2021 Galactic Chimp <63882831+GalacticChimp@users.noreply.github.com>
# SPDX-FileCopyrightText: 2021 Pieter-Jan Briers <pieterjan.briers+git@gmail.com>
# SPDX-FileCopyrightText: 2022 20kdc <asdd2808@gmail.com>
# SPDX-FileCopyrightText: 2024 Aidenkrz <aiden@djkraz.com>
# SPDX-FileCopyrightText: 2024 Kira Bridgeton <161087999+Verbalase@users.noreply.github.com>
# SPDX-FileCopyrightText: 2024 Repo <47093363+Titian3@users.noreply.github.com>
# SPDX-FileCopyrightText: 2024 Vigers Ray <60344369+VigersRay@users.noreply.github.com>
# SPDX-FileCopyrightText: 2025 Aiden <28298836+Aidenkrz@users.noreply.github.com>
# SPDX-FileCopyrightText: 2025 Aiden <aiden@djkraz.com>
#
# SPDX-License-Identifier: AGPL-3.0-or-later

### Connecting dialog when you start up the game

connecting-title = Oasis
connecting-exit = Exit
connecting-retry = Retry
connecting-reconnect = Reconnect
connecting-copy = Copy Message
connecting-redial = Relaunch
connecting-redial-wait = Please wait: { TOSTRING($time, "G3") }
connecting-in-progress = Connecting to server...
connecting-disconnected = Disconnected from server:
connecting-tip = Don't die!
connecting-window-tip = Tip { $numberTip }
connecting-version = Oasis
connecting-fail-reason = Failed to connect to server:
                         { $reason }
connecting-state-NotConnecting = Not connecting
connecting-state-ResolvingHost = Resolving host
connecting-state-EstablishingConnection = Establishing connection
connecting-state-Handshake = Handshake
connecting-state-Connected = Connected

connecting-info-players-loading = Online: ...
connecting-info-players = Online: { $players } / { $max }
connecting-info-players-only = Online: { $players }
connecting-info-players-unavailable = Online: unavailable
connecting-info-map-loading = Map: ...
connecting-info-map = Map: { $map }
connecting-info-map-unknown = Map: unknown
connecting-info-map-unavailable = Map: unavailable
connecting-info-preset-loading = Mode: ...
connecting-info-preset = Mode: { $preset }
connecting-info-preset-unknown = Mode: unknown
connecting-info-preset-unavailable = Mode: unavailable
connecting-info-coins = Coins: { $balance }
connecting-info-coins-unknown = Coins: after login

connecting-whitelist-title = Closed enrollment
connecting-whitelist-body = Oasis only accepts approved players. Fill out the application in Discord and wait for staff approval.
connecting-whitelist-discord = Open Discord
connecting-whitelist-hint = After approval, try connecting again.
