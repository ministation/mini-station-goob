# SPDX-License-Identifier: AGPL-3.0-or-later

# Discord mentions must not use raw "<@&...>" — Fluent treats "<...>" as markup tags and crashes locale load.
discord-round-notifications-new =
    { $rolePing }🆕 **Новый раунд начнётся через 3 минуты!**
    `{ $playerCount }` игроков сейчас играет
discord-round-notifications-started =
    >>> Раунд #{ $id } начался!
    Карта: { $map }
    Режим: { $gamemode }
    Игроков `{ $playerCount }`
discord-round-notifications-end =
    >>> Раунд #{ $id } завершён
    Длительность: { $hours }ч { $minutes }м { $seconds }с
    Игроков `{ $playerCount }`
    Режим: { $gamemode }
discord-round-notifications-end-ping =
    { $rolePing }**Раунд перезапускается!**
    `{ $playerCount }` игроков сейчас играет
    Новый раунд начнётся через 3 минуты!
discord-round-notifications-unknown-map = *Неизвестная карта*
