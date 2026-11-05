#!/usr/bin/env python3
# SPDX-FileCopyrightText: 2026 Space Station 14 Contributors
#
# SPDX-License-Identifier: AGPL-3.0-or-later
"""Generate the azure water RSI from the regular water sprites.

Лазурная вода (FloorAzureWaterEntity) сделана «на основе обычной»: тот же
RSI-набор, что у liquid-water.rsi (стейты water0..7 + full, 4 направления
и 4 кадра анимации, тот же порядок corner-стейтов), только пиксели воды
перекрашены в лазурную палитру. Берег/суша остаются как у обычной воды.

Перекраска повторяет оттенок старого Corvax-спрайта лазурной воды
(hue ≈ 182°, value ≈ 0.72): оттенок воды сводится к 182°, соседние
оттенки сжимаются, яркость повышается.

Запускать из корня репозитория:
    python Tools/generate_azure_water_rsi.py
"""

from __future__ import annotations

import json
from pathlib import Path

import numpy as np
from PIL import Image

ROOT = Path(__file__).resolve().parents[1]
SRC_DIR = ROOT / "Resources" / "Textures" / "Tiles" / "TileEntities" / "liquid-water.rsi"
DST_DIR = (
    ROOT
    / "Resources"
    / "Textures"
    / "_Mini"
    / "Tiles"
    / "TileEntities"
    / "liquid-azure-water.rsi"
)

# Перекраска: оттенок воды (deg) сводится к HUE_CENTER, яркость повышается.
HUE_CENTER = 182.0
HUE_CENTER_SRC = 202.0
HUE_SCALE = 0.7
SAT_SCALE = 1.0
VAL_SCALE = 1.3
VAL_MAX = 0.92
# Пиксели, которые считаем водой (синие/голубые), а не берегом.
WATER_HUE_MIN = 160.0
WATER_HUE_MAX = 250.0
WATER_SAT_MIN = 0.12

COPYRIGHT = (
    "Created by vladimir.s (CC-BY-SA-4.0); azure recolor for Mini Station, "
    "derived from Tiles/TileEntities/liquid-water.rsi"
)


def hsv_to_array(rgb: np.ndarray) -> tuple[np.ndarray, np.ndarray, np.ndarray]:
    r, g, b = rgb[..., 0], rgb[..., 1], rgb[..., 2]
    mx = np.maximum(np.maximum(r, g), b) / 255.0
    mn = np.minimum(np.minimum(r, g), b) / 255.0
    diff = mx - mn
    v = mx
    s = np.where(mx > 0, diff / np.maximum(mx, 1e-6), 0)
    h = np.zeros_like(mx)
    mask = diff > 1e-6
    rc = (mx - r / 255.0) / np.maximum(diff, 1e-6)
    gc = (mx - g / 255.0) / np.maximum(diff, 1e-6)
    bc = (mx - b / 255.0) / np.maximum(diff, 1e-6)
    h = np.where((r / 255.0 >= mx) & mask, bc - gc, h)
    h = np.where((g / 255.0 >= mx) & (r / 255.0 < mx) & mask, 2.0 + rc - bc, h)
    h = np.where((b / 255.0 >= mx) & (g / 255.0 < mx) & mask, 4.0 + gc - rc, h)
    return (h / 6.0) % 1.0, s, v


def array_to_rgb(h: np.ndarray, s: np.ndarray, v: np.ndarray) -> np.ndarray:
    i = (h * 6.0).astype(np.int32) % 6
    f = h * 6.0 - np.floor(h * 6.0)
    p = v * (1 - s)
    q = v * (1 - f * s)
    t = v * (1 - (1 - f) * s)
    r = np.choose(i, [v, q, p, p, t, v])
    g = np.choose(i, [t, v, v, q, p, p])
    b = np.choose(i, [p, p, t, v, v, q])
    return np.stack([r, g, b], axis=-1) * 255.0


def recolor(image: Image.Image) -> Image.Image:
    data = np.asarray(image.convert("RGBA")).astype(np.float32)
    out = data.copy()
    h, s, v = hsv_to_array(data[..., :3])
    deg = h * 360.0
    water = (
        (data[..., 3] > 0)
        & (s > WATER_SAT_MIN)
        & (deg >= WATER_HUE_MIN)
        & (deg <= WATER_HUE_MAX)
    )
    new_h = (HUE_CENTER + (deg - HUE_CENTER_SRC) * HUE_SCALE) / 360.0
    new_s = np.clip(s * SAT_SCALE, 0, 1)
    new_v = np.clip(v * VAL_SCALE, 0, VAL_MAX)
    recolored = array_to_rgb(new_h, new_s, new_v)
    out[..., :3] = np.where(water[..., None], recolored, out[..., :3])
    return Image.fromarray(np.clip(out, 0, 255).astype(np.uint8))


def main() -> None:
    DST_DIR.mkdir(parents=True, exist_ok=True)
    for path in sorted(SRC_DIR.glob("*.png")):
        recolor(Image.open(path)).save(DST_DIR / path.name)
        print(f"recolored {path.name}")

    meta = json.loads((SRC_DIR / "meta.json").read_text(encoding="utf-8"))
    meta["license"] = "CC-BY-SA-4.0"
    meta["copyright"] = COPYRIGHT
    (DST_DIR / "meta.json").write_text(
        json.dumps(meta, ensure_ascii=False, indent=2) + "\n",
        encoding="utf-8",
    )
    print(f"meta written to {DST_DIR / 'meta.json'}")


if __name__ == "__main__":
    main()
