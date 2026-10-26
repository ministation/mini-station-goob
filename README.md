<h1 align="center">Мини-станция</h1>

<p align="center">
  <b>Русскоязычный форк <a href="https://github.com/Goob-Station/Goob-Station">Goob Station</a> для <a href="https://spacestation14.com/">Space Station 14</a>.</b><br>
  Переводим игру на русский, добавляем свой контент и играем на собственном сервере.
</p>

<p align="center">
  <b>Русский</b> | <a href="README.en.md">English</a>
</p>

<p align="center">
  <a href="https://cdn.ministation.ru/fork/ministation"><img alt="Скачать сборку" src="https://img.shields.io/badge/Скачать_сборку-Windows_|_Linux_|_macOS-ffb020?style=for-the-badge&logo=windows&logoColor=white"></a>
  <a href="https://discord.gg/mini-station"><img alt="Discord" src="https://img.shields.io/badge/dynamic/json?url=https%3A%2F%2Fdiscord.com%2Fapi%2Fv10%2Finvites%2Fmini-station%3Fwith_counts%3Dtrue&query=%24.approximate_member_count&label=Discord&style=for-the-badge&logo=discord&logoColor=white&color=5865F2"></a>
  <a href="https://t.me/mini_station"><img alt="Telegram" src="https://img.shields.io/badge/Telegram-@mini__station-26A5E4?style=for-the-badge&logo=telegram&logoColor=white"></a>
</p>

<p align="center">
  <a href="https://ministation.ru"><img alt="Сайт" src="https://img.shields.io/badge/Сайт-ministation.ru-ffb020?style=flat-square&logo=firefoxbrowser&logoColor=white"></a>
  <a href="https://wiki.ministation.ru"><img alt="Вики" src="https://img.shields.io/badge/Вики-wiki.ministation.ru-9b6dff?style=flat-square&logo=gitbook&logoColor=white"></a>
  <a href="https://ministation.ru/donate"><img alt="Донат" src="https://img.shields.io/badge/Донат-поддержать_проект-ff5e5b?style=flat-square&logo=buymeacoffee&logoColor=white"></a>
  <a href="https://github.com/deepseek-ai/deepseek-harness"><img alt="powered by dsh" src="https://img.shields.io/badge/powered_by-dsh-4D6BFE?style=flat-square&logo=deepseek&logoColor=white"></a>
  <a href="https://github.com/ministation/mini-station-goob/actions/workflows/validate.yml"><img alt="CI" src="https://img.shields.io/github/actions/workflow/status/ministation/mini-station-goob/validate.yml?branch=master&style=flat-square&label=CI&color=12b886"></a>
  <a href="LICENSE.TXT"><img alt="Лицензия" src="https://img.shields.io/badge/лицензия-AGPL--3.0--or--later-gold?style=flat-square"></a>
</p>

---

<div align="center">

## О проекте

</div>

**Мини-станция** - некоммерческий русскоязычный проект по Space Station 14. Мы развиваем собственный билд игры: переводим контент, добавляем новые механики и предметы, правим баланс и поддерживаем сервер для нашего сообщества.

Билд основан на [Goob Station](https://github.com/Goob-Station/Goob-Station) и работает на движке [RobustToolbox](https://github.com/space-wizards/RobustToolbox).

- **Русский язык.** Локализация игры, вики и общение в сообществе.
- **Свой контент.** Ежедневные и еженедельные квесты, озвучка текста (ТТС), новые предметы, роли и события.
- **Готовые сборки.** Версии для Windows, Linux и macOS обновляются каждую ночь.
- **ИИ в проекте.** Глубоко интегрируем искусственный интеллект: он помогает нам разрабатывать, переводить и поддерживать сервер.

<div align="center">

## Сборка из исходников

</div>

Понадобятся [.NET SDK 10.0.100+](https://dotnet.microsoft.com/download), Python 3.5+ и Git.

```shell
git clone https://github.com/ministation/mini-station-goob.git
cd mini-station-goob
python RUN_THIS.py
```

`RUN_THIS.py` скачает движок и подмодули. В Linux и macOS команда может называться `python3`.

**Windows**

```bat
cd Scripts\bat
runQuickAll-Debug.bat
```

**Linux и macOS**

```shell
sh Scripts/sh/buildAllDebug.sh
sh Scripts/sh/runQuickAll.sh
```

Оба варианта собирают игру и запускают локальный сервер с клиентом. После запуска подключитесь к `localhost` в окне клиента.

> [!TIP]
> Если `RUN_THIS.py` не смог скачать движок, выполните `git submodule update --init --recursive`. Подробности про окружение есть в [документации Space Station 14](https://docs.spacestation14.com/en/general-development/setup.html).

<div align="center">

## Контрибьют

</div>

Мы рады вкладу в любом виде: код, перевод, спрайты, звук, карты, тесты и документация. Заходите в [Discord](https://discord.gg/mini-station), чтобы обсудить идеи и попросить помощи.

Перед началом стоит заглянуть в [CONTRIBUTING.md](CONTRIBUTING.md), а открытые задачи собраны в [issues](https://github.com/ministation/mini-station-goob/issues).

<div align="center">

## Лицензия

</div>

Весь код в репозитории распространяется под [AGPL-3.0-or-later](LICENSES/AGPL-3.0-or-later.txt), кроме собственного кода Мини-станции: он лицензируется по [лицензионному соглашению Мини-станции](LICENSE.TXT). Файлы содержат заголовки по спецификации REUSE или отдельные `.license` файлы, которые указывают на двойное лицензирование. Это упрощает использование кода в проектах, которые не работают по AGPL.

Большинство медиа-активов лицензированы по [CC-BY-SA 3.0](https://creativecommons.org/licenses/by-sa/3.0/), если в метаданных не указано иное. Часть активов использует некоммерческие лицензии вроде [CC-BY-NC-SA 3.0](https://creativecommons.org/licenses/by-nc-sa/3.0/) или аналогичные: их нужно удалить, если вы планируете использовать проект в коммерческих целях.
