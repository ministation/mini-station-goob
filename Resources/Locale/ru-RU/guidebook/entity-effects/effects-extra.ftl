reagent-effect-guidebook-status-effect-delay = { $type ->
    [add]   { $chance ->
                [1] Вызывает
                *[other] вызывают
            } { LOC($key) } минимум на { NATURALFIXED($time, 3) } { $time ->
                [one] секунду
                [few] секунды
                *[other] секунд
            } с накоплением эффекта
    *[set]  { $chance ->
                [1] Вызывает
                *[other] вызывают
            } { LOC($key) } минимум на { NATURALFIXED($time, 3) } { $time ->
                [one] секунду
                [few] секунды
                *[other] секунд
            } без накопления эффекта
    [remove]{ $chance ->
                [1] Удаляет
                *[other] удаляют
            } { NATURALFIXED($time, 3) } { $time ->
                [one] секунду
                [few] секунды
                *[other] секунд
            } от { LOC($key) }
    } после задержки в { NATURALFIXED($delay, 3) } { $delay ->
                [one] секунду
                [few] секунды
                *[other] секунд
            }
