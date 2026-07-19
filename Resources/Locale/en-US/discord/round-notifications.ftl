# SPDX-FileCopyrightText: 2023 Morb <14136326+Morb0@users.noreply.github.com>
# SPDX-FileCopyrightText: 2025 Aiden <28298836+Aidenkrz@users.noreply.github.com>
# SPDX-FileCopyrightText: 2025 Aiden <aiden@djkraz.com>
#
# SPDX-License-Identifier: AGPL-3.0-or-later

# Discord mentions must not use raw "<@&...>" — Fluent treats "<...>" as markup tags and crashes locale load.
discord-round-notifications-new =
    { $rolePing }🆕 A new round starts in 3 minutes!
    `{ $playerCount }` players online
discord-round-notifications-started = Round #{$id} on map "{$map}" ({$gamemode}) started. Players: {$playerCount}
discord-round-notifications-end = Round #{$id} has ended. It lasted for {$hours} hours, {$minutes} minutes, and {$seconds} seconds. Players: {$playerCount}. Mode: {$gamemode}
discord-round-notifications-end-ping =
    { $rolePing }a new round will start soon!
    `{ $playerCount }` players online
discord-round-notifications-unknown-map = Unknown
