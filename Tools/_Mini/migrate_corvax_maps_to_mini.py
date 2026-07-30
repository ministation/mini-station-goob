#!/usr/bin/env python3
"""Copy CorvaxGoob station maps into Maps/_Mini and emit Mini* gameMap protos."""
from __future__ import annotations

import re
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
CORV_MAPS = ROOT / "Resources/Maps/_CorvaxGoob/Stations"
MINI_MAPS = ROOT / "Resources/Maps/_Mini"
CORV_PROTOS = ROOT / "Resources/Prototypes/_CorvaxGoob/Maps/Stations"
MINI_PROTOS = ROOT / "Resources/Prototypes/_Mini/Maps"

SKIP_DIRS = {"Silly", "CentComm", "Awesome"}
KEEP_MINI_MAPS = {"silly.yml", "aspid.yml"}
KEEP_PROTOS = {"silly.yml", "aspid.yml", "typan.yml"}


def main() -> None:
    for p in MINI_MAPS.glob("*.yml"):
        if p.name.lower() in KEEP_MINI_MAPS:
            print("keep", p.name)
            continue
        p.unlink()
        print("deleted", p.name)

    stations: list[tuple[str, str, str, str]] = []
    for d in sorted(CORV_MAPS.iterdir()):
        if not d.is_dir() or d.name in SKIP_DIRS:
            continue
        src = d / f"corvax_{d.name.lower()}.yml"
        if not src.exists():
            raise SystemExit(f"MISSING {src}")
        text = src.read_text(encoding="utf-8")
        m = re.search(r"- type: BecomesStation\n\s+id: (\S+)", text)
        old_id = m.group(1) if m else f"Corvax{d.name}"
        mini_id = f"Mini{d.name}"
        dest_name = f"{d.name.lower()}.yml"
        dest = MINI_MAPS / dest_name
        new_text = re.sub(
            r"(- type: BecomesStation\n\s+id: )\S+",
            rf"\g<1>{mini_id}",
            text,
            count=1,
        )
        if old_id != mini_id:
            new_text = re.sub(rf"\bid: {re.escape(old_id)}\b", f"id: {mini_id}", new_text)
        dest.write_text(new_text, encoding="utf-8", newline="\n")
        print(f"copied {src.name} -> {dest_name} ({old_id} -> {mini_id})")
        stations.append((d.name, old_id, mini_id, dest_name))

    MINI_PROTOS.mkdir(parents=True, exist_ok=True)
    for p in MINI_PROTOS.glob("*.yml"):
        if p.name.lower() in KEEP_PROTOS:
            print("keep proto", p.name)
            continue
        p.unlink()
        print("deleted proto", p.name)

    for name, _old_id, mini_id, dest_name in stations:
        corv_proto = CORV_PROTOS / f"corvax_{name.lower()}.yml"
        if not corv_proto.exists():
            raise SystemExit(f"MISSING proto {corv_proto}")
        text = corv_proto.read_text(encoding="utf-8")
        text = re.sub(r"(?m)^  id: \S+", f"  id: {mini_id}", text, count=1)
        text = re.sub(
            r"mapPath: /Maps/_CorvaxGoob/Stations/\S+",
            f"mapPath: /Maps/_Mini/{dest_name}",
            text,
            count=1,
        )
        text = re.sub(
            r"(?m)^(  stations:\n    )\S+:",
            rf"\g<1>{mini_id}:",
            text,
            count=1,
        )
        out = MINI_PROTOS / f"{name.lower()}.yml"
        out.write_text(text, encoding="utf-8", newline="\n")
        print(f"proto {out.name} id={mini_id}")

    print("DONE", len(stations))


if __name__ == "__main__":
    main()
