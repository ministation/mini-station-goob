plant-analyzer-component-no-seed = растения не обнаружено

plant-analyzer-component-health = Здоровье:
plant-analyzer-component-age = Возраст:
plant-analyzer-component-water = Вода:
plant-analyzer-component-nutrition = Питание:
plant-analyzer-component-toxins = Токсины:
plant-analyzer-component-pests = Вредители:
plant-analyzer-component-weeds = Сорняки:

plant-analyzer-component-alive = [color=green]ЖИВОЕ[/color]
plant-analyzer-component-dead = [color=red]МЁРТВОЕ[/color]
plant-analyzer-component-unviable = [color=red]НЕЖИЗНЕСПОСОБНОЕ[/color]
plant-analyzer-component-mutating = [color=#00ff5f]МУТИРУЕТ[/color]
plant-analyzer-component-kudzu = [color=red]КУДЗУ[/color]

plant-analyzer-soil = В этом {$holder} содержится некоторое количество [color=white]{$chemicals}[/color], которые {$count ->
    [one]не
    *[other]не
} были поглощены.
plant-analyzer-soil-empty = В этом {$holder} нет непоглощённых химикатов.

plant-analyzer-component-environemt = Этому [color=green]{$seedName}[/color] требуется атмосфера с давлением [color=lightblue]{$kpa}кПа ± {$kpaTolerance}кПа[/color], температурой [color=lightsalmon]{$temp}°K ± {$tempTolerance}°K[/color] и уровнем освещённости [color=white]{$lightLevel} ± {$lightTolerance}[/color].
plant-analyzer-component-environemt-void = Этот [color=green]{$seedName}[/color] должен расти [bolditalic]в вакууме космоса[/bolditalic] при уровне освещённости [color=white]{$lightLevel} ± {$lightTolerance}[/color].
plant-analyzer-component-environemt-gas = Этому [color=green]{$seedName}[/color] требуется атмосфера, содержащая [bold]{$gases}[/bold], с давлением [color=lightblue]{$kpa}кПа ± {$kpaTolerance}кПа[/color], температурой [color=lightsalmon]{$temp}°K ± {$tempTolerance}°K[/color] и уровнем освещённости [color=white]{$lightLevel} ± {$lightTolerance}[/color].

plant-analyzer-produce-plural = {MAKEPLURAL($thing)}
plant-analyzer-output = {$yield ->
    [0]{$gasCount ->
        [0]Похоже, оно лишь потребляет воду и питательные вещества.
        *[other]Похоже, оно лишь превращает воду и питательные вещества в [bold]{$gases}[/bold].
    }
    *[other]У него есть [color=lightgreen]{$yield} {$potency}[/color]{$seedless ->
        [true]{" "}, но оно [color=red]бессемянное[/color]
        *[false]{$nothing}
    }{" "}{$yield ->
        [one]соцветие
        *[other]соцветия
    }{" "}, которые{$gasCount ->
        [0]{$nothing}
        *[other]{$yield ->
            [one]{" "}выделяет
            *[other]{" "}выделяют
        }{" "}[bold]{$gases}[/bold] и
    }{" "}превращаются{$yield ->
        [one]{" "}в {INDEFINITE($firstProduce)} [color=#a4885c]{$produce}[/color]
        *[other]{" "}в [color=#a4885c]{$producePlural}[/color]
    }.{$chemCount ->
        [0]{$nothing}
        *[other]{" "}В его стебле содержатся следы [color=white]{$chemicals}[/color].
    }
}

plant-analyzer-potency-tiny = крошечного
plant-analyzer-potency-small = небольшого
plant-analyzer-potency-below-average = ниже среднего размера
plant-analyzer-potency-average = среднего размера
plant-analyzer-potency-above-average = выше среднего размера
plant-analyzer-potency-large = довольно большого
plant-analyzer-potency-huge = огромного
plant-analyzer-potency-gigantic = гигантского
plant-analyzer-potency-ludicrous = невероятно большого
plant-analyzer-potency-immeasurable = неизмеримо большого

plant-analyzer-print = Печать
plant-analyzer-printout-missing = Н/Д
plant-analyzer-printout = [color=#9FED58][head=2]Отчёт анализатора растений[/head][/color]{$nl
    }──────────────────────────────{$nl
    }[bullet/] Вид: {$seedName}{$nl
    }{$indent}[bullet/] Жизнеспособность: {$viable ->
        [no][color=red]Нет[/color]
        [yes][color=green]Да[/color]
        *[other]{LOC("plant-analyzer-printout-missing")}
    }{$nl
    }{$indent}[bullet/] Выносливость: {$endurance}{$nl
    }{$indent}[bullet/] Продолжительность жизни: {$lifespan}{$nl
    }{$indent}[bullet/] Продукт: [color=#a4885c]{$produce}[/color]{$nl
    }{$indent}[bullet/] Кудзу: {$kudzu ->
        [no][color=green]Нет[/color]
        [yes][color=red]Да[/color]
        *[other]{LOC("plant-analyzer-printout-missing")}
    }{$nl
    }[bullet/] Профиль роста:{$nl
    }{$indent}[bullet/] Вода: [color=cyan]{$water}[/color]{$nl
    }{$indent}[bullet/] Питание: [color=orange]{$nutrients}[/color]{$nl
    }{$indent}[bullet/] Токсины: [color=yellowgreen]{$toxins}[/color]{$nl
    }{$indent}[bullet/] Вредители: [color=magenta]{$pests}[/color]{$nl
    }{$indent}[bullet/] Сорняки: [color=red]{$weeds}[/color]{$nl
    }[bullet/] Профиль окружающей среды:{$nl
    }{$indent}[bullet/] Состав: [bold]{$gasesIn}[/bold]{$nl
    }{$indent}[bullet/] Давление: [color=lightblue]{$kpa}кПа ± {$kpaTolerance}кПа[/color]{$nl
    }{$indent}[bullet/] Температура: [color=lightsalmon]{$temp}°K ± {$tempTolerance}°K[/color]{$nl
    }{$indent}[bullet/] Освещённость: [color=gray][bold]{$lightLevel} ± {$lightTolerance}[/bold][/color]{$nl
    }[bullet/] Соцветия: {$yield ->
        [-1]{LOC("plant-analyzer-printout-missing")}
        [0][color=red]0[/color]
        *[other][color=lightgreen]{$yield} {$potency}[/color]
    }{$nl
    }[bullet/] Семена: {$seeds ->
        [no][color=red]Нет[/color]
        [yes][color=green]Да[/color]
        *[other]{LOC("plant-analyzer-printout-missing")}
    }{$nl
    }[bullet/] Химикаты: [color=gray][bold]{$chemicals}[/bold][/color]{$nl
    }[bullet/] Выбросы: [bold]{$gasesOut}[/bold]
