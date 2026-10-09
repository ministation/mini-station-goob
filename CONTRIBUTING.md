# Space Station 14 Contributing Guidelines

## Правила Mini-Station (форк)

**Каждый PR, меняющий игровой процесс, обязан содержать корректный ченйдлог `:cl:`** в описании PR — бот перенесёт его в игровой ченйдлог.

Как писать нормально:
- пишите для игроков: что изменится в игре, а не какие файлы, классы или системы правились;
- по-русски, без кодового жаргона (никаких «рефактор X», «фикс NRE», названий компонентов);
- тип по смыслу: `add` — новое, `tweak` — изменение существующего, `fix` — исправление, `remove` — удаление;
- одна строка — одно законченное изменение; важнее причина и следствие, чем детали реализации.

Образец:

```
:cl:
- fix: Убрана ошибочная правка скорости в анимации ходьбы, из-за которой шаг мог включаться у стоящего пассажира шаттла.
```

PR без игрового эффекта (рефакторинг, CI, документация) может обойтись без `:cl:`.

Thanks for contributing to Space Station 14.
When contributing, be sure to follow our [codebase conventions](https://docs.spacestation14.com/en/general-development/codebase-info/codebase-organization.html) and [PR guidelines](https://docs.spacestation14.com/en/general-development/codebase-info/pull-request-guidelines.html).

Following these guidelines helps us increase review turnaround time, so be sure to review the linked documents in full.

The last major guidelines update was on **December 6th, 2025**.

### Why is this here?
We put this here so that GitHub will notify you when submitting a pull request that the PR guidelines have changed, if you haven't read the latest version.
