discord-round-notifications-new =
    >>> <@&{ $roleId }>
    🆕 **Новый раунд начнётся через 2 минуты!**
discord-round-notifications-started =
    >>> Раунд #{ $id } начался!
    Карта: { $map }
    Режим: { $gamemode }
discord-round-notifications-end =
    >>> Раунд #{ $id } завершён
    Длительность: { $hours }ч { $minutes }м { $seconds }с
    Режим: { $gamemode }
    ```
    { $manifest }
    ```
discord-round-notifications-end-no-manifest =
    >>> Раунд #{ $id } завершён
    Длительность: { $hours }ч { $minutes }м { $seconds }с
    Режим: { $gamemode }
discord-round-notifications-end-ping =
    >>> **Сервер перезапускается!**
    Новый раунд начнётся через 3 минуты!
discord-round-notifications-unknown-map = *Неизвестная карта*
