const OUTPUT_RATE = 16000;
const FRAME_SAMPLES = 320;
const FRAME_BYTES = 3 + FRAME_SAMPLES / 2;
const PREROLL_FRAMES = 5;
const VAD_HANGOVER_FRAMES = 20;
const LEVEL_REPORT_FRAMES = 3;
const FLAG_TRANSMISSION_START = 1;

const FRAME_SECONDS = FRAME_SAMPLES / OUTPUT_RATE;
const HIGH_PASS_HZ = 90;
const LIMITER_CEILING = 0.89;
const LIMITER_RELEASE_SECONDS = 0.15;
const PRE_EMPHASIS = 0.9;
const GATE_CLOSED_GAIN = 0.03;
const GATE_ATTACK_SECONDS = 0.003;
const GATE_RELEASE_SECONDS = 0.12;
const GATE_HOLD_FRAMES = 15;
const GATE_OPEN_MARGIN_DB = 9;
const GATE_KEEP_MARGIN_DB = 5;
const GATE_BLOCK_SAMPLES = 64;
const GATE_BLOCKS = FRAME_SAMPLES / GATE_BLOCK_SAMPLES;
const GATE_SUSTAINED_BLOCKS = 3;
const GATE_ALWAYS_OPEN_DB = -26;
const FLOOR_MIN_DB = -85;
const FLOOR_MAX_DB = -38;
const FLOOR_FALL = 0.25;
const FLOOR_RISE_DB_PER_SECOND = 0.5;
const FLOOR_STEADY_RISE_DB_PER_SECOND = 8;
const STEADY_RANGE_DB = 3;
const STEADY_FRAMES = 25;

const INDEX_TABLE = [-1, -1, -1, -1, 2, 4, 6, 8, -1, -1, -1, -1, 2, 4, 6, 8];
const STEP_TABLE = [
    7, 8, 9, 10, 11, 12, 13, 14, 16, 17,
    19, 21, 23, 25, 28, 31, 34, 37, 41, 45,
    50, 55, 60, 66, 73, 80, 88, 97, 107, 118,
    130, 143, 157, 173, 190, 209, 230, 253, 279, 307,
    337, 371, 408, 449, 494, 544, 598, 658, 724, 796,
    876, 963, 1060, 1166, 1282, 1411, 1552, 1707, 1878, 2066,
    2272, 2499, 2749, 3024, 3327, 3660, 4026, 4428, 4871, 5358,
    5894, 6484, 7132, 7845, 8630, 9493, 10442, 11487, 12635, 13899,
    15289, 16818, 18500, 20350, 22385, 24623, 27086, 29794, 32767,
];

class Resampler {
    constructor(inputRate, outputRate) {
        this.passthrough = inputRate === outputRate;
        this.step = inputRate / outputRate;
        this.buffer = new Float32Array(8192);
        this.length = 0;
        this.position = 0;

        if (this.passthrough)
            return;

        const cutoff = Math.min(0.5, 0.45 / this.step);
        this.halfWidth = Math.ceil(8 / (2 * cutoff));
        this.resolution = 64;
        this.table = new Float32Array(this.halfWidth * this.resolution + 2);
        for (let i = 0; i < this.table.length; i++) {
            const x = i / this.resolution;
            if (x >= this.halfWidth)
                continue;

            const arg = 2 * cutoff * x;
            const sinc = arg === 0 ? 1 : Math.sin(Math.PI * arg) / (Math.PI * arg);
            const u = x / this.halfWidth;
            const window = 0.42 + 0.5 * Math.cos(Math.PI * u) + 0.08 * Math.cos(2 * Math.PI * u);
            this.table[i] = 2 * cutoff * sinc * window;
        }

        this.position = this.halfWidth;
    }

    kernel(x) {
        const ax = Math.abs(x) * this.resolution;
        const index = ax | 0;
        if (index + 1 >= this.table.length)
            return 0;

        const frac = ax - index;
        return this.table[index] + (this.table[index + 1] - this.table[index]) * frac;
    }

    push(input, emit) {
        if (this.passthrough) {
            for (let i = 0; i < input.length; i++)
                emit(input[i]);
            return;
        }

        if (this.length + input.length > this.buffer.length) {
            const grown = new Float32Array((this.length + input.length) * 2);
            grown.set(this.buffer.subarray(0, this.length));
            this.buffer = grown;
        }

        this.buffer.set(input, this.length);
        this.length += input.length;

        while (this.position + this.halfWidth < this.length) {
            const center = Math.floor(this.position);
            const frac = this.position - center;
            let sum = 0;
            for (let k = -this.halfWidth + 1; k <= this.halfWidth; k++)
                sum += this.buffer[center + k] * this.kernel(k - frac);

            emit(sum);
            this.position += this.step;
        }

        const discard = Math.floor(this.position) - this.halfWidth;
        if (discard > 0) {
            this.buffer.copyWithin(0, discard, this.length);
            this.length -= discard;
            this.position -= discard;
        }
    }
}

// Срез гула и низкочастотного шума: Баттерворт 2-го порядка.
class HighPassFilter {
    constructor(cutoff, rate) {
        const w = 2 * Math.PI * cutoff / rate;
        const alpha = Math.sin(w) / Math.SQRT2;
        const norm = 1 + alpha;
        this.b0 = (1 + Math.cos(w)) / 2 / norm;
        this.b1 = -2 * this.b0;
        this.a1 = -2 * Math.cos(w) / norm;
        this.a2 = (1 - alpha) / norm;
        this.z1 = 0;
        this.z2 = 0;
    }

    process(sample) {
        const output = this.b0 * sample + this.z1;
        this.z1 = this.b1 * sample - this.a1 * output + this.z2;
        this.z2 = this.b0 * sample - this.a2 * output;
        return output;
    }
}

// Плавно убирает громкость на пиках вместо жёсткого среза, из-за которого голос хрипел.
class Limiter {
    constructor(rate) {
        this.gain = 1;
        this.release = 1 - Math.exp(-1 / (LIMITER_RELEASE_SECONDS * rate));
    }

    process(sample) {
        const magnitude = Math.abs(sample);
        const target = magnitude > LIMITER_CEILING ? LIMITER_CEILING / magnitude : 1;
        this.gain = Math.min(target, this.gain + (1 - this.gain) * this.release);
        return sample * this.gain;
    }
}

// Следит за уровнем фонового шума и глушит всё, что заметно не громче него.
// Короткий всплеск (щелчок клавиши) голосом не считается: звук должен занимать
// большую часть кадра и держаться два кадра подряд.
class NoiseGate {
    constructor(rate) {
        this.floor = null;
        this.average = 0;
        this.steady = 0;
        this.loud = false;
        this.hold = 0;
        this.gain = 1;
        this.attack = 1 - Math.exp(-1 / (GATE_ATTACK_SECONDS * rate));
        this.release = 1 - Math.exp(-1 / (GATE_RELEASE_SECONDS * rate));
    }

    get open() {
        return this.hold > 0;
    }

    update(db, levelDb, blocks) {
        this.trackFloor(db);

        const keepDb = this.floor + GATE_KEEP_MARGIN_DB;
        const blockLimit = Math.pow(10, keepDb / 10) * GATE_BLOCK_SAMPLES;
        let active = 0;
        for (let i = 0; i < GATE_BLOCKS; i++) {
            if (blocks[i] >= blockLimit)
                active++;
        }

        const certain = levelDb >= GATE_ALWAYS_OPEN_DB;
        const sustained = active >= GATE_SUSTAINED_BLOCKS;
        const wasLoud = this.loud;
        this.loud = certain || sustained && db >= this.floor + GATE_OPEN_MARGIN_DB;
        if (this.loud && wasLoud || this.open && (certain || sustained && db >= keepDb))
            this.hold = GATE_HOLD_FRAMES;
        else if (this.hold > 0)
            this.hold--;
    }

    trackFloor(db) {
        const level = Math.max(db, FLOOR_MIN_DB);
        if (this.floor === null) {
            this.floor = Math.min(level, FLOOR_MAX_DB);
            this.average = level;
            return;
        }

        this.steady = Math.abs(level - this.average) < STEADY_RANGE_DB ? this.steady + 1 : 0;
        this.average += (level - this.average) * 0.2;

        if (level < this.floor) {
            this.floor += (level - this.floor) * FLOOR_FALL;
            return;
        }

        const rise = (this.steady >= STEADY_FRAMES ? FLOOR_STEADY_RISE_DB_PER_SECOND : FLOOR_RISE_DB_PER_SECOND) * FRAME_SECONDS;
        this.floor = Math.min(this.floor + rise, level, FLOOR_MAX_DB);
    }

    apply(samples, enabled) {
        const target = !enabled || this.open ? 1 : GATE_CLOSED_GAIN;
        const coefficient = target > this.gain ? this.attack : this.release;
        for (let i = 0; i < samples.length; i++) {
            this.gain += (target - this.gain) * coefficient;
            samples[i] *= this.gain;
        }
    }
}

class AdpcmEncoder {
    constructor() {
        this.predictor = 0;
        this.index = 0;
    }

    encode(samples) {
        const out = new Uint8Array(FRAME_BYTES);
        out[0] = this.predictor & 0xff;
        out[1] = (this.predictor >> 8) & 0xff;
        out[2] = this.index;

        for (let i = 0; i < samples.length; i++) {
            let step = STEP_TABLE[this.index];
            let diff = samples[i] - this.predictor;
            let nibble = 0;
            if (diff < 0) {
                nibble = 8;
                diff = -diff;
            }

            let delta = step >> 3;
            if (diff >= step) {
                nibble |= 4;
                diff -= step;
                delta += step;
            }
            step >>= 1;
            if (diff >= step) {
                nibble |= 2;
                diff -= step;
                delta += step;
            }
            step >>= 1;
            if (diff >= step) {
                nibble |= 1;
                delta += step;
            }

            this.predictor += (nibble & 8) ? -delta : delta;
            if (this.predictor > 32767)
                this.predictor = 32767;
            else if (this.predictor < -32768)
                this.predictor = -32768;

            this.index += INDEX_TABLE[nibble];
            if (this.index < 0)
                this.index = 0;
            else if (this.index > 88)
                this.index = 88;

            const byte = 3 + (i >> 1);
            out[byte] |= (i & 1) === 0 ? nibble : nibble << 4;
        }

        return out;
    }
}

class VoiceCaptureProcessor extends AudioWorkletProcessor {
    constructor() {
        super();
        this.resampler = new Resampler(sampleRate, OUTPUT_RATE);
        this.encoder = new AdpcmEncoder();
        this.highPass = new HighPassFilter(HIGH_PASS_HZ, OUTPUT_RATE);
        this.limiter = new Limiter(OUTPUT_RATE);
        this.gate = new NoiseGate(OUTPUT_RATE);
        this.lastValue = 0;
        this.blocks = new Float64Array(GATE_BLOCKS);
        this.frame = new Float32Array(FRAME_SAMPLES);
        this.current = new Float32Array(FRAME_SAMPLES);
        this.delayed = new Float32Array(FRAME_SAMPLES);
        this.pcm = new Int16Array(FRAME_SAMPLES);
        this.frameLength = 0;
        this.preroll = [];
        this.transmitting = false;
        this.wasWanted = false;
        this.vadOpen = false;
        this.vadHang = 0;
        this.levelCounter = 0;
        this.peakDb = -100;
        this.mode = "ptt";
        this.threshold = -45;
        this.gain = 1;
        this.muted = false;
        this.noiseGate = true;
        this.allowed = false;
        this.pushToTalk = false;
        this.net = null;
        this.emit = this.emit.bind(this);
        this.port.onmessage = event => this.configure(event.data);
    }

    configure(message) {
        if (message.type === "net-port") {
            this.net = message.port;
            this.net.onmessage = event => this.configure(event.data);
            return;
        }

        if (message.type !== "config")
            return;

        for (const key of ["mode", "threshold", "gain", "muted", "noiseGate", "allowed", "pushToTalk"]) {
            if (key in message)
                this[key] = message[key];
        }
    }

    emit(sample) {
        this.frame[this.frameLength++] = sample;
        if (this.frameLength === FRAME_SAMPLES) {
            this.frameLength = 0;
            this.processFrame();
        }
    }

    processFrame() {
        let energy = 0;
        let tilted = 0;
        this.blocks.fill(0);
        for (let i = 0; i < FRAME_SAMPLES; i++) {
            const value = this.limiter.process(this.highPass.process(this.frame[i] * this.gain));
            const difference = value - PRE_EMPHASIS * this.lastValue;
            this.lastValue = value;

            energy += value * value;
            tilted += difference * difference;
            this.blocks[(i / GATE_BLOCK_SAMPLES) | 0] += difference * difference;
            this.current[i] = value;
        }

        const db = this.toDb(energy);
        this.peakDb = Math.max(this.peakDb, db);
        this.gate.update(this.toDb(tilted), db, this.blocks);

        // В эфир уходит предыдущий кадр: так шумодав знает, что будет дальше,
        // и не съедает начало слова.
        this.gate.apply(this.delayed, this.noiseGate);
        for (let i = 0; i < FRAME_SAMPLES; i++) {
            this.pcm[i] = Math.round(this.delayed[i] * 32767);
        }

        const buffer = this.delayed;
        this.delayed = this.current;
        this.current = buffer;

        if (db >= this.threshold && (!this.noiseGate || this.gate.open)) {
            this.vadOpen = true;
            this.vadHang = VAD_HANGOVER_FRAMES;
        } else if (this.vadHang > 0) {
            this.vadHang--;
        } else {
            this.vadOpen = false;
        }

        const encoded = this.encoder.encode(this.pcm);
        const wanted = this.mode === "vad" ? this.vadOpen : this.pushToTalk;
        const transmitting = (wanted || this.wasWanted) && this.allowed && !this.muted;
        this.wasWanted = wanted;

        if (transmitting) {
            if (!this.transmitting) {
                let first = true;
                for (const frame of this.preroll) {
                    this.send(frame, first ? FLAG_TRANSMISSION_START : 0);
                    first = false;
                }
                this.preroll = [];
                this.send(encoded, first ? FLAG_TRANSMISSION_START : 0);
            } else {
                this.send(encoded, 0);
            }
        } else {
            this.preroll.push(encoded);
            if (this.preroll.length > PREROLL_FRAMES)
                this.preroll.shift();
        }

        if (transmitting !== this.transmitting) {
            this.transmitting = transmitting;
            this.reportLevel();
        }

        if (++this.levelCounter >= LEVEL_REPORT_FRAMES)
            this.reportLevel();
    }

    toDb(energy) {
        const rms = Math.sqrt(energy / FRAME_SAMPLES);
        return rms > 0 ? 20 * Math.log10(rms) : -100;
    }

    reportLevel() {
        this.levelCounter = 0;
        this.port.postMessage({
            type: "level",
            db: this.peakDb,
            transmitting: this.transmitting,
            voice: this.vadOpen,
        });
        this.peakDb = -100;
    }

    send(payload, flags) {
        if (this.net)
            this.net.postMessage({ flags, payload: payload.buffer }, [payload.buffer]);
    }

    process(inputs) {
        const input = inputs[0];
        if (input && input.length > 0 && input[0].length > 0)
            this.resampler.push(input[0], this.emit);

        return true;
    }
}

registerProcessor("voice-capture", VoiceCaptureProcessor);
