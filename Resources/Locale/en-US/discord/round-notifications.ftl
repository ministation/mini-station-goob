# SPDX-FileCopyrightText: 2023 Morb <14136326+Morb0@users.noreply.github.com>
# SPDX-FileCopyrightText: 2025 Aiden <28298836+Aidenkrz@users.noreply.github.com>
# SPDX-FileCopyrightText: 2025 Aiden <aiden@djkraz.com>
#
# SPDX-License-Identifier: AGPL-3.0-or-later

# Role mention is passed as $rolePing from C# (raw "<@&id>" must not appear in FTL — Fluent treats <> as tags).
# ">>>" is Discord block-quote (side bar). Keep it as the first characters of the message.
discord-round-notifications-new =
    >>> { $rolePing }A new round starts in 3 minutes!
    `{ $playerCount }` players online
discord-round-notifications-started =
    >>> Round #{$id} started!
    Map: {$map}
    Mode: {$gamemode}
    Players: {$playerCount}
discord-round-notifications-end =
    >>> Round #{$id} has ended.
    Duration: {$hours}h {$minutes}m {$seconds}s
    Players: {$playerCount}
    Mode: {$gamemode}
    ```
    {$manifest}
    ```
discord-round-notifications-end-no-manifest =
    >>> Round #{$id} has ended.
    Duration: {$hours}h {$minutes}m {$seconds}s
    Players: {$playerCount}
    Mode: {$gamemode}
discord-round-notifications-end-ping =
    >>> **Round is restarting!**
    `{ $playerCount }` players online
    A new round will start in 3 minutes!
discord-round-notifications-unknown-map = Unknown
