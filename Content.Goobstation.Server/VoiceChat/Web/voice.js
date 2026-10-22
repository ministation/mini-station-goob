"use strict";

const CONFIG = Object.assign({ wsUrl: "", wsPort: 0 }, readConfig());
const TOKEN_KEY = "voice-token";
const SETTINGS_KEY = "voice-settings";
const CLOSE_INVALID_TOKEN = 4001;
const CLOSE_REPLACED = 4002;
const CLOSE_SESSION_ENDED = 4004;
const CLOSE_TOO_MANY_ATTEMPTS = 4005;
const CLOSE_BANNED = 4006;
const CODE_ALPHABET = /[^A-Z0-9]/g;
const CODE_MAX_LENGTH = 12;
const METER_FLOOR_DB = -70;

const DEFAULT_SETTINGS = {
    mode: "ptt",
    threshold: -45,
    gain: 1,
    muted: false,
    effect: "none",
    deviceId: "",
    noiseSuppression: true,
    echoCancellation: true,
    autoGainControl: true,
    keepAwake: true,
};

const el = id => document.getElementById(id);
const ui = {
    status: el("status"),
    statusDetail: el("status-detail"),
    start: el("start"),
    retry: el("retry"),
    codeForm: el("code-form"),
    code: el("code"),
    codeSubmit: el("code-submit"),
    controls: el("controls"),
    indicatorLabel: el("indicator-label"),
    meterFill: el("meter-fill"),
    meterThreshold: el("meter-threshold"),
    modePtt: el("mode-ptt"),
    modeVad: el("mode-vad"),
    thresholdRow: el("threshold-row"),
    threshold: el("threshold"),
    thresholdValue: el("threshold-value"),
    gain: el("gain"),
    gainValue: el("gain-value"),
    mute: el("mute"),
    device: el("device"),
    effectRow: el("effect-row"),
    effect: el("effect"),
    effectDesc: el("effect-desc"),
    noiseSuppression: el("noise-suppression"),
    echoCancellation: el("echo-cancellation"),
    autoGainControl: el("auto-gain-control"),
    keepAwake: el("keep-awake"),
    keepAwakeRow: el("keep-awake-row"),
};

const settings = loadSettings();
let token = takeToken();

let worker = null;
let context = null;
let wakeLock = null;
let starting = false;
let socketReady = false;
let retrying = false;
let stopped = false;
let serverState = null;
let audio = null;
let lastLevel = { db: -100, transmitting: false, voice: false };

function readConfig() {
    try {
        return JSON.parse(document.getElementById("voice-config").textContent) || {};
    } catch {
        return {};
    }
}

function loadSettings() {
    try {
        const stored = JSON.parse(localStorage.getItem(SETTINGS_KEY) || "{}");
        return Object.assign({}, DEFAULT_SETTINGS, stored);
    } catch {
        return Object.assign({}, DEFAULT_SETTINGS);
    }
}

function saveSettings() {
    try {
        localStorage.setItem(SETTINGS_KEY, JSON.stringify(settings));
    } catch {
    }
}

function normalizeCode(code) {
    return String(code || "").toUpperCase().replace(CODE_ALPHABET, "").slice(0, CODE_MAX_LENGTH);
}

function takeToken() {
    const params = new URLSearchParams(location.search);
    const fromLink = normalizeCode(params.get("code") || location.hash.slice(1));
    if (params.has("code") || location.hash) {
        params.delete("code");
        const query = params.toString();
        history.replaceState(null, "", location.pathname + (query ? `?${query}` : ""));
    }

    if (fromLink) {
        rememberToken(fromLink);
        return fromLink;
    }

    try {
        return normalizeCode(localStorage.getItem(TOKEN_KEY));
    } catch {
        return "";
    }
}

function rememberToken(code) {
    try {
        localStorage.setItem(TOKEN_KEY, code);
    } catch {
    }
}

function forgetToken() {
    try {
        localStorage.removeItem(TOKEN_KEY);
    } catch {
    }
}

function setStatus(text, detail, tone) {
    if (ui.status.textContent !== text)
        ui.status.textContent = text;
    if (ui.statusDetail.textContent !== (detail || ""))
        ui.statusDetail.textContent = detail || "";
    document.body.dataset.tone = tone || "neutral";
}

function socketCandidates() {
    if (CONFIG.wsUrl)
        return [CONFIG.wsUrl];

    const sameOrigin = new URL("ws", location.href);
    sameOrigin.protocol = location.protocol === "https:" ? "wss:" : "ws:";
    const list = [sameOrigin.href];

    if (location.protocol === "http:" && CONFIG.wsPort)
        list.push(`ws://${location.hostname}:${CONFIG.wsPort}/voice/ws`);

    return list;
}

function startConnection() {
    stopped = false;
    retrying = false;
    worker.postMessage({ type: "start", candidates: socketCandidates(), token });
    requestWakeLock();
}

function showCodeForm(focus) {
    ui.codeForm.hidden = false;
    ui.start.hidden = true;
    ui.retry.hidden = true;
    ui.code.value = "";
    if (focus)
        ui.code.focus();
}

async function begin() {
    if (starting)
        return;

    starting = true;
    ui.start.hidden = true;
    ui.codeForm.hidden = true;

    if (!audio) {
        setStatus("Запускаем микрофон…", "", "neutral");
        try {
            await startAudio();
        } catch (error) {
            starting = false;
            reportMicError(error);
            return;
        }
    }

    starting = false;
    ui.controls.hidden = false;
    startConnection();
    render();
}

async function requestWakeLock() {
    if (!settings.keepAwake || wakeLock || stopped || !audio || !("wakeLock" in navigator) || document.visibilityState !== "visible")
        return;

    try {
        const lock = await navigator.wakeLock.request("screen");
        if (!settings.keepAwake || stopped) {
            lock.release();
            return;
        }

        wakeLock = lock;
        lock.addEventListener("release", () => {
            if (wakeLock === lock)
                wakeLock = null;
        });
    } catch {
    }
}

function releaseWakeLock() {
    const lock = wakeLock;
    wakeLock = null;
    if (lock)
        lock.release().catch(() => {});
}

function onWorkerMessage(event) {
    const message = event.data;
    switch (message.type) {
        case "connecting":
            socketReady = false;
            break;
        case "ready":
            socketReady = true;
            retrying = false;
            sendEffect();
            break;
        case "state":
            serverState = message.state;
            break;
        case "retrying":
            socketReady = false;
            serverState = null;
            retrying = true;
            setStatus("Сервер голоса недоступен.", `Повтор через ${Math.round(message.delay / 1000)} с.`, "error");
            return;
        case "stopped":
            socketReady = false;
            serverState = null;
            stopped = true;
            releaseWakeLock();
            ui.retry.hidden = message.code !== CLOSE_REPLACED;
            if (message.code === CLOSE_INVALID_TOKEN) {
                forgetToken();
                showCodeForm(false);
                setStatus("Код не подошёл.", "Проверьте код в игре: Esc, «Голосовой чат».", "error");
            }
            else if (message.code === CLOSE_SESSION_ENDED) {
                forgetToken();
                showCodeForm(false);
                setStatus("Вы вышли с игрового сервера.", "Перезайдите и введите новый код.", "error");
            }
            else if (message.code === CLOSE_TOO_MANY_ATTEMPTS) {
                forgetToken();
                showCodeForm(false);
                setStatus("Слишком много неверных кодов.", "Подождите несколько минут и попробуйте снова.", "error");
            }
            else if (message.code === CLOSE_BANNED) {
                forgetToken();
                showCodeForm(false);
                setStatus("Вас забанили в голосовом чате.", "Если вы считаете, что это ошибка, обратитесь к администрации.", "error");
            }
            else if (message.code === CLOSE_REPLACED)
                setStatus("Открыто в другом месте.", "Голосовой чат уже работает в другой вкладке или на другом устройстве.", "error");
            return;
    }

    render();
}

function sendEffect() {
    if (worker && socketReady)
        worker.postMessage({ type: "send", text: JSON.stringify({ t: "effect", effect: settings.effect }) });
}

function renderEffectDescription() {
    const option = ui.effect.selectedOptions[0];
    const desc = option && option.dataset.desc ? option.dataset.desc : "";
    setText(ui.effectDesc, desc);
    ui.effectDesc.hidden = !desc;
}

function pushConfig() {
    if (!audio)
        return;

    audio.node.port.postMessage({
        type: "config",
        mode: settings.mode,
        threshold: settings.threshold,
        gain: settings.gain,
        muted: settings.muted,
    });
}

async function startAudio() {
    if (!context)
        context = new AudioContext();
    const resuming = context.resume();

    const constraints = {
        audio: {
            channelCount: 1,
            noiseSuppression: settings.noiseSuppression,
            echoCancellation: settings.echoCancellation,
            autoGainControl: settings.autoGainControl,
        },
    };

    if (settings.deviceId)
        constraints.audio.deviceId = { exact: settings.deviceId };

    let stream;
    try {
        stream = await navigator.mediaDevices.getUserMedia(constraints);
    } catch (error) {
        if (settings.deviceId && error.name === "OverconstrainedError") {
            settings.deviceId = "";
            saveSettings();
            return startAudio();
        }
        throw error;
    }

    if (!audio)
        await context.audioWorklet.addModule("voice-worklet.js");

    await resuming;

    const source = context.createMediaStreamSource(stream);
    const node = audio ? audio.node : new AudioWorkletNode(context, "voice-capture", {
        numberOfInputs: 1,
        numberOfOutputs: 1,
        outputChannelCount: [1],
        channelCount: 1,
        channelCountMode: "explicit",
    });

    if (!audio) {
        const sink = context.createGain();
        sink.gain.value = 0;
        node.connect(sink).connect(context.destination);
        node.port.onmessage = event => {
            if (event.data.type === "level") {
                lastLevel = event.data;
                renderLevel();
            }
        };

        const channel = new MessageChannel();
        node.port.postMessage({ type: "net-port", port: channel.port1 }, [channel.port1]);
        worker.postMessage({ type: "audio-port", port: channel.port2 }, [channel.port2]);
    }

    source.connect(node);
    audio = { context, node, source, stream };
    pushConfig();
    await refreshDevices();
}

async function restartAudio() {
    if (!audio)
        return;

    audio.source.disconnect();
    audio.stream.getTracks().forEach(track => track.stop());
    try {
        await startAudio();
    } catch (error) {
        reportMicError(error);
    }
}

async function refreshDevices() {
    const devices = (await navigator.mediaDevices.enumerateDevices()).filter(device => device.kind === "audioinput");
    const activeId = audio && audio.stream.getAudioTracks()[0]?.getSettings().deviceId;
    ui.device.replaceChildren();

    for (const [index, device] of devices.entries()) {
        const option = document.createElement("option");
        option.value = device.deviceId;
        option.textContent = device.label || `Микрофон ${index + 1}`;
        option.selected = device.deviceId === (settings.deviceId || activeId);
        ui.device.append(option);
    }
}

function reportMicError(error) {
    const denied = error && (error.name === "NotAllowedError" || error.name === "SecurityError");
    const missing = error && error.name === "NotFoundError";
    if (denied)
        setStatus("Микрофон заблокирован.", "Разрешите доступ в адресной строке и нажмите «Начать».", "error");
    else if (missing)
        setStatus("Микрофон не найден.", "", "error");
    else
        setStatus("Не удалось запустить микрофон.", error && error.message ? error.message : String(error), "error");

    ui.start.hidden = false;
}

function renderLevel() {
    const range = -METER_FLOOR_DB;
    const clamp = value => Math.max(0, Math.min(1, (value - METER_FLOOR_DB) / range));
    ui.meterFill.style.transform = `scaleX(${clamp(lastLevel.db)})`;
    ui.meterThreshold.style.left = `${clamp(settings.threshold) * 100}%`;
    render();
}

function setText(element, text) {
    if (element.textContent !== text)
        element.textContent = text;
}

function syncSlider(input) {
    const min = Number(input.min);
    const max = Number(input.max);
    input.parentElement.style.setProperty("--value", String((Number(input.value) - min) / (max - min)));
}

function render() {
    renderEffectDescription();

    const transmitting = !!lastLevel.transmitting;
    document.body.classList.toggle("transmitting", transmitting);
    ui.thresholdRow.hidden = settings.mode !== "vad";
    ui.effectRow.hidden = !(serverState && serverState.voiceChanger);
    ui.meterThreshold.hidden = settings.mode !== "vad";
    setText(ui.mute, settings.muted ? "Включить" : "Заглушить");
    ui.mute.classList.toggle("negative", settings.muted);
    ui.mute.setAttribute("aria-pressed", String(settings.muted));

    if (!audio)
        setText(ui.indicatorLabel, "Микрофон выключен");
    else if (settings.muted)
        setText(ui.indicatorLabel, "Заглушено");
    else if (transmitting)
        setText(ui.indicatorLabel, "Передача");
    else if (settings.mode === "ptt")
        setText(ui.indicatorLabel, "Удерживайте клавишу в игре");
    else
        setText(ui.indicatorLabel, "Слушаю");

    if (stopped || !audio)
        return;

    if (!socketReady) {
        if (!retrying)
            setStatus("Подключаемся…", "", "neutral");
        return;
    }

    const state = serverState;
    const name = state && state.name ? `Подключено как ${state.name}` : "Подключено";
    if (!state)
        setStatus(name, "", "neutral");
    else if (state.muted)
        setStatus("Заглушено администратором.", "", "error");
    else if (state.selfMuted)
        setStatus(name, "Ваш микрофон заглушён в игре.", "warn");
    else if (state.lobby)
        setStatus(name, state.canSpeak ? "Вы в лобби. Вас слышат все в лобби." : "Голос в лобби отключён.", "neutral");
    else if (!state.inGame)
        setStatus(name, "Войдите в раунд, чтобы говорить.", "neutral");
    else if (!state.canSpeak)
        setStatus(name, "Ваш персонаж сейчас не может говорить.", "warn");
    else if (state.broadcasting)
        setStatus(name, "Трансляция на всю станцию.", "warn");
    else if (state.radio)
        setStatus(name, `Также говорите в рацию «${state.radio}».`, "neutral");
    else
        setStatus(name, "", "neutral");
}

function bindControls() {
    ui.modePtt.checked = settings.mode === "ptt";
    ui.modeVad.checked = settings.mode === "vad";
    ui.threshold.value = settings.threshold;
    ui.gain.value = Math.round(settings.gain * 100);
    syncSlider(ui.threshold);
    syncSlider(ui.gain);
    ui.noiseSuppression.checked = settings.noiseSuppression;
    ui.echoCancellation.checked = settings.echoCancellation;
    ui.autoGainControl.checked = settings.autoGainControl;
    ui.keepAwake.checked = settings.keepAwake;
    ui.keepAwakeRow.hidden = !("wakeLock" in navigator);
    ui.thresholdValue.textContent = `${settings.threshold} dB`;
    ui.gainValue.textContent = `${Math.round(settings.gain * 100)}%`;

    const onModeChange = () => {
        settings.mode = ui.modeVad.checked ? "vad" : "ptt";
        saveSettings();
        pushConfig();
        renderLevel();
    };
    ui.modePtt.addEventListener("change", onModeChange);
    ui.modeVad.addEventListener("change", onModeChange);

    ui.threshold.addEventListener("input", () => {
        syncSlider(ui.threshold);
        settings.threshold = Number(ui.threshold.value);
        ui.thresholdValue.textContent = `${settings.threshold} dB`;
        saveSettings();
        pushConfig();
        renderLevel();
    });

    ui.gain.addEventListener("input", () => {
        syncSlider(ui.gain);
        settings.gain = Number(ui.gain.value) / 100;
        ui.gainValue.textContent = `${ui.gain.value}%`;
        saveSettings();
        pushConfig();
    });

    ui.mute.addEventListener("click", () => {
        settings.muted = !settings.muted;
        saveSettings();
        pushConfig();
        render();
    });

    ui.effect.value = settings.effect;
    renderEffectDescription();
    ui.effect.addEventListener("change", () => {
        settings.effect = ui.effect.value;
        saveSettings();
        renderEffectDescription();
        sendEffect();
    });

    ui.device.addEventListener("change", () => {
        settings.deviceId = ui.device.value;
        saveSettings();
        restartAudio();
    });

    for (const [key, input] of [
        ["noiseSuppression", ui.noiseSuppression],
        ["echoCancellation", ui.echoCancellation],
        ["autoGainControl", ui.autoGainControl],
    ]) {
        input.addEventListener("change", () => {
            settings[key] = input.checked;
            saveSettings();
            restartAudio();
        });
    }

    ui.keepAwake.addEventListener("change", () => {
        settings.keepAwake = ui.keepAwake.checked;
        saveSettings();
        if (settings.keepAwake)
            requestWakeLock();
        else
            releaseWakeLock();
    });

    ui.start.addEventListener("click", begin);

    ui.code.addEventListener("input", () => {
        const normalized = normalizeCode(ui.code.value);
        if (ui.code.value !== normalized)
            ui.code.value = normalized;
    });

    ui.codeForm.addEventListener("submit", event => {
        event.preventDefault();
        const code = normalizeCode(ui.code.value);
        if (!code) {
            ui.code.focus();
            return;
        }

        token = code;
        rememberToken(code);
        ui.code.blur();
        begin();
    });

    document.addEventListener("visibilitychange", () => {
        if (document.visibilityState === "visible")
            requestWakeLock();
    });

    ui.retry.addEventListener("click", () => {
        ui.retry.hidden = true;
        startConnection();
        render();
    });

    if (navigator.mediaDevices)
        navigator.mediaDevices.addEventListener("devicechange", () => audio && refreshDevices());
}

function init() {
    bindControls();
    renderLevel();

    if (!window.isSecureContext || !navigator.mediaDevices || !window.AudioWorkletNode) {
        setStatus("Микрофону нужен HTTPS.", "Страница должна открываться по HTTPS или с localhost.", "error");
        ui.start.hidden = true;
        return;
    }

    worker = new Worker("voice-worker.js");
    worker.onmessage = onWorkerMessage;

    if (!token) {
        setStatus("Введите код голосового чата.", "В игре: Esc, «Голосовой чат».", "neutral");
        showCodeForm(true);
        return;
    }

    setStatus("Готово", "", "neutral");
}

init();
