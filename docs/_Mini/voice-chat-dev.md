# Голосовой чат (Mini)

С мерджа апстрима (18.10.2026) Mini использует **апстримный** голосовой чат
(`Content.Goobstation.{Client,Server,Shared}/VoiceChat`): звук захватывается браузерной
страницей (WebAudio worklet), передаётся по WebSocket на сервер и маршрутизируется им по
близости/рациям. Старая Mini-реализация (`Content.Goobstation.Client/Voice`, Lidgren + NAudio
+ Opus) удалена; флаг сборки `-p:MiniVoiceChat=True` и define `VOICECHAT` больше ничего не
включают (оставлены только для совместимости скриптов сборки).

## Включение

- Клиент: **Audio → Voice Chat** (`voice.client_enabled`, по умолчанию `false`). Пока
  выключено, клиент не захватывает микрофон и не проигрывает входящий голос; игрок остаётся
  на TTS. Пока включено - игрок исключается из TTS (голосовой чат **или** TTS).
- Сервер: `voice.enabled = true` + `voice.ws_bind` (по умолчанию loopback `127.0.0.1:1213`)
  и `voice.public_url`. Reverse-proxy должен пробрасывать `/voice/ws` на `voice.ws_bind`;
  пример - в `Resources/ConfigPresets/_Goobstation/goobSigma.toml`.
- Push-to-talk: `N` (локальный голос) и `M` (рация), настраивается в кейбиндах.
