# SPDX-License-Identifier: AGPL-3.0-or-later

# Role mention is passed as $rolePing from C# (raw "<@&id>" must not appear in FTL — Fluent treats <> as tags).
# ">>>" is Discord block-quote (side bar). Keep it as the first characters of the message.
discord-round-notifications-new =
    >>> { $rolePing }🆕 **Новый раунд начнётся через 3 минуты!**
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
    >>> { $rolePing }**Раунд перезапускается!**
    `{ $playerCount }` игроков сейчас играет
    Новый раунд начнётся через 3 минуты!
discord-round-notifications-unknown-map = *Неизвестная карта*
