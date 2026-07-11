typan-station-goal-objective-issuer = Станция Тайпан

typan-war-objective-issuer = Война станций



typan-war-sender = Штаб станционной войны
typan-war-sender-nt = Штаб станционной войны (Нанотрайзен)
typan-war-sender-typan = Штаб станционной войны (Синдикат)



typan-war-declaration = Объявлена война между станциями Нанотрайзен и Тайпан. Всему персоналу — немедленно вооружиться и следовать протоколам боевой готовности.



typan-war-objective-typan = Минимизируйте потери экипажа Тайпан и выполняйте станционную цель. Код «Омега» — станция на полной боевой готовности.

typan-war-objective-nt = Защитите станцию Нанотрайзен и минимизируйте потери экипажа. Код «Гамма» — военное положение.



typan-war-hud-nt = NT

typan-war-hud-typan = Синдикат

typan-war-hud-pending = Подготовка

typan-war-hud-active = Война

typan-war-hud-ended = Конец

typan-war-hud-winner-nt = Победа NT

typan-war-hud-winner-typan = Победа Синдиката



typan-war-end-warning = До окончания войны станций осталась одна минута.

typan-war-event-supply = [color=#C8C4D8]Война станций:[/color] снабжение развёрнуто на обеих станциях — проверьте военные склады.

typan-war-event-intel = [color=#A8C8FF]Очки NT:[/color] {$nt}. [color=#FFB0B0]Синдикат:[/color] {$typan}.

typan-war-capture-zones-active-header = Внимание, бойцы Нанотрайзен и Синдиката! Обозначены стратегически важные зоны захвата:
typan-war-capture-zones-active-line = Зона {$label} — {$location}
typan-war-capture-zone-named = Зона {$label} ({$location})
typan-war-capture-location-station = {$station} — {$area}
typan-war-capture-location-trade-nt = Торговый аванпост Нанотрайзен
typan-war-capture-location-trade-typan = Торговый аванпост Синдиката
typan-war-hud-zone-line = Зона {$label}: {$location}
typan-war-capture-nt = Нанотрайзен захватил {$zone}.
typan-war-capture-typan = Синдикат захватил {$zone}.
typan-war-capture-neutral = {$zone} стала нейтральной.
typan-war-capture-loot-nt = На {$zone} сброшен ящик [color=#A8C8FF]снабжения Нанотрайзен[/color].
typan-war-capture-loot-typan = На {$zone} сброшен ящик [color=#FFB0B0]снабжения Синдиката[/color].

typan-war-surplus-nt-crate-name = ящик снабжения NT
typan-war-surplus-nt-crate-desc = Командный ящик снабжения Нанотрайзен. Каждая позиция проверяется отдельно — Drozd ~50%, скафандр СБ ~70%, ОБР ~5%, аптечки и патроны со своими шансами.
typan-war-surplus-typan-crate-name = ящик снабжения Синдиката
typan-war-surplus-typan-crate-desc = Полевой ящик снабжения Синдиката. Каждая позиция проверяется отдельно — C-20r ~50%, патрульный скафандр ~70%, кроваво-красный ~5%, аптечки и патроны со своими шансами.

typan-war-hud-zone-inactive = неактивна
typan-war-hud-zone-owner-neutral = нейтральная
typan-war-hud-zone-owner-nanotrasen = NT
typan-war-hud-zone-owner-typan = Синдикат

typan-war-prep-announce = Объявлена война между станциями Нанотрайзен и Тайпан. Блюспейс-прыжки с консоли заблокированы до начала боевых действий. Следуйте протоколам боевой готовности — через пять минут начнутся боевые действия.

typan-war-manifest =
    {"[font size=18][color=#C8C4D8][bold]ВОЙНА СТАНЦИЙ — БОЕВАЯ СВОДКА[/bold][/color][/font]"}
    {"[color=#888898]────────────────────────────────[/color]"}
    {"[color=#C8C4D8]Силы [color=#A8C8FF][bold]Нанотрайзен[/bold][/color] и [color=#FFB0B0][bold]Синдиката (Тайпан)[/bold][/color] ведут открытые боевые действия на общем поле боя.[/color]"}
    {" "}
    {"[color=#C8C4D8][bold]Главные задачи[/bold][/color]"}
    {"[color=#A8C8FF]▸ NT:[/color] уничтожить боеспособные силы Синдиката и удерживать зоны захвата."}
    {"[color=#FFB0B0]▸ Синдикат:[/color] уничтожить боеспособные силы NT и удерживать зоны захвата."}
    {" "}
    {"[color=#C8C4D8][bold]Зоны захвата[/bold][/color] [color=#888898](A / B / C)[/color]"}
    {"[color=#C8C4D8]▸[/color] Три зоны 3×3 появляются с началом боя — станция NT, станция Тайпан и торговый аванпост."}
    {"[color=#C8C4D8]▸[/color] Удержание зоны даёт очки захвата и периодически сбрасывает [bold]ящики снабжения[/bold]."}
    {"[color=#C8C4D8]▸[/color] Зоны NT — [color=#A8C8FF]снабжение NT[/color]; зоны Синдиката — [color=#FFB0B0]снабжение Синдиката[/color]."}
    {" "}
    {"[color=#C8C4D8][bold]Правила боя[/bold][/color]"}
    {"[color=#C8C4D8]▸[/color] Дальнобойное оружие [bold]не бьёт по союзникам[/bold] — при необходимости включите [bold]«Защиту союзников»[/bold]."}
    {"[color=#C8C4D8]▸[/color] Подкрепления возрождаются на захваченных зонах или на спавне должности."}



typan-war-ff-enabled = Защита союзников включена — вы не задеваете своих.

typan-war-ff-disabled = Защита союзников выключена.

typan-war-respawn-title = Запрос подкрепления
typan-war-respawn-timer = Подкрепление через {$seconds} с
typan-war-respawn-ready = Выберите точку высадки
typan-war-respawn-zone = Зона {$label} — {$location}
typan-war-respawn-zone-desc = Высадка на захваченной точке вашей фракции.
typan-war-respawn-base = Спавн должности
typan-war-respawn-base-desc = Возврат на исходную точку спавна.
typan-war-respawn-no-options = Нет доступных точек высадки.
typan-war-respawn-no-profile = Не удалось загрузить профиль персонажа.
typan-war-respawn-failed = Высадка не удалась — попробуйте другую точку.

typan-war-minimap-title = Карта войны
typan-war-minimap-legend = NT {$nt} / Синдикат {$typan} — победа при {$win}
typan-war-minimap-map-legend = Силуэты = станции и шаттлы · треугольники = союзники · буквы = зоны захвата
typan-war-minimap-controls = Колёсико — масштаб · перетаскивание — перемещение
typan-war-ghost-thunderdome-blocked = Thunderdome недоступен во время войны станций.

typan-war-ftl-blocked = Блюспейс-прыжки заблокированы до начала боевых действий.

typan-war-drop-shuttle-docked-nt = Шаттл подкрепления Нанотрайзен пристыковался к станции с {$direction}, локация: {$location}.
typan-war-drop-shuttle-docked-typan = Шаттл подкрепления Синдиката пристыковался к станции с {$direction}, локация: {$location}.

typan-war-balance-lobby-wait = Война станций: на стороне NT слишком много игроков. Вы остались в лобби — зайдите через позднее присоединение на станцию Тайпан.
typan-war-balance-lobby-wait-typan = Война станций: на стороне Тайпан слишком много игроков. Вы остались в лобби — зайдите через позднее присоединение на станцию NT.
typan-war-balance-latejoin-nt-full = На станции NT слишком много игроков. Сейчас доступен только вход на станцию Тайпан.
typan-war-balance-latejoin-typan-full = На станции Тайпан слишком много игроков. Сейчас доступен только вход на станцию NT.
typan-war-balance-latejoin-both-full = Обе стороны переполнены — подождите, пока баланс восстановится.
typan-war-balance-job-denied = Эта фракция переполнена. Выберите сторону с меньшим числом игроков.

typan-war-start-cancelled = Война станций отменена — недостаточно боеспособного персонала на одной из сторон.

typan-war-start-cancelled-nt = Война станций отменена — на стороне NT только {$nt} боеспособных (требуется {$ntMin}).

typan-war-start-cancelled-typan = Война станций отменена — на стороне Тайпан только {$typan} боеспособных (требуется {$typanMin}).



typan-war-end-announce-nt = Война станций завершена. Победа [color=#A8C8FF]Нанотрайзен[/color] — меньшие относительные потери.

typan-war-end-announce-typan = Война станций завершена. Победа [color=#FFB0B0]Синдиката[/color] — меньшие относительные потери.

typan-war-end-announce-nt-elimination = Война станций завершена. Победа [color=#A8C8FF]Нанотрайзен[/color] — все силы [color=#FFB0B0]Синдиката[/color] уничтожены.

typan-war-end-announce-typan-elimination = Война станций завершена. Победа [color=#FFB0B0]Синдиката[/color] — все силы [color=#A8C8FF]Нанотрайзен[/color] уничтожены.

typan-war-end-announce-stalemate = Война станций завершена. Ничья — обе стороны понесли равные потери.



typan-war-round-end-header = [color=#C8C4D8][bold]Война станций[/bold][/color]

typan-war-round-end-initial = Численность армий: NT {$nt}, Тайпан {$typan}

typan-war-round-end-final = Выжило: NT {$nt}, Тайпан {$typan}

typan-war-round-end-losses = Потери: NT {$ntLoss}%, Тайпан {$typanLoss}%

typan-war-round-end-nt-goal = Станционная цель NT: {$goal}

typan-war-round-end-typan-goal = Станционная цель Тайпан: {$goal}

typan-war-round-end-winner-nt = Победитель: Нанотрайзен

typan-war-round-end-winner-typan = Победитель: Тайпан

typan-war-round-end-stalemate = Результат: ничья



game-presets-typan-station-war = Война станций

game-presets-typan-station-war-description = Конфликт между станцией Нанотрайзен и станцией Тайпан. Уничтожьте врага или победите по меньшим потерям к концу таймера.

