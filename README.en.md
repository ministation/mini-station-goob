<h1 align="center">Mini Station</h1>

<p align="center">
  <b>A Russian-language fork of <a href="https://github.com/Goob-Station/Goob-Station">Goob Station</a> for <a href="https://spacestation14.com/">Space Station 14</a>.</b><br>
  We translate the game into Russian, add our own content, and run our own server.
</p>

<p align="center">
  <a href="README.md">Русский</a> | <b>English</b>
</p>

<p align="center">
  <a href="https://cdn.ministation.ru/fork/ministation"><img alt="Download build" src="https://img.shields.io/badge/Download_build-Windows_|_Linux_|_macOS-ffb020?style=for-the-badge&logo=windows&logoColor=white"></a>
  <a href="https://discord.gg/mini-station"><img alt="Discord" src="https://img.shields.io/badge/dynamic/json?url=https%3A%2F%2Fdiscord.com%2Fapi%2Fv10%2Finvites%2Fmini-station%3Fwith_counts%3Dtrue&query=%24.approximate_member_count&label=Discord&style=for-the-badge&logo=discord&logoColor=white&color=5865F2"></a>
  <a href="https://t.me/mini_station"><img alt="Telegram" src="https://img.shields.io/badge/Telegram-@mini__station-26A5E4?style=for-the-badge&logo=telegram&logoColor=white"></a>
</p>

<p align="center">
  <a href="https://ministation.ru"><img alt="Website" src="https://img.shields.io/badge/Website-ministation.ru-ffb020?style=flat-square&logo=firefoxbrowser&logoColor=white"></a>
  <a href="https://wiki.ministation.ru"><img alt="Wiki" src="https://img.shields.io/badge/Wiki-wiki.ministation.ru-9b6dff?style=flat-square&logo=gitbook&logoColor=white"></a>
  <a href="https://ministation.ru/donate"><img alt="Donate" src="https://img.shields.io/badge/Donate-support_the_project-ff5e5b?style=flat-square&logo=buymeacoffee&logoColor=white"></a>
  <a href="https://github.com/deepseek-ai/deepseek-harness"><img alt="powered by dsh" src="https://img.shields.io/badge/powered_by-dsh-4D6BFE?style=flat-square&logo=deepseek&logoColor=white"></a>
  <a href="https://github.com/ministation/mini-station-goob/actions/workflows/validate.yml"><img alt="CI" src="https://img.shields.io/github/actions/workflow/status/ministation/mini-station-goob/validate.yml?branch=master&style=flat-square&label=CI&color=12b886"></a>
  <a href="LICENSE.TXT"><img alt="License" src="https://img.shields.io/badge/license-AGPL--3.0--or--later-gold?style=flat-square"></a>
</p>

---

<div align="center">

## About

</div>

**Mini Station** is a non-commercial Russian-language Space Station 14 project. We maintain our own build: we translate content, add new mechanics and items, adjust balance, and run a server for our community.

The build is based on [Goob Station](https://github.com/Goob-Station/Goob-Station) and runs on the [RobustToolbox](https://github.com/space-wizards/RobustToolbox) engine.

- **Russian language.** Game localization, wiki, and community communication.
- **Original content.** Daily and weekly quests, text-to-speech (TTS), new items, roles, and events.
- **Ready builds.** Windows, Linux, and macOS builds are updated every night.
- **AI in the project.** We deeply integrate artificial intelligence: it helps us develop, translate, and maintain the server.

<div align="center">

## Building from source

</div>

You will need [.NET SDK 10.0.100+](https://dotnet.microsoft.com/download), Python 3.5+, and Git.

```shell
git clone https://github.com/ministation/mini-station-goob.git
cd mini-station-goob
python RUN_THIS.py
```

`RUN_THIS.py` downloads the engine and submodules. On Linux and macOS the command may be `python3`.

**Windows**

```bat
cd Scripts\bat
runQuickAll-Debug.bat
```

**Linux and macOS**

```shell
sh Scripts/sh/buildAllDebug.sh
sh Scripts/sh/runQuickAll.sh
```

Both options build the game and launch a local server with a client. After launch, connect to `localhost` in the client window.

> [!TIP]
> If `RUN_THIS.py` fails to download the engine, run `git submodule update --init --recursive`. See the [Space Station 14 documentation](https://docs.spacestation14.com/en/general-development/setup.html) for environment details.

<div align="center">

## Contributing

</div>

We welcome contributions of any kind: code, translation, sprites, sound, maps, tests, and documentation. Join our [Discord](https://discord.gg/mini-station) to discuss ideas and ask for help.

Before you start, check [CONTRIBUTING.md](CONTRIBUTING.md); open tasks are listed in [issues](https://github.com/ministation/mini-station-goob/issues).

<div align="center">

## License

</div>

All code in the repository is licensed under [AGPL-3.0-or-later](LICENSES/AGPL-3.0-or-later.txt), except original Mini Station code, which is licensed under the [Mini Station license agreement](LICENSE.TXT). Files contain REUSE headers or separate `.license` files that indicate dual licensing. This makes it easier to use the code in projects that do not use AGPL.

Most media assets are licensed under [CC-BY-SA 3.0](https://creativecommons.org/licenses/by-sa/3.0/) unless stated otherwise in the metadata. Some assets use non-commercial licenses such as [CC-BY-NC-SA 3.0](https://creativecommons.org/licenses/by-nc-sa/3.0/) or similar; they must be removed if you plan to use the project commercially.
