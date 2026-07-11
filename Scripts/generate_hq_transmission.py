"""Generate a short tactical HQ incoming-transmission sting for Typan Station War."""
from __future__ import annotations

import math
import random
import struct
import wave
from pathlib import Path

SAMPLE_RATE = 44100
OUTPUT = Path(__file__).resolve().parents[1] / "Resources/Audio/_Mini/TypanWar/hq_transmission.wav"

# Three-note command chime: calm, not alarm.
NOTES = [
    (0.06, 740.0, 0.07),
    (0.16, 880.0, 0.07),
    (0.26, 1046.0, 0.09),
]


def envelope(t: float, start: float, length: float, attack: float = 0.012, release: float = 0.025) -> float:
    local = t - start
    if local < 0 or local > length:
        return 0.0
    if local < attack:
        return local / attack
    if local > length - release:
        return max(0.0, (length - local) / release)
    return 1.0


def radio_tone(t: float, freq: float, start: float, length: float, gain: float = 0.28) -> float:
    env = envelope(t, start, length)
    if env <= 0:
        return 0.0
    wobble = 1.0 + 0.004 * math.sin(2 * math.pi * 6.5 * t)
    tone = math.sin(2 * math.pi * freq * wobble * t)
    harmonic = 0.18 * math.sin(2 * math.pi * freq * 2.0 * t)
    return gain * env * (tone + harmonic)


def radio_open(t: float) -> float:
    if t < 0.02 or t > 0.05:
        return 0.0
    env = min(1.0, (t - 0.02) / 0.006) * min(1.0, (0.05 - t) / 0.012)
    noise = random.uniform(-1.0, 1.0)
    hum = 0.08 * math.sin(2 * math.pi * 120.0 * t)
    return env * (noise * 0.22 + hum)


def carrier(t: float) -> float:
    if t < 0.04 or t > 0.42:
        return 0.0
    fade = min(1.0, (t - 0.04) / 0.03) * min(1.0, (0.42 - t) / 0.08)
    return fade * 0.03 * math.sin(2 * math.pi * 95.0 * t)


def close_tail(t: float) -> float:
    if t < 0.37 or t > 0.48:
        return 0.0
    env = min(1.0, (t - 0.37) / 0.01) * min(1.0, (0.48 - t) / 0.06)
    return env * random.uniform(-1.0, 1.0) * 0.08


def main() -> None:
    random.seed(42)
    duration = 0.52
    frames = int(SAMPLE_RATE * duration)
    samples: list[int] = []

    for i in range(frames):
        t = i / SAMPLE_RATE
        value = radio_open(t) + carrier(t) + close_tail(t)
        for start, freq, length in NOTES:
            value += radio_tone(t, freq, start, length)

        value = max(-1.0, min(1.0, value))
        samples.append(int(value * 32767))

    OUTPUT.parent.mkdir(parents=True, exist_ok=True)
    with wave.open(str(OUTPUT), "wb") as wf:
        wf.setnchannels(1)
        wf.setsampwidth(2)
        wf.setframerate(SAMPLE_RATE)
        wf.writeframes(struct.pack(f"<{len(samples)}h", *samples))

    print(f"Wrote {OUTPUT} ({len(samples) / SAMPLE_RATE:.2f}s)")


if __name__ == "__main__":
    main()
