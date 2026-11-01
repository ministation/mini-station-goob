#!/usr/bin/env python3
# SPDX-License-Identifier: AGPL-3.0-or-later
"""Convert a BYOND .dmi file into an SS14 .rsi folder.

Extracts only the requested states (or all 1-direction states when none given).
Directional states are refused (BYOND and SS14 direction orders differ),
because every sprite we port with this tool is single-direction.

Usage:
    python dmi_to_rsi.py <input.dmi> <output.rsi> [state1,state2,...]

The output meta.json gets CC-BY-SA-3.0 + tgstation attribution; edit copyright
afterwards if the source is not tgstation.
"""

import json
import sys
import zlib
from pathlib import Path

from PIL import Image


def parse_dmi(path: Path):
    data = path.read_bytes()
    metadata = None
    header_size = None
    if data[0:4] == b"ver ":
        # Classic BYOND DMI: version header + "DESC" chunks; canvas size lives in the header.
        header_size = (int.from_bytes(data[4:6], "little"), int.from_bytes(data[6:8], "little"))
        pos = 8
        while pos + 8 <= len(data):
            name = data[pos:pos + 4]
            length = int.from_bytes(data[pos + 4:pos + 8], "big")
            payload = data[pos + 8:pos + 8 + length]
            pos += 8 + length
            if name == b"DESC":
                metadata = zlib.decompress(payload).decode("utf-8")
                break
        if metadata is None:
            raise ValueError("DMI has no DESC chunk")
    else:
        # Modern .dmi: plain PNG with a zTXt "Description" chunk holding the metadata.
        image_probe = Image.open(path)
        width, height = image_probe.size
        for key, value in image_probe.text.items():
            if key.lower() == "description":
                try:
                    metadata = value if isinstance(value, str) else zlib.decompress(value).decode("utf-8")
                except zlib.error:
                    metadata = zlib.decompress(value.encode("latin-1")).decode("utf-8")
                break
        if metadata is None:
            raise ValueError("PNG has no Description zTXt chunk")

    if metadata is None:
        raise ValueError("sprite has no DMI description")

    # Unified key/value scan: keys are separated by ";" (classic) or newlines (PNG-based),
    # "state" starts a new state, earlier keys belong to the header.
    width = height = None
    states = []
    current = None
    for chunk in metadata.replace(";", "\n").split("\n"):
        chunk = chunk.strip()
        if not chunk or "=" not in chunk:
            continue
        key, _, value = chunk.partition("=")
        key = key.strip()
        value = value.strip().strip('"')
        if key == "state":
            current = {
                "name": value,
                "dirs": 1,
                "frames": 1,
                "delays": [0.1],
            }
            states.append(current)
        elif current is None:
            if key == "width":
                width = int(value)
            elif key == "height":
                height = int(value)
        else:
            if key == "dirs":
                current["dirs"] = int(value)
            elif key == "frames":
                current["frames"] = int(value)
                current["delays"] = [0.1] * current["frames"]
            elif key == "delay":
                current["delays"] = [float(x) for x in value.split(",") if x.strip()]

    if width is None and header_size is not None:
        width, height = header_size
    if width is None or height is None:
        raise ValueError("DMI description has no width/height")
    return width, height, states


def convert(input_path: Path, output_path: Path, wanted: list[str] | None):
    width, height, states = parse_dmi(input_path)
    image = Image.open(input_path)

    if wanted is None:
        wanted = [s["name"] for s in states if s["dirs"] == 1]

    known = {s["name"] for s in states}
    if missing := [w for w in wanted if w not in known]:
        raise SystemExit(f"states not found in {input_path.name}: {', '.join(missing)}")

    rows_total = image.height // height
    cols_total = image.width // width
    cells_capacity = rows_total * cols_total
    cells_needed = sum(s["dirs"] * s["frames"] for s in states)
    if cells_needed > cells_capacity:
        raise SystemExit(
            f"sheet too small: {cells_needed} cells needed vs {cells_capacity} available"
        )

    output_path.mkdir(parents=True, exist_ok=True)
    meta_states = []
    cell_index = 0
    for state in states:
        cells = state["dirs"] * state["frames"]
        if state["name"] in wanted:
            if state["dirs"] != 1:
                raise SystemExit(
                    f"state {state['name']} is directional ({state['dirs']} dirs) - refusing, "
                    "extend the tool with direction remapping first"
                )

            frames_png = Image.new("RGBA", (width * state["frames"], height), (0, 0, 0, 0))
            for frame in range(state["frames"]):
                index = cell_index + frame
                x = (index % cols_total) * width
                y = (index // cols_total) * height
                cell = image.crop((x, y, x + width, y + height))
                frames_png.paste(cell, (frame * width, 0))

            frames_png.save(output_path / f"{state['name']}.png")
            meta_states.append({
                "name": state["name"],
                "delays": state["delays"],
            })
        cell_index += cells

    meta = {
        "version": 1,
        "license": "CC-BY-SA-3.0",
        "copyright": "Taken from tgstation (https://github.com/tgstation/tgstation) at commit 3435f7c8b0b.",
        "size": {"x": width, "y": height},
        "states": meta_states,
    }
    (output_path / "meta.json").write_text(
        json.dumps(meta, indent=2, ensure_ascii=False), encoding="utf-8", newline="\n"
    )
    print(f"{input_path.name} -> {output_path} ({len(meta_states)} states, {width}x{height})")


def main():
    if len(sys.argv) < 3:
        raise SystemExit(__doc__)
    input_path = Path(sys.argv[1])
    output_path = Path(sys.argv[2])
    wanted = None
    if len(sys.argv) > 3:
        wanted = [s.strip() for s in sys.argv[3].split(",") if s.strip()]
    convert(input_path, output_path, wanted)


if __name__ == "__main__":
    main()
