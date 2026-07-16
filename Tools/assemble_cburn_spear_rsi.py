#!/usr/bin/env python3
"""Assemble animated cburn_spear.rsi from numbered frame folders."""

from __future__ import annotations

import json
import shutil
from pathlib import Path

from PIL import Image

ROOT = Path(__file__).resolve().parents[1]
RSI = ROOT / "Resources" / "Textures" / "_Goobstation" / "Objects" / "Weapons" / "Melee" / "cburn_spear.rsi"
SRC = RSI / "cburnspear"

SIZE = 32
ICON_DELAY = 0.1
DIR_DELAY = 0.12


def unpack_dirs(sheet: Image.Image) -> list[Image.Image]:
    """Unpack a 64x64 4-direction sheet into South/North/East/West tiles."""
    if sheet.size != (64, 64):
        raise ValueError(f"expected 64x64 dir sheet, got {sheet.size}")
    tiles: list[Image.Image] = []
    for row in range(2):
        for col in range(2):
            tiles.append(
                sheet.crop((col * SIZE, row * SIZE, (col + 1) * SIZE, (row + 1) * SIZE))
            )
    return tiles


def pack_animated_dirs(frames: list[Image.Image]) -> Image.Image:
    """Pack animation frames as columns and directions as rows (RSI convention)."""
    n = len(frames)
    out = Image.new("RGBA", (n * SIZE, 4 * SIZE), (0, 0, 0, 0))
    for fi, sheet in enumerate(frames):
        for di, tile in enumerate(unpack_dirs(sheet.convert("RGBA"))):
            out.paste(tile, (fi * SIZE, di * SIZE))
    return out


def pack_icon(frames: list[Image.Image]) -> Image.Image:
    n = len(frames)
    out = Image.new("RGBA", (n * SIZE, SIZE), (0, 0, 0, 0))
    for i, frame in enumerate(frames):
        im = frame.convert("RGBA")
        if im.size != (SIZE, SIZE):
            raise ValueError(f"icon frame {i} size {im.size}")
        out.paste(im, (i * SIZE, 0))
    return out


def load_numbered(folder: Path) -> list[Image.Image]:
    paths = sorted(folder.glob("*.png"), key=lambda p: int(p.stem))
    if not paths:
        raise FileNotFoundError(folder)
    return [Image.open(p) for p in paths]


def main() -> int:
    if not SRC.is_dir():
        raise SystemExit(f"missing frame source folder: {SRC}")

    states: list[dict] = []

    icon_frames = load_numbered(SRC / "icon.png")
    pack_icon(icon_frames).save(RSI / "icon.png")
    states.append({"name": "icon", "delays": [[ICON_DELAY] * len(icon_frames)]})

    for name in (
        "inhand-left",
        "inhand-right",
        "wielded-inhand-left",
        "wielded-inhand-right",
        "equipped-SUITSTORAGE",
    ):
        frames = load_numbered(SRC / f"{name}.png")
        pack_animated_dirs(frames).save(RSI / f"{name}.png")
        states.append(
            {
                "name": name,
                "directions": 4,
                "delays": [[DIR_DELAY] * len(frames) for _ in range(4)],
            }
        )

    meta = {
        "version": 1,
        "license": "CC-BY-SA-3.0",
        "copyright": "Sprited by Taro Varne. Animated resprite assembled for Mini/Goob.",
        "size": {"x": 32, "y": 32},
        "states": states,
    }
    (RSI / "meta.json").write_text(json.dumps(meta, indent=2) + "\n", encoding="utf-8")
    shutil.rmtree(SRC)

    print("Assembled RSI:")
    for path in sorted(RSI.iterdir()):
        if path.suffix == ".png":
            print(f"  {path.name} {Image.open(path).size}")
        else:
            print(f"  {path.name}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
