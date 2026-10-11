# Голосовой чат: сборка и запуск клиента (Mini)

Голосовой чат клиента (`Content.Goobstation.Client/Voice`) работает с сырыми типами
`Lidgren.Network`, `NAudio` и `Concentus` (Opus). Клиентская песочница движка эти типы
**запрещает** — при загрузке мода она печатает `Sandbox violation: Access to type not allowed`
и роняет клиент на старте:

```
Unhandled exception. System.AggregateException: One or more errors occurred.
 (Assembly Content.Goobstation.Client failed type checks.)
```

Поэтому войс-код клиента собирается **только по флагу** и не входит ни в обычные сборки, ни в
упакованные `FULL_RELEASE`-клиенты.

## Обычная сборка (войса нет, всё работает)

```powershell
dotnet build Content.Client -c Debug
```

Клиент стартует как обычно, песочница включена. Такой клиент, как и опубликованный
`FULL_RELEASE`, в голосовом чате не участвует: сообщения об опт-ине он просто не отправляет.

## Сборка с голосовым чатом (для тестов)

```powershell
# именно Rebuild: смена DefineConstants не инвалидирует инкрементальную сборку,
# и обычный build просто пропустит Content.Goobstation.Client как up-to-date
dotnet build Content.Goobstation.Client -c Debug -p:MiniVoiceChat=True -t:Rebuild
dotnet build Content.Client -c Debug -p:MiniVoiceChat=True
$env:ROBUST_DISABLE_SANDBOX=1     # обязательно: иначе типчек убьёт клиент на старте
```

`-p:MiniVoiceChat=True` определяет `VOICECHAT` и подключает пакеты `NAudio`/`Concentus`.
`ROBUST_DISABLE_SANDBOX=1` — переменная окружения движка (`GameController`), она отключает
проверку типов; для локальной отладки это нормально, для публичных сборок — нет.

Использовать войс на живом сервере (`[voice] enabled = true`) при этом могут только такие
отладочные клиенты: у остальных войс-кода нет.

## Если войс нужен всем игрокам

Тогда песочницу надо не отключать, а расширить: вайтлист лежит в движке —
`RobustToolbox/Robust.Shared/ContentPack/Sandbox.yml` (встроенный ресурс `Robust.Shared`).
Добавить туда нужные пространства имён (`NAudio`, `Concentus`, `Lidgren`) можно только в
движковом форке `ministation/RobustToolbox`, после чего поднять сабмодуль в этом репозитории.
Это ослабление песочницы (контент получает доступ к сырой сети и аудио), поэтому решение —
за владельцем.

## Что было раньше

Раньше войс-код клиента был под `#if DEVELOPMENT`, а `DEVELOPMENT` определяется движком для
**любой** сборки, кроме `FULL_RELEASE`. В итоге падал вообще каждый локальный клиент, а не
только тот, кому нужен войс. Теперь условие — `VOICECHAT` (флаг `MiniVoiceChat`).
